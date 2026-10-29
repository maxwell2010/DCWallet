using System.Text.Json;
using System.Text.RegularExpressions;
using Decimal.Web3;
namespace DCWallet.Core;
public sealed record JournalReconciliation(int Checked,int Updated,int Pending,IReadOnlyList<string> Issues,IReadOnlyList<HistoryRow> Rows);
/// <summary>Receipt reconciliation only. Never signs, broadcasts, or retries a transaction.</summary>
public sealed class JournalReconciler(DecimalClient client,TimeSpan? readInterval=null)
{
    public async Task<JournalReconciliation> ReconcileAsync(string directory,string owner,CancellationToken ct=default)
    {
        owner=WalletDataReader.Address(owner);if(!Directory.Exists(directory))return new(0,0,0,[],[]);
        var checkedCount=0;var updated=0;var pending=0;var issues=new List<string>();var history=new HistoryReader(client);var verifiedRows=new List<HistoryRow>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var file in Directory.GetFiles(directory,"*.json"))
        {
            ct.ThrowIfCancellationRequested(); var hash=Path.GetFileNameWithoutExtension(file);
            if(!Regex.IsMatch(hash,"^0x[0-9a-fA-F]{64}$")||!seen.Add(hash))continue;
            try
            {
                if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0||new FileInfo(file).Length>16384)throw new InvalidDataException();var original=await File.ReadAllTextAsync(file,ct);
                var entry=JsonSerializer.Deserialize<JournalEntry>(original);
                if(entry is null||entry.ChainId!=client.Network.ChainId||!entry.From.Equals(owner,StringComparison.OrdinalIgnoreCase))continue;
                if(!entry.Hash.Equals(hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException();
                if(entry.State is not("sending" or "submitted" or "unknown-check-hash" or "confirmed" or "reverted"))continue;
                checkedCount++;if(readInterval is { } interval&&interval>TimeSpan.Zero)await Task.Delay(interval,ct);var row=await history.VerifyAsync(hash,owner,ct);
                if(row is null){pending++;continue;}
                if(!row.From.Equals(entry.From,StringComparison.OrdinalIgnoreCase)||!string.Equals(row.To,entry.To,StringComparison.OrdinalIgnoreCase)||row.ValueWei.ToString()!=entry.ValueWei)throw new InvalidDataException();
                verifiedRows.Add(row);
                if(entry.State==(row.Success?"confirmed":"reverted"))continue;
                // A concurrent sender may update the entry while the RPC request is outstanding.
                if(await File.ReadAllTextAsync(file,ct)!=original){pending++;continue;}
                var temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";
                try { await File.WriteAllTextAsync(temporary,JsonSerializer.Serialize(entry with{State=row.Success?"confirmed":"reverted"}),ct);ct.ThrowIfCancellationRequested();if(await File.ReadAllTextAsync(file,ct)!=original){pending++;continue;}File.Move(temporary,file,true);updated++; }
                finally { if(File.Exists(temporary))File.Delete(temporary); }
            }
            catch(Exception e)when(e is not OperationCanceledException || !ct.IsCancellationRequested){pending++;issues.Add("Не удалось подтвердить запись "+hash+" по RPC.");}
        }
        return new(checkedCount,updated,pending,issues,verifiedRows.OrderByDescending(r=>r.Block).ToArray());
    }
}
