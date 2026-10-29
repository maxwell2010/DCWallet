# ABI audit: tokens, ERC20 and Checks - 29.10.2026

This audit compares selectors, argument order, tuples and event layouts against independent primary ABI evidence. It does not use SDK-generated calldata fixtures as the authority and does not certify every deployed contract.

## Primary evidence

- Official `decimalteam/go-smart-node` commit `9e6c6d718d662083c4a524376a66d2c50bd4bc77`: `contracts/token/DecimalToken.json`, `contracts/tokenCenter/DecimalTokenCenter.json`.
- Official `decimalteam/dsc-js-sdk` current checkout commit `c7a72278551c517d3c0a37e170f844e8176e385f`: `src/decimalevm/abi/Checks.json`, `src/decimalevm/call.ts`, older full `test/evm/abi/DecimalTokenCenter.json`.
- Existing public testnet receipts for reserve buy/sell/convert, reservless mint/burn and Checks allowance/permit/redeem. Local fork token creation receipt is supplemental EVM evidence, not a public receipt.
- `PrimaryTokenAbiOracleTests`: 21 full calldata vectors generated independently by ethers6.15.0 against the primary Token/TokenCenter/Checks ABIs, with explicit test inputs. No network or artifacts dependency at test execution.

## Findings and effects

| Signature or behavior | Current contract/source evidence | SDK shape | Wallet consequence and resolution |
| --- | --- | --- | --- |
| `TokenDeployed(address,(uint256,uint256,uint256,address,uint8,string,string,string))` | Current node ABI and actual fork receipt topic `0xe17428da190a3db2fb16175ba372d6b241e61e2249e96a18ddac56ed4336aa19` | SDK receipt helper only accepts older `TokenDeployed(address)` | Real mismatch: SDK returns no created token address. Fixed in Core CurrentTokenCreationReceiptReader, with actual-event regression fixture, strict status/emitter/creator checks and legacy support |
| Reserve creation funding | Current testnet `getCommissionSymbol(longSymbol)` returned0.25DEL. Actual fork tx value1250DEL emitted ReserveUpdated1249.75DEL, confirming fee deduction | SDK omitted `reserveValueWei` defaults to `(1000 + old symbol-length fee) DEL`, e.g.1250 or3500DEL | Real economic/UI gap. Core WalletTokenCreationAdapter requires explicit initial reserve, reads fresh commission, sets exact total value=reserve+commission and performs RPC estimation. It never invents a minimum reserve |
| Non-mintable reservless cap | Current official JS createTokenReserveless explicitly forces cap0 when mintable=false | SDK builder accepts caller cap unchanged | Core adapter explicitly rejects nonzero capRaw for mintable=false; no silent economic input changes. Correct creation calldata with false/0 is independently tested |
| Checks `nonces()` | Deployed implementation scopes view to msg.sender, confirmed by public create/redeem campaigns | SDK GetNonceAsync omits From | Selector matches but read semantics differ. CheckLifecycle.GetNonceForAsync supplies creator From; dedicated and generic creation use it |
| `mint(address,uint256)` on reserve token | Absent from current reserve Token ABI | TokenEconomy.BuildMintDraft deliberately throws | Correct restriction, not missing reserve feature. Reservless mint stays separate and was publicly tested |
| `version()` on ERC20 permit | Optional, not in current node Token ABI | SDK tries version then defaults to1 | Not a selector mismatch. Fallback is accepted only if recomputed EIP712 domain matches on-chain DOMAIN_SEPARATOR. Incompatible tokens fail closed |
| `updateDetails(string,uint256)` | Present in reserve Token ABI | Both TokenCreation and TokenEconomy builders encode exact matching selector/order | No encoding mismatch. Not a generic reservless identity/upgrade method. Live authorization/deployment compatibility must be checked before signing |
| Token Center upgrade/admin methods | Node ABI has owner-only pause/unpause, transferOwnership, setContractCenter, upgrade(address,bytes), upgradeToken(address), upgradeTokenReserveless(address) | Not part of current SDK draft catalog | System administration, separate from ordinary wallet-holder transaction scope. No claim that catalog covers every contract admin function. Initialization and renounceOwnership are not ordinary wallet flows |

## Complete builder-family comparison

| SDK method family | Independent primary match | Scope/limitations |
| --- | --- | --- |
| BuildCreateTokenDraft | `createToken((uint256,uint256,uint256,address,uint8,string,string,string))` | Tuple type/order exact; name creator/tokenOwner in sources does not alter encoding. Runtime funding uses wallet adapter |
| BuildCreateReservelessTokenDraft | `createTokenReserveless(string,string,bool,bool,uint256,uint256,string)` | Exact encoding for mintable and non-mintable variants. Non-mintable cap0 validation in adapter |
| BuildUpdateDetailsDraft (both classes) | `updateDetails(string,uint256)` | Reserve token only, not reservless administration |
| BuildBuyDraft / exact-input / exact-output | `buy(uint256,address)`, `buyTokenForExactDEL(uint256,address)`, `buyExactTokenForDEL(uint256,address)` | Value sends provided DEL cap/spend. Exact-output argument is amountOut, not amountOutMin |
| BuildSellDraft / exact-input / exact-output | `sell(uint256,uint256,address)`, `sellExactTokensForDEL(uint256,uint256,address)`, `sellTokensForExactDEL(uint256,uint256,address)` | Exact-output is amountOut then amountInMax; order is correct |
| BuildConvertDraft / with permit | `convert(address,address,uint256,uint256,address)` and 9-argument deadline/v/r/s overload | Exact encoding. Both use TokenCenter target; token swap slippage is not gas fee |
| BuildBurnDraft | `burn(uint256)` | Exact encoding; holder asset destruction remains explicit |
| BuildMintReservelessDraft / ReservelessTokenService mint/checked mint | Official JS mint(recipient,amount) and public QA reservless deployment | Correct separation from reserve-token ABI. Checked builder verifies mintable, owner and cap |
| ReservelessTokenService burn/checked burn | Official JS burn(amount), public QA deployment | Checked builder verifies burnable and balance; not a substitute for on-chain role/feature validation |
| Erc20Service transfer/approve/transferFrom | Current Token ABI `transfer(address,uint256)`, `approve(address,uint256)`, `transferFrom(address,address,uint256)` | String/decimals overloads feed identical raw ABI. Exact encoding independently tested |
| Checks DEL create | `createChecksDEL(address[],uint256,uint256,uint256)` | Correct signers/amount/dueBlock/nonce; payable value is count*amount |
| Checks token create | `createChecksToken(address[],uint256,uint256,uint256,address)` | Token address last; approval and fresh nonce belong to caller workflow |
| Checks token permit create | `createChecksTokenByPermit(address[],uint256,uint256,uint256,address,uint256,uint8,bytes32,bytes32)` | Correct deadline/v/r/s placement, uint8 v and bytes32 r/s |
| Checks redeem | `redeemChecks(bytes[],bytes32[])` | Correct signature/hash arrays. Bearer signs redeemer-bound digest. Receipt status1 is required by wallet helper |

## Read and event details

- Reserve metadata/views and calculateBuyInput/Output/calculateSellInput/Output (single amount and custom supply/reserve/CRR overloads) match current node ABI parameter types/order.
- Reservless cap/mintable/burnable/owner/identity reads are deployment-specific and already used by checked mint/burn tests. They are not claimed to exist on reserve tokens.
- Checks owner/getImpl/nonces function names match current official ABI. Mainnet's official address requires a separate deployment profile; zero owner alone is not proof of an unusable contract.
- CreatedCheck tuple and RedeemCheck(bytes32,uint8) event decoding has public and local lifecycle evidence. Receipt success without the intended event/status is insufficient.

## Verification result

The focused combined run passed6 tests: independent ethers oracle1 (21 calldata cases), wallet creation adapter2, token receipt compatibility3. Vendor files were not changed. No public RPC writes, no new funding and no key/seed reads occurred during this audit.

Still distinguish encoding completeness from implementation and network completeness. Exact-output purchase/sale, non-mintable deployment and privileged updates may have correct ABI while lacking a public end-to-end wallet scenario. Bridge is explicitly outside current scope.
