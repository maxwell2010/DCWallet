using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Decimal.Web3;
using DCWallet.Core;
using Xunit;
namespace DCWallet.Tests;
public sealed class DiscoveryTests
{
    const string Owner = "0x1111111111111111111111111111111111111111";
    const string Token = "0x2222222222222222222222222222222222222222";
    [Theory]
    [InlineData("WITHDRAW-STAKE#0xa16c34ed1c0601c0e749e17ebef19752a15faa01-0x684b", "26699")]
    [InlineData("TRANSFER-STAKE#0xa16c34ed1c0601c0e749e17ebef19752a15faa01-0xff", "255")]
    [InlineData("0", "0")]
    public void FrozenIdentityPreservesExactIndex(string input, string expected)
        => Assert.Equal(BigInteger.Parse(expected), WalletDataReader.ParseFrozenIdentity(input).Index);
    [Theory]
    [InlineData("")][InlineData("-1")][InlineData("WITHDRAW-STAKE#bad-0x01")][InlineData("TRANSFER-STAKE#0xa16c34ed1c0601c0e749e17ebef19752a15faa01-0xgg")]
    public void FrozenIdentityRejectsInvalidInput(string input) => Assert.Throws<InvalidDataException>(() => WalletDataReader.ParseFrozenIdentity(input));
    [Fact]
    public async Task TokensVerifyExactBalanceAndPageUntilEnd()
    {
        using var rpc = new DiscoveryRpc(); var handler = new DiscoveryCatalogHandler(); using var reader = Reader(rpc,handler);
        var result = await reader.TokensAsync(Owner);
        Assert.True(result.Complete); var item = Assert.Single(result.Items); Assert.Equal("1.234567", item.Amount); Assert.Equal("TEST",item.Symbol);
        Assert.Equal(2,handler.TokenPages); Assert.Equal(0,rpc.Broadcasts);
    }
    [Fact]
    public async Task MalformedGraphCannotClaimCompleteEmptyPortfolio()
    {
        using var rpc = new DiscoveryRpc(); using var reader = Reader(rpc,new DiscoveryCatalogHandler { MissingArrays = true });
        var result = await reader.TokensAsync(Owner); Assert.False(result.Complete); Assert.NotEmpty(result.Issues);
    }
    [Fact]
    public async Task NftOwnershipIsVerifiedAndPartialCoveragePreserved()
    {
        using var rpc = new DiscoveryRpc(); using var reader = Reader(rpc,new DiscoveryCatalogHandler { Partial = true });
        var result = await reader.NftsAsync(Owner); Assert.Empty(result.Items); Assert.False(result.Complete); Assert.Contains(result.Issues,x=>x.Contains("не завершил"));
    }
    [Fact]
    public async Task NftVerifiedOwnerIsIncluded()
    {
        using var rpc = new DiscoveryRpc { NftOwner = Owner }; using var reader = Reader(rpc,new DiscoveryCatalogHandler());
        var result = await reader.NftsAsync(Owner); var nft = Assert.Single(result.Items);
        Assert.Equal(new BigInteger(7), nft.TokenId); Assert.Equal(BigInteger.One,nft.Raw); Assert.Equal("ERC721",nft.Standard);
        Assert.Equal(0,rpc.Broadcasts);
    }
    [Fact]
    public async Task CancellationDoesNotReturnSuccessfulEmptyPortfolio()
    {
        using var rpc = new DiscoveryRpc(); using var reader = Reader(rpc,new DiscoveryCatalogHandler());
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>reader.NftsAsync(Owner,cts.Token));
    }
    [Fact]
    public async Task ReorgRejectsEntireSnapshot()
    {
        using var rpc = new DiscoveryRpc { Reorg = true }; using var reader = Reader(rpc,new DiscoveryCatalogHandler());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>reader.TokensAsync(Owner));
    }
    [Fact]
    public async Task WrongChainStopsBeforeDiscovery()
    {
        using var rpc = new DiscoveryRpc { Chain = "0x1" }; var handler = new DiscoveryCatalogHandler(); using var reader = Reader(rpc,handler);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>reader.TokensAsync(Owner)); Assert.Equal(0,handler.TokenPages);
    }
    [Fact]
    public async Task TransportTimeoutPreservesCachedTokenAndReturnsPartialIssues()
    {
        using var rpc = new DiscoveryRpc(); var directory = Path.Combine(Path.GetTempPath(), "dc-discovery-timeout-" + Guid.NewGuid()); Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "75-" + Owner + "-tokens.json"), JsonSerializer.Serialize(new[] {Token}));
            using var reader = new WalletDataReader(new DecimalClient(DecimalNetwork.Mainnet() with { RpcUrl = new Uri(rpc.Url) }), directory, new TimedOutCatalog());
            var result = await reader.TokensAsync(Owner); Assert.False(result.Complete); Assert.NotEmpty(result.Issues);
            Assert.Equal("1.234567", Assert.Single(result.Items).Amount); Assert.Equal(0,rpc.Broadcasts);
        }
        finally { Directory.Delete(directory,true); }
    }
    [Fact]
    public async Task TestnetCatalogTimeoutProducesIncompleteDataSetsRatherThanUncaughtCancellation()
    {
        using var rpc = new DiscoveryRpc { Chain = "0x31524" };
        using var reader = new WalletDataReader(new DecimalClient(DecimalNetwork.Testnet() with { RpcUrl = new Uri(rpc.Url) }), handler:new TimedOutCatalog());
        var tokens = await reader.TokensAsync(Owner); var nfts = await reader.NftsAsync(Owner); var stakes = await reader.StakesAsync(Owner);
        Assert.False(tokens.Complete); Assert.NotEmpty(tokens.Issues); Assert.False(nfts.Complete); Assert.NotEmpty(nfts.Issues); Assert.False(stakes.Complete); Assert.NotEmpty(stakes.Issues);
    }
    [Fact]
    public async Task CallerCancellationDuringCatalogReadStillPropagates()
    {
        using var rpc = new DiscoveryRpc(); var handler = new BlockingCatalog();
        using var reader = new WalletDataReader(new DecimalClient(DecimalNetwork.Mainnet() with { RpcUrl = new Uri(rpc.Url) }), handler:handler);
        using var ct = new CancellationTokenSource(); var read = reader.TokensAsync(Owner,ct.Token);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2)); ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>read);
    }
    [Fact]
    public async Task NormalNftIndexLagAndScopeAreNotesRatherThanOfflineErrors()
    {
        using var rpc = new DiscoveryRpc(); using var reader = Reader(rpc,new DiscoveryCatalogHandler { NormalNftCoverage = true });
        var result = await reader.NftsAsync(Owner);
        Assert.False(result.Complete); Assert.Empty(result.Issues); Assert.NotEmpty(result.CoverageNotes); Assert.Equal(0,rpc.Broadcasts);
    }
    [Fact]
    public async Task FreshCompleteGraphReplacesStaleOptionalQueue()
    {
        using var rpc = new DiscoveryRpc(); using var reader = Reader(rpc,new DiscoveryCatalogHandler { StaleQueue = true });
        var result = await reader.StakesAsync(Owner);
        Assert.True(result.Complete); Assert.Empty(result.Issues); Assert.NotEmpty(result.CoverageNotes); Assert.Equal(0,rpc.Broadcasts);
    }
    [Fact]
    public async Task StaleQueueWithoutGraphNeverClaimsCompleteEmptyStaking()
    {
        using var rpc = new DiscoveryRpc(); using var reader = Reader(rpc,new DiscoveryCatalogHandler { StaleQueue = true, GraphUnavailable = true });
        var result = await reader.StakesAsync(Owner);
        Assert.False(result.Complete); Assert.NotEmpty(result.Issues); Assert.Equal(0,rpc.Broadcasts);
    }
    [Fact]
    public async Task LargeNftIndexLagRemainsAnActualDataIssue()
    {
        using var rpc = new DiscoveryRpc(); using var reader = Reader(rpc,new DiscoveryCatalogHandler { NormalNftCoverage = true, NftVerifiedBlock = 1000 });
        var result = await reader.NftsAsync(Owner);
        Assert.False(result.Complete); Assert.NotEmpty(result.Issues);
    }
    [Fact]
    public async Task OldGraphAndQueueCannotClaimFreshStaking()
    {
        using var rpc = new DiscoveryRpc { BlockNumberHex = "0x1000" }; using var reader = Reader(rpc,new DiscoveryCatalogHandler { StaleQueue = true });
        var result = await reader.StakesAsync(Owner);
        Assert.False(result.Complete); Assert.NotEmpty(result.Issues);
    }
    [Fact]
    public async Task AdvancingHeadDoesNotMakeFreshStakingLookUnavailable()
    {
        using var rpc = new DiscoveryRpc { AdvanceHead = true }; using var reader = Reader(rpc,new DiscoveryCatalogHandler { IndexBlock = 101, StaleQueue = true });
        var result = await reader.StakesAsync(Owner);
        Assert.True(result.Complete); Assert.Empty(result.Issues); Assert.Equal(new BigInteger(101), result.Block); Assert.Equal(0,rpc.Broadcasts);
    }
    sealed class TimedOutCatalog:HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) => throw new TaskCanceledException("fixture transport timeout");
    }
    sealed class BlockingCatalog:HttpMessageHandler
    {
        public TaskCompletionSource Started {get;} = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        { Started.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan,ct); throw new InvalidOperationException(); }
    }
    static WalletDataReader Reader(DiscoveryRpc rpc, DiscoveryCatalogHandler handler) => new(new DecimalClient(DecimalNetwork.Mainnet() with { RpcUrl = new Uri(rpc.Url) }),handler:handler);
}
