using System.Numerics;
using DCWallet.Core;
using Decimal.Web3;
using Xunit;

namespace DCWallet.Tests;
public class HistoryTests
{
    private static string Word(BigInteger value) => value.ToString("x").TrimStart('0').PadLeft(64, '0');
    private static string Data(params BigInteger[] values) => "0x" + string.Concat(values.Select(Word));
    [Fact] public void Single1155PreservesFullUint256()
    {
        var id = (BigInteger.One << 255) + 123; var amount = (BigInteger.One << 256) - 1;
        var result = Assert.Single(HistoryReader.Decode1155(Data(id, amount), false));
        Assert.Equal(id, result.Id); Assert.Equal(amount, result.Amount);
    }
    [Fact] public void Batch1155DecodesDistinctIdsAndAmounts()
    {
        var huge = (BigInteger.One << 200) + 9;
        var result = HistoryReader.Decode1155(Data(64, 160, 2, 7, huge, 2, huge, 1), true);
        Assert.Equal(new[] { (new BigInteger(7), huge), (huge, BigInteger.One) }, result);
    }
    [Theory]
    [InlineData("0x")]
    [InlineData("0xgg")]
    [InlineData("0x00")]
    public void RejectsMalformedSingleEvent(string data) => Assert.Throws<InvalidDataException>(() => HistoryReader.Decode1155(data, false));
    [Fact] public void RejectsOverlappingTruncatedAndUnboundedBatchArrays()
    {
        foreach (var data in new[] { Data(64,64,1,9), Data(64,128,1,7,2,9), Data(64,96,513,513), Data(65,128,1,7,1,9) })
            Assert.Throws<InvalidDataException>(() => HistoryReader.Decode1155(data, true));
    }
    [Fact] public void CsvPreservesPrecisionEscapesQuotesAndSpreadsheetFormulas()
    {
        var huge = (BigInteger.One << 255) + 7;
        var row = new HistoryRow("0xhash", 100, DateTimeOffset.UnixEpoch, "from", "to", 0, 1, true, "=HYPERLINK(\"evil\")",
            new[] { new AssetMovement("contract", "+EVIL", 18, huge, "from", "to", "ERC20") });
        var csv = HistoryReader.Csv(new[] { row });
        Assert.Contains(huge.ToString(), csv); Assert.Contains("\"'+EVIL\"", csv);
        Assert.Contains("\"'=HYPERLINK(\"\"evil\"\")\"", csv); Assert.Contains("0.000000000000000001", csv);
    }
    [Fact] public async Task ReceiptVerificationRejectsWrongNetworkBeforeTrustingHistory()
    {
        using var rpc = new FakeRpc { Chain = "0x1" };
        var network = DecimalNetwork.Mainnet() with { RpcUrl = new Uri(rpc.Url) };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new HistoryReader(new DecimalClient(network)).VerifyAsync("0x" + new string('a',64), "0x" + new string('1',40)));
        Assert.Equal(0, rpc.Broadcasts);
    }
}
