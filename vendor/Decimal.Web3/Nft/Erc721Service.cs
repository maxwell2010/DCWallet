using System.Numerics;
using Decimal.Web3.Transactions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.MessageEncodingServices;

namespace Decimal.Web3.Nft;

/// <summary>Standard ERC721 reads and transaction drafts for Decimal EVM.</summary>
public sealed class Erc721Service
{
    private const string Abi = """
    [
      {"constant":true,"inputs":[{"name":"owner","type":"address"}],"name":"balanceOf","outputs":[{"name":"","type":"uint256"}],"type":"function"},
      {"constant":true,"inputs":[],"name":"name","outputs":[{"name":"","type":"string"}],"type":"function"},
      {"constant":true,"inputs":[],"name":"symbol","outputs":[{"name":"","type":"string"}],"type":"function"},
      {"constant":true,"inputs":[],"name":"contractURI","outputs":[{"name":"","type":"string"}],"type":"function"},
      {"constant":true,"inputs":[],"name":"creator","outputs":[{"name":"","type":"address"}],"type":"function"},
      {"constant":true,"inputs":[],"name":"getAllowMint","outputs":[{"name":"","type":"bool"}],"type":"function"},
      {"constant":true,"inputs":[],"name":"getRefundable","outputs":[{"name":"","type":"bool"}],"type":"function"},
      {"constant":true,"inputs":[],"name":"getNftType","outputs":[{"name":"","type":"uint8"}],"type":"function"},
      {"constant":true,"inputs":[{"name":"tokenId","type":"uint256"}],"name":"ownerOf","outputs":[{"name":"","type":"address"}],"type":"function"},
      {"constant":true,"inputs":[{"name":"tokenId","type":"uint256"}],"name":"tokenURI","outputs":[{"name":"","type":"string"}],"type":"function"},
      {"constant":true,"inputs":[{"name":"tokenId","type":"uint256"}],"name":"getApproved","outputs":[{"name":"","type":"address"}],"type":"function"},
      {"constant":true,"inputs":[{"name":"owner","type":"address"},{"name":"operator","type":"address"}],"name":"isApprovedForAll","outputs":[{"name":"","type":"bool"}],"type":"function"},
      {"inputs":[{"name":"from","type":"address"},{"name":"to","type":"address"},{"name":"tokenId","type":"uint256"}],"name":"transferFrom","outputs":[],"type":"function"},
      {"inputs":[{"name":"from","type":"address"},{"name":"to","type":"address"},{"name":"tokenId","type":"uint256"}],"name":"safeTransferFrom","outputs":[],"type":"function"},
      {"inputs":[{"name":"from","type":"address"},{"name":"to","type":"address"},{"name":"tokenId","type":"uint256"},{"name":"data","type":"bytes"}],"name":"safeTransferFrom","outputs":[],"type":"function"},
      {"inputs":[{"name":"to","type":"address"},{"name":"tokenId","type":"uint256"}],"name":"approve","outputs":[],"type":"function"},
      {"inputs":[{"name":"operator","type":"address"},{"name":"approved","type":"bool"}],"name":"setApprovalForAll","outputs":[],"type":"function"}
    ]
    """;

    private readonly DecimalClient _client;

    public Erc721Service(DecimalClient client) => _client = client ?? throw new ArgumentNullException(nameof(client));

    public Task<BigInteger> BalanceOfAsync(string collection, string owner)
    {
        TransactionDraft.ValidateAddress(owner, nameof(owner));
        return Function(collection, "balanceOf").CallAsync<BigInteger>(owner);
    }

    public Task<string> NameAsync(string collection) => Function(collection, "name").CallAsync<string>();
    public Task<string> SymbolAsync(string collection) => Function(collection, "symbol").CallAsync<string>();
    public Task<string> ContractUriAsync(string collection) => Function(collection, "contractURI").CallAsync<string>();
    public Task<string> CreatorAsync(string collection) => Function(collection, "creator").CallAsync<string>();
    public Task<bool> AllowMintAsync(string collection) => Function(collection, "getAllowMint").CallAsync<bool>();
    public Task<bool> RefundableAsync(string collection) => Function(collection, "getRefundable").CallAsync<bool>();
    public Task<byte> NftTypeAsync(string collection) => Function(collection, "getNftType").CallAsync<byte>();

    public Task<string> OwnerOfAsync(string collection, BigInteger tokenId)
    {
        RequireTokenId(tokenId);
        return Function(collection, "ownerOf").CallAsync<string>(tokenId);
    }

    public Task<string> TokenUriAsync(string collection, BigInteger tokenId)
    {
        RequireTokenId(tokenId);
        return Function(collection, "tokenURI").CallAsync<string>(tokenId);
    }

    public Task<string> GetApprovedAsync(string collection, BigInteger tokenId)
    {
        RequireTokenId(tokenId);
        return Function(collection, "getApproved").CallAsync<string>(tokenId);
    }

    public Task<bool> IsApprovedForAllAsync(string collection, string owner, string operatorAddress)
    {
        TransactionDraft.ValidateAddress(owner, nameof(owner));
        TransactionDraft.ValidateAddress(operatorAddress, nameof(operatorAddress));
        return Function(collection, "isApprovedForAll").CallAsync<bool>(owner, operatorAddress);
    }

    public TransactionDraft TransferFrom(string from, string collection, string to, BigInteger tokenId)
    {
        TransactionDraft.ValidateAddress(from, nameof(from));
        TransactionDraft.ValidateAddress(to, nameof(to));
        RequireTokenId(tokenId);
        return TransactionDraft.ContractCall(from, collection,
            Function(collection, "transferFrom").GetData(from, to, tokenId));
    }

    public TransactionDraft SafeTransferFrom(string from, string collection, string to, BigInteger tokenId)
    {
        TransactionDraft.ValidateAddress(from, nameof(from));
        TransactionDraft.ValidateAddress(to, nameof(to));
        RequireTokenId(tokenId);
        return TransactionDraft.ContractCall(from, collection,
            Function(collection, "safeTransferFrom").GetData(from, to, tokenId));
    }

    public TransactionDraft SafeTransferFrom(string from, string collection, string to,
        BigInteger tokenId, byte[] data)
    {
        TransactionDraft.ValidateAddress(from, nameof(from));
        TransactionDraft.ValidateAddress(to, nameof(to));
        RequireTokenId(tokenId);
        ArgumentNullException.ThrowIfNull(data);
        TransactionDraft.ValidateAddress(collection, nameof(collection));
        var encoder = new FunctionMessageEncodingService<SafeTransferWithDataFunction>(collection, from);
        var callData = encoder.GetCallData(new SafeTransferWithDataFunction
        {
            Owner = from, Recipient = to, TokenId = tokenId, Data = data
        });
        return TransactionDraft.ContractCall(from, collection,
            "0x" + Convert.ToHexString(callData).ToLowerInvariant());
    }

    public TransactionDraft Approve(string from, string collection, string approved, BigInteger tokenId)
    {
        TransactionDraft.ValidateAddress(approved, nameof(approved));
        RequireTokenId(tokenId);
        return TransactionDraft.ContractCall(from, collection,
            Function(collection, "approve").GetData(approved, tokenId));
    }

    public TransactionDraft SetApprovalForAll(string from, string collection, string operatorAddress, bool approved)
    {
        TransactionDraft.ValidateAddress(operatorAddress, nameof(operatorAddress));
        return TransactionDraft.ContractCall(from, collection,
            Function(collection, "setApprovalForAll").GetData(operatorAddress, approved));
    }

    private Function Function(string collection, string name)
    {
        TransactionDraft.ValidateAddress(collection, nameof(collection));
        return _client.Web3.Eth.GetContract(Abi, collection).GetFunction(name);
    }

    private static void RequireTokenId(BigInteger tokenId)
    {
        if (tokenId < 0 || tokenId >= BigInteger.One << 256)
            throw new ArgumentOutOfRangeException(nameof(tokenId));
    }
}

[Function("safeTransferFrom")]
public sealed class SafeTransferWithDataFunction : FunctionMessage
{
    [Parameter("address", "from", 1)] public string Owner { get; set; } = string.Empty;
    [Parameter("address", "to", 2)] public string Recipient { get; set; } = string.Empty;
    [Parameter("uint256", "tokenId", 3)] public BigInteger TokenId { get; set; }
    [Parameter("bytes", "data", 4)] public byte[] Data { get; set; } = [];
}
