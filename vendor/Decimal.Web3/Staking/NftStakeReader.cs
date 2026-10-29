using System.Numerics;
using Decimal.Web3.Transactions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;

namespace Decimal.Web3.Staking;

/// <summary>Read NFT delegation stakes from an explicitly supplied NFT delegation contract.</summary>
public sealed class NftStakeReader
{
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - BigInteger.One;
    private readonly Nethereum.Web3.Web3 _web3;
    private readonly string _contract;

    public NftStakeReader(Nethereum.Web3.Web3 web3, string nftDelegationContract)
    {
        _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
        TransactionDraft.ValidateAddress(nftDelegationContract, nameof(nftDelegationContract));
        _contract = nftDelegationContract;
    }

    public async Task<DelegationStake> GetStakeAsync(
        string validator, string delegator, string nft, BigInteger tokenId, BlockParameter? block = null)
    {
        Validate(validator, delegator, nft, tokenId);
        var result = await _web3.Eth.GetContractQueryHandler<GetNftStakeFunction>()
            .QueryDeserializingToObjectAsync<GetStakeOutput>(new GetNftStakeFunction
            {
                Validator = validator, Delegator = delegator, Nft = nft, TokenId = tokenId
            }, _contract, block ?? BlockParameter.CreateLatest()).ConfigureAwait(false);
        return DelegationStake.FromContract(result?.Stake ??
            throw new InvalidOperationException("Missing NFT stake response"));
    }

    public async Task<DelegationStake> GetHoldStakeAsync(
        string validator, string delegator, string nft, BigInteger tokenId,
        BigInteger holdTimestamp, BlockParameter? block = null)
    {
        Validate(validator, delegator, nft, tokenId);
        ValidateUint256(holdTimestamp, nameof(holdTimestamp));
        var result = await _web3.Eth.GetContractQueryHandler<GetHoldNftStakeFunction>()
            .QueryDeserializingToObjectAsync<GetStakeOutput>(new GetHoldNftStakeFunction
            {
                Validator = validator, Delegator = delegator, Nft = nft,
                TokenId = tokenId, HoldTimestamp = holdTimestamp
            }, _contract, block ?? BlockParameter.CreateLatest()).ConfigureAwait(false);
        return DelegationStake.FromContract(result?.Stake ??
            throw new InvalidOperationException("Missing NFT hold stake response"));
    }

    private static void Validate(string validator, string delegator, string nft, BigInteger tokenId)
    {
        TransactionDraft.ValidateAddress(validator, nameof(validator));
        TransactionDraft.ValidateAddress(delegator, nameof(delegator));
        TransactionDraft.ValidateAddress(nft, nameof(nft));
        ValidateUint256(tokenId, nameof(tokenId));
    }

    private static void ValidateUint256(BigInteger value, string parameter)
    {
        if (value < BigInteger.Zero || value > MaxUint256)
            throw new ArgumentOutOfRangeException(parameter, "Value must fit uint256");
    }
}

[Function("getStake", typeof(GetStakeOutput))]
public sealed class GetNftStakeFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "delegator", 2)] public string Delegator { get; set; } = string.Empty;
    [Parameter("address", "nft", 3)] public string Nft { get; set; } = string.Empty;
    [Parameter("uint256", "tokenId", 4)] public BigInteger TokenId { get; set; }
}

[Function("getHoldStake", typeof(GetStakeOutput))]
public sealed class GetHoldNftStakeFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "delegator", 2)] public string Delegator { get; set; } = string.Empty;
    [Parameter("address", "nft", 3)] public string Nft { get; set; } = string.Empty;
    [Parameter("uint256", "tokenId", 4)] public BigInteger TokenId { get; set; }
    [Parameter("uint256", "timestamp", 5)] public BigInteger HoldTimestamp { get; set; }
}
