using DCWallet.Core;
using Decimal.Web3;
using Xunit;
namespace DCWallet.Tests;
public sealed class NftPauseGuardTests
{
    const string Contract = "0x1111111111111111111111111111111111111111";
    [Theory][InlineData("0",false)][InlineData("1",true)]
    public async Task ReadsActualPauseAndPreventsUnneededPreparation(string word,bool blocked)
    {
        using var rpc = new FakeRpc { ReadResult = (method, _) => method == "eth_chainId" ? "0x4b" : "0x" + word.PadLeft(64,'0') };
        var client = new DecimalClient(DecimalNetwork.Mainnet() with { RpcUrl = new Uri(rpc.Url) });
        if (blocked) await Assert.ThrowsAsync<InvalidOperationException>(() => NftDelegationSupport.RequireDelegationEnabledAsync(client,Contract));
        else await NftDelegationSupport.RequireDelegationEnabledAsync(client,Contract);
        Assert.Equal(0,rpc.Broadcasts);
    }
    [Theory][InlineData("0x")][InlineData("0x0000000000000000000000000000000000000000000000000000000000000002")]
    public async Task MissingGetterOrInvalidBoolCannotMasqueradeAsEnabled(string response)
    {
        using var rpc = new FakeRpc { ReadResult = (method, _) => method == "eth_chainId" ? "0x4b" : response };
        var client = new DecimalClient(DecimalNetwork.Mainnet() with { RpcUrl = new Uri(rpc.Url) });
        await Assert.ThrowsAsync<InvalidDataException>(() => NftDelegationSupport.RequireDelegationEnabledAsync(client,Contract));
        Assert.Equal(0,rpc.Broadcasts);
    }
}
