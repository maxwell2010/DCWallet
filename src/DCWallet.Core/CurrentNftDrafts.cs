using System.Numerics;
using System.Text.Json;
using Decimal.Web3;
using Decimal.Web3.Transactions;
using Nethereum.Util;
namespace DCWallet.Core;
/// <summary>Current official Decimal NFT native-reserve selectors. Deployment estimation remains required.</summary>
public static class CurrentNftDrafts
{
 public static TransactionDraft Mint721WithDel(DecimalClient client,string from,string collection,string recipient,string uri,BigInteger valueWei)=>Build(client,from,collection,"mintByDEL",new[]{"address","string"},new object[]{recipient,uri},valueWei);
 public static TransactionDraft Mint1155WithDel(DecimalClient client,string from,string collection,string recipient,BigInteger id,BigInteger amount,string uri,BigInteger valueWei){if(id<0||id>=(BigInteger.One<<256)||amount<=0||amount>=(BigInteger.One<<256))throw new ArgumentOutOfRangeException(nameof(amount));return Build(client,from,collection,"mintByDEL",new[]{"address","uint256","uint256","string"},new object[]{recipient,id,amount,uri},valueWei);}
 static TransactionDraft Build(DecimalClient client,string from,string collection,string name,string[] types,object[] args,BigInteger value){if(client.Network.ChainId!=202020)throw new NotSupportedException("Reviewed current native NFT profile is testnet-only.");if(value<=0||value>=(BigInteger.One<<256))throw new ArgumentOutOfRangeException(nameof(value));_=WalletDataReader.Address(from);_=WalletDataReader.Address(collection);_=WalletDataReader.Address((string)args[0]);if(string.IsNullOrWhiteSpace((string)args[^1])||((string)args[^1]).Length>4096)throw new ArgumentException("Invalid NFT URI.");var abi=JsonSerializer.Serialize(new[]{new{type="function",name,inputs=types.Select(type=>new{type}),outputs=Array.Empty<object>()}});return TransactionDraft.ContractCall(from,collection,client.Web3.Eth.GetContract(abi,collection).GetFunction(name).GetData(args),value);}
 public static TransactionDraft AdaptLegacyNativeDraft(DecimalClient client,TransactionDraft draft){if(client.Network.ChainId!=202020)return draft;var signatures=new[]{("mintByETH(address,string)","mintByDEL(address,string)"),("mintByETH(address,uint256,uint256,string)","mintByDEL(address,uint256,uint256,string)"),("addReserveByETH(uint256)","addReserveByDEL(uint256)")};foreach(var pair in signatures){var oldSelector="0x"+Sha3Keccack.Current.CalculateHash(pair.Item1)[..8];if(draft.Data.StartsWith(oldSelector,StringComparison.OrdinalIgnoreCase)){if(draft.ValueWei<=0)throw new ArgumentException("Positive native reserve required.");return TransactionDraft.ContractCall(draft.From,draft.To,"0x"+Sha3Keccack.Current.CalculateHash(pair.Item2)[..8]+draft.Data[10..],draft.ValueWei);}}return draft;}
 /// <summary>Read-only compatibility selection: preserve a working legacy ABI; require positive DEL estimation before adapting.</summary>
 public static async Task<TransactionDraft> AdaptLegacyNativeDraftAsync(DecimalClient client,TransactionDraft draft,string? expectedSender=null,CancellationToken ct=default)
 {
  if(expectedSender is not null&&!draft.From.Equals(expectedSender,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Отправитель NFT-транзакции изменился.");
  if(client.Network.ChainId is not(75 or 202020))return draft;
  var signatures=new[]{("mintByETH(address,string)","mintByDEL(address,string)"),("mintByETH(address,uint256,uint256,string)","mintByDEL(address,uint256,uint256,string)"),("addReserveByETH(uint256)","addReserveByDEL(uint256)")};
  var match=signatures.FirstOrDefault(p=>draft.Data.StartsWith("0x"+Sha3Keccack.Current.CalculateHash(p.Item1)[..8],StringComparison.OrdinalIgnoreCase));
  if(match.Item1 is null)return draft;
  if(draft.ValueWei<=0)throw new ArgumentException("Для native-резерва нужна положительная сумма DEL.");
  try{await client.EstimateFeeAsync(draft).WaitAsync(ct);return draft;}
  catch(DecimalTransactionException e)when(e.Code==DecimalTransactionError.ContractRejected){}
  var replacement=TransactionDraft.ContractCall(draft.From,draft.To,"0x"+Sha3Keccack.Current.CalculateHash(match.Item2)[..8]+draft.Data[10..],draft.ValueWei);
  try{await client.EstimateFeeAsync(replacement).WaitAsync(ct);return replacement;}
  catch(DecimalTransactionException e)when(e.Code==DecimalTransactionError.ContractRejected){throw new NotSupportedException("Контракт отклонил оба варианта native NFT-операции. Проверьте ABI коллекции, права автора, номер NFT и минимальный резерв. Версия ABI не подтверждена этой проверкой.",e);}
 }
}
