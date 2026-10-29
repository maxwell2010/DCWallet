using System.Reflection;
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
public sealed class SendRefreshTests
{
    const string Seed = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Call(MainWindow window, string name) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, null);
    static TextBox Field(MainWindow window, string name) => window.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Text == name).Parent!.GetLogicalDescendants().OfType<TextBox>().Single();
    static void Click(MainWindow window, string name) => window.GetLogicalDescendants().OfType<Button>().First(b => b.Content?.ToString() == name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    [AvaloniaFact] public async Task SendAssetRefreshPreservesManualContractAndExactTypedAmount()
    {
        using var rpc = new DiscoveryRpc(); using var signer = WalletSigningSession.FromMnemonic(Seed, 75);
        var dir = Path.Combine(Path.GetTempPath(), "dc-send-refresh-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
        var window = new MainWindow(dir, DecimalNetwork.Mainnet() with { RpcUrl = new Uri(rpc.Url) }, autoLoad: false);
        try
        {
            typeof(MainWindow).GetField("signer", Private)!.SetValue(window, signer); window.Show(); Call(window, "ShowSend");
            var asset = window.GetLogicalDescendants().OfType<ComboBox>().Single(c => c.Items.Cast<object>().Contains("Другой ERC20")); asset.SelectedIndex = asset.ItemCount - 1;
            var contract = Field(window, "Контракт токена ERC20"); var amount = Field(window, "Сумма"); contract.Text = DiscoveryFixture.Owner; amount.Text = "1234567890123456789.000000000000000001";
            void Refresh(long raw)
            {
                typeof(MainWindow).GetField("tokenData", Private)!.SetValue(window, new DataSet<TokenBalance>([new(DiscoveryFixture.Token, "Token", "TEST", 6, raw)], 100, true, []));
                ((Action?)typeof(MainWindow).GetField("PortfolioUpdated", Private)!.GetValue(window))?.Invoke();
            }
            await Task.Delay(30); Refresh(1234567); await Task.Delay(10);
            Assert.Contains(asset.Items, i => i?.ToString()?.Contains("TEST") == true); Assert.Equal(2, asset.SelectedIndex); Assert.Equal(DiscoveryFixture.Owner, contract.Text);
            Assert.Equal("1234567890123456789.000000000000000001", amount.Text); Assert.Same(amount, Field(window, "Сумма"));
            asset.SelectedIndex = 1; Assert.Equal(DiscoveryFixture.Token, contract.Text); Refresh(9000000); await Task.Delay(10);
            Assert.Equal(1, asset.SelectedIndex); Assert.Equal(DiscoveryFixture.Token, contract.Text); Assert.Equal("1234567890123456789.000000000000000001", amount.Text); Assert.Equal(0, rpc.Broadcasts);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }
    [AvaloniaFact] public async Task MultisendRowsAndPermitVisibilityWorkAndEmptyListCannotBroadcast()
    {
        using var rpc = new DiscoveryRpc(); using var signer = WalletSigningSession.FromMnemonic(Seed, 75);
        var dir = Path.Combine(Path.GetTempPath(), "dc-multisend-ui-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
        var window = new MainWindow(dir, DecimalNetwork.Mainnet() with { RpcUrl = new Uri(rpc.Url) }, autoLoad: false);
        try
        {
            typeof(MainWindow).GetField("signer", Private)!.SetValue(window, signer); window.Show(); Call(window, "ShowMultisend");
            int Rows() => window.GetLogicalDescendants().OfType<TextBox>().Count(t => t.Watermark == "Получатель 0x...");
            Assert.Equal(2, Rows()); Click(window, "Добавить получателя"); await Task.Delay(10); Assert.Equal(3, Rows());
            var permit = window.GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Content?.ToString()?.Contains("EIP-2612") == true); Assert.False(permit.IsVisible);
            var asset = window.GetLogicalDescendants().OfType<ComboBox>().Single(c => c.Items.Cast<object>().SequenceEqual(new[] { "DEL", "ERC20" })); asset.SelectedIndex = 1; Assert.True(permit.IsVisible);
            var password = Field(window, "Пароль кошелька для permit"); Assert.False(((Control)password.Parent!).IsVisible); permit.IsChecked = true; Assert.True(((Control)password.Parent!).IsVisible);
            asset.SelectedIndex = 0; Assert.False(((Control)password.Parent!).IsVisible);
            while (Rows() > 0) { Click(window, "Убрать"); await Task.Delay(10); }
            Click(window, "Подготовить рассылку"); await Task.Delay(10);
            var status = (TextBlock)typeof(MainWindow).GetField("status", Private)!.GetValue(window)!; Assert.Contains("Добавьте хотя бы одного", status.Text); Assert.Equal(0, rpc.Broadcasts);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }
}
