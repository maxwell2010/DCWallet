using System.Numerics;
using System.Text.Json;
using System.Security.Cryptography;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Checks;
using Decimal.Web3.Transactions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Xunit;
using Xunit.Abstractions;
namespace DCWallet.Tests;
public class MainnetChecksBehaviorReadFactAttribute:FactAttribute{public MainnetChecksBehaviorReadFactAttribute(){if(Environment.GetEnvironmentVariable("DC_MAINNET_CHECKS_READ")!="1")Skip="Explicit mainnet Checks eth_call-only behavior probe opt-in required";}}
public class MainnetChecksBehaviorReadTests(ITestOutputHelper output)
{
 [MainnetChecksBehaviorReadFact]public async Task PublicCandidateCanBeSimulatedWithoutAnyWalletSigner(){
  const string contract="0x9c3326594b49AFa24db9b988Cda1258d0dcb4e40";var from=MainnetProbeOwner.Required();
  var client=new DecimalClient(DecimalNetwork.Mainnet());await ChecksDeploymentProfile.VerifyAsync(client,contract);Assert.Equal(75,(await client.Web3.Eth.ChainId.SendRequestAsync()).Value);
  var block=await client.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(BlockParameter.CreateLatest());var at=new BlockParameter(block.Number);var code=await client.Web3.Eth.GetCode.SendRequestAsync(contract,at);var hash=Convert.ToHexString(SHA256.HashData(Convert.FromHexString(code[2..]))).ToLowerInvariant();
  Assert.Equal("7a9a364077d8ba3f03e77cfb09e79599faea344568ffa245903ccdb90691139e",hash);
  var balance=(await client.Web3.Eth.GetBalance.SendRequestAsync(from,at)).Value;var service=new ChecksService(client.Web3,contract);var owner=await service.GetOwnerAsync(at);var implementation=await service.GetImplementationAsync(at);
  var nonceRaw=await client.Web3.Eth.Transactions.Call.SendRequestAsync(new CallInput{From=from,To=contract,Data="0x93757526"},at);var nonce=new BigInteger(Convert.FromHexString(nonceRaw[2..]),true,true);var due=block.Number.Value+1000;
  using var bearer=new CheckSecret();var draft=service.BuildCreateDelDraft(from,new[]{bearer.Address},Units.Parse("0.001"),due,nonce);string? returned=null,error=null,rpcData=null;var success=false;
  if(balance>=draft.ValueWei){try{returned=await client.Web3.Eth.Transactions.Call.SendRequestAsync(new CallInput{From=from,To=contract,Data=draft.Data,Value=new HexBigInteger(draft.ValueWei)},at);var bytes=Convert.FromHexString(returned[2..]);success=bytes.Length==96&&new BigInteger(bytes[..32],true,true)==32&&new BigInteger(bytes[32..64],true,true)==1&&bytes[64..].Any(b=>b!=0);}catch(Nethereum.JsonRpc.Client.RpcResponseException e){error=e.Message;rpcData=e.RpcError.Data?.ToString();}}
  else error="Public simulation sender lacks .001 DEL; no funding or transaction attempted.";
  var result=new{chainId=75,contract,from,block=block.Number.Value.ToString(),blockHash=block.BlockHash,runtimeSha256=hash,owner,implementation,fromBalanceDel=Units.Format(balance),nonce=nonce.ToString(),dueBlock=due.ToString(),valueDel="0.001",ephemeralCheckAddress=bearer.Address,success,returned,error,rpcData,broadcast=false,walletSigner=false,configurationGetters="Official ABI has owner/getImpl/nonces only; no fee/maxChecks/due/initialized getters."};
  var root=Environment.GetEnvironmentVariable("DC_MAINNET_CHECKS_READ_ARTIFACTS")??Path.Combine(AppContext.BaseDirectory,"artifacts","mainnet-checks-behavior");Directory.CreateDirectory(root);var json=JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true});await File.WriteAllTextAsync(Path.Combine(root,"mainnet-checks-create-ethcall.json"),json);output.WriteLine(json);
 }
}
