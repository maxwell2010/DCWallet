using System.Globalization;
using System.Numerics;

namespace Decimal.Web3;

/// <summary>Exact conversion between display amounts and integer chain units.</summary>
public static class Units
{
    public static BigInteger Parse(string amount, int decimals = 18)
    {
        if (decimals is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(decimals));
        if (string.IsNullOrWhiteSpace(amount)) throw new FormatException("Amount is empty");
        var parts = amount.Trim().Split('.');
        if (parts.Length is < 1 or > 2 || parts[0].Length == 0 ||
            !parts[0].All(char.IsAsciiDigit) ||
            (parts.Length == 2 && (parts[1].Length == 0 || !parts[1].All(char.IsAsciiDigit))))
            throw new FormatException("Amount must be a non-negative decimal string");
        var fraction = parts.Length == 2 ? parts[1] : "";
        if (fraction.Length > decimals) throw new FormatException("Amount exceeds token precision");
        var whole = BigInteger.Parse(parts[0], CultureInfo.InvariantCulture);
        var scale = BigInteger.Pow(10, decimals);
        return whole * scale +
            (fraction.Length == 0 ? BigInteger.Zero :
                BigInteger.Parse(fraction.PadRight(decimals, '0'), CultureInfo.InvariantCulture));
    }

    public static string Format(BigInteger raw, int decimals = 18)
    {
        if (decimals is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(decimals));
        var scale = BigInteger.Pow(10, decimals);
        var sign = raw.Sign < 0 ? "-" : "";
        var absolute = BigInteger.Abs(raw);
        var whole = absolute / scale;
        if (decimals == 0) return sign + whole.ToString(CultureInfo.InvariantCulture);
        var fraction = (absolute % scale).ToString(CultureInfo.InvariantCulture).PadLeft(decimals, '0').TrimEnd('0');
        return sign + whole.ToString(CultureInfo.InvariantCulture) +
            (fraction.Length == 0 ? "" : "." + fraction);
    }
}
