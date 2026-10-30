using System.Collections.Concurrent;
using System.Numerics;
using System.Net.Http.Json;
using System.Text.Json;
using Decimal.Web3;
using Decimal.Web3.Staking;
using Decimal.Web3.Tokens;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace DCWallet.Core;

public sealed record DataSet<T>(IReadOnlyList<T> Items, BigInteger Block, bool Complete, IReadOnlyList<string> Issues)
{
    public IReadOnlyList<string> CoverageNotes { get; init; } = Array.Empty<string>();
}
public sealed record TokenBalance(string Contract, string Name, string Symbol, int Decimals, BigInteger Raw)
{
    public string Amount => Units.Format(Raw, Decimals);
    public override string ToString() => Symbol + " · " + AmountDisplay.Compact(Raw, Decimals);
}
public sealed record NftBalance(string Contract, BigInteger TokenId, string Standard, string Collection, BigInteger Raw, string Uri);
public sealed record WalletStake(string Id, string Kind, string Validator, string ValidatorName, string Token, string Symbol, int? Decimals, BigInteger Raw, BigInteger TokenId, byte TokenType, BigInteger Hold, BigInteger Unfreeze, byte Status)
{
    public BigInteger? HoldStartTime { get; init; }
    public string Amount => Decimals is { } d ? Units.Format(Raw, d) : Raw + " (минимальные единицы)";
    public bool CanComplete => Kind != "active" && Status == 1 && Unfreeze > 0 && Unfreeze <= DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}

/// <summary>Only public discovery hints are cached. Balances and ownership are always queried on the selected chain.</summary>
public sealed class WalletDataReader : IDisposable
{
    private readonly DecimalClient client;
    private readonly HttpClient http;
    private readonly string? cacheDirectory;
    private string Graph => client.Network.ChainId == 75 ? "https://mainnet-thegraph.decimalchain.com/subgraphs/name/contract-center-2" : "https://testnet-thegraph.decimalchain.com/subgraphs/name/contract-center";
    public WalletDataReader(DecimalClient client, string? cacheDirectory = null, HttpMessageHandler? handler = null)
    {
        this.client = client; this.cacheDirectory = cacheDirectory;
        http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 3 * 1024 * 1024 };
    }
    public void Dispose() => http.Dispose();
    public async Task<JsonElement> JsonAsync(string url, object? body = null, CancellationToken ct = default)
    {
        using var response = body is null ? await http.GetAsync(url, ct) : await http.PostAsJsonAsync(url, body, ct);
        response.EnsureSuccessStatusCode(); var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length > 3 * 1024 * 1024) throw new InvalidDataException("Слишком большой ответ каталога.");
        using var doc = JsonDocument.Parse(bytes); return doc.RootElement.Clone();
    }
    internal static string Address(string? value)
    {
        if (value is null || value.Length != 42 || !value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || !value[2..].All(Uri.IsHexDigit) || value[2..].All(c => c == '0')) throw new InvalidDataException("Некорректный адрес каталога.");
        return value.ToLowerInvariant();
    }
    internal static string S(JsonElement row, string name, string fallback = "") => row.ValueKind == JsonValueKind.Object && row.TryGetProperty(name, out var v) ? v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : v.ValueKind == JsonValueKind.Number ? v.GetRawText() : fallback : fallback;
    internal static bool B(JsonElement row, string name) => row.ValueKind == JsonValueKind.Object && row.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
    internal static JsonElement P(JsonElement row, string name) => row.ValueKind == JsonValueKind.Object && row.TryGetProperty(name, out var v) ? v : default;
    internal static IEnumerable<JsonElement> Rows(JsonElement row, string name) => P(row, name).ValueKind == JsonValueKind.Array ? P(row, name).EnumerateArray().ToArray() : [];
    private static JsonElement[] RequiredRows(JsonElement row, string name)
    {
        var value = P(row, name);
        if (value.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Каталог не вернул обязательный список: " + name);
        return value.EnumerateArray().ToArray();
    }
    internal static BigInteger U(string value)
    {
        if (value.Length is < 1 or > 78 || !value.All(char.IsAsciiDigit) || !BigInteger.TryParse(value, out var n) || n < 0 || n >= BigInteger.One << 256) throw new InvalidDataException("Некорректное целое число каталога.");
        return n;
    }
    internal static string Clean(string value, int length = 100) => new(value.Where(c => !char.IsControl(c) && c is not (>= '\u202a' and <= '\u202e') && c is not (>= '\u2066' and <= '\u2069')).Take(length).ToArray());
    internal static void Identity(JsonElement row, string owner, long chain)
    {
        foreach (var key in new[] { "owner", "address" }) { var value = S(row, key); if (value.Length > 0 && Address(value) != owner) throw new InvalidDataException("Каталог вернул другой адрес."); }
        foreach (var key in new[] { "chainId", "chain_id" }) { var value = S(row, key); if (value.Length > 0 && value != chain.ToString()) throw new InvalidDataException("Каталог вернул другую сеть."); }
        var coverage = P(row, "coverage"); if (coverage.ValueKind == JsonValueKind.Object) Identity(coverage, owner, chain);
    }
    private async Task<JsonElement> GraphAsync(string query, object variables, CancellationToken ct)
    {
        var root = await JsonAsync(Graph, new { query, variables }, ct);
        if (root.TryGetProperty("errors", out _) || P(root, "data").ValueKind != JsonValueKind.Object) throw new InvalidDataException("Индексатор Decimal недоступен.");
        return root.GetProperty("data");
    }
    private async Task<BlockWithTransactionHashes> Snapshot(CancellationToken ct)
    {
        if ((await client.Web3.Eth.ChainId.SendRequestAsync().WaitAsync(ct)).Value != client.Network.ChainId) throw new InvalidOperationException("RPC вернул другую сеть.");
        return await client.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(BlockParameter.CreateLatest()).WaitAsync(ct);
    }
    private async Task CheckSnapshot(BlockWithTransactionHashes block, CancellationToken ct)
    {
        var current = await client.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(block.Number).WaitAsync(ct);
        if (current.BlockHash != block.BlockHash) throw new InvalidOperationException("Блок изменился во время загрузки. Обновите данные.");
    }
    public async Task<BigInteger> NativeBalanceAsync(string ownerInput, CancellationToken ct = default)
    {
        var owner = Address(ownerInput); var block = await Snapshot(ct);
        var value = await client.Web3.Eth.GetBalance.SendRequestAsync(owner, new BlockParameter(block.Number)).WaitAsync(ct);
        await CheckSnapshot(block, ct); return value.Value;
    }
    private string? CacheFile(string kind, string owner) => cacheDirectory is null ? null : Path.Combine(cacheDirectory, client.Network.ChainId + "-" + owner + "-" + kind + ".json");
    private List<T> Cached<T>(string kind, string owner)
    {
        try { var p = CacheFile(kind, owner); return p is not null && new FileInfo(p).Length < 3 * 1024 * 1024 ? JsonSerializer.Deserialize<List<T>>(File.ReadAllText(p), OperationCatalog.Json) ?? [] : []; } catch { return []; }
    }
    private void Cache<T>(string kind, string owner, IEnumerable<T> values)
    {
        var file = CacheFile(kind, owner); if (file is null) return;
        try { Directory.CreateDirectory(cacheDirectory!); File.WriteAllText(file, JsonSerializer.Serialize(values, OperationCatalog.Json)); } catch { /* Cache failure never hides a live balance. */ }
    }
    public async Task<DataSet<TokenBalance>> TokensAsync(string ownerInput, CancellationToken ct = default)
    {
        var owner = Address(ownerInput); var block = await Snapshot(ct); var hints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cached in Cached<string>("tokens", owner)) { try { hints.Add(Address(cached)); } catch { } }
        var issues = new List<string>(); var complete = false;
        try
        {
            var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var skip = 0; ; skip += 100)
            {
                ct.ThrowIfCancellationRequested();
                var data = await GraphAsync("query WalletTokens($owner:String!,$skip:Int!){_meta{block{number}} balances(first:100,skip:$skip,where:{user_:{address:$owner},amount_gt:\"0\"}){token{address}} balanceReservelesses(first:100,skip:$skip,where:{user_:{address:$owner},amount_gt:\"0\"}){token{address}}}", new { owner, skip }, ct);
                var a = RequiredRows(data, "balances"); var b = RequiredRows(data, "balanceReservelesses");
                var before = discovered.Count;
                foreach (var row in a.Concat(b)) { var address = Address(S(P(row, "token"), "address")); hints.Add(address); discovered.Add(address); }
                var indexed = U(S(P(P(data, "_meta"), "block"), "number"));
                if (a.Length < 100 && b.Length < 100) { complete = block.Number.Value >= indexed && block.Number.Value - indexed <= 100; break; }
                if (discovered.Count == before || skip >= 100_000) { issues.Add("Каталог токенов превысил предел обхода; загрузка неполная."); break; }
            }
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested) { issues.Add("Основной каталог токенов недоступен: " + e.Message); }
        if (client.Network.ChainId == 75)
        {
            try
            {
                var root = await JsonAsync($"https://console.mintcandy.ru/api/v1/wallets/{owner}/tokens?media=0", ct: ct); Identity(root, owner, 75);
                foreach (var row in Rows(root, "items")) if (S(row, "denom") != "del") hints.Add(Address(S(row, "contract")));
            }
            catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested) { issues.Add("Резервный каталог токенов недоступен."); }
            try
            {
                var last = new HashSet<string>();
                for (var offset = 0; ; offset += 100)
                {
                    var root = await JsonAsync($"https://explorer.mintcandy.ru/mc-api/v1/erc20/balances/{owner}?limit=100&offset={offset}&include_bank=0", ct: ct);
                    if (!B(root, "Ok")) throw new InvalidDataException(); var result = P(root, "Result"); Identity(result, owner, 75);
                    var rows = RequiredRows(result, "balances"); var before = last.Count;
                    foreach (var row in rows) if (!B(row, "native")) { var token = Address(S(row, "contract_address")); hints.Add(token); last.Add(token); }
                    var pagination = P(result, "pagination"); var total = U(S(pagination, "total", rows.Length.ToString()));
                    if (offset + rows.Length >= total || rows.Length < 100) break;
                    if (last.Count == before || offset >= 100000) { issues.Add("Резервный каталог токенов не завершил пагинацию."); break; }
                }
            }
            catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested) { issues.Add("Каталог токенов эксплорера недоступен."); }
        }
        Cache("tokens", owner, hints); var verified = new ConcurrentBag<TokenBalance>(); var failed = new ConcurrentBag<string>(); var at = new BlockParameter(block.Number);
        await Parallel.ForEachAsync(hints, new ParallelOptions { MaxDegreeOfParallelism = 5, CancellationToken = ct }, async (hint, token) =>
        {
            try
            {
                var service = new Erc20Service(client, hint); var raw = await service.GetBalanceRawAsync(owner, at).WaitAsync(token); var decimals = await service.GetDecimalsAsync(at).WaitAsync(token);
                if (decimals > 36) throw new InvalidDataException("Точность токена превышает поддерживаемую.");
                var symbol = await service.GetSymbolAsync(at).WaitAsync(token); var name = await service.GetNameAsync(at).WaitAsync(token);
                if (raw > 0) verified.Add(new(hint, Clean(name), Clean(symbol, 32), decimals, raw));
            }
            catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested) { failed.Add(hint); }
        });
        if (!failed.IsEmpty) issues.Add("Не удалось проверить токены: " + string.Join(", ", failed));
        if (!complete) issues.Add("Каталог может не включать все внешние токены. Найденные балансы проверены RPC.");
        await CheckSnapshot(block, ct); return new(verified.OrderBy(t => t.Symbol).ToArray(), block.Number.Value, complete && failed.IsEmpty, issues);
    }
    public sealed record NftHint(string Contract, BigInteger TokenId, string Standard, string Name);
    public async Task<DataSet<NftBalance>> NftsAsync(string ownerInput, CancellationToken ct = default)
    {
        var owner = Address(ownerInput); var block = await Snapshot(ct); var hints = Cached<NftHint>("nfts", owner).DistinctBy(h => h.Contract + ":" + h.TokenId).ToDictionary(h => h.Contract + ":" + h.TokenId); var issues = new List<string>(); var notes = new List<string>(); var complete = false;
        try
        {
            var meta = await GraphAsync("{_meta{block{number}}}", new { }, ct); var indexed = U(S(P(P(meta, "_meta"), "block"), "number")); var after = "";
            for (var pages = 0; pages < 1000; pages++)
            {
                var data = await GraphAsync("query WalletNfts($owner:Bytes!,$after:ID!,$block:Int!){nftbalances(first:100,orderBy:id,orderDirection:asc,where:{user_:{address:$owner},amount_gt:\"0\",id_gt:$after},block:{number:$block}){id user{address} collection{address name tokenType} nft{tokenId collection{address}}}}", new { owner, after, block = (int)indexed }, ct);
                var rows = RequiredRows(data, "nftbalances");
                foreach (var row in rows)
                {
                    if (Address(S(P(row, "user"), "address")) != owner) throw new InvalidDataException("Чужой владелец NFT.");
                    var collection = P(row, "collection"); var hint = new NftHint(Address(S(collection, "address")), U(S(P(row, "nft"), "tokenId")), S(collection, "tokenType"), Clean(S(collection, "name")));
                    hints[hint.Contract + ":" + hint.TokenId] = hint;
                }
                if (rows.Length < 100) { complete = block.Number.Value >= indexed && block.Number.Value - indexed <= 100; break; }
                if (pages == 999) throw new InvalidDataException("Каталог NFT превысил предел обхода.");
                var next = S(rows[^1], "id"); if (string.CompareOrdinal(next, after) <= 0 || next.Length == 0) throw new InvalidDataException("Повтор страницы NFT."); after = next;
            }
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested) { issues.Add("Основной каталог NFT недоступен."); }
        if (client.Network.ChainId == 75)
        {
            try
            {
                var offset = 0; var snapshot = ""; var seen = new HashSet<string>();
                while (true)
                {
                    var root = await JsonAsync($"https://explorer.mintcandy.ru/mc-api/v1/nfts/addresses/{owner}/tokens?limit=100&offset={offset}&media=0" + (snapshot.Length > 0 ? "&snapshot=" + Uri.EscapeDataString(snapshot) : ""), ct: ct);
                    if (!B(root, "Ok")) throw new InvalidDataException(); var data = P(root, "Result"); Identity(root, owner, 75); Identity(data, owner, 75);
                    var coverage = P(data, "coverage");
                    var unsupported = Rows(coverage, "unsupported_scope").Concat(Rows(data, "unsupported_scope")).Any();
                    if (unsupported) notes.Add("Автоматический поиск охватывает NFT, известные каталогам Decimal.");
                    if (B(data, "partial") || B(root, "partial"))
                    {
                        complete = false;
                        var reasons = Rows(coverage, "issues").Select(v => v.GetString() ?? "").ToArray();
                        var indexed = S(coverage, "index_block"); var head = S(coverage, "verified_block");
                        var normalLag = reasons.Length > 0 && reasons.All(v => v == "index_block_differs_from_head") &&
                            BigInteger.TryParse(indexed, out var indexedBlock) && BigInteger.TryParse(head, out var verifiedBlock) &&
                            indexedBlock <= verifiedBlock && verifiedBlock - indexedBlock <= 100;
                        if (!normalLag && (reasons.Length > 0 || !unsupported))
                            issues.Add("Каталог NFT пока не завершил обновление данных.");
                        else if (!unsupported) notes.Add("Некоторые внешние NFT могут отсутствовать в автоматическом поиске.");
                    }
                    if (unsupported) complete = false;
                    var rows = RequiredRows(data, "items");
                    foreach (var row in rows)
                    {
                        var hint = new NftHint(Address(S(row, "contract")), U(S(row, "token_id")), S(row, "standard"), Clean(S(row, "collection", S(row, "name", "NFT")))); hints[hint.Contract + ":" + hint.TokenId] = hint;
                    }
                    var next = P(data, "next_offset"); if (next.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) break;
                    var nextOffset = (int)U(next.ToString()); snapshot = S(data, "snapshot");
                    if (snapshot.Length > 128 || snapshot.Length == 0 || nextOffset <= offset || nextOffset > 100000 || !seen.Add(nextOffset + ":" + snapshot)) throw new InvalidDataException("Некорректная пагинация NFT."); offset = nextOffset;
                }
            }
            catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested) { complete = false; issues.Add("Резервный каталог NFT недоступен."); }
        }
        Cache("nfts", owner, hints.Values); var found = new ConcurrentBag<NftBalance>(); var failed = new ConcurrentBag<string>(); var at = new BlockParameter(block.Number);
        const string abi = """[{"type":"function","name":"ownerOf","inputs":[{"type":"uint256"}],"outputs":[{"type":"address"}]},{"type":"function","name":"balanceOf","inputs":[{"type":"address"},{"type":"uint256"}],"outputs":[{"type":"uint256"}]},{"type":"function","name":"tokenURI","inputs":[{"type":"uint256"}],"outputs":[{"type":"string"}]},{"type":"function","name":"uri","inputs":[{"type":"uint256"}],"outputs":[{"type":"string"}]}]""";
        await Parallel.ForEachAsync(hints.Values, new ParallelOptions { MaxDegreeOfParallelism = 5, CancellationToken = ct }, async (hint, token) =>
        {
            try
            {
                _ = Address(hint.Contract); var contract = client.Web3.Eth.GetContract(abi, hint.Contract); var standard = hint.Standard.ToUpperInvariant(); var erc721 = standard.Contains("721");
                if (!erc721 && !standard.Contains("1155")) throw new InvalidDataException("Неизвестный стандарт NFT.");
                var raw = erc721 ? Address(await contract.GetFunction("ownerOf").CallAsync<string>(at, hint.TokenId).WaitAsync(token)) == owner ? BigInteger.One : BigInteger.Zero : await contract.GetFunction("balanceOf").CallAsync<BigInteger>(at, owner, hint.TokenId).WaitAsync(token);
                if (raw == 0) return; string uri;
                try { uri = await contract.GetFunction(erc721 ? "tokenURI" : "uri").CallAsync<string>(at, hint.TokenId).WaitAsync(token); } catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested) { uri = ""; }
                found.Add(new(hint.Contract, hint.TokenId, erc721 ? "ERC721" : "ERC1155", hint.Name, raw, uri.Length <= 4096 ? uri : ""));
            }
            catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested) { failed.Add(hint.Contract + ":" + hint.TokenId); }
        });
        if (!complete && notes.Count == 0) notes.Add("Некоторые NFT могут отсутствовать в автоматическом поиске."); if (!failed.IsEmpty) issues.Add("Не удалось проверить NFT: " + string.Join(", ", failed));
        await CheckSnapshot(block, ct); return new(found.OrderBy(n => n.Collection).ThenBy(n => n.TokenId).ToArray(), block.Number.Value, complete && failed.IsEmpty, issues) { CoverageNotes = notes.Distinct().ToArray() };
    }
    public sealed record StakeHint(string Id, string Kind, string Validator, string Token, BigInteger Hold, BigInteger TokenId, string ValidatorName, bool Nft = false, string? QueueContract = null);
    private static readonly object NftLocatorGate = new();
    /// <summary>Remember only public coordinates, never amounts or ownership. Every subsequent display rechecks RPC.</summary>
    public bool RememberNftStake(string ownerInput,string validatorInput,string nftInput,BigInteger tokenId,BigInteger hold,string validatorName="")
    {
        if(client.Network.ChainId!=202020)throw new NotSupportedException("Combined NFT locators require the reviewed testnet profile.");
        var owner=Address(ownerInput);var validator=Address(validatorInput);var nft=Address(nftInput);_=U(tokenId.ToString());_=U(hold.ToString());
        var id="combined:"+validator+":"+nft+":"+tokenId+":"+hold;var hint=new StakeHint(id,"active",validator,nft,hold,tokenId,Clean(validatorName),true,CombinedNftDelegation.Proxy);
        var file=CacheFile("nft-stake-locators",owner);if(file is null)return false;
        lock(NftLocatorGate)
        {
            string? temporary=null;
            try
            {
                var hints=Cached<StakeHint>("nft-stake-locators",owner).Where(h=>h.Id!=id).Take(10000).Append(hint).ToArray();
                Directory.CreateDirectory(cacheDirectory!);temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";
                File.WriteAllText(temporary,JsonSerializer.Serialize(hints,OperationCatalog.Json));File.Move(temporary,file,true);return true;
            }
            catch(IOException){return false;}
            catch(UnauthorizedAccessException){return false;}
            finally{if(temporary is not null&&File.Exists(temporary))try{File.Delete(temporary);}catch(IOException){}}
        }
    }
    public static (BigInteger Index, string? Contract) ParseFrozenIdentity(string value)
    {
        if (value.All(char.IsAsciiDigit)) return (U(value), null);
        var parts = value.Split('#'); if (parts.Length != 2 || parts[0] is not ("WITHDRAW-STAKE" or "TRANSFER-STAKE")) throw new InvalidDataException("Некорректный идентификатор заявки.");
        var split = parts[1].Split('-'); if (split.Length != 2 || !split[1].StartsWith("0x") || split[1].Length is < 3 or > 66 || !split[1][2..].All(Uri.IsHexDigit)) throw new InvalidDataException("Некорректный номер заявки.");
        return (BigInteger.Parse("0" + split[1][2..], System.Globalization.NumberStyles.HexNumber), Address(split[0]));
    }
    public async Task<DataSet<WalletStake>> StakesAsync(string ownerInput, CancellationToken ct = default)
    {
        var owner = Address(ownerInput); var block = await Snapshot(ct); var hints = Cached<StakeHint>("stakes", owner).DistinctBy(h => h.Kind + ":" + h.Id).ToDictionary(h => h.Kind + ":" + h.Id); var issues = new List<string>(); var sourceIssues = new List<string>(); var notes = new List<string>(); var complete = false; var consoleFresh = false; var queueFresh = false;
        if(client.Network.ChainId==202020)foreach(var hint in Cached<StakeHint>("nft-stake-locators",owner))hints[hint.Kind+":"+hint.Id]=hint;
        if (client.Network.ChainId == 75)
        {
            try
            {
                var root = await JsonAsync($"https://console.mintcandy.ru/api/v1/wallets/{owner}/staking", ct: ct); Identity(root, owner, 75);
                foreach (var row in RequiredRows(root, "positions"))
                {
                    if (B(row, "verificationError")) continue;
                    var validator = Address(S(row, "validator")); var token = Address(S(row, "contract")); var hold = U(S(row, "holdTimestamp", "0")); var nft = S(row, "nftId"); var id = validator + ":" + token + ":" + hold + ":" + nft;
                    hints["active:" + id] = new(id, "active", validator, token, hold, nft.Length > 0 ? U(nft) : 0, Clean(S(row, "validatorName")), nft.Length > 0);
                }
                foreach (var kind in new[] { ("withdrawals", "withdraw"), ("transfers", "transfer") }) foreach (var row in RequiredRows(root, kind.Item1)) { var id = U(S(row, "id")).ToString(); hints[kind.Item2 + ":" + id] = new(id, kind.Item2, "", "", 0, 0, ""); }
                consoleFresh = BigInteger.TryParse(S(root, "verifiedBlock"), out var consoleBlock) && BigInteger.Abs(consoleBlock - block.Number.Value) <= 100 && P(root, "sourceStale").ValueKind == JsonValueKind.False && P(root, "requestsTruncated").ValueKind == JsonValueKind.False && P(root, "error").ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
            }
            catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested) { sourceIssues.Add("Каталог ставок консоли недоступен."); }
            try
            {
                var root = await JsonAsync($"https://explorer.mintcandy.ru/v1/wallets/{owner}/staking", ct: ct); Identity(root, owner, 75); var data = P(root, "Result");
                if (!B(root, "Ok")) throw new InvalidDataException();
                Identity(data, owner, 75);
                queueFresh = B(data, "withdrawals_authoritative") && B(data, "withdrawals_fresh") && BigInteger.TryParse(S(data, "withdrawals_checked_block"), out var queueBlock) && BigInteger.Abs(queueBlock - block.Number.Value) <= 100;
                if (!queueFresh) notes.Add("Резервный каталог разморозки обновляется; используется свежий индекс Decimal.");
                foreach (var row in RequiredRows(data, "withdrawals")) { var id = U(S(row, "id")).ToString(); hints["withdraw:" + id] = new(id, "withdraw", "", "", 0, 0, Clean(S(row, "validator_name"))); }
            }
            catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested) { sourceIssues.Add("Не удалось загрузить резервную очередь разморозки."); }
        }
        complete = consoleFresh && queueFresh;
        // The Graph also supplies hold/NFT identities and supports the selected testnet.
        try
        {
            var graphMeta = await GraphAsync("{_meta{block{number}}}", new { }, ct);
            var indexed = U(S(P(P(graphMeta, "_meta"), "block"), "number"));
            // The index can advance while discovery HTTP requests are in flight.
            if (indexed > block.Number.Value) block = await Snapshot(ct);
            foreach (var kind in new[] { ("stakes", "active"), ("withdrawStakes", "withdraw"), ("transferStakes", "transfer") })
            {
                var after = "";
                for (var pages = 0; pages < 1000; pages++)
                {
                    var query = "query Stakes($owner:Bytes!,$after:ID!,$block:Int!){" + kind.Item1 + "(first:100,orderBy:id,orderDirection:asc,where:{delegator:$owner,id_gt:$after},block:{number:$block}){id delegator " + (kind.Item2 == "transfer" ? "oldValidator" : "validator") + "{address} token{address} nft{collection{address}} tokenId holdTimestamp}}";
                    var data = await GraphAsync(query, new { owner, after, block = (int)indexed }, ct); var rows = RequiredRows(data, kind.Item1);
                    foreach (var row in rows)
                    {
                        if (Address(S(row, "delegator")) != owner) throw new InvalidDataException("Чужой стейк.");
                        var id = S(row, "id"); var nft = P(row, "nft").ValueKind == JsonValueKind.Object; string? queueContract = null;
                        if (kind.Item2 != "active") { var parsed = ParseFrozenIdentity(id); id = parsed.Index.ToString(); queueContract = parsed.Contract; }
                        var token = nft ? Address(S(P(P(row, "nft"), "collection"), "address")) : Address(S(P(row, "token"), "address"));
                        var key = kind.Item2 + ":" + id;
                        var hint = new StakeHint(id, kind.Item2, Address(S(P(row, kind.Item2 == "transfer" ? "oldValidator" : "validator"), "address")), token, U(S(row, "holdTimestamp", "0")), U(S(row, "tokenId", "0")), hints.TryGetValue(key, out var previous) ? previous.ValidatorName : "", nft, queueContract);
                        hints[kind.Item2 + ":" + id] = hint;
                    }
                    if (rows.Length < 100) break; if (pages == 999) throw new InvalidDataException("Каталог стейкинга превысил предел обхода."); var next = S(rows[^1], "id"); if (string.CompareOrdinal(next, after) <= 0 || next.Length == 0) throw new InvalidDataException("Повтор страницы стейкинга."); after = next;
                }
            }
            complete |= indexed <= block.Number.Value && block.Number.Value - indexed <= 100;
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested) { sourceIssues.Add("Индексатор стейкинга недоступен; используются резервные каталоги."); }
        if(client.Network.ChainId==202020)
        {
            try
            {
                var after="";
                for(var pages=0;pages<1000;pages++)
                {
                    var data=await GraphAsync("query WalletCombinedNftStakes($owner:Bytes!,$after:ID!){nftstakes(first:100,orderBy:id,orderDirection:asc,where:{delegator:$owner,isActive:true,id_gt:$after}){id delegator validator{address} nftContract{address} tokenId coinStake{holdTimestamp}}}",new{owner,after},ct);
                    var rows=RequiredRows(data,"nftstakes");
                    foreach(var row in rows)
                    {
                        if(Address(S(row,"delegator"))!=owner)throw new InvalidDataException("Чужой NFT-стейк.");
                        var id=S(row,"id");if(id.Length==0||id.Length>300)throw new InvalidDataException("Некорректный идентификатор NFT-стейка.");
                        hints["active:"+id]=new(id,"active",Address(S(P(row,"validator"),"address")),Address(S(P(row,"nftContract"),"address")),U(S(P(row,"coinStake"),"holdTimestamp","0")),U(S(row,"tokenId")),"",true,CombinedNftDelegation.Proxy);
                    }
                    if(rows.Length<100)break;var next=S(rows[^1],"id");if(string.CompareOrdinal(next,after)<=0||pages==999)throw new InvalidDataException("Каталог NFT-стейкинга не продвигает курсор.");after=next;
                }
            }
            catch(Exception e)when(e is not OperationCanceledException||!ct.IsCancellationRequested){complete=false;issues.Add("Каталог combined NFT-стейкинга недоступен; сохраненные позиции проверяются по RPC.");}
        }
        Cache("stakes", owner, hints.Values); var delegation = await client.ResolveSystemContractAsync("delegation").WaitAsync(ct); var wdel = await client.ResolveSystemContractAsync("wdel").WaitAsync(ct);
        var verified = new ConcurrentBag<WalletStake>(); var failures = new ConcurrentBag<string>(); var at = new BlockParameter(block.Number);
        await Parallel.ForEachAsync(hints.Values.DistinctBy(h => h.Kind == "active" ? h.Kind + ":" + h.Validator + ":" + h.Token + ":" + h.Hold + ":" + h.TokenId : h.Kind + ":" + h.Id), new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (hint, token) =>
        {
            try
            {
                DelegationStake state; BigInteger unfreeze = 0; byte status = 0;
                var combinedNft=hint.Nft&&client.Network.ChainId==202020;
                var contract = combinedNft ? CombinedNftDelegation.Proxy : hint.Nft ? await client.ResolveSystemContractAsync("delegation-nft").WaitAsync(token) : delegation;
                if (hint.Kind == "active")
                {
                    if(combinedNft)
                    {
                        var position=await CombinedNftDelegation.GetStakeAsync(client,hint.Token,hint.TokenId,owner,hint.Validator,hint.Hold,at,token);
                        if(position is null||!position.IsActive)return;
                        state=new(position.Validator,position.Delegator,position.Nft,position.Amount,position.TokenId,position.NftType,position.Hold);
                    }
                    else if (hint.Nft)
                    {
                        NftDelegationSupport.RequireSupported(client.Network.ChainId, contract);
                        var output = hint.Hold > 0
                            ? await StakeResponseDecoder.QueryAsync(client.Web3, contract, new GetNftHoldStake() { Validator = hint.Validator, Delegator = owner, Collection = hint.Token, TokenId = hint.TokenId, Hold = hint.Hold }, at).WaitAsync(token)
                            : await StakeResponseDecoder.QueryAsync(client.Web3, contract, new GetNftStake() { Validator = hint.Validator, Delegator = owner, Collection = hint.Token, TokenId = hint.TokenId }, at).WaitAsync(token);
                        state = output;
                        if (state.Exists && state.TokenId != hint.TokenId) throw new InvalidDataException("Номер NFT-стейка не совпал.");
                    }
                    else if (hint.Hold > 0 && hint.Token.Equals(wdel, StringComparison.OrdinalIgnoreCase))
                    {
                        var output = await StakeResponseDecoder.QueryAsync(client.Web3, contract, new GetNativeHoldStake() { Validator = hint.Validator, Delegator = owner, Hold = hint.Hold }, at).WaitAsync(token); state = output;
                    }
                    else state = hint.Hold > 0 ? await new StakeReader(client.Web3, contract).GetHoldStakeAsync(hint.Validator, owner, hint.Token, hint.Hold, at).WaitAsync(token) : await new StakeReader(client.Web3, contract).GetStakeAsync(hint.Validator, owner, hint.Token, at).WaitAsync(token);
                    if (!state.Validator.Equals(hint.Validator, StringComparison.OrdinalIgnoreCase) || !state.Token.Equals(hint.Token, StringComparison.OrdinalIgnoreCase) || state.HoldTimestamp != hint.Hold) { if (!state.Exists) return; throw new InvalidDataException("Идентификатор стейка не совпал."); }
                }
                else
                {
                    var parsed = ParseFrozenIdentity(hint.Id);
                    if ((hint.QueueContract ?? parsed.Contract) is { } expected && !expected.Equals(contract, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Каталог указал другой контракт очереди.");
                    var frozen = await new FrozenStakeReader(client.Web3, contract).GetFrozenStakeAsync(parsed.Index, at).WaitAsync(token);
                    if (frozen.FreezeType != (hint.Kind == "withdraw" ? 1 : 2)) throw new InvalidDataException("Тип замороженной заявки не совпадает с каталогом.");
                    state = frozen.Stake; unfreeze = frozen.UnfreezeTimestamp; status = frozen.FreezeStatus;
                    if (status != 1) return;
                }
                if (!state.Exists) return; if (!state.Delegator.Equals(owner, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Чужой владелец стейка.");
                var native = state.Token.Equals(wdel, StringComparison.OrdinalIgnoreCase) || state.TokenType == 4; int? decimals = native ? 18 : state.TokenType is 2 or 3 ? 0 : null; var symbol = native ? "DEL" : state.TokenType is 2 or 3 ? "NFT" : "Токен";
                if (decimals is null) { var service = new Erc20Service(client, state.Token); decimals = await service.GetDecimalsAsync(at).WaitAsync(token); if (decimals > 36) throw new InvalidDataException("Точность токена превышает поддерживаемую."); symbol = Clean(await service.GetSymbolAsync(at).WaitAsync(token), 32); }
                verified.Add(new(hint.Id, hint.Kind, state.Validator, hint.ValidatorName, state.Token, symbol, decimals, state.AmountRaw, state.TokenId, state.TokenType, state.HoldTimestamp, unfreeze, status) { HoldStartTime = state.HoldStartTime });
            }
            catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested) { failures.Add(hint.Id); }
        });
        if (complete) notes.AddRange(sourceIssues); else { issues.AddRange(sourceIssues); issues.Add("Не удалось полностью обновить стейки. Повторите загрузку."); }
        if (!failures.IsEmpty) issues.Add("Не удалось проверить позиции: " + string.Join(", ", failures));
        await CheckSnapshot(block, ct); return new(verified.OrderBy(s => s.Kind).ThenBy(s => s.Symbol).ToArray(), block.Number.Value, complete && failures.IsEmpty, issues) { CoverageNotes = notes.Distinct().ToArray() };
    }
}

[Nethereum.ABI.FunctionEncoding.Attributes.Function("getStake", typeof(GetStakeOutput))]
public class GetNftStake : Nethereum.Contracts.FunctionMessage
{
    [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("address", "validator", 1)] public string Validator { get; set; } = "";
    [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("address", "delegator", 2)] public string Delegator { get; set; } = "";
    [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("address", "nft", 3)] public string Collection { get; set; } = "";
    [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("uint256", "tokenId", 4)] public BigInteger TokenId { get; set; }
}
[Nethereum.ABI.FunctionEncoding.Attributes.Function("getHoldStake", typeof(GetStakeOutput))]
public sealed class GetNftHoldStake : GetNftStake
{
    [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("uint256", "holdTimestamp", 5)] public BigInteger Hold { get; set; }
}

[Nethereum.ABI.FunctionEncoding.Attributes.Function("getHoldStakeDEL", typeof(GetStakeOutput))]
public sealed class GetNativeHoldStake : Nethereum.Contracts.FunctionMessage
{
    [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("address", "validator", 1)] public string Validator { get; set; } = "";
    [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("address", "delegator", 2)] public string Delegator { get; set; } = "";
    [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("uint256", "holdTimestamp", 3)] public BigInteger Hold { get; set; }
}
