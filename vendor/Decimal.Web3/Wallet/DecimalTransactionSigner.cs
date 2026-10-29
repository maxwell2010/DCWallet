using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3.Accounts;
using Nethereum.Signer;

namespace Decimal.Web3.Wallet;

/// <summary>Reuses one derived Decimal EVM account for local transaction signing. No RPC calls are made.</summary>
public sealed class DecimalTransactionSigner : IDisposable
{
    private readonly object _gate = new();
    private readonly AccountOfflineTransactionSigner _signer = new();
    private Account? _account;

    internal DecimalTransactionSigner(Account account, BigInteger chainId)
    {
        _account = account;
        Address = account.Address;
        ChainId = chainId;
    }

    public string Address { get; }
    public BigInteger ChainId { get; }

    internal string SignDigest(byte[] digest)
    {
        ArgumentNullException.ThrowIfNull(digest);
        if (digest.Length != 32) throw new ArgumentException("Digest must be 32 bytes", nameof(digest));
        lock (_gate)
            return new MessageSigner().Sign(digest, new EthECKey(CurrentAccount().PrivateKey));
    }

    /// <summary>Signs a fully specified EIP-155 legacy transaction; all amounts are in wei.</summary>
    public string SignLegacyTransaction(BigInteger nonce, string to, BigInteger valueWei,
        BigInteger gasPriceWei, BigInteger gasLimit, string data = "0x")
    {
        lock (_gate)
        {
            var account = CurrentAccount();
            if (nonce < 0) throw new ArgumentOutOfRangeException(nameof(nonce));
            if (valueWei < 0) throw new ArgumentOutOfRangeException(nameof(valueWei));
            if (gasPriceWei < 0) throw new ArgumentOutOfRangeException(nameof(gasPriceWei));
            if (gasLimit <= 0) throw new ArgumentOutOfRangeException(nameof(gasLimit));
            DecimalWallet.ValidateTransactionFields(to, data);
            var transaction = new TransactionInput
            {
                From = Address,
                To = to,
                Nonce = new HexBigInteger(nonce),
                Value = new HexBigInteger(valueWei),
                GasPrice = new HexBigInteger(gasPriceWei),
                Gas = new HexBigInteger(gasLimit),
                Data = data
            };
            return _signer.SignTransaction(account, transaction, ChainId);
        }
    }

    /// <summary>Signs a fully specified EIP-1559 transaction; all amounts are in wei.</summary>
    public string SignEip1559Transaction(BigInteger nonce, string to, BigInteger valueWei,
        BigInteger maxPriorityFeePerGasWei, BigInteger maxFeePerGasWei,
        BigInteger gasLimit, string data = "0x")
    {
        lock (_gate)
        {
            var account = CurrentAccount();
            if (nonce < 0) throw new ArgumentOutOfRangeException(nameof(nonce));
            if (valueWei < 0) throw new ArgumentOutOfRangeException(nameof(valueWei));
            if (maxPriorityFeePerGasWei < 0) throw new ArgumentOutOfRangeException(nameof(maxPriorityFeePerGasWei));
            if (maxFeePerGasWei < maxPriorityFeePerGasWei)
                throw new ArgumentOutOfRangeException(nameof(maxFeePerGasWei), "Maximum fee must cover priority fee.");
            if (gasLimit <= 0) throw new ArgumentOutOfRangeException(nameof(gasLimit));
            DecimalWallet.ValidateTransactionFields(to, data);
            var transaction = new TransactionInput
            {
                From = Address,
                To = to,
                Type = new HexBigInteger(2),
                Nonce = new HexBigInteger(nonce),
                Value = new HexBigInteger(valueWei),
                MaxPriorityFeePerGas = new HexBigInteger(maxPriorityFeePerGasWei),
                MaxFeePerGas = new HexBigInteger(maxFeePerGasWei),
                Gas = new HexBigInteger(gasLimit),
                Data = data
            };
            return _signer.SignTransaction(account, transaction, ChainId);
        }
    }

    /// <summary>Releases references to the derived account; managed memory cannot be guaranteed erased.</summary>
    public void Dispose()
    {
        lock (_gate) _account = null;
    }

    internal void EnsureNotDisposed()
    {
        lock (_gate) _ = CurrentAccount();
    }

    private Account CurrentAccount() => _account ?? throw new ObjectDisposedException(nameof(DecimalTransactionSigner));
}
