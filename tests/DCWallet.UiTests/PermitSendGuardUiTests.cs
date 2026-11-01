using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using DCWallet.Core;
using DCWallet.Tests;
using Decimal.Web3;
using Decimal.Web3.Transactions;
using Decimal.Web3.Wallet;
using Xunit;
namespace DCWallet.UiTests;

/// <summary>Exercise the shared final-send guard with synthetic wallets and loopback RPC only.</summary>
public sealed class PermitSendGuardUiTests
{
    [AvaloniaFact] public Task LockDuringFinalRevalidationPreventsBroadcast() => Cancellation("Lock");
    [AvaloniaFact] public Task NavigationDuringFinalRevalidationPreventsBroadcast() => Cancellation("ShowSend");

    static async Task Cancellation(string action)
    {
        using var rpc=new FakeRpc{Chain="0x31524",AcceptBroadcast=true};
        var dir=Path.Combine(Path.GetTempPath(),"dc-permit-send-cancel-"+Guid.NewGuid());MainWindow? w=null;
        var entered=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);var calls=0;
        try
        {
            w=await Open(dir,rpc);
            async Task Guard(){if(Interlocked.Increment(ref calls)>1){entered.TrySetResult(true);await release.Task;}}
            await Preview(w,Guard);
            Confirm(w);Click(w,"Отправить подтвержденную транзакцию");await Wait(()=>entered.Task.IsCompleted);
            // A sidebar navigation/lock can occur while asynchronous revalidation is pending.
            typeof(MainWindow).GetMethod(action,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(w,null);
            release.TrySetResult(true);await Wait(()=>!Busy(w));
            Assert.Equal(2,calls);Assert.Equal(0,rpc.Broadcasts);
            Assert.Null(typeof(MainWindow).GetField("prepared",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w));
            Assert.False(Directory.Exists(Path.Combine(dir,"journal"))&&Directory.EnumerateFiles(Path.Combine(dir,"journal"),"*.json").Any());
        }
        finally{release.TrySetResult(true);w?.Close();if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }

    [AvaloniaFact] public async Task RepeatedSendClickWhileFinalGuardAwaitsHasOneCallbackAndOneBroadcast()
    {
        using var rpc=new FakeRpc{Chain="0x31524",AcceptBroadcast=true};
        var dir=Path.Combine(Path.GetTempPath(),"dc-permit-double-send-"+Guid.NewGuid());MainWindow? w=null;
        var entered=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);var calls=0;
        try
        {
            w=await Open(dir,rpc);
            async Task Guard(){if(Interlocked.Increment(ref calls)>1){entered.TrySetResult(true);await release.Task;}}
            await Preview(w,Guard);Confirm(w);
            var send=w.GetLogicalDescendants().OfType<Button>().Single(b=>b.Content?.ToString()=="Отправить подтвержденную транзакцию");
            send.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Wait(()=>entered.Task.IsCompleted);
            // Raise a second event deliberately, even though the normal UI is already disabled.
            send.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(2,calls);Assert.Equal(0,rpc.Broadcasts);
            release.TrySetResult(true);await Wait(()=>!Busy(w));
            Assert.Equal(2,calls);Assert.Equal(1,rpc.Broadcasts);
            Assert.Contains("Отправлено в RPC",Labels(w));
            var journal=Directory.GetFiles(Path.Combine(dir,"journal"),"*.json");Assert.Single(journal);
        }
        finally{release.TrySetResult(true);w?.Close();if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }

    static async Task<MainWindow> Open(string dir,FakeRpc rpc)
    {
        await Vault.CreateAsync(Path.Combine(dir,"wallet.vault"),DecimalWallet.GenerateMnemonic(),"local-guard-password");
        var w=new MainWindow(dir,DecimalNetwork.Testnet() with{RpcUrl=new Uri(rpc.Url)},autoLoad:false);w.Show();
        w.GetLogicalDescendants().OfType<TextBox>().Single().Text="local-guard-password";
        Click(w,"Открыть кошелек");await Wait(()=>Labels(w).Contains("Баланс еще не загружен"));return w;
    }
    static Task Preview(MainWindow w,Func<Task> guard)
    {
        var owner=(string)typeof(MainWindow).GetProperty("Address",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w)!;
        var draft=TransactionDraft.Del(owner,"0x1111111111111111111111111111111111111111","0.001");
        return (Task)typeof(MainWindow).GetMethod("Preview",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(w,new object?[]{draft,"Локальная проверка защиты отправки",null,null,null,guard})!;
    }
    static void Confirm(MainWindow w)=>w.GetLogicalDescendants().OfType<CheckBox>().Single(c=>c.Content?.ToString()?.StartsWith("Подтверждаю параметры")==true).IsChecked=true;
    static bool Busy(MainWindow w)=>(bool)typeof(MainWindow).GetField("busy",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w)!;
    static IEnumerable<string?> Labels(MainWindow w)=>w.GetLogicalDescendants().OfType<TextBlock>().Select(t=>t.Text);
    static void Click(MainWindow w,string label)=>w.GetLogicalDescendants().OfType<Button>().Single(b=>b.Content?.ToString()==label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    static async Task Wait(Func<bool> ready){for(var i=0;i<500&&!ready();i++)await Task.Delay(10);Assert.True(ready());}
}
