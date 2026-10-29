using System.Numerics;
using Decimal.Web3.Transactions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.MessageEncodingServices;

namespace Decimal.Web3.TokenEconomy;

/// <summary>Offline token-economy calldata builders. Amounts are raw integer units; no calls are sent.</summary>
public sealed class TokenEconomyDrafts
{
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - 1;
    private readonly string _tokenCenter;

    /// <param name="tokenCenter">Explicit TokenCenter address for the target network.</param>
    public TokenEconomyDrafts(string tokenCenter)
    {
        TransactionDraft.ValidateAddress(tokenCenter, nameof(tokenCenter));
        _tokenCenter = tokenCenter;
    }

    public TransactionDraft BuildBuyDraft(string from, string token, BigInteger valueWei,
        BigInteger amountOutMinRaw, string recipient)
    {
        ValidateTokenAndRecipient(token, recipient);
        ValidateUint256(valueWei, nameof(valueWei));
        ValidateUint256(amountOutMinRaw, nameof(amountOutMinRaw));
        return Build(from, token, new BuyFunction { AmountOutMin = amountOutMinRaw, Recipient = recipient }, valueWei);
    }

    public TransactionDraft BuildSellDraft(string from, string token, BigInteger amountInRaw,
        BigInteger amountOutMinWei, string recipient)
    {
        ValidateTokenAndRecipient(token, recipient);
        ValidateUint256(amountInRaw, nameof(amountInRaw));
        ValidateUint256(amountOutMinWei, nameof(amountOutMinWei));
        return Build(from, token, new SellFunction
        {
            AmountIn = amountInRaw, AmountOutMin = amountOutMinWei, Recipient = recipient
        });
    }

    /// <summary>Spend an exact DEL amount while enforcing a minimum token output.</summary>
    public TransactionDraft BuildBuyTokenForExactDelDraft(string from, string token,
        BigInteger valueWei, BigInteger amountOutMinRaw, string recipient)
    {
        ValidateTokenAndRecipient(token, recipient);
        ValidatePositiveUint256(valueWei, nameof(valueWei));
        ValidateUint256(amountOutMinRaw, nameof(amountOutMinRaw));
        return Build(from, token, new BuyTokenForExactDelFunction
        {
            AmountOutMin = amountOutMinRaw, Recipient = recipient
        }, valueWei);
    }

    /// <summary>Buy an exact token amount, capped by the supplied DEL value.</summary>
    public TransactionDraft BuildBuyExactTokenForDelDraft(string from, string token,
        BigInteger amountOutRaw, BigInteger maxValueWei, string recipient)
    {
        ValidateTokenAndRecipient(token, recipient);
        ValidatePositiveUint256(amountOutRaw, nameof(amountOutRaw));
        ValidatePositiveUint256(maxValueWei, nameof(maxValueWei));
        return Build(from, token, new BuyExactTokenForDelFunction
        {
            AmountOut = amountOutRaw, Recipient = recipient
        }, maxValueWei);
    }

    /// <summary>Sell an exact token amount while enforcing a minimum DEL output.</summary>
    public TransactionDraft BuildSellExactTokensForDelDraft(string from, string token,
        BigInteger amountInRaw, BigInteger amountOutMinWei, string recipient)
    {
        ValidateTokenAndRecipient(token, recipient);
        ValidatePositiveUint256(amountInRaw, nameof(amountInRaw));
        ValidateUint256(amountOutMinWei, nameof(amountOutMinWei));
        return Build(from, token, new SellExactTokensForDelFunction
        {
            AmountIn = amountInRaw, AmountOutMin = amountOutMinWei, Recipient = recipient
        });
    }

    /// <summary>Receive an exact DEL amount while capping the tokens sold.</summary>
    public TransactionDraft BuildSellTokensForExactDelDraft(string from, string token,
        BigInteger amountOutWei, BigInteger amountInMaxRaw, string recipient)
    {
        ValidateTokenAndRecipient(token, recipient);
        ValidatePositiveUint256(amountOutWei, nameof(amountOutWei));
        ValidatePositiveUint256(amountInMaxRaw, nameof(amountInMaxRaw));
        return Build(from, token, new SellTokensForExactDelFunction
        {
            AmountOut = amountOutWei, AmountInMax = amountInMaxRaw, Recipient = recipient
        });
    }

    public TransactionDraft BuildConvertDraft(string from, string tokenIn, string tokenOut,
        BigInteger amountInRaw, BigInteger amountOutMinRaw, string recipient)
    {
        ValidateConversion(tokenIn, tokenOut, amountInRaw, amountOutMinRaw, recipient);
        return Build(from, _tokenCenter, new ConvertFunction
        {
            TokenIn = tokenIn, TokenOut = tokenOut, AmountIn = amountInRaw,
            AmountOutMin = amountOutMinRaw, Recipient = recipient
        });
    }

    public TransactionDraft BuildConvertWithPermitDraft(string from, string tokenIn, string tokenOut,
        BigInteger amountInRaw, BigInteger amountOutMinRaw, string recipient,
        BigInteger deadline, byte v, byte[] r, byte[] s)
    {
        ValidateConversion(tokenIn, tokenOut, amountInRaw, amountOutMinRaw, recipient);
        ValidateUint256(deadline, nameof(deadline));
        ValidateBytes32(r, nameof(r));
        ValidateBytes32(s, nameof(s));
        return Build(from, _tokenCenter, new ConvertWithPermitFunction
        {
            TokenIn = tokenIn, TokenOut = tokenOut, AmountIn = amountInRaw,
            AmountOutMin = amountOutMinRaw, Recipient = recipient, Deadline = deadline,
            V = v, R = (byte[])r.Clone(), S = (byte[])s.Clone()
        });
    }

    public TransactionDraft BuildBurnDraft(string from, string token, BigInteger amountRaw)
    {
        TransactionDraft.ValidateAddress(token, nameof(token));
        ValidateUint256(amountRaw, nameof(amountRaw));
        return Build(from, token, new BurnFunction { Value = amountRaw });
    }

    public TransactionDraft BuildUpdateDetailsDraft(string from, string token, string newIdentity,
        BigInteger newMaxTotalSupplyRaw)
    {
        TransactionDraft.ValidateAddress(token, nameof(token));
        ArgumentNullException.ThrowIfNull(newIdentity);
        ValidateUint256(newMaxTotalSupplyRaw, nameof(newMaxTotalSupplyRaw));
        return Build(from, token, new UpdateDetailsFunction
        {
            NewIdentity = newIdentity, NewMaxTotalSupply = newMaxTotalSupplyRaw
        });
    }

    /// <summary>The pinned DecimalToken ABI does not expose mint(address,uint256).</summary>
    public TransactionDraft BuildMintDraft(string from, string token, string to, BigInteger amountRaw) =>
        throw new NotSupportedException("mint(address,uint256) is absent from DecimalToken ABI at go-smart-node 9e6c6d7");

    /// <summary>Mint a reservless token that exposes mint(address,uint256). Verify the token type and minting role before signing.</summary>
    public TransactionDraft BuildMintReservelessDraft(string from, string token, string to, BigInteger amountRaw)
    {
        ValidateTokenAndRecipient(token, to);
        ValidateUint256(amountRaw, nameof(amountRaw));
        if (amountRaw == BigInteger.Zero)
            throw new ArgumentOutOfRangeException(nameof(amountRaw), "Mint amount must be positive");
        return Build(from, token, new MintReservelessFunction { Recipient = to, Amount = amountRaw });
    }

    private static TransactionDraft Build<TFunction>(string from, string contract, TFunction function,
        BigInteger valueWei = default) where TFunction : FunctionMessage, new()
    {
        TransactionDraft.ValidateAddress(from, nameof(from));
        var encoder = new FunctionMessageEncodingService<TFunction>(contract, from);
        var data = "0x" + Convert.ToHexString(encoder.GetCallData(function)).ToLowerInvariant();
        return TransactionDraft.ContractCall(from, contract, data, valueWei);
    }

    private static void ValidateTokenAndRecipient(string token, string recipient)
    {
        TransactionDraft.ValidateAddress(token, nameof(token));
        TransactionDraft.ValidateAddress(recipient, nameof(recipient));
    }

    private static void ValidateConversion(string tokenIn, string tokenOut,
        BigInteger amountInRaw, BigInteger amountOutMinRaw, string recipient)
    {
        TransactionDraft.ValidateAddress(tokenIn, nameof(tokenIn));
        TransactionDraft.ValidateAddress(tokenOut, nameof(tokenOut));
        TransactionDraft.ValidateAddress(recipient, nameof(recipient));
        if (string.Equals(tokenIn, tokenOut, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Input and output tokens must differ", nameof(tokenOut));
        ValidateUint256(amountInRaw, nameof(amountInRaw));
        ValidateUint256(amountOutMinRaw, nameof(amountOutMinRaw));
    }

    private static void ValidateUint256(BigInteger value, string parameter)
    {
        if (value < 0 || value > MaxUint256)
            throw new ArgumentOutOfRangeException(parameter, "Value must fit uint256");
    }

    private static void ValidatePositiveUint256(BigInteger value, string parameter)
    {
        ValidateUint256(value, parameter);
        if (value.IsZero)
            throw new ArgumentOutOfRangeException(parameter, "Amount must be positive");
    }

    private static void ValidateBytes32(byte[] value, string parameter)
    {
        ArgumentNullException.ThrowIfNull(value, parameter);
        if (value.Length != 32)
            throw new ArgumentException("Expected 32 bytes", parameter);
    }
}

// Signatures and order: decimalteam/go-smart-node contracts/token and tokenCenter ABI at 9e6c6d7.
[Function("buy")]
public sealed class BuyFunction : FunctionMessage
{
    [Parameter("uint256", "amountOutMin", 1)] public BigInteger AmountOutMin { get; set; }
    [Parameter("address", "recipient", 2)] public string Recipient { get; set; } = string.Empty;
}

[Function("buyTokenForExactDEL")]
public sealed class BuyTokenForExactDelFunction : FunctionMessage
{
    [Parameter("uint256", "amountOutMin", 1)] public BigInteger AmountOutMin { get; set; }
    [Parameter("address", "recipient", 2)] public string Recipient { get; set; } = string.Empty;
}

[Function("buyExactTokenForDEL")]
public sealed class BuyExactTokenForDelFunction : FunctionMessage
{
    [Parameter("uint256", "amountOut", 1)] public BigInteger AmountOut { get; set; }
    [Parameter("address", "recipient", 2)] public string Recipient { get; set; } = string.Empty;
}

[Function("sell")]
public sealed class SellFunction : FunctionMessage
{
    [Parameter("uint256", "amountIn", 1)] public BigInteger AmountIn { get; set; }
    [Parameter("uint256", "amountOutMin", 2)] public BigInteger AmountOutMin { get; set; }
    [Parameter("address", "recipient", 3)] public string Recipient { get; set; } = string.Empty;
}

[Function("sellExactTokensForDEL")]
public sealed class SellExactTokensForDelFunction : FunctionMessage
{
    [Parameter("uint256", "amountIn", 1)] public BigInteger AmountIn { get; set; }
    [Parameter("uint256", "amountOutMin", 2)] public BigInteger AmountOutMin { get; set; }
    [Parameter("address", "recipient", 3)] public string Recipient { get; set; } = string.Empty;
}

[Function("sellTokensForExactDEL")]
public sealed class SellTokensForExactDelFunction : FunctionMessage
{
    [Parameter("uint256", "amountOut", 1)] public BigInteger AmountOut { get; set; }
    [Parameter("uint256", "amountInMax", 2)] public BigInteger AmountInMax { get; set; }
    [Parameter("address", "recipient", 3)] public string Recipient { get; set; } = string.Empty;
}

[Function("burn")]
public sealed class BurnFunction : FunctionMessage
{
    [Parameter("uint256", "value", 1)] public BigInteger Value { get; set; }
}

[Function("updateDetails")]
public sealed class UpdateDetailsFunction : FunctionMessage
{
    [Parameter("string", "newIdentity", 1)] public string NewIdentity { get; set; } = string.Empty;
    [Parameter("uint256", "newMaxTotalSupply", 2)] public BigInteger NewMaxTotalSupply { get; set; }
}

// Supported by the deployed reservless token and the JS SDK, but not by the reservable DecimalToken ABI.
[Function("mint")]
public sealed class MintReservelessFunction : FunctionMessage
{
    [Parameter("address", "recipient", 1)] public string Recipient { get; set; } = string.Empty;
    [Parameter("uint256", "amount", 2)] public BigInteger Amount { get; set; }
}

[Function("convert")]
public sealed class ConvertFunction : FunctionMessage
{
    [Parameter("address", "tokenIn", 1)] public string TokenIn { get; set; } = string.Empty;
    [Parameter("address", "tokenOut", 2)] public string TokenOut { get; set; } = string.Empty;
    [Parameter("uint256", "amountIn", 3)] public BigInteger AmountIn { get; set; }
    [Parameter("uint256", "amountOutMin", 4)] public BigInteger AmountOutMin { get; set; }
    [Parameter("address", "recipient", 5)] public string Recipient { get; set; } = string.Empty;
}

[Function("convert")]
public sealed class ConvertWithPermitFunction : FunctionMessage
{
    [Parameter("address", "tokenIn", 1)] public string TokenIn { get; set; } = string.Empty;
    [Parameter("address", "tokenOut", 2)] public string TokenOut { get; set; } = string.Empty;
    [Parameter("uint256", "amountIn", 3)] public BigInteger AmountIn { get; set; }
    [Parameter("uint256", "amountOutMin", 4)] public BigInteger AmountOutMin { get; set; }
    [Parameter("address", "recipient", 5)] public string Recipient { get; set; } = string.Empty;
    [Parameter("uint256", "deadline", 6)] public BigInteger Deadline { get; set; }
    [Parameter("uint8", "v", 7)] public byte V { get; set; }
    [Parameter("bytes32", "r", 8)] public byte[] R { get; set; } = new byte[32];
    [Parameter("bytes32", "s", 9)] public byte[] S { get; set; } = new byte[32];
}
