using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Wallet;
using Xunit;
namespace DCWallet.UiTests;
public sealed class SafePipelineTests
{
    [AvaloniaFact]
    public Task SignedSafeProposalWithOuterSuccessAndInnerFailureIsNeverDisplayedAsExecuted() => RunPipeline(true);
    [AvaloniaFact]
    public Task SignedSafeProposalWithMatchingExecutionSuccessIsConfirmed() => RunPipeline(false);
    private static async Task RunPipeline(bool failInternal)
    {
        var phrase=DecimalWallet.GenerateMnemonic();var owner=DecimalWallet.DeriveAddress(phrase);
        using var rpc=new SafePipelineRpc(owner,failInternal);var dir=Path.Combine(Path.GetTempPath(),"dc-safe-pipeline-"+Guid.NewGuid());MainWindow? window=null;
        const string password="ephemeral-safe-test-password";
        try
        {
            await Vault.CreateAsync(Path.Combine(dir,"wallet.vault"),phrase,password);phrase="";
            window=new MainWindow(dir,DecimalNetwork.Custom("fixture",75,rpc.Url),autoLoad:false);window.Show();Fields(window).Single().Text=password;Click(window,"Открыть кошелек");await Wait(()=>Labels(window).Contains("Баланс еще не загружен"));
            typeof(MainWindow).GetMethod("ShowSafe",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,null);
            var fields=Fields(window).ToArray();fields[0].Text=SafePipelineRpc.Safe;fields[1].Text=owner;fields[3].Text="0.001";
            Click(window,"Создать предложение");await Wait(()=>Fields(window).Any(f=>f.Text?.Contains("SafeTx hash:")==true));
            var summary=Fields(window).Single(f=>f.Text?.Contains("SafeTx hash:")==true).Text!;rpc.ProposalHash=summary.Split("SafeTx hash: ")[1].Trim();
            var secret=Fields(window).Last();secret.Text=password;Click(window,"Подписать показанное предложение");await Wait(()=>Fields(window).Any(f=>f.Text?.StartsWith(owner+":",StringComparison.OrdinalIgnoreCase)==true));
            Assert.Equal("",secret.Text);Assert.Equal(0,rpc.Broadcasts);
            Click(window,"Проверить подписи и подготовить исполнение");await Wait(()=>Labels(window).Contains("Проверка транзакции"));Assert.Equal(0,rpc.Broadcasts);Assert.True(rpc.Simulations>0);
            window.GetLogicalDescendants().OfType<CheckBox>().Single().IsChecked=true;Click(window,"Отправить подтвержденную транзакцию");await Wait(()=>Labels(window).Contains("Отправлено в RPC"));Assert.Equal(1,rpc.Broadcasts);
            Click(window,"Проверить исполнение");
            if(failInternal)
            {
                await Wait(()=>Labels(window).Any(t=>t?.Contains("Safe подтвердил ошибку внутреннего действия")==true));
                Assert.DoesNotContain(Labels(window),t=>t?.StartsWith("Исполнено. Блок")==true);
            }
            else await Wait(()=>Labels(window).Any(t=>t?.StartsWith("Исполнено. Блок 100")==true));
            Assert.Equal(1,rpc.Broadcasts);
        }
        finally{window?.Close();if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }
    static IEnumerable<TextBox> Fields(Window w)=>w.GetLogicalDescendants().OfType<TextBox>();
    static IEnumerable<string?> Labels(Window w)=>w.GetLogicalDescendants().OfType<TextBlock>().Select(t=>t.Text);
    static void Click(Window w,string label)=>w.GetLogicalDescendants().OfType<Button>().First(b=>b.Content?.ToString()==label||b.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text==label)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    static async Task Wait(Func<bool> ready){for(var i=0;i<300&&!ready();i++)await Task.Delay(20);Assert.True(ready());}
    sealed class SafePipelineRpc:IDisposable
    {
        public const string Safe="0x2222222222222222222222222222222222222222";
        readonly HttpListener listener=new();readonly string owner;public string Url{get;}public string ProposalHash="0x"+new string('a',64);public int Broadcasts,Simulations;
        public SafePipelineRpc(string owner,bool failInternal)
        {
            this.owner=owner;var socket=new TcpListener(IPAddress.Loopback,0);socket.Start();var port=((IPEndPoint)socket.LocalEndpoint).Port;socket.Stop();Url=$"http://127.0.0.1:{port}/";listener.Prefixes.Add(Url);listener.Start();
            _=Task.Run(async()=>{while(listener.IsListening){HttpListenerContext context;try{context=await listener.GetContextAsync();}catch{break;}try{
                using var doc=JsonDocument.Parse(await new StreamReader(context.Request.InputStream).ReadToEndAsync());var root=doc.RootElement;var method=root.GetProperty("method").GetString();object result="0x0";
                if(method=="eth_chainId")result="0x4b";else if(method=="eth_blockNumber")result="0x64";else if(method=="eth_getCode")result="0x60006000";
                else if(method=="eth_getBalance")result="0x56bc75e2d63100000";else if(method=="eth_gasPrice")result="0x1";else if(method=="eth_estimateGas")result="0x186a0";
                else if(method=="eth_call")
                {
                    var data=root.GetProperty("params")[0].GetProperty("data").GetString()!;
                    if(data.StartsWith(Selector("getOwnersWithWeights()")))result="0x"+Word(32)+Word(1)+owner[2..].PadLeft(64,'0')+Word(1);
                    else if(data.StartsWith(Selector("getWeightThreshold()")))result="0x"+Word(1);
                    else if(data.StartsWith(Selector("nonce()")))result="0x"+Word(7);
                    else if(data.StartsWith(Selector("getTransactionHash(address,uint256,bytes,uint8,uint256,uint256,uint256,address,address,uint256)")))result=ProposalHash;
                    else {Interlocked.Increment(ref Simulations);result="0x"+Word(1);}
                }
                else if(method=="eth_sendRawTransaction")
                {Interlocked.Increment(ref Broadcasts);result=new Decimal.Web3.Transactions.SignedTransaction(root.GetProperty("params")[0].GetString()!,"","",0,0,0).Hash;}
                else if(method=="eth_getTransactionReceipt")result=new{transactionHash=root.GetProperty("params")[0].GetString(),blockHash="0x"+new string('c',64),blockNumber="0x64",transactionIndex="0x0",from=owner,to=Safe,status="0x1",gasUsed="0x10000",cumulativeGasUsed="0x10000",effectiveGasPrice="0x1",logs=new[]{new{address=Safe,topics=new[]{"0x"+Nethereum.Util.Sha3Keccack.Current.CalculateHash((failInternal?"ExecutionFailure":"ExecutionSuccess")+"(bytes32,uint256)"),ProposalHash},data="0x"+Word(0),logIndex="0x0"}}};
                var bytes=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{jsonrpc="2.0",id=root.GetProperty("id").Clone(),result}));context.Response.ContentType="application/json";await context.Response.OutputStream.WriteAsync(bytes);
            }finally{context.Response.Close();}}});
        }
        static string Selector(string signature)=>"0x"+Nethereum.Util.Sha3Keccack.Current.CalculateHash(signature)[..8];static string Word(int n)=>n.ToString("x").PadLeft(64,'0');
        public void Dispose(){listener.Stop();listener.Close();}
    }
}
