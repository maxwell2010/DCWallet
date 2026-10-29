using System.Numerics;
using System.Text;
using Decimal.Web3.Multisend;
using Decimal.Web3.Transactions;
using Decimal.Web3.Wallet;
using Nethereum.Util;

namespace Decimal.Web3.Tokens;

public sealed record Erc20PermitData(
    string TokenAddress, string TokenName, string TokenVersion,
    string Owner, string Spender, BigInteger ValueRaw,
    BigInteger Nonce, BigInteger Deadline, BigInteger ChainId);

/// <summary>Strict EIP-2612 permit preparation and local signing for compatible ERC20 tokens.</summary>
public sealed class Erc20PermitService
{
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - BigInteger.One;
    private static readonly byte[] DomainTypeHash = Keccak("EIP712Domain(string name,string version,uint256 chainId,address verifyingContract)");
    private static readonly byte[] PermitTypeHash = Keccak("Permit(address owner,address spender,uint256 value,uint256 nonce,uint256 deadline)");
    private readonly DecimalClient _client;
    private readonly Erc20Service _token;

    public Erc20PermitService(DecimalClient client, string tokenAddress)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _token = new Erc20Service(client, tokenAddress);
    }

    public async Task<Erc20PermitData> PrepareAsync(
        string owner, string spender, BigInteger valueRaw, BigInteger deadline)
    {
        TransactionDraft.ValidateAddress(owner, nameof(owner));
        TransactionDraft.ValidateAddress(spender, nameof(spender));
        RequireUint(valueRaw, nameof(valueRaw));
        RequireUint(deadline, nameof(deadline));
        if (valueRaw == 0) throw new ArgumentOutOfRangeException(nameof(valueRaw));
        if (deadline <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            throw new ArgumentOutOfRangeException(nameof(deadline), "Permit deadline must be in the future");

        var actualChain = (await _client.Web3.Eth.ChainId.SendRequestAsync().ConfigureAwait(false)).Value;
        if (actualChain != _client.Network.ChainId)
            throw new InvalidOperationException("RPC chain ID does not match the selected Decimal network");
        var nameTask = _token.GetNameAsync();
        var nonceTask = _token.GetNonceAsync(owner);
        var domainTask = _token.GetDomainSeparatorAsync();
        await Task.WhenAll(nameTask, nonceTask, domainTask).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(nameTask.Result) || domainTask.Result is not { Length: 32 })
            throw new NotSupportedException("Token does not expose a valid EIP-2612 domain");
        var version = "1";
        try
        {
            version = await _token.GetVersionAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // OpenZeppelin ERC20Permit commonly uses version 1 without exposing version().
        }
        var data = new Erc20PermitData(_token.TokenAddress, nameTask.Result, version,
            owner, spender, valueRaw, nonceTask.Result, deadline, actualChain);
        var domain = CalculateDomainSeparator(data);
        if (!domain.AsSpan().SequenceEqual(domainTask.Result))
            throw new NotSupportedException("Token EIP-712 domain does not match its on-chain DOMAIN_SEPARATOR");
        return data;
    }

    public async Task<Erc20PermitSignature> SignAsync(
        string spender, BigInteger valueRaw, BigInteger deadline, DecimalTransactionSigner signer)
    {
        ArgumentNullException.ThrowIfNull(signer);
        signer.EnsureNotDisposed();
        if (signer.ChainId != _client.Network.ChainId)
            throw new InvalidOperationException("Signer chain ID does not match the selected Decimal network");
        var data = await PrepareAsync(signer.Address, spender, valueRaw, deadline).ConfigureAwait(false);
        var rawHex = signer.SignDigest(CalculateDigest(data));
        var raw = Convert.FromHexString(rawHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? rawHex[2..] : rawHex);
        if (raw.Length != 65 || raw[64] is not (27 or 28))
            throw new InvalidOperationException("Signer returned an invalid EIP-712 signature");
        return new Erc20PermitSignature(deadline, raw[64], raw[..32], raw[32..64]);
    }

    public static byte[] CalculateDomainSeparator(Erc20PermitData data)
    {
        Validate(data);
        return Keccak(Combine(DomainTypeHash, Keccak(Encoding.UTF8.GetBytes(data.TokenName)),
            Keccak(Encoding.UTF8.GetBytes(data.TokenVersion)), Word(data.ChainId), Word(data.TokenAddress)));
    }

    public static byte[] CalculateDigest(Erc20PermitData data)
    {
        Validate(data);
        var structHash = Keccak(Combine(PermitTypeHash, Word(data.Owner), Word(data.Spender),
            Word(data.ValueRaw), Word(data.Nonce), Word(data.Deadline)));
        return Keccak(Combine([0x19, 0x01], CalculateDomainSeparator(data), structHash));
    }

    private static void Validate(Erc20PermitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        TransactionDraft.ValidateAddress(data.TokenAddress, nameof(data));
        TransactionDraft.ValidateAddress(data.Owner, nameof(data));
        TransactionDraft.ValidateAddress(data.Spender, nameof(data));
        if (string.IsNullOrWhiteSpace(data.TokenName) || string.IsNullOrWhiteSpace(data.TokenVersion))
            throw new ArgumentException("Permit domain requires token name and version", nameof(data));
        RequireUint(data.ValueRaw, nameof(data));
        RequireUint(data.Nonce, nameof(data));
        RequireUint(data.Deadline, nameof(data));
        RequireUint(data.ChainId, nameof(data));
    }

    private static void RequireUint(BigInteger value, string name)
    {
        if (value < 0 || value > MaxUint256) throw new ArgumentOutOfRangeException(name);
    }

    private static byte[] Word(BigInteger value)
    {
        RequireUint(value, nameof(value));
        var result = new byte[32];
        var raw = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        raw.CopyTo(result.AsSpan(32 - raw.Length));
        return result;
    }

    private static byte[] Word(string address)
    {
        var result = new byte[32];
        Convert.FromHexString(address[2..]).CopyTo(result, 12);
        return result;
    }

    private static byte[] Combine(params byte[][] parts)
    {
        var result = new byte[parts.Sum(part => part.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }
        return result;
    }

    private static byte[] Keccak(string value) => Keccak(Encoding.ASCII.GetBytes(value));
    private static byte[] Keccak(byte[] value) => Sha3Keccack.Current.CalculateHash(value);
}
