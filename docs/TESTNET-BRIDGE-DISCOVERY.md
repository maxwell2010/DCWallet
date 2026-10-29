# Bridge-v2 testnet discovery

Checked 2026-10-29 without sending funds. Existing stand is Decimal chain202020. No bridge-v2 deployment profile or second-chain route was verified.

Primary source inspected: local official dsc-js-sdk snapshot src/endpoints.ts and src/subgraph/queries.ts getBridgeContracts/getBridgeETHContracts/getBridgeBSCContracts. The SDK discovers addresses from bridges{id implementation}; it supplies no static testnet bridge-v2 contract addresses. The query endpoints are:

- https://testnet-thegraph.decimalchain.com/subgraphs/name/bridge
- https://testnet-thegraph.decimalchain.com/subgraphs/name/bridgeETH
- https://testnet-thegraph.decimalchain.com/subgraphs/name/bridgeBSC

The parent observed bounded12second timeouts for these endpoints. Contract Center bridge was unregistered. An additional bounded official testnet API/OpenAPI investigation could not retrieve a response: contracts?search=bridge, contracts?name=bridge, /swagger.json, /api-json, /swagger/doc.json all timed out6seconds. Those are discovery probes, not claims that the endpoints exist or that no bridge exists. Public results are in artifacts/bridge-discovery/api-probes.jsonl.

Search of official SDK/own SDK source and public official-domain indexed results provided no authoritative source/target deployment addresses. Historical decimal-contracts code uses a different bridge API and is not treated as proof of the current bridge-v2 deployment.

Remaining external inputs before a genuine existing-stand settlement test: actual bridge-v2 source address and runtime/implementation ABI, target-chain RPC/bridge address and available recipient signer/gas, route/asset configuration, guardian/relayer VM production endpoint, finalization and replay behavior. A local mock or unrelated deployment would not close this test.

No bridge deposit, approval or completion was broadcast. Fee estimates or calldata fixtures are not cross-chain settlement proof.

## Current official source recheck

The official JS repository was refreshed to c7a72278551c517d3c0a37e170f844e8176e385f, dated2026-10-28, before this recheck. Current files still discover bridge contracts solely through bridges{id implementation} at the same three Graph endpoints (queries.ts648-680; endpoints.ts149-153,218-227). There are no static bridge-v2 contract addresses or guardian/relayer/VM retrieval endpoints in src/docs. index.ts bridgeCompleteTransfer requires encodedVM supplied by the caller; it does not fetch or produce one.

Current endpoints.ts does identify remote RPC networks: Ethereum test environment uses rpc.sepolia.org and BSC test environment uses bsc-testnet-dataseed.bnbchain.org. These RPC names do not identify counterpart bridge contracts or prove a configured route. getAccountAndBridge uses bridge route IDs1/56/75, so route IDs must not be confused with EVM chain202020/11155111/97. No address or VAA endpoint appeared in the current source refresh; this remains an external bridge discovery/settlement blocker, not an ABI test failure.

## Explicit go-smart-node master check

At the user's requested final source check, git ls-remote https://bitbucket.org/decimalteam/go-smart-node.git HEAD returned9e6c6d718d662083c4a524376a66d2c50bd4bc77. The existing research checkout has exactly that HEAD, dated2026-07-23, so it was inspected as current master without altering upstream.

The tracked contracts/ folders are center, delegation, delegationNft, nft1155, nft721, nftCenter, token, tokenCenter and validator. There is no tracked Bridge/Wormhole/guardian contract or bridge-v2 deployment manifest. A tracked-source search for completeTransfer, encodedVM, minimalServiceFees, wormhole and guardian has no matches. contracts/center/DecimalContractCenter.json exposes generic getContractAddress(string), setAddress and setContractAddress; it supplies no registered bridge address.

Genesis and migration configuration were inspected at ci-scripts/ansible/roles/decimal-init/templates/genesis-testnet.json.j2 and cmd/genesis_converter/. atomic_swap_pool references there concern the legacy swap module, not a verified bridge-v2 source/target route. The remaining occurrences of bridge language are app/hold_start_backfill.go129 (coin migration to EVM), app/upgrades.go291 (clients/indexers denomination update), cmd/dscd/evm_del_audit.go330 (NFT migration to EVM), plus IBC relayer comments. None provides an EVM bridge-v2 address, guardian VM producer or second-chain deployment.

Conclusion scoped to this current master: the requested missing bridge route/deployment information is not present. Per the user's explicit fallback instruction, bridge settlement is skipped for now. No bridge transaction was sent; this is a skipped external integration, not a passed settlement test.
