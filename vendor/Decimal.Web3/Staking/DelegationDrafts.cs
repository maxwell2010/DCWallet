using System.Numerics;
using Decimal.Web3.Transactions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.MessageEncodingServices;

namespace Decimal.Web3.Staking;

/// <summary>Offline delegation transaction drafts. No approvals or transactions are submitted.</summary>
public sealed class DelegationDrafts
{
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - BigInteger.One;
    private readonly string _contract;

    public DelegationDrafts(DecimalNetwork network, string? delegationContract = null)
    {
        ArgumentNullException.ThrowIfNull(network);
        if (network.ChainId == 202020 && delegationContract is null)
            throw new ArgumentException("Testnet delegation address must be supplied explicitly", nameof(delegationContract));
        var address = delegationContract ?? network.DelegationContract;
        TransactionDraft.ValidateAddress(address, delegationContract is null ? nameof(network) : nameof(delegationContract));
        _contract = address;
    }

    public TransactionDraft BuildDelegateDelDraft(string from, string validator, string amountDel)
    {
        TransactionDraft.ValidateAddress(validator, nameof(validator));
        var value = Units.Parse(amountDel);
        ValidateUint256(value, nameof(amountDel));
        return Build(from, new DelegateDelFunction { Validator = validator }, value);
    }

    public TransactionDraft BuildDelegateHoldDelDraft(
        string from, string validator, string amountDel, BigInteger holdTimestamp)
    {
        TransactionDraft.ValidateAddress(validator, nameof(validator));
        var value = Units.Parse(amountDel);
        ValidateUint256(value, nameof(amountDel));
        ValidateUint256(holdTimestamp, nameof(holdTimestamp));
        return Build(from, new DelegateHoldDelFunction
        {
            Validator = validator, HoldTimestamp = holdTimestamp
        }, value);
    }

    public TransactionDraft BuildDelegateErc20Draft(string from, string validator, string token, BigInteger amountRaw)
    {
        ValidateStake(validator, token, amountRaw);
        return Build(from, new DelegateStakeFunction { Validator = validator, Token = token, Amount = amountRaw });
    }

    public TransactionDraft BuildDelegateHoldDraft(
        string from, string validator, string token, BigInteger amountRaw, BigInteger holdTimestamp)
    {
        ValidateStake(validator, token, amountRaw);
        ValidateUint256(holdTimestamp, nameof(holdTimestamp));
        return Build(from, new DelegateHoldFunction
        {
            Validator = validator, Token = token, Amount = amountRaw, HoldTimestamp = holdTimestamp
        });
    }

    public TransactionDraft BuildDelegateByPermitDraft(
        string from, string validator, string token, BigInteger amountRaw,
        BigInteger deadline, byte v, byte[] r, byte[] s)
    {
        ValidateStake(validator, token, amountRaw);
        ValidatePermit(deadline, r, s);
        return Build(from, new DelegateByPermitFunction
        {
            Validator = validator, Token = token, Amount = amountRaw, Deadline = deadline,
            V = v, R = (byte[])r.Clone(), S = (byte[])s.Clone()
        });
    }

    public TransactionDraft BuildDelegateHoldByPermitDraft(
        string from, string validator, string token, BigInteger amountRaw, BigInteger holdTimestamp,
        BigInteger deadline, byte v, byte[] r, byte[] s)
    {
        ValidateStake(validator, token, amountRaw);
        ValidateUint256(holdTimestamp, nameof(holdTimestamp));
        ValidatePermit(deadline, r, s);
        return Build(from, new DelegateHoldByPermitFunction
        {
            Validator = validator, Token = token, Amount = amountRaw, HoldTimestamp = holdTimestamp,
            Deadline = deadline, V = v, R = (byte[])r.Clone(), S = (byte[])s.Clone()
        });
    }

    public TransactionDraft BuildUnbondDelDraft(
        string from, string validator, string wrappedDelToken, string amountDel) =>
        BuildWithdrawDraft(from, validator, wrappedDelToken, Units.Parse(amountDel));

    public TransactionDraft BuildWithdrawDraft(
        string from, string validator, string token, BigInteger amountRaw)
    {
        ValidateStake(validator, token, amountRaw);
        return Build(from, new WithdrawFunction
        {
            Validator = validator, Token = token, Amount = amountRaw
        });
    }

    public TransactionDraft BuildWithdrawDraft(
        string from, string validator, string token, string amount, int decimals) =>
        BuildWithdrawDraft(from, validator, token, Units.Parse(amount, decimals));

    public TransactionDraft BuildWithdrawHoldDraft(
        string from, string validator, string token, BigInteger amountRaw, BigInteger holdTimestamp)
    {
        ValidateStake(validator, token, amountRaw);
        ValidateUint256(holdTimestamp, nameof(holdTimestamp));
        return Build(from, new WithdrawHoldFunction
        {
            Validator = validator, Token = token, Amount = amountRaw, HoldTimestamp = holdTimestamp
        });
    }

    public TransactionDraft BuildTransferDraft(
        string from, string validator, string token, BigInteger amountRaw, string newValidator)
    {
        ValidateStake(validator, token, amountRaw);
        TransactionDraft.ValidateAddress(newValidator, nameof(newValidator));
        return Build(from, new TransferStakeFunction
        {
            Validator = validator, Token = token, Amount = amountRaw, NewValidator = newValidator
        });
    }

    public TransactionDraft BuildTransferHoldDraft(
        string from, string validator, string token, BigInteger amountRaw,
        BigInteger holdTimestamp, string newValidator)
    {
        ValidateStake(validator, token, amountRaw);
        ValidateUint256(holdTimestamp, nameof(holdTimestamp));
        TransactionDraft.ValidateAddress(newValidator, nameof(newValidator));
        return Build(from, new TransferHoldFunction
        {
            Validator = validator, Token = token, Amount = amountRaw,
            HoldTimestamp = holdTimestamp, NewValidator = newValidator
        });
    }

    public TransactionDraft BuildHoldDraft(
        string from, string validator, string token, BigInteger amountRaw,
        BigInteger oldHoldTimestamp, BigInteger newHoldTimestamp)
    {
        ValidateStake(validator, token, amountRaw);
        ValidateUint256(oldHoldTimestamp, nameof(oldHoldTimestamp));
        ValidateUint256(newHoldTimestamp, nameof(newHoldTimestamp));
        return Build(from, new HoldStakeFunction
        {
            Validator = validator, Token = token, Amount = amountRaw,
            OldHoldTimestamp = oldHoldTimestamp, NewHoldTimestamp = newHoldTimestamp
        });
    }

    public TransactionDraft BuildResetHoldDraft(
        string from, string validator, string delegator, string token, BigInteger holdTimestamp)
    {
        TransactionDraft.ValidateAddress(validator, nameof(validator));
        TransactionDraft.ValidateAddress(delegator, nameof(delegator));
        TransactionDraft.ValidateAddress(token, nameof(token));
        ValidateUint256(holdTimestamp, nameof(holdTimestamp));
        return Build(from, new ResetHoldFunction
        {
            Validator = validator, Delegator = delegator, Token = token, HoldTimestamp = holdTimestamp
        });
    }

    public TransactionDraft BuildResetHoldsDraft(
        string from, string validator, string delegator, string token,
        IReadOnlyList<BigInteger> holdTimestamps)
    {
        TransactionDraft.ValidateAddress(validator, nameof(validator));
        TransactionDraft.ValidateAddress(delegator, nameof(delegator));
        TransactionDraft.ValidateAddress(token, nameof(token));
        return Build(from, new ResetHoldsFunction
        {
            Validator = validator, Delegator = delegator, Token = token,
            HoldTimestamps = CopyTimestamps(holdTimestamps, nameof(holdTimestamps))
        });
    }

    public TransactionDraft BuildWithdrawWithResetDraft(
        string from, string validator, string token, BigInteger amountRaw,
        IReadOnlyList<BigInteger> holdTimestampsToReset)
    {
        ValidateStake(validator, token, amountRaw);
        return Build(from, new WithdrawWithResetFunction
        {
            Validator = validator, Token = token, Amount = amountRaw,
            HoldTimestampsToReset = CopyTimestamps(holdTimestampsToReset, nameof(holdTimestampsToReset))
        });
    }

    public TransactionDraft BuildWithdrawDelWithResetDraft(
        string from, string validator, string wrappedDelToken, string amountDel,
        IReadOnlyList<BigInteger> holdTimestampsToReset) =>
        BuildWithdrawWithResetDraft(from, validator, wrappedDelToken, Units.Parse(amountDel), holdTimestampsToReset);

    /// <summary>Draft completion of previously frozen stake indexes. Maturity must be checked on chain.</summary>
    public TransactionDraft BuildCompleteDraft(string from, IReadOnlyList<BigInteger> frozenStakeIndexes)
    {
        ArgumentNullException.ThrowIfNull(frozenStakeIndexes);
        if (frozenStakeIndexes.Count == 0)
            throw new ArgumentException("At least one frozen stake index is required", nameof(frozenStakeIndexes));
        var indexes = CopyTimestamps(frozenStakeIndexes, nameof(frozenStakeIndexes));
        if (indexes.Distinct().Count() != indexes.Count)
            throw new ArgumentException("Frozen stake indexes must be unique", nameof(frozenStakeIndexes));
        return Build(from, new CompleteStakeFunction { Indexes = indexes });
    }

    public TransactionDraft BuildTransferWithResetDraft(
        string from, string oldValidator, string token, BigInteger amountRaw,
        string newValidator, IReadOnlyList<BigInteger> holdTimestampsToReset)
    {
        ValidateStake(oldValidator, token, amountRaw);
        TransactionDraft.ValidateAddress(newValidator, nameof(newValidator));
        return Build(from, new TransferWithResetFunction
        {
            OldValidator = oldValidator, Token = token, Amount = amountRaw,
            NewValidator = newValidator,
            HoldTimestampsToReset = CopyTimestamps(holdTimestampsToReset, nameof(holdTimestampsToReset))
        });
    }

    public TransactionDraft BuildTransferDelWithResetDraft(
        string from, string oldValidator, string wrappedDelToken, string amountDel,
        string newValidator, IReadOnlyList<BigInteger> holdTimestampsToReset) =>
        BuildTransferWithResetDraft(from, oldValidator, wrappedDelToken, Units.Parse(amountDel),
            newValidator, holdTimestampsToReset);

    public TransactionDraft BuildHoldWithResetDraft(
        string from, string validator, string token, BigInteger amountRaw,
        BigInteger newHoldTimestamp, IReadOnlyList<BigInteger> holdTimestampsToReset)
    {
        ValidateStake(validator, token, amountRaw);
        ValidateUint256(newHoldTimestamp, nameof(newHoldTimestamp));
        return Build(from, new HoldWithResetFunction
        {
            Validator = validator, Token = token, Amount = amountRaw,
            NewHoldTimestamp = newHoldTimestamp,
            HoldTimestampsToReset = CopyTimestamps(holdTimestampsToReset, nameof(holdTimestampsToReset))
        });
    }

    private TransactionDraft Build<TFunction>(string from, TFunction function, BigInteger valueWei = default)
        where TFunction : FunctionMessage, new()
    {
        TransactionDraft.ValidateAddress(from, nameof(from));
        var encoder = new FunctionMessageEncodingService<TFunction>(_contract, from);
        var data = "0x" + Convert.ToHexString(encoder.GetCallData(function)).ToLowerInvariant();
        return TransactionDraft.ContractCall(from, _contract, data, valueWei);
    }

    private static void ValidateStake(string validator, string token, BigInteger amountRaw)
    {
        TransactionDraft.ValidateAddress(validator, nameof(validator));
        TransactionDraft.ValidateAddress(token, nameof(token));
        ValidateUint256(amountRaw, nameof(amountRaw));
    }

    private static List<BigInteger> CopyTimestamps(IReadOnlyList<BigInteger> values, string parameter)
    {
        ArgumentNullException.ThrowIfNull(values, parameter);
        var copy = new List<BigInteger>(values.Count);
        foreach (var value in values)
        {
            ValidateUint256(value, parameter);
            copy.Add(value);
        }
        return copy;
    }

    private static void ValidatePermit(BigInteger deadline, byte[] r, byte[] s)
    {
        ValidateUint256(deadline, nameof(deadline));
        ValidateBytes32(r, nameof(r));
        ValidateBytes32(s, nameof(s));
    }

    private static void ValidateBytes32(byte[] value, string parameter)
    {
        ArgumentNullException.ThrowIfNull(value, parameter);
        if (value.Length != 32)
            throw new ArgumentException("Expected 32 bytes", parameter);
    }

    private static void ValidateUint256(BigInteger value, string parameter)
    {
        if (value < BigInteger.Zero || value > MaxUint256)
            throw new ArgumentOutOfRangeException(parameter, "Value must fit uint256");
    }
}

[Function("delegateDEL")]
public sealed class DelegateDelFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
}

[Function("delegateHoldDEL")]
public sealed class DelegateHoldDelFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("uint256", "holdTimestamp", 2)] public BigInteger HoldTimestamp { get; set; }
}

// Signatures and order: go-smart-node contracts/delegation/DecimalDelegation.json at 9e6c6d7.
[Function("delegate")]
public sealed class DelegateStakeFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "token", 2)] public string Token { get; set; } = string.Empty;
    [Parameter("uint256", "amount", 3)] public BigInteger Amount { get; set; }
}

[Function("delegateHold")]
public sealed class DelegateHoldFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "token", 2)] public string Token { get; set; } = string.Empty;
    [Parameter("uint256", "amount", 3)] public BigInteger Amount { get; set; }
    [Parameter("uint256", "holdTimestamp", 4)] public BigInteger HoldTimestamp { get; set; }
}

[Function("delegateByPermit")]
public sealed class DelegateByPermitFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "token", 2)] public string Token { get; set; } = string.Empty;
    [Parameter("uint256", "amount", 3)] public BigInteger Amount { get; set; }
    [Parameter("uint256", "deadline", 4)] public BigInteger Deadline { get; set; }
    [Parameter("uint8", "v", 5)] public byte V { get; set; }
    [Parameter("bytes32", "r", 6)] public byte[] R { get; set; } = new byte[32];
    [Parameter("bytes32", "s", 7)] public byte[] S { get; set; } = new byte[32];
}

[Function("delegateHoldByPermit")]
public sealed class DelegateHoldByPermitFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "token", 2)] public string Token { get; set; } = string.Empty;
    [Parameter("uint256", "amount", 3)] public BigInteger Amount { get; set; }
    [Parameter("uint256", "holdTimestamp", 4)] public BigInteger HoldTimestamp { get; set; }
    [Parameter("uint256", "deadline", 5)] public BigInteger Deadline { get; set; }
    [Parameter("uint8", "v", 6)] public byte V { get; set; }
    [Parameter("bytes32", "r", 7)] public byte[] R { get; set; } = new byte[32];
    [Parameter("bytes32", "s", 8)] public byte[] S { get; set; } = new byte[32];
}

[Function("withdraw")]
public sealed class WithdrawFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "token", 2)] public string Token { get; set; } = string.Empty;
    [Parameter("uint256", "amount", 3)] public BigInteger Amount { get; set; }
}

[Function("withdrawHold")]
public sealed class WithdrawHoldFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "token", 2)] public string Token { get; set; } = string.Empty;
    [Parameter("uint256", "amount", 3)] public BigInteger Amount { get; set; }
    [Parameter("uint256", "holdTimestamp", 4)] public BigInteger HoldTimestamp { get; set; }
}

[Function("transfer")]
public sealed class TransferStakeFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "token", 2)] public string Token { get; set; } = string.Empty;
    [Parameter("uint256", "amount", 3)] public BigInteger Amount { get; set; }
    [Parameter("address", "newValidator", 4)] public string NewValidator { get; set; } = string.Empty;
}

[Function("transferHold")]
public sealed class TransferHoldFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "token", 2)] public string Token { get; set; } = string.Empty;
    [Parameter("uint256", "amount", 3)] public BigInteger Amount { get; set; }
    [Parameter("uint256", "holdTimestamp", 4)] public BigInteger HoldTimestamp { get; set; }
    [Parameter("address", "newValidator", 5)] public string NewValidator { get; set; } = string.Empty;
}

[Function("hold")]
public sealed class HoldStakeFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "token", 2)] public string Token { get; set; } = string.Empty;
    [Parameter("uint256", "amount", 3)] public BigInteger Amount { get; set; }
    [Parameter("uint256", "oldHoldTimestamp", 4)] public BigInteger OldHoldTimestamp { get; set; }
    [Parameter("uint256", "newHoldTimestamp", 5)] public BigInteger NewHoldTimestamp { get; set; }
}

[Function("resetHold")]
public sealed class ResetHoldFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "delegator", 2)] public string Delegator { get; set; } = string.Empty;
    [Parameter("address", "token", 3)] public string Token { get; set; } = string.Empty;
    [Parameter("uint256", "holdTimestamp", 4)] public BigInteger HoldTimestamp { get; set; }
}

[Function("resetHolds")]
public sealed class ResetHoldsFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "delegator", 2)] public string Delegator { get; set; } = string.Empty;
    [Parameter("address", "token", 3)] public string Token { get; set; } = string.Empty;
    [Parameter("uint256[]", "holdTimestamps", 4)] public List<BigInteger> HoldTimestamps { get; set; } = new();
}

[Function("withdrawWithReset")]
public sealed class WithdrawWithResetFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "token", 2)] public string Token { get; set; } = string.Empty;
    [Parameter("uint256", "amount", 3)] public BigInteger Amount { get; set; }
    [Parameter("uint256[]", "holdTimestampsToReset", 4)]
    public List<BigInteger> HoldTimestampsToReset { get; set; } = new();
}

[Function("complete")]
public sealed class CompleteStakeFunction : FunctionMessage
{
    [Parameter("uint256[]", "indexes", 1)]
    public List<BigInteger> Indexes { get; set; } = new();
}

[Function("transferWithReset")]
public sealed class TransferWithResetFunction : FunctionMessage
{
    [Parameter("address", "oldValidator", 1)] public string OldValidator { get; set; } = string.Empty;
    [Parameter("address", "token", 2)] public string Token { get; set; } = string.Empty;
    [Parameter("uint256", "amount", 3)] public BigInteger Amount { get; set; }
    [Parameter("address", "newValidator", 4)] public string NewValidator { get; set; } = string.Empty;
    [Parameter("uint256[]", "holdTimestampsToReset", 5)]
    public List<BigInteger> HoldTimestampsToReset { get; set; } = new();
}

[Function("holdWithReset")]
public sealed class HoldWithResetFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "token", 2)] public string Token { get; set; } = string.Empty;
    [Parameter("uint256", "amount", 3)] public BigInteger Amount { get; set; }
    [Parameter("uint256", "newHoldTimestamp", 4)] public BigInteger NewHoldTimestamp { get; set; }
    [Parameter("uint256[]", "holdTimestampsToReset", 5)]
    public List<BigInteger> HoldTimestampsToReset { get; set; } = new();
}
