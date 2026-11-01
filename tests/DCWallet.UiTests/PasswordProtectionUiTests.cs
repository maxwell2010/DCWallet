using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using DCWallet.Core;
using Decimal.Web3;
using Decimal.Web3.Wallet;
using Xunit;
namespace DCWallet.UiTests;
public sealed class PasswordProtectionUiTests
{
    [AvaloniaFact]public async Task WarningConsentDisablePasswordlessUnlockAndReenable()
    {
        var dir=Path.Combine(Path.GetTempPath(),"dc-password-ui-"+Guid.NewGuid());var path=Path.Combine(dir,"wallet.vault");MainWindow? w=null;
        try{
            await Vault.CreateAsync(path,DecimalWallet.GenerateMnemonic(),"synthetic-ui-password");w=new MainWindow(dir,DecimalNetwork.Testnet(),autoLoad:false);w.Show();Field(w,"Пароль кошелька").Text="synthetic-ui-password";Click(w,"Открыть кошелек");await Wait(()=>Labels(w).Contains("Баланс еще не загружен"));
            typeof(MainWindow).GetMethod("ShowSettings",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(w,null);
            Assert.Contains(Labels(w),s=>s?.Contains("любой, кто получит файл кошелька")==true);Field(w,"Пароль для изменения защиты").Text="synthetic-ui-password";Click(w,"Отключить пароль");await Wait(()=>Labels(w).Contains("Подтвердите предупреждение безопасности."));Assert.False(Vault.IsPasswordless(path));
            w.GetLogicalDescendants().OfType<CheckBox>().Single(c=>c.Content?.ToString()=="Понимаю риск и хочу отключить пароль").IsChecked=true;Click(w,"Отключить пароль");await Wait(()=>Vault.IsPasswordless(path)&&Labels(w).Contains("Открыть кошелек"));
            Assert.False(((Control)Field(w,"Пароль кошелька").Parent!).IsVisible);Click(w,"Открыть кошелек");await Wait(()=>Labels(w).Contains("Баланс еще не загружен"));
            typeof(MainWindow).GetMethod("ShowSettings",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(w,null);Field(w,"Новый пароль для включения защиты").Text="new-synthetic-ui-password";Field(w,"Повтор нового пароля защиты").Text="new-synthetic-ui-password";Click(w,"Включить парольную защиту");await Wait(()=>!Vault.IsPasswordless(path)&&Labels(w).Contains("Открыть кошелек"));
            Assert.True(((Control)Field(w,"Пароль кошелька").Parent!).IsVisible);
        }finally{w?.Close();if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }
    static IEnumerable<string?> Labels(Window w)=>w.GetLogicalDescendants().OfType<TextBlock>().Select(x=>x.Text);
    static TextBox Field(Window w,string label)=>w.GetLogicalDescendants().OfType<TextBlock>().Single(x=>x.Text==label).Parent!.GetLogicalDescendants().OfType<TextBox>().Single();
    static void Click(Window w,string label)=>w.GetLogicalDescendants().OfType<Button>().First(x=>x.Content?.ToString()==label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    static async Task Wait(Func<bool> f){for(var i=0;i<500&&!f();i++)await Task.Delay(20);Assert.True(f());}
}
