using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Xunit;

namespace DCWallet.UiTests;

public sealed class UpdatePromptTests
{
    [AvaloniaFact]
    public async Task UpdateDialogShowsVersionsAndOpensOnlySelectedDownloadInAllThemes()
    {
        foreach (var theme in new[] { "light", "dark", "neon" })
        {
            var directory = Path.Combine(Path.GetTempPath(), "dc-update-" + Guid.NewGuid());
            Directory.CreateDirectory(directory);
            MainWindow? owner = null; UpdatePromptWindow? prompt = null;
            try
            {
                await File.WriteAllTextAsync(Path.Combine(directory, "preferences.json"), "{\"Theme\":\"" + theme + "\"}");
                owner = new MainWindow(directory, autoLoad: false); owner.Show();
                var download = new Uri("https://github.com/maxwell2010/DCWallet/releases/download/alpha/DCWallet-win-x64.exe");
                Uri? opened = null;
                prompt = new UpdatePromptWindow("0.2.0-alpha.2", "0.2.0-alpha.3", download, uri => opened = uri)
                { RequestedThemeVariant = owner.ActualThemeVariant };
                var modal = prompt.ShowDialog(owner);
                var labels = prompt.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToArray();
                Assert.Contains("Установлена: 0.2.0-alpha.2", labels);
                Assert.Contains("Новая версия: 0.2.0-alpha.3", labels);
                Assert.Contains(labels, text => text?.Contains("откроет скачивание в браузере") == true);
                Assert.Null(opened);
                Assert.NotEqual(((ISolidColorBrush)prompt.Background!).Color, ((ISolidColorBrush)prompt.Foreground!).Color);
                var button = prompt.GetLogicalDescendants().OfType<Button>().Single(b => b.Content?.ToString() == "Обновить");
                Assert.NotEqual(((ISolidColorBrush)button.Background!).Color, ((ISolidColorBrush)button.Foreground!).Color);
                var output = Environment.GetEnvironmentVariable("DC_QA_DIR") ?? Path.Combine(AppContext.BaseDirectory, "screenshots");
                Directory.CreateDirectory(output);
                await UiFrameCapture.SaveAsync(prompt, Path.Combine(output, theme + "-update-prompt.png"));
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await modal; Assert.Equal(download, opened);
            }
            finally { prompt?.Close(); owner?.Close(); Directory.Delete(directory, true); }
        }
    }

    [AvaloniaFact]
    public async Task LaterDismissesWithoutOpeningDownloadAndLaunchFailureCanBeDismissed()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dc-update-dismiss-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var owner = new MainWindow(directory, autoLoad: false); owner.Show();
        var opened = 0;
        var prompt = new UpdatePromptWindow("0.1.0", "0.2.0", new Uri("https://github.com/maxwell2010/DCWallet/releases/tag/alpha"), _ => { opened++; throw new InvalidOperationException("No browser"); });
        try
        {
            var modal = prompt.ShowDialog(owner);
            Click(prompt, "Позже"); await modal; Assert.Equal(0, opened);
            prompt = new UpdatePromptWindow("0.1.0", "0.2.0", new Uri("https://github.com/maxwell2010/DCWallet/releases/tag/alpha"), _ => { opened++; throw new InvalidOperationException("No browser"); });
            modal = prompt.ShowDialog(owner);
            Click(prompt, "Обновить");
            Assert.False(modal.IsCompleted); Assert.Equal(1, opened);
            Assert.Contains(prompt.GetLogicalDescendants().OfType<TextBlock>(), t => t.IsVisible && t.Text?.StartsWith("Не удалось открыть браузер") == true);
            Click(prompt, "Позже"); await modal;
        }
        finally { prompt.Close(); owner.Close(); Directory.Delete(directory, true); }
    }
    static void Click(Window window, string text) => window.GetLogicalDescendants().OfType<Button>().Single(b => b.Content?.ToString() == text).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
