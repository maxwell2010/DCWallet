using System.Net;
using System.Net.Sockets;
using Xunit;

namespace DCWallet.Tests;

public sealed class TestHttpListenerTests
{
    [Fact]
    public async Task ShutdownCancelsPartialRequestWithoutBackgroundCallbacks()
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        var listener = new TestHttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        using var client = new TcpClient();
        try
        {
            var pending = listener.GetContextAsync();
            await client.ConnectAsync(IPAddress.Loopback, port);
            await client.GetStream().WriteAsync("POST / HTTP/1.1\r\n"u8.ToArray());
            listener.Stop();
            await Assert.ThrowsAnyAsync<Exception>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(listener.IsListening);
            Assert.True(pending.IsCompleted);
        }
        finally { listener.Close(); }
    }
}
