# Decimal Safe

The wallet uses the weighted Safe ABI in the local Console MintCandy profile `console/src/protocol/sdk_extensions_v1.json` version 0.1.3. It reads `getOwnersWithWeights`, `getWeightThreshold` and `nonce` at one block. Standard owner-count quorum is not substituted when weighted queries fail.

Mainnet75 deployment addresses are taken from that profile. Official contract metadata was checked on 2026-10-29:

- [SafeL2 metadata](https://api.decimalchain.com/api/v1/contracts/0x15949c33775154549d073168c1094c5f3b28b5cb), status `verify`.
- [Factory ABI](https://api.decimalchain.com/api/v1/contracts/compact/0x92466f09d5c82e8ddb8aaa7c5adc63d43111f6c1).

Creation requires exact runtime code hashes from the official mainnet RPC, checked on every preparation:

| Contract | SHA256 of runtime bytes |
| --- | --- |
| SafeL2 | `23413b3f39372f34ba7d3edb306ac16dd7e55e6dcff93b4e9697c7f9750e2601` |
| Factory | `a74bdd377b7e91b67d2af0166c25534664b93b9a89fd60b1a15608c5b5d87fed` |

The factory call is `createProxyWithNonce(singleton, initializer, saltNonce)` with weighted `setup` and zero module/payment/refund parameters. The receipt requires the expected factory's `ProxyCreation` event naming the pinned implementation.

Execution checks the fresh nonce, weighted owner quorum, local versus contract transaction hash, individual EOA signatures or on-chain `approvedHashes`, and a successful boolean simulation before the normal review screen. Receipt confirmation requires the matching `ExecutionSuccess` and rejects matching `ExecutionFailure`. `approveHash` is a separate paid transaction.

Funded creation and execution were subsequently verified on testnet202020: EOA, approved_hash and two owners with weights2+1/threshold3. See VALIDATION-0.2-alpha.2.md and TESTNET-SAFE-PROFILE.md. Contract signature EIP-1271 and delegatecall proposals are unsupported. Registry/profile changes require a new reviewed deployment profile.
