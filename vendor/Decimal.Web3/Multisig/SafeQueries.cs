using System.Numerics;
using Decimal.Web3.Transactions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;

namespace Decimal.Web3.Multisig;

/// <summary>Read-only queries against a caller-supplied Safe deployment.</summary>
public sealed class SafeQueries
{
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - 1;
    private readonly Nethereum.Web3.Web3 _web3;
    private readonly string _safe;
    private readonly DecimalNetwork? _network;

    public SafeQueries(Nethereum.Web3.Web3 web3, string safeAddress, DecimalNetwork? network = null)
    {
        _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
        TransactionDraft.ValidateAddress(safeAddress, nameof(safeAddress));
        if (network is not null && network.ChainId <= 0)
            throw new ArgumentOutOfRangeException(nameof(network), "Network chain ID must be positive.");
        _safe = safeAddress;
        _network = network;
    }

    public async Task<BigInteger> GetNonceAsync(BlockParameter? block = null)
    {
        await ValidateChainAsync();
        return await _web3.Eth.GetContractQueryHandler<SafeNonceFunction>().QueryAsync<BigInteger>(
            _safe, new SafeNonceFunction(), block ?? BlockParameter.CreateLatest());
    }

    public async Task<BigInteger> GetThresholdAsync(BlockParameter? block = null)
    {
        await ValidateChainAsync();
        return await _web3.Eth.GetContractQueryHandler<SafeThresholdFunction>().QueryAsync<BigInteger>(
            _safe, new SafeThresholdFunction(), block ?? BlockParameter.CreateLatest());
    }

    public async Task<IReadOnlyList<string>> GetOwnersAsync(BlockParameter? block = null)
    {
        await ValidateChainAsync();
        var owners = await _web3.Eth.GetContractQueryHandler<SafeOwnersFunction>().QueryAsync<List<string>>(
            _safe, new SafeOwnersFunction(), block ?? BlockParameter.CreateLatest());
        foreach (var owner in owners) TransactionDraft.ValidateAddress(owner, nameof(owners));
        return owners;
    }

    public async Task<bool> IsOwnerAsync(string owner, BlockParameter? block = null)
    {
        TransactionDraft.ValidateAddress(owner, nameof(owner));
        await ValidateChainAsync();
        return await _web3.Eth.GetContractQueryHandler<SafeIsOwnerFunction>().QueryAsync<bool>(
            _safe, new SafeIsOwnerFunction { Owner = owner }, block ?? BlockParameter.CreateLatest());
    }

    /// <summary>Checks one live Safe state snapshot before returning an unsigned execution draft.</summary>
    public async Task<TransactionDraft> BuildExecutionDraftAsync(string executor, SafeTransactionDraft transaction,
        BigInteger chainId, IEnumerable<SafeSignature> signatures)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        if (!string.Equals(transaction.SafeAddress, _safe, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Transaction targets a different Safe.", nameof(transaction));
        TransactionDraft.ValidateAddress(executor, nameof(executor));
        ArgumentNullException.ThrowIfNull(signatures);
        var supplied = signatures.ToArray();
        SafeTransactions.PackEoaSignatures(transaction, chainId, supplied);

        var actualChainId = (await _web3.Eth.ChainId.SendRequestAsync()).Value;
        if (actualChainId != chainId || (_network is not null && actualChainId != _network.ChainId))
            throw new InvalidOperationException("RPC chain ID does not match the Safe transaction network.");
        var blockNumber = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
        var block = new BlockParameter(blockNumber);
        var nonce = await _web3.Eth.GetContractQueryHandler<SafeNonceFunction>()
            .QueryAsync<BigInteger>(_safe, new SafeNonceFunction(), block);
        if (nonce != transaction.Nonce)
            throw new InvalidOperationException("Safe transaction nonce is no longer current.");
        var threshold = await _web3.Eth.GetContractQueryHandler<SafeThresholdFunction>()
            .QueryAsync<BigInteger>(_safe, new SafeThresholdFunction(), block);
        var owners = await _web3.Eth.GetContractQueryHandler<SafeOwnersFunction>()
            .QueryAsync<List<string>>(_safe, new SafeOwnersFunction(), block);
        if (owners is null || owners.Count == 0 || threshold < 1 ||
            owners.Any(owner => !IsValidOwner(owner)) ||
            owners.Distinct(StringComparer.OrdinalIgnoreCase).Count() != owners.Count)
            throw new InvalidOperationException("Safe returned an invalid owner or threshold state.");
        var ownerSet = new HashSet<string>(owners, StringComparer.OrdinalIgnoreCase);
        if (supplied.Any(signature => !ownerSet.Contains(signature.Signer)))
            throw new InvalidOperationException("Safe execution includes a signer who is not a current owner.");
        return SafeTransactions.BuildExecutionDraft(executor, transaction, chainId, supplied);
    }

    private static bool IsValidOwner(string? owner)
    {
        try
        {
            TransactionDraft.ValidateAddress(owner!, nameof(owner));
            return !string.Equals(owner, SafeTransactionDraft.ZeroAddress, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Returns the on-chain SafeTx hash. The Safe must implement getTransactionHash.</summary>
    public async Task<string> GetTransactionHashAsync(SafeTransactionDraft transaction, BlockParameter? block = null)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        if (!string.Equals(transaction.SafeAddress, _safe, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Transaction targets a different Safe.", nameof(transaction));
        TransactionDraft.ValidateAddress(transaction.To, nameof(transaction));
        TransactionDraft.ValidateAddress(transaction.GasToken, nameof(transaction));
        TransactionDraft.ValidateAddress(transaction.RefundReceiver, nameof(transaction));
        if (transaction.Operation != 0)
            throw new NotSupportedException("Only Safe Call operation 0 is supported.");
        ValidateUint(transaction.ValueWei);
        ValidateUint(transaction.SafeTxGas);
        ValidateUint(transaction.BaseGas);
        ValidateUint(transaction.GasPriceWei);
        ValidateUint(transaction.Nonce);
        if (transaction.Data is null || !transaction.Data.StartsWith("0x", StringComparison.Ordinal) ||
            transaction.Data.Length % 2 != 0 || !transaction.Data.AsSpan(2).ToString().All(Uri.IsHexDigit))
            throw new ArgumentException("Transaction data must be even-length 0x-prefixed hex.", nameof(transaction));

        await ValidateChainAsync();
        var hash = await _web3.Eth.GetContractQueryHandler<SafeTransactionHashFunction>().QueryAsync<byte[]>(
            _safe, new SafeTransactionHashFunction
            {
                To = transaction.To,
                Value = transaction.ValueWei,
                Data = Convert.FromHexString(transaction.Data[2..]),
                Operation = transaction.Operation,
                SafeTxGas = transaction.SafeTxGas,
                BaseGas = transaction.BaseGas,
                SafeGasPrice = transaction.GasPriceWei,
                GasToken = transaction.GasToken,
                RefundReceiver = transaction.RefundReceiver,
                SafeNonce = transaction.Nonce
            }, block ?? BlockParameter.CreateLatest());
        if (hash is null || hash.Length != 32)
            throw new InvalidOperationException("Safe returned an invalid transaction hash.");
        return "0x" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task ValidateChainAsync()
    {
        if (_network is null) return;
        var actual = (await _web3.Eth.ChainId.SendRequestAsync()).Value;
        if (actual != _network.ChainId)
            throw new InvalidOperationException("RPC chain ID does not match the selected Decimal network");
    }

    private static void ValidateUint(BigInteger value)
    {
        if (value < 0 || value > MaxUint256)
            throw new ArgumentOutOfRangeException(nameof(value), "Safe transaction value must fit uint256.");
    }
}

[Function("nonce", "uint256")]
public sealed class SafeNonceFunction : FunctionMessage;

[Function("getThreshold", "uint256")]
public sealed class SafeThresholdFunction : FunctionMessage;

[Function("getOwners", "address[]")]
public sealed class SafeOwnersFunction : FunctionMessage;

[Function("isOwner", "bool")]
public sealed class SafeIsOwnerFunction : FunctionMessage
{
    [Parameter("address", "owner", 1)] public string Owner { get; set; } = string.Empty;
}

[Function("getTransactionHash", "bytes32")]
public sealed class SafeTransactionHashFunction : FunctionMessage
{
    [Parameter("address", "to", 1)] public string To { get; set; } = string.Empty;
    [Parameter("uint256", "value", 2)] public BigInteger Value { get; set; }
    [Parameter("bytes", "data", 3)] public byte[] Data { get; set; } = [];
    [Parameter("uint8", "operation", 4)] public byte Operation { get; set; }
    [Parameter("uint256", "safeTxGas", 5)] public BigInteger SafeTxGas { get; set; }
    [Parameter("uint256", "baseGas", 6)] public BigInteger BaseGas { get; set; }
    [Parameter("uint256", "gasPrice", 7)] public BigInteger SafeGasPrice { get; set; }
    [Parameter("address", "gasToken", 8)] public string GasToken { get; set; } = string.Empty;
    [Parameter("address", "refundReceiver", 9)] public string RefundReceiver { get; set; } = string.Empty;
    [Parameter("uint256", "_nonce", 10)] public BigInteger SafeNonce { get; set; }
}
