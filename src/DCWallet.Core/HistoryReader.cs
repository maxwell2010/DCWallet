using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Decimal.Web3;
using Decimal.Web3.Tokens;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using static DCWallet.Core.WalletDataReader;

namespace DCWallet.Core;
public sealed record AssetMovement(string Contract, string Symbol, int? Decimals, BigInteger Raw, string From, string To, string Standard, BigInteger? TokenId = null)
{
    public string Amount => Decimals is { } decimals ? Units.Format(Raw, decimals) : Raw + " (минимальные единицы)";
}
public sealed record HistoryRow(string Hash, BigInteger Block, DateTimeOffset Timestamp, string From, string? To, BigInteger ValueWei, BigInteger FeeWei, bool Success, string Kind, IReadOnlyList<AssetMovement>? Movements = null, string Memo = "", string Verification = "rpc", bool? InnerSuccess = null)
{
    public string VerificationReason { get; init; } = "";
}
public sealed record HistoryPage(IReadOnlyList<HistoryRow> Rows, int NextOffset, int Failed, bool MayHaveMore, int Total = 0, bool Complete = false, IReadOnlyList<string>? Issues = null)
{
    public int Verified => Rows.Count(r => r.Verification == "rpc");
    public int Archived => Rows.Count(r => r.Verification.StartsWith("indexer", StringComparison.Ordinal));
    public IReadOnlyList<HistoryVerificationDetails> VerificationDetails { get; init; } = Array.Empty<HistoryVerificationDetails>();
}
public sealed partial class HistoryReader(DecimalClient client, bool archiveFallback = true, Func<Task<HistoryReader>>? archiveReaderFactory = null, Func<DecimalClient, WalletDataReader>? catalogReaderFactory = null)
{
    private volatile bool preferArchive;
    private volatile HistoryReader? archiveReader;
    private readonly SemaphoreSlim archiveGate = new(1, 1);
    private async Task<HistoryReader> ArchiveAsync(CancellationToken ct)
    {
        if (archiveReader is not null) return archiveReader;
        await archiveGate.WaitAsync(ct);
        try
        {
            if (archiveReader is not null) return archiveReader;
            if (archiveReaderFactory is not null) return archiveReader = await archiveReaderFactory().WaitAsync(ct);
            var archiveClient = new DecimalClient(client.Network with { RpcUrl = new Uri("https://node4-mainnet.decimalchain.com/web3/") });
            if ((await archiveClient.Web3.Eth.ChainId.SendRequestAsync().WaitAsync(ct)).Value != client.Network.ChainId) throw new InvalidOperationException("Архивный RPC вернул другую сеть.");
            return archiveReader = new HistoryReader(archiveClient, false);
        }
        finally { archiveGate.Release(); }
    }
    private static readonly string Transfer = Topic("Transfer(address,address,uint256)"), Single = Topic("TransferSingle(address,address,address,uint256,uint256)"), Batch = Topic("TransferBatch(address,address,address,uint256[],uint256[])");
    private readonly ConcurrentDictionary<string, Task<(string, int?)>> metadata = new();
    [GeneratedRegex("^0x[0-9a-fA-F]{64}$")] private static partial Regex HashPattern();
    private static string Topic(string signature) => "0x" + Sha3Keccack.Current.CalculateHash(signature);
    public async Task<HistoryPage> ReadAsync(string owner, int offset = 0) => await ReadPageAsync(owner, offset, default);
    private async Task<HistoryPage> ReadPageAsync(string ownerInput, int offset, CancellationToken ct)
    {
        var owner = Address(ownerInput); if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (client.Network.ChainId is not (75 or 202020)) throw new InvalidOperationException("Каталог истории доступен для сетей Decimal mainnet и testnet.");
        if ((await client.Web3.Eth.ChainId.SendRequestAsync().WaitAsync(ct)).Value != client.Network.ChainId) throw new InvalidOperationException("RPC вернул другую сеть.");
        using var http = catalogReaderFactory?.Invoke(client) ?? new WalletDataReader(client);
        var api = client.Network.ChainId == 75 ? "https://api.decimalchain.com" : "https://testnet-api.decimalchain.com";
        var root = await http.JsonAsync($"{api}/api/v1/txs/txs-by-address/{owner}?offset={offset}&limit=50", ct: ct);
        if (!B(root, "Ok") || P(P(root, "Result"), "txs").ValueKind != JsonValueKind.Array) throw new InvalidDataException("Неизвестный формат истории.");
        var result = P(root, "Result"); var candidates = Rows(result, "txs").ToArray(); if (candidates.Length > 500) throw new InvalidDataException("Слишком большая страница истории.");
        var count = U(S(result, "count", (offset + candidates.Length).ToString()));
        if (count > 1_000_000) throw new InvalidDataException("Слишком большой каталог истории.");
        var total = (int)count; var rows = new ConcurrentBag<HistoryRow>(); var details = new ConcurrentBag<HistoryVerificationDetails>(); var failed = 0;
        await Parallel.ForEachAsync(candidates, new ParallelOptions { MaxDegreeOfParallelism = 3, CancellationToken = ct }, async (candidate, token) =>
        {
            try
            {
                var evm = S(candidate, "evm_hash");
                if (evm.Length == 0) { rows.Add(ParseLegacy(candidate)); return; }
                if (!evm.StartsWith("0x")) evm = "0x" + evm;
                var verification = await VerifyDetailedAsync(evm, owner, token, true);
                details.Add(verification.Details);
                if (verification.Row is { } row) rows.Add(row);
                else
                {
                    // Read the archived row before incrementing errors, so a malformed row is counted once.
                    var archived = ParseIndexed(candidate, evm) with { VerificationReason = verification.Details.Unavailable ? "archive_unavailable" : "verification_failed" };
                    rows.Add(archived);
                    if (!verification.Details.Unavailable) Interlocked.Increment(ref failed);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested) { Interlocked.Increment(ref failed); }
        });
        return new(rows.OrderByDescending(r => r.Block).ThenByDescending(r => r.Timestamp).ToArray(), offset + candidates.Length, failed, offset + candidates.Length < total && candidates.Length > 0, total, offset + candidates.Length >= total,
            rows.Any(r => r.Verification != "rpc") ? new[] { "Есть архивные записи без RPC-подтверждения." } : []) { VerificationDetails = details.OrderBy(d => d.Hash).ToArray() };
    }
    public async Task<HistoryPage> ReadAllAsync(string owner, IProgress<HistoryPage>? progress = null, CancellationToken ct = default)
    {
        var rows = new Dictionary<string, HistoryRow>(StringComparer.OrdinalIgnoreCase); var offset = 0; var failures = 0; var total = 0; var issues = new HashSet<string>(); var complete = false; var verificationDetails = new Dictionary<string, HistoryVerificationDetails>(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            var page = await ReadPageAsync(owner, offset, ct); var before = rows.Count;
            foreach (var row in page.Rows) rows[row.Hash] = row; failures += page.Failed; total = Math.Max(total, page.Total);
            foreach (var issue in page.Issues ?? []) issues.Add(issue);
            foreach (var detail in page.VerificationDetails) verificationDetails[detail.Hash] = detail;
            var snapshot = new HistoryPage(rows.Values.OrderByDescending(r => r.Block).ThenByDescending(r => r.Timestamp).ToArray(), page.NextOffset, failures, page.MayHaveMore, total, false, issues.ToArray()) { VerificationDetails = verificationDetails.Values.ToArray() }; progress?.Report(snapshot);
            if (!page.MayHaveMore) { complete = page.NextOffset >= total; offset = page.NextOffset; break; }
            if (page.NextOffset <= offset || before == rows.Count || page.NextOffset > 1_000_000) { issues.Add("Индексатор не продвигает курсор. История загружена не полностью."); break; }
            offset = page.NextOffset;
        }
        if (complete && rows.Count < total)
        {
            complete = false;
            issues.Add("Каталог просмотрен, но часть записей повреждена или повторяется. История загружена не полностью.");
        }
        if (client.Network.ChainId == 202020)
        {
            complete = false; issues.Add("Официальный каталог testnet отстает от сети: новые операции могут отсутствовать. Дополните историю проверкой локального журнала или известного хэша.");
        }
        return new(rows.Values.OrderByDescending(r => r.Block).ThenByDescending(r => r.Timestamp).ToArray(), offset, failures, !complete, total, complete, issues.ToArray()) { VerificationDetails = verificationDetails.Values.ToArray() };
    }
    private static HistoryRow ParseLegacy(JsonElement row)
    {
        var hash = S(row, "hash"); if (hash.Length != 64 || !hash.All(Uri.IsHexDigit)) throw new InvalidDataException("Некорректный исторический хэш.");
        var coin = S(row, "coin"); var amount = U(S(row, "amount", "0")); var type = Clean(S(row, "method", S(row, "type", "Историческая операция")));
        var from = Clean(S(row, "sender")); var to = Clean(S(row, "recipient"));
        return new(hash, U(S(row, "block")), DateTimeOffset.Parse(S(row, "timestamp"), CultureInfo.InvariantCulture), from, to,
            coin.Equals("del", StringComparison.OrdinalIgnoreCase) ? amount : 0, U(S(P(row, "fee"), "gas_amount", "0")), S(row, "status").Equals("Success", StringComparison.OrdinalIgnoreCase), type,
            amount > 0 ? new[] { new AssetMovement("", Clean(coin, 32), coin.Equals("del", StringComparison.OrdinalIgnoreCase) ? 18 : null, amount, from, to, "legacy") } : [], Verification: "indexer-legacy");
    }
    private static HistoryRow ParseIndexed(JsonElement row, string hash)
    {
        if (!HashPattern().IsMatch(hash)) throw new InvalidDataException("Некорректный хэш архива.");
        var from = Clean(S(row, "sender")); var to = Clean(S(row, "recipient")); var movements = new List<AssetMovement>();
        foreach (var move in Rows(row, "transfer"))
        {
            try
            {
                var contract = S(move, "token"); var native = contract.Equals("del", StringComparison.OrdinalIgnoreCase); if (!native) contract = Address(contract);
                var decimalsText = S(move, "decimals"); int? decimals = native ? 18 : int.TryParse(decimalsText, out var d) && d is >= 0 and <= 36 ? d : null;
                movements.Add(new(native ? "" : contract, Clean(S(move, "symbol", native ? "DEL" : "Токен"), 32), decimals, U(S(move, "amount")), Clean(S(move, "address_from")), Clean(S(move, "address_to")), "indexer"));
            }
            catch { /* Preserve the transaction even if an archived event cannot be decoded. */ }
        }
        var coin = S(row, "coin"); var value = coin.Equals("del", StringComparison.OrdinalIgnoreCase) ? U(S(row, "amount", "0")) : 0;
        if (movements.Count == 0 && value > 0) movements.Add(new("", "DEL", 18, value, from, to, "indexer"));
        return new(hash, U(S(row, "block")), DateTimeOffset.Parse(S(row, "timestamp"), CultureInfo.InvariantCulture), from, to, value, U(S(P(row, "fee"), "gas_amount", "0")), S(row, "status").Equals("Success", StringComparison.OrdinalIgnoreCase), Clean(S(row, "method", "Операция архива")), movements, Verification: "indexer-evm");
    }
    public Task<HistoryRow?> VerifyAsync(string hash, string owner, CancellationToken ct = default, bool allowIndexerLink = false)
        => VerifyCoreAsync(hash, owner, ct, allowIndexerLink, null);
    private async Task<HistoryRow?> VerifyCoreAsync(string hash, string owner, CancellationToken ct, bool allowIndexerLink, Action<HistoryVerificationAttempt>? observe)
    {
        var canArchive = archiveFallback && client.Network.ChainId == 75 && (archiveReaderFactory is not null || client.Network.RpcUrl.Host.EndsWith(".decimalchain.com", StringComparison.OrdinalIgnoreCase));
        async Task<HistoryRow?> Primary() => await VerifyPrimaryAsync(hash, owner, ct, allowIndexerLink, code => observe?.Invoke(new("primary", code)));
        async Task<HistoryRow?> Archived() => await (await ArchiveAsync(ct)).VerifyPrimaryAsync(hash, owner, ct, allowIndexerLink, code => observe?.Invoke(new("archive", code)));
        if (!canArchive)
        {
            var primary = await Primary(); if (primary is not null) observe?.Invoke(new("primary", "verified")); return primary;
        }
        // An archive can lag recent blocks. A previous success must never disable the primary RPC.
        foreach (var isArchive in preferArchive ? new[] { true, false } : new[] { false, true })
        {
            try
            {
                var row = isArchive ? await Archived() : await Primary();
                if (row is not null) { observe?.Invoke(new(isArchive ? "archive" : "primary", "verified")); preferArchive = isArchive; return row; }
            }
            catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested) { observe?.Invoke(new(isArchive ? "archive" : "primary", VerificationErrorCode(e))); }
        }
        return null;
    }
    private async Task<HistoryRow?> VerifyPrimaryAsync(string hash, string owner, CancellationToken ct, bool allowIndexerLink, Action<string>? rejected = null)
    {
        owner = Address(owner); if (!HashPattern().IsMatch(hash)) throw new ArgumentException("Неверный хэш.");
        if ((await client.Web3.Eth.ChainId.SendRequestAsync().WaitAsync(ct)).Value != client.Network.ChainId) throw new InvalidOperationException("RPC вернул другую сеть.");
        var txTask = client.Web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(hash); var receiptTask = client.Web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(hash); await Task.WhenAll(txTask, receiptTask).WaitAsync(ct);
        HistoryRow? Reject(string code) { rejected?.Invoke(code); return null; }
        var tx = txTask.Result; var receipt = receiptTask.Result;
        if (tx is null && receipt is null) return Reject("transaction_and_receipt_missing");
        if (tx is null) return Reject("transaction_missing");
        if (receipt is null) return Reject("receipt_missing");
        if (receipt.BlockNumber is null || receipt.Status is null) return Reject("receipt_incomplete");
        if (tx.BlockHash != receipt.BlockHash || tx.BlockNumber?.Value != receipt.BlockNumber.Value || !hash.Equals(tx.TransactionHash, StringComparison.OrdinalIgnoreCase) || !hash.Equals(receipt.TransactionHash, StringComparison.OrdinalIgnoreCase)) return Reject("transaction_receipt_mismatch");
        if (receipt.Status.Value != 0 && receipt.Status.Value != 1 || !string.Equals(receipt.From, tx.From, StringComparison.OrdinalIgnoreCase) || !string.Equals(receipt.To, tx.To, StringComparison.OrdinalIgnoreCase)) return Reject("receipt_identity_mismatch");
        var block = await client.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(receipt.BlockNumber).WaitAsync(ct);
        if (block is null) return Reject("block_missing");
        if (block.BlockHash != receipt.BlockHash || block.Number.Value != receipt.BlockNumber.Value || block.Timestamp.Value < 0 || block.Timestamp.Value > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60) return Reject("block_mismatch");
        var direct = owner.Equals(tx.From, StringComparison.OrdinalIgnoreCase) || owner.Equals(tx.To, StringComparison.OrdinalIgnoreCase); var moves = new List<AssetMovement>();
        if (receipt.Status.Value == 1)
        {
            if (direct && tx.Value.Value > 0) moves.Add(new("", "DEL", 18, tx.Value.Value, tx.From, tx.To ?? "", "native"));
            foreach (var log in receipt.Logs ?? [])
            {
                try
                {
                    var topics = log.Topics?.Select(t => t?.ToString() ?? "").ToArray() ?? []; var address = Address(log.Address);
                    if (topics.Length < 3) continue; var signature = topics[0].ToLowerInvariant(); string from, to;
                    if (signature == Transfer)
                    {
                        from = TopicAddress(topics[1]); to = TopicAddress(topics[2]); if (from != owner && to != owner) continue;
                        if (topics.Length == 4) moves.Add(new(address, "NFT", 0, 1, from, to, "ERC721", Hex(topics[3])));
                        else if (topics.Length == 3 && log.Data.Length == 66)
                        {
                            var meta = await metadata.GetOrAdd(address + ":" + receipt.BlockNumber.HexValue, _ => Metadata(address, new BlockParameter(receipt.BlockNumber))).WaitAsync(ct);
                            moves.Add(new(address, meta.Item1, meta.Item2, Hex(log.Data), from, to, "ERC20"));
                        }
                    }
                    else if (topics.Length == 4 && (signature == Single || signature == Batch))
                    {
                        from = TopicAddress(topics[2]); to = TopicAddress(topics[3]); if (from != owner && to != owner) continue;
                        foreach (var pair in Decode1155(log.Data, signature == Batch)) moves.Add(new(address, "NFT", 0, pair.Amount, from, to, "ERC1155", pair.Id));
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested) { /* Unknown event data is never guessed as a transfer. */ }
            }
        }
        if (!direct && moves.Count == 0 && !allowIndexerLink) return Reject("address_relation_unverified");
        var memo = "";
        if (tx.Input is { Length: > 2 and < 2050 } && (tx.To is null || !tx.Input.StartsWith("0xa9059cbb", StringComparison.OrdinalIgnoreCase)))
        {
            try { var text = new UTF8Encoding(false, true).GetString(Convert.FromHexString(tx.Input[2..])); if (text.All(c => !char.IsControl(c) || c is '\n' or '\r' or '\t')) memo = Clean(text, 500); } catch { }
        }
        var kind = receipt.Status.Value == 0 ? "Ошибка исполнения" : moves.Any(m => m.Standard is "ERC721" or "ERC1155") ? "Операция NFT" : moves.Any(m => m.Standard == "ERC20") ? "Перевод / операция токена" : tx.Input is null or "0x" ? "Перевод DEL" : memo.Length > 0 ? "Перевод с сообщением" : "Вызов контракта";
        if (receipt.Status.Value == 1 && memo.Length == 0) kind = HistoryCallClassifier.Classify(tx.Input, kind);
        var innerSuccess = HistorySafeOutcome.Read(tx.Input, tx.To, receipt);
        if (receipt.Status.Value == 1 && HistorySafeOutcome.IsExecution(tx.Input))
            kind = innerSuccess == false ? "Safe: внутреннее исполнение отклонено" : innerSuccess == true ? "Safe: внутреннее исполнение подтверждено" : "Safe: внутренний результат не подтвержден";
        return new(hash, receipt.BlockNumber.Value, DateTimeOffset.FromUnixTimeSeconds((long)block.Timestamp.Value), tx.From, tx.To, direct ? tx.Value.Value : 0, receipt.GasUsed.Value * (receipt.EffectiveGasPrice?.Value ?? tx.GasPrice.Value), receipt.Status.Value == 1, kind, moves, memo, direct || moves.Count > 0 ? "rpc" : "rpc-receipt-indexer-link", innerSuccess);
    }
    private async Task<(string, int?)> Metadata(string contract, BlockParameter at)
    {
        try { var service = new Erc20Service(client, contract); var decimals = await service.GetDecimalsAsync(at); var symbol = Clean(await service.GetSymbolAsync(at), 32); return (symbol, decimals <= 36 ? decimals : null); }
        catch { return (contract[..10], null); }
    }
    private static string TopicAddress(string text)
    {
        if (!HashPattern().IsMatch(text) || text.Substring(2, 24).Any(c => c != '0')) throw new InvalidDataException(); return "0x" + text[26..].ToLowerInvariant();
    }
    private static BigInteger Hex(string value) => BigInteger.Parse("0" + (value.StartsWith("0x") ? value[2..] : value), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
    public static IReadOnlyList<(BigInteger Id, BigInteger Amount)> Decode1155(string data, bool batch)
    {
        if (!data.StartsWith("0x") || data.Length > 131074 || data[2..].Length % 64 != 0 || !data[2..].All(Uri.IsHexDigit)) throw new InvalidDataException("Некорректное событие NFT.");
        var body = data[2..]; BigInteger Word(int index) => index < 0 || (index + 1) * 64 > body.Length ? throw new InvalidDataException() : Hex(body.Substring(index * 64, 64));
        if (!batch) { if (body.Length != 128) throw new InvalidDataException(); return new[] { (Word(0), Word(1)) }; }
        var idsOffset = Word(0); var amountsOffset = Word(1); if (idsOffset < 64 || amountsOffset < 64 || idsOffset % 32 != 0 || amountsOffset % 32 != 0 || idsOffset > body.Length / 2 || amountsOffset > body.Length / 2) throw new InvalidDataException();
        var ids = (int)(idsOffset / 32); var amounts = (int)(amountsOffset / 32); var count = Word(ids);
        if (count < 0 || count > 512 || count != Word(amounts)) throw new InvalidDataException();
        // Dynamic arrays must be disjoint and entirely inside the event data.
        var idsEnd = ids + 1 + (int)count; var amountsEnd = amounts + 1 + (int)count;
        if (idsEnd > body.Length / 64 || amountsEnd > body.Length / 64 || ids < amountsEnd && amounts < idsEnd) throw new InvalidDataException();
        var output = new List<(BigInteger, BigInteger)>();
        for (var i = 0; i < (int)count; i++) output.Add((Word(ids + 1 + i), Word(amounts + 1 + i))); return output;
    }
    public static string Csv(IEnumerable<HistoryRow> rows)
    {
        static string Cell(string text) { if (text.Length > 0 && "=+-@\t\r\n".Contains(text[0])) text = "'" + text; return "\"" + text.Replace("\"", "\"\"") + "\""; }
        var b = new StringBuilder("time,block,hash,status,kind,from,to,asset,contract,token_id,amount,amount_raw,fee_del,verification,inner_status,verification_reason\r\n");
        foreach (var row in rows) foreach (var move in row.Movements is { Count: > 0 } m ? m : new[] { new AssetMovement("", "DEL", 18, row.ValueWei, row.From, row.To ?? "", "native") })
            b.AppendLine(string.Join(',', new[] { row.Timestamp.ToString("O"), row.Block.ToString(), row.Hash, row.Success ? "success" : "failed", row.Kind, move.From, move.To, move.Symbol, move.Contract, move.TokenId?.ToString() ?? "", move.Amount, move.Raw.ToString(), Units.Format(row.FeeWei), row.Verification, row.InnerSuccess is true ? "success" : row.InnerSuccess is false ? "failed" : "", row.VerificationReason }.Select(Cell)));
        return b.ToString();
    }
}
