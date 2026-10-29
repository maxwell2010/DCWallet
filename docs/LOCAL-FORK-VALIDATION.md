# Local Decimal EVM fork validation - 29.10.2026

Five opt-in ForkCompletionTests passed in the final run, 0 failures. These are LOCAL FORK checks, not new public-testnet receipts.

- Pre-mutation profile: chain202020, Ganache7.9.2, QA balance, combined delegation proxy runtime and ERC1967 implementation storage matched public pinned head0x156f471.
- DEL check creation followed by locally mined expiry blocks: the contract rejected redemption after dueBlock.
- Token Center owner role: QA ownership write reverted; the actual unlocked owner could transfer ownership in the isolated fork.
- Reserve token creation: real local EVM deployment, current tuple TokenDeployed event, metadata, creator, holder balance and positive reserve/CRR were verified.
- DEL hold: delegate0.001, verify exact held amount/timestamp, move local time forward two years, reset hold, verify held amount0 and restored ordinary stake delta.

Each mutating case uses evm_snapshot/evm_revert. Local account balances were changed for gas and creation reserve; no public account was funded, no seed was read, no signed transaction was sent upstream. Local receipts are in ignored artifacts/fork-qa/receipts.jsonl and the captured token creation receipt is in token-create-receipt.json. TRX: tests/DCWallet.Tests/TestResults/fork-completion-final.trx.

## Compatibility limits

The public RPC does not provide earliest, block0 or block1. Ganache initialization therefore uses the actual pinned-head metadata object only when its earliest-metadata request returns null. Code, storage, balance, nonce, call and receipt responses are never substituted. Upstream methods matching send/sign/personal/wallet/miner/evm mutations are rejected by transport.

Ganache reconstructs the Ethereum fork header: its hash differs from Decimal's canonical upstream header. profile-proof.json records both hashes and canonicalHeaderMatches=false. The verified state reads match; this is not a canonical Decimal header or consensus proof. Ganache does not execute Decimal Cosmos consensus hooks, validator lifecycle or reward distribution. These tests therefore cannot certify full chain behavior.

The owned local server PID37064 was stopped after tests. It is not left running. A future launch script now serializes upstream reads to at most2 requests/second to avoid interfering with public-network cleanup. Public RPC use by this agent is paused.

## Wallet compatibility found and fixed

The deployed TokenDeployed event has an address plus Token tuple. The vendored SDK reader only recognized the older address-only event. Core/CurrentTokenCreationReceiptReader supports both, with strict receipt status, emitter, duplicate-address and optional expected-creator checks. Three regression tests use a sanitized actual fork event, no runtime artifacts dependency. An address-only event cannot prove creator and is rejected when expectedCreator is requested.
