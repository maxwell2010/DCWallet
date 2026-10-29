using System.Numerics;
using System.Text;
using Decimal.Web3.Transactions;
using Decimal.Web3.Wallet;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.MessageEncodingServices;
using Nethereum.Signer;
using Nethereum.Util;
using HdWallet = Nethereum.HdWallet.Wallet;

namespace Decimal.Web3.Multisig;

/// <summary>Offline EIP-712 SafeTx hashing, EOA signatures, and execTransaction calldata.</summary>
public static class SafeTransactions
{
    private static readonly byte[] DomainTypeHash = Keccak(Encoding.ASCII.GetBytes(
        "EIP712Domain(uint256 chainId,address verifyingContract)"));
    private static readonly byte[] SafeTxTypeHash = Keccak(Encoding.ASCII.GetBytes(
        "SafeTx(address to,uint256 value,bytes data,uint8 operation,uint256 safeTxGas,uint256 baseGas,uint256 gasPrice,address gasToken,address refundReceiver,uint256 nonce)"));
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - 1;

    public static string CalculateHash(SafeTransactionDraft transaction, BigInteger chainId) =>
        "0x" + Convert.ToHexString(HashBytes(transaction, chainId)).ToLowerInvariant();

    /// <summary>Builds an unsigned owner call to approve this SafeTx hash on-chain.</summary>
    public static TransactionDraft BuildApproveHashDraft(string owner, SafeTransactionDraft transaction,
        BigInteger chainId)
    {
        TransactionDraft.ValidateAddress(owner, nameof(owner));
        var hash = HashBytes(transaction, chainId);
        var encoder = new FunctionMessageEncodingService<SafeApproveHash>(transaction.SafeAddress, owner);
        var data = "0x" + Convert.ToHexString(encoder.GetCallData(new SafeApproveHash
        {
            HashToApprove = hash
        })).ToLowerInvariant();
        return TransactionDraft.ContractCall(owner, transaction.SafeAddress, data);
    }

    /// <summary>Signs the SafeTx digest directly, without an EIP-191 personal-message prefix.</summary>
    public static SafeSignature SignTypedData(SafeTransactionDraft transaction, BigInteger chainId, EthECKey ownerKey)
    {
        ArgumentNullException.ThrowIfNull(ownerKey);
        var digest = HashBytes(transaction, chainId);
        return new SafeSignature(ownerKey.GetPublicAddress(), new MessageSigner().Sign(digest, ownerKey));
    }

    /// <summary>Derives an owner from a BIP39 mnemonic for a single offline SafeTx signature.</summary>
    public static SafeSignature SignTypedDataFromMnemonic(SafeTransactionDraft transaction, BigInteger chainId,
        string mnemonic, int index = 0, string passphrase = "")
    {
        var digest = HashBytes(transaction, chainId);
        if (!DecimalWallet.IsValidMnemonic(mnemonic))
            throw new ArgumentException("Invalid English BIP39 mnemonic.", nameof(mnemonic));
        ArgumentNullException.ThrowIfNull(passphrase);
        if (index is < 0 or > 9) throw new ArgumentOutOfRangeException(nameof(index));
        var ownerKey = new HdWallet(mnemonic.Trim(), passphrase, HdWallet.DEFAULT_PATH).GetEthereumKey(index);
        return new SafeSignature(ownerKey.GetPublicAddress(), new MessageSigner().Sign(digest, ownerKey));
    }

    /// <summary>Sorts EOA signatures by owner address and rejects duplicates or signatures for another SafeTx.</summary>
    public static string PackEoaSignatures(SafeTransactionDraft transaction, BigInteger chainId,
        IEnumerable<SafeSignature> signatures)
    {
        ArgumentNullException.ThrowIfNull(signatures);
        var digest = HashBytes(transaction, chainId);
        var ordered = signatures.OrderBy(signature => signature.Signer, StringComparer.OrdinalIgnoreCase).ToArray();
        if (ordered.Length == 0) throw new ArgumentException("At least one signature is required.", nameof(signatures));

        var bytes = new byte[checked(ordered.Length * 65)];
        string? previous = null;
        for (var i = 0; i < ordered.Length; i++)
        {
            var signature = ordered[i];
            TransactionDraft.ValidateAddress(signature.Signer, nameof(signatures));
            if (string.Equals(previous, signature.Signer, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Duplicate Safe owner signature.", nameof(signatures));
            previous = signature.Signer;
            var raw = ParseHex(signature.Data, nameof(signatures));
            if (raw.Length != 65 || raw[64] is not (27 or 28))
                throw new ArgumentException("Expected a 65-byte EIP-712 EOA signature with v=27 or 28.", nameof(signatures));
            string recovered;
            try
            {
                recovered = new MessageSigner().EcRecover(digest, signature.Data);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException)
            {
                throw new ArgumentException("Invalid Safe owner signature.", nameof(signatures), exception);
            }
            if (!string.Equals(recovered, signature.Signer, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Signature does not match owner and SafeTx hash.", nameof(signatures));
            Buffer.BlockCopy(raw, 0, bytes, i * 65, 65);
        }
        return "0x" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>Builds an unsigned outer EOA transaction. It does not check threshold, nonce, or Safe deployment.</summary>
    public static TransactionDraft BuildExecutionDraft(string executor, SafeTransactionDraft transaction,
        BigInteger chainId, IEnumerable<SafeSignature> signatures)
    {
        var packed = PackEoaSignatures(transaction, chainId, signatures);
        TransactionDraft.ValidateAddress(executor, nameof(executor));
        var encoder = new FunctionMessageEncodingService<SafeExecTransaction>(transaction.SafeAddress, executor);
        var data = "0x" + Convert.ToHexString(encoder.GetCallData(new SafeExecTransaction
        {
            To = transaction.To,
            Value = transaction.ValueWei,
            Data = ParseHex(transaction.Data, nameof(transaction)),
            Operation = transaction.Operation,
            SafeTxGas = transaction.SafeTxGas,
            BaseGas = transaction.BaseGas,
            SafeGasPrice = transaction.GasPriceWei,
            GasToken = transaction.GasToken,
            RefundReceiver = transaction.RefundReceiver,
            Signatures = ParseHex(packed, nameof(signatures))
        })).ToLowerInvariant();
        return TransactionDraft.ContractCall(executor, transaction.SafeAddress, data);
    }

    private static byte[] HashBytes(SafeTransactionDraft transaction, BigInteger chainId)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        if (chainId <= 0 || chainId > MaxUint256) throw new ArgumentOutOfRangeException(nameof(chainId));
        TransactionDraft.ValidateAddress(transaction.SafeAddress, nameof(transaction));
        TransactionDraft.ValidateAddress(transaction.To, nameof(transaction));
        TransactionDraft.ValidateAddress(transaction.GasToken, nameof(transaction));
        TransactionDraft.ValidateAddress(transaction.RefundReceiver, nameof(transaction));
        if (transaction.Operation != 0) throw new NotSupportedException("Only Safe Call operation 0 is supported.");
        var data = ParseHex(transaction.Data, nameof(transaction));

        var domainHash = Keccak(Combine(DomainTypeHash, Word(chainId), Word(transaction.SafeAddress)));
        var txHash = Keccak(Combine(SafeTxTypeHash, Word(transaction.To), Word(transaction.ValueWei),
            Keccak(data), Word(transaction.Operation), Word(transaction.SafeTxGas), Word(transaction.BaseGas),
            Word(transaction.GasPriceWei), Word(transaction.GasToken), Word(transaction.RefundReceiver), Word(transaction.Nonce)));
        return Keccak(Combine([0x19, 0x01], domainHash, txHash));
    }

    private static byte[] Word(BigInteger value)
    {
        if (value < 0 || value > MaxUint256) throw new ArgumentOutOfRangeException(nameof(value));
        var result = new byte[32];
        var raw = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        raw.CopyTo(result.AsSpan(32 - raw.Length));
        return result;
    }

    private static byte[] Word(string address)
    {
        var word = new byte[32];
        Convert.FromHexString(address[2..]).CopyTo(word, 12);
        return word;
    }

    private static byte[] ParseHex(string hex, string parameter)
    {
        if (hex is null || !hex.StartsWith("0x", StringComparison.Ordinal) || hex.Length % 2 != 0 ||
            !hex.AsSpan(2).ToString().All(Uri.IsHexDigit))
            throw new ArgumentException("Expected even-length 0x-prefixed hex.", parameter);
        return Convert.FromHexString(hex[2..]);
    }

    private static byte[] Combine(params byte[][] parts)
    {
        var result = new byte[parts.Sum(part => part.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }
        return result;
    }

    private static byte[] Keccak(byte[] data) => Sha3Keccack.Current.CalculateHash(data);

    [Function("execTransaction", "bool")]
    public sealed class SafeExecTransaction : FunctionMessage
    {
        [Parameter("address", "to", 1)] public string To { get; set; } = string.Empty;
        [Parameter("uint256", "value", 2)] public BigInteger Value { get; set; }
        [Parameter("bytes", "data", 3)] public byte[] Data { get; set; } = [];
        [Parameter("uint8", "operation", 4)] public byte Operation { get; set; }
        [Parameter("uint256", "safeTxGas", 5)] public BigInteger SafeTxGas { get; set; }
        [Parameter("uint256", "baseGas", 6)] public BigInteger BaseGas { get; set; }
        [Parameter("uint256", "gasPrice", 7)] public BigInteger SafeGasPrice { get; set; }
        [Parameter("address", "gasToken", 8)] public string GasToken { get; set; } = string.Empty;
        [Parameter("address", "refundReceiver", 9)] public string RefundReceiver { get; set; } = string.Empty;
        [Parameter("bytes", "signatures", 10)] public byte[] Signatures { get; set; } = [];
    }

    [Function("approveHash")]
    public sealed class SafeApproveHash : FunctionMessage
    {
        [Parameter("bytes32", "hashToApprove", 1)] public byte[] HashToApprove { get; set; } = [];
    }
}
