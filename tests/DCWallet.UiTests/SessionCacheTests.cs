using System.Numerics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using DCWallet.Core;
using Xunit;

namespace DCWallet.UiTests;

public sealed class SessionCacheTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Owner = "0x1111111111111111111111111111111111111111";
    static void Set(MainWindow window, string name, object? value) => typeof(MainWindow).GetField(name, Private)!.SetValue(window, value);
    static object? Get(MainWindow window, string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
    static void Call(MainWindow window, string name) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, null);

    [AvaloniaFact]
    public void ReopeningSameAddressCannotReuseConfirmedBalanceFromPreviousSessionOrRpc()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dc-session-cache-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var window = new MainWindow(directory, autoLoad: false);
        try
        {
            window.Show(); Set(window, "watchAddress", Owner); Call(window, "ShowAssets");
            Set(window, "nativeBalance", BigInteger.Pow(10, 18));
            Set(window, "tokenData", new DataSet<TokenBalance>([], 100, true, []));
            Set(window, "dataReadAt", DateTimeOffset.UtcNow);
            Call(window, "Lock");
            Assert.Null(Get(window, "nativeBalance")); Assert.Null(Get(window, "tokenData"));
            Assert.Equal(default(DateTimeOffset), Get(window, "dataReadAt"));
            Set(window, "watchAddress", Owner); Call(window, "ShowAssets");
            Assert.DoesNotContain(window.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Баланс подтвержден в выбранной сети");
            Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Токены еще не загружены.");
        }
        finally { window.Close(); Directory.Delete(directory, true); }
    }
}
