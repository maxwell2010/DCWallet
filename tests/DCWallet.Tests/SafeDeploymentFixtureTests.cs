using System.Text.Json;
using Nethereum.Hex.HexTypes;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Nethereum.RPC.Eth.DTOs;
using Xunit;
namespace DCWallet.Tests;
public class SafeDeploymentFixtureTests
{
    [Fact] public void OnlyTwoReviewedInitRuntimePairsAreAccepted()
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"SafeQaFixtures","SafeQa.compiled.json")));
        foreach(var contract in fixture.RootElement.GetProperty("contracts").EnumerateObject())
        {
            var init=contract.Value.GetProperty("creationBytecode").GetString()!;var runtime=contract.Value.GetProperty("runtimeSha256").GetString()!;
            AdvancedTestContext.ValidateDeploymentFixture(init,runtime);
            Assert.Throws<InvalidOperationException>(()=>AdvancedTestContext.ValidateDeploymentFixture(init,new string('0',64)));
            Assert.Throws<InvalidOperationException>(()=>AdvancedTestContext.ValidateDeploymentFixture(init+"00",runtime));
        }
        Assert.Throws<InvalidOperationException>(()=>AdvancedTestContext.ValidateDeploymentFixture("0x6000",SafeAdvancedNetworkScenario.OwnerRuntimeSha256));
    }
    [Fact] public void OfflineNativeCreationSignerAcceptsEmptyDestination()
    {
        var account=new Account(EthECKey.GenerateKey().GetPrivateKey());
        var raw=new AccountOfflineTransactionSigner().SignTransaction(account,new TransactionInput { From=account.Address,To=null,Data="0x60006000f3",Value=new HexBigInteger(0),Nonce=new HexBigInteger(0),Gas=new HexBigInteger(100000),GasPrice=new HexBigInteger(1)},202020);
        Assert.NotEmpty(raw);
        var hex=raw.StartsWith("0x")?raw[2..]:raw;
        var decoded=(Nethereum.RLP.RLPCollection)Nethereum.RLP.RLP.Decode(Convert.FromHexString(hex));
        Assert.True(decoded[3].RLPData is null || decoded[3].RLPData.Length == 0);
    }
}
