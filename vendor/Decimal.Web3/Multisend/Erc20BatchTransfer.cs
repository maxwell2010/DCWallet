using System.Numerics;
using Decimal.Web3.Tokens;
using Decimal.Web3.Transactions;

namespace Decimal.Web3.Multisend;

public sealed record Erc20Recipient(string Address, BigInteger AmountRaw);

/// <summary>A safe series of direct ERC20 transfers. No allowance or arbitrary-call contract is used.</summary>
public sealed record Erc20BatchTransferPlan(
    IReadOnlyList<TransactionDraft> Drafts,
    BigInteger TotalAmountRaw,
    BigInteger TokenBalanceRaw,
    IReadOnlyList<FeeQuote> FeeQuotes)
{
    public int TransactionCount => Drafts.Count;
    public BigInteger EstimatedFeeWei => FeeQuotes.Aggregate(BigInteger.Zero, (sum, quote) => sum + quote.EstimatedFeeWei);
    public BigInteger MaximumFeeWei => FeeQuotes.Aggregate(BigInteger.Zero, (sum, quote) => sum + quote.MaximumFeeWei);
    public BigInteger NativeBalanceWei => FeeQuotes.Count == 0 ? BigInteger.Zero : FeeQuotes[0].NativeBalanceWei;
    public bool CanAffordTokens => TokenBalanceRaw >= TotalAmountRaw;
    public bool CanAffordFees => NativeBalanceWei >= MaximumFeeWei;
    public bool CanAfford => CanAffordTokens && CanAffordFees;
}

/// <summary>Builds and quotes ERC20 payouts without signing or broadcasting.</summary>
public sealed class Erc20BatchTransfer
{
    private readonly DecimalClient _client;
    private readonly Erc20Service _token;

    public Erc20BatchTransfer(DecimalClient client, string tokenAddress)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _token = new Erc20Service(client, tokenAddress);
    }

    public IReadOnlyList<TransactionDraft> BuildDrafts(string from, IReadOnlyList<Erc20Recipient> recipients)
    {
        TransactionDraft.ValidateAddress(from, nameof(from));
        ArgumentNullException.ThrowIfNull(recipients);
        if (recipients.Count == 0)
            throw new ArgumentException("At least one recipient is required", nameof(recipients));

        var drafts = new TransactionDraft[recipients.Count];
        var total = BigInteger.Zero;
        for (var i = 0; i < recipients.Count; i++)
        {
            var recipient = recipients[i] ?? throw new ArgumentException("Recipient is required", nameof(recipients));
            if (recipient.AmountRaw <= BigInteger.Zero)
                throw new ArgumentOutOfRangeException(nameof(recipients), "Each amount must be positive");
            total += recipient.AmountRaw;
            if (total >= BigInteger.One << 256)
                throw new ArgumentOutOfRangeException(nameof(recipients), "Total amount must fit uint256");
            drafts[i] = _token.BuildTransferDraft(from, recipient.Address, recipient.AmountRaw);
        }
        return Array.AsReadOnly(drafts);
    }

    public async Task<Erc20BatchTransferPlan> EstimateAsync(
        string from, IReadOnlyList<Erc20Recipient> recipients, bool exact = false)
    {
        var drafts = BuildDrafts(from, recipients);
        var balance = await _token.GetBalanceRawAsync(from).ConfigureAwait(false);
        var total = recipients.Aggregate(BigInteger.Zero, (sum, recipient) => sum + recipient.AmountRaw);
        if (balance < total)
            throw new DecimalTransactionException(DecimalTransactionError.InsufficientToken);

        var quotes = new FeeQuote[drafts.Count];
        for (var i = 0; i < drafts.Count; i++)
            quotes[i] = await _client.EstimateFeeAsync(drafts[i], exact).ConfigureAwait(false);
        return new Erc20BatchTransferPlan(drafts, total, balance, Array.AsReadOnly(quotes));
    }
}
