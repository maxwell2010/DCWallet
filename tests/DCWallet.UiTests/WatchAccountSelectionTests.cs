using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Xunit;
namespace DCWallet.UiTests;
public sealed class WatchAccountSelectionTests
{
    [AvaloniaFact]
    public void WatchAfterDerivedAddressDoesNotTriggerUnexpectedLockOrInventMoreAddresses()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dc-watch-selection-" + Guid.NewGuid());
        Directory.CreateDirectory(directory); var window = new MainWindow(directory,autoLoad:false);
        try
        {
            window.Show();
            typeof(MainWindow).GetField("accountIndex",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(window,3);
            typeof(MainWindow).GetMethod("ShowWatch",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null);
            const string owner = "0x1111111111111111111111111111111111111111";
            window.GetLogicalDescendants().OfType<TextBox>().First().Text = owner;
            window.GetLogicalDescendants().OfType<Button>().Single(b=>b.Content?.ToString()=="Открыть для просмотра").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(owner,typeof(MainWindow).GetField("watchAddress",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window));
            var selector = window.GetLogicalDescendants().OfType<ComboBox>().Single(c=>c.Name=="AccountSelector");
            Assert.Single(selector.Items); Assert.False(selector.IsEnabled); Assert.Equal(0,selector.SelectedIndex);
        }
        finally { window.Close(); Directory.Delete(directory,true); }
    }
}
