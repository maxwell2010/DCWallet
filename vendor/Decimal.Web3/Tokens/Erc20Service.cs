using System.Numerics;
using Decimal.Web3.Transactions;
using Nethereum.Contracts;
using Nethereum.Contracts.MessageEncodingServices;
using Nethereum.RPC.Eth.DTOs;

namespace Decimal.Web3.Tokens;

public sealed record Erc20Metadata(string Name, string Symbol, byte Decimals, BigInteger TotalSupplyRaw);

/// <summary>ERC20 reads and unsigned contract-call drafts. This service never submits transactions.</summary>
public sealed class Erc20Service
{
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - BigInteger.One;
    private readonly DecimalClient _client;

    public string TokenAddress { get; }

    public Erc20Service(DecimalClient client, string tokenAddress)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        TransactionDraft.ValidateAddress(tokenAddress, nameof(tokenAddress));
        TokenAddress = tokenAddress;
    }

    public async Task<Erc20Metadata> GetMetadataAsync(BlockParameter? block = null)
    {
        var snapshot = block ?? BlockParameter.CreateLatest();
        var name = GetNameAsync(snapshot);
        var symbol = GetSymbolAsync(snapshot);
        var decimals = GetDecimalsAsync(snapshot);
        var totalSupply = GetTotalSupplyRawAsync(snapshot);
        await Task.WhenAll(name, symbol, decimals, totalSupply).ConfigureAwait(false);
        return new Erc20Metadata(name.Result, symbol.Result, decimals.Result, totalSupply.Result);
    }

    public Task<string> GetNameAsync(BlockParameter? block = null) =>
        QueryAsync<Erc20NameFunction, string>(new(), block);

    public Task<string> GetSymbolAsync(BlockParameter? block = null) =>
        QueryAsync<Erc20SymbolFunction, string>(new(), block);

    public Task<byte> GetDecimalsAsync(BlockParameter? block = null) =>
        QueryAsync<Erc20DecimalsFunction, byte>(new(), block);

    public Task<BigInteger> GetTotalSupplyRawAsync(BlockParameter? block = null) =>
        QueryAsync<Erc20TotalSupplyFunction, BigInteger>(new(), block);

    public Task<string> GetVersionAsync(BlockParameter? block = null) =>
        QueryAsync<Erc20VersionFunction, string>(new(), block);

    public Task<byte[]> GetDomainSeparatorAsync(BlockParameter? block = null) =>
        QueryAsync<Erc20DomainSeparatorFunction, byte[]>(new(), block);

    public Task<BigInteger> GetNonceAsync(string owner, BlockParameter? block = null)
    {
        TransactionDraft.ValidateAddress(owner, nameof(owner));
        return QueryAsync<Erc20NonceFunction, BigInteger>(new() { Owner = owner }, block);
    }

    public Task<BigInteger> GetBalanceRawAsync(string account, BlockParameter? block = null)
    {
        TransactionDraft.ValidateAddress(account, nameof(account));
        return QueryAsync<Erc20BalanceOfFunction, BigInteger>(new() { Account = account }, block);
    }

    public Task<BigInteger> GetAllowanceRawAsync(string owner, string spender, BlockParameter? block = null)
    {
        TransactionDraft.ValidateAddress(owner, nameof(owner));
        TransactionDraft.ValidateAddress(spender, nameof(spender));
        return QueryAsync<Erc20AllowanceFunction, BigInteger>(new() { Owner = owner, Spender = spender }, block);
    }

    public TransactionDraft BuildTransferDraft(string from, string to, BigInteger amountRaw)
    {
        TransactionDraft.ValidateAddress(to, nameof(to));
        ValidateAmount(amountRaw);
        return BuildDraft(from, new Erc20TransferFunction { To = to, Amount = amountRaw });
    }

    public TransactionDraft BuildTransferDraft(string from, string to, string amount, int decimals) =>
        BuildTransferDraft(from, to, Units.Parse(amount, decimals));

    public TransactionDraft BuildApproveDraft(string from, string spender, BigInteger amountRaw)
    {
        TransactionDraft.ValidateAddress(spender, nameof(spender));
        ValidateAmount(amountRaw);
        return BuildDraft(from, new Erc20ApproveFunction { Spender = spender, Amount = amountRaw });
    }

    public TransactionDraft BuildApproveDraft(string from, string spender, string amount, int decimals) =>
        BuildApproveDraft(from, spender, Units.Parse(amount, decimals));

    public TransactionDraft BuildTransferFromDraft(string sender, string owner, string to, BigInteger amountRaw)
    {
        TransactionDraft.ValidateAddress(owner, nameof(owner));
        TransactionDraft.ValidateAddress(to, nameof(to));
        ValidateAmount(amountRaw);
        return BuildDraft(sender, new Erc20TransferFromFunction { From = owner, To = to, Amount = amountRaw });
    }

    public TransactionDraft BuildTransferFromDraft(string sender, string owner, string to, string amount, int decimals) =>
        BuildTransferFromDraft(sender, owner, to, Units.Parse(amount, decimals));

    private Task<T> QueryAsync<TFunction, T>(TFunction function, BlockParameter? block)
        where TFunction : FunctionMessage, new() =>
        _client.Web3.Eth.GetContractQueryHandler<TFunction>().QueryAsync<T>(
            TokenAddress, function, block ?? BlockParameter.CreateLatest());

    private TransactionDraft BuildDraft<TFunction>(string sender, TFunction function)
        where TFunction : FunctionMessage, new()
    {
        TransactionDraft.ValidateAddress(sender, nameof(sender));
        var encoder = new FunctionMessageEncodingService<TFunction>(TokenAddress, sender);
        var data = "0x" + Convert.ToHexString(encoder.GetCallData(function)).ToLowerInvariant();
        return TransactionDraft.ContractCall(sender, TokenAddress, data);
    }

    private static void ValidateAmount(BigInteger amountRaw)
    {
        if (amountRaw < BigInteger.Zero || amountRaw > MaxUint256)
            throw new ArgumentOutOfRangeException(nameof(amountRaw), "Amount must fit uint256");
    }
}
