using System.Numerics;
using Decimal.Web3.Transactions;
using Nethereum.Contracts;

namespace Decimal.Web3.Nft;

/// <summary>Standard ERC1155 reads and unsigned transfer drafts.</summary>
public sealed class Erc1155Service
{
    private const string Abi = """
    [
      {"constant":true,"inputs":[{"name":"account","type":"address"},{"name":"id","type":"uint256"}],"name":"balanceOf","outputs":[{"name":"","type":"uint256"}],"type":"function"},
      {"constant":true,"inputs":[{"name":"accounts","type":"address[]"},{"name":"ids","type":"uint256[]"}],"name":"balanceOfBatch","outputs":[{"name":"","type":"uint256[]"}],"type":"function"},
      {"constant":true,"inputs":[{"name":"id","type":"uint256"}],"name":"uri","outputs":[{"name":"","type":"string"}],"type":"function"},
      {"constant":true,"inputs":[],"name":"name","outputs":[{"name":"","type":"string"}],"type":"function"},
      {"constant":true,"inputs":[],"name":"symbol","outputs":[{"name":"","type":"string"}],"type":"function"},
      {"constant":true,"inputs":[],"name":"contractURI","outputs":[{"name":"","type":"string"}],"type":"function"},
      {"constant":true,"inputs":[],"name":"creator","outputs":[{"name":"","type":"address"}],"type":"function"},
      {"constant":true,"inputs":[],"name":"getAllowMint","outputs":[{"name":"","type":"bool"}],"type":"function"},
      {"constant":true,"inputs":[],"name":"getRefundable","outputs":[{"name":"","type":"bool"}],"type":"function"},
      {"constant":true,"inputs":[],"name":"getNftType","outputs":[{"name":"","type":"uint8"}],"type":"function"},
      {"constant":true,"inputs":[{"name":"id","type":"uint256"}],"name":"rate","outputs":[{"name":"","type":"uint256"}],"type":"function"},
      {"constant":true,"inputs":[{"name":"id","type":"uint256"}],"name":"exists","outputs":[{"name":"","type":"bool"}],"type":"function"},
      {"constant":true,"inputs":[{"name":"id","type":"uint256"}],"name":"totalSupply","outputs":[{"name":"","type":"uint256"}],"type":"function"},
      {"constant":true,"inputs":[{"name":"account","type":"address"},{"name":"operator","type":"address"}],"name":"isApprovedForAll","outputs":[{"name":"","type":"bool"}],"type":"function"},
      {"inputs":[{"name":"operator","type":"address"},{"name":"approved","type":"bool"}],"name":"setApprovalForAll","outputs":[],"type":"function"},
      {"inputs":[{"name":"from","type":"address"},{"name":"to","type":"address"},{"name":"id","type":"uint256"},{"name":"value","type":"uint256"},{"name":"data","type":"bytes"}],"name":"safeTransferFrom","outputs":[],"type":"function"},
      {"inputs":[{"name":"from","type":"address"},{"name":"to","type":"address"},{"name":"ids","type":"uint256[]"},{"name":"values","type":"uint256[]"},{"name":"data","type":"bytes"}],"name":"safeBatchTransferFrom","outputs":[],"type":"function"}
    ]
    """;

    private readonly DecimalClient _client;

    public Erc1155Service(DecimalClient client) => _client = client ?? throw new ArgumentNullException(nameof(client));

    public Task<BigInteger> BalanceOfAsync(string collection, string owner, BigInteger tokenId)
    {
        TransactionDraft.ValidateAddress(owner, nameof(owner));
        RequireNonNegative(tokenId, nameof(tokenId));
        return Function(collection, "balanceOf").CallAsync<BigInteger>(owner, tokenId);
    }

    public Task<string> UriAsync(string collection, BigInteger tokenId)
    {
        RequireNonNegative(tokenId, nameof(tokenId));
        return Function(collection, "uri").CallAsync<string>(tokenId);
    }

    public Task<string> NameAsync(string collection) => Function(collection, "name").CallAsync<string>();
    public Task<string> SymbolAsync(string collection) => Function(collection, "symbol").CallAsync<string>();
    public Task<string> ContractUriAsync(string collection) => Function(collection, "contractURI").CallAsync<string>();
    public Task<string> CreatorAsync(string collection) => Function(collection, "creator").CallAsync<string>();
    public Task<bool> AllowMintAsync(string collection) => Function(collection, "getAllowMint").CallAsync<bool>();
    public Task<bool> RefundableAsync(string collection) => Function(collection, "getRefundable").CallAsync<bool>();
    public Task<byte> NftTypeAsync(string collection) => Function(collection, "getNftType").CallAsync<byte>();

    public Task<BigInteger> RateAsync(string collection, BigInteger tokenId)
    {
        RequireNonNegative(tokenId, nameof(tokenId));
        return Function(collection, "rate").CallAsync<BigInteger>(tokenId);
    }

    public Task<bool> ExistsAsync(string collection, BigInteger tokenId)
    {
        RequireNonNegative(tokenId, nameof(tokenId));
        return Function(collection, "exists").CallAsync<bool>(tokenId);
    }

    public Task<BigInteger> TotalSupplyAsync(string collection, BigInteger tokenId)
    {
        RequireNonNegative(tokenId, nameof(tokenId));
        return Function(collection, "totalSupply").CallAsync<BigInteger>(tokenId);
    }

    public Task<List<BigInteger>> BalanceOfBatchAsync(string collection,
        IReadOnlyList<string> owners, IReadOnlyList<BigInteger> tokenIds)
    {
        ArgumentNullException.ThrowIfNull(owners);
        ArgumentNullException.ThrowIfNull(tokenIds);
        if (owners.Count == 0 || owners.Count != tokenIds.Count)
            throw new ArgumentException("ERC1155 batch requires matching nonempty owners and token IDs");
        foreach (var owner in owners) TransactionDraft.ValidateAddress(owner, nameof(owners));
        foreach (var id in tokenIds) RequireNonNegative(id, nameof(tokenIds));
        return Function(collection, "balanceOfBatch")
            .CallAsync<List<BigInteger>>(owners.ToArray(), tokenIds.ToArray());
    }

    public Task<bool> IsApprovedForAllAsync(string collection, string owner, string operatorAddress)
    {
        TransactionDraft.ValidateAddress(owner, nameof(owner));
        TransactionDraft.ValidateAddress(operatorAddress, nameof(operatorAddress));
        return Function(collection, "isApprovedForAll").CallAsync<bool>(owner, operatorAddress);
    }

    public TransactionDraft SetApprovalForAll(string from, string collection, string operatorAddress, bool approved)
    {
        TransactionDraft.ValidateAddress(from, nameof(from));
        TransactionDraft.ValidateAddress(operatorAddress, nameof(operatorAddress));
        return TransactionDraft.ContractCall(from, collection,
            Function(collection, "setApprovalForAll").GetData(operatorAddress, approved));
    }

    public TransactionDraft SafeTransferFrom(string from, string collection, string to,
        BigInteger tokenId, BigInteger amount, byte[]? data = null)
    {
        ValidateTransfer(from, to);
        RequireNonNegative(tokenId, nameof(tokenId));
        RequirePositive(amount, nameof(amount));
        return TransactionDraft.ContractCall(from, collection,
            Function(collection, "safeTransferFrom").GetData(from, to, tokenId, amount, data ?? []));
    }

    public TransactionDraft SafeBatchTransferFrom(string from, string collection, string to,
        IReadOnlyList<BigInteger> tokenIds, IReadOnlyList<BigInteger> amounts, byte[]? data = null)
    {
        ValidateTransfer(from, to);
        ArgumentNullException.ThrowIfNull(tokenIds);
        ArgumentNullException.ThrowIfNull(amounts);
        if (tokenIds.Count == 0 || tokenIds.Count != amounts.Count)
            throw new ArgumentException("ERC1155 batch requires matching nonempty token IDs and amounts");
        foreach (var id in tokenIds) RequireNonNegative(id, nameof(tokenIds));
        foreach (var amount in amounts) RequirePositive(amount, nameof(amounts));
        return TransactionDraft.ContractCall(from, collection,
            Function(collection, "safeBatchTransferFrom").GetData(
                from, to, tokenIds.ToArray(), amounts.ToArray(), data ?? []));
    }

    private Function Function(string collection, string name)
    {
        TransactionDraft.ValidateAddress(collection, nameof(collection));
        return _client.Web3.Eth.GetContract(Abi, collection).GetFunction(name);
    }

    private static void ValidateTransfer(string from, string to)
    {
        TransactionDraft.ValidateAddress(from, nameof(from));
        TransactionDraft.ValidateAddress(to, nameof(to));
    }

    private static void RequireNonNegative(BigInteger value, string name)
    {
        if (value < 0 || value >= BigInteger.One << 256)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void RequirePositive(BigInteger value, string name)
    {
        if (value <= 0 || value >= BigInteger.One << 256)
            throw new ArgumentOutOfRangeException(name);
    }
}
