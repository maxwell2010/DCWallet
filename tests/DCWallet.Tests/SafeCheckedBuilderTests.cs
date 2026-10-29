using System.Numerics;
using System.Text.Json;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Multisig;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
namespace DCWallet.Tests;
public class SafeCheckedBuilderTests
{
    static string Uint(BigInteger n)=>"0x"+n.ToString("x").TrimStart('0').PadLeft(64,'0');
    static string Selector(string n)=>"0x"+Sha3Keccack.Current.CalculateHash(n)[..8];
    [Theory]
    [InlineData(false,false,true)]
    [InlineData(true,false,false)]
    [InlineData(false,true,false)]
    public async Task CheckedSafeExecutionUsesPinnedNonceAndCurrentOwners(bool staleNonce,bool foreignOwner,bool success)
    {
        const string safe="0x2222222222222222222222222222222222222222", to="0x3333333333333333333333333333333333333333";
        var key=new EthECKey("0000000000000000000000000000000000000000000000000000000000000001"); var owner=key.GetPublicAddress();
        var tx=SafeTransactionDrafts.BuildDel(safe,to,1,0); var signatures=new[]{SafeTransactions.SignTypedData(tx,75,key)};
        using var rpc=new FakeRpc { ReadResult=(name,p)=>{
            if(name=="eth_chainId") return "0x4b"; if(name=="eth_blockNumber") return "0x64";
            var data=p[0].GetProperty("data").GetString(); Assert.Equal("0x64",p[1].GetString());
            if(data==Selector("nonce()")) return Uint(staleNonce?1:0);
            if(data==Selector("getThreshold()")) return Uint(1);
            if(data==Selector("getOwners()")) return Uint(32)+Uint(1)[2..]+(foreignOwner?to:owner)[2..].ToLowerInvariant().PadLeft(64,'0');
            throw new Exception("Unexpected Safe read");
        }};
        var client=new DecimalClient(DecimalNetwork.Mainnet() with{RpcUrl=new Uri(rpc.Url)});
        var op=OperationCatalog.All.Single(o=>o.Method.DeclaringType==typeof(SafeQueries)&&o.Method.Name=="BuildExecutionDraftAsync");
        var json=JsonSerializer.Serialize(new Dictionary<string,object?>{["$safeAddress"]=safe,["executor"]=owner,["transaction"]=tx,["chainId"]=new BigInteger(75),["signatures"]=signatures},OperationCatalog.Json);
        if(success){var draft=await OperationCatalog.BuildAsync(op,json,client,owner); Assert.Equal(safe,draft.To); Assert.Equal(BigInteger.Zero,draft.ValueWei); Assert.Equal(SafeTransactions.BuildExecutionDraft(owner,tx,75,signatures).Data,draft.Data);}
        else await Assert.ThrowsAsync<InvalidOperationException>(()=>OperationCatalog.BuildAsync(op,json,client,owner));
        Assert.Equal(0,rpc.Broadcasts);
    }
}
