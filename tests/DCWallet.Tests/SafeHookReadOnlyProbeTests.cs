using Decimal.Web3;
using Xunit;
namespace DCWallet.Tests;
public class SafeHookReadOnlyProbeTests
{
    [DeploymentRecoveryFact] public async Task ExistingQaOwnerReturnsRejectedMagicForZeroSignature()
    {
        var client=new DecimalClient(DecimalNetwork.Testnet()with{RpcUrl=new Uri("https://202020.rpc.thirdweb.com")});
        const string abi="""[{"type":"function","name":"isValidSignature","inputs":[{"type":"bytes32"},{"type":"bytes"}],"outputs":[{"type":"bytes4"}]}]""";
        var function=client.Web3.Eth.GetContract(abi,"0x9ee0b592cc8d8451b15c42a2c0e48dca39a4da24").GetFunction("isValidSignature");
        var output=await AdvancedTestContext.ReadOnlyRetryAsync(()=>function.CallAsync<byte[]>(new byte[32],new byte[65]));
        Assert.Equal("ffffffff",Convert.ToHexString(output).ToLowerInvariant());
    }
}
