using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Transactions;
using Decimal.Web3.Wallet;
using Xunit;

namespace DCWallet.Tests;
public sealed class TransferTests
{
    [Fact] public async Task SuccessfulBroadcastPreservesLocalHashAndSubmittedJournal()
    {
        using var rpc = new FakeRpc { AcceptBroadcast = true }; var directory = Path.Combine(Path.GetTempPath(), "dc-tests-" + Guid.NewGuid());
        try
        {
            var client = new DecimalClient(DecimalNetwork.Custom("fixture", 75, rpc.Url));
            using var signer = DecimalWallet.CreateTransactionSigner(DecimalWallet.GenerateMnemonic(), 75);
            var session = new TransferSession(client, directory);
            var transfer = await session.PrepareAsync(TransactionDraft.Del(signer.Address, signer.Address, "0.001"), signer);
            var result = await session.SendAsync(transfer); Assert.Equal(transfer.Signed.Hash, result.Hash); Assert.Equal(1, rpc.Broadcasts);
            var entry = JsonSerializer.Deserialize<JournalEntry>(await File.ReadAllTextAsync(Path.Combine(directory, result.Hash + ".json")))!;
            Assert.Equal("submitted", entry.State);
            await Assert.ThrowsAsync<InvalidOperationException>(() => session.SendAsync(transfer)); Assert.Equal(1, rpc.Broadcasts);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Fact] public async Task FailedBroadcastRecordsHashAndCannotBeRetriedAutomatically()
    {
        using var rpc = new FakeRpc(); var directory = Path.Combine(Path.GetTempPath(), "dc-tests-" + Guid.NewGuid());
        try
        {
            var client = new DecimalClient(DecimalNetwork.Custom("fixture", 75, rpc.Url));
            using var signer = DecimalWallet.CreateTransactionSigner(DecimalWallet.GenerateMnemonic(), 75);
            var session = new TransferSession(client, directory);
            var prepared = await session.PrepareAsync(TransactionDraft.Del(signer.Address, "0x" + new string('2', 40), "0.001"), signer);
            Assert.Equal(0, rpc.Broadcasts); Assert.False(Directory.Exists(directory));
            await Assert.ThrowsAnyAsync<Exception>(() => session.SendAsync(prepared));
            Assert.Equal(1, rpc.Broadcasts);
            await Assert.ThrowsAsync<InvalidOperationException>(() => session.SendAsync(prepared));
            Assert.Equal(1, rpc.Broadcasts);
            var entry = JsonSerializer.Deserialize<JournalEntry>(await File.ReadAllTextAsync(Path.Combine(directory, prepared.Signed.Hash + ".json")))!;
            Assert.Equal(prepared.Signed.Hash, entry.Hash); Assert.Equal("unknown-check-hash", entry.State);
            Assert.DoesNotContain(prepared.Signed.RawHex, await File.ReadAllTextAsync(Path.Combine(directory, prepared.Signed.Hash + ".json")));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Fact] public async Task ClearingPreparedTransferPreventsBroadcast()
    {
        using var rpc = new FakeRpc(); var client = new DecimalClient(DecimalNetwork.Custom("fixture", 75, rpc.Url));
        using var signer = DecimalWallet.CreateTransactionSigner(DecimalWallet.GenerateMnemonic(), 75);
        var session = new TransferSession(client, Path.GetTempPath());
        var prepared = await session.PrepareAsync(TransactionDraft.Del(signer.Address, signer.Address, "0.001"), signer);
        session.Clear(); await Assert.ThrowsAsync<InvalidOperationException>(() => session.SendAsync(prepared)); Assert.Equal(0, rpc.Broadcasts);
    }
    [Fact] public async Task WrongChainStopsBeforeSigningOrBroadcast()
    {
        using var rpc = new FakeRpc { Chain = "0x1" }; var client = new DecimalClient(DecimalNetwork.Custom("fixture", 75, rpc.Url));
        using var signer = DecimalWallet.CreateTransactionSigner(DecimalWallet.GenerateMnemonic(), 75);
        var session = new TransferSession(client, Path.GetTempPath());
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.PrepareAsync(TransactionDraft.Del(signer.Address, signer.Address, "0.001"), signer));
        Assert.Equal(0, rpc.Broadcasts);
    }
}
