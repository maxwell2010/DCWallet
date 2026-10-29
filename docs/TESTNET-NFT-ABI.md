> Historical investigation and intermediate results below. Current wallet release and final network outcomes: [VALIDATION-0.2-alpha.2.md](VALIDATION-0.2-alpha.2.md). Safe, Checks and NFT lifecycle now have confirmed testnet results.

# Testnet NFT delegation ABI investigation

Read-only investigation on 2026-10-29. No SDK files were changed and no transaction was broadcast.

The old Contract Center `delegation-nft` address is `0x07e2ad4dfc91412de09e33e4650254948b21a20c`. A public C# read returned the explicit revert: `DecimalDelegationNFT: deprecated, use DecimalDelegation at 0x0d8E8CE36DADe42682dd8cC6288D6A8D324bac2c`. This does not establish ABI compatibility at the suggested replacement.

Official testnet metadata confirms both are verified **proxies**, with proxy ABI only:

- [Old NFT proxy](https://testnet-api.decimalchain.com/api/v1/contracts/0x07e2ad4dfc91412de09e33e4650254948b21a20c): `DecimalDelegationNFTProxy`, status `verify`.
- [Combined proxy](https://testnet-api.decimalchain.com/api/v1/contracts/0x0d8e8ce36dade42682dd8cc6288d6a8d324bac2c): `DecimalDelegationProxy`, status `verify`.

The verified proxy source uses ERC1967. Storage slot `0x360894a13ba1a3210667c828492db98dca3e2076cc3735a920a3ca505d382bbc` was read through public chain202020 RPC `https://202020.rpc.thirdweb.com` because the official testnet RPC timed out. The observed state around block `0x156efd9`:

| Proxy | Current implementation |
| --- | --- |
| Old NFT `0x07e2...a20c` | `0xf9790e912a10b1cddc8aa3c8c2b6a8205446d4d4` |
| Combined `0x0d8e...ac2c` | `0x2f29724dcf9e3fe2864db03c5310cb368aa96f09` |

The combined proxy's constructor initially named a different implementation, `0x2f227537e47cc5465c7bbac5bc1020e4e11975f40`. Constructor metadata must not be substituted for current proxy storage.

The current combined implementation has 24316 runtime bytes, SHA256 `ddd4687ae3204d3178180ac762c9241cbc30c27a7f984fc130f219f58132d1fc`. Its Solidity CBOR trailer names metadata CID `QmWM4xhHphrHDXEuPmPfauZkbZkRrHgzjuV8ZN42sYRTGA`. Official API lookup of the implementation returned 404; attempts to retrieve that metadata from the Decimal gateway and ipfs.io timed out, while dweb.link returned429. Therefore no verified ABI for the **current implementation** was obtained.

As corroborating negative evidence only, the runtime does not contain the current SDK's known NFT selectors:

| Existing SDK signature | Selector | Runtime bytes |
| --- | --- | --- |
| `getStake(address,address,address,uint256)` | `347406a2` | absent |
| `getHoldStake(address,address,address,uint256,uint256)` | `8dcf9a3b` | absent |
| `delegateDRC721(address,address,uint256)` | `556e055f` | absent |
| `delegateDRC1155(address,address,uint256,uint256)` | `1caa077d` | absent |

The known fungible-token `getStake(address,address,address)` and `getHoldStake(address,address,address,uint256)` selectors are present. Selector presence does not prove semantics or NFT support, so no replacement ABI is inferred from this scan.

Conclusion: a verified current implementation ABI/source and SDK migration are required before enabling testnet NFT staking. `NftDelegationSupport.RequireSupported` provides an explicit early testnet block, including the deprecated address and unverified replacements. It does not block ordinary NFT ownership reads or transfers. Mainnet behavior remains separate and must not be inferred from testnet results.

Public evidence snapshots are under `artifacts/testnet-public-probe/`: proxy metadata JSON, implementation runtime, CID, and `nft-delegation-implementation-evidence.json`.

## Current investigation: official combined NFT interface found

The official JS SDK snapshot `6790d35e2decb0cbb06a9149a9c476c834f99223` contains a separate, newer combined delegation interface in `src/decimalevm/index.ts:874` and `call.ts:528`. It explicitly deprecates DRC-named methods and uses `delegateNFT`, `delegateNFT1155`, `withdrawNFT` and `withdrawNFT1155` on the ordinary `delegation` contract. Primary source: https://bitbucket.org/decimalteam/dsc-js-sdk/src/6790d35e2decb0cbb06a9149a9c476c834f99223/src/decimalevm/call.ts .

Observed dispatcher selectors include `delegateNFT1155(address,address,uint256,uint256)` = `319b41a6` and `withdrawNFT1155(address,address,uint256)` = `e29192c3`. This observation corroborates the published signatures; selectors alone are not the source of their ABI.

A fresh testnet ERC1967 implementation and SHA256 check passed using `CombinedNftDelegation.VerifyAsync`. A read-only `eth_call` of the published delegateNFT1155 signature, from the existing QA owner for collection `0x918e742d8dd0de7e7930fe0489cdbce1701fb29d`, ID1, quantity1, validator `0x05BaBC196B96C22f763f7e30a84E74665A920f98`, succeeded with `0x`. Evidence: `artifacts/nft-new-method-simulation.txt`. No transaction was sent by this probe.

The targeted helper pins the implementation/runtime and supports only this testnet deployment. The broad legacy testnet guard remains unchanged. A full implementation metadata ABI and NFT active-position getter layout are still separate evidence gaps; the upcoming controlled lifecycle must verify actual NFT balances and emitted frozen requests.

### Active-position read distinction

At the confirmed delegate block 22474977, ordinary getStake(validator, delegator, NFT collection) returned an empty stake. The published getStakes(bytes32[]) call using the exact StakeId from the NFT withdrawal event instead returned a DEL reserve position (tokenType4 in the official enum), raw amount10^18, token0x0e7c2cd8dc3a8ae787a8142f45b268ec27feeb8b. The withdrawal event separately identifies the actual ERC1155 collection, ID1 and amount1. Evidence: nft-active-getter-probe.jsonl and nft-get-stakes-by-id.txt under artifacts.

The official JS NFTStake interface explicitly distinguishes reserveToken/reserveAmount from nftContract/tokenId/amount. Consequently the generic stake query must not display reserve units as NFT quantity. No active NFT getter is invented from this result. The targeted delegate UI verifies the actual NFT balance and approval, while frozen-request processing verifies the emitted underlying NFT tuple.

## Current official SDK and verified NFT-position getter

The freshly fetched official JS SDK HEAD is `c7a72278551c517d3c0a37e170f844e8176e385f` (2026-10-28). Its `src/decimalevm/call.ts` publishes the exact main-delegation read signature `getNFTStake(address nftContract,uint256 tokenId,address delegator,address validator,uint256 holdTimestamp)`, selector `3d76c3f2`. Primary source: https://bitbucket.org/decimalteam/dsc-js-sdk/src/c7a72278551c517d3c0a37e170f844e8176e385f/src/decimalevm/call.ts . The current SDK guide describes the standalone delegation-nft path as preferred, but that does not override the observed testnet deprecation revert. Deployment-specific behavior is tested directly.

At confirmed NFT delegate block22474977, this getter returned the exact ten-field NFTStake interface: underlying QA collection, ID1, amount1, type3, QA owner, validator, DEL reserve token/amount, hold0, active=true. At withdrawal block22474979 it returned the same NFT coordinates with active=false. Evidence: `artifacts/nft-current-getNFTStake-proof.jsonl`. This closes the earlier active-getter gap for the pinned testnet implementation.

`CombinedNftDelegation.GetStakeAsync` checks the deployment profile, exact tuple size, field ranges and requested identity. `WalletDataReader` maps actual NFT amount rather than reserveAmount, optionally discovers official nftstakes entities and merges atomically saved public NFT locators. A locator does not store or prove a balance; it is rechecked through the current RPC on every refresh. The manual NFT workspace remains usable while Graph is unavailable.
