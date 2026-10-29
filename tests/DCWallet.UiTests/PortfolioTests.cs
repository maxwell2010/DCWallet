using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using DCWallet.Core;
using DCWallet.Tests;
using Decimal.Web3;
using Xunit;

namespace DCWallet.UiTests;
public sealed class PortfolioTests
{
    [AvaloniaFact]
    public async Task AssetsLoadAutomaticallyAndRefreshPreservesTokenFormInAllThemes()
    {
        foreach (var theme in new[] { "light", "dark", "neon" })
        {
            using var rpc = new DiscoveryRpc { NftOwner = DiscoveryFixture.Owner };
            var directory = Path.Combine(Path.GetTempPath(), "dc-portfolio-" + Guid.NewGuid()); Directory.CreateDirectory(directory);
            MainWindow? window = null;
            try
            {
                await File.WriteAllTextAsync(Path.Combine(directory, "preferences.json"), "{\"Theme\":\"" + theme + "\"}");
                window = new MainWindow(directory, DecimalNetwork.Mainnet() with { RpcUrl = new Uri(rpc.Url) },
                    dataReaderFactory: client => new WalletDataReader(client, handler: new DiscoveryCatalogHandler()));
                window.Show(); Click(window, "Только просмотр"); Fields(window).First().Text = DiscoveryFixture.Owner; Click(window, "Открыть для просмотра");
                await Wait(window, "TEST   1.2346");
                Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(), t => ToolTip.GetTip(t)?.ToString() == "Точно: 1.234567 TEST");
                Assert.Contains(Labels(window), t => t == "Баланс подтвержден в выбранной сети");
                Click(window, "Токены"); var contract = Fields(window).Single(); contract.Text = DiscoveryFixture.Token;
                Click(window, "Обновить все активы"); await Task.Delay(200);
                Assert.Same(contract, Fields(window).Single()); Assert.Equal(DiscoveryFixture.Token, contract.Text);
                Click(window, "NFT"); await Wait(window, "NFT · #7");
                var output = Environment.GetEnvironmentVariable("DC_QA_DIR") ?? Path.Combine(AppContext.BaseDirectory, "screenshots"); Directory.CreateDirectory(output);
                using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame); frame.Save(Path.Combine(output, theme + "-populated-nfts.png"));
                Assert.Equal(0, rpc.Broadcasts); Click(window, "Заблокировать");
                Assert.DoesNotContain(Labels(window), t => t?.Contains("1.234567") == true || t?.Contains("NFT · #7") == true);
            }
            finally { window?.Close(); Directory.Delete(directory, true); }
        }
    }
    static IEnumerable<TextBox> Fields(Window window) => window.GetLogicalDescendants().OfType<TextBox>();
    static IEnumerable<string?> Labels(Window window) => window.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text);
    static void Click(Window window, string label) => window.GetLogicalDescendants().OfType<Button>().First(b => b.Content?.ToString() == label || b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == label)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    static async Task Wait(Window window, string value)
    {
        for (var attempt = 0; attempt < 200 && !Labels(window).Contains(value); attempt++) await Task.Delay(25);
        Assert.Contains(value, Labels(window));
    }
}
