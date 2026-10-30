using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using DCWallet.Core;
using Decimal.Web3.Wallet;
using Decimal.Web3;
using Xunit;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Input;
using System.Reflection;
using Avalonia.VisualTree;
using Avalonia.Controls.Presenters;

[assembly: AvaloniaTestApplication(typeof(DCWallet.UiTests.TestApp))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace DCWallet.UiTests;
public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
public sealed class WindowTests
{
    [AvaloniaFact] public async Task PrimaryButtonHoverPressedAndDisabledKeepNeonBackground()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dc-hover-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "preferences.json"), "{\"Theme\":\"neon\"}");
            var window = new MainWindow(dir, autoLoad: false); window.Show();
            var button = window.GetLogicalDescendants().OfType<Button>().First(b => b.Classes.Contains("primary"));
            Save(window, "neon-primary-normal.png"); var p = button.TranslatePoint(new Point(20, 20), window)!.Value;
            window.MouseMove(p); await Task.Delay(30);
            var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().First(c => c.Name == "PART_ContentPresenter");
            Assert.Equal(Color.Parse("#52DCF4"), ((ISolidColorBrush)presenter.Background!).Color); Save(window, "neon-primary-hover.png");
            window.MouseDown(p, MouseButton.Left); await Task.Delay(30);
            Assert.Equal(Color.Parse("#0CB4D5"), ((ISolidColorBrush)presenter.Background!).Color); Save(window, "neon-primary-pressed.png");
            window.MouseMove(new Point(1, 1)); window.MouseUp(new Point(1, 1), MouseButton.Left); button.IsEnabled = false; await Task.Delay(30);
            Assert.Equal(Color.Parse("#19CCEF"), ((ISolidColorBrush)presenter.Background!).Color); Save(window, "neon-primary-disabled.png"); window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }
    [AvaloniaFact] public async Task CryptopunkThemePersistsAndPrimaryActionKeepsContrastingText()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dc-neon-" + Guid.NewGuid());
        try
        {
            var window = new MainWindow(dir, autoLoad: false); window.Show(); Click(window, "Настройки подключения"); Click(window, "Криптопанк");
            Assert.Equal(ThemeVariant.Dark, window.ActualThemeVariant); CheckContrast(window);
            Click(window, "Вернуться ко входу");
            var primary = window.GetLogicalDescendants().OfType<Button>().First(b => b.Classes.Contains("primary"));
            Assert.Equal(Color.Parse("#19CCEF"), ((ISolidColorBrush)primary.Background!).Color);
            Assert.Equal(Color.Parse("#06101C"), ((ISolidColorBrush)primary.Foreground!).Color);
            window.Close();
            var reopened = new MainWindow(dir, autoLoad: false); reopened.Show();
            Assert.Equal(Color.Parse("#070711"), ((ISolidColorBrush)reopened.Background!).Color); Assert.Equal(ThemeVariant.Dark, reopened.ActualThemeVariant);
            Click(reopened, "Настройки подключения");
            var notify = typeof(MainWindow).GetMethod("OnSystemColorsChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
            notify.Invoke(reopened, new object?[] { null, new PlatformColorValues { ThemeVariant = PlatformThemeVariant.Light } }); await Task.Delay(20);
            Assert.Equal(ThemeVariant.Dark, reopened.ActualThemeVariant); CheckContrast(reopened); Save(reopened, "neon-persisted-settings.png"); reopened.Close();
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
    [AvaloniaFact] public async Task SystemThemeKeepsControlsReadableAndSettingsReturnStaysVisibleWhenScrolled()
    {
        foreach (var previous in new[] { "light", "dark", "neon" })
        {
            var dir = Path.Combine(Path.GetTempPath(), "dc-system-theme-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
            try
            {
                await File.WriteAllTextAsync(Path.Combine(dir, "preferences.json"), "{\"Theme\":\"" + previous + "\"}");
                var window = new MainWindow(dir, autoLoad: false); window.Show(); Click(window, "Настройки подключения"); Click(window, "Как в системе");
                var osDark = window.PlatformSettings!.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark;
                Assert.Equal(osDark ? ThemeVariant.Dark : ThemeVariant.Light, window.ActualThemeVariant);
                CheckContrast(window);
                // Emulate the native notification for each OS color preference.
                var notify = typeof(MainWindow).GetMethod("OnSystemColorsChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
                foreach (var mode in new[] { PlatformThemeVariant.Dark, PlatformThemeVariant.Light })
                {
                    notify.Invoke(window, new object?[] { null, new PlatformColorValues { ThemeVariant = mode } }); await Task.Delay(20);
                    Assert.Equal(mode == PlatformThemeVariant.Dark ? ThemeVariant.Dark : ThemeVariant.Light, window.ActualThemeVariant);
                    CheckContrast(window);
                    var scroll = window.GetLogicalDescendants().OfType<ScrollViewer>().Single(s => s.Name == "MainScroll"); scroll.Offset = new Vector(0, scroll.Extent.Height); await Task.Delay(20);
                    var back = window.GetLogicalDescendants().OfType<Button>().Single(b => b.Content?.ToString() == "Вернуться ко входу");
                    Assert.True(back.IsEffectivelyVisible); var point = back.TranslatePoint(default, window)!.Value; Assert.InRange(point.Y, 0, window.Bounds.Height - back.Bounds.Height);
                    Save(window, "system-" + previous + "-to-" + mode + ".png");
                }
                Click(window, "Вернуться ко входу");
                Assert.Contains(window.GetLogicalDescendants().OfType<Button>(), b => b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Создать кошелек"));
                Click(window, "Настройки подключения"); window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
                Assert.False(window.GetLogicalDescendants().OfType<Button>().Single(b => b.Content?.ToString() == "Вернуться ко входу").IsEffectivelyVisible);
                window.Close();
            }
            finally { Directory.Delete(dir, true); }
        }
    }
    private static void CheckContrast(Window window)
    {
        static double Luminance(Color color)
        {
            static double Linear(byte v) { var c = v / 255d; return c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4); }
            return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
        }
        var background = Luminance(((ISolidColorBrush)window.Background!).Color);
        foreach (var box in window.GetLogicalDescendants().OfType<CheckBox>())
        {
            var foreground = Luminance(((ISolidColorBrush)box.Foreground!).Color);
            Assert.True((Math.Max(background, foreground) + .05) / (Math.Min(background, foreground) + .05) >= 4.5);
        }
    }
    [AvaloniaFact] public async Task BothThemesRenderWalletScreensAndThemePreferenceSurvives()
    {
        foreach (var theme in new[] { "light", "dark", "neon" })
        {
            using var rpc = new DCWallet.Tests.FakeRpc();
            var dir = Path.Combine(Path.GetTempPath(), "dc-themes-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
            try
            {
                await File.WriteAllTextAsync(Path.Combine(dir, "preferences.json"), "{\"Theme\":\"" + theme + "\"}");
                await Vault.CreateAsync(Path.Combine(dir, "wallet.vault"), DecimalWallet.GenerateMnemonic(), "temporary-test-password");
                var window = new MainWindow(dir, DecimalNetwork.Custom("fixture", 75, rpc.Url), autoLoad: false); window.Show(); Save(window, theme + "-unlock.png");
                window.GetLogicalDescendants().OfType<TextBox>().Single().Text = "temporary-test-password"; Click(window, "Открыть кошелек"); await WaitFor(window, "Баланс еще не загружен");
                Save(window, theme + "-overview.png");
                foreach (var route in new[] { "Отправить", "Обмен", "Стейкинг", "Токены", "Счета", "Настройки", "История", "Мост" })
                {
                    Click(window, route); Save(window, theme + "-" + route + ".png");
                    Assert.True(window.GetLogicalDescendants().OfType<Button>().First(b => b.Content is StackPanel p && p.Children.OfType<TextBlock>().Any(t => t.Text == "Настройки")).Bounds.Height > 0);
                }
                Click(window, "Отправить"); Click(window, "Продолжить"); await Task.Delay(50); Save(window, theme + "-error.png");
                var sendFields = window.GetLogicalDescendants().OfType<TextBox>().ToArray(); sendFields[0].Text = "0x" + new string('2', 40); sendFields[1].Text = "0.001";
                Click(window, "Продолжить"); await WaitFor(window, "Проверка транзакции"); Save(window, theme + "-preview.png"); Assert.Equal(0, rpc.Broadcasts);
                Click(window, "Настройки"); Click(window, theme == "light" ? "Темная" : "Светлая");
                Assert.Contains(theme == "light" ? "dark" : "light", await File.ReadAllTextAsync(Path.Combine(dir, "preferences.json")));
                Save(window, theme + "-switched.png"); Click(window, "Вернуться к кошельку"); await WaitFor(window, "Баланс еще не загружен"); window.Close();
            }
            finally { Directory.Delete(dir, true); }
        }
    }
    [AvaloniaFact] public async Task OnboardingStepsRenderInBothThemesAndWatchOnlyCannotSend()
    {
        foreach (var theme in new[] { "light", "dark", "neon" })
        {
            var dir = Path.Combine(Path.GetTempPath(), "dc-onboarding-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
            try
            {
                await File.WriteAllTextAsync(Path.Combine(dir, "preferences.json"), "{\"Theme\":\"" + theme + "\"}");
                var window = new MainWindow(dir, autoLoad: false); window.Show(); Save(window, theme + "-welcome.png");
                Click(window, "Фраза восстановления"); Save(window, theme + "-restore.png"); Click(window, "← Назад");
                Click(window, "Создать кошелек"); Save(window, theme + "-phrase.png"); Click(window, "Далее"); Save(window, theme + "-verify.png"); Click(window, "Пропустить проверку");
                foreach (var password in window.GetLogicalDescendants().OfType<TextBox>()) password.Text = "temporary-test-password";
                Save(window, theme + "-password.png");
                window.Close();
                var watch = new MainWindow(dir, autoLoad: false); watch.Show(); Click(watch, "Только просмотр");
                watch.GetLogicalDescendants().OfType<TextBox>().First().Text = "0x" + new string('2', 40); Click(watch, "Открыть для просмотра"); await WaitFor(watch, "Баланс еще не загружен");
                Click(watch, "Отправить"); Assert.DoesNotContain(watch.GetLogicalDescendants().OfType<Button>(), b => b.Content?.ToString() == "Продолжить"); Save(watch, theme + "-watch.png"); watch.Close();
            }
            finally { Directory.Delete(dir, true); }
        }
    }
    [AvaloniaFact] public async Task CreationWizardWritesEncryptedWalletAndReturnsToUnlock()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dc-create-" + Guid.NewGuid());
        try
        {
            var window = new MainWindow(dir, autoLoad: false); window.Show(); Click(window, "Создать кошелек"); Click(window, "Далее"); Click(window, "Пропустить проверку");
            foreach (var password in window.GetLogicalDescendants().OfType<TextBox>()) password.Text = "temporary-test-password";
            Click(window, "Создать кошелек");
            for (var i = 0; i < 100 && !File.Exists(Path.Combine(dir, "wallet.vault")); i++) await Task.Delay(50);
            Assert.True(File.Exists(Path.Combine(dir, "wallet.vault")));
            Assert.True(DecimalWallet.IsValidMnemonic(await Vault.OpenAsync(Path.Combine(dir, "wallet.vault"), "temporary-test-password")));
            window.Close();
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
    [AvaloniaFact] public async Task PreviewDoesNotSendAndConfirmationAttemptsBroadcastExactlyOnce()
    {
        using var rpc = new DCWallet.Tests.FakeRpc();
        var dir = Path.Combine(Path.GetTempPath(), "dc-ui-" + Guid.NewGuid());
        try
        {
            await Vault.CreateAsync(Path.Combine(dir, "wallet.vault"), DecimalWallet.GenerateMnemonic(), "temporary-test-password");
            var window = new MainWindow(dir, DecimalNetwork.Custom("fixture", 75, rpc.Url), autoLoad: false); window.Show();
            window.GetLogicalDescendants().OfType<TextBox>().Single().Text = "temporary-test-password"; Click(window, "Открыть кошелек");
            await WaitFor(window, "Баланс еще не загружен"); Click(window, "Отправить");
            var fields = window.GetLogicalDescendants().OfType<TextBox>().ToArray(); fields[0].Text = "0x" + new string('2', 40); fields[1].Text = "0.001";
            Click(window, "Продолжить"); await WaitFor(window, "Проверка транзакции");
            Assert.Equal(0, rpc.Broadcasts); Save(window, "preview.png");
            Click(window, "Отправить подтвержденную транзакцию"); await Task.Delay(50); Assert.Equal(0, rpc.Broadcasts);
            window.GetLogicalDescendants().OfType<CheckBox>().Single().IsChecked = true;
            Click(window, "Отправить подтвержденную транзакцию");
            for (var i = 0; i < 60 && rpc.Broadcasts == 0; i++) await Task.Delay(50);
            Assert.Equal(1, rpc.Broadcasts); await Task.Delay(50);
            Click(window, "Отправить подтвержденную транзакцию"); await Task.Delay(50); Assert.Equal(1, rpc.Broadcasts);
            Assert.Single(Directory.GetFiles(Path.Combine(dir, "journal"))); window.Close();
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
    [AvaloniaFact] public void StartupAndSettingsRenderWithoutSeedOrNetwork()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dc-ui-" + Guid.NewGuid());
        var window = new MainWindow(dir, autoLoad: false); window.Show();
        Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "DC WALLET");
        Save(window, "welcome.png");
        Click(window, "Настройки подключения");
        Assert.Contains(window.GetLogicalDescendants().OfType<ComboBox>(), c => c.Items.Count == 10);
        Save(window, "settings.png"); window.Close();
    }
    [AvaloniaFact] public async Task UnlockNavigateAndLockClearsWalletViews()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dc-ui-" + Guid.NewGuid());
        try
        {
            await Vault.CreateAsync(Path.Combine(dir, "wallet.vault"), DecimalWallet.GenerateMnemonic(), "temporary-test-password");
            var window = new MainWindow(dir, autoLoad: false); window.Show();
            window.GetLogicalDescendants().OfType<TextBox>().Single().Text = "temporary-test-password";
            Click(window, "Открыть кошелек");
            for (var i = 0; i < 100 && !window.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Баланс еще не загружен"); i++) await Task.Delay(50);
            Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Баланс еще не загружен");
            Save(window, "assets.png"); Click(window, "Отправить"); Save(window, "send.png");
            Click(window, "Обзор"); Click(window, "Получить"); Assert.Contains(window.GetLogicalDescendants().OfType<Image>(), i => i.Width == 240); Save(window, "receive.png");
            Click(window, "Мост");
            Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Межсетевые переводы временно недоступны."); Save(window, "bridge.png");
            Click(window, "Заблокировать");
            Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Кошелек заблокирован");
            Assert.DoesNotContain(window.GetLogicalDescendants().OfType<TextBox>(), t => t.Text == "temporary-test-password"); window.Close();
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
    private static void Click(Window window, string label)
    {
        var button = window.GetLogicalDescendants().OfType<Button>().First(b => b.Content?.ToString() == label || b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == label));
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
    private static async Task WaitFor(Window window, string text)
    {
        for (var i = 0; i < 100 && !window.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == text); i++) await Task.Delay(50);
        Assert.True(window.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == text), string.Join(" | ", window.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text)));
    }
    private static void Save(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("DC_QA_DIR") ?? Path.Combine(AppContext.BaseDirectory, "screenshots"); Directory.CreateDirectory(directory);
        using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame); frame.Save(Path.Combine(directory, name));
    }
}
