namespace Decimal.Web3.Transactions;

public enum DecimalTransactionError
{
    InsufficientDel,
    InsufficientFee,
    InsufficientToken,
    ApprovalRequired,
    FeeBelowMinimum,
    NonceChanged,
    ContractRejected,
    NetworkUnavailable,
    Unknown
}

/// <summary>A safe message for transaction failures; technical RPC details remain in InnerException.</summary>
public sealed class DecimalTransactionException : Exception
{
    public DecimalTransactionError Code { get; }
    public string? TransactionHash { get; }

    public string UserMessageRu => Code switch
    {
        DecimalTransactionError.InsufficientDel => "Недостаточно DEL на балансе.",
        DecimalTransactionError.InsufficientFee => "Недостаточно DEL для комиссии.",
        DecimalTransactionError.InsufficientToken => "Недостаточно токенов на балансе.",
        DecimalTransactionError.ApprovalRequired => "Нужно разрешение на списание токена.",
        DecimalTransactionError.FeeBelowMinimum => "Комиссия ниже минимума сети. Обновите расчет.",
        DecimalTransactionError.NonceChanged => "Счетчик транзакций изменился. Подпишите транзакцию заново.",
        DecimalTransactionError.ContractRejected => "Контракт отклонил транзакцию. Проверьте параметры.",
        DecimalTransactionError.NetworkUnavailable => "Не удалось получить данные сети. Попробуйте позже.",
        _ => "Не удалось выполнить транзакцию. Попробуйте позже."
    };

    public DecimalTransactionException(DecimalTransactionError code, Exception? innerException = null,
        string? transactionHash = null)
        : base(EnglishMessage(code), innerException)
    {
        Code = code;
        TransactionHash = transactionHash;
    }

    public static DecimalTransactionException FromRpcError(Exception error, string? transactionHash = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        var message = error.ToString().ToLowerInvariant();
        var code = message switch
        {
            _ when message.Contains("minimum global fee") || message.Contains("replacement transaction underpriced") =>
                DecimalTransactionError.FeeBelowMinimum,
            _ when message.Contains("nonce too low") || message.Contains("nonce has already been used") =>
                DecimalTransactionError.NonceChanged,
            _ when message.Contains("insufficient erc20") || message.Contains("insufficient token") ||
                   message.Contains("transfer amount exceeds balance") => DecimalTransactionError.InsufficientToken,
            _ when message.Contains("allowance") || message.Contains("not approved") =>
                DecimalTransactionError.ApprovalRequired,
            _ when message.Contains("insufficient funds") &&
                   (message.Contains("gas") || message.Contains("fee")) => DecimalTransactionError.InsufficientFee,
            _ when message.Contains("insufficient funds") || message.Contains("insufficient del") =>
                DecimalTransactionError.InsufficientDel,
            _ when message.Contains("execution reverted") || message.Contains("revert") =>
                DecimalTransactionError.ContractRejected,
            _ when error is TimeoutException or HttpRequestException or TaskCanceledException ||
                   message.Contains("connection refused") || message.Contains("rpc endpoint") =>
                DecimalTransactionError.NetworkUnavailable,
            _ => DecimalTransactionError.Unknown
        };
        return new DecimalTransactionException(code, error, transactionHash);
    }

    private static string EnglishMessage(DecimalTransactionError code) => code switch
    {
        DecimalTransactionError.InsufficientDel => "Insufficient DEL balance.",
        DecimalTransactionError.InsufficientFee => "Insufficient DEL for the network fee.",
        DecimalTransactionError.InsufficientToken => "Insufficient token balance.",
        DecimalTransactionError.ApprovalRequired => "Token spending approval is required.",
        DecimalTransactionError.FeeBelowMinimum => "Fee is below the network minimum. Refresh the quote.",
        DecimalTransactionError.NonceChanged => "Transaction nonce changed. Sign again.",
        DecimalTransactionError.ContractRejected => "Contract rejected the transaction.",
        DecimalTransactionError.NetworkUnavailable => "Network data is unavailable. Try again later.",
        _ => "Transaction could not be completed. Try again later."
    };
}
