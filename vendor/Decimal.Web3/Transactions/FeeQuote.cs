using System.Numerics;

namespace Decimal.Web3.Transactions;

/// <summary>Network fee estimate and maximum reserved amount before signing.</summary>
public sealed record FeeQuote(
    TransactionDraft Draft,
    BigInteger EstimatedGas,
    BigInteger GasLimit,
    BigInteger GasPriceWei,
    BigInteger NativeBalanceWei,
    bool Exact)
{
    public BigInteger ValueWei => Draft.ValueWei;
    public BigInteger EstimatedFeeWei => EstimatedGas * GasPriceWei;
    public BigInteger MaximumFeeWei => GasLimit * GasPriceWei;
    public BigInteger RequiredWei => ValueWei + MaximumFeeWei;
    public BigInteger MissingWei => BigInteger.Max(BigInteger.Zero, RequiredWei - NativeBalanceWei);
    public bool CanAfford => MissingWei.IsZero;
    public string EstimatedFeeDel => Units.Format(EstimatedFeeWei);
    public string MaximumFeeDel => Units.Format(MaximumFeeWei);
}
