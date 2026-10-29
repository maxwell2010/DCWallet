using DCWallet.Core;
using Xunit;
namespace DCWallet.Tests;
public class OperationInputRulesTests
{
    [Fact] public void RawUint256RetainsFullPrecision() => OperationInputRules.Validate("{\"amountRaw\":\"115792089237316195423570985008687907853269984665640564039457584007913129639935\"}");
    [Theory]
    [InlineData("-1")]
    [InlineData("1.1")]
    [InlineData("1e18")]
    [InlineData("115792089237316195423570985008687907853269984665640564039457584007913129639936")]
    public void InvalidRawAmountsRejected(string amount) => Assert.Throws<ArgumentException>(() => OperationInputRules.Validate("{\"amountRaw\":\"" + amount + "\"}"));
    [Fact] public void NestedRecipientsValidated() => Assert.Throws<ArgumentException>(() => OperationInputRules.Validate("{\"recipients\":[{\"recipient\":\"wrong-chain-address\"}]}"));
    [Fact] public void MetadataIsNotMistakenForAddress() => OperationInputRules.Validate("{\"name\":\"token\",\"meta\":\"text\"}");
}
