using System.Collections.Concurrent;
using System.Numerics;
using System.Reflection;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using DCWallet.Core;
using DCWallet.Tests;
using Decimal.Web3;
using Decimal.Web3.Wallet;
using Xunit;
namespace DCWallet.UiTests;
public sealed class SwapQuoteTests
{
    [AvaloniaFact]public Task BuyQuoteUsesDeployedExactDelSelectorWithoutApproval()=>Scenario(false);
    [AvaloniaFact]public Task SellQuoteUsesDeployedExactTokensSelectorWithoutApproval()=>Scenario(true);
    [AvaloniaFact]public Task InFlightQuoteCannotBindOldAmountToEditedForm()=>Scenario(false,true);
    [AvaloniaFact]public Task TokenToTokenUsesCenterConvertWithExactMinimum()=>Scenario(false,false,true);
    static async Task Scenario(bool sell,bool race=false,bool convert=false)
    {
        var calls=new ConcurrentBag<string>();using var gate=new ManualResetEventSlim(!race);var quoteStarted=0;
        using var rpc=new FakeRpc{ReadResult=(method,args)=>
        {
            if(method!="eth_call")return method switch{"eth_chainId"=>"0x4b","eth_estimateGas"=>"0x5208","eth_gasPrice"=>"0x1","eth_getBalance"=>"0x56bc75e2d63100000",_=>"0x0"};
            var data=args[0].GetProperty("data").GetString()!;calls.Add(data[..10]);
            if(data.StartsWith(Selector("decimals()")))return Word(6);
            if(data.StartsWith(Selector("symbol()")))return Str("TEST");
            if(data.StartsWith(Selector("name()")))return Str("Reserve test");
            if(data.StartsWith(Selector("totalSupply()")))return Word(1000000000);
            if(data.StartsWith(Selector("calculateBuyOutput(uint256)"))) {Interlocked.Increment(ref quoteStarted);gate.Wait(TimeSpan.FromSeconds(5));return Word(1500000);}
            if(data.StartsWith(Selector("calculateSellOutput(uint256)")))return Word(BigInteger.Parse("500000000000000000"));
            return "0x"+DiscoveryFixture.Token[2..].PadLeft(64,'0');
        }};
        var dir=Path.Combine(Path.GetTempPath(),"dc-swap-quote-"+Guid.NewGuid());MainWindow? window=null;
        try
        {
            await Vault.CreateAsync(Path.Combine(dir,"wallet.vault"),DecimalWallet.GenerateMnemonic(),"temporary-swap-password");
            window=new MainWindow(dir,DecimalNetwork.Mainnet()with{RpcUrl=new Uri(rpc.Url)},autoLoad:false);window.Show();window.GetLogicalDescendants().OfType<TextBox>().Single().Text="temporary-swap-password";Click(window,"Открыть кошелек");await Wait(()=>Labels(window).Contains("Баланс еще не загружен"));
            typeof(MainWindow).GetMethod("ShowSwap",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null);
            var direction=window.GetLogicalDescendants().OfType<ComboBox>().Single(c=>c.Items.Cast<object>().Contains("DEL → Токен"));direction.SelectedIndex=convert?2:sell?1:0; if(convert)Field(window,"Получить резервный токен").Text="0x3333333333333333333333333333333333333333";
            Field(window,"Контракт резервного токена").Text=DiscoveryFixture.Token;var amount=Field(window,"Сколько отдать");amount.Text="1";
            Click(window,"Обновить цену");
            if(race)
            {
                await Wait(()=>Volatile.Read(ref quoteStarted)>0);amount.Text="2";gate.Set();await Task.Delay(150);
                Click(window,"Продолжить обмен");await Wait(()=>Labels(window).Any(t=>t?.Contains("Обновите цену для выбранных параметров")==true));
                Assert.DoesNotContain("Проверка транзакции",Labels(window));amount.Text="1";Click(window,"Обновить цену");
            }
            await Wait(()=>Labels(window).Any(t=>t?.StartsWith("Получите примерно")==true));
            Assert.Contains(Labels(window),t=>t?.StartsWith(sell?"Получите примерно 0.5 DEL":"Получите примерно 1.5 TEST")==true);
            Assert.DoesNotContain(window.GetLogicalDescendants().OfType<Button>(),b=>b.IsVisible && !convert && b.Content?.ToString()?.Contains("Разрешить")==true);
            amount.Text="2";Click(window,"Продолжить обмен");await Wait(()=>Labels(window).Any(t=>t?.Contains("Обновите цену для выбранных параметров")==true));Assert.DoesNotContain("Проверка транзакции",Labels(window));
            Click(window,"Обновить цену");await Wait(()=>Labels(window).Any(t=>t?.Contains("Котировка обновлена")==true));Click(window,"Продолжить обмен");await Wait(()=>Labels(window).Contains("Проверка транзакции"));
            var encoded=window.GetLogicalDescendants().OfType<TextBox>().Single(t=>t.IsReadOnly).Text!;
            Assert.StartsWith(Selector(convert?"convert(address,address,uint256,uint256,address)":sell?"sellExactTokensForDEL(uint256,uint256,address)":"buyTokenForExactDEL(uint256,address)"),encoded);
            var first=BigInteger.Parse("0"+encoded.Substring(convert?138:10,64),System.Globalization.NumberStyles.HexNumber);
            Assert.Equal(convert||sell?new BigInteger(2000000):new BigInteger(1485000),first); if(convert)Assert.Equal(new BigInteger(1485000),BigInteger.Parse("0"+encoded.Substring(202,64),System.Globalization.NumberStyles.HexNumber));
            if(!convert)Assert.DoesNotContain(Selector("allowance(address,address)"),calls);Assert.DoesNotContain(Selector("approve(address,uint256)"),calls);Assert.Equal(0,rpc.Broadcasts);
        }
        finally{window?.Close();if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }
    static TextBox Field(Window w,string name)=>w.GetLogicalDescendants().OfType<TextBlock>().Single(t=>t.Text==name).Parent!.GetLogicalDescendants().OfType<TextBox>().Single();
    static IEnumerable<string?> Labels(Window w)=>w.GetLogicalDescendants().OfType<TextBlock>().Select(t=>t.Text);
    static void Click(Window w,string label)=>w.GetLogicalDescendants().OfType<Button>().First(b=>b.Content?.ToString()==label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    static async Task Wait(Func<bool> ready){for(var i=0;i<300&&!ready();i++)await Task.Delay(15);Assert.True(ready());}
    static string Selector(string s)=>"0x"+Nethereum.Util.Sha3Keccack.Current.CalculateHash(s)[..8];
    static string Word(BigInteger n)=>"0x"+n.ToString("x").PadLeft(64,'0');
    static string Str(string s)=>Word(32)+Word(s.Length)[2..]+Convert.ToHexString(Encoding.UTF8.GetBytes(s)).PadRight(64,'0');
}
