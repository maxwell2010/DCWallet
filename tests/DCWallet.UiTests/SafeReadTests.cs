using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using DCWallet.Tests;
using Decimal.Web3;
using Decimal.Web3.Multisig;
using Xunit;
namespace DCWallet.UiTests;
public sealed class SafeReadTests
{
    [AvaloniaFact]
    public async Task ExistingSafeDisplaysWeightedOwnersAndNonceAndRejectsWrongDomainHash()
    {
        using var rpc=new SafeRpc();var directory=Path.Combine(Path.GetTempPath(),"dc-safe-read-"+Guid.NewGuid());Directory.CreateDirectory(directory);MainWindow? window=null;
        try
        {
            window=new MainWindow(directory,DecimalNetwork.Mainnet() with {RpcUrl=new Uri(rpc.Url)},autoLoad:false);window.Show();
            Click(window,"Только просмотр");window.GetLogicalDescendants().OfType<TextBox>().First().Text=DiscoveryFixture.Owner;Click(window,"Открыть для просмотра");
            typeof(MainWindow).GetMethod("ShowSafe",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,null);
            window.GetLogicalDescendants().OfType<TextBox>().First().Text=DiscoveryFixture.Token;Click(window,"Загрузить владельцев и порог");
            for(var i=0;i<200&&!Labels(window).Any(t=>t?.Contains("Порог: 3; nonce: 7")==true);i++)await Task.Delay(10);
            Assert.Contains(Labels(window),t=>t?.Contains("Порог: 3; nonce: 7")==true && t.Contains(DiscoveryFixture.Owner+" · вес 2") && t.Contains(DiscoveryFixture.Token+" · вес 1"));
            var draft=SafeTransactionDrafts.BuildDel(DiscoveryFixture.Token,DiscoveryFixture.Owner,1,7);
            var check=(Task)typeof(MainWindow).GetMethod("CheckSafeHash",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,new object[]{draft,75L})!;
            var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>check);Assert.Contains("другой домен",error.Message);Assert.Equal(0,rpc.Broadcasts);
        }
        finally{window?.Close();Directory.Delete(directory,true);}
    }
    static IEnumerable<string?> Labels(Window window)=>window.GetLogicalDescendants().OfType<TextBlock>().Select(t=>t.Text);
    static void Click(Window window,string label)=>window.GetLogicalDescendants().OfType<Button>().First(b=>b.Content?.ToString()==label||b.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text==label)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    sealed class SafeRpc:IDisposable
    {
        readonly HttpListener listener=new();public string Url{get;}public int Broadcasts;
        public SafeRpc()
        {
            var socket=new TcpListener(IPAddress.Loopback,0);socket.Start();var port=((IPEndPoint)socket.LocalEndpoint).Port;socket.Stop();Url=$"http://127.0.0.1:{port}/";listener.Prefixes.Add(Url);listener.Start();
            _=Task.Run(async()=>{while(listener.IsListening){HttpListenerContext context;try{context=await listener.GetContextAsync();}catch{break;}try{
                using var doc=JsonDocument.Parse(await new StreamReader(context.Request.InputStream).ReadToEndAsync());var root=doc.RootElement;var method=root.GetProperty("method").GetString();object result="0x0";
                if(method=="eth_chainId")result="0x4b";else if(method=="eth_blockNumber")result="0x64";else if(method=="eth_getCode")result="0x60006000";
                else if(method=="eth_call")
                {
                    var data=root.GetProperty("params")[0].GetProperty("data").GetString()!;
                    if(data.StartsWith(Selector("getOwnersWithWeights()")))result="0x"+Word(32)+Word(2)+AddressWord(DiscoveryFixture.Owner)+Word(2)+AddressWord(DiscoveryFixture.Token)+Word(1);
                    else if(data.StartsWith(Selector("getWeightThreshold()")))result="0x"+Word(3);
                    else if(data.StartsWith(Selector("nonce()")))result="0x"+Word(7);
                    else result="0x"+Word(0);
                }
                else if(method=="eth_sendRawTransaction")Interlocked.Increment(ref Broadcasts);
                var bytes=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {jsonrpc="2.0",id=root.GetProperty("id").Clone(),result}));context.Response.ContentType="application/json";await context.Response.OutputStream.WriteAsync(bytes);
            }finally{context.Response.Close();}}});
        }
        static string Selector(string signature)=>"0x"+Nethereum.Util.Sha3Keccack.Current.CalculateHash(signature)[..8];
        static string Word(int number)=>number.ToString("x").PadLeft(64,'0');
        static string AddressWord(string address)=>address[2..].PadLeft(64,'0');
        public void Dispose(){listener.Stop();listener.Close();}
    }
}
