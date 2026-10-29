using System.Net;
using System.Text;
using DCWallet.Core;
using Decimal.Web3;
using Xunit;
namespace DCWallet.Tests;
public sealed class AssetCatalogTests
{
    const string Address="0x2222222222222222222222222222222222222222";
    [Fact] public async Task SearchFindsUnownedContractAndEscapesQuery()
    {
        var handler=new Fixture("{\"items\":[{\"contract\":\""+Address+"\",\"symbol\":\"NEW\",\"title\":\"Network token\",\"canTrade\":true}]}");
        using var reader=new AssetCatalogReader(DecimalNetwork.Mainnet(),handler);
        var result=await reader.SearchTokensAsync("NEW & X");
        Assert.Equal(Address,Assert.Single(result.Items).Address);Assert.True(result.Complete);Assert.Contains("NEW%20%26%20X",handler.Url);
    }
    [Fact] public async Task TestnetNeverUsesMainnetCatalog()
    {
        var handler=new Fixture("{}");using var reader=new AssetCatalogReader(DecimalNetwork.Testnet(),handler);
        await Assert.ThrowsAsync<NotSupportedException>(()=>reader.SearchTokensAsync("new")); Assert.Null(handler.Url);
    }
    [Fact] public async Task CappedValidatorsAreMarkedIncompleteAndOnlyFetchedOnce()
    {
        var entries=Enumerable.Range(1,120).Select(i=>new {address="0x"+i.ToString("x").PadLeft(40,'0'),name="Validator "+i});
        var handler=new Fixture(System.Text.Json.JsonSerializer.Serialize(new {items=entries})); using var reader=new AssetCatalogReader(DecimalNetwork.Mainnet(),handler);
        var result=await reader.ValidatorsAsync();Assert.Equal(120,result.Items.Count);Assert.False(result.Complete);Assert.NotNull(result.Notice);Assert.Equal(1,handler.Calls);
    }
    [Fact] public async Task InvalidAddressesAreRejected()
    {
        using var reader=new AssetCatalogReader(DecimalNetwork.Mainnet(),new Fixture("{\"items\":[{\"address\":\"not-address\"}]}"));
        await Assert.ThrowsAsync<InvalidDataException>(()=>reader.ValidatorsAsync());
    }
    [Fact] public async Task CancellationPreventsRequest()
    {
        var handler=new Fixture("{}");using var reader=new AssetCatalogReader(DecimalNetwork.Mainnet(),handler);using var ct=new CancellationTokenSource();ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>reader.SearchTokensAsync("new",ct.Token));Assert.Null(handler.Url);
    }
    [Fact] public async Task MissingArrayIsNotAnEmptySuccess()
    {
        using var reader=new AssetCatalogReader(DecimalNetwork.Mainnet(),new Fixture("{}"));await Assert.ThrowsAsync<InvalidDataException>(()=>reader.SearchTokensAsync("new"));
    }
    sealed class Fixture(string body):HttpMessageHandler
    {
        public string? Url;public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {Calls++;Url=request.RequestUri!.AbsoluteUri;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body,Encoding.UTF8,"application/json")});}
    }
}
