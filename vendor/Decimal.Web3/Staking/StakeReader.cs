using System.Numerics;
using Decimal.Web3.Transactions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;

namespace Decimal.Web3.Staking;

/// <summary>Read an exact delegated stake or hold from the Decimal delegation contract.</summary>
public sealed class StakeReader
{
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - BigInteger.One;
    private readonly Nethereum.Web3.Web3 _web3;
    private readonly string _delegationAddress;

    public StakeReader(Nethereum.Web3.Web3 web3, string delegationAddress)
    {
        _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
        TransactionDraft.ValidateAddress(delegationAddress, nameof(delegationAddress));
        _delegationAddress = delegationAddress;
    }

    public Task<DelegationStake> GetStakeAsync(
        string validator, string delegator, string token, BlockParameter? block = null) =>
        QueryAsync<GetStakeFunction>(validator, delegator, token, null, block);

    public Task<DelegationStake> GetHoldStakeAsync(
        string validator, string delegator, string token, BigInteger holdTimestamp,
        BlockParameter? block = null)
    {
        if (holdTimestamp < BigInteger.Zero || holdTimestamp > MaxUint256)
            throw new ArgumentOutOfRangeException(nameof(holdTimestamp));
        return QueryAsync<GetHoldStakeFunction>(validator, delegator, token, holdTimestamp, block);
    }

    private async Task<DelegationStake> QueryAsync<TFunction>(
        string validator, string delegator, string token, BigInteger? holdTimestamp, BlockParameter? block)
        where TFunction : FunctionMessage, new()
    {
        TransactionDraft.ValidateAddress(validator, nameof(validator));
        TransactionDraft.ValidateAddress(delegator, nameof(delegator));
        TransactionDraft.ValidateAddress(token, nameof(token));
        TFunction function = holdTimestamp is null
            ? (TFunction)(FunctionMessage)new GetStakeFunction
            {
                Validator = validator, Delegator = delegator, Token = token
            }
            : (TFunction)(FunctionMessage)new GetHoldStakeFunction
            {
                Validator = validator, Delegator = delegator, Token = token,
                HoldTimestamp = holdTimestamp.Value
            };
        var output = await _web3.Eth.GetContractQueryHandler<TFunction>()
            .QueryDeserializingToObjectAsync<GetStakeOutput>(
                function, _delegationAddress, block ?? BlockParameter.CreateLatest())
            .ConfigureAwait(false);
        if (output?.Stake is null)
            throw new InvalidOperationException("Missing delegation stake response");
        return DelegationStake.FromContract(output.Stake);
    }
}

[Function("getStake", typeof(GetStakeOutput))]
public sealed class GetStakeFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "delegator", 2)] public string Delegator { get; set; } = string.Empty;
    [Parameter("address", "token", 3)] public string Token { get; set; } = string.Empty;
}

[Function("getHoldStake", typeof(GetStakeOutput))]
public sealed class GetHoldStakeFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "delegator", 2)] public string Delegator { get; set; } = string.Empty;
    [Parameter("address", "token", 3)] public string Token { get; set; } = string.Empty;
    [Parameter("uint256", "holdTimestamp", 4)] public BigInteger HoldTimestamp { get; set; }
}

[FunctionOutput]
public sealed class GetStakeOutput : IFunctionOutputDTO
{
    [Parameter("tuple", "", 1)] public DelegationStakeDto Stake { get; set; } = new();
}
