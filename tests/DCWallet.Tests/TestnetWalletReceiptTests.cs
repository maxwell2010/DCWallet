using System.Text.Json;
using DCWallet.Core;
using Decimal.Web3;
using Xunit;
namespace DCWallet.Tests;
public sealed class WalletReceiptReadFactAttribute:FactAttribute
{
    public WalletReceiptReadFactAttribute() { if(Environment.GetEnvironmentVariable("DC_TESTNET_WALLET_RECEIPTS")!="1") Skip="Explicit wallet testnet receipt audit opt-in required"; }
}
public class TestnetWalletReceiptTests
{
    [WalletReceiptReadFact,Trait("Category","TestnetRead")]
    public async Task WalletHistoryVerifiesEveryConfirmedIntegrationReceiptAndJournal()
    {
        var report=Environment.GetEnvironmentVariable("DC_TESTNET_WALLET_REPORT")??throw new InvalidOperationException("Public integration report required");
        using var doc=JsonDocument.Parse(await File.ReadAllTextAsync(report)); Assert.Equal(202020,doc.RootElement.GetProperty("chainId").GetInt32());
        var owner=doc.RootElement.GetProperty("owner").GetString()!;
        var client=new DecimalClient(DecimalNetwork.Testnet() with{RpcUrl=new Uri("https://202020.rpc.thirdweb.com")}); var history=new HistoryReader(client,false);var output=new List<object>();
        async Task<T> Read<T>(Func<Task<T>> action){for(var retry=0;;retry++){try{return await action();}catch(Exception e)when(retry<4&&e.ToString().Contains("429")){await Task.Delay(TimeSpan.FromSeconds(5*(retry+1)));}}}
        var hashes=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var nonces=new HashSet<System.Numerics.BigInteger>();var totalFee=System.Numerics.BigInteger.Zero;
        foreach(var item in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            if(item.GetProperty("state").GetString()!="confirmed")continue;
            var hash=item.GetProperty("hash").GetString()!;Assert.True(hashes.Add(hash),"A wallet case was accidentally duplicated");var journalPath=Path.Combine(Path.GetDirectoryName(report)!,hash+".json");
            var journal=JsonSerializer.Deserialize<JournalEntry>(await File.ReadAllTextAsync(journalPath))!;Assert.Equal(hash,journal.Hash);Assert.Equal(202020,journal.ChainId);Assert.Contains(journal.State,new[]{"submitted","confirmed"});
            await Task.Delay(TimeSpan.FromSeconds(2));var row=await Read(()=>history.VerifyAsync(hash,owner));Assert.NotNull(row);Assert.True(row.Success);Assert.Equal("rpc",row.Verification);
            Assert.Equal(Units.Parse(item.GetProperty("paidFeeDel").GetString()!),row.FeeWei);
            Assert.Equal(owner,row.From,StringComparer.OrdinalIgnoreCase);Assert.Equal(item.GetProperty("to").GetString(),row.To,StringComparer.OrdinalIgnoreCase);
            Assert.True(row.FeeWei<=Units.Parse(item.GetProperty("maximumFeeDel").GetString()!));totalFee+=row.FeeWei;
            var tx=await Read(()=>client.Web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(hash));Assert.True(nonces.Add(tx.Nonce.Value),"Repeated nonce in confirmed wallet cases");
            var name=item.GetProperty("case").GetString();
            if(name=="DEL memo self")Assert.Equal("DC WALLET pipeline QA",row.Memo);
            if(name=="QA ERC20 self one raw")Assert.Contains(row.Movements!,m=>m.Standard=="ERC20"&&m.Raw==1);
            if(name=="QA ERC1155 self id1 amount1")Assert.Contains(row.Movements!,m=>m.Standard=="ERC1155"&&m.Raw==1&&m.TokenId==1);
            output.Add(new{label=name,hash,verified=row.Verification,kind=row.Kind,feeDel=Units.Format(row.FeeWei),movements=row.Movements});
        }
        Assert.NotEmpty(output);Assert.Equal(Units.Parse(doc.RootElement.GetProperty("paidDel").GetString()!),totalFee);Assert.True(totalFee<=Units.Parse("5"));
        var reconciliation=await new JournalReconciler(client,TimeSpan.FromSeconds(2)).ReconcileAsync(Path.GetDirectoryName(report)!,owner);
        if(reconciliation.Pending>0){await Task.Delay(TimeSpan.FromSeconds(15));reconciliation=await new JournalReconciler(client,TimeSpan.FromSeconds(3)).ReconcileAsync(Path.GetDirectoryName(report)!,owner);}
        Assert.Empty(reconciliation.Issues);Assert.Equal(output.Count,reconciliation.Rows.Count);Assert.Equal(0,reconciliation.Pending);
        await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(report)!,"history-audit.json"),JsonSerializer.Serialize(output,OperationCatalog.Json));
    }
}
