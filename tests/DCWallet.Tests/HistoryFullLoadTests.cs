using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DCWallet.Core;
using Decimal.Web3;
using Xunit;
namespace DCWallet.Tests;
public sealed class HistoryFullLoadTests
{
    const string Owner="0x1111111111111111111111111111111111111111",To="0x2222222222222222222222222222222222222222";
    static readonly string BlockHash="0x"+new string('c',64);
    static string Hash(int i)=>"0x"+i.ToString("x64");
    static object Candidate(int i,bool malformed=false)=>new{hash=i.ToString("X64"),evm_hash=Hash(i),block=100,timestamp="2026-01-01T00:00:00Z",sender=Owner,recipient=To,amount=malformed?"bad":"1",coin="del",status="Success",method="Transfer",fee=new{gas_amount="42000"},transfer=Array.Empty<object>()};
    sealed class Catalog(int total,bool malformed=false,List<int>? offsets=null):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            var offset=int.Parse(Regex.Match(request.RequestUri!.Query,@"offset=(\d+)").Groups[1].Value);
            if(offsets is not null) lock(offsets)offsets.Add(offset);
            var response=new{Ok=true,Result=new{count=total,txs=Enumerable.Range(offset+1,Math.Min(50,total-offset)).Select(i=>Candidate(i,malformed)).ToArray()}};
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(response),Encoding.UTF8,"application/json")});
        }
    }
    static object? Rpc(string method,JsonElement args,int available,bool invalidReceipt=false)
    {
        if(method=="eth_chainId")return "0x4b";
        if(method=="eth_getBlockByNumber")return new{number="0x64",hash=BlockHash,timestamp="0x65000000",transactions=Array.Empty<string>()};
        var hash=args[0].GetString()!;
        if(Convert.ToInt32(hash[2..],16)>available)return null;
        if(method=="eth_getTransactionByHash")return new{hash,from=Owner,to=To,value="0x1",gasPrice="0x2",input="0x",nonce="0x1",blockNumber="0x64",blockHash=BlockHash,type="0x0"};
        if(method=="eth_getTransactionReceipt")return new{transactionHash=hash,from=invalidReceipt?To:Owner,to=To,blockNumber="0x64",blockHash=BlockHash,status="0x1",gasUsed="0x5208",effectiveGasPrice="0x2",logs=Array.Empty<object>()};
        throw new InvalidOperationException(method);
    }
    [Fact]
    public async Task All64LoadAcrossTwoPagesAnd34MissingReceiptsAreNot34CorruptTransactions()
    {
        var offsets=new List<int>();using var rpc=new FakeRpc{ReadResult=(m,a)=>Rpc(m,a,30)};
        var client=new DecimalClient(DecimalNetwork.Mainnet()with{RpcUrl=new Uri(rpc.Url)});
        var reader=new HistoryReader(client,false,catalogReaderFactory:c=>new WalletDataReader(c,handler:new Catalog(64,offsets:offsets)));
        var result=await reader.ReadAllAsync(Owner);
        Assert.Equal(64,result.Rows.Count);Assert.Equal(64,result.Total);Assert.True(result.Complete);Assert.Equal(0,result.Failed);
        Assert.Equal(30,result.Verified);Assert.Equal(34,result.Archived);Assert.Equal(new[]{0,50},offsets);
        Assert.Equal(64,result.VerificationDetails.Count);Assert.Equal(34,result.VerificationDetails.Count(d=>d.Unavailable));
        Assert.All(result.Rows.Where(r=>r.Verification=="indexer-evm"),r=>Assert.Equal("archive_unavailable",r.VerificationReason));
        Assert.Contains("verification_reason",HistoryReader.Csv(result.Rows));Assert.Contains("archive_unavailable",HistoryReader.Csv(result.Rows));Assert.Equal(0,rpc.Broadcasts);
    }
    [Fact]
    public async Task InvalidReceiptIdentityStillCountsAsVerificationError()
    {
        using var rpc=new FakeRpc{ReadResult=(m,a)=>Rpc(m,a,1,true)};
        var client=new DecimalClient(DecimalNetwork.Mainnet()with{RpcUrl=new Uri(rpc.Url)});
        var reader=new HistoryReader(client,false,catalogReaderFactory:c=>new WalletDataReader(c,handler:new Catalog(1)));
        var result=await reader.ReadAllAsync(Owner);
        Assert.Equal(1,result.Failed);Assert.Equal("verification_failed",Assert.Single(result.Rows).VerificationReason);
        var detail=Assert.Single(result.VerificationDetails);Assert.False(detail.Unavailable);Assert.Equal("receipt_identity_mismatch",Assert.Single(detail.Attempts).Code);
    }
    [Fact]
    public async Task ArchiveTransportFailureIsNotDisguisedAsUnavailableHistoricalData()
    {
        using var rpc=new FakeRpc{ReadResult=(m,a)=>Rpc(m,a,0)};
        var client=new DecimalClient(DecimalNetwork.Mainnet()with{RpcUrl=new Uri(rpc.Url)});
        var reader=new HistoryReader(client,archiveReaderFactory:()=>Task.FromException<HistoryReader>(new HttpRequestException("fixture offline")),
            catalogReaderFactory:c=>new WalletDataReader(c,handler:new Catalog(1)));
        var result=await reader.ReadAllAsync(Owner);
        Assert.Equal(1,result.Failed);Assert.False(Assert.Single(result.VerificationDetails).Unavailable);
        Assert.Contains(result.VerificationDetails[0].Attempts,a=>a.Source=="archive"&&a.Code=="transport_error");
        Assert.Equal("verification_failed",Assert.Single(result.Rows).VerificationReason);Assert.Equal(0,rpc.Broadcasts);
    }
    [Fact]
    public async Task MalformedArchivedRecordCountsOnlyOnceAndCannotMakeHistoryComplete()
    {
        using var rpc=new FakeRpc{ReadResult=(m,a)=>Rpc(m,a,0)};
        var client=new DecimalClient(DecimalNetwork.Mainnet()with{RpcUrl=new Uri(rpc.Url)});
        var reader=new HistoryReader(client,false,catalogReaderFactory:c=>new WalletDataReader(c,handler:new Catalog(1,true)));
        var result=await reader.ReadAllAsync(Owner);
        Assert.Equal(1,result.Failed);Assert.Empty(result.Rows);Assert.False(result.Complete);Assert.Equal(0,rpc.Broadcasts);
    }
}
