using System.Numerics;
using DCWallet.Core;
using Decimal.Web3;
using Nethereum.Util;
using Xunit;
namespace DCWallet.Tests;
public class CheckedBuilderTests
{
    const string Owner="0x1111111111111111111111111111111111111111", Contract="0x2222222222222222222222222222222222222222";
    static string Uint(BigInteger n)=>"0x"+n.ToString("x").TrimStart('0').PadLeft(64,'0');
    static string Text(string t)=>Uint(32)+Uint(t.Length)[2..]+Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(t)).PadRight(64,'0');
    static string Selector(string name)=>"0x"+Sha3Keccack.Current.CalculateHash(name)[..8];
    [Theory]
    [InlineData("BuildMintCheckedAsync",true,true,1000,10,true)]
    [InlineData("BuildMintCheckedAsync",false,true,1000,10,false)]
    [InlineData("BuildMintCheckedAsync",true,true,10,10,false)]
    [InlineData("BuildBurnCheckedAsync",true,true,1000,10,true)]
    [InlineData("BuildBurnCheckedAsync",true,false,1000,10,false)]
    [InlineData("BuildBurnCheckedAsync",true,true,1000,0,false)]
    public async Task CheckedBuildersRespectRpcPermissionsCapAndBalance(string method,bool mintable,bool burnable,int cap,int balance,bool success)
    {
        using var rpc=new FakeRpc { ReadResult=(name,p)=> {
            if(name=="eth_chainId") return "0x4b";
            var data=p[0].GetProperty("data").GetString()!;
            if(data==Selector("name()")||data==Selector("symbol()")||data==Selector("identity()")) return Text("FIXTURE");
            if(data==Selector("owner()")) return "0x"+Owner[2..].PadLeft(64,'0');
            if(data==Selector("mintable()")) return Uint(mintable?1:0);
            if(data==Selector("burnable()")) return Uint(burnable?1:0);
            if(data==Selector("cap()")) return Uint(cap);
            if(data==Selector("totalSupply()")) return Uint(10);
            if(data==Selector("decimals()")) return Uint(18);
            if(data.StartsWith(Selector("balanceOf(address)"))) return Uint(balance);
            throw new Exception("Unexpected RPC call "+data);
        }};
        var client=new DecimalClient(DecimalNetwork.Mainnet() with { RpcUrl=new Uri(rpc.Url) });
        var op=OperationCatalog.All.Single(o=>o.Method.Name==method);
        var args=new Dictionary<string,object?> { ["$tokenAddress"]=Contract,["from"]=Owner,["recipient"]=Owner,["amountRaw"]=new BigInteger(1),["block"]=null };
        var json=System.Text.Json.JsonSerializer.Serialize(args,OperationCatalog.Json);
        if(success) {
            var draft=await OperationCatalog.BuildAsync(op,json,client,Owner);
            Assert.Equal(Contract,draft.To); Assert.Equal(BigInteger.Zero,draft.ValueWei);
            Assert.StartsWith(Selector(method.Contains("Mint")?"mint(address,uint256)":"burn(uint256)"),draft.Data);
            Assert.EndsWith(new string('0',63)+"1",draft.Data);
        } else await Assert.ThrowsAsync<InvalidOperationException>(()=>OperationCatalog.BuildAsync(op,json,client,Owner));
        Assert.Equal(0,rpc.Broadcasts);
    }
}
