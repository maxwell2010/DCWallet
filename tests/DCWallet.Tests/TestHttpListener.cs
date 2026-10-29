using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DCWallet.Tests;

// Loopback-only JSON fixture transport. Avoid HttpListener's Unix shutdown callback
// race when clients leave an idle keep-alive connection during fixture disposal.
internal sealed class TestHttpListener
{
    readonly CancellationTokenSource stopped = new();
    readonly HashSet<TcpClient> clients = [];
    TcpListener? listener;
    public List<string> Prefixes { get; } = [];
    public bool IsListening => listener is not null && !stopped.IsCancellationRequested;
    public void Start()
    {
        var uri = new Uri(Prefixes.Single());
        if (!uri.IsLoopback) throw new InvalidOperationException("Fixture must bind loopback");
        listener = new TcpListener(IPAddress.Loopback, uri.Port);
        listener.Start();
    }
    public async Task<TestHttpContext> GetContextAsync()
    {
        var client = await listener!.AcceptTcpClientAsync(stopped.Token);
        lock (clients) clients.Add(client);
        try
        {
            var stream = client.GetStream();
            var headers = new List<byte>();
            var one = new byte[1];
            while (headers.Count < 16384)
            {
                await stream.ReadExactlyAsync(one, stopped.Token);
                headers.Add(one[0]);
                var n = headers.Count;
                if (n >= 4 && headers[n-4] == 13 && headers[n-3] == 10 && headers[n-2] == 13 && headers[n-1] == 10) break;
            }
            var lines = Encoding.ASCII.GetString(headers.ToArray()).Split("\r\n");
            var lengthHeader = lines.Single(l => l.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
            var length = int.Parse(lengthHeader[(lengthHeader.IndexOf(':') + 1)..], System.Globalization.CultureInfo.InvariantCulture);
            if (length < 0 || length > 1048576) throw new InvalidDataException("Fixture request too large");
            var body = new byte[length];
            await stream.ReadExactlyAsync(body, stopped.Token);
            return new TestHttpContext(new MemoryStream(body), new TestHttpResponse(client, () => { lock (clients) clients.Remove(client); }));
        }
        catch { Release(client); throw; }
    }
    void Release(TcpClient client) { lock (clients) clients.Remove(client); client.Dispose(); }
    public void Stop()
    {
        stopped.Cancel();
        listener?.Stop();
        lock (clients) { foreach (var client in clients) client.Dispose(); clients.Clear(); }
    }
    public void Close() => Stop();
}
internal sealed class TestHttpContext(Stream body, TestHttpResponse response)
{
    public TestHttpRequest Request { get; } = new(body);
    public TestHttpResponse Response { get; } = response;
}
internal sealed class TestHttpRequest(Stream body)
{
    public Stream InputStream { get; } = body;
}
internal sealed class TestHttpResponse(TcpClient client, Action release)
{
    public string ContentType { get; set; } = "application/json";
    public MemoryStream OutputStream { get; } = new();
    public void Close()
    {
        try
        {
            var body = OutputStream.ToArray();
            var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: {ContentType}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
            var stream = client.GetStream();
            stream.Write(header);
            stream.Write(body);
        }
        finally { client.Dispose(); OutputStream.Dispose(); release(); }
    }
}
