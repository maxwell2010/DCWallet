using Decimal.Web3.Transactions;
using System.Numerics;
using Nethereum.Contracts;
using Nethereum.Contracts.MessageEncodingServices;
using Nethereum.RPC.Eth.DTOs;

namespace Decimal.Web3.Validators;

/// <summary>Read-only validator queries and unsigned management drafts. Never signs or submits transactions.</summary>
public sealed class ValidatorService
{
    public const string MainnetMasterValidatorAddress = "0x630B03FF9EeD4C4A468dA9f481DF23F542070Aa4";
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - 1;
    private readonly DecimalClient _client;

    public string MasterValidatorAddress { get; }

    public ValidatorService(DecimalClient client, string? masterValidatorAddress = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        if (masterValidatorAddress is null && client.Network.ChainId != 75)
            throw new ArgumentException("Resolve the MasterValidator address for this network or supply it explicitly",
                nameof(masterValidatorAddress));
        var address = masterValidatorAddress ?? MainnetMasterValidatorAddress;
        TransactionDraft.ValidateAddress(address, nameof(masterValidatorAddress));
        if (address.Equals("0x0000000000000000000000000000000000000000", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("MasterValidator address cannot be zero", nameof(masterValidatorAddress));
        MasterValidatorAddress = address;
    }

    /// <summary>Resolve through Contract Center, including testnet, after the client's chain check.</summary>
    public static async Task<ValidatorService> CreateResolvedAsync(DecimalClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        var address = await client.ResolveSystemContractAsync("master-validator").ConfigureAwait(false);
        return new ValidatorService(client, address);
    }

    public TransactionDraft BuildPauseSelfDraft(string from) => BuildDraft(from, new PauseSelfFunction());

    public TransactionDraft BuildAddCandidateDelDraft(string from, string validator, string meta, string amountDel)
    {
        TransactionDraft.ValidateAddress(validator, nameof(validator));
        ValidateMeta(meta);
        var valueWei = Units.Parse(amountDel);
        ValidatePositiveAmount(valueWei, nameof(amountDel));
        return BuildDraft(from, new AddCandidateDelFunction { Validator = validator, Meta = meta }, valueWei);
    }

    public TransactionDraft BuildAddCandidateWithTokenDraft(
        string from, string validator, string meta, string token, BigInteger amountRaw)
    {
        TransactionDraft.ValidateAddress(validator, nameof(validator));
        TransactionDraft.ValidateAddress(token, nameof(token));
        if (token.Equals(TransactionDraft.BurnAddress, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Token address cannot be zero", nameof(token));
        ValidateMeta(meta);
        ValidatePositiveAmount(amountRaw, nameof(amountRaw));
        return BuildDraft(from, new AddCandidateFunction
        {
            Validator = validator,
            Meta = meta,
            InitialStake = new InitialValidatorStake { Token = token, Amount = amountRaw }
        });
    }

    /// <summary>Owner is a trusted address obtained from GetOwnerAsync; this offline check does not query the chain.</summary>
    public TransactionDraft BuildAddValidatorDelDraft(
        string from, string owner, string validator, string meta, string amountDel)
    {
        ValidateOwner(from, owner);
        TransactionDraft.ValidateAddress(validator, nameof(validator));
        ValidateMeta(meta);
        var valueWei = Units.Parse(amountDel);
        ValidatePositiveAmount(valueWei, nameof(amountDel));
        return BuildDraft(from, new AddValidatorDelFunction { Validator = validator, Meta = meta }, valueWei);
    }

    public TransactionDraft BuildAddValidatorWithTokenDraft(
        string from, string owner, string validator, string meta, string token, BigInteger amountRaw)
    {
        ValidateOwner(from, owner);
        TransactionDraft.ValidateAddress(validator, nameof(validator));
        TransactionDraft.ValidateAddress(token, nameof(token));
        if (token.Equals(TransactionDraft.BurnAddress, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Token address cannot be zero", nameof(token));
        ValidateMeta(meta);
        ValidatePositiveAmount(amountRaw, nameof(amountRaw));
        return BuildDraft(from, new AddValidatorFunction
        {
            Validator = validator,
            Meta = meta,
            InitialStake = new InitialValidatorStake { Token = token, Amount = amountRaw }
        });
    }

    public TransactionDraft BuildAddValidatorsDraft(string from, string owner, IReadOnlyList<ValidatorMeta> metas)
    {
        ValidateOwner(from, owner);
        if (metas is null || metas.Count == 0)
            throw new ArgumentException("At least one validator metadata entry is required", nameof(metas));
        var copy = new List<ValidatorMeta>(metas.Count);
        foreach (var entry in metas)
        {
            if (entry is null) throw new ArgumentException("Metadata entries cannot be null", nameof(metas));
            TransactionDraft.ValidateAddress(entry.Validator, nameof(metas));
            ValidateMeta(entry.Meta);
            copy.Add(new ValidatorMeta { Validator = entry.Validator, Meta = entry.Meta });
        }
        return BuildDraft(from, new AddValidatorsFunction { Metas = copy });
    }

    public TransactionDraft BuildApproveCandidatesDraft(string from, string owner, IReadOnlyList<string> candidates)
    {
        ValidateOwner(from, owner);
        if (candidates is null || candidates.Count == 0)
            throw new ArgumentException("At least one candidate is required", nameof(candidates));
        var copy = new List<string>(candidates.Count);
        foreach (var candidate in candidates)
        {
            TransactionDraft.ValidateAddress(candidate, nameof(candidates));
            copy.Add(candidate);
        }
        return BuildDraft(from, new ApproveCandidatesFunction { Candidates = copy });
    }

    public TransactionDraft BuildAddPenaltyDraft(
        string from, string owner, string validator, BigInteger penaltyPercentage)
    {
        ValidateOwner(from, owner);
        TransactionDraft.ValidateAddress(validator, nameof(validator));
        if (penaltyPercentage < 0 || penaltyPercentage > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(penaltyPercentage), "Penalty must fit uint16");
        return BuildDraft(from, new AddPenaltyFunction
        {
            Validator = validator, PenaltyPercentage = (ushort)penaltyPercentage
        });
    }

    public TransactionDraft BuildRemoveValidatorDraft(string from, string validator)
    {
        TransactionDraft.ValidateAddress(validator, nameof(validator));
        return BuildDraft(from, new RemoveValidatorFunction { Validator = validator });
    }

    public TransactionDraft BuildUpdateValidatorMetaDraft(string from, string validator, string meta)
    {
        TransactionDraft.ValidateAddress(validator, nameof(validator));
        ValidateMeta(meta);
        return BuildDraft(from, new UpdateValidatorMetaFunction { Validator = validator, Meta = meta });
    }

    public TransactionDraft BuildUnpauseSelfDraft(string from) => BuildDraft(from, new UnpauseSelfFunction());

    public TransactionDraft BuildPauseValidatorDraft(string from, string validator)
    {
        TransactionDraft.ValidateAddress(validator, nameof(validator));
        return BuildDraft(from, new PauseValidatorFunction { Validator = validator });
    }

    public TransactionDraft BuildUnpauseValidatorDraft(string from, string validator)
    {
        TransactionDraft.ValidateAddress(validator, nameof(validator));
        return BuildDraft(from, new UnpauseValidatorFunction { Validator = validator });
    }

    public Task<byte> GetValidatorStatusAsync(string validator, BlockParameter? block = null)
    {
        TransactionDraft.ValidateAddress(validator, nameof(validator));
        return QueryAsync<GetValidatorStatusFunction, byte>(new() { Validator = validator }, block);
    }

    public Task<bool> IsActiveAsync(string validator, BlockParameter? block = null)
    {
        TransactionDraft.ValidateAddress(validator, nameof(validator));
        return QueryAsync<IsValidatorActiveFunction, bool>(new() { Validator = validator }, block);
    }

    public Task<bool> IsMemberAsync(string validator, BlockParameter? block = null)
    {
        TransactionDraft.ValidateAddress(validator, nameof(validator));
        return QueryAsync<IsValidatorMemberFunction, bool>(new() { Validator = validator }, block);
    }

    public async Task<ValidatorRecord> GetValidatorAsync(string validator, BlockParameter? block = null)
    {
        TransactionDraft.ValidateAddress(validator, nameof(validator));
        var output = await _client.Web3.Eth.GetContractQueryHandler<GetValidatorFunction>()
            .QueryDeserializingToObjectAsync<GetValidatorOutput>(
                new GetValidatorFunction { Validator = validator }, MasterValidatorAddress,
                block ?? BlockParameter.CreateLatest()).ConfigureAwait(false);
        return output?.Validator ?? throw new InvalidOperationException("Missing validator response");
    }

    public Task<List<ushort>> GetValidatorPenaltiesAsync(string validator, BlockParameter? block = null)
    {
        TransactionDraft.ValidateAddress(validator, nameof(validator));
        return QueryAsync<GetValidatorPenaltiesFunction, List<ushort>>(
            new() { Validator = validator }, block);
    }

    public async Task<GetRewardPerBlockOutput> GetRewardPerBlockAsync(BlockParameter? block = null) =>
        await _client.Web3.Eth.GetContractQueryHandler<GetRewardPerBlockFunction>()
            .QueryDeserializingToObjectAsync<GetRewardPerBlockOutput>(
                new GetRewardPerBlockFunction(), MasterValidatorAddress,
                block ?? BlockParameter.CreateLatest()).ConfigureAwait(false)
        ?? throw new InvalidOperationException("Missing reward-per-block response");

    public Task<string> GetOwnerAsync(BlockParameter? block = null) =>
        QueryAsync<GetMasterValidatorOwnerFunction, string>(new(), block);

    public Task<string> GetImplementationAsync(BlockParameter? block = null) =>
        QueryAsync<GetMasterValidatorImplementationFunction, string>(new(), block);

    private Task<T> QueryAsync<TFunction, T>(TFunction function, BlockParameter? block)
        where TFunction : FunctionMessage, new() =>
        _client.Web3.Eth.GetContractQueryHandler<TFunction>().QueryAsync<T>(
            MasterValidatorAddress, function, block ?? BlockParameter.CreateLatest());

    private TransactionDraft BuildDraft<TFunction>(string from, TFunction function, BigInteger valueWei = default)
        where TFunction : FunctionMessage, new()
    {
        TransactionDraft.ValidateAddress(from, nameof(from));
        var encoder = new FunctionMessageEncodingService<TFunction>(MasterValidatorAddress, from);
        var data = "0x" + Convert.ToHexString(encoder.GetCallData(function)).ToLowerInvariant();
        return TransactionDraft.ContractCall(from, MasterValidatorAddress, data, valueWei);
    }

    private static void ValidateMeta(string meta)
    {
        if (string.IsNullOrWhiteSpace(meta))
            throw new ArgumentException("Validator metadata cannot be empty", nameof(meta));
    }

    private static void ValidateOwner(string from, string owner)
    {
        TransactionDraft.ValidateAddress(from, nameof(from));
        TransactionDraft.ValidateAddress(owner, nameof(owner));
        if (!from.Equals(owner, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Sender must match the supplied owner", nameof(from));
    }

    private static void ValidatePositiveAmount(BigInteger amount, string parameter)
    {
        if (amount <= BigInteger.Zero || amount > MaxUint256)
            throw new ArgumentOutOfRangeException(parameter, "Amount must be a positive uint256 value");
    }
}
