using Decimal.Web3.Transactions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;

namespace Decimal.Web3.Checks;

public sealed record RedeemedCheck(byte[] CheckHash, byte Status);

[Event("RedeemCheck")]
public sealed class RedeemCheckEventDto : IEventDTO
{
    [Parameter("bytes32", "checkHash", 1, false)] public byte[] CheckHash { get; set; } = [];
    [Parameter("uint8", "status", 2, false)] public byte Status { get; set; }
}

public static class ChecksReceiptReader
{
    public static async Task<IReadOnlyList<RedeemedCheck>> ReadRedeemedAsync(
        Nethereum.Web3.Web3 web3, string checksContract, string transactionHash)
    {
        ArgumentNullException.ThrowIfNull(web3);
        TransactionDraft.ValidateAddress(checksContract, nameof(checksContract));
        if (transactionHash is null || transactionHash.Length != 66 ||
            !transactionHash.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            !transactionHash.AsSpan(2).ToString().All(Uri.IsHexDigit))
            throw new ArgumentException("Expected a 32-byte transaction hash", nameof(transactionHash));
        var receipt = await web3.Eth.Transactions.GetTransactionReceipt
            .SendRequestAsync(transactionHash).ConfigureAwait(false);
        if (receipt is null)
            throw new InvalidOperationException("Transaction receipt is not available yet");
        return DecodeRedeemed(receipt, checksContract);
    }

    public static IReadOnlyList<RedeemedCheck> DecodeRedeemed(TransactionReceipt receipt, string checksContract)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        TransactionDraft.ValidateAddress(checksContract, nameof(checksContract));
        if (receipt.Status?.Value == 0)
            throw new InvalidOperationException("Transaction reverted");
        var result = new List<RedeemedCheck>();
        foreach (var decoded in receipt.DecodeAllEvents<RedeemCheckEventDto>())
        {
            if (!string.Equals(decoded.Log.Address, checksContract, StringComparison.OrdinalIgnoreCase))
                continue;
            result.Add(new RedeemedCheck((byte[])decoded.Event.CheckHash.Clone(), decoded.Event.Status));
        }
        return result;
    }
}
