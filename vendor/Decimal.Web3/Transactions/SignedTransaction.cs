using System.Numerics;
using Nethereum.Util;

namespace Decimal.Web3.Transactions;

public sealed record SignedTransaction(
    string RawHex,
    string From,
    string To,
    BigInteger Nonce,
    BigInteger GasLimit,
    BigInteger GasPriceWei)
{
    /// <summary>Locally computed transaction hash, available even if the broadcast response is lost.</summary>
    public string Hash
    {
        get
        {
            var hex = RawHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? RawHex[2..] : RawHex;
            return "0x" + Convert.ToHexString(
                Sha3Keccack.Current.CalculateHash(Convert.FromHexString(hex))).ToLowerInvariant();
        }
    }
}

public sealed record TransactionResult(
    string Hash,
    BigInteger? BlockNumber,
    bool? Success,
    BigInteger? GasUsed,
    BigInteger? EffectiveGasPriceWei)
{
    public BigInteger? PaidFeeWei => GasUsed is null || EffectiveGasPriceWei is null
        ? null : GasUsed.Value * EffectiveGasPriceWei.Value;
    public string? PaidFeeDel => PaidFeeWei is { } fee ? Units.Format(fee) : null;
}
