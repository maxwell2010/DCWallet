using System;
using System.Collections.Generic;
using System.Numerics;
using NBitcoin;
using Nethereum.Signer;
using HdWallet = Nethereum.HdWallet.Wallet;

namespace Decimal.Web3.Wallet;

/// <summary>Local BIP39 and Decimal EVM BIP44 operations. No RPC calls are made.</summary>
public static class DecimalWallet
{
    public const string DerivationPathTemplate = "m/44'/60'/0'/0/{index}";

    public static string GenerateMnemonic(int wordCount = 12)
    {
        if (wordCount is not (12 or 15 or 18 or 21 or 24))
            throw new ArgumentOutOfRangeException(nameof(wordCount), "BIP39 requires 12, 15, 18, 21, or 24 words.");

        return new Mnemonic(Wordlist.English, (WordCount)wordCount).ToString();
    }

    public static bool IsValidMnemonic(string? mnemonic)
    {
        if (string.IsNullOrWhiteSpace(mnemonic)) return false;

        try
        {
            return new Mnemonic(mnemonic, Wordlist.English).IsValidChecksum;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static string DerivationPath(int index)
    {
        CheckIndex(index);
        return $"m/44'/60'/0'/0/{index}";
    }

    public static string DeriveAddress(string mnemonic, int index = 0, string passphrase = "") =>
        CreateWallet(mnemonic, passphrase).GetAccount(CheckedIndex(index)).Address;

    public static IReadOnlyList<string> DeriveAddresses(string mnemonic, string passphrase = "")
    {
        var wallet = CreateWallet(mnemonic, passphrase);
        var addresses = new string[10];
        for (var index = 0; index < addresses.Length; index++)
            addresses[index] = wallet.GetAccount(index).Address;
        return Array.AsReadOnly(addresses);
    }

    public static string SignPersonalMessage(string mnemonic, string message, int index = 0, string passphrase = "")
    {
        ArgumentNullException.ThrowIfNull(message);
        var key = CreateWallet(mnemonic, passphrase).GetEthereumKey(CheckedIndex(index));
        return new EthereumMessageSigner().EncodeUTF8AndSign(message, key);
    }

    /// <summary>Signs an already computed 32-byte digest without an EIP-191 prefix.</summary>
    public static string SignDigest(string mnemonic, byte[] digest, int index = 0, string passphrase = "")
    {
        ArgumentNullException.ThrowIfNull(digest);
        if (digest.Length != 32) throw new ArgumentException("Digest must be 32 bytes.", nameof(digest));
        var key = CreateWallet(mnemonic, passphrase).GetEthereumKey(CheckedIndex(index));
        return new MessageSigner().Sign(digest, key);
    }

    /// <summary>Derives the account once for repeated local transaction signing.</summary>
    public static DecimalTransactionSigner CreateTransactionSigner(
        string mnemonic, BigInteger chainId, int index = 0, string passphrase = "")
    {
        if (chainId <= 0) throw new ArgumentOutOfRangeException(nameof(chainId));
        var account = CreateWallet(mnemonic, passphrase).GetAccount(CheckedIndex(index), chainId);
        return new DecimalTransactionSigner(account, chainId);
    }

    /// <summary>Signs a fully specified EIP-155 legacy transaction locally; values are wei.</summary>
    public static string SignLegacyTransaction(
        string mnemonic, BigInteger chainId, BigInteger nonce, string to,
        BigInteger valueWei, BigInteger gasPriceWei, BigInteger gasLimit,
        string data = "0x", int index = 0, string passphrase = "")
    {
        using var signer = CreateTransactionSigner(mnemonic, chainId, index, passphrase);
        return signer.SignLegacyTransaction(nonce, to, valueWei, gasPriceWei, gasLimit, data);
    }

    /// <summary>Signs a fully specified EIP-1559 transaction locally; values are wei.</summary>
    public static string SignEip1559Transaction(
        string mnemonic, BigInteger chainId, BigInteger nonce, string to,
        BigInteger valueWei, BigInteger maxPriorityFeePerGasWei,
        BigInteger maxFeePerGasWei, BigInteger gasLimit,
        string data = "0x", int index = 0, string passphrase = "")
    {
        using var signer = CreateTransactionSigner(mnemonic, chainId, index, passphrase);
        return signer.SignEip1559Transaction(nonce, to, valueWei,
            maxPriorityFeePerGasWei, maxFeePerGasWei, gasLimit, data);
    }

    private static HdWallet CreateWallet(string mnemonic, string passphrase)
    {
        if (!IsValidMnemonic(mnemonic)) throw new ArgumentException("Invalid English BIP39 mnemonic.", nameof(mnemonic));
        ArgumentNullException.ThrowIfNull(passphrase);
        return new HdWallet(mnemonic.Trim(), passphrase, HdWallet.DEFAULT_PATH);
    }

    internal static void ValidateTransactionFields(string to, string data)
    {
        if (to is null || to.Length != 42 || !to.StartsWith("0x", StringComparison.Ordinal) ||
            !AllHex(to.AsSpan(2)))
            throw new ArgumentException("Recipient must be a 20-byte hex address.", nameof(to));
        if (data is null || data.Length < 2 || data.Length % 2 != 0 ||
            !data.StartsWith("0x", StringComparison.Ordinal) || !AllHex(data.AsSpan(2)))
            throw new ArgumentException("Data must be an even-length hex string.", nameof(data));
    }

    private static bool AllHex(ReadOnlySpan<char> text)
    {
        foreach (var character in text)
            if (!Uri.IsHexDigit(character)) return false;
        return true;
    }

    private static int CheckedIndex(int index)
    {
        CheckIndex(index);
        return index;
    }

    private static void CheckIndex(int index)
    {
        if (index is < 0 or > 9) throw new ArgumentOutOfRangeException(nameof(index), "Index must be 0 through 9.");
    }
}
