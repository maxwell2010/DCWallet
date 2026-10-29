# DC WALLET privacy notice - publisher review draft

Technical draft dated 2026-10-29. The legal publisher and its public privacy contact must be confirmed before this notice is used as the Microsoft Store privacy policy. This file describes the reviewed application behavior; it is not a promise about independent third-party services or their retention periods.

## Local wallet data

DC WALLET is a non-custodial Decimal wallet. Recovery phrases and private keys are generated or imported on the device and stored in password-encrypted wallet files. Signing occurs locally. The application does not intentionally send recovery phrases, private keys or the wallet encryption password to RPC providers, indexers, GitHub or the publisher.

Preferences, profile names, contacts and the transaction journal are local files. The journal includes public transaction hashes, addresses, chain ID, values and transaction states. Exported CSV/XLSX tables and encrypted backups are written to locations selected by the user. Files copied to other devices, cloud folders or clipboard managers are subject to those tools' behavior. Password encryption does not protect an already unlocked session from a compromised operating system.

## Network requests

When a wallet or watch-only address is opened and refreshed, its public address and asset/history queries are sent to blockchain RPC and discovery services. Queries can run automatically while the wallet is open. Mainnet history may also use an archive RPC. Token/NFT/staking discovery uses Decimal indexers, The Graph endpoints and MintCandy console/explorer services. Token/validator searches send the search text to the catalog provider. A custom RPC receives corresponding requests when configured.

Providers can observe the requesting IP address, request time, public wallet address and requested contracts or transactions. Public addresses can be associated with a person and should not be treated as anonymous. These services have their own policies, availability and retention practices. Choosing a custom RPC does not by itself disable all separate discovery/indexer requests.

On explicit transaction confirmation, a signed transaction is broadcast to the selected network RPC. Broadcasts expose the sender, recipient or contract, value and transaction data to the public blockchain and may be retained permanently. Signed transaction data is not the private key. Blockchain records cannot be deleted by removing the app.

Portable Windows builds ask permission before the first GitHub update check. The choice can be disabled or enabled in Settings; closing the prompt without agreeing does not start a request. When enabled, one startup check reads public release metadata to offer a newer version. Existing macOS/Linux startup behavior is unchanged. The version-check request does not include wallet addresses, balances, recovery phrases or private keys. GitHub and download infrastructure can observe normal connection metadata. Store builds disable the external GitHub updater. Packaged Windows identity is also detected at runtime to use Store-managed updates. Opening an explorer or download link launches the user's browser and connects to the selected service.

No application advertising, behavioral analytics or publisher-operated crash-report upload was found in the reviewed source. This does not describe telemetry independently collected by Windows, Microsoft Store, GitHub, the browser or network providers.

## Control, deletion and support

Users can close or lock the wallet, select a different RPC, export an encrypted backup and remove local wallet/profile files through the application's available controls or their operating system. Deletion of a local file does not guarantee erasure from backups, clipboard history, storage remnants or public blockchain records. MSIX installation may redirect local storage; confirm the displayed data directory and retain a tested encrypted backup before uninstalling or migrating installations.

Technical support: [DC WALLET GitHub issues](https://github.com/maxwell2010/DCWallet/issues). Do not post recovery phrases, private keys, passwords, live bearer checks or unredacted financial records in a public issue. Publisher privacy contact: **required before Store submission**. The publisher must supply its legal identity and a suitable privacy contact and approve the published policy URL.

## Third-party service examples

Reviewed defaults include `node.decimalchain.com`, `node4-mainnet.decimalchain.com`, `api.decimalchain.com`, `testnet-api.decimalchain.com`, Decimal mainnet/testnet The Graph endpoints, `console.mintcandy.ru`, `explorer.mintcandy.ru` and `api.github.com`. Endpoint selection can vary with network and user settings. This list is descriptive, not a guarantee that providers do not change.
