> Historical investigation and intermediate results below. Current wallet release and final network outcomes: [VALIDATION-0.2-alpha.2.md](VALIDATION-0.2-alpha.2.md). Safe, Checks and NFT lifecycle now have confirmed testnet results.

# Checks testnet deployment proof

Read-only audit on 2026-10-29. No Checks transaction was sent by this investigator.

The official JS source snapshot is `D:/my_codex/Codex/decimal-research-20260929/dsc-js-sdk-official`, with origin [decimalteam/dsc-js-sdk](https://bitbucket.org/decimalteam/dsc-js-sdk/src/master/). `src/decimalevm/index.ts:1869` derives a bearer signing key from the password and signs EIP-191 over packed `(bytes32 checkHash,address redeemer)`. `call.ts:1082` reads `nonces()`, simulates creation, then submits it. The ABI artifact is `src/decimalevm/abi/Checks.json`.

Current public RPC `https://202020.rpc.thirdweb.com` returned `getImpl()` for the stand Checks proxy `0xb6161caa8bd7167c2fa67b93b041ff9a94d6a735` as `0xbaca5a733002f5323abe4c83c91b87212e7a3ca3`.

| Runtime | Bytes | SHA256 |
| --- | --- | --- |
| Current implementation | 6318 | `7a9a364077d8ba3f03e77cfb09e79599faea344568ffa245903ccdb90691139e` |
| Official JS artifact | 6258 | `34a69775d46478eb7f88732daf49582925584dfb71d406b114185f6452020b74` |

The implementation differs even after removing Solidity CBOR metadata. Its trailer identifies solc0.8.20 and metadata CID `QmNi5pYGoWq2TiBWXvbop2PCK27CsCtKnVpDTRpFWsxm76`. Official testnet metadata requests timed out; ipfs.io/dweb.link failed and the Pinata gateway timed out. Runtime size differences might be compiler or source differences, but that is not deployment identity proof. The scenario's exact identity guard therefore stops before depositing funds.

`ChecksNetworkScenario` is prepared to create one check for0.001tDEL, save an encrypted recovery key before creation, derive the actual hash from the matching `CreatedCheck` receipt, redeem to the same QA owner, and verify amount returned less fee. Redeem status enum semantics are not guessed; the byte is recorded and financial effect is verified. Replay `eth_call` is diagnostic until rejection or a failing status can be proven without a paid replay.

`CheckLifecycleTests` includes an independent ethers6 `Wallet.signMessage` vector proving the C# implementation prefixes EIP-191 exactly once. The bearer key is ephemeral; plaintext keys and redemption signatures are not written to reports.

Evidence: `artifacts/checks-inspect/result.json`, `actual.hex`, `expected.hex`, `metadata-cid.txt`.

## Fresh official Git sources

Both public Bitbucket repositories were fetched read-only successfully over Git on 2026-10-29; no repository scripts were executed.

- `decimalteam/decimal-contracts` HEAD `018a6c6d00616c3e24ae44a8fac429b2ccabbd12`, dated2024-06-03: historical bridge source; no DecimalChecks Solidity source or Hardhat build-info was found.
- `decimalteam/dsc-js-sdk` HEAD `c7a72278551c517d3c0a37e170f844e8176e385f`, dated2026-10-28: current Checks artifact still contains6258runtime bytes and SHA256 `34a69775d46478eb7f88732daf49582925584dfb71d406b114185f6452020b74`, identical to the prior snapshot. Thus current official SDK ABI evidence exists, but its bundled deployment bytecode does not identify the observed6318byte implementation.

Additional metadata routes: w3s403, 4everland timeout, Sourcify partial_match404/full_match timeout; CIDv1 `bafybeiafpwlquict4jlytqhobq6i7tmyx7ccvqcw6kxjcuzsle76ud5lne` on dweb/w3s subdomains429, Lighthouse402 and Filebase timeout. No route produced current Solidity metadata/ABI.

## Explicit testnet compatibility campaign

The parent agent authorized a separate, testnet-only compatibility profile for proxy `0xb6161caa8bd7167c2fa67b93b041ff9a94d6a735`, current implementation `0xbaca5a733002f5323abe4c83c91b87212e7a3ca3` and exact observed runtime SHA256 `7a9a364077d8ba3f03e77cfb09e79599faea344568ffa245903ccdb90691139e`. This is deliberately labeled behavior validation with the current official ABI, not source-bytecode identity verification. Other deployments and mainnet cannot use this test profile.

Before deposit, the scenario checks chain202020, implementation identity, runtime hash, current nonce and a payable eth_call of createChecksDEL for one0.001tDEL check. The decoded result must be a single nonzero bytes32 hash. An encrypted bearer recovery backup and public predicted hash are saved before broadcast. The actual CreatedCheck must match the prediction and bearer signer.

`DC_CHECKS_RECOVERY_DIRECTORY` resumes a saved encrypted bearer inside the campaign root without creating a second check. Replay is checked through a recognized contract revert during preflight, or a separately authorized controlled paid replay of the already redeemed check: the recipient must receive zero additional credit, paying only transaction fee, with a different emitted status. Transport failures are not counted as replay rejection.

No live Checks outcome is claimed by this document; the parent campaign report is authoritative for actual submissions and receipts.

## Creator-scoped nonce: live preflight resolved

A zero-From SDK query returned nonce0. The QA create simulation then reverted with selector `0x756688fe`, which the official ABI identifies as `InvalidNonce()`. Simulating the same call as the contract owner initially succeeded, but this was not an owner restriction: its caller nonce happened to be0.

Explicit `eth_call` From the actual QA creator returned nonce6. With this nonce, the QA createChecksDEL simulation succeeded with96bytes of output and gas estimate `0x26926`. The wrapper `CheckLifecycle.GetNonceForAsync(client,contract,creator)` preserves From and validates chain. A local RPC regression demonstrates creator6 versus omitted-From0. No owner restriction is inferred or enforced.

Evidence: `artifacts/checks-inspect/preflight-roles-public.jsonl` and `preflight-caller-nonce.jsonl`. These are read-only probes, not transaction receipts.

## Live result, 2026-10-29

The pinned testnet compatibility profile passed real creation and redemption of0.001tDEL. CreatedCheck hash matched the prediction, redemption status1 and amount minus fee were verified. Recovered public calldata was rejected on replay through eth_call; no paid replay was needed. The nonce wrapper uses explicit creator From. This validates behavior, not byte-for-byte identity with the official artifact. Public transaction evidence is recorded in VALIDATION-0.2-alpha.2.md.
