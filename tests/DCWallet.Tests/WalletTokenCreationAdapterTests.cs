using System.Numerics;
using System.Text.Json;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.TokenCreation;
using Xunit;
namespace DCWallet.Tests;
public sealed class WalletTokenCreationAdapterTests
{
    const string Owner="0x1111111111111111111111111111111111111111",Center="0x3333333333333333333333333333333333333333";
    static Operation Operation(string name)=>OperationCatalog.All.Single(o=>o.Method.DeclaringType==typeof(TokenCreationDrafts)&&o.Method.Name==name);
    [Fact] public async Task ExplicitReservePlusFreshCommissionReplacesOldHardcodedDefault()
    {
        using var rpc=new FakeRpc{ReadResult=(method,parameters)=>method switch{"eth_chainId"=>"0x4b","eth_getCode"=>"0x6000","eth_call"=>"0x"+Units.Parse("0.25").ToString("x").PadLeft(64,'0'),"eth_estimateGas"=>"0x5208","eth_gasPrice"=>"0x1","eth_getBalance"=>"0xde0b6b3a7640000",_=>"0x0"}};
        var client=new DecimalClient(DecimalNetwork.Mainnet() with{RpcUrl=new Uri(rpc.Url)});var original=new TokenCreationDrafts(Center).BuildCreateTokenDraft(Owner,"name","symbol",1000,1,9999,50,"identity");Assert.Equal(Units.Parse("3500"),original.ValueWei);
        var json=JsonSerializer.Serialize(new{symbol="symbol",reserveValueWei=Units.Parse("0.001").ToString()});var result=await WalletTokenCreationAdapter.AdaptAsync(Operation("BuildCreateTokenDraft"),json,client,Owner,original);Assert.Equal(Units.Parse("0.251"),result.ValueWei);Assert.Equal(original.Data,result.Data);Assert.Equal(0,rpc.Broadcasts);
    }
    [Fact] public async Task MissingReserveAndNonMintableNonzeroCapAreRejectedBeforeRpc()
    {
        var client=new DecimalClient(DecimalNetwork.Mainnet());var draft=new TokenCreationDrafts(Center).BuildCreateTokenDraft(Owner,"name","symbol",1000,1,9999,50,"identity");await Assert.ThrowsAsync<ArgumentException>(()=>WalletTokenCreationAdapter.AdaptAsync(Operation("BuildCreateTokenDraft"),"{}",client,Owner,draft));await Assert.ThrowsAsync<ArgumentException>(()=>WalletTokenCreationAdapter.AdaptAsync(Operation("BuildCreateReservelessTokenDraft"),"{\"mintable\":false,\"capRaw\":\"1\"}",client,Owner,draft));
    }
}
