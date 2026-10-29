# Decimal Web3 SDK for .NET

An early, independent C# implementation for Decimal EVM. It targets .NET 8 and uses Nethereum for RPC, ABI encoding and signing. This source tree is not a published NuGet release.

## Build

```powershell
dotnet restore csharp/tests/Decimal.Web3.Tests/Decimal.Web3.Tests.csproj
dotnet test csharp/tests/Decimal.Web3.Tests/Decimal.Web3.Tests.csproj
dotnet pack csharp/src/Decimal.Web3/Decimal.Web3.csproj -c Release
```

Add the project to another .NET solution with a project reference, or install a locally built `.nupkg`. The package name is `MintCandy.Decimal.Web3`; the current version is `0.1.0-alpha.1`.

## Networks and Wallets

```csharp
using Decimal.Web3;
using Decimal.Web3.Wallet;

var network = DecimalNetwork.Testnet(); // Mainnet() is the default
var client = new DecimalClient(network);
var phrase = Environment.GetEnvironmentVariable("DECIMAL_TEST_MNEMONIC")
    ?? throw new InvalidOperationException("Set DECIMAL_TEST_MNEMONIC");
var sender = DecimalWallet.DeriveAddress(phrase, index: 0);
var addresses = DecimalWallet.DeriveAddresses(phrase); // indexes 0..9
var balanceWei = await client.GetBalanceWeiAsync(sender);
Console.WriteLine(Units.Format(balanceWei) + " DEL");
```

Do not save phrases in source, logs or command history. A custom RPC can be selected with `DecimalNetwork.Custom(name, chainId, rpcUrl)`. Mainnet and testnet defaults use Decimal endpoints only. The C# package has no project-owned RPC or API URL.

Testnet system contract addresses must be resolved from its Contract Center before use:

```csharp
var resolved = await client.ResolveSystemContractsAsync();
var staking = new Decimal.Web3.Staking.DelegationDrafts(resolved);
```

For a verified reservless ERC20 token:

```csharp
var token = new Decimal.Web3.Tokens.ReservelessTokenService(client, tokenAddress);
var info = await token.GetInfoAsync();
Console.WriteLine($"{info.Metadata.Symbol}: supply={Units.Format(info.Metadata.TotalSupplyRaw)}, cap={Units.Format(info.CapRaw)}");
var mintDraft = await token.BuildMintCheckedAsync(sender, sender, Units.Parse("1"));
var burnDraft = await token.BuildBurnCheckedAsync(sender, Units.Parse("1"));
// Quote and sign either draft separately before any explicit broadcast.
```

## Fee, Sign and Send

```csharp
using Decimal.Web3.Transactions;

using var signer = DecimalWallet.CreateTransactionSigner(phrase, client.Network.ChainId);
var draft = TransactionDraft.Del(signer.Address, recipient, "0.01", memo: "hello");
var quote = await client.EstimateFeeAsync(draft, exact: true);
Console.WriteLine($"Estimated {quote.EstimatedFeeDel} DEL; reserve {quote.MaximumFeeDel} DEL");
if (!quote.CanAfford) throw new InvalidOperationException("Insufficient DEL");

var signed = await client.SignWithSignerAsync(draft, quote, signer);
Console.WriteLine($"Signed transaction hash: {signed.Hash}");
// Only this explicit call broadcasts:
var pending = await client.BroadcastAsync(signed);
var result = await client.WaitForReceiptAsync(pending.Hash);
Console.WriteLine($"{result.Hash} block={result.BlockNumber} success={result.Success} paid={result.PaidFeeDel}");
```

`EstimateFeeAsync` uses `eth_estimateGas` and the RPC gas-price oracle. `exact: true` uses the estimated gas as the limit; the default reserves 10% more gas. A quote is not a guarantee of final fee or successful execution. The fee is charged in DEL, including token and NFT transactions. No transaction is sent by tests unless a separate caller explicitly invokes `BroadcastAsync`.

Keep one `DecimalTransactionSigner` for a short series of transactions from the same wallet and network. It derives the account once; `SignWithSignerAsync` checks the sender and chain ID before signing. The original `SignAsync(draft, quote, phrase)` remains available for one-off calls and now derives the account only once per call. Disposing the signer releases its account reference but cannot guarantee erasure of key material from managed memory. Do not retain a signer longer than needed, and never log the phrase or signed raw transaction.

`DecimalTransactionException` exposes a stable `Code` and a short `UserMessageRu`; the original RPC error stays in `InnerException`. If `BroadcastAsync` loses its RPC response, the exception's `TransactionHash` (or `signed.Hash`) identifies the exact signed transaction. Check that hash on chain before considering a retry. A broadcast timeout does not prove that the transaction failed.

## Current Scope

| Area | Implemented | Network verified |
| --- | --- | --- |
| BIP39 / BIP44 wallet indexes 0..9, offline signatures | Yes | Offline tests |
| DEL balance, single transfer with memo, fee, sign, broadcast, receipt | Yes | One testnet self-transfer confirmed |
| ERC20 balance, metadata, allowance, transfer/approve/transferFrom drafts | Yes | One self-transfer confirmed on testnet |
| ERC721 and ERC1155 reads, transfer/approval drafts | Yes | ERC1155 single and batch self-transfers confirmed on testnet |
| DEL multisend, one recipient as a plain transfer | Yes | Two-recipient memo multisend confirmed on testnet |
| Delegation DEL/ERC20, holds, withdrawal/transfer/reset and permit drafts | Partial | DEL delegate and unbond confirmed on testnet |
| Direct, hold and frozen stake reads | Yes | Testnet read-only RPC |
| Token buy/sell/convert/burn and creation/update drafts | Partial | Reservless create, mint and burn confirmed on testnet |
| Decimal NFT collection/mint/burn/delegation/withdraw/transfer drafts | Partial | ABI tests only |
| Validator management drafts and status reads | Partial | ABI tests only |
| Safe multisig payloads, EIP-712 signatures and execution draft | Partial | Offline tests only; no Safe deployment or execution confirmed |
| Bridge-v2 drafts and minimum service fee read | Partial | ABI tests only |
| Check nonce/owner/implementation reads and create/redeem drafts | Partial | Offline ABI tests only |
| Reservable-token mint, ERC20 multisend | Not yet | Not tested |

`BuildMintReservelessDraft` is available for a verified reservless token; minting a reservable DecimalToken remains unsupported until its contract ABI is confirmed. Creation receipts can be decoded with `TokenCreationReceiptReader` to obtain the new token address. The old Python-profile `addReserveByDEL` and `setTokenURI` NFT calls require an explicit `allowUnverifiedAbi` flag because they are absent from the pinned official NFT ABI. ERC20 multisend is intentionally unsupported because giving allowance to an arbitrary-call Multicall contract is unsafe. The default public testnet RPC timed out at the chain-ID request; an alternate public testnet RPC supported the testnet transactions listed in the validation report. No mainnet transaction has been broadcast by this C# package. For feature-complete Decimal workflows continue using the Python SDK while this port is completed and validated.

See [validation results](docs/VALIDATION.md) and the [source map](docs/UPSTREAM_SOURCES.md) before using draft-only methods.
The [compatibility backlog](docs/COMPATIBILITY_BACKLOG.md) tracks the remaining JS, Go and node parity work. An additional testnet dry-run confirmed gas estimation for `complete([224])` on a matured frozen stake; it did not send or complete the withdrawal.

License: MIT. Project: [MintCandy](https://mintcandy.ru/). Maintainer: [@Maxwell2019](https://t.me/Maxwell2019).
