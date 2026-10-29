using System.Text.Json;
using System.Numerics;
using Decimal.Web3;
using Decimal.Web3.Transactions;
using Decimal.Web3.Tokens;
using DCWallet.Core;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using Xunit;
using Xunit.Abstractions;
namespace DCWallet.Tests;
public class StakingReadProbeTests(ITestOutputHelper output)
{
 [Nft721PlanReadFact]public async Task InspectKnownQaAbiWithoutSigning(){var client=new DecimalClient(DecimalNetwork.Testnet() with{RpcUrl=new Uri("https://202020.rpc.thirdweb.com")});const string nft="0x9Cc8BFB95BaAE03BfE6E0a0E00548762BEDdac54";var results=new List<object>();
 const string mintAbi="""[{"type":"function","name":"mintByDEL","inputs":[{"type":"address"},{"type":"string"}],"outputs":[]}]""";
 var data=client.Web3.Eth.GetContract(mintAbi,nft).GetFunction("mintByDEL").GetData(AdvancedTestContext.ExpectedOwner,"ipfs://dc-qa-721/1");
 try{var quote=await client.EstimateFeeAsync(TransactionDraft.ContractCall(AdvancedTestContext.ExpectedOwner,nft,data,Units.Parse("0.001")));results.Add(new{probe="mintByDEL",maximumFee=quote.MaximumFeeDel,quote.CanAfford,valueDel="0.001",broadcast=false});}catch(Exception e){results.Add(new{probe="mintByDEL",error=e.ToString(),rpcData=e is Nethereum.JsonRpc.Client.RpcResponseException rpc?rpc.RpcError.Data?.ToString():null,broadcast=false});}
 try{var d=StakingNftDrafts.Delegate(client,AdvancedTestContext.ExpectedOwner,Erc20StakingNetworkScenario.Validator,nft,1,1,true,0);var q=await client.EstimateFeeAsync(d);results.Add(new{probe="delegateNFT721",maximumFeeDel=q.MaximumFeeDel,estimatedGas=q.EstimatedGas.ToString(),gasLimit=q.GasLimit.ToString(),gasPriceWei=q.GasPriceWei.ToString(),q.CanAfford});}catch(Exception e){results.Add(new{probe="delegateNFT721",error=e.ToString(),rpcData=e.InnerException is Nethereum.JsonRpc.Client.RpcResponseException rpc?rpc.RpcError.Data?.ToString():null});}
 foreach(var token in new[]{"0xc310f25e3429efc5ede11a908682f0a31cfc92ce","0x37748f3e857a71e4334125a935052ce1e603b7b5"}){
 try{var permit=await new Erc20PermitService(client,token).PrepareAsync(AdvancedTestContext.ExpectedOwner,CombinedNftDelegation.Proxy,1,DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds());results.Add(new{probe="permit-domain",token,permit.TokenName,permit.TokenVersion,nonce=permit.Nonce.ToString()});}catch(Exception e){results.Add(new{probe="permit-domain",token,error=e.ToString(),rpcData=e is Nethereum.JsonRpc.Client.RpcResponseException rpc?rpc.RpcError.Data?.ToString():null});}
 try{var draft=new Decimal.Web3.Staking.DelegationDrafts(client.Network,CombinedNftDelegation.Proxy).BuildDelegateErc20Draft(AdvancedTestContext.ExpectedOwner,Erc20StakingNetworkScenario.Validator,token,1);await client.Web3.Eth.Transactions.Call.SendRequestAsync(new CallInput{From=draft.From,To=draft.To,Data=draft.Data});results.Add(new{probe="delegate1raw",token,status="eth_call-success"});}catch(Exception e){results.Add(new{probe="delegate1raw",token,error=e.ToString(),rpcData=e is Nethereum.JsonRpc.Client.RpcResponseException rpc?rpc.RpcError.Data?.ToString():null});}
 }
 results.Add(new{errorSelectors=new[]{"InvalidToken()","InvalidTokenType()","ZeroAmount()","InvalidAmount()","ERC20InsufficientAllowance(address,uint256,uint256)","ERC20InsufficientBalance(address,uint256,uint256)"}.Select(s=>new{signature=s,selector="0x"+Nethereum.Util.Sha3Keccack.Current.CalculateHash(s)[..8]}).ToArray()});
 var json=JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true});Directory.CreateDirectory("artifacts/staking-read-probe");await File.WriteAllTextAsync("artifacts/staking-read-probe/result.json",json);output.WriteLine(json);
 }
}
