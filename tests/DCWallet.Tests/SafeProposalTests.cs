using Decimal.Web3.Multisig;
using Xunit;
namespace DCWallet.Tests;
public class SafeProposalTests
{
    // Public BIP39 test vector, never funded or used as a user wallet.
    private const string Seed = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
    private static SafeTransactionDraft Proposal() => SafeTransactionDrafts.BuildDel("0x1111111111111111111111111111111111111111", "0x2222222222222222222222222222222222222222", 100, 3);
    [Fact] public void SignatureMatchesOnlyOneProposalAndChain()
    {
        var tx = Proposal(); var signature = SafeTransactions.SignTypedDataFromMnemonic(tx, 75, Seed);
        Assert.Equal(132, SafeTransactions.PackEoaSignatures(tx, 75, [signature]).Length);
        Assert.Throws<ArgumentException>(() => SafeTransactions.PackEoaSignatures(tx with { ValueWei = 101 }, 75, [signature]));
        Assert.Throws<ArgumentException>(() => SafeTransactions.PackEoaSignatures(tx, 202020, [signature]));
        Assert.Throws<ArgumentException>(() => SafeTransactions.PackEoaSignatures(tx with { Nonce = 4 }, 75, [signature]));
    }
    [Fact] public void DuplicateSignatureCannotSatisfyThreshold()
    {
        var tx = Proposal(); var signature = SafeTransactions.SignTypedDataFromMnemonic(tx, 75, Seed);
        Assert.Throws<ArgumentException>(() => SafeTransactions.PackEoaSignatures(tx, 75, [signature, signature]));
    }
    [Fact] public void ForgedOwnerCannotBePacked()
    {
        var tx = Proposal(); var signature = SafeTransactions.SignTypedDataFromMnemonic(tx, 75, Seed);
        Assert.Throws<ArgumentException>(() => SafeTransactions.PackEoaSignatures(tx, 75, [signature with { Signer = tx.To }]));
    }
    [Fact] public void DelegateCallIsRejected() => Assert.Throws<NotSupportedException>(() => SafeTransactions.CalculateHash(Proposal() with { Operation = 1 }, 75));
}
