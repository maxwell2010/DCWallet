using System.Numerics;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Multisig;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Newtonsoft.Json;
using Xunit;
namespace DCWallet.Tests;
public sealed class HistorySafeOutcomeTests
{
 const string Owner="0x1111111111111111111111111111111111111111",Safe="0x2222222222222222222222222222222222222222";
 static readonly string Hash="0x"+new string('a',64),BlockHash="0x"+new string('b',64);
 static string Input(){var tx=SafeTransactionDrafts.BuildDel(Safe,Owner,1,0);return SafeAdvancedTransactions.Execute(Owner,tx,75,[SafeAdvancedTransactions.Sign(tx,75,EthECKey.GenerateKey())],new HashSet<string>()).Data;}
 static object Event(string name,string address=Safe,string? data=null)=>new{address,topics=new[]{"0x"+Nethereum.Util.Sha3Keccack.Current.CalculateHash(name+"(bytes32,uint256)"),Hash},data=data??"0x"+new string('0',64)};
 static TransactionReceipt Receipt(params object[] logs)=>JsonConvert.DeserializeObject<TransactionReceipt>(JsonConvert.SerializeObject(new{status="0x1",logs}))!;
 [Fact]public void ExactSuccessAndFailureRemainSeparateFromOuterStatus()
 {
  Assert.True(HistorySafeOutcome.Read(Input(),Safe,Receipt(Event("ExecutionSuccess"))));
  Assert.False(HistorySafeOutcome.Read(Input(),Safe,Receipt(Event("ExecutionFailure"))));
  Assert.Null(HistorySafeOutcome.Read(Input(),Safe,Receipt()));
  Assert.False(HistorySafeOutcome.Read(Input(),Safe,Receipt(Event("ExecutionSuccess"),Event("ExecutionFailure"))));
 }
 [Fact]public void WrongEmitterMalformedPayloadAndNonSafeCallsDoNotConfirmInnerOutcome()
 {
  Assert.Null(HistorySafeOutcome.Read(Input(),Safe,Receipt(Event("ExecutionSuccess",Owner))));
  Assert.Null(HistorySafeOutcome.Read(Input(),Safe,Receipt(Event("ExecutionSuccess",data:"0x00"))));
  Assert.Null(HistorySafeOutcome.Read("0xa9059cbb"+new string('0',128),Safe,Receipt(Event("ExecutionSuccess"))));
  Assert.Null(HistorySafeOutcome.Read("0x6a761202",Safe,Receipt(Event("ExecutionSuccess"))));
 }
 [Theory]
 [InlineData("ExecutionSuccess",true,true)]
 [InlineData("ExecutionFailure",true,false)]
 [InlineData(null,true,null)]
 [InlineData("ExecutionFailure",false,null)]
 public async Task CanonicalHistoryPreservesOuterSuccessWhileReportingInnerOutcome(string? eventName,bool outer,bool? expectedInner)
 {
  var input=Input();var logs=eventName is null?Array.Empty<object>():new[]{Event(eventName)};
  using var rpc=new FakeRpc{ReadResult=(method,args)=>method switch{
   "eth_chainId"=>"0x4b",
   "eth_getTransactionByHash"=>new{hash=Hash,blockHash=BlockHash,blockNumber="0x1",from=Owner,to=Safe,value="0x0",input,gasPrice="0x1",nonce="0x0",gas="0x5208"},
   "eth_getTransactionReceipt"=>new{transactionHash=Hash,blockHash=BlockHash,blockNumber="0x1",from=Owner,to=Safe,status=outer?"0x1":"0x0",gasUsed="0x5208",effectiveGasPrice="0x1",logs},
   "eth_getBlockByNumber"=>new{hash=BlockHash,number="0x1",timestamp="0x1",transactions=new[]{Hash}},
   _=>throw new InvalidOperationException("Unexpected fixture read "+method)}};
  var row=await new HistoryReader(new DecimalClient(DecimalNetwork.Mainnet()with{RpcUrl=new Uri(rpc.Url)}),false).VerifyAsync(Hash,Owner);
  Assert.NotNull(row);Assert.Equal(outer,row.Success);Assert.Equal(expectedInner,row.InnerSuccess);Assert.Equal(Safe,row.To);Assert.Equal(Hash,row.Hash);Assert.Equal(0,rpc.Broadcasts);
  if(!outer)Assert.Equal("Ошибка исполнения",row.Kind);
  else if(expectedInner==false){Assert.Contains("отклонено",row.Kind);Assert.Equal("Ошибка внутри Safe",HistorySafeOutcome.DisplayStatus(row));var csv=HistoryReader.Csv([row]);Assert.Contains("inner_status",csv);Assert.Contains("\"success\"",csv);Assert.Contains("\"failed\"",csv);}
  else if(expectedInner==true)Assert.Contains("подтверждено",row.Kind);
  else{Assert.Contains("не подтвержден",row.Kind);Assert.Equal("Внешняя транзакция подтверждена",HistorySafeOutcome.DisplayStatus(row));}
 }
}
