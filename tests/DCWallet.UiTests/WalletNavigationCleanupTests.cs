using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using Avalonia.LogicalTree;
using DCWallet.Core;
using DCWallet.Tests;
using Decimal.Web3;
using Decimal.Web3.Wallet;
using Xunit;
namespace DCWallet.UiTests;
public sealed class WalletNavigationCleanupTests
{
    const string Owner=DiscoveryFixture.Owner;
    static IEnumerable<string?> Labels(Window w)=>w.GetLogicalDescendants().OfType<TextBlock>().Select(t=>t.Text);
    static void Click(Window w,string label)=>w.GetLogicalDescendants().OfType<Button>().First(b=>b.Content?.ToString()==label||b.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text==label)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    static void Set(MainWindow w,string field,object value)=>typeof(MainWindow).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(w,value);
    [AvaloniaFact]
    public async Task QuietCoverageBridgeAndHistoryInEveryTheme()
    {
        foreach(var theme in new[]{"light","dark","neon"})
        {
            var dir=Path.Combine(Path.GetTempPath(),"dc-nav-cleanup-"+Guid.NewGuid());Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(Path.Combine(dir,"preferences.json"),"{\"Theme\":\""+theme+"\"}");
            var w=new MainWindow(dir,autoLoad:false);
            try
            {
                w.Show();Click(w,"Только просмотр");w.GetLogicalDescendants().OfType<TextBox>().First().Text=Owner;Click(w,"Открыть для просмотра");
                Assert.DoesNotContain("Инструменты",Labels(w));
                var copy=w.GetLogicalDescendants().OfType<Button>().Single(b=>b.Name=="WalletAddressCopy");
                Assert.DoesNotContain("фраза",((TextBlock)copy.Content!).Text);Assert.DoesNotContain("просмотр",((TextBlock)copy.Content!).Text);
                copy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(30);
                Assert.Equal(Owner,await w.Clipboard!.TryGetTextAsync());
                Click(w,"Мост");Assert.Contains("Межсетевые переводы временно недоступны.",Labels(w));
                Assert.DoesNotContain(w.GetLogicalDescendants().OfType<Button>(),b=>b.Content?.ToString()=="Подготовить операцию");
                var shots=Environment.GetEnvironmentVariable("DC_QA_DIR")??Path.Combine(AppContext.BaseDirectory,"screenshots");Directory.CreateDirectory(shots);
                await UiFrameCapture.SaveAsync(w,Path.Combine(shots,theme+"-bridge-paused.png"));
                Set(w,"nftData",new DataSet<NftBalance>([],100,false,[]){CoverageNotes=["Внешние коллекции вне автоматического поиска."]});
                Set(w,"stakeData",new DataSet<WalletStake>([],100,true,[]));
                Click(w,"NFT");Assert.DoesNotContain(w.GetLogicalDescendants().OfType<Expander>(),e=>e.Header?.ToString()?.Contains("источников")==true||e.Header?.ToString()=="Не удалось обновить некоторые данные");
                var row=new HistoryRow("0x"+new string('a',64),100,DateTimeOffset.UnixEpoch,Owner,DiscoveryFixture.Token,1,1,true,"Перевод",Verification:"indexer-evm");
                Set(w,"historyData",new HistoryPage([row],1,1,false,1,false,["Часть старых записей"]));Set(w,"historyOwner","75:"+Owner);
                Click(w,"История");
                Assert.DoesNotContain(Labels(w),v=>v?.Contains("Часть старых")==true||v?.Contains("Ошибок проверки")==true);
                Assert.Contains(Labels(w),v=>v?.Contains("Данные архива")==true);
                Assert.DoesNotContain(Labels(w),v=>v?.Contains("Перевод · Исполнено")==true);
                await UiFrameCapture.SaveAsync(w,Path.Combine(shots,theme+"-history-clean.png"));
            }
            finally{w.Close();Directory.Delete(dir,true);}
        }
    }
    [AvaloniaFact]
    public async Task SendOffersBurnRatherThanDuplicateChecksAndRequiresConfirmation()
    {
        var dir=Path.Combine(Path.GetTempPath(),"dc-burn-nav-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        await Vault.CreateAsync(Path.Combine(dir,"wallet.vault"),DecimalWallet.GenerateMnemonic(),"temporary-test-password");
        await File.WriteAllTextAsync(Path.Combine(dir,"preferences.json"),"{\"Theme\":\"neon\"}");
        var w=new MainWindow(dir,autoLoad:false);
        try
        {
            w.Show();w.GetLogicalDescendants().OfType<TextBox>().Single().Text="temporary-test-password";Click(w,"Открыть кошелек");
            for(var i=0;i<100&&!Labels(w).Contains("Баланс еще не загружен");i++)await Task.Delay(25);
            Click(w,"Отправить");Assert.DoesNotContain(w.GetLogicalDescendants().OfType<Button>(),b=>b.Content?.ToString()=="Чеком");Assert.Contains(w.GetLogicalDescendants().OfType<Button>(),b=>b.Content?.ToString()=="Сжигание");
            Click(w,"Сжигание");Assert.Contains("Я понимаю, что сжигание необратимо",w.GetLogicalDescendants().OfType<CheckBox>().Select(c=>c.Content?.ToString()));
            var shots=Environment.GetEnvironmentVariable("DC_QA_DIR")??Path.Combine(AppContext.BaseDirectory,"screenshots");Directory.CreateDirectory(shots);
            await UiFrameCapture.SaveAsync(w,Path.Combine(shots,"neon-send-burn.png"));
            w.GetLogicalDescendants().OfType<TextBox>().Single().Text="1";Click(w,"Продолжить");
            await Task.Delay(50);Assert.Contains("Подтвердите необратимость сжигания.",Labels(w));
        }
        finally{w.Close();Directory.Delete(dir,true);}
    }
}
