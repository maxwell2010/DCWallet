using System.Numerics;
using Decimal.Web3;
using Decimal.Web3.Wallet;
using Decimal.Web3.Tokens;
using Decimal.Web3.Multisend;
using Nethereum.Web3.Accounts;
using Nethereum.Signer;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
namespace DCWallet.Core;
public sealed class WalletSigningSession : IDisposable
{
 private readonly object gate=new(); private Account? account;
 public string Address {get;} public BigInteger ChainId {get;}
 private WalletSigningSession(Account value,BigInteger chain){if(chain<=0)throw new ArgumentOutOfRangeException(nameof(chain));account=value;Address=value.Address;ChainId=chain;}
 public static WalletSigningSession FromMnemonic(string mnemonic,BigInteger chainId,int index=0){if(!DecimalWallet.IsValidMnemonic(mnemonic)||index is <0 or >9)throw new ArgumentException("Invalid mnemonic or account index.");return new(new Nethereum.HdWallet.Wallet(mnemonic,"").GetAccount(index),chainId);}
 public static WalletSigningSession FromPrivateKey(string key,BigInteger chainId){var normalized=ValidatePrivateKey(key);return new(new Account(normalized,chainId),chainId);}
 public static string ValidatePrivateKey(string value){var key=value?.Trim()??"";if(key.StartsWith("0x",StringComparison.OrdinalIgnoreCase))key=key[2..];if(key.Length!=64||!key.All(Uri.IsHexDigit))throw new ArgumentException("Invalid private key.");var scalar=new BigInteger(Convert.FromHexString(key),true,true);var order=new BigInteger(Convert.FromHexString("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEBAAEDCE6AF48A03BBFD25E8CD0364141"),true,true);if(scalar<=0||scalar>=order)throw new ArgumentException("Invalid private key.");try{_=new EthECKey(key);}catch{throw new ArgumentException("Invalid private key.");}return key.ToLowerInvariant();}
 public string SignDigest(byte[] digest){if(digest is null||digest.Length!=32)throw new ArgumentException("Digest must contain 32 bytes.");lock(gate)return new MessageSigner().Sign(digest,new EthECKey(Current().PrivateKey));}
 public string SignLegacyTransaction(BigInteger nonce,string to,BigInteger valueWei,BigInteger gasPriceWei,BigInteger gasLimit,string data="0x"){
  if(nonce<0||valueWei<0||gasPriceWei<0||gasLimit<=0)throw new ArgumentOutOfRangeException(nameof(nonce));
  _=Decimal.Web3.Transactions.TransactionDraft.ContractCall(Address,to,data,valueWei);
  lock(gate)return new AccountOfflineTransactionSigner().SignTransaction(Current(),new TransactionInput{From=Address,To=to,Data=data,Nonce=new HexBigInteger(nonce),Value=new HexBigInteger(valueWei),GasPrice=new HexBigInteger(gasPriceWei),Gas=new HexBigInteger(gasLimit)},ChainId);
 }
 public async Task<Erc20PermitSignature> SignPermitAsync(DecimalClient client,string token,string spender,BigInteger amount,BigInteger deadline){if(client.Network.ChainId!=ChainId)throw new InvalidOperationException("Signer chain mismatch.");var data=await new Erc20PermitService(client,token).PrepareAsync(Address,spender,amount,deadline);var raw=Convert.FromHexString(SignDigest(Erc20PermitService.CalculateDigest(data)).Replace("0x",""));if(raw.Length!=65||raw[64] is not(27 or 28))throw new InvalidOperationException("Invalid permit signature.");return new(deadline,raw[64],raw[..32],raw[32..64]);}
 private Account Current()=>account??throw new ObjectDisposedException(nameof(WalletSigningSession));
 public void Dispose(){lock(gate)account=null;}
 public override string ToString()=>"WalletSigningSession "+Address;
}
