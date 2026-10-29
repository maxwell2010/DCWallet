using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using DCWallet.Core;
using DCWallet.Tests;
using Decimal.Web3;
using Xunit;
namespace DCWallet.UiTests;
public sealed class PickerTests
{
    [AvaloniaFact] public async Task PickerReceivesBackgroundPortfolioWithoutReplacingUserFields()
    {
        using var rpc = new DiscoveryRpc();
        var directory = Path.Combine(Path.GetTempPath(), "dc-picker-live-" + Guid.NewGuid()); Directory.CreateDirectory(directory);
        var window = new MainWindow(directory, DecimalNetwork.Mainnet() with { RpcUrl = new Uri(rpc.Url) }, dataReaderFactory: client => new WalletDataReader(client, handler: new DiscoveryCatalogHandler()));
        try
        {
            typeof(MainWindow).GetField("watchAddress", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, DiscoveryFixture.Owner);
            var input = new TextBox { Text = "manual-contract" }; var amount = new TextBox { Text = "123.000001" };
            var picker = (Control)typeof(MainWindow).GetMethod("AttachTokenPicker", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [input, false])!;
            var root = new StackPanel(); root.Children.Add(input); root.Children.Add(amount); root.Children.Add(picker); window.Content = root; window.Show();
            var list = picker.GetLogicalDescendants().OfType<ComboBox>().Single(); Assert.Empty(list.Items);
            await (Task)typeof(MainWindow).GetMethod("ReadWalletDataAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null)!;
            Assert.Contains(list.Items, item => item?.ToString()?.Contains("TEST") == true);
            Assert.Same(input, root.Children[0]); Assert.Equal("manual-contract", input.Text); Assert.Equal("123.000001", amount.Text); Assert.Equal(0, rpc.Broadcasts);
        }
        finally { window.Close(); Directory.Delete(directory, true); }
    }
    [AvaloniaFact] public async Task TokenPickerFiltersNamesAndSelectsExactAddressWithoutChangingAmount()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dc-picker-" + Guid.NewGuid()); Directory.CreateDirectory(directory);
        var window = new MainWindow(directory, autoLoad: false);
        try
        {
            const string first = "0x1111111111111111111111111111111111111111", second = "0x2222222222222222222222222222222222222222";
            typeof(MainWindow).GetField("tokenData", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, new DataSet<TokenBalance>([new(first, "First", "TEST", 6, 1234567), new(second, "Second", "OTHER", 18, 1)], 100, true, []));
            var input = new TextBox { Text = "manual" }; var amount = new TextBox { Text = "999999999999999999999.000000000000000001" };
            var picker = (Control)typeof(MainWindow).GetMethod("AttachTokenPicker", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [input, false])!;
            var root = new StackPanel(); root.Children.Add(input); root.Children.Add(amount); root.Children.Add(picker); window.Content = root; window.Show(); await Task.Delay(30);
            var search = picker.GetLogicalDescendants().OfType<TextBox>().Single(); search.Text = "other"; await Task.Delay(20);
            var list = picker.GetLogicalDescendants().OfType<ComboBox>().Single(); Assert.Single(list.Items); list.SelectedIndex = 0;
            Assert.Equal(second, input.Text); Assert.Equal("999999999999999999999.000000000000000001", amount.Text);
            input.Text = "0x3333333333333333333333333333333333333333"; Assert.Equal("0x3333333333333333333333333333333333333333", input.Text);
            typeof(MainWindow).GetField("tokenData", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, new DataSet<TokenBalance>([new(first, "New balance", "OTHER NEW", 6, 999999), new(second, "Second", "OTHER", 18, 1)], 101, true, []));
            ((Action?)typeof(MainWindow).GetField("PortfolioUpdated", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window))?.Invoke(); await Task.Delay(20);
            Assert.Equal(2, list.Items.Count); Assert.Equal("other", search.Text);
            Assert.Equal("0x3333333333333333333333333333333333333333", input.Text);
            Assert.Equal("999999999999999999999.000000000000000001", amount.Text);
            window.Content = new TextBlock();
            Assert.Null(typeof(MainWindow).GetField("PortfolioUpdated", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window));
        }
        finally { window.Close(); Directory.Delete(directory, true); }
    }
}
