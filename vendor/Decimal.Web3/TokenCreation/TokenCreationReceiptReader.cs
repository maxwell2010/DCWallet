using Decimal.Web3.Transactions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;

namespace Decimal.Web3.TokenCreation;

public sealed record CreatedToken(string Address, bool IsReserveless);

[Event("TokenDeployed")]
public sealed class TokenDeployedEventDto : IEventDTO
{
    [Parameter("address", "tokenAddress", 1, false)]
    public string TokenAddress { get; set; } = string.Empty;
}

[Event("TokenReservelessDeployed")]
public sealed class TokenReservelessDeployedEventDto : IEventDTO
{
    [Parameter("address", "tokenAddress", 1, false)]
    public string TokenAddress { get; set; } = string.Empty;
}

public static class TokenCreationReceiptReader
{
    public static async Task<IReadOnlyList<CreatedToken>> ReadAsync(
        Nethereum.Web3.Web3 web3, string tokenCenter, string transactionHash)
    {
        ArgumentNullException.ThrowIfNull(web3);
        TransactionDraft.ValidateAddress(tokenCenter, nameof(tokenCenter));
        if (transactionHash is null || transactionHash.Length != 66 ||
            !transactionHash.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            !transactionHash.AsSpan(2).ToString().All(Uri.IsHexDigit))
            throw new ArgumentException("Expected a 32-byte transaction hash", nameof(transactionHash));
        var receipt = await web3.Eth.Transactions.GetTransactionReceipt
            .SendRequestAsync(transactionHash).ConfigureAwait(false);
        if (receipt is null)
            throw new InvalidOperationException("Transaction receipt is not available yet");
        return DecodeReceipt(receipt, tokenCenter);
    }

    public static IReadOnlyList<CreatedToken> DecodeReceipt(TransactionReceipt receipt, string tokenCenter)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        TransactionDraft.ValidateAddress(tokenCenter, nameof(tokenCenter));
        if (receipt.Status?.Value == 0)
            throw new InvalidOperationException("Transaction reverted");
        var result = new List<CreatedToken>();
        foreach (var decoded in receipt.DecodeAllEvents<TokenDeployedEventDto>())
        {
            if (!string.Equals(decoded.Log.Address, tokenCenter, StringComparison.OrdinalIgnoreCase))
                continue;
            TransactionDraft.ValidateAddress(decoded.Event.TokenAddress, nameof(decoded.Event.TokenAddress));
            result.Add(new CreatedToken(decoded.Event.TokenAddress, false));
        }
        foreach (var decoded in receipt.DecodeAllEvents<TokenReservelessDeployedEventDto>())
        {
            if (!string.Equals(decoded.Log.Address, tokenCenter, StringComparison.OrdinalIgnoreCase))
                continue;
            TransactionDraft.ValidateAddress(decoded.Event.TokenAddress, nameof(decoded.Event.TokenAddress));
            result.Add(new CreatedToken(decoded.Event.TokenAddress, true));
        }
        return result;
    }
}
