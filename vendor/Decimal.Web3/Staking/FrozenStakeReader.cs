using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Decimal.Web3.Transactions;
using Nethereum.Contracts.MessageEncodingServices;
using Nethereum.RPC.Eth.DTOs;

namespace Decimal.Web3.Staking;

public sealed record DelegationStake(
    string Validator,
    string Delegator,
    string Token,
    BigInteger AmountRaw,
    BigInteger TokenId,
    byte TokenType,
    BigInteger HoldTimestamp)
{
    public BigInteger? HoldStartTime { get; init; }

    public bool Exists => AmountRaw > BigInteger.Zero;

    public static DelegationStake FromContract(DelegationStakeDto value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DelegationStake(value.Validator, value.Delegator, value.Token,
            value.Amount, value.TokenId, value.TokenType, value.HoldTimestamp);
    }
}

public sealed record FrozenStake(
    BigInteger Index,
    DelegationStake Stake,
    byte FreezeStatus,
    byte FreezeType,
    BigInteger UnfreezeTimestamp)
{
    // Status/type 1 is the active withdrawal interpretation used by the Python SDK.
    public bool IsActiveWithdrawal => Stake.Exists && FreezeStatus == 1 && FreezeType == 1;

    public bool IsMatured(BigInteger nowTimestamp) =>
        IsActiveWithdrawal && UnfreezeTimestamp > BigInteger.Zero && UnfreezeTimestamp <= nowTimestamp;

    public static FrozenStake FromContract(BigInteger index, FrozenStakeDto value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var stake = value.Stake ?? throw new ArgumentException("Missing stake tuple", nameof(value));
        return new FrozenStake(
            index,
            DelegationStake.FromContract(stake),
            value.FreezeStatus, value.FreezeType, value.UnfreezeTimestamp);
    }

    public static FrozenStake FromContract(BigInteger index, FrozenStakeWithExtendedStakeDto value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new FrozenStake(index, new DelegationStake(
                value.Stake.Validator, value.Stake.Delegator, value.Stake.Token,
                value.Stake.Amount, value.Stake.TokenId, value.Stake.TokenType,
                value.Stake.HoldTimestamp) { HoldStartTime = value.Stake.HoldStartTime },
            value.FreezeStatus, value.FreezeType, value.UnfreezeTimestamp);
    }
}

public sealed class FrozenStakeReader
{
    public const int MaxBatchSize = 128;
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - BigInteger.One;

    private readonly Nethereum.Web3.Web3 _web3;
    private readonly string _delegationAddress;
    public FrozenStakeReader(Nethereum.Web3.Web3 web3, string delegationAddress)
    {
        _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
        TransactionDraft.ValidateAddress(delegationAddress, nameof(delegationAddress));
        _delegationAddress = delegationAddress;
    }

    public async Task<FrozenStake> GetFrozenStakeAsync(BigInteger index, BlockParameter? block = null)
    {
        ValidateIndex(index);
        var raw = await CallAsync(new GetFrozenStakeFunction { Index = index }, block).ConfigureAwait(false);
        return StakeResponseDecoder.DecodeFrozen(raw, index);
    }

    // The official ABI declares this view; availability on a particular deployment is not verified here.
    public async Task<IReadOnlyList<FrozenStake>> GetFrozenStakesAsync(
        IReadOnlyList<BigInteger> indexes, BlockParameter? block = null)
    {
        ArgumentNullException.ThrowIfNull(indexes);
        if (indexes.Count > MaxBatchSize)
            throw new ArgumentOutOfRangeException(nameof(indexes), $"Batch exceeds {MaxBatchSize} indexes");
        if (indexes.Count == 0)
            return Array.Empty<FrozenStake>();

        var requested = new List<BigInteger>(indexes.Count);
        foreach (var index in indexes)
        {
            ValidateIndex(index);
            requested.Add(index);
        }

        var raw = await CallAsync(new GetFrozenStakesFunction { StakeIndexes = requested }, block).ConfigureAwait(false);
        return StakeResponseDecoder.DecodeFrozenBatch(raw, requested);
    }

    private Task<string> CallAsync<T>(T function, BlockParameter? block) where T : Nethereum.Contracts.FunctionMessage, new()
    {
        var data = "0x" + Convert.ToHexString(new FunctionMessageEncodingService<T>(_delegationAddress).GetCallData(function));
        return _web3.Eth.Transactions.Call.SendRequestAsync(
            new CallInput { To = _delegationAddress, Data = data }, block ?? BlockParameter.CreateLatest());
    }

    private static void ValidateIndex(BigInteger index)
    {
        if (index < BigInteger.Zero || index > MaxUint256)
            throw new ArgumentOutOfRangeException(nameof(index), "Index must fit uint256");
    }
}
