using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Xunit;

namespace DCWallet.UiTests;

public sealed class UpdateConsentTests
{
    static void Click(Window window, string text) => window.GetLogicalDescendants().OfType<Button>().Single(b => b.Content?.ToString() == text).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    [AvaloniaFact]
    public async Task ConsentOffersLocalDisclosureAndExplicitChoiceInAllThemes()
    {
        foreach (var theme in new[] { "light", "dark", "neon" })
        {
            var dir = Path.Combine(Path.GetTempPath(), "dc-consent-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(Path.Combine(dir, "preferences.json"), JsonSerializer.Serialize(new { Theme = theme }));
            var owner = new MainWindow(dir, autoLoad: false); owner.Show();
            try
            {
                foreach (var enabled in new[] { false, true })
                {
                    var dialog = new UpdateConsentWindow { RequestedThemeVariant = owner.ActualThemeVariant };
                    var result = dialog.ShowDialog<bool?>(owner);
                    Assert.False(result.IsCompleted);
                    Assert.Contains(dialog.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("IP-адрес") == true);
                    Assert.Contains(dialog.GetLogicalDescendants().OfType<Button>(), b => b.Content?.ToString() == "Политика конфиденциальности на GitHub");
                    var allow = dialog.GetLogicalDescendants().OfType<Button>().Single(b => b.Content?.ToString() == "Проверять");
                    dialog.UpdateLayout();
                    var position = allow.TranslatePoint(new Point(allow.Bounds.Width / 2, allow.Bounds.Height / 2), dialog)!.Value;
                    dialog.MouseMove(position); await Task.Delay(30);
                    var output = Environment.GetEnvironmentVariable("DC_QA_DIR") ?? Path.Combine(AppContext.BaseDirectory, "screenshots");
                    Directory.CreateDirectory(output);
                    await UiFrameCapture.SaveAsync(dialog, Path.Combine(output, theme + "-update-consent-hover.png"));
                    Click(dialog, enabled ? "Проверять" : "Не проверять");
                    Assert.Equal(enabled, await result);
                }
            }
            finally { owner.Close(); Directory.Delete(dir, true); }
        }
    }
    [AvaloniaFact]
    public async Task WindowsDeclinePersistsAndSettingsCanEnableWithoutAnotherPrompt()
    {
        if (!OperatingSystem.IsWindows()) return;
        var dir = Path.Combine(Path.GetTempPath(), "dc-consent-save-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
        var owner = new MainWindow(dir, autoLoad: false); owner.Show();
        var ensure = typeof(MainWindow).GetMethod("EnsureWindowsUpdateConsentAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Task<bool> Check() => (Task<bool>)ensure.Invoke(owner, null)!;
        try
        {
            var store = (bool)typeof(App).Assembly.GetType("DCWallet.DistributionPolicy")!.GetProperty("UsesStoreUpdates")!.GetValue(null)!;
            if (store)
            {
                Assert.False(await Check()); Assert.Empty(owner.OwnedWindows);
                Assert.False(File.Exists(Path.Combine(dir, "preferences.json")));
                typeof(MainWindow).GetMethod("ShowSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, null);
                Assert.DoesNotContain(owner.GetLogicalDescendants().OfType<CheckBox>(), c => c.Name == "WindowsUpdateChecks");
                Assert.Contains(owner.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Обновления устанавливаются через Microsoft Store") == true);
                return;
            }
            var first = Check(); Assert.False(first.IsCompleted);
            Click(owner.OwnedWindows.OfType<UpdateConsentWindow>().Single(), "Не проверять");
            Assert.False(await first);
            Assert.False(await Check()); Assert.Empty(owner.OwnedWindows);
            using (var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(dir, "preferences.json")))) Assert.False(json.RootElement.GetProperty("CheckForUpdatesWindows").GetBoolean());
            typeof(MainWindow).GetMethod("ShowSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, null);
            owner.GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Name == "WindowsUpdateChecks").IsChecked = true;
            Assert.True(await Check()); Assert.Empty(owner.OwnedWindows);
        }
        finally { owner.Close(); Directory.Delete(dir, true); }
    }
}
