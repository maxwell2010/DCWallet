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
                value.Stake.HoldTimestamp),
            value.FreezeStatus, value.FreezeType, value.UnfreezeTimestamp);
    }
}

public sealed class FrozenStakeReader
{
    public const int MaxBatchSize = 128;
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - BigInteger.One;

    private readonly Nethereum.Web3.Web3 _web3;
    private readonly string _delegationAddress;
    private readonly SemaphoreSlim _layoutGate = new(1, 1);
    private bool? _hasExtendedStake;

    public FrozenStakeReader(Nethereum.Web3.Web3 web3, string delegationAddress)
    {
        _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
        TransactionDraft.ValidateAddress(delegationAddress, nameof(delegationAddress));
        _delegationAddress = delegationAddress;
    }

    public async Task<FrozenStake> GetFrozenStakeAsync(BigInteger index, BlockParameter? block = null)
    {
        ValidateIndex(index);
        var hasExtendedStake = await HasExtendedStakeAsync(block).ConfigureAwait(false);
        var query = _web3.Eth.GetContractQueryHandler<GetFrozenStakeFunction>();
        if (hasExtendedStake)
        {
            var legacy = await query.QueryDeserializingToObjectAsync<GetFrozenStakeExtendedOutput>(
                new GetFrozenStakeFunction { Index = index }, _delegationAddress,
                block ?? BlockParameter.CreateLatest()).ConfigureAwait(false);
            if (legacy?.FrozenStake is null)
                throw new InvalidOperationException("Missing getFrozenStake contract response");
            return FrozenStake.FromContract(index, legacy.FrozenStake);
        }
        var result = await query.QueryDeserializingToObjectAsync<GetFrozenStakeOutput>(
            new GetFrozenStakeFunction { Index = index }, _delegationAddress,
            block ?? BlockParameter.CreateLatest()).ConfigureAwait(false);
        if (result?.FrozenStake is null)
            throw new InvalidOperationException("Missing getFrozenStake contract response");
        return FrozenStake.FromContract(index, result.FrozenStake);
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

        var hasExtendedStake = await HasExtendedStakeAsync(block).ConfigureAwait(false);

        var query = _web3.Eth.GetContractQueryHandler<GetFrozenStakesFunction>();
        if (hasExtendedStake)
        {
            var legacy = await query.QueryDeserializingToObjectAsync<GetFrozenStakesExtendedOutput>(
                new GetFrozenStakesFunction { StakeIndexes = requested }, _delegationAddress,
                block ?? BlockParameter.CreateLatest()).ConfigureAwait(false);
            if (legacy?.FrozenStakes is null || legacy.FrozenStakes.Count != requested.Count)
                throw new InvalidOperationException("Unexpected getFrozenStakes contract response length");
            var decoded = new FrozenStake[requested.Count];
            for (var i = 0; i < decoded.Length; i++)
                decoded[i] = FrozenStake.FromContract(requested[i], legacy.FrozenStakes[i]);
            return decoded;
        }
        var result = await query.QueryDeserializingToObjectAsync<GetFrozenStakesOutput>(
            new GetFrozenStakesFunction { StakeIndexes = requested }, _delegationAddress,
            block ?? BlockParameter.CreateLatest()).ConfigureAwait(false);
        if (result?.FrozenStakes is null || result.FrozenStakes.Count != requested.Count)
            throw new InvalidOperationException("Unexpected getFrozenStakes contract response length");

        var stakes = new FrozenStake[requested.Count];
        for (var i = 0; i < stakes.Length; i++)
            stakes[i] = FrozenStake.FromContract(requested[i], result.FrozenStakes[i]);
        return stakes;
    }

    private async Task<bool> HasExtendedStakeAsync(BlockParameter? block)
    {
        // Historical blocks may predate an ABI change, so never reuse the latest-block layout.
        if (block is not null)
            return await ProbeLayoutAsync(block).ConfigureAwait(false);
        if (_hasExtendedStake is { } cached) return cached;
        await _layoutGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_hasExtendedStake is { } checkedLayout) return checkedLayout;
            _hasExtendedStake = await ProbeLayoutAsync(BlockParameter.CreateLatest()).ConfigureAwait(false);
            return _hasExtendedStake.Value;
        }
        finally
        {
            _layoutGate.Release();
        }
    }

    private async Task<bool> ProbeLayoutAsync(BlockParameter block)
    {
        var encoder = new FunctionMessageEncodingService<GetFrozenStakeFunction>(_delegationAddress);
        var data = "0x" + Convert.ToHexString(encoder.GetCallData(
            new GetFrozenStakeFunction { Index = BigInteger.Zero })).ToLowerInvariant();
        var raw = await _web3.Eth.Transactions.Call.SendRequestAsync(
            new CallInput { To = _delegationAddress, Data = data }, block).ConfigureAwait(false);
        return raw?.Length switch
        {
            2 + 64 * 10 => false,
            2 + 64 * 11 => true,
            _ => throw new InvalidOperationException("Unknown getFrozenStake response layout")
        };
    }

    private static void ValidateIndex(BigInteger index)
    {
        if (index < BigInteger.Zero || index > MaxUint256)
            throw new ArgumentOutOfRangeException(nameof(index), "Index must fit uint256");
    }
}
