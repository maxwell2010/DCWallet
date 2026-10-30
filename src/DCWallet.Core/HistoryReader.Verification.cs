namespace DCWallet.Core;

public sealed record HistoryVerificationAttempt(string Source, string Code);
public sealed record HistoryVerificationDetails(string Hash, IReadOnlyList<HistoryVerificationAttempt> Attempts)
{
    // Missing historical data is distinct from an invalid proof or a transport failure.
    public bool Unavailable => Attempts.Count > 0 && Attempts.All(a => a.Code is
        "transaction_and_receipt_missing" or "transaction_missing" or "receipt_missing" or "block_missing");
}
public sealed record HistoryVerificationResult(HistoryRow? Row, HistoryVerificationDetails Details);

public sealed partial class HistoryReader
{
    public async Task<HistoryVerificationResult> VerifyDetailedAsync(string hash, string owner,
        CancellationToken ct = default, bool allowIndexerLink = false)
    {
        var attempts = new List<HistoryVerificationAttempt>();
        HistoryRow? row;
        try { row = await VerifyCoreAsync(hash, owner, ct, allowIndexerLink, attempts.Add); }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        { attempts.Add(new("primary", VerificationErrorCode(e))); row = null; }
        return new(row, new(hash, attempts.ToArray()));
    }
    private static string VerificationErrorCode(Exception error) => error switch
    {
        HttpRequestException => "transport_error",
        OperationCanceledException or TimeoutException => "transport_timeout",
        InvalidDataException => "invalid_response",
        InvalidOperationException e when e.Message is "RPC вернул другую сеть." or "Архивный RPC вернул другую сеть." => "wrong_network",
        _ when error.GetType().Name == "RpcClientTimeoutException" => "transport_timeout",
        _ => "rpc_error"
    };
}
