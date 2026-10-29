using System.Numerics;
using Decimal.Web3.Transactions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;

namespace Decimal.Web3.Nft;

public sealed record CreatedNft(string Address, byte NftType, NftCollectionMeta Metadata);

public sealed record NftMovement(string From, string To, string? Operator,
    IReadOnlyList<BigInteger> TokenIds, IReadOnlyList<BigInteger> Amounts);

public sealed record NftUriUpdate(BigInteger TokenId, string Uri);

[Event("NFTCreated")]
public sealed class NftCreatedEventDto : IEventDTO
{
    [Parameter("address", "tokenAddress", 1, false)]
    public string TokenAddress { get; set; } = string.Empty;

    [Parameter("uint8", "nftType", 2, true)]
    public byte NftType { get; set; }

    [Parameter("tuple", "nft", 3, false)]
    public NftCollectionMeta Metadata { get; set; } = new();
}

[Event("Transfer")]
public sealed class NftTransferEventDto : IEventDTO
{
    [Parameter("address", "from", 1, true)] public string From { get; set; } = string.Empty;
    [Parameter("address", "to", 2, true)] public string To { get; set; } = string.Empty;
    [Parameter("uint256", "tokenId", 3, true)] public BigInteger TokenId { get; set; }
}

[Event("TransferSingle")]
public sealed class NftTransferSingleEventDto : IEventDTO
{
    [Parameter("address", "operator", 1, true)] public string Operator { get; set; } = string.Empty;
    [Parameter("address", "from", 2, true)] public string From { get; set; } = string.Empty;
    [Parameter("address", "to", 3, true)] public string To { get; set; } = string.Empty;
    [Parameter("uint256", "id", 4, false)] public BigInteger TokenId { get; set; }
    [Parameter("uint256", "value", 5, false)] public BigInteger Amount { get; set; }
}

[Event("TransferBatch")]
public sealed class NftTransferBatchEventDto : IEventDTO
{
    [Parameter("address", "operator", 1, true)] public string Operator { get; set; } = string.Empty;
    [Parameter("address", "from", 2, true)] public string From { get; set; } = string.Empty;
    [Parameter("address", "to", 3, true)] public string To { get; set; } = string.Empty;
    [Parameter("uint256[]", "ids", 4, false)] public List<BigInteger> TokenIds { get; set; } = [];
    [Parameter("uint256[]", "values", 5, false)] public List<BigInteger> Amounts { get; set; } = [];
}

[Event("URI")]
public sealed class NftUriEventDto : IEventDTO
{
    [Parameter("string", "value", 1, false)] public string Uri { get; set; } = string.Empty;
    [Parameter("uint256", "id", 2, true)] public BigInteger TokenId { get; set; }
}

/// <summary>Offline decoders for events in the pinned Decimal NFT ABIs.</summary>
public static class NftReceiptReader
{
    public static async Task<IReadOnlyList<CreatedNft>> ReadCreatedAsync(
        Nethereum.Web3.Web3 web3, string nftCenter, string transactionHash) =>
        DecodeCreated(await ReadReceiptAsync(web3, nftCenter, transactionHash).ConfigureAwait(false), nftCenter);

    public static async Task<IReadOnlyList<NftMovement>> ReadMovementsAsync(
        Nethereum.Web3.Web3 web3, string collection, string transactionHash) =>
        DecodeMovements(await ReadReceiptAsync(web3, collection, transactionHash).ConfigureAwait(false), collection);

    public static async Task<IReadOnlyList<NftUriUpdate>> ReadUriUpdatesAsync(
        Nethereum.Web3.Web3 web3, string collection, string transactionHash) =>
        DecodeUriUpdates(await ReadReceiptAsync(web3, collection, transactionHash).ConfigureAwait(false), collection);

    public static IReadOnlyList<CreatedNft> DecodeCreated(TransactionReceipt receipt, string nftCenter)
    {
        ValidateReceipt(receipt, nftCenter);
        var result = new List<CreatedNft>();
        foreach (var decoded in receipt.DecodeAllEvents<NftCreatedEventDto>())
        {
            if (!Matches(decoded.Log.Address, nftCenter)) continue;
            TransactionDraft.ValidateAddress(decoded.Event.TokenAddress, nameof(decoded.Event.TokenAddress));
            result.Add(new CreatedNft(decoded.Event.TokenAddress, decoded.Event.NftType,
                decoded.Event.Metadata));
        }
        return result;
    }

    public static IReadOnlyList<NftMovement> DecodeMovements(TransactionReceipt receipt, string collection)
    {
        ValidateReceipt(receipt, collection);
        var result = new List<(BigInteger Index, NftMovement Movement)>();
        foreach (var decoded in receipt.DecodeAllEvents<NftTransferEventDto>())
        {
            if (!Matches(decoded.Log.Address, collection)) continue;
            var value = decoded.Event;
            result.Add((decoded.Log.LogIndex.Value,
                new NftMovement(value.From, value.To, null, [value.TokenId], [BigInteger.One])));
        }
        foreach (var decoded in receipt.DecodeAllEvents<NftTransferSingleEventDto>())
        {
            if (!Matches(decoded.Log.Address, collection)) continue;
            var value = decoded.Event;
            result.Add((decoded.Log.LogIndex.Value,
                new NftMovement(value.From, value.To, value.Operator, [value.TokenId], [value.Amount])));
        }
        foreach (var decoded in receipt.DecodeAllEvents<NftTransferBatchEventDto>())
        {
            if (!Matches(decoded.Log.Address, collection)) continue;
            var value = decoded.Event;
            if (value.TokenIds.Count != value.Amounts.Count)
                throw new InvalidOperationException("TransferBatch has mismatched IDs and amounts");
            result.Add((decoded.Log.LogIndex.Value,
                new NftMovement(value.From, value.To, value.Operator, value.TokenIds, value.Amounts)));
        }
        return result.OrderBy(item => item.Index).Select(item => item.Movement).ToArray();
    }

    public static IReadOnlyList<NftUriUpdate> DecodeUriUpdates(TransactionReceipt receipt, string collection)
    {
        ValidateReceipt(receipt, collection);
        return receipt.DecodeAllEvents<NftUriEventDto>()
            .Where(decoded => Matches(decoded.Log.Address, collection))
            .OrderBy(decoded => decoded.Log.LogIndex.Value)
            .Select(decoded => new NftUriUpdate(decoded.Event.TokenId, decoded.Event.Uri))
            .ToArray();
    }

    private static void ValidateReceipt(TransactionReceipt receipt, string emitter)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        TransactionDraft.ValidateAddress(emitter, nameof(emitter));
        if (receipt.Status?.Value == BigInteger.Zero)
            throw new InvalidOperationException("Transaction reverted");
    }

    private static async Task<TransactionReceipt> ReadReceiptAsync(
        Nethereum.Web3.Web3 web3, string emitter, string transactionHash)
    {
        ArgumentNullException.ThrowIfNull(web3);
        TransactionDraft.ValidateAddress(emitter, nameof(emitter));
        if (transactionHash is null || transactionHash.Length != 66 ||
            !transactionHash.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            !transactionHash.AsSpan(2).ToString().All(Uri.IsHexDigit))
            throw new ArgumentException("Expected a 32-byte transaction hash", nameof(transactionHash));
        return await web3.Eth.Transactions.GetTransactionReceipt
            .SendRequestAsync(transactionHash).ConfigureAwait(false) ??
            throw new InvalidOperationException("Transaction receipt is not available yet");
    }

    private static bool Matches(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
}
