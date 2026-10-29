using System.Numerics;
using Decimal.Web3.Transactions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.MessageEncodingServices;

namespace Decimal.Web3.Bridge;

/// <summary>Unsigned bridge-v2 calls. Supply a verified deployment address for the selected network.</summary>
public sealed class BridgeDrafts
{
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - BigInteger.One;
    private readonly string _contract;

    public BridgeDrafts(string contract)
    {
        TransactionDraft.ValidateAddress(contract, nameof(contract));
        _contract = contract;
    }

    public TransactionDraft BuildTransferNative(
        string from, string recipient, BigInteger amountWei, BigInteger serviceFeeWei,
        ushort targetChainId, BigInteger nonce)
    {
        RequireUint(amountWei, nameof(amountWei));
        RequireUint(serviceFeeWei, nameof(serviceFeeWei));
        RequireUint(nonce, nameof(nonce));
        var total = amountWei + serviceFeeWei;
        RequireUint(total, nameof(amountWei));
        return Encode(from, new BridgeNativeFunction
        {
            Recipient = AddressAsBytes32(recipient),
            TargetChainId = targetChainId,
            BridgeNonce = nonce,
            ServiceFee = serviceFeeWei
        }, total);
    }

    public TransactionDraft BuildTransferToken(
        string from, string token, string recipient, BigInteger amountRaw,
        BigInteger serviceFeeWei, ushort targetChainId, BigInteger nonce)
    {
        TransactionDraft.ValidateAddress(token, nameof(token));
        RequireUint(amountRaw, nameof(amountRaw));
        RequireUint(serviceFeeWei, nameof(serviceFeeWei));
        RequireUint(nonce, nameof(nonce));
        return Encode(from, new BridgeTokenFunction
        {
            Token = token,
            Recipient = AddressAsBytes32(recipient),
            Amount = amountRaw,
            TargetChainId = targetChainId,
            BridgeNonce = nonce
        }, serviceFeeWei);
    }

    public TransactionDraft BuildCompleteTransfer(string from, byte[] encodedVm, bool unwrapWrappedDel)
    {
        ArgumentNullException.ThrowIfNull(encodedVm);
        if (encodedVm.Length == 0) throw new ArgumentException("Encoded VM is required", nameof(encodedVm));
        return Encode(from, new BridgeCompleteFunction
        {
            EncodedVm = (byte[])encodedVm.Clone(),
            Unwrap = unwrapWrappedDel
        });
    }

    private TransactionDraft Encode<T>(string from, T function, BigInteger valueWei = default)
        where T : FunctionMessage, new()
    {
        TransactionDraft.ValidateAddress(from, nameof(from));
        var encoder = new FunctionMessageEncodingService<T>(_contract, from);
        var data = "0x" + Convert.ToHexString(encoder.GetCallData(function)).ToLowerInvariant();
        return TransactionDraft.ContractCall(from, _contract, data, valueWei);
    }

    private static byte[] AddressAsBytes32(string address)
    {
        TransactionDraft.ValidateAddress(address, nameof(address));
        var result = new byte[32];
        Convert.FromHexString(address[2..]).CopyTo(result, 12);
        return result;
    }

    private static void RequireUint(BigInteger value, string parameter)
    {
        if (value < 0 || value > MaxUint256) throw new ArgumentOutOfRangeException(parameter);
    }
}

[Function("wrapAndTransferETH")]
public sealed class BridgeNativeFunction : FunctionMessage
{
    [Parameter("bytes32", "recipient", 1)] public byte[] Recipient { get; set; } = new byte[32];
    [Parameter("uint16", "toChainId", 2)] public ushort TargetChainId { get; set; }
    [Parameter("uint256", "nonce", 3)] public BigInteger BridgeNonce { get; set; }
    [Parameter("uint256", "serviceFee", 4)] public BigInteger ServiceFee { get; set; }
}

[Function("transferTokens")]
public sealed class BridgeTokenFunction : FunctionMessage
{
    [Parameter("address", "token", 1)] public string Token { get; set; } = string.Empty;
    [Parameter("bytes32", "recipient", 2)] public byte[] Recipient { get; set; } = new byte[32];
    [Parameter("uint256", "amount", 3)] public BigInteger Amount { get; set; }
    [Parameter("uint16", "toChainId", 4)] public ushort TargetChainId { get; set; }
    [Parameter("uint256", "nonce", 5)] public BigInteger BridgeNonce { get; set; }
}

[Function("completeTransfer")]
public sealed class BridgeCompleteFunction : FunctionMessage
{
    [Parameter("bytes", "encodedVM", 1)] public byte[] EncodedVm { get; set; } = [];
    [Parameter("bool", "unwrapWETH", 2)] public bool Unwrap { get; set; }
}
