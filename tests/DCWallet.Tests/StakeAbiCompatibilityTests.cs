using System.Numerics;
using Decimal.Web3.Staking;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace DCWallet.Tests;

public sealed class StakeAbiCompatibilityTests
{
    private const string Address = "0x1111111111111111111111111111111111111111";
    private static string Word(BigInteger n) => n.ToString("x").TrimStart('0').PadLeft(64, '0');
    private static string Payload(bool extended, byte type = 1, int amount = 123456)
        => string.Concat(Enumerable.Repeat(new string('0', 24) + Address[2..], 3))
           + Word(amount) + Word(42) + Word(type) + Word(1900000000)
           + (extended ? Word(1800000000) : "");
    private static string Frozen(bool extended) => Payload(extended) + Word(1) + Word(2) + Word(2000000000);

    [Theory]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    [InlineData(false, 3)] [InlineData(true, 3)]
    [InlineData(false, 4)] [InlineData(true, 4)]
    public void StakePreservesAmountTokenAndBothHoldTimes(bool extended, byte type)
    {
        var stake = StakeResponseDecoder.Decode("0x" + Payload(extended, type));
        Assert.Equal(new BigInteger(123456), stake.AmountRaw);
        Assert.Equal(new BigInteger(42), stake.TokenId);
        Assert.Equal(type, stake.TokenType);
        Assert.Equal(new BigInteger(1900000000), stake.HoldTimestamp);
        Assert.Equal(extended ? new BigInteger(1800000000) : (BigInteger?)null, stake.HoldStartTime);
        Assert.Equal(Address, stake.Delegator);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FrozenBatchUsesCorrectStride(bool extended)
    {
        var raw = "0x" + Word(32) + Word(2) + Frozen(extended) + Frozen(extended);
        var rows = StakeResponseDecoder.DecodeFrozenBatch(raw, new BigInteger[] { 7, 9 });
        Assert.Equal(2, rows.Count);
        Assert.Equal(9, rows[1].Index);
        Assert.Equal(2, rows[1].FreezeType);
        Assert.Equal(2000000000, rows[1].UnfreezeTimestamp);
        Assert.Equal(123456, rows[1].Stake.AmountRaw);
        var single = StakeResponseDecoder.DecodeFrozen("0x" + Frozen(extended), 9);
        Assert.Equal(rows[1], single);
    }

    [Fact]
    public void RejectsUnknownLayoutsOffsetsCountsAndPadding()
    {
        var good = "0x" + Payload(true);
        foreach (var raw in new[] { "0x", good[..^2], good + Word(0), good.Replace("0x", "0xzz"),
                     "0x01" + good[4..] })
            Assert.Throws<InvalidDataException>(() => StakeResponseDecoder.Decode(raw));
        var badUint8 = "0x" + Payload(true);
        badUint8 = badUint8[..(2 + 160 * 2)] + "01" + badUint8[(4 + 160 * 2)..];
        Assert.Throws<InvalidDataException>(() => StakeResponseDecoder.Decode(badUint8));
        foreach (var raw in new[] { "0x" + Word(64) + Word(1) + Frozen(true),
                     "0x" + Word(32) + Word(2) + Frozen(true),
                     "0x" + Word(32) + Word(1) + Frozen(true) + Word(0) })
            Assert.Throws<InvalidDataException>(() => StakeResponseDecoder.DecodeFrozenBatch(raw, new BigInteger[] { 1 }));
        Assert.Empty(StakeResponseDecoder.DecodeFrozenBatch("0x" + Word(32) + Word(0), Array.Empty<BigInteger>()));
    }

    [Fact]
    public async Task SameReaderHandlesUpgradeAndHistoricalBlockWithoutLayoutCache()
    {
        var extended = false;
        var calls = new List<string>();
        using var rpc = new FakeRpc { ReadResult = (method, args) =>
        {
            Assert.Equal("eth_call", method);
            calls.Add(args[1].GetString()!);
            return "0x" + Frozen(extended);
        }};
        var web3 = new Nethereum.Web3.Web3(rpc.Url);
        var reader = new FrozenStakeReader(web3, Address);
        Assert.Null((await reader.GetFrozenStakeAsync(1)).Stake.HoldStartTime);
        extended = true;
        Assert.Equal(1800000000, (await reader.GetFrozenStakeAsync(1)).Stake.HoldStartTime);
        extended = false;
        Assert.Null((await reader.GetFrozenStakeAsync(1, new BlockParameter(new HexBigInteger(123)))).Stake.HoldStartTime);
        Assert.Equal(new[] { "latest", "latest", "0x7b" }, calls);
        Assert.Equal(0, rpc.Broadcasts);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TokenAndNftReadPathsUseStrictResponseDecoder(bool extended)
    {
        using var rpc = new FakeRpc { ReadResult = (method, _) =>
        {
            Assert.Equal("eth_call", method);
            return "0x" + Payload(extended, 3);
        }};
        var web3 = new Nethereum.Web3.Web3(rpc.Url);
        var token = new StakeReader(web3, Address);
        var nft = new NftStakeReader(web3, Address);
        var rows = new[] { await token.GetStakeAsync(Address, Address, Address),
            await token.GetHoldStakeAsync(Address, Address, Address, 1900000000),
            await nft.GetStakeAsync(Address, Address, Address, 42),
            await nft.GetHoldStakeAsync(Address, Address, Address, 42, 1900000000) };
        Assert.All(rows, s => Assert.Equal(extended ? new BigInteger(1800000000) : (BigInteger?)null, s.HoldStartTime));
        Assert.Equal(0, rpc.Broadcasts);
    }
}
