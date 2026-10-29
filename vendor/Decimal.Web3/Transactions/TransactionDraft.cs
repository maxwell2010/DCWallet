using System.Numerics;

namespace Decimal.Web3.Transactions;

/// <summary>Unsigned transaction data. All monetary values are integer wei.</summary>
public sealed record TransactionDraft(string From, string To, BigInteger ValueWei, string Data = "0x")
{
    public const string BurnAddress = "0x0000000000000000000000000000000000000000";

    public static TransactionDraft Del(string from, string to, string amountDel, string? memo = null)
    {
        ValidateAddress(from, nameof(from));
        ValidateAddress(to, nameof(to));
        var data = memo is null ? "0x" : "0x" + Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(memo)).ToLowerInvariant();
        return new(from, to, Units.Parse(amountDel), data);
    }

    /// <summary>Irreversibly burn DEL by transferring it to the zero address.</summary>
    public static TransactionDraft BurnDel(string from, string amountDel)
    {
        var amountWei = Units.Parse(amountDel);
        if (amountWei <= BigInteger.Zero)
            throw new ArgumentOutOfRangeException(nameof(amountDel), "Burn amount must be positive");
        return Del(from, BurnAddress, amountDel);
    }

    public static TransactionDraft ContractCall(string from, string contract, string data, BigInteger valueWei = default)
    {
        ValidateAddress(from, nameof(from));
        ValidateAddress(contract, nameof(contract));
        if (valueWei < 0) throw new ArgumentOutOfRangeException(nameof(valueWei));
        if (data is null || !data.StartsWith("0x", StringComparison.Ordinal) ||
            data.Length % 2 != 0 || !data.AsSpan(2).ToString().All(Uri.IsHexDigit))
            throw new ArgumentException("Data must be an even-length hex string", nameof(data));
        return new(from, contract, valueWei, data);
    }

    internal static void ValidateAddress(string address, string parameter)
    {
        if (address is null || address.Length != 42 || !address.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            !address.AsSpan(2).ToString().All(Uri.IsHexDigit))
            throw new ArgumentException("Expected a 20-byte EVM address", parameter);
    }
}
