using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using DCWallet.Core;
using System.Runtime.InteropServices;

namespace DCWallet;
public sealed class App : Application
{
    static int updateCheckStarted;
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
    }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;
            window.Opened += async (_, _) => await CheckForUpdatesAsync(window);
        }
        base.OnFrameworkInitializationCompleted();
    }
    static async Task CheckForUpdatesAsync(MainWindow owner)
    {
        if (Environment.GetEnvironmentVariable("DC_HEADLESS") == "1" || Interlocked.Exchange(ref updateCheckStarted, 1) != 0) return;
        try
        {
            if (DistributionPolicy.UsesStoreUpdates || !await owner.EnsureWindowsUpdateConsentAsync() || !owner.IsVisible) return;
        }
        catch (Exception) { return; /* Consent or preferences failure must not interrupt startup. */ }
        using var closed = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        void Cancel(object? sender, EventArgs args) => closed.Cancel();
        owner.Closed += Cancel;
        try
        {
            var update = await new ReleaseUpdateClient().CheckAsync(AppInfo.Version, closed.Token);
            if (update is null || closed.IsCancellationRequested || !owner.IsVisible) return;
            var platform = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
            var runtime = platform + "-" + RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
            var dialog = new UpdatePromptWindow(update.CurrentVersion, update.NewVersion, update.GetDownloadUri(runtime) ?? update.ReleaseUri)
            { RequestedThemeVariant = owner.ActualThemeVariant };
            await dialog.ShowDialog(owner);
        }
        catch (Exception) { /* An unavailable update service must not interrupt wallet startup. */ }
        finally { owner.Closed -= Cancel; }
    }
}
