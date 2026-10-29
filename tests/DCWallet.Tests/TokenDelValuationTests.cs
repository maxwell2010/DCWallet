using System.Numerics;
using System.Text.Json;
using DCWallet.Core;
using Decimal.Web3;
using Nethereum.Util;
using Xunit;
namespace DCWallet.Tests;
public sealed class TokenDelValuationTests
{
    static string Word(BigInteger number) => "0x" + number.ToString("x").TrimStart('0').PadLeft(64, '0');
    static string Selector(string signature) => "0x" + Sha3Keccack.Current.CalculateHash(signature)[..8];
    [Theory]
    [InlineData(25, "1.25", false, true)]
    [InlineData(0, "1.25", false, false)]
    [InlineData(25, "11", false, false)]
    [InlineData(25, "1.25", true, false)]
    public async Task ValuationUsesPinnedBlockAndRejectsInvalidReserveOrReorg(int crr, string quote, bool reorg, bool valid)
    {
        var blocks = 0; var latestCalls = 0;
        using var rpc = new FakeRpc { ReadResult = (method, parameters) =>
        {
            if (method == "eth_chainId") return "0x4b";
            if (method == "eth_getBlockByNumber") return new { number = "0x64", hash = "0x" + new string(reorg && ++blocks > 1 ? 'b' : 'a', 64), transactions = Array.Empty<string>(), timestamp = "0x65000000" };
            if (method != "eth_call") return "0x0";
            if (parameters[1].ToString() != "0x64") latestCalls++;
            var data = parameters[0].GetProperty("data").GetString()!;
            return data[..10] == Selector("crr()") ? Word(crr) : data[..10] == Selector("reserve()") ? Word(Units.Parse("10")) : Word(Units.Parse(quote));
        }};
        var reader = new TokenDelValuation(new DecimalClient(DecimalNetwork.Mainnet() with { RpcUrl = new Uri(rpc.Url) }));
        var holding = new TokenBalance(DiscoveryFixture.Token, "Token", "TEST", 6, 1234567);
        if (valid) { var result = await reader.ReadAsync(holding, 100); Assert.Equal(Units.Parse(quote), result.Wei); Assert.Equal(100, result.Block); }
        else if (crr == 0) await Assert.ThrowsAsync<NotSupportedException>(() => reader.ReadAsync(holding, 100));
        else if (reorg) await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadAsync(holding, 100));
        else await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAsync(holding, 100));
        Assert.Equal(0, latestCalls); Assert.Equal(0, rpc.Broadcasts);
    }
}
