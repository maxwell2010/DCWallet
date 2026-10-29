using System.Numerics;
using Decimal.Web3.Transactions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.MessageEncodingServices;

namespace Decimal.Web3.Nft;

/// <summary>Offline Decimal NFT calls. Staking requires a separate NFT operator approval.</summary>
public sealed class DecimalNftDrafts
{
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - 1;
    private readonly string _nftCenter;
    private readonly string? _delegationNft;

    public DecimalNftDrafts(DecimalNetwork network, string? nftCenterContract = null,
        string? delegationNftContract = null)
    {
        ArgumentNullException.ThrowIfNull(network);
        _nftCenter = nftCenterContract ?? network.NftCenterContract;
        TransactionDraft.ValidateAddress(_nftCenter, nftCenterContract is null ? nameof(network) : nameof(nftCenterContract));
        if (delegationNftContract is not null)
            TransactionDraft.ValidateAddress(delegationNftContract, nameof(delegationNftContract));
        _delegationNft = delegationNftContract;
    }

    public TransactionDraft BuildCreateErc721Draft(string from, string creator, string symbol,
        string name, string contractUri, bool refundable) =>
        Create(from, creator, symbol, name, contractUri, refundable, true);

    public TransactionDraft BuildCreateErc1155Draft(string from, string creator, string symbol,
        string name, string contractUri, bool refundable) =>
        Create(from, creator, symbol, name, contractUri, refundable, false);

    /// <summary>Older deployed DRC721 creation ABI observed on testnet; do not confuse with createERC721.</summary>
    public TransactionDraft BuildCreateDrc721LegacyDraft(string from, string creator, string symbol,
        string name, string contractUri, bool allowMint, bool burnable) =>
        CreateLegacy(from, creator, symbol, name, contractUri, allowMint, burnable, true);

    /// <summary>Older deployed DRC1155 creation ABI observed on testnet; do not confuse with createERC1155.</summary>
    public TransactionDraft BuildCreateDrc1155LegacyDraft(string from, string creator, string symbol,
        string name, string contractUri, bool allowMint, bool burnable) =>
        CreateLegacy(from, creator, symbol, name, contractUri, allowMint, burnable, false);

    public TransactionDraft BuildMintErc721Draft(string from, string collection, string recipient,
        string tokenUri, BigInteger reserveAmountRaw, string reserveToken)
    {
        ValidateMint(collection, recipient, tokenUri);
        Uint(reserveAmountRaw, nameof(reserveAmountRaw));
        TransactionDraft.ValidateAddress(reserveToken, nameof(reserveToken));
        return Build(from, collection, new MintDrc721Function
        {
            Recipient = recipient, TokenUri = tokenUri,
            ReserveAmount = reserveAmountRaw, ReserveToken = reserveToken
        });
    }

    public TransactionDraft BuildMintErc721ByEthDraft(string from, string collection,
        string recipient, string tokenUri, BigInteger valueWei)
    {
        ValidateMint(collection, recipient, tokenUri);
        PositiveUint(valueWei, nameof(valueWei));
        return Build(from, collection, new MintDrc721ByEthFunction
        {
            Recipient = recipient, TokenUri = tokenUri
        }, valueWei);
    }

    /// <summary>Mint without reserve using the current NFT ABI's zero reserve tuple.</summary>
    public TransactionDraft BuildMintReservelessErc721Draft(string from, string collection,
        string recipient, string tokenUri) =>
        BuildMintErc721Draft(from, collection, recipient, tokenUri, BigInteger.Zero,
            "0x0000000000000000000000000000000000000000");

    public TransactionDraft BuildMintErc1155Draft(string from, string collection, string recipient,
        BigInteger tokenId, BigInteger amountToMint, string tokenUri,
        BigInteger reserveAmountRaw, string reserveToken)
    {
        ValidateMint(collection, recipient, tokenUri);
        Uint(tokenId, nameof(tokenId));
        PositiveUint(amountToMint, nameof(amountToMint));
        Uint(reserveAmountRaw, nameof(reserveAmountRaw));
        TransactionDraft.ValidateAddress(reserveToken, nameof(reserveToken));
        return Build(from, collection, new MintDrc1155Function
        {
            Recipient = recipient, TokenId = tokenId, AmountToMint = amountToMint,
            TokenUri = tokenUri, ReserveAmount = reserveAmountRaw, ReserveToken = reserveToken
        });
    }

    public TransactionDraft BuildMintErc1155ByEthDraft(string from, string collection,
        string recipient, BigInteger tokenId, BigInteger amountToMint, string tokenUri,
        BigInteger valueWei)
    {
        ValidateMint(collection, recipient, tokenUri);
        Uint(tokenId, nameof(tokenId));
        PositiveUint(amountToMint, nameof(amountToMint));
        PositiveUint(valueWei, nameof(valueWei));
        return Build(from, collection, new MintDrc1155ByEthFunction
        {
            Recipient = recipient, TokenId = tokenId, AmountToMint = amountToMint, TokenUri = tokenUri
        }, valueWei);
    }

    /// <summary>Mint without reserve using the current NFT ABI's zero reserve tuple.</summary>
    public TransactionDraft BuildMintReservelessErc1155Draft(string from, string collection,
        string recipient, BigInteger tokenId, BigInteger amountToMint, string tokenUri) =>
        BuildMintErc1155Draft(from, collection, recipient, tokenId, amountToMint, tokenUri,
            BigInteger.Zero, "0x0000000000000000000000000000000000000000");

    public TransactionDraft BuildBurnErc721Draft(string from, string collection, BigInteger tokenId)
    {
        TransactionDraft.ValidateAddress(collection, nameof(collection));
        Uint(tokenId, nameof(tokenId));
        return Build(from, collection, new BurnDrc721Function { TokenId = tokenId });
    }

    public TransactionDraft BuildBurnErc1155Draft(string from, string collection,
        BigInteger tokenId, BigInteger amount)
    {
        TransactionDraft.ValidateAddress(collection, nameof(collection));
        Uint(tokenId, nameof(tokenId));
        PositiveUint(amount, nameof(amount));
        return Build(from, collection, new BurnDrc1155Function { TokenId = tokenId, Amount = amount });
    }

    public TransactionDraft BuildDelegateErc721Draft(string from, string validator,
        string collection, BigInteger tokenId)
    {
        ValidateStake(validator, collection, tokenId);
        return Build(from, DelegationNft(), new DelegateDrc721Function
        {
            Validator = validator, Nft = collection, TokenId = tokenId
        });
    }

    public TransactionDraft BuildDelegateErc1155Draft(string from, string validator,
        string collection, BigInteger tokenId, BigInteger amount)
    {
        ValidateStake(validator, collection, tokenId);
        PositiveUint(amount, nameof(amount));
        return Build(from, DelegationNft(), new DelegateDrc1155Function
        {
            Validator = validator, Nft = collection, TokenId = tokenId, Amount = amount
        });
    }

    public TransactionDraft BuildDelegateHoldErc721Draft(string from, string validator,
        string collection, BigInteger tokenId, BigInteger holdTimestamp)
    {
        ValidateStake(validator, collection, tokenId);
        Uint(holdTimestamp, nameof(holdTimestamp));
        return Build(from, DelegationNft(), new DelegateHoldDrc721Function
        {
            Validator = validator, Nft = collection, TokenId = tokenId, HoldTimestamp = holdTimestamp
        });
    }

    public TransactionDraft BuildDelegateHoldErc1155Draft(string from, string validator,
        string collection, BigInteger tokenId, BigInteger amount, BigInteger holdTimestamp)
    {
        ValidateStake(validator, collection, tokenId);
        PositiveUint(amount, nameof(amount));
        Uint(holdTimestamp, nameof(holdTimestamp));
        return Build(from, DelegationNft(), new DelegateHoldDrc1155Function
        {
            Validator = validator, Nft = collection, TokenId = tokenId,
            Amount = amount, HoldTimestamp = holdTimestamp
        });
    }

    public TransactionDraft BuildWithdrawDraft(string from, string validator, string collection,
        BigInteger tokenId, BigInteger amount)
    {
        ValidateStake(validator, collection, tokenId);
        PositiveUint(amount, nameof(amount));
        return Build(from, DelegationNft(), new WithdrawNftFunction
        {
            Validator = validator, Nft = collection, TokenId = tokenId, Amount = amount
        });
    }

    public TransactionDraft BuildWithdrawHoldDraft(string from, string validator, string collection,
        BigInteger tokenId, BigInteger amount, BigInteger holdTimestamp)
    {
        ValidateStake(validator, collection, tokenId);
        PositiveUint(amount, nameof(amount));
        Uint(holdTimestamp, nameof(holdTimestamp));
        return Build(from, DelegationNft(), new WithdrawHoldNftFunction
        {
            Validator = validator, Nft = collection, TokenId = tokenId,
            Amount = amount, HoldTimestamp = holdTimestamp
        });
    }

    public TransactionDraft BuildTransferStakeDraft(string from, string validator, string collection,
        BigInteger tokenId, BigInteger amount, string newValidator)
    {
        ValidateStake(validator, collection, tokenId);
        PositiveUint(amount, nameof(amount));
        TransactionDraft.ValidateAddress(newValidator, nameof(newValidator));
        return Build(from, DelegationNft(), new TransferNftStakeFunction
        {
            Validator = validator, Nft = collection, TokenId = tokenId,
            Amount = amount, NewValidator = newValidator
        });
    }

    public TransactionDraft BuildTransferHoldStakeDraft(string from, string validator, string collection,
        BigInteger tokenId, BigInteger amount, BigInteger holdTimestamp, string newValidator)
    {
        ValidateStake(validator, collection, tokenId);
        PositiveUint(amount, nameof(amount));
        Uint(holdTimestamp, nameof(holdTimestamp));
        TransactionDraft.ValidateAddress(newValidator, nameof(newValidator));
        return Build(from, DelegationNft(), new TransferHoldNftStakeFunction
        {
            Validator = validator, Nft = collection, TokenId = tokenId,
            Amount = amount, HoldTimestamp = holdTimestamp, NewValidator = newValidator
        });
    }

    public TransactionDraft BuildTransferErc721Draft(string from, string collection,
        string recipient, BigInteger tokenId)
    {
        TransactionDraft.ValidateAddress(collection, nameof(collection));
        TransactionDraft.ValidateAddress(recipient, nameof(recipient));
        Uint(tokenId, nameof(tokenId));
        return Build(from, collection, new SafeTransferDrc721Function
        {
            Owner = from, Recipient = recipient, TokenId = tokenId
        });
    }

    public TransactionDraft BuildTransferErc1155Draft(string from, string collection,
        string recipient, BigInteger tokenId, BigInteger amount, byte[] data)
    {
        TransactionDraft.ValidateAddress(collection, nameof(collection));
        TransactionDraft.ValidateAddress(recipient, nameof(recipient));
        Uint(tokenId, nameof(tokenId));
        PositiveUint(amount, nameof(amount));
        ArgumentNullException.ThrowIfNull(data);
        return Build(from, collection, new SafeTransferDrc1155Function
        {
            Owner = from, Recipient = recipient, TokenId = tokenId, Amount = amount, Data = data
        });
    }

    public TransactionDraft BuildAddDelReserveDraft(string from, string collection,
        BigInteger tokenId, BigInteger valueWei, bool allowUnverifiedAbi = false)
    {
        TransactionDraft.ValidateAddress(collection, nameof(collection));
        Uint(tokenId, nameof(tokenId));
        PositiveUint(valueWei, nameof(valueWei));
        if (!allowUnverifiedAbi)
            throw new NotSupportedException("addReserveByDEL is not in the pinned official NFT ABI");
        return Build(from, collection, new AddDelReserveNftFunction { TokenId = tokenId }, valueWei);
    }

    /// <summary>Current NFT ABI uses addReserve(uint256,uint256) for token reserve units.</summary>
    public TransactionDraft BuildAddReserveDraft(string from, string collection,
        BigInteger tokenId, BigInteger amountRaw)
    {
        TransactionDraft.ValidateAddress(collection, nameof(collection));
        Uint(tokenId, nameof(tokenId));
        PositiveUint(amountRaw, nameof(amountRaw));
        return Build(from, collection, new AddReserveNftFunction
        {
            TokenId = tokenId, AmountRaw = amountRaw
        });
    }

    /// <summary>Current upstream ABI uses addReserveByETH(uint256) with payable wei.</summary>
    public TransactionDraft BuildAddEthReserveDraft(string from, string collection,
        BigInteger tokenId, BigInteger valueWei)
    {
        TransactionDraft.ValidateAddress(collection, nameof(collection));
        Uint(tokenId, nameof(tokenId));
        PositiveUint(valueWei, nameof(valueWei));
        return Build(from, collection, new AddEthReserveNftFunction { TokenId = tokenId }, valueWei);
    }

    public TransactionDraft BuildSetTokenUriDraft(string from, string collection,
        BigInteger tokenId, string tokenUri, bool allowUnverifiedAbi = false)
    {
        TransactionDraft.ValidateAddress(collection, nameof(collection));
        Uint(tokenId, nameof(tokenId));
        ArgumentNullException.ThrowIfNull(tokenUri);
        if (!allowUnverifiedAbi)
            throw new NotSupportedException("setTokenURI is not in the pinned official NFT ABI");
        return Build(from, collection, new SetTokenUriNftFunction { TokenId = tokenId, TokenUri = tokenUri });
    }

    public TransactionDraft BuildDisableMintDraft(string from, string collection)
    {
        TransactionDraft.ValidateAddress(collection, nameof(collection));
        return Build(from, collection, new DisableMintNftFunction());
    }

    private TransactionDraft Create(string from, string creator, string symbol, string name,
        string contractUri, bool refundable, bool erc721)
    {
        TransactionDraft.ValidateAddress(creator, nameof(creator));
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(contractUri);
        var meta = new NftCollectionMeta
        {
            Creator = creator, Symbol = symbol, Name = name,
            ContractUri = contractUri, Refundable = refundable
        };
        return erc721
            ? Build(from, _nftCenter, new CreateDrc721Function { Meta = meta })
            : Build(from, _nftCenter, new CreateDrc1155Function { Meta = meta });
    }

    private TransactionDraft CreateLegacy(string from, string creator, string symbol, string name,
        string contractUri, bool allowMint, bool burnable, bool erc721)
    {
        TransactionDraft.ValidateAddress(creator, nameof(creator));
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(contractUri);
        var meta = new LegacyNftCollectionMeta
        {
            Creator = creator, Symbol = symbol, Name = name,
            ContractUri = contractUri, AllowMint = allowMint, Burnable = burnable
        };
        return erc721
            ? Build(from, _nftCenter, new CreateLegacyDrc721Function { Meta = meta })
            : Build(from, _nftCenter, new CreateLegacyDrc1155Function { Meta = meta });
    }

    private string DelegationNft() => _delegationNft ??
        throw new InvalidOperationException("Delegation NFT contract address must be supplied explicitly");

    private static TransactionDraft Build<T>(string from, string contract, T function,
        BigInteger valueWei = default) where T : FunctionMessage, new()
    {
        TransactionDraft.ValidateAddress(from, nameof(from));
        var encoder = new FunctionMessageEncodingService<T>(contract, from);
        var data = "0x" + Convert.ToHexString(encoder.GetCallData(function)).ToLowerInvariant();
        return TransactionDraft.ContractCall(from, contract, data, valueWei);
    }

    private static void ValidateMint(string collection, string recipient, string tokenUri)
    {
        TransactionDraft.ValidateAddress(collection, nameof(collection));
        TransactionDraft.ValidateAddress(recipient, nameof(recipient));
        ArgumentNullException.ThrowIfNull(tokenUri);
    }

    private static void ValidateStake(string validator, string collection, BigInteger tokenId)
    {
        TransactionDraft.ValidateAddress(validator, nameof(validator));
        TransactionDraft.ValidateAddress(collection, nameof(collection));
        Uint(tokenId, nameof(tokenId));
    }

    private static void Uint(BigInteger value, string parameter)
    {
        if (value < 0 || value > MaxUint256)
            throw new ArgumentOutOfRangeException(parameter, "Value must fit uint256");
    }

    private static void PositiveUint(BigInteger value, string parameter)
    {
        Uint(value, parameter);
        if (value.IsZero) throw new ArgumentOutOfRangeException(parameter, "Value must be positive");
    }
}

// ABI: decimalteam/go-smart-node/contracts/{nftCenter,nft721,nft1155,delegationNft}/*.json.
// addReserveByDEL and setTokenURI follow the local Python nft.py profile; current upstream NFT ABIs omit them.
[Struct("NFT")]
public sealed class NftCollectionMeta
{
    [Parameter("address", "creator", 1)] public string Creator { get; set; } = string.Empty;
    [Parameter("string", "symbol", 2)] public string Symbol { get; set; } = string.Empty;
    [Parameter("string", "name", 3)] public string Name { get; set; } = string.Empty;
    [Parameter("string", "contractURI", 4)] public string ContractUri { get; set; } = string.Empty;
    [Parameter("bool", "refundable", 5)] public bool Refundable { get; set; }
}

[Function("createERC721")]
public sealed class CreateDrc721Function : FunctionMessage
{
    [Parameter("tuple", "meta", 1)] public NftCollectionMeta Meta { get; set; } = new();
}

[Function("createERC1155")]
public sealed class CreateDrc1155Function : FunctionMessage
{
    [Parameter("tuple", "meta", 1)] public NftCollectionMeta Meta { get; set; } = new();
}

// Historical testnet receipts use createDRC721/1155 with allowMint and burnable.
[Struct("NFT")]
public sealed class LegacyNftCollectionMeta
{
    [Parameter("address", "creator", 1)] public string Creator { get; set; } = string.Empty;
    [Parameter("string", "symbol", 2)] public string Symbol { get; set; } = string.Empty;
    [Parameter("string", "name", 3)] public string Name { get; set; } = string.Empty;
    [Parameter("string", "contractURI", 4)] public string ContractUri { get; set; } = string.Empty;
    [Parameter("bool", "allowMint", 5)] public bool AllowMint { get; set; }
    [Parameter("bool", "burnable", 6)] public bool Burnable { get; set; }
}

[Function("createDRC721")]
public sealed class CreateLegacyDrc721Function : FunctionMessage
{
    [Parameter("tuple", "meta", 1)] public LegacyNftCollectionMeta Meta { get; set; } = new();
}

[Function("createDRC1155")]
public sealed class CreateLegacyDrc1155Function : FunctionMessage
{
    [Parameter("tuple", "meta", 1)] public LegacyNftCollectionMeta Meta { get; set; } = new();
}

[Function("mint")]
public sealed class MintDrc721Function : FunctionMessage
{
    [Parameter("address", "recipient", 1)] public string Recipient { get; set; } = string.Empty;
    [Parameter("string", "tokenURI", 2)] public string TokenUri { get; set; } = string.Empty;
    [Parameter("uint256", "reserveAmount", 3)] public BigInteger ReserveAmount { get; set; }
    [Parameter("address", "reserveToken", 4)] public string ReserveToken { get; set; } = string.Empty;
}

[Function("mintByETH")]
public sealed class MintDrc721ByEthFunction : FunctionMessage
{
    [Parameter("address", "recipient", 1)] public string Recipient { get; set; } = string.Empty;
    [Parameter("string", "tokenURI", 2)] public string TokenUri { get; set; } = string.Empty;
}

[Function("mint")]
public sealed class MintDrc1155Function : FunctionMessage
{
    [Parameter("address", "recipient", 1)] public string Recipient { get; set; } = string.Empty;
    [Parameter("uint256", "tokenId", 2)] public BigInteger TokenId { get; set; }
    [Parameter("uint256", "amountToMint", 3)] public BigInteger AmountToMint { get; set; }
    [Parameter("string", "tokenURI", 4)] public string TokenUri { get; set; } = string.Empty;
    [Parameter("uint256", "reserveAmount", 5)] public BigInteger ReserveAmount { get; set; }
    [Parameter("address", "reserveToken", 6)] public string ReserveToken { get; set; } = string.Empty;
}

[Function("mintByETH")]
public sealed class MintDrc1155ByEthFunction : FunctionMessage
{
    [Parameter("address", "recipient", 1)] public string Recipient { get; set; } = string.Empty;
    [Parameter("uint256", "tokenId", 2)] public BigInteger TokenId { get; set; }
    [Parameter("uint256", "amountToMint", 3)] public BigInteger AmountToMint { get; set; }
    [Parameter("string", "tokenURI", 4)] public string TokenUri { get; set; } = string.Empty;
}

[Function("burn")]
public sealed class BurnDrc721Function : FunctionMessage
{
    [Parameter("uint256", "tokenId", 1)] public BigInteger TokenId { get; set; }
}

[Function("burn")]
public sealed class BurnDrc1155Function : FunctionMessage
{
    [Parameter("uint256", "tokenId", 1)] public BigInteger TokenId { get; set; }
    [Parameter("uint256", "amount", 2)] public BigInteger Amount { get; set; }
}

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
public sealed class TransferNftStakeFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "nft", 2)] public string Nft { get; set; } = string.Empty;
    [Parameter("uint256", "tokenId", 3)] public BigInteger TokenId { get; set; }
    [Parameter("uint256", "amount", 4)] public BigInteger Amount { get; set; }
    [Parameter("address", "newValidator", 5)] public string NewValidator { get; set; } = string.Empty;
}

[Function("transferHold")]
public sealed class TransferHoldNftStakeFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "nft", 2)] public string Nft { get; set; } = string.Empty;
    [Parameter("uint256", "tokenId", 3)] public BigInteger TokenId { get; set; }
    [Parameter("uint256", "amount", 4)] public BigInteger Amount { get; set; }
    [Parameter("uint256", "holdTimestamp", 5)] public BigInteger HoldTimestamp { get; set; }
    [Parameter("address", "newValidator", 6)] public string NewValidator { get; set; } = string.Empty;
}

[Function("safeTransferFrom")]
public sealed class SafeTransferDrc721Function : FunctionMessage
{
    [Parameter("address", "from", 1)] public string Owner { get; set; } = string.Empty;
    [Parameter("address", "to", 2)] public string Recipient { get; set; } = string.Empty;
    [Parameter("uint256", "tokenId", 3)] public BigInteger TokenId { get; set; }
}

[Function("safeTransferFrom")]
public sealed class SafeTransferDrc1155Function : FunctionMessage
{
    [Parameter("address", "from", 1)] public string Owner { get; set; } = string.Empty;
    [Parameter("address", "to", 2)] public string Recipient { get; set; } = string.Empty;
    [Parameter("uint256", "id", 3)] public BigInteger TokenId { get; set; }
    [Parameter("uint256", "value", 4)] public BigInteger Amount { get; set; }
    [Parameter("bytes", "data", 5)] public byte[] Data { get; set; } = [];
}

[Function("addReserveByDEL")]
public sealed class AddDelReserveNftFunction : FunctionMessage
{
    [Parameter("uint256", "tokenId", 1)] public BigInteger TokenId { get; set; }
}

[Function("addReserve")]
public sealed class AddReserveNftFunction : FunctionMessage
{
    [Parameter("uint256", "tokenId", 1)] public BigInteger TokenId { get; set; }
    [Parameter("uint256", "addedReserveAmount", 2)] public BigInteger AmountRaw { get; set; }
}

[Function("addReserveByETH")]
public sealed class AddEthReserveNftFunction : FunctionMessage
{
    [Parameter("uint256", "tokenId", 1)] public BigInteger TokenId { get; set; }
}

[Function("setTokenURI")]
public sealed class SetTokenUriNftFunction : FunctionMessage
{
    [Parameter("uint256", "tokenId", 1)] public BigInteger TokenId { get; set; }
    [Parameter("string", "tokenURI", 2)] public string TokenUri { get; set; } = string.Empty;
}

[Function("disableMint")]
public sealed class DisableMintNftFunction : FunctionMessage
{
}
