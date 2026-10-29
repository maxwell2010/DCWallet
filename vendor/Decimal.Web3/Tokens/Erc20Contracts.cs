using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Decimal.Web3.Tokens;

[Function("name", "string")]
public sealed class Erc20NameFunction : FunctionMessage;

[Function("symbol", "string")]
public sealed class Erc20SymbolFunction : FunctionMessage;

[Function("decimals", "uint8")]
public sealed class Erc20DecimalsFunction : FunctionMessage;

[Function("totalSupply", "uint256")]
public sealed class Erc20TotalSupplyFunction : FunctionMessage;

[Function("version", "string")]
public sealed class Erc20VersionFunction : FunctionMessage;

[Function("DOMAIN_SEPARATOR", "bytes32")]
public sealed class Erc20DomainSeparatorFunction : FunctionMessage;

[Function("nonces", "uint256")]
public sealed class Erc20NonceFunction : FunctionMessage
{
    [Parameter("address", "owner", 1)]
    public string Owner { get; set; } = string.Empty;
}

[Function("balanceOf", "uint256")]
public sealed class Erc20BalanceOfFunction : FunctionMessage
{
    [Parameter("address", "account", 1)]
    public string Account { get; set; } = string.Empty;
}

[Function("allowance", "uint256")]
public sealed class Erc20AllowanceFunction : FunctionMessage
{
    [Parameter("address", "owner", 1)]
    public string Owner { get; set; } = string.Empty;

    [Parameter("address", "spender", 2)]
    public string Spender { get; set; } = string.Empty;
}

[Function("transfer", "bool")]
public sealed class Erc20TransferFunction : FunctionMessage
{
    [Parameter("address", "to", 1)]
    public string To { get; set; } = string.Empty;

    [Parameter("uint256", "amount", 2)]
    public BigInteger Amount { get; set; }
}

[Function("approve", "bool")]
public sealed class Erc20ApproveFunction : FunctionMessage
{
    [Parameter("address", "spender", 1)]
    public string Spender { get; set; } = string.Empty;

    [Parameter("uint256", "amount", 2)]
    public BigInteger Amount { get; set; }
}

[Function("transferFrom", "bool")]
public sealed class Erc20TransferFromFunction : FunctionMessage
{
    [Parameter("address", "from", 1)]
    public string From { get; set; } = string.Empty;

    [Parameter("address", "to", 2)]
    public string To { get; set; } = string.Empty;

    [Parameter("uint256", "amount", 3)]
    public BigInteger Amount { get; set; }
}
