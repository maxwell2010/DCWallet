using System.Numerics;
using Decimal.Web3.Transactions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;

namespace Decimal.Web3.Bridge;

/// <summary>Read-only bridge-v2 service fee lookup for a caller-supplied deployment.</summary>
public sealed class BridgeQueries
{
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - BigInteger.One;
    private readonly Nethereum.Web3.Web3 _web3;
    private readonly string _contract;

    public BridgeQueries(Nethereum.Web3.Web3 web3, string contract)
    {
        _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
        TransactionDraft.ValidateAddress(contract, nameof(contract));
        _contract = contract;
    }

    public Task<BigInteger> GetMinimumServiceFeeAsync(ushort targetChainId, BlockParameter? block = null) =>
        _web3.Eth.GetContractQueryHandler<BridgeMinimumServiceFeeFunction>().QueryAsync<BigInteger>(
            _contract, new BridgeMinimumServiceFeeFunction { TargetChainId = targetChainId },
            block ?? BlockParameter.CreateLatest());

    /// <summary>Combines the contract's reported minimum fee with the native transfer value rule. This does not verify route availability or settlement.</summary>
    public async Task<BridgeFeeQuote> QuoteNativeAsync(
        ushort targetChainId, BigInteger amountWei, BlockParameter? block = null)
    {
        RequireUint(amountWei, nameof(amountWei));
        var minimumFee = await GetMinimumServiceFeeAsync(targetChainId, block).ConfigureAwait(false);
        return BridgeFeeQuote.Native(targetChainId, amountWei, minimumFee);
    }

    /// <summary>For token transfers only the reported service fee is sent as native value. Token allowance is not checked.</summary>
    public async Task<BridgeFeeQuote> QuoteTokenAsync(
        ushort targetChainId, BlockParameter? block = null)
    {
        var minimumFee = await GetMinimumServiceFeeAsync(targetChainId, block).ConfigureAwait(false);
        return BridgeFeeQuote.Token(targetChainId, minimumFee);
    }

    private static void RequireUint(BigInteger value, string parameter)
    {
        if (value < 0 || value > MaxUint256) throw new ArgumentOutOfRangeException(parameter);
    }
}

/// <summary>A snapshot of the reported minimum service fee and call value computed from it, not a settlement guarantee.</summary>
public sealed record BridgeFeeQuote(ushort TargetChainId, BigInteger MinimumServiceFeeWei, BigInteger CallValueAtReportedMinimumWei)
{
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - BigInteger.One;

    public static BridgeFeeQuote Native(ushort targetChainId, BigInteger amountWei, BigInteger minimumServiceFeeWei)
    {
        RequireUint(amountWei, nameof(amountWei));
        RequireUint(minimumServiceFeeWei, nameof(minimumServiceFeeWei));
        var value = amountWei + minimumServiceFeeWei;
        RequireUint(value, nameof(amountWei));
        return new(targetChainId, minimumServiceFeeWei, value);
    }

    public static BridgeFeeQuote Token(ushort targetChainId, BigInteger minimumServiceFeeWei)
    {
        RequireUint(minimumServiceFeeWei, nameof(minimumServiceFeeWei));
        return new(targetChainId, minimumServiceFeeWei, minimumServiceFeeWei);
    }

    private static void RequireUint(BigInteger value, string parameter)
    {
        if (value < 0 || value > MaxUint256) throw new ArgumentOutOfRangeException(parameter);
    }
}

[Function("minimalServiceFees", "uint256")]
public sealed class BridgeMinimumServiceFeeFunction : FunctionMessage
{
    [Parameter("uint16", "toChainId", 1)] public ushort TargetChainId { get; set; }
}
