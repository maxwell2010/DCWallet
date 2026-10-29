using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Numerics;

namespace Decimal.Web3.Validators;

// ABI: decimalteam/go-smart-node/contracts/validator/DecimalMasterValidator.json.
[Struct("Stake")]
public sealed class InitialValidatorStake
{
    [Parameter("address", "token", 1)] public string Token { get; set; } = string.Empty;
    [Parameter("uint256", "amount", 2)] public BigInteger Amount { get; set; }
}

[Function("addCandidate")]
public sealed class AddCandidateFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("string", "meta", 2)] public string Meta { get; set; } = string.Empty;
    [Parameter("tuple", "initialStake", 3)] public InitialValidatorStake InitialStake { get; set; } = new();
}

[Function("addCandidateDEL")]
public sealed class AddCandidateDelFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("string", "meta", 2)] public string Meta { get; set; } = string.Empty;
}

[Function("addValidator")]
public sealed class AddValidatorFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("string", "meta", 2)] public string Meta { get; set; } = string.Empty;
    [Parameter("tuple", "initialStake", 3)] public InitialValidatorStake InitialStake { get; set; } = new();
}

[Function("addValidatorDEL")]
public sealed class AddValidatorDelFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("string", "meta", 2)] public string Meta { get; set; } = string.Empty;
}

[Struct("ValidatorMeta")]
public sealed class ValidatorMeta
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("string", "meta", 2)] public string Meta { get; set; } = string.Empty;
}

[Function("addValidators")]
public sealed class AddValidatorsFunction : FunctionMessage
{
    [Parameter("tuple[]", "metas", 1)] public List<ValidatorMeta> Metas { get; set; } = new();
}

[Function("approveCandidates")]
public sealed class ApproveCandidatesFunction : FunctionMessage
{
    [Parameter("address[]", "candidates", 1)] public List<string> Candidates { get; set; } = new();
}

[Function("addPenalty")]
public sealed class AddPenaltyFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("uint16", "penaltyPercentage", 2)] public ushort PenaltyPercentage { get; set; }
}

[Function("removeValidator")]
public sealed class RemoveValidatorFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
}

[Function("updateValidatorMeta")]
public sealed class UpdateValidatorMetaFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
    [Parameter("string", "meta", 2)] public string Meta { get; set; } = string.Empty;
}

[Function("pauseSelf")]
public sealed class PauseSelfFunction : FunctionMessage;

[Function("unpauseSelf")]
public sealed class UnpauseSelfFunction : FunctionMessage;

[Function("pauseValidator")]
public sealed class PauseValidatorFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
}

[Function("unpauseValidator")]
public sealed class UnpauseValidatorFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
}

[Function("getValidatorStatus", "uint8")]
public sealed class GetValidatorStatusFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
}

[Function("isActive", "bool")]
public sealed class IsValidatorActiveFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
}

[Function("isMember", "bool")]
public sealed class IsValidatorMemberFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
}

// ABI: go-smart-node/contracts/validator/DecimalMasterValidator.json.
// The ABI spells penaltyPercantages this way; keep it for decoder compatibility.
[Struct("Validator")]
public sealed class ValidatorRecord
{
    [Parameter("uint8", "status", 1)] public byte Status { get; set; }
    [Parameter("bool", "paused", 2)] public bool Paused { get; set; }
    [Parameter("uint16[]", "penaltyPercantages", 3)] public List<ushort> PenaltyPercantages { get; set; } = new();
}

[FunctionOutput]
public sealed class GetValidatorOutput : IFunctionOutputDTO
{
    [Parameter("tuple", "", 1)] public ValidatorRecord Validator { get; set; } = new();
}

[Function("getValidator", typeof(GetValidatorOutput))]
public sealed class GetValidatorFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
}

[Function("getValidatorPenalties", "uint16[]")]
public sealed class GetValidatorPenaltiesFunction : FunctionMessage
{
    [Parameter("address", "validator", 1)] public string Validator { get; set; } = string.Empty;
}

[FunctionOutput]
public sealed class GetRewardPerBlockOutput : IFunctionOutputDTO
{
    [Parameter("uint256", "rewardPerBlock", 1)] public BigInteger RewardPerBlock { get; set; }
    [Parameter("bool", "enabled", 2)] public bool Enabled { get; set; }
}

[Function("getRewardPerBlock", typeof(GetRewardPerBlockOutput))]
public sealed class GetRewardPerBlockFunction : FunctionMessage;

[Function("owner", "address")]
public sealed class GetMasterValidatorOwnerFunction : FunctionMessage;

[Function("getImpl", "address")]
public sealed class GetMasterValidatorImplementationFunction : FunctionMessage;
