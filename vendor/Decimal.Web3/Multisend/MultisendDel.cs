using System.Numerics;
using Decimal.Web3.Transactions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.MessageEncodingServices;

namespace Decimal.Web3.Multisend;

public sealed record DelRecipient(string Address, string AmountDel);

/// <summary>Builds a single DEL transfer or a multi-recipient aggregate draft.</summary>
public static class MultisendDel
{
    public static TransactionDraft Build(
        string from, IReadOnlyList<DelRecipient> recipients, string? memo = null,
        string? aggregateContract = null)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        if (recipients.Count == 0) throw new ArgumentException("At least one recipient is required", nameof(recipients));
        if (recipients.Count == 1)
            return TransactionDraft.Del(from, recipients[0].Address, recipients[0].AmountDel, memo);
        TransactionDraft.ValidateAddress(from, nameof(from));
        TransactionDraft.ValidateAddress(aggregateContract!, nameof(aggregateContract));

        var calls = new List<AggregateCall>(recipients.Count + (string.IsNullOrEmpty(memo) ? 0 : 1));
        var total = BigInteger.Zero;
        foreach (var recipient in recipients)
        {
            TransactionDraft.ValidateAddress(recipient.Address, nameof(recipients));
            var amount = Units.Parse(recipient.AmountDel);
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(recipients), "Amount must be positive");
            total += amount;
            calls.Add(new AggregateCall { Target = recipient.Address, Value = amount, CallData = [] });
        }
        if (!string.IsNullOrEmpty(memo))
            calls.Add(new AggregateCall
            {
                Target = "0x0000000000000000000000000000000000000000",
                Value = BigInteger.Zero,
                CallData = System.Text.Encoding.UTF8.GetBytes(memo)
            });
        var encoder = new FunctionMessageEncodingService<AggregateFunction>(aggregateContract!, from);
        var data = "0x" + Convert.ToHexString(encoder.GetCallData(new AggregateFunction { Calls = calls }))
            .ToLowerInvariant();
        return TransactionDraft.ContractCall(from, aggregateContract!, data, total);
    }
}

[Struct("Call")]
public sealed class AggregateCall
{
    [Parameter("address", "target", 1)] public string Target { get; set; } = string.Empty;
    [Parameter("uint256", "value", 2)] public BigInteger Value { get; set; }
    [Parameter("bytes", "callData", 3)] public byte[] CallData { get; set; } = [];
}

[Function("aggregate")]
public sealed class AggregateFunction : FunctionMessage
{
    [Parameter("tuple[]", "calls", 1)] public List<AggregateCall> Calls { get; set; } = new();
}
