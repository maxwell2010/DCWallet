# ABI readiness - validators, Safe, multisend and transactions

Date: 2026-10-29. Static audit plus explicitly identified public readonly and confirmed lifecycle evidence. No new public transaction was sent by this audit.

## Scope and evidence

There are22 catalog entries in these groups:13 ValidatorService,3 Multisig (including the checked async execution builder),3 Multisend and3 TransactionDraft. Twenty-one synchronous fixtures were inspected; the SafeQueries async builder has separate controlled-RPC tests. A fixture exercises one argument/branch, not every branch of its method.

Sources: vendored SDK, current official JS checkout c7a72278551c517d3c0a37e170f844e8176e385f, current go-smart-node9e6c6d718d662083c4a524376a66d2c50bd4bc77 contracts/validator/DecimalMasterValidator.json, and Console contracts/safe-multisend/artifact.json. Current JS loads most deployed ABIs from v1/contracts/compact/{address} at runtime through src/decimalevm/contract.ts; its method names alone do not provide an independently pinned ABI. Temporary selector evidence: artifacts/abi-safe-validator-selectors.json.

## Validator selectors

All13 current SDK draft selectors exactly match the node ABI. No selector mismatch was found in these13 methods.

| SDK operation | ABI signature | Selector |
| --- | --- | --- |
| AddCandidateDel | addCandidateDEL(address,string) | 0xda4d56f0 |
| AddCandidateWithToken | addCandidate(address,string,(address,uint256)) | 0xce6416e7 |
| AddValidatorDel | addValidatorDEL(address,string) | 0x91234204 |
| AddValidatorWithToken | addValidator(address,string,(address,uint256)) | 0x8e7d0f53 |
| AddValidators | addValidators((address,string)[]) | 0xcdba1d92 |
| ApproveCandidates | approveCandidates(address[]) | 0x8c9ad016 |
| AddPenalty | addPenalty(address,uint16) | 0x0af66c5a |
| RemoveValidator | removeValidator(address) | 0x40a141ff |
| UpdateValidatorMeta | updateValidatorMeta(address,string) | 0xf2a19f61 |
| PauseSelf | pauseSelf() | 0xaf69ca76 |
| UnpauseSelf | unpauseSelf() | 0x9391d5e8 |
| PauseValidator | pauseValidator(address) | 0x0ae65e7a |
| UnpauseValidator | unpauseValidator(address) | 0x0437e4fd |

Public readonly probe on chain202020 resolved master0x5dBE5eb59D73f596E19A8f64f226C71BbC8Ea36F, implementation0xd1892c59bc3b812b8c94e20b55441999f45de962, and global owner0x35119df12afdf848b7ef2536af2411ab0a611c45. QA owner is a different address. addCandidateDEL(QA address, QA metadata) accepted eth_call value1wei and0.001DEL; zero value returned selector0x1f2a2005, not decoded by this node ABI. This establishes simulated positive inputs, not an exact universal minimum or a paid candidate lifecycle. removeValidator(QA address) returned NotMember0x291fc442, not a proof of successful cleanup of a newly created candidate. No consensus node was joined, activated or modified. Evidence: artifacts/validator-readonly-preflight.json.

### Validator branch coverage still required

- Current catalog fixtures encode all13 methods. ExtendedDraftTests independently checks pauseSelf, unpauseSelf and removeValidator selectors.
- SDK owner-gated offline builders compare sender against a caller-supplied owner value. They do not query the real global owner; a fixture cannot establish on-chain authorization.
- Positive role branches for addValidator/addValidators/approveCandidates/addPenalty, member pause/unpause/update/remove and a safely reversible candidate lifecycle still need exact deployed-state fork fixtures or a designated disposable controlled validator. Public requests for production keys are unnecessary and inappropriate.
- ForkCompletionTests.TokenCenterRoleChecksRejectQaAndPermitActualOwnerOnFork covers TokenCenter ownership only. It does not certify MasterValidator roles.
- Negative cases should separately cover wrong owner, missing membership, duplicate candidate, empty metadata, zero/overflow stake, penalty boundaries, wrong chain and state changes. Existing generic encoding fixtures do not prove these live rejection branches.

### Explicit scope exclusion: global network governance

The node ABI additionally contains five global network-governance write functions. These are explicitly excluded from the ordinary-wallet/own-validator-control scope. They have no current ValidatorService draft builder or dedicated wallet form:

| Function | Selector | Classification |
| --- | --- | --- |
| renounceOwnership() | 0x715018a6 | Unimplemented privileged administration; destructive ownership loss |
| transferOwnership(address) | 0xf2fde38b | Unimplemented privileged administration |
| upgrade(address,bytes) | 0xc987336c | Unimplemented privileged proxy administration |
| setRewardPerBlock(uint256) | 0xbb872b4a | Unimplemented privileged administration |
| clearRewardPerBlock() | 0xdca960cb | Unimplemented privileged administration |

These are supported by the inspected ABI, not ABI-unsupported. The explicit scope exclusion does not mean they were implemented or tested. The wallet does not claim every global network administrative operation. ABI presence alone does not establish public testnet owner authority. Ordinary own-validator actions remain represented by the13 listed SDK draft methods; selector equality does not establish their live role branches.

## Safe: catalog versus wallet-owned implementation

The distinction matters:

| Path | Result |
| --- | --- |
| SDK SafeTransactions.BuildApproveHashDraft | Encodes approveHash(bytes32), selector0xd4d9bdcd. This is the standard Safe interface, different from Decimal weighted Safe |
| Wallet WeightedSafeDrafts.Approve | Encodes approveHash((address,uint256,bytes,uint8,uint256,uint256,uint256,address,address,uint256)), selector0xdf61e711; actual Decimal testnet approval/approved_hash execution confirmed |
| SDK SafeQueries.BuildExecutionDraftAsync | Reads getThreshold() and checks signature count. Public eth_call getThreshold() on the verified Decimal QA Safe returned0x; getOwners() returned its actual owner list. This SDK path does not implement weighted quorum |
| SDK SafeTransactions.BuildExecutionDraft | execTransaction selector0x6a761202 matches, but SDK supports only Call/EOA signatures and does not establish live weighted quorum or deployed compatibility |
| Wallet ShowSafe / SafeAdvancedTransactions | Weighted reader, actual nonce/hash equality, mixed EOA/approved_hash/contract packing, Call/DelegateCall, explicit DelegateCall consent and target code check, target Safe simulation, internal ExecutionSuccess receipt proof |

The wallet-owned implementation is separate from the118-entry SDK catalog. It must not be counted as proof that each generic SDK Safe form is compatible. Initial audit found generic Multisig entries selectable in the operations workbench. Root subsequently fixed the public UI: the common account menu opens ShowSafe directly, ShowOperations("Multisig") redirects to ShowSafe, and Multisig is excluded from generic group choices. The118-entry SDK catalog remains available for fixture regression. SafeNavigationRegressionTests adds three passing headless regressions covering the actual menu, every Tools group and the legacy route; all lead to the weighted Safe workspace or exclude legacy SDK editors. This audit did not change app/vendor files.

Dynamic signature packing matches current official JS buildSignatureBytes, including relative offsets and unpadded dynamic tails. Independent ethers6 operation1 hash vector is asserted by SafeAdvancedTests. Confirmed public scenarios include EOA, approved_hash, weighted2+1/threshold3, contract-owner Call and DelegateCall, valid/invalid modern EIP-1271 digest and wrong-signature rejection. Completion receipt audit verified both advanced inner hashes independently; final Safe nonce2 and balance0. See TESTNET-SAFE-PROFILE.md and artifacts/completion-testnet/completion-receipt-audit.json.

## Multisend

- CandySafeMultisend.sendToken(address,address[],uint256[],bytes)0x1c20e0df and sendTokenWithPermit(address,address[],uint256[],bytes,uint256,uint8,bytes32,bytes32)0x1f2a4920 exactly match the reviewed Console artifact. They are wallet/Console extensions, not functions of the node MasterValidator ABI or native Decimal transaction types.
- MultisendDel multi-recipient branch encodes aggregate((address,uint256,bytes)[])0x1acaa198, matching current official JS Multicall.json. One-recipient branch produces a normal native transfer with optional memo. Its catalog fixture takes that single-recipient branch, so its first memo bytes must not be mistaken for a function selector.
- The pinned runtime/chain check and exact allowance/permit flow matter beyond selector equality. The generic arbitrary-call aggregate is not an ERC20 authorization target. Erc20BatchTransfer returns multiple direct drafts/plans and is intentionally outside the draft-returning catalog.
- Table import/export and field validation have separate tests; they do not establish token-specific permit behavior or every failure mode of every ERC20.

## Native transaction builders

TransactionDraft.Del and BurnDel have no ABI selector. Memo is plain UTF-8 transaction data; BurnDel is a positive-value transfer to the zero address. TransactionDraft.ContractCall passes caller-provided calldata after syntax/value checks and cannot by itself establish an ABI or role. Neither arbitrary contract calls nor native burn should be labeled as successful network execution merely because their fixture exists.

## Readiness conclusion

All13 catalog validator selectors match the node ABI, both reviewed token multisend selectors match Console, and native transfer builders are correctly distinguished from ABI methods. Two SDK Safe paths have concrete deployed-profile incompatibilities; they are now excluded from the generic public UI and routed to the verified dedicated weighted Safe workflow. Five global MasterValidator governance writes are explicitly outside wallet scope, rather than falsely labeled implemented. Remaining validator positive/negative role branches are evidence gaps, not grounds to request production keys or mutate production validators. A readiness claim must retain these scope limits and distinguish selector equality from exercised authorization branches.
