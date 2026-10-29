using System.Numerics;
using System.Text;
using Decimal.Web3.Multisig;
using Decimal.Web3.Transactions;
using Nethereum.Contracts.MessageEncodingServices;
using Nethereum.Signer;
using Nethereum.Util;
namespace DCWallet.Core;
/// <summary>Wallet-owned Safe encoding supporting operation 1 and dynamic contract signatures. No network certification.</summary>
public static class SafeAdvancedTransactions
{
    static byte[] Hash(byte[] value) => Sha3Keccack.Current.CalculateHash(value);
    static byte[] Join(params byte[][] values) => values.SelectMany(v => v).ToArray();
    static byte[] Word(BigInteger value) { if(value < 0 || value >= BigInteger.One << 256) throw new ArgumentOutOfRangeException(nameof(value)); var bytes=value.ToByteArray(true,true); var result=new byte[32]; bytes.CopyTo(result,32-bytes.Length); return result; }
    static byte[] Address(string value) { ArgumentNullException.ThrowIfNull(value); if (!System.Text.RegularExpressions.Regex.IsMatch(value, "^0x[0-9a-fA-F]{40}$")) throw new ArgumentException("Invalid Safe address."); return Join(new byte[12],Convert.FromHexString(value[2..])); }
    static byte[] Hex(string value) { if(value is null || !value.StartsWith("0x",StringComparison.Ordinal) || value.Length%2!=0 || value.Length>131074) throw new ArgumentException("Expected bounded 0x hex."); return Convert.FromHexString(value[2..]); }
    public static byte[] SigningData(SafeTransactionDraft tx, BigInteger chain)
    {
        if(tx.Operation == 1 && tx.ValueWei != 0) throw new ArgumentException("DelegateCall cannot transfer native value.");
        if(chain<=0 || tx.Operation>1) throw new ArgumentException("Invalid chain or Safe operation.");
        Address(tx.SafeAddress); Address(tx.To);
        var domain=Hash(Join(Hash(Encoding.ASCII.GetBytes("EIP712Domain(uint256 chainId,address verifyingContract)")),Word(chain),Address(tx.SafeAddress)));
        var inner=Hash(Join(Hash(Encoding.ASCII.GetBytes("SafeTx(address to,uint256 value,bytes data,uint8 operation,uint256 safeTxGas,uint256 baseGas,uint256 gasPrice,address gasToken,address refundReceiver,uint256 nonce)")),Address(tx.To),Word(tx.ValueWei),Hash(Hex(tx.Data)),Word(tx.Operation),Word(tx.SafeTxGas),Word(tx.BaseGas),Word(tx.GasPriceWei),Address(tx.GasToken),Address(tx.RefundReceiver),Word(tx.Nonce)));
        return Join([0x19,0x01],domain,inner);
    }
    public static string CalculateHash(SafeTransactionDraft tx, BigInteger chain) => "0x"+Convert.ToHexString(Hash(SigningData(tx,chain))).ToLowerInvariant();
    public static SafeSignature Sign(SafeTransactionDraft tx,BigInteger chain,EthECKey key) => new(key.GetPublicAddress(),new MessageSigner().Sign(Hash(SigningData(tx,chain)),key));
    public static byte[] Pack(SafeTransactionDraft tx, BigInteger chain, IReadOnlyList<SafeSignature> signatures,IReadOnlySet<string> approved)
    {
        var digest=Hash(SigningData(tx,chain));
        var ordered=signatures.OrderBy(s=>s.Signer,StringComparer.OrdinalIgnoreCase).ToArray();
        if(ordered.Length is <1 or >100 || ordered.Select(s=>s.Signer).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=ordered.Length) throw new ArgumentException("Unique Safe signatures required.");
        var head=new List<byte>(); var tail=new List<byte>();
        foreach(var sig in ordered)
        {
            var owner=Address(sig.Signer);
            if(sig.Data.StartsWith("contract:",StringComparison.Ordinal))
            {
                var data=Hex(sig.Data[9..]); if(data.Length==0) throw new ArgumentException("Empty contract signature.");
                head.AddRange(owner); head.AddRange(Word(checked(ordered.Length*65+tail.Count))); head.Add(0);
                tail.AddRange(Word(data.Length)); tail.AddRange(data);
            }
            else if(sig.Data=="approved_hash")
            {
                if(!approved.Contains(sig.Signer)) throw new ArgumentException("Unverified on-chain approval.");
                head.AddRange(owner); head.AddRange(new byte[32]); head.Add(1);
            }
            else
            {
                var raw=Hex(sig.Data);
                if(raw.Length!=65 || raw[64] is not (27 or 28) || !new MessageSigner().EcRecover(digest,sig.Data).Equals(sig.Signer,StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Wrong Safe EOA signature.");
                head.AddRange(raw);
            }
        }
        if (head.Count + tail.Count > 65536) throw new ArgumentException("Packed signatures exceed 64 KiB.");
        return head.Concat(tail).ToArray();
    }
    public static TransactionDraft Execute(string executor,SafeTransactionDraft tx,BigInteger chain,IReadOnlyList<SafeSignature> signatures,IReadOnlySet<string> approved)
    {
        var message=new SafeTransactions.SafeExecTransaction { To=tx.To,Value=tx.ValueWei,Data=Hex(tx.Data),Operation=tx.Operation,SafeTxGas=tx.SafeTxGas,BaseGas=tx.BaseGas,SafeGasPrice=tx.GasPriceWei,GasToken=tx.GasToken,RefundReceiver=tx.RefundReceiver,Signatures=Pack(tx,chain,signatures,approved) };
        var data=new FunctionMessageEncodingService<SafeTransactions.SafeExecTransaction>(tx.SafeAddress,executor).GetCallData(message);
        return TransactionDraft.ContractCall(executor,tx.SafeAddress,"0x"+Convert.ToHexString(data).ToLowerInvariant());
    }
}
