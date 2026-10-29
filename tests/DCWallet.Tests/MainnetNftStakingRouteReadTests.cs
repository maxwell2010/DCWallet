using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Nft;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Xunit;
namespace DCWallet.Tests;
public class MainnetNftStakingRouteReadTests
{
 [Fact]public void KnownPauseErrorSelectorMatchesPrimaryAbi()=>Assert.Equal("0xd93c0665","0x"+Nethereum.Util.Sha3Keccack.Current.CalculateHash("EnforcedPause()")[..8]);
 [MainnetChecksBehaviorReadFact]public async Task InspectRegistryLegacyAndCombinedRoutesWithoutSigning(){
  const string nft="0x00001c9a89433faf5e17b83afdb247bf80e5ee13",validator="0x05BaBC196B96C22f763f7e30a84E74665A920f98";var client=new DecimalClient(DecimalNetwork.Mainnet());Assert.Equal(75,(await client.Web3.Eth.ChainId.SendRequestAsync()).Value);var legacy=await client.ResolveSystemContractAsync("delegation-nft");var combined=await client.ResolveSystemContractAsync("delegation");var nftOwner=await new Erc721Service(client).OwnerOfAsync(nft,1);var owner=MainnetProbeOwner.Required();var block=await client.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(BlockParameter.CreateLatest());var at=new BlockParameter(block.Number);var routes=new List<object>();
  foreach(var item in new[]{(name:"legacy delegation-nft",address:legacy),(name:"combined delegation",address:combined)}){var code=await client.Web3.Eth.GetCode.SendRequestAsync(item.address,at);var slot=await client.Web3.Eth.GetStorageAt.SendRequestAsync(item.address,new HexBigInteger("0x360894a13ba1a3210667c828492db98dca3e2076cc3735a920a3ca505d382bbc"),at);string? implementation=slot.Length==66&&slot[26..].Any(c=>c!='0')?"0x"+slot[26..]:null;var implCode=implementation is null?null:await client.Web3.Eth.GetCode.SendRequestAsync(implementation,at);var calls=new List<object>();
   if(item.address==legacy){await Call("getStake",new[]{"address","address","address","uint256"},new object[]{validator,owner,nft,BigInteger.One});await Call("delegateDRC721",new[]{"address","address","uint256"},new object[]{validator,nft,BigInteger.One});}
   else{await Call("getNFTStake",new[]{"address","uint256","address","address","uint256"},new object[]{nft,BigInteger.One,owner,validator,BigInteger.Zero});await Call("delegateNFT",new[]{"address","address","uint256"},new object[]{validator,nft,BigInteger.One});}
   routes.Add(new{item.name,item.address,proxyBytes=(code.Length-2)/2,proxySha256=Sha(code),implementation,implementationBytes=implCode is null?0:(implCode.Length-2)/2,implementationSha256=implCode is null?null:Sha(implCode),matchesReviewedTestnetRuntime=implCode is not null&&Sha(implCode)==CombinedNftDelegation.RuntimeSha256,calls});
   async Task Call(string method,string[] types,object[] args){var abi=JsonSerializer.Serialize(new[]{new{type="function",name=method,inputs=types.Select(type=>new{type}),outputs=Array.Empty<object>()}});var data=client.Web3.Eth.GetContract(abi,item.address).GetFunction(method).GetData(args);try{var raw=await client.Web3.Eth.Transactions.Call.SendRequestAsync(new CallInput{From=owner,To=item.address,Data=data},at);calls.Add(new{method,selector=data[..10],success=true,returned=raw});}catch(Nethereum.JsonRpc.Client.RpcResponseException e){var raw=e.RpcError.Data?.ToString();calls.Add(new{method,selector=data[..10],success=false,error=e.Message,rpcData=raw,decodedError=DecodeError(raw)});}}
  }
  var root=Environment.GetEnvironmentVariable("DC_MAINNET_CHECKS_READ_ARTIFACTS")??Path.Combine(AppContext.BaseDirectory,"artifacts","mainnet-nft-staking");Directory.CreateDirectory(root);await File.WriteAllTextAsync(Path.Combine(root,"mainnet-nft-staking-routes.json"),JsonSerializer.Serialize(new{chainId=75,block=block.Number.Value.ToString(),blockHash=block.BlockHash,nft,nftOwner,owner,validator,validatorNote="Public testnet validator used only as nonzero probe argument; mainnet validity not asserted.",routes,broadcast=false,signer=false},new JsonSerializerOptions{WriteIndented=true}));
 }
 [MainnetChecksBehaviorReadFact]public async Task ClarifyPauseOriginWithThreeBoundedGetterCalls(){
  var client=new DecimalClient(DecimalNetwork.Mainnet());Assert.Equal(75,(await client.Web3.Eth.ChainId.SendRequestAsync()).Value);var rows=new List<object>();
  foreach(var entry in new[]{(name:"legacy delegation-nft",address:"0xE45ADFCC739a0d10CE9462B58866c9A1a06035E2"),(name:"selected ERC721 collection",address:"0x00001c9a89433faf5e17b83afdb247bf80e5ee13"),(name:"registry delegation",address:"0xA16c34Ed1c0601c0E749e17ebeF19752A15FaA01")}){
   try{var raw=await client.Web3.Eth.Transactions.Call.SendRequestAsync(new CallInput{To=entry.address,Data="0x5c975abb"});bool? paused=raw.Length==66&&raw[2..].All(Uri.IsHexDigit)&&new BigInteger(Convert.FromHexString(raw[2..]),true,true)<=1?new BigInteger(Convert.FromHexString(raw[2..]),true,true)==1:null;rows.Add(new{entry.name,entry.address,getter="paused()",raw,paused});}catch(Nethereum.JsonRpc.Client.RpcResponseException e){rows.Add(new{entry.name,entry.address,getter="paused()",error=e.Message,rpcData=e.RpcError.Data?.ToString(),paused=(bool?)null});}
  }
  var root=Environment.GetEnvironmentVariable("DC_MAINNET_CHECKS_READ_ARTIFACTS")??Path.Combine(AppContext.BaseDirectory,"artifacts","mainnet-nft-staking");Directory.CreateDirectory(root);await File.WriteAllTextAsync(Path.Combine(root,"mainnet-nft-pause-origin.json"),JsonSerializer.Serialize(new{chainId=75,selector="0x5c975abb",signature="paused()",rows,broadcast=false,signer=false},new JsonSerializerOptions{WriteIndented=true}));
 }
 static string Sha(string code)=>Convert.ToHexString(SHA256.HashData(Convert.FromHexString(code[2..]))).ToLowerInvariant();
 static string? DecodeError(string? raw){if(raw=="0xd93c0665")return "EnforcedPause()";if(raw is null||!raw.StartsWith("0x08c379a0")||raw.Length<138)return null;try{var length=(int)new BigInteger(Convert.FromHexString(raw.Substring(74,64)),true,true);if(length<0||length>4096||raw.Length<138+length*2)return null;return Encoding.UTF8.GetString(Convert.FromHexString(raw.Substring(138,length*2)));}catch{return null;}}
}
