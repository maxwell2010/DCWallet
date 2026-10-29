using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Xunit;

namespace DCWallet.UiTests;

internal static class UiFrameCapture
{
    public static async Task SaveAsync(Window window, string path)
    {
        // SizeToContent and composition can finish after ShowDialog on native ARM runners.
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(25);
            using var frame = window.CaptureRenderedFrame();
            if (frame is null) continue;
            Assert.True(frame.PixelSize.Width > 0 && frame.PixelSize.Height > 0);
            frame.Save(path);
            return;
        }
        Assert.Fail($"Visible dialog did not produce a rendered frame within 5 seconds. Bounds: {window.Bounds}");
    }
}
