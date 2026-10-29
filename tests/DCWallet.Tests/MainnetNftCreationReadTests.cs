using System.Security.Cryptography;
using System.Text.Json;
using Decimal.Web3;
using Decimal.Web3.Nft;
using Decimal.Web3.Transactions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using Xunit;
using Xunit.Abstractions;
namespace DCWallet.Tests;
public class MainnetNftCreationReadTests(ITestOutputHelper output)
{
 [MainnetChecksBehaviorReadFact]public async Task CompareOnlyKnownModernAndLegacyCreationCalldata(){
  var owner=MainnetProbeOwner.Required();var client=new DecimalClient(DecimalNetwork.Mainnet());Assert.Equal(75,(await client.Web3.Eth.ChainId.SendRequestAsync()).Value);var resolved=await client.ResolveSystemContractsAsync();var center=resolved.NftCenterContract;var block=await client.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(BlockParameter.CreateLatest());var at=new BlockParameter(block.Number);var code=await client.Web3.Eth.GetCode.SendRequestAsync(center,at);var builder=new DecimalNftDrafts(resolved,center);
  var symbol="qa"+Guid.NewGuid().ToString("N")[..8];var entries=new[]{(name:"createERC721 tuple5",draft:builder.BuildCreateErc721Draft(owner,owner,symbol,"DC readonly audit","ipfs://readonly-audit",true)),(name:"createERC1155 tuple5",draft:builder.BuildCreateErc1155Draft(owner,owner,symbol,"DC readonly audit","ipfs://readonly-audit",true)),(name:"createDRC721 tuple6",draft:builder.BuildCreateDrc721LegacyDraft(owner,owner,symbol,"DC readonly audit","ipfs://readonly-audit",true,true)),(name:"createDRC1155 tuple6",draft:builder.BuildCreateDrc1155LegacyDraft(owner,owner,symbol,"DC readonly audit","ipfs://readonly-audit",true,true))};
  var results=new List<object>();foreach(var entry in entries){var call=new CallInput{From=owner,To=center,Data=entry.draft.Data,Value=new HexBigInteger(0)};try{var returned=await client.Web3.Eth.Transactions.Call.SendRequestAsync(call,at);var gas=await client.Web3.Eth.Transactions.EstimateGas.SendRequestAsync(call);results.Add(new{entry.name,selector=entry.draft.Data[..10],success=true,returned,gas=gas.Value.ToString(),valueWei="0"});}catch(Nethereum.JsonRpc.Client.RpcResponseException e){results.Add(new{entry.name,selector=entry.draft.Data[..10],success=false,error=e.Message,rpcData=e.RpcError.Data?.ToString(),valueWei="0"});}}
  var report=new{chainId=75,owner,center,block=block.Number.Value.ToString(),blockHash=block.BlockHash,runtimeSha256=Convert.ToHexString(SHA256.HashData(Convert.FromHexString(code[2..]))).ToLowerInvariant(),results,broadcast=false,signer=false};var root=Environment.GetEnvironmentVariable("DC_MAINNET_CHECKS_READ_ARTIFACTS")??Path.Combine(AppContext.BaseDirectory,"artifacts","mainnet-nft-audit");Directory.CreateDirectory(root);var json=JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true});await File.WriteAllTextAsync(Path.Combine(root,"mainnet-nft-create-abi-probe.json"),json);output.WriteLine(json);
 }
}
