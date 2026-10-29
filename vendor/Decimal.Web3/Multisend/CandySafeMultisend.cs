using System.Numerics;
using System.Text;
using Decimal.Web3.Tokens;
using Decimal.Web3.Transactions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.MessageEncodingServices;
using Nethereum.Util;

namespace Decimal.Web3.Multisend;

public sealed record SafeMultisendDeployment(long ChainId, string Address, string RuntimeHash)
{
    private const string ReviewedRuntimeHash = "0x7784c7a41662304b6f6c183f2169a9b6844a1a2f192021d8f3d1ccbdacfc821e";

    public static SafeMultisendDeployment Mainnet { get; } = new(
        75, "0x9b0658e219636215f7f78a88c241ef3bbff447a9", ReviewedRuntimeHash);

    public static SafeMultisendDeployment Testnet { get; } = new(
        202020, "0xf5d23ad4ba70d22554d25bcfe9bae2af828a6ea4", ReviewedRuntimeHash);

    public static SafeMultisendDeployment ForNetwork(DecimalNetwork network) => network.ChainId switch
    {
        75 => Mainnet,
        202020 => Testnet,
        _ => throw new NotSupportedException("No reviewed multisend deployment is pinned for this chain")
    };
}

public sealed record Erc20PermitSignature(BigInteger Deadline, byte V, byte[] R, byte[] S);

/// <summary>One-transaction ERC20 payouts through the reviewed sender-restricted contract.</summary>
public sealed class CandySafeMultisend
{
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - BigInteger.One;
    private readonly DecimalClient _client;
    private readonly SafeMultisendDeployment _deployment;

    public CandySafeMultisend(DecimalClient client, SafeMultisendDeployment? deployment = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _deployment = deployment ?? SafeMultisendDeployment.ForNetwork(client.Network);
        if (_deployment.ChainId != client.Network.ChainId)
            throw new ArgumentException("Deployment chain ID does not match the client network", nameof(deployment));
        TransactionDraft.ValidateAddress(_deployment.Address, nameof(deployment));
        if (_deployment.Address.Equals(TransactionDraft.BurnAddress, StringComparison.OrdinalIgnoreCase) ||
            _deployment.RuntimeHash is null || _deployment.RuntimeHash.Length != 66 ||
            !_deployment.RuntimeHash.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            !_deployment.RuntimeHash.AsSpan(2).ToString().All(Uri.IsHexDigit))
            throw new ArgumentException("Deployment must pin a nonzero contract and runtime hash", nameof(deployment));
    }

    public async Task VerifyDeploymentAsync()
    {
        var chainId = (await _client.Web3.Eth.ChainId.SendRequestAsync().ConfigureAwait(false)).Value;
        if (chainId != _deployment.ChainId)
            throw new InvalidOperationException("RPC chain ID does not match the multisend deployment");
        var code = await _client.Web3.Eth.GetCode.SendRequestAsync(_deployment.Address).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(code) || code == "0x")
            throw new InvalidOperationException("Multisend contract is not deployed on this network");
        var bytes = Convert.FromHexString(code.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? code[2..] : code);
        var actualHash = "0x" + Convert.ToHexString(Sha3Keccack.Current.CalculateHash(bytes)).ToLowerInvariant();
        if (!actualHash.Equals(_deployment.RuntimeHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Multisend runtime does not match the reviewed deployment");
    }

    public TransactionDraft BuildTokenDraft(
        string from, string tokenAddress, IReadOnlyList<Erc20Recipient> recipients, string? memo = null)
    {
        var batch = ValidateBatch(from, tokenAddress, recipients, memo);
        var function = new SendTokenFunction
        {
            Token = tokenAddress,
            Recipients = batch.Addresses,
            Amounts = batch.Amounts,
            Memo = batch.Memo
        };
        return Encode(from, function);
    }

    public TransactionDraft BuildTokenWithPermitDraft(
        string from, string tokenAddress, IReadOnlyList<Erc20Recipient> recipients,
        Erc20PermitSignature permit, string? memo = null)
    {
        var batch = ValidateBatch(from, tokenAddress, recipients, memo);
        ArgumentNullException.ThrowIfNull(permit);
        if (permit.Deadline < 0 || permit.Deadline > MaxUint256 || permit.V is not (27 or 28) ||
            permit.R is not { Length: 32 } || permit.S is not { Length: 32 })
            throw new ArgumentException("Permit must contain a valid deadline and 65-byte ECDSA signature", nameof(permit));
        var function = new SendTokenWithPermitFunction
        {
            Token = tokenAddress,
            Recipients = batch.Addresses,
            Amounts = batch.Amounts,
            Memo = batch.Memo,
            Deadline = permit.Deadline,
            V = permit.V,
            R = permit.R,
            S = permit.S
        };
        return Encode(from, function);
    }

    public async Task<FeeQuote> EstimateTokenAsync(
        string from, string tokenAddress, IReadOnlyList<Erc20Recipient> recipients,
        string? memo = null, bool exact = false)
    {
        var batch = ValidateBatch(from, tokenAddress, recipients, memo);
        await VerifyDeploymentAsync().ConfigureAwait(false);
        var token = new Erc20Service(_client, tokenAddress);
        var balanceTask = token.GetBalanceRawAsync(from);
        var allowanceTask = token.GetAllowanceRawAsync(from, _deployment.Address);
        await Task.WhenAll(balanceTask, allowanceTask).ConfigureAwait(false);
        if (balanceTask.Result < batch.Total)
            throw new DecimalTransactionException(DecimalTransactionError.InsufficientToken);
        if (allowanceTask.Result < batch.Total)
            throw new DecimalTransactionException(DecimalTransactionError.ApprovalRequired);
        return await _client.EstimateFeeAsync(BuildTokenDraft(from, tokenAddress, recipients, memo), exact)
            .ConfigureAwait(false);
    }

    public async Task<FeeQuote> EstimateTokenWithPermitAsync(
        string from, string tokenAddress, IReadOnlyList<Erc20Recipient> recipients,
        Erc20PermitSignature permit, string? memo = null, bool exact = false)
    {
        var draft = BuildTokenWithPermitDraft(from, tokenAddress, recipients, permit, memo);
        await VerifyDeploymentAsync().ConfigureAwait(false);
        var token = new Erc20Service(_client, tokenAddress);
        var balance = await token.GetBalanceRawAsync(from).ConfigureAwait(false);
        var total = recipients.Aggregate(BigInteger.Zero, (sum, recipient) => sum + recipient.AmountRaw);
        if (balance < total)
            throw new DecimalTransactionException(DecimalTransactionError.InsufficientToken);
        return await _client.EstimateFeeAsync(draft, exact).ConfigureAwait(false);
    }

    private (List<string> Addresses, List<BigInteger> Amounts, byte[] Memo, BigInteger Total) ValidateBatch(
        string from, string tokenAddress, IReadOnlyList<Erc20Recipient> recipients, string? memo)
    {
        TransactionDraft.ValidateAddress(from, nameof(from));
        TransactionDraft.ValidateAddress(tokenAddress, nameof(tokenAddress));
        if (tokenAddress.Equals(TransactionDraft.BurnAddress, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Token address must be nonzero", nameof(tokenAddress));
        ArgumentNullException.ThrowIfNull(recipients);
        if (recipients.Count is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(recipients), "Recipient count must be from 1 to 500");
        var memoBytes = Encoding.UTF8.GetBytes(memo ?? string.Empty);
        if (memoBytes.Length > 256)
            throw new ArgumentException("Memo exceeds 256 UTF-8 bytes", nameof(memo));

        var addresses = new List<string>(recipients.Count);
        var amounts = new List<BigInteger>(recipients.Count);
        var total = BigInteger.Zero;
        foreach (var recipient in recipients)
        {
            if (recipient is null) throw new ArgumentException("Recipient is required", nameof(recipients));
            TransactionDraft.ValidateAddress(recipient.Address, nameof(recipients));
            if (recipient.Address.Equals(TransactionDraft.BurnAddress, StringComparison.OrdinalIgnoreCase) ||
                recipient.Address.Equals(_deployment.Address, StringComparison.OrdinalIgnoreCase) ||
                recipient.Address.Equals(from, StringComparison.OrdinalIgnoreCase) ||
                recipient.AmountRaw <= 0 || recipient.AmountRaw > MaxUint256)
                throw new ArgumentException("Invalid recipient or amount for the deployed contract", nameof(recipients));
            total += recipient.AmountRaw;
            if (total > MaxUint256)
                throw new ArgumentOutOfRangeException(nameof(recipients), "Total amount must fit uint256");
            addresses.Add(recipient.Address);
            amounts.Add(recipient.AmountRaw);
        }
        return (addresses, amounts, memoBytes, total);
    }

    private TransactionDraft Encode<TFunction>(string from, TFunction function)
        where TFunction : FunctionMessage, new()
    {
        var encoder = new FunctionMessageEncodingService<TFunction>(_deployment.Address, from);
        var data = "0x" + Convert.ToHexString(encoder.GetCallData(function)).ToLowerInvariant();
        return TransactionDraft.ContractCall(from, _deployment.Address, data);
    }
}

[Function("sendToken")]
public sealed class SendTokenFunction : FunctionMessage
{
    [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("address", "token", 1)] public string Token { get; set; } = string.Empty;
    [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("address[]", "recipients", 2)] public List<string> Recipients { get; set; } = new();
    [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("uint256[]", "amounts", 3)] public List<BigInteger> Amounts { get; set; } = new();
    [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("bytes", "memo", 4)] public byte[] Memo { get; set; } = [];
}

[Function("sendTokenWithPermit")]
public sealed class SendTokenWithPermitFunction : FunctionMessage
{
    [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("address", "token", 1)] public string Token { get; set; } = string.Empty;
    [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("address[]", "recipients", 2)] public List<string> Recipients { get; set; } = new();
    [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("uint256[]", "amounts", 3)] public List<BigInteger> Amounts { get; set; } = new();
    [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("bytes", "memo", 4)] public byte[] Memo { get; set; } = [];
    [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("uint256", "deadline", 5)] public BigInteger Deadline { get; set; }
    [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("uint8", "v", 6)] public byte V { get; set; }
    [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("bytes32", "r", 7)] public byte[] R { get; set; } = [];
    [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("bytes32", "s", 8)] public byte[] S { get; set; } = [];
}
