using System.Security.Cryptography;
using System.Text.Json;
using Decimal.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using Nethereum.Util;
using Xunit;
namespace DCWallet.Tests;
public class MainnetNftNativeProfileReadTests
{
 [MainnetChecksBehaviorReadFact]public async Task InspectTwoExistingPublicCollectionsWithoutWrites(){
  var from=MainnetProbeOwner.Required();var client=new DecimalClient(DecimalNetwork.Mainnet());Assert.Equal(75,(await client.Web3.Eth.ChainId.SendRequestAsync()).Value);var block=await client.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(BlockParameter.CreateLatest());var at=new BlockParameter(block.Number);var entries=new[]{(address:"0x00001c9a89433faf5e17b83afdb247bf80e5ee13",standard:"ERC721"),(address:"0x9ab79492146e3d7835e6b13dfc9f797806b3f141",standard:"ERC1155")};var profiles=new List<object>();
  foreach(var entry in entries){var code=await client.Web3.Eth.GetCode.SendRequestAsync(entry.address,at);var slot=await client.Web3.Eth.GetStorageAt.SendRequestAsync(entry.address,new HexBigInteger("0x360894a13ba1a3210667c828492db98dca3e2076cc3735a920a3ca505d382bbc"),at);var selectorList=Pushes(code,4).Distinct().ToArray();var targets=Pushes(code,20).Where(x=>x.Any(c=>c!='0')).Distinct().Take(4).Select(x=>"0x"+x).ToList();if(slot.Length==66&&slot[26..].Any(c=>c!='0'))targets.Add("0x"+slot[26..]);
   var implementations=new List<object>();foreach(var target in targets.Distinct()){var runtime=await client.Web3.Eth.GetCode.SendRequestAsync(target,at);if(runtime.Length<4)continue;implementations.Add(new{address=target,bytes=(runtime.Length-2)/2,sha256=Sha(runtime),selectors=Pushes(runtime,4).Distinct().ToArray()});}
   var calls=new List<object>();foreach(var getter in new[]{"creator","getImpl","getImplementation","implementation"}){var abi=JsonSerializer.Serialize(new[]{new{type="function",name=getter,inputs=Array.Empty<object>(),outputs=Array.Empty<object>()}});var data=client.Web3.Eth.GetContract(abi,entry.address).GetFunction(getter).GetData();var response=await Call(getter,data,0);calls.Add(response);
    var encoded=JsonSerializer.SerializeToElement(response);if(getter=="implementation"&&encoded.GetProperty("success").GetBoolean()&&encoded.TryGetProperty("returned",out var value)&&value.GetString() is {Length:66} raw&&raw[26..].Any(c=>c!='0')){var address="0x"+raw[26..];var implementationCode=await client.Web3.Eth.GetCode.SendRequestAsync(address,at);if(implementationCode.Length>=4)implementations.Add(new{address,bytes=(implementationCode.Length-2)/2,sha256=Sha(implementationCode),selectors=Pushes(implementationCode,4).Distinct().ToArray()});}
}
   foreach(var name in new[]{"mintByETH","mintByDEL"}){var types=entry.standard=="ERC721"?new[]{"address","string"}:new[]{"address","uint256","uint256","string"};var args=entry.standard=="ERC721"?new object[]{from,"ipfs://read-only-native-audit"}:new object[]{from,System.Numerics.BigInteger.One,System.Numerics.BigInteger.One,"ipfs://read-only-native-audit"};var abi=JsonSerializer.Serialize(new[]{new{type="function",name,inputs=types.Select(type=>new{type}),outputs=Array.Empty<object>()}});var data=client.Web3.Eth.GetContract(abi,entry.address).GetFunction(name).GetData(args);calls.Add(await Call(name,data,Units.Parse("0.001")));}
   foreach(var name in new[]{"addReserveByETH","addReserveByDEL"}){var abi=JsonSerializer.Serialize(new[]{new{type="function",name,inputs=new[]{new{type="uint256"}},outputs=Array.Empty<object>()}});var data=client.Web3.Eth.GetContract(abi,entry.address).GetFunction(name).GetData(System.Numerics.BigInteger.One);calls.Add(await Call(name,data,Units.Parse("0.001")));}
   profiles.Add(new{entry.address,entry.standard,bytes=(code.Length-2)/2,sha256=Sha(code),erc1967Slot=slot,selectors=selectorList,implementations,calls});
   async Task<object> Call(string method,string data,System.Numerics.BigInteger value){try{var raw=await client.Web3.Eth.Transactions.Call.SendRequestAsync(new CallInput{From=from,To=entry.address,Data=data,Value=new HexBigInteger(value)},at);return new{method,selector=data[..10],success=true,returned=raw};}catch(Nethereum.JsonRpc.Client.RpcResponseException e){return new{method,selector=data[..10],success=false,error=e.Message,rpcData=e.RpcError.Data?.ToString()};}}
  }
  var root=Environment.GetEnvironmentVariable("DC_MAINNET_CHECKS_READ_ARTIFACTS")??Path.Combine(AppContext.BaseDirectory,"artifacts","mainnet-nft-native-profile");Directory.CreateDirectory(root);await File.WriteAllTextAsync(Path.Combine(root,"mainnet-nft-native-profiles.json"),JsonSerializer.Serialize(new{chainId=75,block=block.Number.Value.ToString(),blockHash=block.BlockHash,from,profiles,broadcast=false,signer=false},new JsonSerializerOptions{WriteIndented=true}));
 }
 static string Sha(string hex)=>Convert.ToHexString(SHA256.HashData(Convert.FromHexString(hex[2..]))).ToLowerInvariant();
 static IEnumerable<string> Pushes(string hex,int length){var bytes=Convert.FromHexString(hex[2..]);for(var i=0;i<bytes.Length;i++){var op=bytes[i];if(op>=0x60&&op<=0x7f){var n=op-0x5f;if(i+n>=bytes.Length)yield break;if(n==length)yield return Convert.ToHexString(bytes.AsSpan(i+1,n)).ToLowerInvariant();i+=n;}}}
}
