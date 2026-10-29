# Isolated Safe QA contracts

These contracts are test fixtures for chain202020 only. Both hard-code the public designated QA owner. QaSignatureOwner checks low-s ECDSA signatures for the exact transaction digest and exposes both legacy bytes and EIP-1271 bytes32 hooks. QaDelegateTarget has no storage variables or writes, no generic arbitrary calls, and returns only one wei to that owner.

Compiler: solc0.8.30+commit.73712a01, optimizer200, EVM Paris. SafeQa.compiled.json includes creation/runtime bytecode, ABI, both source maps, source SHA256 and runtime SHA256. Reproduce from repository root with node tests/DCWallet.Tests/SafeQaFixtures/compile.cjs <local-path-to-solc-package>. No compiler download is performed by this script. Review the exact source and compiler inputs before deployment.

SafeAdvancedNetworkScenario is opt-in through the parent campaign only. Preparing these files does not constitute a deployment or network validation. Native CREATE deployment uses the root-controlled journaled deployment helper; no mnemonic or key belongs in these fixtures.
