using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
namespace DCWallet.Tests;
internal static class DiscoveryFixture
{
    public const string Owner = "0x1111111111111111111111111111111111111111";
    public const string Token = "0x2222222222222222222222222222222222222222";
}
internal sealed class DiscoveryCatalogHandler : HttpMessageHandler
    {
        public int TokenPages; public int IndexBlock { get; set; } = 100; public bool MissingArrays { get; set; } public bool Partial { get; set; } public bool NormalNftCoverage { get; set; } public int NftVerifiedBlock { get; set; } = 100; public bool StaleQueue { get; set; } public bool GraphUnavailable { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            object response;
            if(request.Method == HttpMethod.Post)
            {
                var body = await request.Content!.ReadAsStringAsync(ct);
                if(GraphUnavailable) response = new { errors = new[] { new {message="fixture unavailable"} } };
                else if(body.Contains("WalletTokens"))
                {
                    TokenPages++;
                    response = MissingArrays ? new {data=new { _meta=new { block=new {number=IndexBlock}}}} : (object)new {data=new {_meta=new {block=new {number=IndexBlock}},balances=TokenPages==1 ? Enumerable.Range(0,100).Select(_=>new {token=new {address=DiscoveryFixture.Token}}).ToArray(): [],balanceReservelesses=Array.Empty<object>()}};
                }
                else if(body.Contains("WalletNfts")) response = new {data=new {nftbalances=new[] {new {id="nft-1",user=new {address=DiscoveryFixture.Owner},collection=new {address=DiscoveryFixture.Token,name="NFT",tokenType="DRC721"},nft=new {tokenId="7"}}}}};
                else if(body.Contains("query Stakes")) response = new {data=new {stakes=Array.Empty<object>(),withdrawStakes=Array.Empty<object>(),transferStakes=Array.Empty<object>()}};
                else response = new {data=new {_meta=new {block=new {number=IndexBlock}}}};
            }
            else if(request.RequestUri!.AbsolutePath.Contains("/nfts/")) response= NormalNftCoverage ? new {Ok=true,Result=new {items=Array.Empty<object>(),partial=true,coverage=new {chain_id=75,index_block=98,verified_block=NftVerifiedBlock,unsupported_scope=new[]{"external_nft_collections"},issues=new[]{"index_block_differs_from_head"}}}} : (object)new {Ok=true,Result=new {items=Array.Empty<object>(),partial=Partial}};
            else if(request.RequestUri.AbsolutePath.Contains("erc20")) response=new {Ok=true,Result=new {address=DiscoveryFixture.Owner,balances=Array.Empty<object>(),pagination=new {total=0}}};
            else if(request.RequestUri.AbsolutePath.Contains("/staking")) response=new {Ok=true,Result=new {withdrawals_authoritative=true,withdrawals_fresh=!StaleQueue,withdrawals_checked_block=100,withdrawals=Array.Empty<object>()},positions=Array.Empty<object>(),withdrawals=Array.Empty<object>(),transfers=Array.Empty<object>(),sourceStale=false,requestsTruncated=false,verifiedBlock=100};
            else response=new {items=Array.Empty<object>()};
            return new(HttpStatusCode.OK) {Content=new StringContent(JsonSerializer.Serialize(response),Encoding.UTF8,"application/json")};
        }
    }
internal sealed class DiscoveryRpc : IDisposable
    {
        readonly TestHttpListener listener=new(); public string Url {get;} public string Chain {get;init;}="0x4b"; public string NftOwner {get;init;}=DiscoveryFixture.Token; public bool AdvanceHead {get;init;} public string BlockNumberHex {get;init;}="0x64"; public bool Reorg {get;init;} public int Broadcasts; int blocks; int snapshots;
        public DiscoveryRpc()
        {
            var socket=new TcpListener(IPAddress.Loopback,0);socket.Start();var port=((IPEndPoint)socket.LocalEndpoint).Port;socket.Stop();Url=$"http://127.0.0.1:{port}/";listener.Prefixes.Add(Url);listener.Start();
            _=Task.Run(async()=> {while(listener.IsListening) {TestHttpContext context;try{context=await listener.GetContextAsync();}catch{break;} try {
                using var doc=JsonDocument.Parse(await new StreamReader(context.Request.InputStream).ReadToEndAsync());var root=doc.RootElement;var method=root.GetProperty("method").GetString();object? result;
                if(method=="eth_chainId")result=Chain;
                else if(method=="eth_getBalance")result="0xde0b6b3a7640000";
                else if(method=="eth_getBlockByNumber") {var requested=root.GetProperty("params")[0].GetString();var number=AdvanceHead ? requested=="latest" ? Interlocked.Increment(ref snapshots)==1 ? "0x64" : "0x65" : requested : BlockNumberHex; result=new {number,hash="0x"+new string(Reorg&&Interlocked.Increment(ref blocks)>1?'b':'a',64),timestamp="0x65000000",transactions=Array.Empty<string>()};}
                else if(method=="eth_call") {var data=root.GetProperty("params")[0].GetProperty("data").GetString()!;result=data[..10] switch {"0x70a08231"=>Word(1234567),"0x313ce567"=>Word(6),"0x95d89b41"=>Str("TEST"),"0x06fdde03"=>Str("Mock Token"),"0x6352211e"=>"0x"+new string('0',24)+NftOwner[2..],_=>"0x"+new string('0',24)+DiscoveryFixture.Token[2..]};}
                else {if(method=="eth_sendRawTransaction")Interlocked.Increment(ref Broadcasts);result="0x0";}
                var bytes=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {jsonrpc="2.0",id=root.GetProperty("id").Clone(),result}));context.Response.ContentType="application/json";await context.Response.OutputStream.WriteAsync(bytes);
            } finally {context.Response.Close();}}});
        }
        static string Word(int n)=>"0x"+n.ToString("x").PadLeft(64,'0');
        static string Str(string text)=>Word(32)+Word(text.Length)[2..]+Convert.ToHexString(Encoding.UTF8.GetBytes(text)).PadRight(64,'0');
        public void Dispose(){listener.Stop();listener.Close();}
    }
