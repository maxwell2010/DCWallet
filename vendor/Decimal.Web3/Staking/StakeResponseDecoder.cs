using System.Numerics;
using Nethereum.Contracts;
using Nethereum.Contracts.MessageEncodingServices;
using Nethereum.RPC.Eth.DTOs;

namespace Decimal.Web3.Staking;

/// <summary>Strict decoding for legacy seven-word and current eight-word static Stake tuples.</summary>
public static class StakeResponseDecoder
{
    public static async Task<DelegationStake> QueryAsync<T>(Nethereum.Web3.Web3 web3,
        string contract, T function, BlockParameter? block = null) where T : FunctionMessage, new()
    {
        Transactions.TransactionDraft.ValidateAddress(contract, nameof(contract));
        var raw = await web3.Eth.Transactions.Call.SendRequestAsync(new CallInput
        {
            To = contract, Data = "0x" + Convert.ToHexString(new FunctionMessageEncodingService<T>(contract).GetCallData(function))
        }, block ?? BlockParameter.CreateLatest()).ConfigureAwait(false);
        return Decode(raw);
    }

    public static DelegationStake Decode(string raw)
    {
        var bytes = Bytes(raw);
        if (bytes.Length != 7 * 32 && bytes.Length != 8 * 32)
            throw new InvalidDataException("Unknown Stake response layout");
        return Stake(bytes);
    }

    public static FrozenStake DecodeFrozen(string raw, BigInteger index)
    {
        var bytes = Bytes(raw);
        if (bytes.Length != 10 * 32 && bytes.Length != 11 * 32)
            throw new InvalidDataException("Unknown FrozenStake response layout");
        return Frozen(bytes, index);
    }

    public static IReadOnlyList<FrozenStake> DecodeFrozenBatch(string raw, IReadOnlyList<BigInteger> indexes)
    {
        var bytes = Bytes(raw);
        if (bytes.Length < 64 || Uint(bytes.AsSpan(0, 32)) != 32 || Uint(bytes.AsSpan(32, 32)) != indexes.Count)
            throw new InvalidDataException("Invalid FrozenStake array offset or count");
        if (indexes.Count == 0)
        {
            if (bytes.Length != 64) throw new InvalidDataException("Unexpected empty array payload");
            return Array.Empty<FrozenStake>();
        }
        var payload = bytes.Length - 64;
        if (payload != indexes.Count * 320 && payload != indexes.Count * 352)
            throw new InvalidDataException("Unknown FrozenStake array layout");
        var stride = payload / indexes.Count;
        return indexes.Select((index, i) => Frozen(bytes.AsSpan(64 + i * stride, stride), index)).ToArray();
    }

    private static FrozenStake Frozen(ReadOnlySpan<byte> bytes, BigInteger index)
    {
        var stakeBytes = bytes.Length - 96;
        return new(index, Stake(bytes[..stakeBytes]), Small(bytes.Slice(stakeBytes, 32)),
            Small(bytes.Slice(stakeBytes + 32, 32)), Uint(bytes.Slice(stakeBytes + 64, 32)));
    }

    private static DelegationStake Stake(ReadOnlySpan<byte> b) => new(
        Address(b[..32]), Address(b.Slice(32, 32)), Address(b.Slice(64, 32)),
        Uint(b.Slice(96, 32)), Uint(b.Slice(128, 32)), Small(b.Slice(160, 32)), Uint(b.Slice(192, 32)))
        { HoldStartTime = b.Length == 256 ? Uint(b.Slice(224, 32)) : null };

    private static string Address(ReadOnlySpan<byte> word)
    {
        if (word[..12].ContainsAnyExcept((byte)0)) throw new InvalidDataException("Nonzero address padding");
        return "0x" + Convert.ToHexString(word[12..]).ToLowerInvariant();
    }
    private static BigInteger Uint(ReadOnlySpan<byte> word) => new(word, isUnsigned: true, isBigEndian: true);
    private static byte Small(ReadOnlySpan<byte> word)
    {
        if (word[..31].ContainsAnyExcept((byte)0)) throw new InvalidDataException("Nonzero uint8 padding");
        return word[31];
    }
    private static byte[] Bytes(string raw)
    {
        if (raw is null || !raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || raw.Length % 64 != 2)
            throw new InvalidDataException("Invalid ABI response length");
        try { return Convert.FromHexString(raw[2..]); }
        catch (FormatException e) { throw new InvalidDataException("Invalid ABI hex", e); }
    }
}
