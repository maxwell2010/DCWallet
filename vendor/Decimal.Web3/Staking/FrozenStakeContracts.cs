using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Decimal.Web3.Staking;

// ABI: decimalteam/go-smart-node/contracts/delegation/DecimalDelegation.json.
[Function("getFrozenStake", typeof(GetFrozenStakeOutput))]
public sealed class GetFrozenStakeFunction : FunctionMessage
{
    [Parameter("uint256", "index", 1)]
    public BigInteger Index { get; set; }
}

[Function("getFrozenStakes", typeof(GetFrozenStakesOutput))]
public sealed class GetFrozenStakesFunction : FunctionMessage
{
    [Parameter("uint256[]", "stakeIndexes", 1)]
    public List<BigInteger> StakeIndexes { get; set; } = new();
}

[Struct("Stake")]
public sealed class DelegationStakeDto
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "delegator", 2)] public string Delegator { get; set; } = string.Empty;
    [Parameter("address", "token", 3)] public string Token { get; set; } = string.Empty;
    [Parameter("uint256", "amount", 4)] public BigInteger Amount { get; set; }
    [Parameter("uint256", "tokenId", 5)] public BigInteger TokenId { get; set; }
    [Parameter("uint8", "tokenType", 6)] public byte TokenType { get; set; }
    [Parameter("uint256", "holdTimestamp", 7)] public BigInteger HoldTimestamp { get; set; }
}

[Struct("FrozenStake")]
public sealed class FrozenStakeDto
{
    [Parameter("tuple", "stake", 1)] public DelegationStakeDto Stake { get; set; } = new();
    [Parameter("uint8", "freezeStatus", 2)] public byte FreezeStatus { get; set; }
    [Parameter("uint8", "freezeType", 3)] public byte FreezeType { get; set; }
    [Parameter("uint256", "unfreezeTimestamp", 4)] public BigInteger UnfreezeTimestamp { get; set; }
}

// Some deployed delegation contracts include an additional uint256 in Stake.
[Struct("Stake")]
public sealed class ExtendedDelegationStakeDto
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("address", "delegator", 2)] public string Delegator { get; set; } = string.Empty;
    [Parameter("address", "token", 3)] public string Token { get; set; } = string.Empty;
    [Parameter("uint256", "amount", 4)] public BigInteger Amount { get; set; }
    [Parameter("uint256", "tokenId", 5)] public BigInteger TokenId { get; set; }
    [Parameter("uint8", "tokenType", 6)] public byte TokenType { get; set; }
    [Parameter("uint256", "holdTimestamp", 7)] public BigInteger HoldTimestamp { get; set; }
    [Parameter("uint256", "extraIndex", 8)] public BigInteger ExtraIndex { get; set; }
}

[Struct("FrozenStake")]
public sealed class FrozenStakeWithExtendedStakeDto
{
    [Parameter("tuple", "stake", 1)] public ExtendedDelegationStakeDto Stake { get; set; } = new();
    [Parameter("uint8", "freezeStatus", 2)] public byte FreezeStatus { get; set; }
    [Parameter("uint8", "freezeType", 3)] public byte FreezeType { get; set; }
    [Parameter("uint256", "unfreezeTimestamp", 4)] public BigInteger UnfreezeTimestamp { get; set; }
}

[FunctionOutput]
public sealed class GetFrozenStakeOutput : IFunctionOutputDTO
{
    [Parameter("tuple", "", 1)] public FrozenStakeDto FrozenStake { get; set; } = new();
}

[FunctionOutput]
public sealed class GetFrozenStakesOutput : IFunctionOutputDTO
{
    [Parameter("tuple[]", "", 1)] public List<FrozenStakeDto> FrozenStakes { get; set; } = new();
}

[FunctionOutput]
public sealed class GetFrozenStakeExtendedOutput : IFunctionOutputDTO
{
    [Parameter("tuple", "", 1)] public FrozenStakeWithExtendedStakeDto FrozenStake { get; set; } = new();
}

[FunctionOutput]
public sealed class GetFrozenStakesExtendedOutput : IFunctionOutputDTO
{
    [Parameter("tuple[]", "", 1)] public List<FrozenStakeWithExtendedStakeDto> FrozenStakes { get; set; } = new();
}
