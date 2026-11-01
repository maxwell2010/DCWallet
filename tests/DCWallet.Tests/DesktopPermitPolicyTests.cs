using DCWallet.Core;
using Decimal.Web3;
using Xunit;
namespace DCWallet.Tests;
public class DesktopPermitPolicyTests
{
    [Fact] public async Task MainnetPermitIsRejectedBeforeAnyRpc()
    {
        // An unreachable local endpoint ensures a network call cannot pass unnoticed.
        var client=new DecimalClient(DecimalNetwork.Mainnet() with { RpcUrl=new Uri("http://127.0.0.1:1/") });
        var error=await Assert.ThrowsAsync<NotSupportedException>(()=>DesktopPermitPolicy.ValidateAsync(client,"0x"+new string('1',40),"0x"+new string('2',40),"0x"+new string('3',40),1));
        Assert.Contains("Testnet",error.Message);
    }
}
