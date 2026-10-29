using System.Numerics;
using Decimal.Web3.Transactions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;

namespace Decimal.Web3.Tokens;

public sealed record ReservableTokenState(
    Erc20Metadata Metadata,
    string Creator,
    BigInteger ReserveWei,
    BigInteger Crr,
    BigInteger MinTotalSupplyRaw,
    BigInteger MaxTotalSupplyRaw,
    BigInteger CurrentPriceRaw);

/// <summary>Read-only state and on-chain quotes for Decimal reserve-backed tokens.</summary>
public sealed class ReservableTokenService
{
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - 1;
    private readonly DecimalClient _client;
    private readonly Erc20Service _erc20;

    public string TokenAddress => _erc20.TokenAddress;

    public ReservableTokenService(DecimalClient client, string tokenAddress)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _erc20 = new Erc20Service(client, tokenAddress);
    }

    public async Task<ReservableTokenState> GetStateAsync(BlockParameter? block = null)
    {
        var snapshot = block ?? BlockParameter.CreateLatest();
        var metadata = _erc20.GetMetadataAsync(snapshot);
        var creator = GetCreatorAsync(snapshot);
        var reserve = GetReserveWeiAsync(snapshot);
        var crr = GetCrrAsync(snapshot);
        var minSupply = GetMinTotalSupplyRawAsync(snapshot);
        var maxSupply = GetMaxTotalSupplyRawAsync(snapshot);
        var price = GetCurrentPriceRawAsync(snapshot);
        await Task.WhenAll(metadata, creator, reserve, crr, minSupply, maxSupply, price).ConfigureAwait(false);
        return new(metadata.Result, creator.Result, reserve.Result, crr.Result,
            minSupply.Result, maxSupply.Result, price.Result);
    }

    public Task<string> GetCreatorAsync(BlockParameter? block = null) =>
        QueryAsync<CreatorFunction, string>(new(), block);

    public Task<string> GetIdentityAsync(BlockParameter? block = null) =>
        QueryAsync<IdentityFunction, string>(new(), block);

    public Task<BigInteger> GetReserveWeiAsync(BlockParameter? block = null) =>
        QueryAsync<ReserveFunction, BigInteger>(new(), block);

    public Task<BigInteger> GetCrrAsync(BlockParameter? block = null) =>
        QueryAsync<CrrFunction, BigInteger>(new(), block);

    public Task<BigInteger> GetMinTotalSupplyRawAsync(BlockParameter? block = null) =>
        QueryAsync<MinTotalSupplyFunction, BigInteger>(new(), block);

    public Task<BigInteger> GetMaxTotalSupplyRawAsync(BlockParameter? block = null) =>
        QueryAsync<MaxTotalSupplyFunction, BigInteger>(new(), block);

    public Task<BigInteger> GetCurrentPriceRawAsync(BlockParameter? block = null) =>
        QueryAsync<CurrentPriceFunction, BigInteger>(new(), block);

    public Task<BigInteger> CalculateBuyInputWeiAsync(BigInteger amountOutRaw, BlockParameter? block = null) =>
        QueryAsync<CalculateBuyInputFunction, BigInteger>(
            new() { Amount = ValidatePositive(amountOutRaw, nameof(amountOutRaw)) }, block);

    public Task<BigInteger> CalculateBuyOutputRawAsync(BigInteger valueWei, BlockParameter? block = null) =>
        QueryAsync<CalculateBuyOutputFunction, BigInteger>(
            new() { Amount = ValidatePositive(valueWei, nameof(valueWei)) }, block);

    public Task<BigInteger> CalculateSellInputRawAsync(BigInteger amountOutWei, BlockParameter? block = null) =>
        QueryAsync<CalculateSellInputFunction, BigInteger>(
            new() { Amount = ValidatePositive(amountOutWei, nameof(amountOutWei)) }, block);

    public Task<BigInteger> CalculateSellOutputWeiAsync(BigInteger amountInRaw, BlockParameter? block = null) =>
        QueryAsync<CalculateSellOutputFunction, BigInteger>(
            new() { Amount = ValidatePositive(amountInRaw, nameof(amountInRaw)) }, block);

    public Task<BigInteger> CalculateBuyInputWeiAsync(BigInteger supplyRaw, BigInteger reserveWei,
        BigInteger crr, BigInteger amountOutRaw, BlockParameter? block = null) =>
        QueryAsync<CalculateBuyInputCustomFunction, BigInteger>(new()
        {
            Supply = ValidateUint256(supplyRaw, nameof(supplyRaw)),
            CustomReserve = ValidateUint256(reserveWei, nameof(reserveWei)),
            CustomCrr = ValidateUint256(crr, nameof(crr)),
            AmountOut = ValidatePositive(amountOutRaw, nameof(amountOutRaw))
        }, block);

    public Task<BigInteger> CalculateBuyOutputRawAsync(BigInteger supplyRaw, BigInteger reserveWei,
        BigInteger crr, BigInteger amountInWei, BlockParameter? block = null) =>
        QueryAsync<CalculateBuyOutputCustomFunction, BigInteger>(new()
        {
            Supply = ValidateUint256(supplyRaw, nameof(supplyRaw)),
            CustomReserve = ValidateUint256(reserveWei, nameof(reserveWei)),
            CustomCrr = ValidateUint256(crr, nameof(crr)),
            AmountIn = ValidatePositive(amountInWei, nameof(amountInWei))
        }, block);

    public Task<BigInteger> CalculateSellInputRawAsync(BigInteger supplyRaw, BigInteger reserveWei,
        BigInteger crr, BigInteger amountOutWei, BlockParameter? block = null) =>
        QueryAsync<CalculateSellInputCustomFunction, BigInteger>(new()
        {
            Supply = ValidateUint256(supplyRaw, nameof(supplyRaw)),
            CustomReserve = ValidateUint256(reserveWei, nameof(reserveWei)),
            CustomCrr = ValidateUint256(crr, nameof(crr)),
            AmountOut = ValidatePositive(amountOutWei, nameof(amountOutWei))
        }, block);

    public Task<BigInteger> CalculateSellOutputWeiAsync(BigInteger supplyRaw, BigInteger reserveWei,
        BigInteger crr, BigInteger amountInRaw, BlockParameter? block = null) =>
        QueryAsync<CalculateSellOutputCustomFunction, BigInteger>(new()
        {
            Supply = ValidateUint256(supplyRaw, nameof(supplyRaw)),
            CustomReserve = ValidateUint256(reserveWei, nameof(reserveWei)),
            CustomCrr = ValidateUint256(crr, nameof(crr)),
            AmountIn = ValidatePositive(amountInRaw, nameof(amountInRaw))
        }, block);

    private Task<T> QueryAsync<TFunction, T>(TFunction function, BlockParameter? block)
        where TFunction : FunctionMessage, new() =>
        _client.Web3.Eth.GetContractQueryHandler<TFunction>().QueryAsync<T>(
            TokenAddress, function, block ?? BlockParameter.CreateLatest());

    private static BigInteger ValidatePositive(BigInteger value, string parameter)
    {
        ValidateUint256(value, parameter);
        if (value.IsZero)
            throw new ArgumentOutOfRangeException(parameter, "Amount must be a positive uint256");
        return value;
    }

    private static BigInteger ValidateUint256(BigInteger value, string parameter)
    {
        if (value < 0 || value > MaxUint256)
            throw new ArgumentOutOfRangeException(parameter, "Value must fit uint256");
        return value;
    }
}

// The one-argument view overloads are defined in DecimalToken.json.
[Function("creator", "address")]
public sealed class CreatorFunction : FunctionMessage;

[Function("identity", "string")]
public sealed class IdentityFunction : FunctionMessage;

[Function("reserve", "uint256")]
public sealed class ReserveFunction : FunctionMessage;

[Function("crr", "uint256")]
public sealed class CrrFunction : FunctionMessage;

[Function("minTotalSupply", "uint256")]
public sealed class MinTotalSupplyFunction : FunctionMessage;

[Function("maxTotalSupply", "uint256")]
public sealed class MaxTotalSupplyFunction : FunctionMessage;

[Function("getCurrentPrice", "uint256")]
public sealed class CurrentPriceFunction : FunctionMessage;

[Function("calculateBuyInput", "uint256")]
public sealed class CalculateBuyInputFunction : FunctionMessage
{
    [Parameter("uint256", "amount", 1)] public BigInteger Amount { get; set; }
}

[Function("calculateBuyOutput", "uint256")]
public sealed class CalculateBuyOutputFunction : FunctionMessage
{
    [Parameter("uint256", "amount", 1)] public BigInteger Amount { get; set; }
}

[Function("calculateSellInput", "uint256")]
public sealed class CalculateSellInputFunction : FunctionMessage
{
    [Parameter("uint256", "amount", 1)] public BigInteger Amount { get; set; }
}

[Function("calculateSellOutput", "uint256")]
public sealed class CalculateSellOutputFunction : FunctionMessage
{
    [Parameter("uint256", "amount", 1)] public BigInteger Amount { get; set; }
}

[Function("calculateBuyInput", "uint256")]
public sealed class CalculateBuyInputCustomFunction : FunctionMessage
{
    [Parameter("uint256", "supply", 1)] public BigInteger Supply { get; set; }
    [Parameter("uint256", "customReserve", 2)] public BigInteger CustomReserve { get; set; }
    [Parameter("uint256", "customCrr", 3)] public BigInteger CustomCrr { get; set; }
    [Parameter("uint256", "amountOut", 4)] public BigInteger AmountOut { get; set; }
}

[Function("calculateBuyOutput", "uint256")]
public sealed class CalculateBuyOutputCustomFunction : FunctionMessage
{
    [Parameter("uint256", "supply", 1)] public BigInteger Supply { get; set; }
    [Parameter("uint256", "customReserve", 2)] public BigInteger CustomReserve { get; set; }
    [Parameter("uint256", "customCrr", 3)] public BigInteger CustomCrr { get; set; }
    [Parameter("uint256", "amountIn", 4)] public BigInteger AmountIn { get; set; }
}

[Function("calculateSellInput", "uint256")]
public sealed class CalculateSellInputCustomFunction : FunctionMessage
{
    [Parameter("uint256", "supply", 1)] public BigInteger Supply { get; set; }
    [Parameter("uint256", "customReserve", 2)] public BigInteger CustomReserve { get; set; }
    [Parameter("uint256", "customCrr", 3)] public BigInteger CustomCrr { get; set; }
    [Parameter("uint256", "amountOut", 4)] public BigInteger AmountOut { get; set; }
}

[Function("calculateSellOutput", "uint256")]
public sealed class CalculateSellOutputCustomFunction : FunctionMessage
{
    [Parameter("uint256", "supply", 1)] public BigInteger Supply { get; set; }
    [Parameter("uint256", "customReserve", 2)] public BigInteger CustomReserve { get; set; }
    [Parameter("uint256", "customCrr", 3)] public BigInteger CustomCrr { get; set; }
    [Parameter("uint256", "amountIn", 4)] public BigInteger AmountIn { get; set; }
}
