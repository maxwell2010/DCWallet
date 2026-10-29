# Testnet Safe profile

Observed 2026-10-29 on chain202020 through public RPC. Official JS SDK snapshot src/endpoints.ts identifies singleton 0xe0f30fccae2f9f9c7efee9af044c436841d466ee and factory 0x4cc406b1713d3df67e739d6f5918b6c98f614a38. Its src/decimalevm/call.ts createMultiSig uses weighted setup(ownersData,weightThreshold,zero,bytes,handler,zero,0,zero) and createProxyWithNonce.

Live code: singleton14325bytes SHA256 f49186973c2c5b4dbcbba188fdc7fffd9c56fbb6ca86fd20aa4e6640987b2b23; factory1984bytes SHA256 bb8b0fe03c6fe89ac3d7e20771fb00ead353aecddf5e974d3bdd6c9f4f485b8f. Profile verifies chain and both hashes before use.

A read-only estimate using weighted QA owner/threshold succeeds: 0.300988984725 tDEL, maximum0.331088954625. This proves simulated creation only. Paid lifecycle results belong to the parent's serialized integration report, not this preflight.

TestnetSafeProfile keeps deployment addresses separate from mainnet. SafeNetworkScenario performs creation, weighted ownership checks, funding, typed EOA execution, on-chain approveHash/approved_hash execution, zero-balance confirmation and a negative insufficient-balance simulation. It never invokes itself or reads secrets; the parent AdvancedTestContext supplies the strict testnet signer and bounded SendAsync.

## Advanced contract-owner and DelegateCall proof - 2026-10-29

The root-controlled public testnet campaign completed six unique transactions across resumed runs. Resumes verified existing receipts and runtime profiles; neither deployment nor Safe funding was repeated.

- QA owner contract: 0x9ee0b592cc8d8451b15c42a2c0e48dca39a4da24. The compiled fixture verifies exact-digest low-s ECDSA signatures by the designated QA owner through both legacy bytes and EIP-1271 bytes32 hooks. It is not an always-true signature validator.
- No-storage delegate target: 0x8338ff0807ce2ab16acc00c0854907fe71ba3942. Its only action returns one wei to the designated QA owner, with no storage writes or generic calls.
- Contract-owner Safe: 0x3076623e219610396eb7da67700af90ddcb73df6, owner weight1, threshold1.
- Call execution: 0x8ea6a54caadd3d238a4bede4659d0c08a46b058a20fd13d8df86ac8654c06173, nonce0.
- DelegateCall execution: 0x47c0b40d8f7de58c80645e8dafb81fd1d2700458f7a339515612a03ef0605254, nonce1.

The completed scenario verified modern EIP-1271 correct/wrong digest responses, Safe rejection of a wrong contract signature, on-chain transaction hash equality for Call and DelegateCall, canonical successful receipts with ExecutionSuccess, final nonce2, zero Safe balance and unchanged owner. The two execution fees totaled0.2077236018tDEL. Root report: artifacts/completion-testnet/run-20261029T051733-ab2859/report.json. Deployment and creation/funding evidence remain in the earlier resumed public reports.

This proves this pinned testnet Safe deployment with the reviewed QA validator contract. Other EIP-1271 handlers, arbitrary DelegateCall targets and weighted combinations still require their own compatibility checks. A standalone magic-value response does not replace Safe execution simulation.

Final independent read-only audit completed after all public submissions stopped:29 unique completion receipts and29 journal rows verified, pending0. This includes both advanced Safe internal ExecutionSuccess proofs decoded from their actual execution calldata. Proof: artifacts/completion-testnet/completion-receipt-audit.json. A separate final-state audit re-read Safe nonce2 and zero balance: artifacts/completion-testnet/completion-final-state.json. These audits performed no signing or broadcasts.
