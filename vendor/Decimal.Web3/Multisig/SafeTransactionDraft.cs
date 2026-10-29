using System.Numerics;
using Decimal.Web3.Transactions;

namespace Decimal.Web3.Multisig;

/// <summary>
/// Unsigned SafeTx payload. Nonce is the Safe contract nonce, not the owner's EOA nonce.
/// This is not an Ethereum transaction and cannot be broadcast directly.
/// </summary>
public sealed record SafeTransactionDraft(
    string SafeAddress,
    string To,
    BigInteger ValueWei,
    string Data,
    byte Operation,
    BigInteger SafeTxGas,
    BigInteger BaseGas,
    BigInteger GasPriceWei,
    string GasToken,
    string RefundReceiver,
    BigInteger Nonce)
{
    public const string ZeroAddress = TransactionDraft.BurnAddress;
}
