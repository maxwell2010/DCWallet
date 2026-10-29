using System.Globalization;
using System.Text.Json;
using Decimal.Web3;
namespace DCWallet.Core;

// Catalog entries are discovery hints only. Callers must verify selected contracts through their selected RPC before preparing transactions.
public sealed record CatalogToken(string Address, string Name, string Symbol, bool CanTrade);
public sealed record CatalogValidator(string Address, string Name);
public sealed record CatalogResult<T>(IReadOnlyList<T> Items, bool Complete, string? Notice = null);
public sealed class AssetCatalogReader : IDisposable
{
    readonly DecimalNetwork network;
    readonly HttpClient http;
    public AssetCatalogReader(DecimalNetwork network, HttpMessageHandler? handler = null)
    {
        this.network = network;
        http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(12), MaxResponseContentBufferSize = 2_000_000 };
    }
    public void Dispose() => http.Dispose();
    void Supported()
    {
        if(network.ChainId != 75) throw new NotSupportedException("Каталог MintCandy доступен для основной сети. В выбранной сети введите контракт вручную.");
    }
    static string Query(string query)
    {
        var value = query.Trim();
        if(value.Length > 96 || value.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) == UnicodeCategory.Format)) throw new ArgumentException("Некорректная строка поиска.",nameof(query));
        return value;
    }
    static string Text(string text,int max=120) => new(text.Where(c=> !char.IsControl(c) && char.GetUnicodeCategory(c)!=UnicodeCategory.Format).Take(max).ToArray());
    async Task<JsonElement[]> Read(string path,CancellationToken ct)
    {
        Supported(); ct.ThrowIfCancellationRequested();
        using var response=await http.GetAsync("https://console.mintcandy.ru/api/v1/"+path,ct);
        response.EnsureSuccessStatusCode();
        using var doc=JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(ct));
        var root=doc.RootElement;
        foreach(var key in new[]{"chainId","chain_id"}) if(root.TryGetProperty(key,out var chain) && chain.ToString()!="75") throw new InvalidDataException("Каталог вернул другую сеть.");
        if(!root.TryGetProperty("items",out var rows) || rows.ValueKind!=JsonValueKind.Array || rows.GetArrayLength()>120) throw new InvalidDataException("Некорректный ответ каталога.");
        return rows.EnumerateArray().Select(row=>row.Clone()).ToArray();
    }
    public async Task<CatalogResult<CatalogToken>> SearchTokensAsync(string query,CancellationToken ct=default)
    {
        Supported(); ct.ThrowIfCancellationRequested(); var needle=Query(query);
        if(needle.Length==0) return new([],true);
        var rows=await Read("tokens/search?q="+Uri.EscapeDataString(needle)+"&limit=10",ct);
        if(rows.Length>10)throw new InvalidDataException("Каталог превысил лимит результатов.");
        var result=new List<CatalogToken>();
        foreach(var row in rows)
        {
            var symbol=Text(WalletDataReader.S(row,"symbol",WalletDataReader.S(row,"denom")),96);
            var address=WalletDataReader.S(row,"contract");
            // DEL is represented by an empty contract, never as a fabricated ERC20 address.
            if(address.Length==0 && !symbol.Equals("DEL",StringComparison.OrdinalIgnoreCase)) continue;
            if(address.Length>0) address=WalletDataReader.Address(address);
            result.Add(new(address,Text(WalletDataReader.S(row,"title",WalletDataReader.S(row,"name"))),symbol,WalletDataReader.B(row,"canTrade")));
        }
        return new(result.DistinctBy(t=>t.Address,StringComparer.OrdinalIgnoreCase).ToArray(),rows.Length<10,rows.Length==10?"Показаны первые 10 совпадений. Уточните название.":null);
    }
    public async Task<CatalogResult<CatalogValidator>> ValidatorsAsync(string query="",CancellationToken ct=default)
    {
        var needle=Query(query);
        // Console exposes a bounded list, without offset support. Do not loop identical pages or claim an exhaustive list when capped.
        var rows=await Read("validators?limit=120",ct);
        var result=rows.Select(r=>new CatalogValidator(WalletDataReader.Address(WalletDataReader.S(r,"address")),Text(WalletDataReader.S(r,"name"),100)))
            .DistinctBy(v=>v.Address,StringComparer.OrdinalIgnoreCase).Where(v=>v.Address.Contains(needle,StringComparison.OrdinalIgnoreCase)||v.Name.Contains(needle,StringComparison.OrdinalIgnoreCase)).OrderBy(v=>v.Name).ToArray();
        return new(result,rows.Length<120,rows.Length==120?"Каталог ограничен 120 валидаторами. Другой адрес можно ввести вручную.":null);
    }
}
