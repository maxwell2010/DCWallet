using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
namespace DCWallet.Tests;
internal sealed class FakeRpc : IDisposable
{
    private readonly HttpListener listener = new();
    private readonly Task loop;
    public string Url { get; }
    public string Chain { get; init; } = "0x4b";
    public int Broadcasts;
    public bool AcceptBroadcast { get; init; }
    public Func<string, JsonElement, object?>? ReadResult { get; init; }
    public FakeRpc()
    {
        var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start(); var port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
        Url = $"http://127.0.0.1:{port}/"; listener.Prefixes.Add(Url); listener.Start();
        loop = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await listener.GetContextAsync(); } catch { break; }
                using var doc = JsonDocument.Parse(await new StreamReader(context.Request.InputStream).ReadToEndAsync());
                var root = doc.RootElement; var method = root.GetProperty("method").GetString();
                object? result = ReadResult is null ? method switch { "eth_chainId" => Chain, "eth_estimateGas" => "0x5208", "eth_gasPrice" => "0x1", "eth_getBalance" => "0xde0b6b3a7640000", "eth_getTransactionCount" => "0x0", _ => "0x0" } : ReadResult(method!, root.GetProperty("params"));
                object response;
                if (method == "eth_sendRawTransaction")
                {
                    Interlocked.Increment(ref Broadcasts);
                    if (AcceptBroadcast)
                    {
                        var raw = root.GetProperty("params")[0].GetString()!;
                        response = new { jsonrpc = "2.0", id = root.GetProperty("id").Clone(), result = new Decimal.Web3.Transactions.SignedTransaction(raw, "", "", 0, 0, 0).Hash };
                    }
                    else response = new { jsonrpc = "2.0", id = root.GetProperty("id").Clone(), error = new { code = -32000, message = "fixture: uncertain transport result" } };
                }
                else response = new { jsonrpc = "2.0", id = root.GetProperty("id").Clone(), result };
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response)); context.Response.ContentType = "application/json"; await context.Response.OutputStream.WriteAsync(bytes); context.Response.Close();
            }
        });
    }
    public void Dispose() { listener.Stop(); listener.Close(); }
}
