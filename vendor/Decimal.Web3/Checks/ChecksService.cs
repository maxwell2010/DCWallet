using System.Numerics;
using System.Text;
using Decimal.Web3.Transactions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.MessageEncodingServices;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

namespace Decimal.Web3.Checks;

/// <summary>Permit fields supplied by the caller. This type does not create or sign a permit.</summary>
public sealed record ChecksPermit(BigInteger Deadline, byte V, byte[] R, byte[] S);

/// <summary>Python checks.py ABI reads and unsigned drafts for an explicitly supplied contract.</summary>
public sealed class ChecksService
{
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - BigInteger.One;
    private static readonly BigInteger Secp256k1Order = BigInteger.Parse(
        "0FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEBAAEDCE6AF48A03BBFD25E8CD0364141",
        System.Globalization.NumberStyles.HexNumber);
    private static readonly BigInteger Secp256k1HalfOrder = Secp256k1Order >> 1;
    private readonly Nethereum.Web3.Web3 _web3;
    private readonly string _contract;
    private static readonly byte[] PersonalMessagePrefix = Encoding.ASCII.GetBytes("\u0019Ethereum Signed Message:\n32");

    public ChecksService(Nethereum.Web3.Web3 web3, string contract)
    {
        _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
        TransactionDraft.ValidateAddress(contract, nameof(contract));
        _contract = contract;
    }

    public Task<BigInteger> GetNonceAsync(BlockParameter? block = null) =>
        _web3.Eth.GetContractQueryHandler<ChecksNoncesFunction>().QueryAsync<BigInteger>(
            _contract, new ChecksNoncesFunction(), block ?? BlockParameter.CreateLatest());

    public Task<string> GetOwnerAsync(BlockParameter? block = null) =>
        _web3.Eth.GetContractQueryHandler<ChecksOwnerFunction>().QueryAsync<string>(
            _contract, new ChecksOwnerFunction(), block ?? BlockParameter.CreateLatest());

    public Task<string> GetImplementationAsync(BlockParameter? block = null) =>
        _web3.Eth.GetContractQueryHandler<ChecksImplementationFunction>().QueryAsync<string>(
            _contract, new ChecksImplementationFunction(), block ?? BlockParameter.CreateLatest());

    /// <summary>Hash of Solidity packed (bytes32 checkHash, address redeemer).</summary>
    public static byte[] CalculateRedeemMessageHash(byte[] checkHash, string redeemer)
    {
        var hash = CopyBytes32(checkHash, nameof(checkHash));
        TransactionDraft.ValidateAddress(redeemer, nameof(redeemer));
        var packed = new byte[52];
        hash.CopyTo(packed, 0);
        Convert.FromHexString(redeemer[2..]).CopyTo(packed, 32);
        return Sha3Keccack.Current.CalculateHash(packed);
    }

    /// <summary>EIP-191 digest signed by the check signer for redemption.</summary>
    public static byte[] CalculateRedeemSigningDigest(byte[] checkHash, string redeemer)
    {
        var messageHash = CalculateRedeemMessageHash(checkHash, redeemer);
        var message = new byte[PersonalMessagePrefix.Length + messageHash.Length];
        PersonalMessagePrefix.CopyTo(message, 0);
        messageHash.CopyTo(message, PersonalMessagePrefix.Length);
        return Sha3Keccack.Current.CalculateHash(message);
    }

    public TransactionDraft BuildCreateDelDraft(
        string from, IReadOnlyList<string> signers, BigInteger amountWei, BigInteger dueBlock, BigInteger nonce)
    {
        var addresses = CopySigners(signers);
        RequireNonEmpty(addresses.Count, nameof(signers));
        RequireUint(amountWei, nameof(amountWei));
        RequireUint(dueBlock, nameof(dueBlock));
        RequireUint(nonce, nameof(nonce));
        var valueWei = addresses.Count * amountWei;
        RequireUint(valueWei, nameof(amountWei));
        return Encode(from, new CreateChecksDelFunction
        {
            Signers = addresses, Amount = amountWei, DueBlock = dueBlock, CheckNonce = nonce
        }, valueWei);
    }

    public TransactionDraft BuildCreateTokenDraft(
        string from, IReadOnlyList<string> signers, BigInteger amountRaw, BigInteger dueBlock,
        BigInteger nonce, string token)
    {
        var addresses = CopySigners(signers);
        RequireNonEmpty(addresses.Count, nameof(signers));
        RequireUint(amountRaw, nameof(amountRaw));
        RequireUint(dueBlock, nameof(dueBlock));
        RequireUint(nonce, nameof(nonce));
        TransactionDraft.ValidateAddress(token, nameof(token));
        return Encode(from, new CreateChecksTokenFunction
        {
            Signers = addresses, Amount = amountRaw, DueBlock = dueBlock, CheckNonce = nonce, Token = token
        });
    }

    public TransactionDraft BuildCreateTokenByPermitDraft(
        string from, IReadOnlyList<string> signers, BigInteger amountRaw, BigInteger dueBlock,
        BigInteger nonce, string token, ChecksPermit permit)
    {
        ArgumentNullException.ThrowIfNull(permit);
        var addresses = CopySigners(signers);
        RequireNonEmpty(addresses.Count, nameof(signers));
        RequireUint(amountRaw, nameof(amountRaw));
        RequireUint(dueBlock, nameof(dueBlock));
        RequireUint(nonce, nameof(nonce));
        RequireUint(permit.Deadline, nameof(permit));
        TransactionDraft.ValidateAddress(token, nameof(token));
        ValidateSignature(permit.V, permit.R, permit.S, nameof(permit));
        return Encode(from, new CreateChecksTokenByPermitFunction
        {
            Signers = addresses, Amount = amountRaw, DueBlock = dueBlock, CheckNonce = nonce, Token = token,
            Deadline = permit.Deadline, V = permit.V,
            R = CopyBytes32(permit.R, nameof(permit)), S = CopyBytes32(permit.S, nameof(permit))
        });
    }

    public TransactionDraft BuildRedeemDraft(
        string from, IReadOnlyList<byte[]> signatures, IReadOnlyList<byte[]> checks)
    {
        ArgumentNullException.ThrowIfNull(signatures);
        ArgumentNullException.ThrowIfNull(checks);
        RequireNonEmpty(checks.Count, nameof(checks));
        if (signatures.Count != checks.Count)
            throw new ArgumentException("Each check requires one signature", nameof(signatures));
        var signatureCopies = new List<byte[]>(signatures.Count);
        foreach (var signature in signatures)
        {
            ArgumentNullException.ThrowIfNull(signature);
            if (signature.Length != 65)
                throw new ArgumentException("Each signature must be 65 bytes", nameof(signatures));
            ValidateSignature(signature[64], signature[..32], signature[32..64], nameof(signatures));
            signatureCopies.Add((byte[])signature.Clone());
        }
        var checkCopies = new List<byte[]>(checks.Count);
        foreach (var check in checks)
            checkCopies.Add(CopyBytes32(check, nameof(checks)));
        return Encode(from, new RedeemChecksFunction { Signatures = signatureCopies, Checks = checkCopies });
    }

    private TransactionDraft Encode<T>(string from, T function, BigInteger valueWei = default)
        where T : FunctionMessage, new()
    {
        TransactionDraft.ValidateAddress(from, nameof(from));
        var encoder = new FunctionMessageEncodingService<T>(_contract, from);
        var data = "0x" + Convert.ToHexString(encoder.GetCallData(function)).ToLowerInvariant();
        return TransactionDraft.ContractCall(from, _contract, data, valueWei);
    }

    private static List<string> CopySigners(IReadOnlyList<string> signers)
    {
        ArgumentNullException.ThrowIfNull(signers);
        var result = new List<string>(signers.Count);
        foreach (var signer in signers)
        {
            TransactionDraft.ValidateAddress(signer, nameof(signers));
            result.Add(signer);
        }
        return result;
    }

    private static byte[] CopyBytes32(byte[] value, string parameter)
    {
        if (value is null || value.Length != 32)
            throw new ArgumentException("Expected exactly 32 bytes", parameter);
        return (byte[])value.Clone();
    }

    private static void RequireUint(BigInteger value, string parameter)
    {
        if (value < BigInteger.Zero || value > MaxUint256)
            throw new ArgumentOutOfRangeException(parameter, "Value must fit uint256");
    }

    private static void RequireNonEmpty(int count, string parameter)
    {
        if (count == 0) throw new ArgumentException("At least one item is required", parameter);
    }

    private static void ValidateSignature(byte v, byte[] r, byte[] s, string parameter)
    {
        if (v is not (27 or 28) || r is null || s is null || r.Length != 32 || s.Length != 32)
            throw new ArgumentException("Expected a 65-byte ECDSA signature with v 27 or 28", parameter);
        var rValue = new BigInteger(r, isUnsigned: true, isBigEndian: true);
        var sValue = new BigInteger(s, isUnsigned: true, isBigEndian: true);
        if (rValue < 1 || rValue >= Secp256k1Order || sValue < 1 || sValue > Secp256k1HalfOrder)
            throw new ArgumentException("ECDSA signature values are outside the canonical range", parameter);
    }
}

[Function("nonces", "uint256")]
public sealed class ChecksNoncesFunction : FunctionMessage;

[Function("owner", "address")]
public sealed class ChecksOwnerFunction : FunctionMessage;

[Function("getImpl", "address")]
public sealed class ChecksImplementationFunction : FunctionMessage;

[Function("createChecksDEL", "bytes32[]")]
public sealed class CreateChecksDelFunction : FunctionMessage
{
    [Parameter("address[]", "signers", 1)] public List<string> Signers { get; set; } = [];
    [Parameter("uint256", "amount", 2)] public BigInteger Amount { get; set; }
    [Parameter("uint256", "dueBlock", 3)] public BigInteger DueBlock { get; set; }
    [Parameter("uint256", "nonce", 4)] public BigInteger CheckNonce { get; set; }
}

[Function("createChecksToken", "bytes32[]")]
public sealed class CreateChecksTokenFunction : FunctionMessage
{
    [Parameter("address[]", "signers", 1)] public List<string> Signers { get; set; } = [];
    [Parameter("uint256", "amount", 2)] public BigInteger Amount { get; set; }
    [Parameter("uint256", "dueBlock", 3)] public BigInteger DueBlock { get; set; }
    [Parameter("uint256", "nonce", 4)] public BigInteger CheckNonce { get; set; }
    [Parameter("address", "token", 5)] public string Token { get; set; } = string.Empty;
}

[Function("createChecksTokenByPermit", "bytes32[]")]
public sealed class CreateChecksTokenByPermitFunction : FunctionMessage
{
    [Parameter("address[]", "signers", 1)] public List<string> Signers { get; set; } = [];
    [Parameter("uint256", "amount", 2)] public BigInteger Amount { get; set; }
    [Parameter("uint256", "dueBlock", 3)] public BigInteger DueBlock { get; set; }
    [Parameter("uint256", "nonce", 4)] public BigInteger CheckNonce { get; set; }
    [Parameter("address", "token", 5)] public string Token { get; set; } = string.Empty;
    [Parameter("uint256", "deadline", 6)] public BigInteger Deadline { get; set; }
    [Parameter("uint8", "v", 7)] public byte V { get; set; }
    [Parameter("bytes32", "r", 8)] public byte[] R { get; set; } = new byte[32];
    [Parameter("bytes32", "s", 9)] public byte[] S { get; set; } = new byte[32];
}

[Function("redeemChecks")]
public sealed class RedeemChecksFunction : FunctionMessage
{
    [Parameter("bytes[]", "signatures", 1)] public List<byte[]> Signatures { get; set; } = [];
    [Parameter("bytes32[]", "checks", 2)] public List<byte[]> Checks { get; set; } = [];
}
