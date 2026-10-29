using System.Numerics;
using System.Reflection;
using System.Text.Json;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Checks;
using Decimal.Web3.Multisig;
using Decimal.Web3.Transactions;
using Nethereum.Signer;
using Xunit;
namespace DCWallet.Tests;
public class CatalogEncodingTests
{
    const string Owner="0x1111111111111111111111111111111111111111", Address="0x2222222222222222222222222222222222222222";
    static byte[] Word() { var b=new byte[32]; b[^1]=1; return b; }
    static object? Sample(Type t,string name)
    {
        if(t==typeof(string)) return name switch { "from" or "sender" or "owner"=>Owner, "tokenOut"=>"0x3333333333333333333333333333333333333333", "data"=>"0x0102", "symbol"=>"FIXTURE", "name"=>"Fixture Token", "meta"=>"{\"name\":\"fixture\"}", "identity" or "memo"=>"fixture", "tokenUri" or "contractUri"=>"ipfs://fixture", _ when name.Contains("amount",StringComparison.OrdinalIgnoreCase)=>"1", _=>Address };
        if(t==typeof(BigInteger)||t==typeof(BigInteger?)) return new BigInteger(1);
        if(t==typeof(bool)) return true;
        if(t==typeof(byte)) return (byte)(name=="v"?27:name=="crr"?50:1);
        if(t==typeof(byte[])) { if(name=="signature") return Word().Concat(Word()).Append((byte)27).ToArray(); return Word(); }
        if(t.IsPrimitive) return Convert.ChangeType(name=="decimals"?18:1,t);
        if(t.IsArray) { var a=Array.CreateInstance(t.GetElementType()!,1); a.SetValue(Sample(t.GetElementType()!,name=="signatures"?"signature":"check"),0); return a; }
        if(t.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(t)) { var type=t.GetGenericArguments()[0]; var list=(System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type))!; list.Add(Sample(type,name=="signatures"?"signature":name)); return list; }
        if(t==typeof(ChecksPermit)) return new ChecksPermit(2000000000,27,Word(),Word());
        if(t==typeof(Decimal.Web3.Multisend.SafeMultisendDeployment)) return Decimal.Web3.Multisend.SafeMultisendDeployment.Mainnet;
        if(t==typeof(Decimal.Web3.Multisend.Erc20PermitSignature)) return new Decimal.Web3.Multisend.Erc20PermitSignature(2000000000,27,Word(),Word());
        if(t==typeof(Decimal.Web3.Multisend.Erc20Recipient)) return new Decimal.Web3.Multisend.Erc20Recipient(Address,1);
        if(t==typeof(Decimal.Web3.Multisend.DelRecipient)) return new Decimal.Web3.Multisend.DelRecipient(Address,"1");
        if(t==typeof(Nethereum.RPC.Eth.DTOs.BlockParameter)) return null;
        if(t==typeof(Decimal.Web3.Validators.ValidatorMeta)) { var instance=Activator.CreateInstance(t)!; foreach(var p in t.GetProperties().Where(p=>p.CanWrite)) p.SetValue(instance,Sample(p.PropertyType,p.Name.ToLowerInvariant())); return instance; }
        throw new Exception("No fixture for "+t+" "+name);
    }
    public static IEnumerable<object[]> Cases()=>OperationCatalog.All.Where(o=>o.Method.ReturnType==typeof(TransactionDraft)).Select(o=>new object[]{o.Id});
    [Theory,MemberData(nameof(Cases))]
    public async Task EveryOfflineCatalogBuilderMatchesBaselineOrExplicitUnsupportedRejection(string id)
    {
        var op=OperationCatalog.All.Single(o=>o.Id==id); var client=new DecimalClient(); var args=new Dictionary<string,object?>();
        foreach(var p in op.Constructor?.GetParameters()??[]) if(p.ParameterType!=typeof(DecimalNetwork)&&p.ParameterType!=typeof(DecimalClient)&&p.ParameterType!=typeof(Nethereum.Web3.Web3)) args["$"+p.Name]=Sample(p.ParameterType,p.Name!);
        foreach(var p in op.Method.GetParameters()) args[p.Name!]=SampleSpecial(op,p);
        if(op.Method.DeclaringType==typeof(Decimal.Web3.TokenEconomy.TokenEconomyDrafts)&&op.Method.Name=="BuildMintDraft")
        {
            await Assert.ThrowsAsync<NotSupportedException>(()=>OperationCatalog.BuildAsync(op,JsonSerializer.Serialize(args,OperationCatalog.Json),client,Owner)); return;
        }
        var draft=await OperationCatalog.BuildAsync(op,JsonSerializer.Serialize(args,OperationCatalog.Json),client,Owner);
        Assert.Equal(Owner,draft.From); Assert.True(draft.ValueWei>=0); Assert.True(draft.Data.Length>=2); Assert.StartsWith("0x",draft.Data);
        var root=Environment.GetEnvironmentVariable("DC_CATALOG_FIXTURES")??Path.Combine(AppContext.BaseDirectory,"CatalogFixtures");
        var path=Path.Combine(root,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(id)))+".json");
        var fixture=new DraftFixture(id,JsonSerializer.Serialize(args,OperationCatalog.Json),draft.To,draft.ValueWei.ToString(),draft.Data);
        if(Environment.GetEnvironmentVariable("DC_GENERATE_FIXTURES")=="1") { Directory.CreateDirectory(root); await File.WriteAllTextAsync(path,JsonSerializer.Serialize(fixture,new JsonSerializerOptions{WriteIndented=true})); }
        var expected=JsonSerializer.Deserialize<DraftFixture>(await File.ReadAllTextAsync(path))!;
        // JSON indentation uses platform line endings; calldata and values stay byte-exact.
        Assert.Equal(id,expected.Id); Assert.Equal(fixture.Input.ReplaceLineEndings("\n"),expected.Input.ReplaceLineEndings("\n")); Assert.Equal(expected.Target,draft.To); Assert.Equal(expected.Value,draft.ValueWei.ToString()); Assert.Equal(expected.Calldata,draft.Data);
    }
    static object? SampleSpecial(Operation op,ParameterInfo p)
    {
        if(op.Method.DeclaringType==typeof(SafeTransactions))
        {
            var tx=SafeTransactionDrafts.BuildDel(Address,Owner,1,0);
            if(p.ParameterType==typeof(SafeTransactionDraft)) return tx;
            if(p.Name=="chainId") return new BigInteger(75);
            if(p.Name=="signatures") return new[]{SafeTransactions.SignTypedData(tx,75,new EthECKey("0000000000000000000000000000000000000000000000000000000000000001"))};
            if(p.Name=="executor") return Owner;
        }
        return Sample(p.ParameterType,p.Name!);
    }
    public sealed record DraftFixture(string Id,string Input,string Target,string Value,string Calldata);
}
