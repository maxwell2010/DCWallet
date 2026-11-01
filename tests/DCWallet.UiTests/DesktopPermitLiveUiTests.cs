using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using DCWallet.Core;
using DCWallet.Tests;
using Decimal.Web3;
using Decimal.Web3.Staking;
using Decimal.Web3.Tokens;
using Decimal.Web3.Transactions;
using Decimal.Web3.Wallet;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace DCWallet.UiTests;

[Xunit.Sdk.XunitTestCaseDiscoverer("Avalonia.Headless.XUnit.AvaloniaUIFactDiscoverer", "Avalonia.Headless.XUnit")]
public sealed class DesktopPermitLiveFactAttribute : FactAttribute
{
    public DesktopPermitLiveFactAttribute()
    { if (Environment.GetEnvironmentVariable("DC_DESKTOP_PERMIT_UI_WRITE") != "1") Skip = "Explicit isolated Desktop Permit Testnet UI opt-in required"; }
}

/// <summary>Real Avalonia controls, signer, session and RPC. Not a mock network or installed-app test.</summary>
public sealed class DesktopPermitLiveUiTests
{
    const string Owner="0xb92bd490c8e9e5d9c46d71666418d4e7ae4534e9", Token="0x2F6caF49f9D2C13Cbf6963E663BbC3287Af0F40D", Output="0x37748f3e857A71E4334125A935052Ce1e603b7B5", Validator="0x05babc196b96c22f763f7e30a84e74665a920f98", Delegation=CombinedNftDelegation.Proxy, Center="0xb8F9c4A26b91a65Ac0D3F34e230a4C9ad09aEF4a";
    static readonly BigInteger Amount=BigInteger.Parse("1000000000000000");
    [DesktopPermitLiveFact]
    public async Task DelegateHoldAndConvertThroughDesktopControlsOnIsolatedTestnet()
    {
        var root=Path.GetFullPath(Environment.GetEnvironmentVariable("DC_DESKTOP_PERMIT_UI_ARTIFACTS")??throw new InvalidOperationException("Artifact directory required"));
        Directory.CreateDirectory(root);
        using var exclusive=new FileStream(Path.Combine(root,"execution.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        var ledger=Path.Combine(root,"broadcast-ledger.jsonl");
        var resume=Environment.GetEnvironmentVariable("DC_DESKTOP_PERMIT_UI_RESUME")=="1";
        var completed=File.Exists(ledger)?File.ReadAllLines(ledger).Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>JsonDocument.Parse(x).RootElement.Clone()).ToArray():[];
        if(completed.Length>0&&!resume)throw new InvalidOperationException("Existing broadcasts must be reconciled read-only; never repeat campaign");
        if(completed.Select(x=>x.GetProperty("operation").GetString()).Distinct().Count()!=completed.Length)throw new InvalidOperationException("Duplicate campaign operation");
        var source=Environment.GetEnvironmentVariable("DC_EXISTING_STAND_ENV")??throw new InvalidOperationException("Dedicated test signer locator required");
        string? phrase=null;
        foreach(var line in File.ReadLines(source)){var i=line.IndexOf('=');if(i>0&&line[..i].Trim()=="DECIMAL_TESTNET_TEST_MNEMONIC"){phrase=line[(i+1)..].Trim().Trim('"','\'');break;}}
        if(phrase is null||!DecimalWallet.DeriveAddress(phrase).Equals(Owner,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Wrong isolated signer");
        var password=Convert.ToHexString(RandomNumberGenerator.GetBytes(24));var vault=Path.Combine(root,"wallet.vault");
        await Vault.CreateAsync(vault,phrase,password);phrase=null;
        using var relay=new Relay(root,ledger);
        var net=DecimalNetwork.Testnet()with{RpcUrl=new Uri(relay.Url),DelegationContract=Delegation,TokenCenterContract=Center};
        var client=new DecimalClient(net);MainWindow? w=null;
        var reports=new List<object>();
        try
        {
            Assert.Equal(202020,(await client.Web3.Eth.ChainId.SendRequestAsync()).Value);
            foreach(var done in completed){var hash=done.GetProperty("hash").GetString()!;await Confirm(client,hash);using var recovered=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root,"recovered-"+done.GetProperty("operation").GetString()+".json")));Assert.Equal(hash,recovered.RootElement.GetProperty("hash").GetString());reports.Add(recovered.RootElement.Clone());}
            w=new MainWindow(root,net,autoLoad:false);w.Show();FieldFirst(w).Text=password;Click(w,"Открыть кошелек");await Wait(w,()=>Labels(w).Contains("Баланс еще не загружен"));
            foreach(var kind in new[]{"delegate","hold","swap"})
            {
                if(completed.Any(x=>x.GetProperty("operation").GetString()==kind))continue;
                await ChainOnly(client);var spender=kind=="swap"?Center:Delegation;
                var before=await State(client,spender,0);Assert.Equal(BigInteger.Zero,before.Allowance);Assert.Equal(before.Nonce,before.Pending);
                await File.WriteAllTextAsync(Path.Combine(root,kind+"-before.json"),JsonSerializer.Serialize(Public(before)));
                var erc=new Erc20Service(client,Token);var meta=await erc.GetMetadataAsync();
                Set(w,"tokenData",new DataSet<TokenBalance>([new(Token,meta.Name,meta.Symbol,meta.Decimals,before.Balance)],before.Block,true,[]));
                async Task Open()
                {
                    if(kind=="swap")
                    {
                        Invoke(w,"ShowSwap");w.GetLogicalDescendants().OfType<ComboBox>().Single(c=>c.Items.Cast<object>().Contains("Токен → Токен")).SelectedIndex=2;
                        Field(w,"Контракт резервного токена").Text=Token;Field(w,"Получить резервный токен").Text=Output;Field(w,"Сколько отдать").Text="0.001";Click(w,"Обновить цену");await Wait(w,()=>Labels(w).Any(t=>t?.Contains("Котировка обновлена")==true));Click(w,"Продолжить обмен");
                    }
                    else
                    {
                        Invoke(w,"ShowStakingForm",false);var asset=w.GetLogicalDescendants().OfType<ComboBox>().Single(c=>c.Items.Cast<object>().Any(i=>i.ToString()?.StartsWith("AMTOKEN")==true));asset.SelectedIndex=1;
                        Field(w,"Адрес валидатора").Text=Validator;Field(w,"Количество").Text="0.001";
                        if(kind=="hold"){w.GetLogicalDescendants().OfType<CheckBox>().Single(c=>c.Content?.ToString()=="Делегировать с hold до выбранной даты").IsChecked=true;w.GetLogicalDescendants().OfType<CheckBox>().Single(c=>c.Name=="DesktopShortHold").IsChecked=true;w.GetLogicalDescendants().OfType<TextBox>().Single(c=>c.Name=="DesktopHoldMinutes").Text="5";}
                        Click(w,"Делегировать");
                    }
                    await Wait(w,()=>w.GetLogicalDescendants().OfType<Button>().Any(b=>b.Name=="DesktopPermitSign"));
                }
                await Open();await UiFrameCapture.SaveAsync(w,Path.Combine(root,kind+"-permit.png"));ClickName(w,"DesktopPermitCancel");
                Assert.Equal(0,relay.Broadcasts);var cancelled=await State(client,spender,0);Assert.Equal(before.PermitNonce,cancelled.PermitNonce);Assert.Equal(before.Nonce,cancelled.Nonce);
                await Open();NamedBox(w,"DesktopPermitPassword").Text=password;ClickName(w,"DesktopPermitSign");await Wait(w,()=>Labels(w).Contains("Проверка транзакции"));
                await UiFrameCapture.SaveAsync(w,Path.Combine(root,kind+"-fee-cancel.png"));Click(w,"Отменить");Assert.Equal(0,relay.Broadcasts);
                cancelled=await State(client,spender,0);Assert.Equal(before.PermitNonce,cancelled.PermitNonce);Assert.Equal(before.Nonce,cancelled.Nonce);
                await Open();NamedBox(w,"DesktopPermitPassword").Text=password;ClickName(w,"DesktopPermitSign");await Wait(w,()=>Labels(w).Contains("Проверка транзакции"));
                var transfer=(PreparedTransfer)typeof(MainWindow).GetField("prepared",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(w)!;
                Validate(transfer,kind,spender,before.Nonce);var hold=kind=="hold"?Word(transfer.Draft.Data,3):BigInteger.Zero;
                before=before with{Position=await Position(client,hold)};
                await UiFrameCapture.SaveAsync(w,Path.Combine(root,kind+"-fee.png"));
                relay.Active=transfer;relay.Operation=kind;
                w.GetLogicalDescendants().OfType<CheckBox>().Single(c=>c.Content?.ToString()?.StartsWith("Подтверждаю параметры")==true).IsChecked=true;
                Click(w,"Отправить подтвержденную транзакцию");await Wait(w,()=>Labels(w).Contains("Отправлено в RPC"));Assert.Equal(1,relay.Broadcasts);
                await UiFrameCapture.SaveAsync(w,Path.Combine(root,kind+"-sent.png"));
                var receipt=await Confirm(client,transfer.Signed.Hash);var after=await State(client,spender,hold);
                Assert.Equal(before.PermitNonce+1,after.PermitNonce);Assert.Equal(before.Nonce+1,after.Nonce);Assert.Equal(BigInteger.Zero,after.Allowance);Assert.Equal(before.Balance-Amount,after.Balance);
                if(kind=="swap")Assert.True(after.Output-before.Output>=Word(transfer.Draft.Data,3));else Assert.Equal(before.Position+Amount,after.Position);
                var replay=false;try{await client.Web3.Eth.Transactions.Call.SendRequestAsync(new CallInput{From=Owner,To=spender,Data=transfer.Draft.Data},BlockParameter.CreateLatest());}catch(Nethereum.JsonRpc.Client.RpcResponseException e){replay=e.Message.Contains("revert",StringComparison.OrdinalIgnoreCase);}
                Assert.True(replay);
                reports.Add(new{operation=kind,hash=transfer.Signed.Hash,before=Public(before),after=Public(after),hold=hold.ToString(),receiptBlock=receipt.BlockNumber.Value.ToString(),blockHash=receipt.BlockHash,gasUsed=receipt.GasUsed.Value.ToString(),gasPrice=(receipt.EffectiveGasPrice?.Value??transfer.Signed.GasPriceWei).ToString(),replayRejected=replay});
                await File.WriteAllTextAsync(Path.Combine(root,"results.json"),JsonSerializer.Serialize(new{chainId=202020,kind="real Avalonia Headless controls + real RPC/signing, not installed app",reports},new JsonSerializerOptions{WriteIndented=true}));
                relay.Broadcasts=0;
            }
        }
        finally{w?.Close();password="";File.Delete(vault);}
    }
    record Snapshot(string Block,string Balance,string Output,string Allowance,string PermitNonce,string Nonce,string Pending,string Position);
    static Snapshot Public(StateValue s)=>new(s.Block.ToString(),s.Balance.ToString(),s.Output.ToString(),s.Allowance.ToString(),s.PermitNonce.ToString(),s.Nonce.ToString(),s.Pending.ToString(),s.Position.ToString());
    // BigInteger is serialized as decimal strings explicitly below, not framework object internals.
    record StateValue(BigInteger Block,BigInteger Balance,BigInteger Output,BigInteger Allowance,BigInteger PermitNonce,BigInteger Nonce,BigInteger Pending,BigInteger Position);
    static async Task<StateValue> State(DecimalClient c,string spender,BigInteger hold){var t=new Erc20Service(c,Token);return new((await c.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value,await t.GetBalanceRawAsync(Owner),await new Erc20Service(c,Output).GetBalanceRawAsync(Owner),await t.GetAllowanceRawAsync(Owner,spender),await t.GetNonceAsync(Owner),(await c.Web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(Owner,BlockParameter.CreateLatest())).Value,(await c.Web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(Owner,BlockParameter.CreatePending())).Value,await Position(c,hold));}
    static async Task<BigInteger> Position(DecimalClient c,BigInteger hold)=>hold>0?(await new StakeReader(c.Web3,Delegation).GetHoldStakeAsync(Validator,Owner,Token,hold)).AmountRaw:(await new StakeReader(c.Web3,Delegation).GetStakeAsync(Validator,Owner,Token)).AmountRaw;
    static async Task ChainOnly(DecimalClient c)=>Assert.Equal(new BigInteger(202020),(await c.Web3.Eth.ChainId.SendRequestAsync()).Value);
    static BigInteger Word(string data,int index)=>new(Convert.FromHexString(data.Substring(10+index*64,64)),true,true);
    static void Validate(PreparedTransfer p,string kind,string spender,BigInteger nonce){Assert.Equal(202020,p.ChainId);Assert.Equal(nonce,p.Signed.Nonce);Assert.Equal(Owner,p.Draft.From,true);Assert.Equal(spender,p.Draft.To,true);Assert.Equal(BigInteger.Zero,p.Draft.ValueWei);Assert.True(p.Signed.GasLimit*p.Signed.GasPriceWei<=Units.Parse("2"));Assert.Equal(Amount,Word(p.Draft.Data,2));var sig=kind=="swap"?"convert(address,address,uint256,uint256,address,uint256,uint8,bytes32,bytes32)":kind=="hold"?"delegateHoldByPermit(address,address,uint256,uint256,uint256,uint8,bytes32,bytes32)":"delegateByPermit(address,address,uint256,uint256,uint8,bytes32,bytes32)";Assert.StartsWith("0x"+Nethereum.Util.Sha3Keccack.Current.CalculateHash(sig)[..8],p.Draft.Data);}
    static async Task<TransactionReceipt> Confirm(DecimalClient c,string hash){var limit=DateTime.UtcNow.AddMinutes(12);while(DateTime.UtcNow<limit){var r=await c.Web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(hash);if(r is not null&&(await c.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value>=r.BlockNumber.Value+11){Assert.Equal(BigInteger.One,r.Status.Value);Assert.Equal(r.BlockHash,(await c.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(new BlockParameter(r.BlockNumber))).BlockHash);return r;}await Task.Delay(3000);}throw new TimeoutException("Reconcile saved hash read-only; never resend");}
    static IEnumerable<string?> Labels(Window w)=>w.GetLogicalDescendants().OfType<TextBlock>().Select(t=>t.Text);
    static TextBox NamedBox(Window w,string name)=>w.GetLogicalDescendants().OfType<TextBox>().Single(t=>t.Name==name);
    static TextBox FieldFirst(Window w)=>w.GetLogicalDescendants().OfType<TextBox>().First();
    static TextBox Field(Window w,string label)=>w.GetLogicalDescendants().OfType<TextBlock>().Single(t=>t.Text==label).Parent!.GetLogicalDescendants().OfType<TextBox>().Single();
    static void Click(Window w,string label)=>w.GetLogicalDescendants().OfType<Button>().First(b=>b.Content?.ToString()==label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    static void ClickName(Window w,string name)=>w.GetLogicalDescendants().OfType<Button>().Single(b=>b.Name==name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    static void Invoke(MainWindow w,string method,params object[] values)=>typeof(MainWindow).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(w,values);
    static void Set(MainWindow w,string name,object value)=>typeof(MainWindow).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(w,value);
    static async Task Wait(Window w,Func<bool> ready){var limit=DateTime.UtcNow.AddSeconds(90);while(DateTime.UtcNow<limit){if(ready())return;await Task.Delay(30);}throw new TimeoutException("UI wait failed: "+string.Join(" | ",Labels(w).Where(t=>t is not null)));}

    sealed class Relay:IDisposable
    {
        readonly TestHttpListener listener=new();readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(30)};readonly string root,ledger;BigInteger maximum;public string Url{get;}public PreparedTransfer? Active;public string Operation="";public int Broadcasts;
        public Relay(string root,string ledger){this.root=root;this.ledger=ledger;var s=new TcpListener(IPAddress.Loopback,0);s.Start();var port=((IPEndPoint)s.LocalEndpoint).Port;s.Stop();Url=$"http://127.0.0.1:{port}/";listener.Prefixes.Add(Url);listener.Start();_=Task.Run(async()=>{while(listener.IsListening){TestHttpContext ctx;try{ctx=await listener.GetContextAsync();}catch{break;}_=Serve(ctx);}});}
        async Task Serve(TestHttpContext ctx){try{var text=await new StreamReader(ctx.Request.InputStream).ReadToEndAsync();using var d=JsonDocument.Parse(text);var j=d.RootElement;var method=j.GetProperty("method").GetString()!;
            if(method=="eth_sendRawTransaction"){
                var p=Interlocked.Exchange(ref Active,null)??throw new InvalidOperationException("Unapproved send");Assert.Equal(Convert.FromHexString(p.Signed.RawHex.StartsWith("0x")?p.Signed.RawHex[2..]:p.Signed.RawHex),Convert.FromHexString(j.GetProperty("params")[0].GetString()!.Replace("0x","")));Assert.Equal(202020,p.ChainId);Assert.Equal(1,Interlocked.Increment(ref Broadcasts));maximum+=p.Signed.GasLimit*p.Signed.GasPriceWei;Assert.True(maximum<=Units.Parse("6"));Assert.True(File.Exists(Path.Combine(root,"journal",p.Signed.Hash+".json")));
                using var file=new FileStream(ledger,FileMode.Append,FileAccess.Write,FileShare.Read);var bytes=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{operation=Operation,hash=p.Signed.Hash,nonce=p.Signed.Nonce.ToString(),chainId=202020})+"\n");file.Write(bytes);file.Flush(true);
            }else if(!new[]{"eth_chainId","eth_blockNumber","eth_getBlockByNumber","eth_getBalance","eth_getTransactionCount","eth_getTransactionReceipt","eth_getTransactionByHash","eth_getCode","eth_getStorageAt","eth_call","eth_estimateGas","eth_gasPrice","eth_maxPriorityFeePerGas"}.Contains(method))throw new InvalidOperationException("RPC method refused");
            using var response=await http.PostAsync("https://testnet-val.decimalchain.com/web3/",new StringContent(text,Encoding.UTF8,"application/json"));response.EnsureSuccessStatusCode();var data=await response.Content.ReadAsByteArrayAsync();await ctx.Response.OutputStream.WriteAsync(data);
        }catch(Exception e){await File.AppendAllTextAsync(Path.Combine(root,"relay-refusals.txt"),e.GetType().Name+"\n");await ctx.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("{\"jsonrpc\":\"2.0\",\"id\":1,\"error\":{\"code\":-32000,\"message\":\"Isolated Testnet relay refused request\"}}"));}finally{ctx.Response.Close();}}
        public void Dispose(){listener.Stop();http.Dispose();}
    }
}
