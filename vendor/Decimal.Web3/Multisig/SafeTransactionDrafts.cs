using System.Numerics;
using Decimal.Web3.Transactions;
using Nethereum.Contracts;
using Nethereum.Contracts.MessageEncodingServices;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Decimal.Web3.Multisig;

/// <summary>Offline SafeTx drafts for standard Call operations. No RPC, signing, or deployment.</summary>
public static class SafeTransactionDrafts
{
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - 1;

    public static SafeTransactionDraft BuildDel(string safeAddress, string recipient, BigInteger amountWei,
        BigInteger nonce) => BuildContractCall(safeAddress, recipient, amountWei, "0x", nonce);

    public static SafeTransactionDraft BuildErc20Transfer(string safeAddress, string token, string recipient,
        BigInteger amountRaw, BigInteger nonce)
    {
        TransactionDraft.ValidateAddress(recipient, nameof(recipient));
        RequirePositiveUint256(amountRaw, nameof(amountRaw));
        return BuildContractCall(safeAddress, token, 0,
            Encode(token, safeAddress, new SafeErc20Transfer { Recipient = recipient, Amount = amountRaw }), nonce);
    }

    public static SafeTransactionDraft BuildErc721Transfer(string safeAddress, string collection,
        string recipient, BigInteger tokenId, BigInteger nonce)
    {
        TransactionDraft.ValidateAddress(recipient, nameof(recipient));
        RequireUint256(tokenId, nameof(tokenId));
        return BuildContractCall(safeAddress, collection, 0,
            Encode(collection, safeAddress, new SafeErc721Transfer
            {
                From = safeAddress, Recipient = recipient, TokenId = tokenId, Data = []
            }), nonce);
    }

    public static SafeTransactionDraft BuildErc1155Transfer(string safeAddress, string collection,
        string recipient, BigInteger tokenId, BigInteger amountRaw, BigInteger nonce)
    {
        TransactionDraft.ValidateAddress(recipient, nameof(recipient));
        RequireUint256(tokenId, nameof(tokenId));
        RequirePositiveUint256(amountRaw, nameof(amountRaw));
        return BuildContractCall(safeAddress, collection, 0,
            Encode(collection, safeAddress, new SafeErc1155Transfer
            {
                From = safeAddress, Recipient = recipient, TokenId = tokenId,
                Amount = amountRaw, Data = []
            }), nonce);
    }

    /// <summary>Wrap an already ABI-encoded call. Only Safe operation 0 (Call) is supported.</summary>
    public static SafeTransactionDraft BuildContractCall(string safeAddress, string to,
        BigInteger valueWei, string data, BigInteger nonce)
    {
        TransactionDraft.ValidateAddress(safeAddress, nameof(safeAddress));
        TransactionDraft.ValidateAddress(to, nameof(to));
        RequireUint256(valueWei, nameof(valueWei));
        RequireUint256(nonce, nameof(nonce));
        if (data is null || !data.StartsWith("0x", StringComparison.Ordinal) ||
            data.Length % 2 != 0 || !data.AsSpan(2).ToString().All(Uri.IsHexDigit))
            throw new ArgumentException("Data must be an even-length hex string", nameof(data));

        return new SafeTransactionDraft(safeAddress, to, valueWei, data, 0, 0, 0, 0,
            SafeTransactionDraft.ZeroAddress, SafeTransactionDraft.ZeroAddress, nonce);
    }

    private static string Encode<T>(string contract, string safeAddress, T function)
        where T : FunctionMessage, new()
    {
        TransactionDraft.ValidateAddress(contract, nameof(contract));
        TransactionDraft.ValidateAddress(safeAddress, nameof(safeAddress));
        var encoder = new FunctionMessageEncodingService<T>(contract, safeAddress);
        return "0x" + Convert.ToHexString(encoder.GetCallData(function)).ToLowerInvariant();
    }

    private static void RequireUint256(BigInteger value, string name)
    {
        if (value < 0 || value > MaxUint256) throw new ArgumentOutOfRangeException(name);
    }

    private static void RequirePositiveUint256(BigInteger value, string name)
    {
        if (value <= 0 || value > MaxUint256) throw new ArgumentOutOfRangeException(name);
    }

    [Function("transfer", "bool")]
    public sealed class SafeErc20Transfer : FunctionMessage
    {
        [Parameter("address", "to", 1)] public string Recipient { get; set; } = string.Empty;
        [Parameter("uint256", "amount", 2)] public BigInteger Amount { get; set; }
    }

    [Function("safeTransferFrom")]
    public sealed class SafeErc721Transfer : FunctionMessage
    {
        [Parameter("address", "from", 1)] public string From { get; set; } = string.Empty;
        [Parameter("address", "to", 2)] public string Recipient { get; set; } = string.Empty;
        [Parameter("uint256", "tokenId", 3)] public BigInteger TokenId { get; set; }
        [Parameter("bytes", "data", 4)] public byte[] Data { get; set; } = [];
    }

    [Function("safeTransferFrom")]
    public sealed class SafeErc1155Transfer : FunctionMessage
    {
        [Parameter("address", "from", 1)] public string From { get; set; } = string.Empty;
        [Parameter("address", "to", 2)] public string Recipient { get; set; } = string.Empty;
        [Parameter("uint256", "id", 3)] public BigInteger TokenId { get; set; }
        [Parameter("uint256", "value", 4)] public BigInteger Amount { get; set; }
        [Parameter("bytes", "data", 5)] public byte[] Data { get; set; } = [];
    }
}
