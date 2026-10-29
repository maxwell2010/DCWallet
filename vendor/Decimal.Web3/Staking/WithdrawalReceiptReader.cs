using System.Numerics;
using Decimal.Web3.Transactions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;

namespace Decimal.Web3.Staking;

/// <summary>A withdrawal request emitted by the delegation contract.</summary>
public sealed record WithdrawalReceipt(byte[] StakeId, FrozenStake FrozenStake)
{
    public BigInteger StakeIndex => FrozenStake.Index;
    public DelegationStake Stake => FrozenStake.Stake;
    public BigInteger UnfreezeTimestamp => FrozenStake.UnfreezeTimestamp;
}

// decimalteam/go-smart-node contracts/delegation/DecimalDelegation.json at 9e6c6d7.
[Event("WithdrawRequest")]
public sealed class WithdrawRequestEventDto : IEventDTO
{
    [Parameter("bytes32", "stakeId", 1, false)]
    public byte[] StakeId { get; set; } = Array.Empty<byte>();

    [Parameter("uint256", "stakeIndex", 2, false)]
    public BigInteger StakeIndex { get; set; }

    [Parameter("tuple", "frozenStake", 3, false)]
    public FrozenStakeDto FrozenStake { get; set; } = new();
}

// The deployed testnet event has one additional uint256 inside its Stake tuple.
[Event("WithdrawRequest")]
public sealed class WithdrawRequestExtendedEventDto : IEventDTO
{
    [Parameter("bytes32", "stakeId", 1, false)]
    public byte[] StakeId { get; set; } = Array.Empty<byte>();

    [Parameter("uint256", "stakeIndex", 2, false)]
    public BigInteger StakeIndex { get; set; }

    [Parameter("tuple", "frozenStake", 3, false)]
    public FrozenStakeWithExtendedStakeDto FrozenStake { get; set; } = new();
}

public static class WithdrawalReceiptReader
{
    /// <summary>Read all withdrawal requests from one mined transaction receipt.</summary>
    public static async Task<IReadOnlyList<WithdrawalReceipt>> ReadAsync(
        Nethereum.Web3.Web3 web3, string delegationContract, string transactionHash)
    {
        ArgumentNullException.ThrowIfNull(web3);
        TransactionDraft.ValidateAddress(delegationContract, nameof(delegationContract));
        ValidateHash(transactionHash);

        var receipt = await web3.Eth.Transactions.GetTransactionReceipt
            .SendRequestAsync(transactionHash).ConfigureAwait(false);
        if (receipt is null)
            throw new InvalidOperationException("Transaction receipt is not available yet");
        return DecodeReceipt(receipt, delegationContract);
    }

    /// <summary>Decode a receipt already obtained from an RPC, useful for offline processing.</summary>
    public static IReadOnlyList<WithdrawalReceipt> DecodeReceipt(
        TransactionReceipt receipt, string delegationContract)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        TransactionDraft.ValidateAddress(delegationContract, nameof(delegationContract));
        if (receipt.Status?.Value == BigInteger.Zero)
            throw new InvalidOperationException("Transaction reverted");

        var result = new List<WithdrawalReceipt>();
        foreach (var decoded in receipt.DecodeAllEvents<WithdrawRequestEventDto>())
        {
            if (!string.Equals(decoded.Log.Address, delegationContract, StringComparison.OrdinalIgnoreCase))
                continue;
            var value = decoded.Event;
            if (value.StakeId is null || value.StakeId.Length != 32 || value.FrozenStake is null)
                throw new InvalidOperationException("Incomplete WithdrawRequest event");
            result.Add(new WithdrawalReceipt(
                value.StakeId, FrozenStake.FromContract(value.StakeIndex, value.FrozenStake)));
        }
        foreach (var decoded in receipt.DecodeAllEvents<WithdrawRequestExtendedEventDto>())
        {
            if (!string.Equals(decoded.Log.Address, delegationContract, StringComparison.OrdinalIgnoreCase))
                continue;
            var value = decoded.Event;
            if (value.StakeId is null || value.StakeId.Length != 32 || value.FrozenStake is null)
                throw new InvalidOperationException("Incomplete WithdrawRequest event");
            result.Add(new WithdrawalReceipt(
                value.StakeId, FrozenStake.FromContract(value.StakeIndex, value.FrozenStake)));
        }
        return result;
    }

    private static void ValidateHash(string transactionHash)
    {
        if (transactionHash is null || transactionHash.Length != 66 ||
            !transactionHash.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            !transactionHash.AsSpan(2).ToString().All(Uri.IsHexDigit))
            throw new ArgumentException("Expected a 32-byte transaction hash", nameof(transactionHash));
    }
}
