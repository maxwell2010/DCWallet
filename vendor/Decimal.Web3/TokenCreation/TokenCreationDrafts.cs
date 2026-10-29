using System.Numerics;
using Decimal.Web3.Transactions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.MessageEncodingServices;

namespace Decimal.Web3.TokenCreation;

/// <summary>Offline token creation and metadata calldata builders. Amounts are raw integer units.</summary>
public sealed class TokenCreationDrafts
{
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - 1;
    private static readonly BigInteger WeiPerDel = BigInteger.Pow(10, 18);
    private readonly string _tokenCenter;

    public TokenCreationDrafts(string tokenCenter)
    {
        TransactionDraft.ValidateAddress(tokenCenter, nameof(tokenCenter));
        _tokenCenter = tokenCenter;
    }

    public TransactionDraft BuildCreateTokenDraft(string from, string name, string symbol,
        BigInteger initialMintRaw, BigInteger minTotalSupplyRaw, BigInteger maxTotalSupplyRaw,
        byte crr, string identity, BigInteger? reserveValueWei = null, string? creator = null)
    {
        TransactionDraft.ValidateAddress(from, nameof(from));
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(identity);
        ValidateSymbol(symbol);
        ValidateUint256(initialMintRaw, nameof(initialMintRaw));
        ValidateUint256(minTotalSupplyRaw, nameof(minTotalSupplyRaw));
        ValidateUint256(maxTotalSupplyRaw, nameof(maxTotalSupplyRaw));
        var resolvedCreator = creator ?? from;
        TransactionDraft.ValidateAddress(resolvedCreator, nameof(creator));
        var valueWei = reserveValueWei ?? RequiredReserveWei(symbol);
        ValidateUint256(valueWei, nameof(reserveValueWei));

        return Build(from, _tokenCenter, new CreateTokenFunction
        {
            Meta = new TokenCreationMeta
            {
                InitialMint = initialMintRaw, MinTotalSupply = minTotalSupplyRaw,
                MaxTotalSupply = maxTotalSupplyRaw, Creator = resolvedCreator,
                Crr = crr, Identity = identity, Symbol = symbol, Name = name
            }
        }, valueWei);
    }

    public TransactionDraft BuildCreateReservelessTokenDraft(string from, string name, string symbol,
        bool mintable, bool burnable, BigInteger initialMintRaw, BigInteger capRaw, string identity)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(identity);
        ValidateSymbol(symbol);
        ValidateUint256(initialMintRaw, nameof(initialMintRaw));
        ValidateUint256(capRaw, nameof(capRaw));
        return Build(from, _tokenCenter, new CreateTokenReservelessFunction
        {
            Name = name, Symbol = symbol, Mintable = mintable, Burnable = burnable,
            InitialMint = initialMintRaw, Cap = capRaw, Identity = identity
        });
    }

    public TransactionDraft BuildUpdateDetailsDraft(string from, string token,
        string identity, BigInteger maxTotalSupplyRaw)
    {
        TransactionDraft.ValidateAddress(token, nameof(token));
        ArgumentNullException.ThrowIfNull(identity);
        ValidateUint256(maxTotalSupplyRaw, nameof(maxTotalSupplyRaw));
        return Build(from, token, new UpdateTokenDetailsFunction
        {
            NewIdentity = identity, NewMaxTotalSupply = maxTotalSupplyRaw
        });
    }

    private static BigInteger RequiredReserveWei(string symbol)
    {
        var commissionDel = symbol.Trim().Length switch
        {
            3 => 2_500_000,
            4 => 250_000,
            5 => 25_000,
            6 => 2_500,
            _ => 250
        };
        return (1_000 + commissionDel) * WeiPerDel;
    }

    private static void ValidateSymbol(string symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Token symbol is required", nameof(symbol));
    }

    private static void ValidateUint256(BigInteger value, string parameter)
    {
        if (value < 0 || value > MaxUint256)
            throw new ArgumentOutOfRangeException(parameter, "Value must fit uint256");
    }

    private static TransactionDraft Build<TFunction>(string from, string contract, TFunction function,
        BigInteger valueWei = default) where TFunction : FunctionMessage, new()
    {
        TransactionDraft.ValidateAddress(from, nameof(from));
        var encoder = new FunctionMessageEncodingService<TFunction>(contract, from);
        var data = "0x" + Convert.ToHexString(encoder.GetCallData(function)).ToLowerInvariant();
        return TransactionDraft.ContractCall(from, contract, data, valueWei);
    }
}

// ABI: decimalteam/go-smart-node contracts/tokenCenter/DecimalTokenCenter.json at 9e6c6d7.
[Struct("Token")]
public sealed class TokenCreationMeta
{
    [Parameter("uint256", "initialMint", 1)] public BigInteger InitialMint { get; set; }
    [Parameter("uint256", "minTotalSupply", 2)] public BigInteger MinTotalSupply { get; set; }
    [Parameter("uint256", "maxTotalSupply", 3)] public BigInteger MaxTotalSupply { get; set; }
    [Parameter("address", "creator", 4)] public string Creator { get; set; } = string.Empty;
    [Parameter("uint8", "crr", 5)] public byte Crr { get; set; }
    [Parameter("string", "identity", 6)] public string Identity { get; set; } = string.Empty;
    [Parameter("string", "symbol", 7)] public string Symbol { get; set; } = string.Empty;
    [Parameter("string", "name", 8)] public string Name { get; set; } = string.Empty;
}

[Function("createToken")]
public sealed class CreateTokenFunction : FunctionMessage
{
    [Parameter("tuple", "meta", 1)] public TokenCreationMeta Meta { get; set; } = new();
}

[Function("createTokenReserveless")]
public sealed class CreateTokenReservelessFunction : FunctionMessage
{
    [Parameter("string", "name", 1)] public string Name { get; set; } = string.Empty;
    [Parameter("string", "symbol", 2)] public string Symbol { get; set; } = string.Empty;
    [Parameter("bool", "mintable", 3)] public bool Mintable { get; set; }
    [Parameter("bool", "burnable", 4)] public bool Burnable { get; set; }
    [Parameter("uint256", "initialMint", 5)] public BigInteger InitialMint { get; set; }
    [Parameter("uint256", "cap", 6)] public BigInteger Cap { get; set; }
    [Parameter("string", "identity", 7)] public string Identity { get; set; } = string.Empty;
}

// ABI: decimalteam/go-smart-node contracts/token/DecimalToken.json at 9e6c6d7.
[Function("updateDetails")]
public sealed class UpdateTokenDetailsFunction : FunctionMessage
{
    [Parameter("string", "newIdentity", 1)] public string NewIdentity { get; set; } = string.Empty;
    [Parameter("uint256", "newMaxTotalSupply", 2)] public BigInteger NewMaxTotalSupply { get; set; }
}
