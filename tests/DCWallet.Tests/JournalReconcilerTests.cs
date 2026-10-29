using System.Text.Json;
using DCWallet.Core;
using Decimal.Web3;
using Xunit;
namespace DCWallet.Tests;
public class JournalReconcilerTests
{
    const string Owner="0x1111111111111111111111111111111111111111",Target="0x2222222222222222222222222222222222222222";
    static readonly string Hash="0x"+new string('a',64),Block="0x"+new string('b',64);
    [Theory]
    [InlineData("submitted","0x1","confirmed")]
    [InlineData("unknown-check-hash","0x1","confirmed")]
    [InlineData("sending","0x0","reverted")]
    public async Task ReceiptUpdatesJournalWithoutBroadcast(string state,string status,string expected)
    {
        using var rpc=new FakeRpc{ReadResult=(method,p)=>method switch {
            "eth_chainId"=>"0x4b",
            "eth_getTransactionByHash"=>new{hash=Hash,blockHash=Block,blockNumber="0x64",from=Owner,to=Target,value="0x1",gasPrice="0x1",nonce="0x0",gas="0x5208",input="0x"},
            "eth_getTransactionReceipt"=>new{transactionHash=Hash,blockHash=Block,blockNumber="0x64",from=Owner,to=Target,status,gasUsed="0x5208",effectiveGasPrice="0x1",logs=Array.Empty<object>()},
            "eth_getBlockByNumber"=>new{number="0x64",hash=Block,timestamp="0x6553f100",transactions=new[]{Hash}}, _=>null }};
        var directory=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try{var file=Path.Combine(directory,Hash+".json");await File.WriteAllTextAsync(file,JsonSerializer.Serialize(new JournalEntry(Hash,Owner,Target,"1",75,state,DateTimeOffset.UtcNow)));
            var result=await new JournalReconciler(new DecimalClient(DecimalNetwork.Mainnet() with{RpcUrl=new Uri(rpc.Url)})).ReconcileAsync(directory,Owner);
            Assert.Equal(1,result.Updated);Assert.Single(result.Rows);Assert.Empty(result.Issues);Assert.Equal(expected,JsonSerializer.Deserialize<JournalEntry>(await File.ReadAllTextAsync(file))!.State);Assert.Equal(0,rpc.Broadcasts);
        }finally{Directory.Delete(directory,true);}
    }
    [Fact] public async Task MissingReceiptPreservesUnknownState()
    {
        using var rpc=new FakeRpc{ReadResult=(method,p)=>method=="eth_chainId"?"0x4b":null};var directory=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try{var file=Path.Combine(directory,Hash+".json");var content=JsonSerializer.Serialize(new JournalEntry(Hash,Owner,Target,"1",75,"unknown-check-hash",DateTimeOffset.UtcNow));await File.WriteAllTextAsync(file,content);
            var result=await new JournalReconciler(new DecimalClient(DecimalNetwork.Mainnet() with{RpcUrl=new Uri(rpc.Url)})).ReconcileAsync(directory,Owner);
            Assert.Equal(0,result.Updated);Assert.Equal(1,result.Pending);Assert.Equal(content,await File.ReadAllTextAsync(file));Assert.Equal(0,rpc.Broadcasts);
        }finally{Directory.Delete(directory,true);}
    }
    [Fact] public async Task HashFilenameMismatchIsRejectedWithoutRpcAndWithoutChangingEntry()
    {
        var calls=0;using var rpc=new FakeRpc{ReadResult=(method,p)=>{Interlocked.Increment(ref calls);return null;}};var directory=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try{var file=Path.Combine(directory,Hash+".json");var content=JsonSerializer.Serialize(new JournalEntry("../../outside",Owner,Target,"1",75,"submitted",DateTimeOffset.UtcNow));await File.WriteAllTextAsync(file,content);
            var result=await new JournalReconciler(new DecimalClient(DecimalNetwork.Mainnet() with{RpcUrl=new Uri(rpc.Url)})).ReconcileAsync(directory,Owner);
            Assert.Equal(0,result.Updated);Assert.Empty(result.Rows);Assert.Single(result.Issues);Assert.Equal(content,await File.ReadAllTextAsync(file));Assert.Equal(0,calls);Assert.Equal(0,rpc.Broadcasts);
        }finally{Directory.Delete(directory,true);}
    }
}
