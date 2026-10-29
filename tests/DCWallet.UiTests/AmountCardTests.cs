using System.Reflection;
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
public sealed class AmountCardTests
{
    [AvaloniaFact]
    public async Task RoundedCardsHaveExactTooltipsAndDelValuationAcrossThemes()
    {
        foreach (var theme in new[] { "light", "dark", "neon" })
        {
            var directory = Path.Combine(Path.GetTempPath(), "dc-value-ui-" + Guid.NewGuid()); Directory.CreateDirectory(directory); MainWindow? window = null;
            try
            {
                await File.WriteAllTextAsync(Path.Combine(directory, "preferences.json"), "{\"Theme\":\"" + theme + "\"}");
                window = new MainWindow(directory, autoLoad: false); window.Show(); Click(window, "Только просмотр");
                window.GetLogicalDescendants().OfType<TextBox>().First().Text = DiscoveryFixture.Owner; Click(window, "Открыть для просмотра");
                Set(window, "nativeBalance", Units.Parse("13.876309661736049615"));
                Set(window, "tokenData", new DataSet<TokenBalance>([new(DiscoveryFixture.Token, "Резервный токен", "TEST", 6, 1234567)], 100, true, []));
                var values = (Dictionary<string, TokenDelEstimate?>)typeof(MainWindow).GetField("tokenValues", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
                values[DiscoveryFixture.Token] = new(100, Units.Parse("2.34567890123456789"));
                typeof(MainWindow).GetMethod("ShowAssets", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
                var texts = window.GetLogicalDescendants().OfType<TextBlock>().ToArray();
                Assert.Contains(texts, t => t.Text == "13.8763" && ToolTip.GetTip(t)?.ToString() == "Точно: 13.876309661736049615 DEL");
                Assert.Contains(texts, t => t.Text == "TEST   1.2346" && ToolTip.GetTip(t)?.ToString() == "Точно: 1.234567 TEST");
                Assert.Contains(texts, t => t.Text == "≈ 2.3457 DEL" && ToolTip.GetTip(t)?.ToString()?.Contains("2.34567890123456789 DEL") == true);
                var root = Environment.GetEnvironmentVariable("DC_QA_DIR") ?? Path.Combine(AppContext.BaseDirectory, "screenshots"); Directory.CreateDirectory(root);
                using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame); frame.Save(Path.Combine(root, theme + "-rounded-value-cards.png"));
            }
            finally { window?.Close(); Directory.Delete(directory, true); }
        }
    }
    static void Set(MainWindow window, string field, object value) => typeof(MainWindow).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, value);
    static void Click(MainWindow window, string label) => window.GetLogicalDescendants().OfType<Button>().First(b => b.Content?.ToString() == label || b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == label)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
