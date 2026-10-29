namespace Decimal.Web3.Multisig;

/// <summary>An EIP-712 EOA signature over one SafeTx hash.</summary>
public sealed record SafeSignature(string Signer, string Data);
