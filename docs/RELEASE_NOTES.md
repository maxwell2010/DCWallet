DC WALLET 0.2.0-alpha.3 - native Decimal desktop alpha.

Adds encrypted private-key import, independent wallet profiles, compatible BIP39 vault migration, weighted Safe contract signatures and DelegateCall, encrypted check files, token check allowance/permit workflows, current NFT ABI compatibility and clear transaction-history descriptions. Safe inner failure is shown separately from outer receipt success.

Validation: 322 Core + 35 Avalonia Headless tests passed; 23 opt-in network/fork tests are separate. This stage confirmed29 new testnet transactions, independently audited canonical receipts and final balances, with pending0. Five local-fork checks covered expiry, roles, reserve-token creation and hold/time travel; local Ganache headers differ from Decimal, so these are not public-network confirmations. No mainnet signing or broadcast occurred.

Token-to-token allowance/permit cycles, ERC721 delegate/withdraw/complete, reserve-token permit stake/withdraw/complete and contract-owner Safe Call/DelegateCall restored their stated baselines. Current Token Center fees are queried instead of old hardcoded creation defaults. Legacy SDK Safe forms are routed to the weighted wallet implementation.

Mainnet NFT delegation is currently paused on the registry contract. The wallet checks pause before preparing new approval/delegation. It does not attempt governance unpause or substitute the testnet combined ABI. Bridge is excluded by user instruction. Rare ABI/role combinations and native Linux/macOS/ARM64 execution still need their recorded validation scope; all118 SDK builders do not mean all possible chain-admin operations are implemented or certified.

Six self-contained binaries are built; Windows x64 passes the final-file self-test. Versioned assets and permanent-name copies share verified SHA256 hashes. DOWNLOADS.json identifies the exact version; SHA256SUMS.txt covers both names. GitHub latest links require a published stable release and exclude draft/prerelease.

See docs/VALIDATION-COMPLETION.md, docs/CAPABILITIES.md and the three ABI audit reports in the tagged source. This remains an unsigned alpha; no public GitHub release has been made.
