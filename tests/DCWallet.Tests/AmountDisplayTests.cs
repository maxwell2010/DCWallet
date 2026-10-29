using System.Numerics;
using DCWallet.Core;
using Decimal.Web3;
using Xunit;
namespace DCWallet.Tests;
public sealed class AmountDisplayTests
{
    [Theory]
    [InlineData("13.876309661736049615", "13.8763")]
    [InlineData("29.363639950752258304", "29.3636")]
    [InlineData("999.99999", "1\u202f000")]
    [InlineData("0.000012345678", "0.00001235")]
    [InlineData("0.000000000000000001", "< 0.000000000001")]
    [InlineData("0", "0")]
    public void RoundedDisplayPreservesExactSource(string exact, string expected)
    {
        var raw = Units.Parse(exact); Assert.Equal(expected, AmountDisplay.Compact(raw)); Assert.Equal(exact, Units.Format(raw));
    }
    [Fact] public void HugeUint256AndNegativeAmountsNeverUseFloatingPoint()
    {
        var raw = (BigInteger.One << 256) - 1; Assert.DoesNotContain("E", AmountDisplay.Compact(raw)); Assert.StartsWith("-", AmountDisplay.Compact(-raw));
    }
}
