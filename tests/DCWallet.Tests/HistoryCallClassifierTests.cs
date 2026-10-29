using System.Numerics;
using System.Text.Json;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Multisig;
using Decimal.Web3.Staking;
using Decimal.Web3.TokenEconomy;
using Decimal.Web3.Tokens;
using Nethereum.Signer;
using Xunit;
namespace DCWallet.Tests;
public class HistoryCallClassifierTests
{
 const string Owner="0x1111111111111111111111111111111111111111",Target="0x2222222222222222222222222222222222222222";
 [Fact] public void ActualLifecycleFamiliesDescribeIntentWithoutClaimingContractIdentity()
 {
  var client=new DecimalClient();var erc=new Erc20Service(client,Target);var stake=new DelegationDrafts(DecimalNetwork.Mainnet(),Target);var economy=new TokenEconomyDrafts(Target);
  var cases=new[]{
   (erc.BuildApproveDraft(Owner,Target,1).Data,"Вызов: Разрешение на актив"),
   (erc.BuildTransferDraft(Owner,Target,1).Data,"Вызов: Перевод актива"),
   (stake.BuildCompleteDraft(Owner,[230]).Data,"Вызов: Завершение вывода стейка"),
   (stake.BuildDelegateDelDraft(Owner,Target,"0.001").Data,"Вызов: Делегирование"),
   (economy.BuildSellExactTokensForDelDraft(Owner,Target,1,1,Owner).Data,"Вызов: Продажа токена")};
  foreach(var(data,expected)in cases)Assert.Equal(expected,HistoryCallClassifier.Classify(data,"Вызов контракта"));
  var tx=SafeTransactionDrafts.BuildDel(Target,Owner,1,0);var sig=SafeAdvancedTransactions.Sign(tx,202020,EthECKey.GenerateKey());
  Assert.Equal("Вызов: Исполнение предложения Safe",HistoryCallClassifier.Classify(SafeAdvancedTransactions.Execute(Owner,tx,202020,[sig],new HashSet<string>()).Data,"Вызов контракта"));
 }
 [Fact] public void CurrentNftNativeMintAndStakingSelectorsAreIncluded()
 {
  var client=new DecimalClient(DecimalNetwork.Testnet());
  Assert.Equal("Вызов: Выпуск актива",HistoryCallClassifier.Classify(CurrentNftDrafts.Mint721WithDel(client,Owner,Target,Owner,"ipfs://qa",1).Data,"fallback"));
  foreach(var erc721 in new[]{true,false})foreach(var hold in new[]{BigInteger.Zero,BigInteger.One})
  {
   Assert.Equal("Вызов: Делегирование",HistoryCallClassifier.Classify(StakingNftDrafts.Delegate(client,Owner,Target,Target,1,1,erc721,hold).Data,"fallback"));
   Assert.Equal("Вызов: Заявка на вывод стейка",HistoryCallClassifier.Classify(StakingNftDrafts.Withdraw(client,Owner,Target,Target,1,erc721,hold).Data,"fallback"));
  }
 }
 [Fact] public void AmbiguousErc20Erc721SignaturesNeverGuessAssetType()
 {
  string Call(string signature,int words)=>"0x"+Nethereum.Util.Sha3Keccack.Current.CalculateHash(signature)[..8]+new string('0',64*words);
  Assert.Equal("Вызов: Разрешение на актив",HistoryCallClassifier.Classify(Call("approve(address,uint256)",2),"NFT"));
  Assert.Equal("Вызов: Перевод актива",HistoryCallClassifier.Classify(Call("transferFrom(address,address,uint256)",3),"Токен"));
 }
 [Theory][InlineData(null)][InlineData("0x")][InlineData("0xabc")][InlineData("0xzzzzzzzz")][InlineData("0xa9059cbb")][InlineData("0xdeadbeef")][InlineData("0xa9059cbb00")]
 public void InvalidUnknownAndTruncatedPayloadPreservesFallback(string? input)=>Assert.Equal("Исходный вид",HistoryCallClassifier.Classify(input,"Исходный вид"));
 [Fact] public void FailedReceiptCannotBeRelabeledAsSuccessfulIntent()
 {
  var data=new Erc20Service(new DecimalClient(),Target).BuildApproveDraft(Owner,Target,1).Data;
  Assert.Equal("Ошибка исполнения",HistoryCallClassifier.Classify(data,"Ошибка исполнения",false));
 }
 [Fact] public void MalformedDynamicOffsetsAndLengthsAreNotInterpreted()
 {
  var data=new DelegationDrafts(DecimalNetwork.Mainnet(),Target).BuildCompleteDraft(Owner,[230]).Data;
  Assert.Equal("fallback",HistoryCallClassifier.Classify(data[..10]+new string('f',64)+data[74..],"fallback"));
  Assert.Equal("fallback",HistoryCallClassifier.Classify(data[..74]+new string('f',64)+data[138..],"fallback"));
 }
 [Fact] public void CatalogFixturesCoverStakesChecksSwapsAndNftIntentLabels()
 {
  var expected=new Dictionary<string,string>{{"ChecksService.BuildCreateTokenDraft","Создание чеков"},{"ChecksService.BuildCreateTokenByPermitDraft","Создание чеков"},{"ChecksService.BuildRedeemDraft","Погашение чеков"},{"TokenEconomyDrafts.BuildConvert","Обмен токенов"},{"DecimalNftDrafts.BuildDelegate","Делегирование"}};
  foreach(var pair in expected)
  {
   var fixtures=Directory.GetFiles(Path.Combine(AppContext.BaseDirectory,"CatalogFixtures"),"*.json").Select(p=>JsonDocument.Parse(File.ReadAllText(p))).ToArray();
   try{var rows=fixtures.Where(d=>d.RootElement.GetProperty("Id").GetString()!.StartsWith(pair.Key,StringComparison.Ordinal)).ToArray();Assert.NotEmpty(rows);foreach(var row in rows)Assert.Equal("Вызов: "+pair.Value,HistoryCallClassifier.Classify(row.RootElement.GetProperty("Calldata").GetString(),"fallback"));}
   finally{foreach(var fixture in fixtures)fixture.Dispose();}
  }
 }
}
