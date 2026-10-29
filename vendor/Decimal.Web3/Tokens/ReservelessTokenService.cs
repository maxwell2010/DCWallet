using System.Numerics;
using Decimal.Web3.TokenEconomy;
using Decimal.Web3.Transactions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.MessageEncodingServices;
using Nethereum.RPC.Eth.DTOs;

namespace Decimal.Web3.Tokens;

public sealed record ReservelessTokenInfo(
    Erc20Metadata Metadata,
    BigInteger CapRaw,
    bool Mintable,
    bool Burnable,
    string Owner,
    string Identity);

/// <summary>Reads and drafts specific to Decimal reservless ERC20 tokens.</summary>
public sealed class ReservelessTokenService
{
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - 1;
    private readonly DecimalClient _client;
    private readonly Erc20Service _erc20;

    public string TokenAddress => _erc20.TokenAddress;

    public ReservelessTokenService(DecimalClient client, string tokenAddress)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _erc20 = new Erc20Service(client, tokenAddress);
    }

    public async Task<ReservelessTokenInfo> GetInfoAsync(BlockParameter? block = null)
    {
        var snapshot = block ?? BlockParameter.CreateLatest();
        var metadata = _erc20.GetMetadataAsync(snapshot);
        var cap = GetCapRawAsync(snapshot);
        var mintable = GetMintableAsync(snapshot);
        var burnable = GetBurnableAsync(snapshot);
        var owner = GetOwnerAsync(snapshot);
        var identity = GetIdentityAsync(snapshot);
        await Task.WhenAll(metadata, cap, mintable, burnable, owner, identity).ConfigureAwait(false);
        return new ReservelessTokenInfo(metadata.Result, cap.Result, mintable.Result,
            burnable.Result, owner.Result, identity.Result);
    }

    public Task<BigInteger> GetCapRawAsync(BlockParameter? block = null) =>
        QueryAsync<ReservelessCapFunction, BigInteger>(new(), block);

    public Task<bool> GetMintableAsync(BlockParameter? block = null) =>
        QueryAsync<ReservelessMintableFunction, bool>(new(), block);

    public Task<bool> GetBurnableAsync(BlockParameter? block = null) =>
        QueryAsync<ReservelessBurnableFunction, bool>(new(), block);

    public Task<string> GetOwnerAsync(BlockParameter? block = null) =>
        QueryAsync<ReservelessOwnerFunction, string>(new(), block);

    public Task<string> GetIdentityAsync(BlockParameter? block = null) =>
        QueryAsync<ReservelessIdentityFunction, string>(new(), block);

    public TransactionDraft BuildMintDraft(string from, string recipient, BigInteger amountRaw)
    {
        TransactionDraft.ValidateAddress(recipient, nameof(recipient));
        ValidatePositiveAmount(amountRaw);
        return Build(from, new MintReservelessFunction { Recipient = recipient, Amount = amountRaw });
    }

    public async Task<TransactionDraft> BuildMintCheckedAsync(
        string from, string recipient, BigInteger amountRaw, BlockParameter? block = null)
    {
        TransactionDraft.ValidateAddress(from, nameof(from));
        var draft = BuildMintDraft(from, recipient, amountRaw);
        var info = await GetInfoAsync(block).ConfigureAwait(false);
        if (!info.Mintable)
            throw new InvalidOperationException("Token minting is disabled");
        if (!info.Owner.Equals(from, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only the token owner can mint");
        if (amountRaw > info.CapRaw - info.Metadata.TotalSupplyRaw)
            throw new InvalidOperationException("Mint amount exceeds token cap");
        return draft;
    }

    public TransactionDraft BuildBurnDraft(string from, BigInteger amountRaw)
    {
        ValidatePositiveAmount(amountRaw);
        return Build(from, new BurnFunction { Value = amountRaw });
    }

    public async Task<TransactionDraft> BuildBurnCheckedAsync(
        string from, BigInteger amountRaw, BlockParameter? block = null)
    {
        TransactionDraft.ValidateAddress(from, nameof(from));
        var draft = BuildBurnDraft(from, amountRaw);
        var snapshot = block ?? BlockParameter.CreateLatest();
        var burnable = GetBurnableAsync(snapshot);
        var balance = _erc20.GetBalanceRawAsync(from, snapshot);
        await Task.WhenAll(burnable, balance).ConfigureAwait(false);
        if (!burnable.Result)
            throw new InvalidOperationException("Token burning is disabled");
        if (balance.Result < amountRaw)
            throw new InvalidOperationException("Insufficient token balance to burn");
        return draft;
    }

    private Task<T> QueryAsync<TFunction, T>(TFunction function, BlockParameter? block)
        where TFunction : FunctionMessage, new() =>
        _client.Web3.Eth.GetContractQueryHandler<TFunction>().QueryAsync<T>(
            TokenAddress, function, block ?? BlockParameter.CreateLatest());

    private TransactionDraft Build<TFunction>(string from, TFunction function)
        where TFunction : FunctionMessage, new()
    {
        TransactionDraft.ValidateAddress(from, nameof(from));
        var encoder = new FunctionMessageEncodingService<TFunction>(TokenAddress, from);
        var data = "0x" + Convert.ToHexString(encoder.GetCallData(function)).ToLowerInvariant();
        return TransactionDraft.ContractCall(from, TokenAddress, data);
    }

    private static void ValidatePositiveAmount(BigInteger amountRaw)
    {
        if (amountRaw <= 0 || amountRaw > MaxUint256)
            throw new ArgumentOutOfRangeException(nameof(amountRaw), "Amount must be a positive uint256");
    }
}

[Function("cap", "uint256")]
public sealed class ReservelessCapFunction : FunctionMessage;

[Function("mintable", "bool")]
public sealed class ReservelessMintableFunction : FunctionMessage;

[Function("burnable", "bool")]
public sealed class ReservelessBurnableFunction : FunctionMessage;

[Function("owner", "address")]
public sealed class ReservelessOwnerFunction : FunctionMessage;

[Function("identity", "string")]
public sealed class ReservelessIdentityFunction : FunctionMessage;
