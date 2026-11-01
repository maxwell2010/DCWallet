using System.Numerics;
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
public sealed class DesktopStakingPermitUiTests
{
    const string Token = "0x2222222222222222222222222222222222222222";
    const string Contract = "0x3333333333333333333333333333333333333333";
    const string Validator = "0x4444444444444444444444444444444444444444";
    const long BlockTime = 1800000000;

    [AvaloniaFact]
    public async Task ExistingAllowanceUsesOrdinaryHoldAndTestnetDeadlineComesFromBlock()
    {
        using var rpc = Rpc(1000000000000000000);
        var dir = Path.Combine(Path.GetTempPath(), "dc-desktop-hold-" + Guid.NewGuid()); MainWindow? w = null;
        try
        {
            w = await Open(dir, rpc); SelectToken(w); Field(w, "Адрес валидатора").Text = Validator; Field(w, "Количество").Text = "0.001";
            w.GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Content?.ToString() == "Делегировать с hold до выбранной даты").IsChecked = true;
            var shortHold = w.GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Name == "DesktopShortHold"); Assert.True(shortHold.IsVisible); shortHold.IsChecked = true;
            Assert.Equal("5", w.GetLogicalDescendants().OfType<TextBox>().Single(c => c.Name == "DesktopHoldMinutes").Text);
            Click(w, "Делегировать"); await Wait(() => Labels(w).Contains("Проверка транзакции"));
            var encoded = w.GetLogicalDescendants().OfType<TextBox>().Single(t => t.IsReadOnly).Text!;
            Assert.StartsWith(Selector("delegateHold(address,address,uint256,uint256)"), encoded);
            Assert.Equal(new BigInteger(BlockTime + 300), BigInteger.Parse("0" + encoded.Substring(202, 64), System.Globalization.NumberStyles.HexNumber));
            Assert.Equal(0, rpc.Broadcasts);
        }
        finally { w?.Close(); if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public async Task InsufficientNonzeroAllowanceRequiresExplicitRevokeAndNeverAutoApproves()
    {
        using var rpc = Rpc(1);
        var dir = Path.Combine(Path.GetTempPath(), "dc-desktop-reset-" + Guid.NewGuid()); MainWindow? w = null;
        try
        {
            w = await Open(dir, rpc); SelectToken(w); Field(w, "Адрес валидатора").Text = Validator; Field(w, "Количество").Text = "0.001";
            Click(w, "Делегировать"); await Wait(() => Labels(w).Any(s => s?.Contains("Сначала явно отзовите") == true));
            Assert.DoesNotContain("Проверка транзакции", Labels(w));
            Click(w, "Разрешить использование выбранного токена / NFT"); await Wait(() => Labels(w).Any(s => s?.Contains("Сначала явно отзовите прежнее разрешение") == true));
            Assert.Equal(0, rpc.Broadcasts);
            Click(w, "Отозвать разрешение выбранного токена"); await Wait(() => Labels(w).Contains("Проверка транзакции"));
            var encoded = w.GetLogicalDescendants().OfType<TextBox>().Single(t => t.IsReadOnly).Text!;
            Assert.StartsWith(Selector("approve(address,uint256)"), encoded); Assert.Equal(new string('0', 64), encoded.Substring(74, 64));
            Assert.Equal(0, rpc.Broadcasts);
        }
        finally { w?.Close(); if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    static FakeRpc Rpc(BigInteger allowance) => new()
    {
        Chain = "0x31524", ReadResult = (method, args) =>
        {
            if (method == "eth_getBlockByNumber") return new { number = "0x10", timestamp = "0x" + BlockTime.ToString("x"), hash = "0x" + new string('a', 64) };
            if (method != "eth_call") return method switch { "eth_chainId" => "0x31524", "eth_estimateGas" => "0x5208", "eth_gasPrice" => "0x1", "eth_getBalance" => "0x56bc75e2d63100000", _ => "0x0" };
            var data = args[0].GetProperty("data").GetString()!;
            if (data.StartsWith(Selector("allowance(address,address)"))) return Word(allowance);
            if (data.StartsWith(Selector("balanceOf(address)"))) return Word(BigInteger.Pow(10, 20));
            return "0x" + Contract[2..].PadLeft(64, '0');
        }
    };
    static async Task<MainWindow> Open(string dir, FakeRpc rpc)
    {
        Directory.CreateDirectory(dir); await Vault.CreateAsync(Path.Combine(dir, "wallet.vault"), DecimalWallet.GenerateMnemonic(), "desktop-fixture-password");
        var w = new MainWindow(dir, DecimalNetwork.Testnet() with { RpcUrl = new Uri(rpc.Url) }, autoLoad: false); w.Show();
        w.GetLogicalDescendants().OfType<TextBox>().Single().Text = "desktop-fixture-password"; Click(w, "Открыть кошелек"); await Wait(() => Labels(w).Contains("Баланс еще не загружен"));
        typeof(MainWindow).GetField("tokenData", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(w, new DataSet<TokenBalance>(new[] { new TokenBalance(Token, "Reserve", "TEST", 18, BigInteger.Pow(10, 20)) }, 16, true, Array.Empty<string>()));
        typeof(MainWindow).GetMethod("ShowStakingForm", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(w, new object[] { false }); return w;
    }
    static void SelectToken(MainWindow w) => w.GetLogicalDescendants().OfType<ComboBox>().Single(c => c.Items.Cast<object>().Any(v => v.ToString()?.StartsWith("TEST ·") == true)).SelectedIndex = 1;
    static TextBox Field(MainWindow w, string label) => w.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Text == label).Parent!.GetLogicalDescendants().OfType<TextBox>().Single();
    static IEnumerable<string?> Labels(MainWindow w) => w.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text);
    static void Click(MainWindow w, string label) => w.GetLogicalDescendants().OfType<Button>().Single(b => b.Content?.ToString() == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    static async Task Wait(Func<bool> ready) { for (var i = 0; i < 400 && !ready(); i++) await Task.Delay(15); Assert.True(ready()); }
    static string Selector(string signature) => "0x" + Nethereum.Util.Sha3Keccack.Current.CalculateHash(signature)[..8];
    static string Word(BigInteger n) => "0x" + n.ToString("x").PadLeft(64, '0');
}
