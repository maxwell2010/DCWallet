using System.Text.Json;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Transactions;
using Xunit;

namespace DCWallet.Tests;

public sealed class JournalAtomicityTests
{
    [Fact]
    public async Task StateReplacementKeepsPreviousCompleteRecordForExistingReaders()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dc-atomic-journal-" + Guid.NewGuid());
        FileStream? previous = null;
        using var rpc = new FakeRpc
        {
            AcceptBroadcast = true,
            ReadResult = (method, _) =>
            {
                if (method == "eth_sendRawTransaction" && Directory.Exists(directory) && previous is null)
                {
                    var path = Directory.GetFiles(directory, "*.json").SingleOrDefault();
                    if (path is not null) previous = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                }
                return method switch
                {
                    "eth_chainId" => "0x4b", "eth_estimateGas" => "0x5208", "eth_gasPrice" => "0x1",
                    "eth_getBalance" => "0xde0b6b3a7640000", _ => "0x0"
                };
            }
        };
        try
        {
            using var signer = WalletSigningSession.FromPrivateKey(new string('0', 63) + "1", 75);
            var session = new TransferSession(new DecimalClient(DecimalNetwork.Custom("fixture", 75, rpc.Url)), directory);
            var transfer = await session.PrepareAsync(TransactionDraft.Del(signer.Address, signer.Address, "0.001"), signer);
            await session.SendAsync(transfer);
            Assert.NotNull(previous);
            var original = await JsonSerializer.DeserializeAsync<JournalEntry>(previous);
            Assert.Equal("sending", original!.State);
            var current = JsonSerializer.Deserialize<JournalEntry>(await File.ReadAllTextAsync(Path.Combine(directory, transfer.Signed.Hash + ".json")));
            Assert.Equal("submitted", current!.State);
            Assert.Equal(original.Hash, current.Hash);
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally { previous?.Dispose(); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
