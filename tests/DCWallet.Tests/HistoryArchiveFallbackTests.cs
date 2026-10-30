using System.Numerics;
using System.Net;
using System.Text.Json;
using DCWallet.Core;
using Decimal.Web3;
using Xunit;
namespace DCWallet.Tests;
public sealed class HistoryArchiveFallbackTests
{
    const string Owner="0x1111111111111111111111111111111111111111", To="0x2222222222222222222222222222222222222222";
    static readonly string Old="0x"+new string('a',64),Recent="0x"+new string('b',64),Block="0x"+new string('c',64);
    static object? Reply(string method,JsonElement args,Func<string,bool> exists)
    {
        if(method=="eth_chainId")return "0x4b";
        if(method=="eth_getBlockByNumber")return new{number="0x64",hash=Block,timestamp="0x65000000",transactions=Array.Empty<string>()};
        var hash=args[0].GetString()!;if(!exists(hash))return null;
        if(method=="eth_getTransactionByHash")return new{hash,from=Owner,to=To,value="0x1",gasPrice="0x2",input="0x",nonce="0x1",blockNumber="0x64",blockHash=Block,type="0x0"};
        if(method=="eth_getTransactionReceipt")return new{transactionHash=hash,from=Owner,to=To,blockNumber="0x64",blockHash=Block,status="0x1",gasUsed="0x5208",effectiveGasPrice="0x2",logs=Array.Empty<object>()};
        throw new InvalidOperationException(method);
    }
    static DecimalClient Client(FakeRpc rpc)=>new(DecimalNetwork.Mainnet() with{RpcUrl=new Uri(rpc.Url)});
    [Fact]
    public async Task SuccessfulOldReceiptDoesNotTrapNewTransactionsOnLaggingArchive()
    {
        using var primary=new FakeRpc{ReadResult=(m,a)=>Reply(m,a,h=>h==Recent)};
        using var archive=new FakeRpc{ReadResult=(m,a)=>Reply(m,a,h=>h==Old)};
        var reader=new HistoryReader(Client(primary),archiveReaderFactory:()=>Task.FromResult(new HistoryReader(Client(archive),false)));
        var old=await reader.VerifyAsync(Old,Owner);Assert.NotNull(old);Assert.Equal("rpc",old.Verification);
        var recent=await reader.VerifyAsync(Recent,Owner);Assert.NotNull(recent);Assert.Equal(Recent,recent.Hash);Assert.Equal("rpc",recent.Verification);
        Assert.Equal(0,primary.Broadcasts);Assert.Equal(0,archive.Broadcasts);
    }
    [Fact]
    public async Task FailedArchiveConnectionIsNotCachedForever()
    {
        using var primary=new FakeRpc{ReadResult=(m,a)=>Reply(m,a,_=>false)};
        using var archive=new FakeRpc{ReadResult=(m,a)=>Reply(m,a,_=>true)};
        var attempts=0;
        var reader=new HistoryReader(Client(primary),archiveReaderFactory:()=>++attempts==1
            ? Task.FromException<HistoryReader>(new HttpRequestException("fixture",null,HttpStatusCode.ServiceUnavailable))
            : Task.FromResult(new HistoryReader(Client(archive),false)));
        Assert.Null(await reader.VerifyAsync(Old,Owner));
        Assert.NotNull(await reader.VerifyAsync(Old,Owner));Assert.Equal(2,attempts);Assert.Equal(0,primary.Broadcasts);Assert.Equal(0,archive.Broadcasts);
    }
}
