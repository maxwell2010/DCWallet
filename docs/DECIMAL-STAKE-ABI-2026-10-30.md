# Decimal Stake ABI compatibility - 2026-10-30

Source reviewed: [DecimalDelegation.go at a46cb48d](https://github.com/decimalteam/go-smart-node/blob/a46cb48d7965c1a64a495cebb22e735dfe4b9da3/contracts/delegation/DecimalDelegation.go).

DC WALLET does not call getStakes(bytes32[]). It reads individual token/native/NFT stakes, holds and frozen requests. These share the affected Stake tuple. Existing single-tuple reads used a seven-field DTO and discarded the eighth word. The old frozen reader supported both sizes but cached its latest-block layout and called the eighth word extraIndex.

The wallet now decodes each response using its actual length. Legacy Stake has seven words; current Stake has eight. FrozenStake has ten or eleven words. Frozen arrays validate offset, count and exact stride. Addresses and uint8 values require zero ABI padding. Unsupported or malformed replies throw and retain the existing incomplete-data indication, rather than manufacturing a zero balance. No layout is cached across latest or historical reads.

Amount remains word 3, holdTimestamp remains word 6. Nullable HoldStartTime comes from word 7 and is unavailable for legacy replies. It is preserved in wallet positions and extended withdrawal receipts, and shown as the hold start date where available. Amount conversion, token decimals, maturity checks and transaction arguments are unchanged. No division by 1000 is introduced. Combined NFT testnet positions have their own ABI and remain unchanged; mainnet NFT staking remains paused.

Validator catalog reads use the existing API rather than local getStakes tuple aggregation. Public discovery caches contain coordinates only; balances remain block-pinned RPC reads followed by snapshot validation. The existing partial-source and stale-load handling is preserved. This patch does not add or change RPC failover policy.

Validation:

- 14 regression cases cover both layouts, token/NFT types, exact amounts and hold times, frozen arrays, malformed payloads, upgrade within the same reader instance, historical blocks, and read-only RPC routing.
- Full core suite: 361 passed, 23 opt-in network/fork tests skipped.
- UI suite: 40 passed. Application build: no warnings or errors.
- Read-only mainnet probe on node.decimalchain.com and 75.rpc.thirdweb.com at block 34349868 returned eight-word getStake responses on chain 75. Empty public coordinates were used to check deployed response layout only. This is not evidence of populated hold or NFT position lifecycle validation. No transaction was signed or broadcast.

Current hashes and original snapshot hashes for vendored patches are recorded in vendor/SOURCE.json. Upstream SDK checkout is unchanged. No public release or deployment was performed as part of this compatibility check.
