using System.Numerics;
using Decimal.Web3.Transactions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.MessageEncodingServices;

namespace Decimal.Web3.Staking;

/// <summary>Offline NFT delegation drafts for an explicitly supplied NFT delegation contract.</summary>
public sealed class NftDelegationDrafts
{
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - BigInteger.One;
    private readonly string _contract;

    public NftDelegationDrafts(string nftDelegationContract)
    {
        TransactionDraft.ValidateAddress(nftDelegationContract, nameof(nftDelegationContract));
        _contract = nftDelegationContract;
    }

    public TransactionDraft BuildDelegateDrc721Draft(string from, string validator, string nft, BigInteger tokenId)
    {
        ValidateNft(validator, nft, tokenId);
        return Build(from, new DelegateDrc721Function { Validator = validator, Nft = nft, TokenId = tokenId });
    }

    public TransactionDraft BuildDelegateDrc1155Draft(
        string from, string validator, string nft, BigInteger tokenId, BigInteger amount)
    {
        ValidateNft(validator, nft, tokenId);
        ValidateAmount(amount);
        return Build(from, new DelegateDrc1155Function
        {
            Validator = validator, Nft = nft, TokenId = tokenId, Amount = amount
        });
    }

    public TransactionDraft BuildDelegateHoldDrc721Draft(
        string from, string validator, string nft, BigInteger tokenId, BigInteger holdTimestamp)
    {
        ValidateNft(validator, nft, tokenId);
        ValidateUint256(holdTimestamp, nameof(holdTimestamp));
        return Build(from, new DelegateHoldDrc721Function
        {
            Validator = validator, Nft = nft, TokenId = tokenId, HoldTimestamp = holdTimestamp
        });
    }

    public TransactionDraft BuildDelegateHoldDrc1155Draft(
        string from, string validator, string nft, BigInteger tokenId, BigInteger amount, BigInteger holdTimestamp)
    {
        ValidateNft(validator, nft, tokenId);
        ValidateAmount(amount);
        ValidateUint256(holdTimestamp, nameof(holdTimestamp));
        return Build(from, new DelegateHoldDrc1155Function
        {
            Validator = validator, Nft = nft, TokenId = tokenId, Amount = amount,
            HoldTimestamp = holdTimestamp
        });
    }

    public TransactionDraft BuildWithdrawDraft(
        string from, string validator, string nft, BigInteger tokenId, BigInteger amount)
    {
        ValidateNft(validator, nft, tokenId);
        ValidateAmount(amount);
        return Build(from, new WithdrawNftFunction
        {
            Validator = validator, Nft = nft, TokenId = tokenId, Amount = amount
        });
    }

    public TransactionDraft BuildWithdrawHoldDraft(
        string from, string validator, string nft, BigInteger tokenId, BigInteger amount, BigInteger holdTimestamp)
    {
        ValidateNft(validator, nft, tokenId);
        ValidateAmount(amount);
        ValidateUint256(holdTimestamp, nameof(holdTimestamp));
        return Build(from, new WithdrawHoldNftFunction
        {
            Validator = validator, Nft = nft, TokenId = tokenId, Amount = amount,
            HoldTimestamp = holdTimestamp
        });
    }

    public TransactionDraft BuildTransferDraft(
        string from, string validator, string nft, BigInteger tokenId, BigInteger amount, string target)
    {
        ValidateNft(validator, nft, tokenId);
        ValidateAmount(amount);
        TransactionDraft.ValidateAddress(target, nameof(target));
        return Build(from, new TransferNftFunction
        {
            Validator = validator, Nft = nft, TokenId = tokenId, Amount = amount, Target = target
        });
    }

    public TransactionDraft BuildTransferHoldDraft(
        string from, string validator, string nft, BigInteger tokenId, BigInteger amount,
        BigInteger holdTimestamp, string target)
    {
        ValidateNft(validator, nft, tokenId);
        ValidateAmount(amount);
        ValidateUint256(holdTimestamp, nameof(holdTimestamp));
        TransactionDraft.ValidateAddress(target, nameof(target));
        return Build(from, new TransferHoldNftFunction
        {
            Validator = validator, Nft = nft, TokenId = tokenId, Amount = amount,
            HoldTimestamp = holdTimestamp, Target = target
        });
    }

    private TransactionDraft Build<TFunction>(string from, TFunction function)
        where TFunction : FunctionMessage, new()
    {
        TransactionDraft.ValidateAddress(from, nameof(from));
        var encoder = new FunctionMessageEncodingService<TFunction>(_contract, from);
        var data = "0x" + Convert.ToHexString(encoder.GetCallData(function)).ToLowerInvariant();
        return TransactionDraft.ContractCall(from, _contract, data);
    }

    private static void ValidateNft(string validator, string nft, BigInteger tokenId)
    {
        TransactionDraft.ValidateAddress(validator, nameof(validator));
        TransactionDraft.ValidateAddress(nft, nameof(nft));
        ValidateUint256(tokenId, nameof(tokenId));
    }

    private static void ValidateAmount(BigInteger amount)
    {
        ValidateUint256(amount, nameof(amount));
        if (amount.IsZero) throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive");
    }

    private static void ValidateUint256(BigInteger value, string parameter)
    {
        if (value < BigInteger.Zero || value > MaxUint256)
            throw new ArgumentOutOfRangeException(parameter, "Value must fit uint256");
    }
}

// Local pinned NFT delegation ABI: profiles/candy_mainnet_v1.json, abi.nftDelegation.
[Function("delegateDRC721")]
public sealed class DelegateDrc721Function : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "nft", 2)] public string Nft { get; set; } = string.Empty;
    [Parameter("uint256", "tokenId", 3)] public BigInteger TokenId { get; set; }
}

[Function("delegateDRC1155")]
public sealed class DelegateDrc1155Function : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "nft", 2)] public string Nft { get; set; } = string.Empty;
    [Parameter("uint256", "tokenId", 3)] public BigInteger TokenId { get; set; }
    [Parameter("uint256", "amount", 4)] public BigInteger Amount { get; set; }
}

[Function("delegateHoldDRC721")]
public sealed class DelegateHoldDrc721Function : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "nft", 2)] public string Nft { get; set; } = string.Empty;
    [Parameter("uint256", "tokenId", 3)] public BigInteger TokenId { get; set; }
    [Parameter("uint256", "holdTimestamp", 4)] public BigInteger HoldTimestamp { get; set; }
}

[Function("delegateHoldDRC1155")]
public sealed class DelegateHoldDrc1155Function : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "nft", 2)] public string Nft { get; set; } = string.Empty;
    [Parameter("uint256", "tokenId", 3)] public BigInteger TokenId { get; set; }
    [Parameter("uint256", "amount", 4)] public BigInteger Amount { get; set; }
    [Parameter("uint256", "holdTimestamp", 5)] public BigInteger HoldTimestamp { get; set; }
}

[Function("withdraw")]
public sealed class WithdrawNftFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "nft", 2)] public string Nft { get; set; } = string.Empty;
    [Parameter("uint256", "tokenId", 3)] public BigInteger TokenId { get; set; }
    [Parameter("uint256", "amount", 4)] public BigInteger Amount { get; set; }
}

[Function("withdrawHold")]
public sealed class WithdrawHoldNftFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "nft", 2)] public string Nft { get; set; } = string.Empty;
    [Parameter("uint256", "tokenId", 3)] public BigInteger TokenId { get; set; }
    [Parameter("uint256", "amount", 4)] public BigInteger Amount { get; set; }
    [Parameter("uint256", "holdTimestamp", 5)] public BigInteger HoldTimestamp { get; set; }
}

[Function("transfer")]
public sealed class TransferNftFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "nft", 2)] public string Nft { get; set; } = string.Empty;
    [Parameter("uint256", "tokenId", 3)] public BigInteger TokenId { get; set; }
    [Parameter("uint256", "amount", 4)] public BigInteger Amount { get; set; }
    [Parameter("address", "target", 5)] public string Target { get; set; } = string.Empty;
}

[Function("transferHold")]
public sealed class TransferHoldNftFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "nft", 2)] public string Nft { get; set; } = string.Empty;
    [Parameter("uint256", "tokenId", 3)] public BigInteger TokenId { get; set; }
    [Parameter("uint256", "amount", 4)] public BigInteger Amount { get; set; }
    [Parameter("uint256", "holdTimestamp", 5)] public BigInteger HoldTimestamp { get; set; }
    [Parameter("address", "target", 6)] public string Target { get; set; } = string.Empty;
}
