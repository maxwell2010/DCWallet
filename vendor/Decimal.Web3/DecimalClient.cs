using System.Numerics;
using Decimal.Web3.Transactions;
using Decimal.Web3.Wallet;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

namespace Decimal.Web3;

/// <summary>Read, quote, sign and explicitly broadcast Decimal EVM transactions.</summary>
public sealed class DecimalClient
{
    private readonly SemaphoreSlim _chainCheckGate = new(1, 1);
    private volatile bool _chainValidated;

    public DecimalNetwork Network { get; }
    public Nethereum.Web3.Web3 Web3 { get; }

    public DecimalClient(DecimalNetwork? network = null)
    {
        Network = network ?? DecimalNetwork.Mainnet();
        Web3 = new Nethereum.Web3.Web3(Network.RpcUrl.ToString());
    }

    /// <summary>Resolve an official system contract by slug from Contract Center.</summary>
    public async Task<string> ResolveSystemContractAsync(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("Contract slug is required", nameof(slug));
        TransactionDraft.ValidateAddress(Network.ContractCenterContract, nameof(Network.ContractCenterContract));
        await ValidateChainAsync();
        return await ResolveSystemContractUncheckedAsync(slug);
    }

    private async Task<string> ResolveSystemContractUncheckedAsync(string slug)
    {
        const string abi = """
            [{"constant":true,"inputs":[{"name":"name","type":"string"}],
              "name":"getContractAddress","outputs":[{"name":"","type":"address"}],"type":"function"}]
            """;
        var address = await Web3.Eth.GetContract(abi, Network.ContractCenterContract)
            .GetFunction("getContractAddress").CallAsync<string>(slug);
        TransactionDraft.ValidateAddress(address, nameof(address));
        if (address.Equals("0x0000000000000000000000000000000000000000", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("System contract is not registered");
        return address;
    }

    /// <summary>Get a network configuration with child contract addresses pinned to the current registry.</summary>
    public async Task<DecimalNetwork> ResolveSystemContractsAsync()
    {
        TransactionDraft.ValidateAddress(Network.ContractCenterContract, nameof(Network.ContractCenterContract));
        await ValidateChainAsync();
        var delegation = ResolveSystemContractUncheckedAsync("delegation");
        var tokenCenter = ResolveSystemContractUncheckedAsync("token-center");
        var nftCenter = ResolveSystemContractUncheckedAsync("nft-center");
        var wdel = ResolveSystemContractUncheckedAsync("wdel");
        await Task.WhenAll(delegation, tokenCenter, nftCenter, wdel);
        return Network with
        {
            DelegationContract = delegation.Result,
            TokenCenterContract = tokenCenter.Result,
            NftCenterContract = nftCenter.Result,
            WdelContract = wdel.Result
        };
    }

    public async Task<BigInteger> GetBalanceWeiAsync(string address)
    {
        TransactionDraft.ValidateAddress(address, nameof(address));
        await ValidateChainAsync();
        return (await Web3.Eth.GetBalance.SendRequestAsync(address)).Value;
    }

    public async Task<FeeQuote> EstimateFeeAsync(TransactionDraft draft, bool exact = false)
    {
        ArgumentNullException.ThrowIfNull(draft);
        try
        {
            await ValidateChainAsync();
            var input = ToInput(draft);
            var gasTask = Web3.Eth.Transactions.EstimateGas.SendRequestAsync(input);
            var gasPriceTask = Web3.Eth.GasPrice.SendRequestAsync();
            var balanceTask = GetBalanceWeiAsync(draft.From);
            await Task.WhenAll(gasTask, gasPriceTask, balanceTask);
            var gas = gasTask.Result.Value;
            var gasPrice = gasPriceTask.Result.Value;
            var balance = balanceTask.Result;
            var gasLimit = exact ? gas : (gas * 110 + 99) / 100;
            return new FeeQuote(draft, gas, gasLimit, gasPrice, balance, exact);
        }
        catch (InvalidOperationException ex) when (ex.Message == "RPC chain ID does not match the selected Decimal network")
        {
            throw;
        }
        catch (Exception ex)
        {
            throw DecimalTransactionException.FromRpcError(ex);
        }
    }

    public async Task<SignedTransaction> SignAsync(
        TransactionDraft draft, FeeQuote quote, string mnemonic, int accountIndex = 0, string passphrase = "")
    {
        ValidateSignRequest(draft, quote);
        using var signer = DecimalWallet.CreateTransactionSigner(mnemonic, Network.ChainId, accountIndex, passphrase);
        return await SignWithSignerAsync(draft, quote, signer);
    }

    /// <summary>Signs with a reusable derived account after checking quote, sender and chain.</summary>
    public async Task<SignedTransaction> SignWithSignerAsync(
        TransactionDraft draft, FeeQuote quote, DecimalTransactionSigner signer)
    {
        ValidateSignRequest(draft, quote);
        ArgumentNullException.ThrowIfNull(signer);
        signer.EnsureNotDisposed();
        if (signer.ChainId != Network.ChainId)
            throw new InvalidOperationException("Signer chain ID does not match the selected Decimal network");
        if (!signer.Address.Equals(draft.From, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Signer account does not match transaction sender");
        await ValidateChainAsync();
        var nonce = (await Web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(
            draft.From, BlockParameter.CreatePending())).Value;
        var raw = signer.SignLegacyTransaction(nonce, draft.To, draft.ValueWei,
            quote.GasPriceWei, quote.GasLimit, draft.Data);
        return new SignedTransaction(raw, draft.From, draft.To, nonce, quote.GasLimit, quote.GasPriceWei);
    }

    private static void ValidateSignRequest(TransactionDraft draft, FeeQuote quote)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(quote);
        if (quote.Draft != draft || quote.GasLimit <= 0 || quote.GasPriceWei < 0)
            throw new ArgumentException("Fee quote does not match the transaction", nameof(quote));
        if (!quote.CanAfford)
            throw new DecimalTransactionException(quote.NativeBalanceWei < draft.ValueWei
                ? DecimalTransactionError.InsufficientDel : DecimalTransactionError.InsufficientFee);
    }

    public async Task<TransactionResult> BroadcastAsync(SignedTransaction signed, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signed);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await ValidateChainAsync();
            var hash = await Web3.Eth.Transactions.SendRawTransaction.SendRequestAsync(signed.RawHex);
            return new TransactionResult(hash, null, null, null, null);
        }
        catch (InvalidOperationException ex) when (ex.Message == "RPC chain ID does not match the selected Decimal network")
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            throw DecimalTransactionException.FromRpcError(ex, signed.Hash);
        }
    }

    public async Task<TransactionResult> WaitForReceiptAsync(
        string hash, TimeSpan? timeout = null, TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
    {
        var until = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        var poll = pollInterval ?? TimeSpan.FromSeconds(2);
        if (poll <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(pollInterval));
        while (DateTimeOffset.UtcNow <= until)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var receipt = await Web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(hash);
            if (receipt is not null)
            {
                var effectiveGasPrice = receipt.EffectiveGasPrice?.Value;
                if (effectiveGasPrice is null)
                {
                    var transaction = await Web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(hash);
                    effectiveGasPrice = transaction?.GasPrice?.Value;
                }
                return new TransactionResult(
                    hash,
                    receipt.BlockNumber?.Value,
                    receipt.Status is null ? null : receipt.Status.Value == BigInteger.One,
                    receipt.GasUsed?.Value,
                    effectiveGasPrice);
            }
            await Task.Delay(poll, cancellationToken);
        }
        return new TransactionResult(hash, null, null, null, null);
    }

    private async Task ValidateChainAsync()
    {
        if (_chainValidated) return;
        await _chainCheckGate.WaitAsync();
        try
        {
            if (_chainValidated) return;
            var actual = (await Web3.Eth.ChainId.SendRequestAsync()).Value;
            if (actual != Network.ChainId)
                throw new InvalidOperationException("RPC chain ID does not match the selected Decimal network");
            _chainValidated = true;
        }
        finally
        {
            _chainCheckGate.Release();
        }
    }

    private static TransactionInput ToInput(TransactionDraft draft) => new()
    {
        From = draft.From,
        To = draft.To,
        Value = new HexBigInteger(draft.ValueWei),
        Data = draft.Data
    };
}
