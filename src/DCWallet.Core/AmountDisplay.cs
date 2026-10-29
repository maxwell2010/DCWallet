using System.Numerics;
using Decimal.Web3;
namespace DCWallet.Core;
/// <summary>Display-only rounding. Inputs, quotes and signing always retain the original integers.</summary>
public static class AmountDisplay
{
    public static string Compact(BigInteger raw, int decimals = 18)
    {
        if (decimals is < 0 or > 36) throw new ArgumentOutOfRangeException(nameof(decimals));
        var negative = raw < 0; var absolute = BigInteger.Abs(raw); var places = Math.Min(4, decimals);
        var exact = Units.Format(absolute, decimals);
        if (absolute > 0 && absolute < BigInteger.Pow(10, decimals))
        {
            var fraction = exact.Split('.').ElementAtOrDefault(1) ?? ""; var zeros = fraction.TakeWhile(c => c == '0').Count();
            places = Math.Min(decimals, Math.Min(12, zeros + 4));
        }
        var factor = BigInteger.Pow(10, decimals - places); var rounded = (absolute + factor / 2) / factor;
        if (absolute > 0 && rounded == 0) return (negative ? "> -" : "< ") + Units.Format(BigInteger.One, places);
        var text = Units.Format(rounded, places); var parts = text.Split('.'); var whole = parts[0];
        for (var i = whole.Length - 3; i > 0; i -= 3) whole = whole.Insert(i, "\u202f");
        return (negative ? "-" : "") + whole + (parts.Length > 1 ? "." + parts[1] : "");
    }
}
