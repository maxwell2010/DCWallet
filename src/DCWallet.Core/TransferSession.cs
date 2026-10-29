using System.Text.Json;
using Decimal.Web3;
using Decimal.Web3.Transactions;
using Decimal.Web3.Wallet;

namespace DCWallet.Core;

public sealed record PreparedTransfer(TransactionDraft Draft, FeeQuote Quote, SignedTransaction Signed, long ChainId, DateTimeOffset CreatedAt);
public sealed record JournalEntry(string Hash, string From, string To, string ValueWei, long ChainId, string State, DateTimeOffset CreatedAt);
public sealed class TransferSession(DecimalClient client, string journalDirectory)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private PreparedTransfer? pending;
    private int generation;
    public void Clear() { Interlocked.Increment(ref generation); pending = null; }
    public async Task<PreparedTransfer> PrepareAsync(TransactionDraft draft, DecimalTransactionSigner signer)
        => await PrepareCoreAsync(draft,quote=>client.SignWithSignerAsync(draft,quote,signer));
    public Task<PreparedTransfer> PrepareAsync(TransactionDraft draft,WalletSigningSession signer)
        => PrepareCoreAsync(draft,async quote=>{
            if(quote.Draft!=draft||quote.GasLimit<=0||quote.GasPriceWei<0||!quote.CanAfford)throw new InvalidOperationException("Invalid fee quote or insufficient balance.");
            if(signer.ChainId!=client.Network.ChainId||!signer.Address.Equals(draft.From,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Signer chain or sender mismatch.");
            if((await client.Web3.Eth.ChainId.SendRequestAsync()).Value!=client.Network.ChainId)throw new InvalidOperationException("RPC chain mismatch.");
            var nonce=(await client.Web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(draft.From,Nethereum.RPC.Eth.DTOs.BlockParameter.CreatePending())).Value;
            var raw=signer.SignLegacyTransaction(nonce,draft.To,draft.ValueWei,quote.GasPriceWei,quote.GasLimit,draft.Data);
            return new SignedTransaction(raw,draft.From,draft.To,nonce,quote.GasLimit,quote.GasPriceWei);
        });
    private async Task<PreparedTransfer> PrepareCoreAsync(TransactionDraft draft,Func<FeeQuote,Task<SignedTransaction>> sign)
    {
        await gate.WaitAsync();
        try
        {
            pending = null;
            var version = Volatile.Read(ref generation);
            var quote = await client.EstimateFeeAsync(draft);
            var signed = await sign(quote);
            if (version != Volatile.Read(ref generation)) throw new InvalidOperationException("Подготовка отменена. Кошелек или параметры изменились.");
            return pending = new(draft, quote, signed, client.Network.ChainId, DateTimeOffset.UtcNow);
        }
        finally { gate.Release(); }
    }
    public async Task<TransactionResult> SendAsync(PreparedTransfer approved)
    {
        await gate.WaitAsync();
        try
        {
            if (!ReferenceEquals(approved, pending)) throw new InvalidOperationException("Подготовьте транзакцию заново.");
            if (DateTimeOffset.UtcNow - approved.CreatedAt > TimeSpan.FromMinutes(2)) { pending = null; throw new InvalidOperationException("Расчет комиссии устарел. Подготовьте транзакцию заново."); }
            pending = null; // One attempt only, even if a node accepted it and the response was lost.
            Directory.CreateDirectory(journalDirectory);
            var entry = new JournalEntry(approved.Signed.Hash, approved.Draft.From, approved.Draft.To, approved.Draft.ValueWei.ToString(), approved.ChainId, "sending", DateTimeOffset.UtcNow);
            var file = Path.Combine(journalDirectory, approved.Signed.Hash + ".json");
            await WriteJournalAsync(file, entry);
            try
            {
                var result = await client.BroadcastAsync(approved.Signed);
                if (!string.Equals(result.Hash, approved.Signed.Hash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("RPC вернул другой хэш. Проверьте локальный хэш транзакции в журнале.");
                await WriteJournalAsync(file, entry with { State = "submitted" });
                return result;
            }
            catch
            {
                await WriteJournalAsync(file, entry with { State = "unknown-check-hash" });
                throw;
            }
        }
        finally { gate.Release(); }
    }
    private static async Task WriteJournalAsync(string path, JournalEntry entry)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, entry);
                await stream.FlushAsync();
                stream.Flush(flushToDisk: true);
            }
            // A failed state replacement preserves the previous complete hash record.
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path, overwrite: false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
