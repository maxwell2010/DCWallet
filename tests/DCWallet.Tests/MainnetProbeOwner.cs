using Decimal.Web3.Transactions;
namespace DCWallet.Tests;
internal static class MainnetProbeOwner
{
    public static string Required()
    {
        var owner = Environment.GetEnvironmentVariable("DC_MAINNET_READ_OWNER") ?? throw new InvalidOperationException("Set DC_MAINNET_READ_OWNER to an explicitly selected public address for this opt-in read-only probe.");
        _ = TransactionDraft.Del(owner, owner, "0");
        return owner;
    }
}
