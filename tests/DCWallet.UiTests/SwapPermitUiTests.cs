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
using Decimal.Web3.Tokens;
using Decimal.Web3.Wallet;
using Xunit;
namespace DCWallet.UiTests;
public sealed class SwapPermitUiTests
{
    [AvaloniaFact] public Task ChangedUnpinnedContractPreventsPermitSignatureAndApproval() => Scenario("unpinned");
    [AvaloniaFact] public Task InsufficientNonzeroAllowanceRequiresExplicitRevocation() => Scenario("revoke");
    [AvaloniaFact] public Task WorseQuoteCannotReplaceOriginalMinimum() => Scenario("price");
    static async Task Scenario(string mode)
    {
        var phrase = DecimalWallet.GenerateMnemonic(); var owner = DecimalWallet.DeriveAddress(phrase);
        var token = DiscoveryFixture.Token; var output = "0x3333333333333333333333333333333333333333"; var buys = 0;
        var domain = Erc20PermitService.CalculateDomainSeparator(new Erc20PermitData(token,"Reserve test","1",owner,token,1,0,2000000000,202020));
        using var rpc = new FakeRpc { ReadResult = (method,args) =>
        {
            if(method != "eth_call") return method switch { "eth_chainId"=>"0x31524", "eth_estimateGas"=>"0x5208", "eth_gasPrice"=>"0x1", "eth_getBalance"=>"0x56bc75e2d63100000", "eth_blockNumber"=>"0x1", _=>"0x0" };
            var data=args[0].GetProperty("data").GetString()!;
            if(data.StartsWith(Selector("decimals()"))) return Word(6);
            if(data.StartsWith(Selector("symbol()"))) return Str("TEST");
            if(data.StartsWith(Selector("name()"))) return Str("Reserve test");
            if(data.StartsWith(Selector("version()"))) return Str("1");
            if(data.StartsWith(Selector("totalSupply()"))) return Word(1000000000);
            if(data.StartsWith(Selector("balanceOf(address)"))) return Word(100000000);
            if(data.StartsWith(Selector("nonces(address)"))) return Word(0);
            if(data.StartsWith(Selector("DOMAIN_SEPARATOR()"))) return "0x"+Convert.ToHexString(mode=="unsupported"?new byte[32]:domain);
            if(data.StartsWith(Selector("allowance(address,address)"))) return Word(mode=="revoke"?1:0);
            if(data.StartsWith(Selector("calculateBuyOutput(uint256)"))) return Word(mode=="price"&&Interlocked.Increment(ref buys)>1?1:1500000);
            if(data.StartsWith(Selector("calculateSellOutput(uint256)"))) return Word(BigInteger.Parse("500000000000000000"));
            return "0x"+token[2..].PadLeft(64,'0');
        }};
        var dir=Path.Combine(Path.GetTempPath(),"dc-swap-permit-"+Guid.NewGuid()); MainWindow? window=null;
        try
        {
            await Vault.CreateAsync(Path.Combine(dir,"wallet.vault"),phrase,"temporary-permit-password");
            window=new MainWindow(dir,DecimalNetwork.Testnet() with {RpcUrl=new Uri(rpc.Url)},autoLoad:false);window.Show();
            window.GetLogicalDescendants().OfType<TextBox>().Single().Text="temporary-permit-password";Click(window,"Открыть кошелек");await Wait(()=>Labels(window).Contains("Баланс еще не загружен"));
            typeof(MainWindow).GetMethod("ShowSwap",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null);
            window.GetLogicalDescendants().OfType<ComboBox>().Single(c=>c.Items.Cast<object>().Contains("DEL → Токен")).SelectedIndex=2;
            Field(window,"Контракт резервного токена").Text=token;Field(window,"Получить резервный токен").Text=output;Field(window,"Сколько отдать").Text="1";
            Click(window,"Обновить цену");await Wait(()=>Labels(window).Any(t=>t?.Contains("Котировка обновлена")==true));Click(window,"Продолжить обмен");
            if(mode=="revoke") await Wait(()=>Labels(window).Any(t=>t?.Contains("явно отзовите")==true));
            else if(mode=="price") await Wait(()=>Labels(window).Any(t=>t?.Contains("Цена ухудшилась")==true));
            else await Wait(()=>Labels(window).Contains("Не удалось подтвердить контракт, баланс или разрешение Permit. Повторите проверку."));
            Assert.Equal(0,rpc.Broadcasts);Assert.DoesNotContain("Проверка транзакции",Labels(window));
            Assert.DoesNotContain("Разрешение по подписи",Labels(window));
            if(mode=="revoke")
            {
                Click(window,"Отозвать прежнее разрешение конвертации");await Wait(()=>Labels(window).Contains("Проверка транзакции"));
                var calldata=window.GetLogicalDescendants().OfType<TextBox>().Single(t=>t.IsReadOnly).Text!;
                Assert.StartsWith(Selector("approve(address,uint256)"),calldata);
                Assert.Equal(new string('0',64),calldata[^64..]);Assert.Equal(0,rpc.Broadcasts);
            }
        }
        finally {window?.Close();if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }
    static TextBox Field(Window w,string name)=>w.GetLogicalDescendants().OfType<TextBlock>().Single(t=>t.Text==name).Parent!.GetLogicalDescendants().OfType<TextBox>().Single();
    static IEnumerable<string?> Labels(Window w)=>w.GetLogicalDescendants().OfType<TextBlock>().Select(t=>t.Text);
    static void Click(Window w,string name)=>w.GetLogicalDescendants().OfType<Button>().First(b=>b.Content?.ToString()==name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    static async Task Wait(Func<bool> ready){for(var i=0;i<400&&!ready();i++)await Task.Delay(15);Assert.True(ready());}
    static string Selector(string s)=>"0x"+Nethereum.Util.Sha3Keccack.Current.CalculateHash(s)[..8];
    static string Word(BigInteger n)=>"0x"+n.ToString("x").PadLeft(64,'0');
    static string Str(string s)=>Word(32)+Word(s.Length)[2..]+Convert.ToHexString(Encoding.UTF8.GetBytes(s)).PadRight(64,'0');
}
