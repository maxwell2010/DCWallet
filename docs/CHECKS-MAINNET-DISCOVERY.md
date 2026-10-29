# Mainnet Checks - read-only discovery, 29.10.2026

Source: current official `decimalteam/dsc-js-sdk` checkout, commit `c7a72278551c517d3c0a37e170f844e8176e385f`, `src/endpoints.ts`, `checksAddresses[NETWORKS.MAINNET]`.

The published candidate is `0x9c3326594b49AFa24db9b988Cda1258d0dcb4e40`. Through the official mainnet RPC, chain ID 75, it has 6318 runtime bytes and SHA256 `7a9a364077d8ba3f03e77cfb09e79599faea344568ffa245903ccdb90691139e`. This exactly equals the pinned testnet Checks implementation runtime, rather than the testnet proxy runtime. The current SDK `getImplementation()` and `owner()` calls both returned the zero address. Contract Center lookups for `checks` and `check` did not produce a usable registered address.

This proves the candidate exists and shares the observed implementation runtime. It does not prove that this address is an initialized mainnet application deployment with valid dependencies. No mainnet transaction was signed or sent. This initial structural inspection alone did not establish behavioral usability. The successful mainnet eth_call follow-up below now supports a pinned direct-runtime wallet profile, with fresh code/chain verification and simulation for each action. The generic draft catalog is not a deployment certification.

Public read result: `artifacts/checks-mainnet-readproof.json`. No secrets were read.

## Second reserve-token lookup on testnet

Token Center resolved from current Contract Center to `0xb8F9c4A26b91a65Ac0D3F34e230a4C9ad09aEF4a`. A bounded read-only `TokenDeployed(address)` scan in eight consecutive 1000-block windows ending at the current head produced no candidates. Four literal token addresses from official JS test files were queried: none yielded a verified live token with positive reserve and CRR. Historical test constants are not proof of a current deployment. No new token was created by this discovery.

## Successful bounded symbol lookup

Further read-only lookup of twelve explicit symbols through current `TokenCenter.tokens(string)` found `testcoin`: `0x66EFc62bbB7b0F2289b4330fa012aA6335D6bB7d`. At block22475817 the RPC returned symbol `testcoin`, name `Testcoin`, decimals18, reserve1671187095067504936wei and CRR10. Runtime is333bytes, SHA256 `19d4bba3f0d6e0d372d17ee7942029771a6495f10825d6131251ddc7aaaedb83`. A quote for0.0001DEL returned2510876083417847946raw; selling that quantity quoted99994614896398wei on the same pinned block. This is a usable second reserve-backed testnet candidate, subject to fresh preflight before any transaction. Proof: `artifacts/second-reserve-token-readproof.json`.

No token creation is needed for this discovery. The current TokenCenter reports `getCommissionSymbol("DCQA20261029B")=250000000000000000wei` (0.25DEL); MIN_RESERVE/MIN_INITIAL_MINT/MIN_CRR/MAX_CRR getters from the older full artifact revert on this deployment. The vendored SDK default creation value is1250DEL, which is not an authoritative current deployed minimum. It must not be spent as an unverified QA default. No creation quote was funded.

The current published Checks ABI exposes only owner/getImpl/nonces and create/redeem/upgrade/ownership writes. It contains no fee/maxLoop/due configuration getters or initialize entry. Zero owner/getImpl alone does not prove that ordinary create/redeem paths are unusable. The follow-up below tests create behavior directly. No guessed getter or initialization call was sent.

## Behavioral simulation at the published mainnet candidate

A follow-up probe at canonical mainnet block34334957 (`0x1a317bbef5b9fac93da94ec52dfc27a80bf2ca1230c21bcc902fb5ba3e6cffa3`) tested behavior rather than inferring it from `owner()` or `getImpl()`.

The public wallet address `[private probe address omitted]` had[private probe balance omitted]DEL. An `eth_call` from that address, using its explicitly scoped nonce0, amount0.001DEL, due block34335957 and one newly generated ephemeral check signer, successfully executed `createChecksDEL`. Its return was the expected ABI `bytes32[]` with exactly one hash: `0x107e80b5902cc6e6b6bb5fc2862771ebf97c88cee44f343d4bbf5073dd48f94e`. The candidate's exact runtime hash remained `7a9a364077d8ba3f03e77cfb09e79599faea344568ffa245903ccdb90691139e`.

Thus zero owner/getImpl does not make this deployment unusable for check creation. A reviewed direct-runtime mainnet profile can use fresh chain/code verification and simulation. This probe does not prove successful mainnet redemption, token/permit variants, or durable state changes. No wallet key was read, no transaction was signed or broadcast, and the ephemeral bearer key was disposed without export. The simulated check was never funded.

The current official Checks ABI has owner/getImpl/nonces views and create/redeem/ownership/upgrade writes. It exposes no fee, max-check count, due-block configuration or initialization getter, so no speculative getter was called.

Proof: `artifacts/mainnet-checks-behavior/mainnet-checks-create-ethcall.json`. Reproducible opt-in probe: `MainnetChecksBehaviorReadTests`, `DC_MAINNET_CHECKS_READ=1`; it contains no broadcast call.
