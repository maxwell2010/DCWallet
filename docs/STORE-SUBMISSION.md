# Microsoft Store submission preparation

Prepared 2026-10-29 for DC WALLET. This is a submission checklist, not a claim of Store certification or an application already submitted. macOS distribution is unchanged.

## Account and product identity

DC WALLET signs cryptocurrency transactions and imports private keys/recovery phrases. A Microsoft Store **Company account is required**, even though keys stay local. A read-only wallet exception does not apply to this product. See sections 10.2.6 and 10.8.3 of the [current Store policies](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies). Do not submit the full wallet through an Individual account or describe it as read-only.

The publisher must register and verify the Company account, reserve the product name, and supply these actual Partner Center values before building submission packages:

| Required value | Status |
| --- | --- |
| Package/Identity/Name | Required from the reserved product's identity page |
| Package/Identity/Publisher, including exact CN | Required from Partner Center; not the GitHub username |
| Package/Properties/PublisherDisplayName | Required from the verified publisher |
| Store product ID and Package Family Name | Required after reservation; useful for support and Store links |
| Legal publisher name, country, verified support contact | Publisher confirmation required |
| Public HTTPS privacy policy URL | Publisher must approve and publish the privacy draft |
| Supported markets and IARC questionnaire | Publisher completion required; no age rating invented here |

Keep identity stable across updates. Version, architecture and publisher are package identity components; see [Microsoft package identity](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/package-identity-overview). Preview identity values used for local packaging are not assigned Store identities.

## Packaging and capability explanation

Prepare x64 and ARM64 MSIX packages using the packaging scripts and matching self-contained Windows outputs. The preparation package uses Store version 1.0.0.0, separately from application version 0.2.0. Store requires a nonzero first component and a zero fourth component; use a strictly increasing approved package version on later submissions. Microsoft signs approved Store-distributed MSIX packages. Unsigned preparation artifacts are not consumer-installable, trusted Store releases.

Proposed restricted-capability justification for `runFullTrust`:

> DC WALLET is an existing Avalonia/.NET desktop application. Full-trust desktop execution is needed to run its .NET process, encrypt and access user-selected local wallet files, use standard file pickers and clipboard functions, and communicate with blockchain RPC services. It does not require administrator elevation, install drivers, mine cryptocurrency, or modify system trust stores. Private keys remain local; transaction signing is performed on the user's device.

Capability declarations and their justification remain subject to Microsoft review. Do not request unrelated capabilities.

## Submission gates

- Build with the real reserved identity, unpack and validate both packages, confirm payload architecture and logos, and run native tests. Run Windows App Certification Kit on the final packages where available. Packaging success alone is not Store acceptance.
- Store-packaged builds must use Store-managed updates and must not route users into installing the GitHub portable EXE as an update. Confirm that the Store variant disables the GitHub startup updater before submission. Portable Windows builds request permission before the first GitHub check and offer an opt-out in Settings. macOS/Linux behavior is unchanged.
- Test clean installation, wallet creation/import, encrypted backup export, lock/unlock, file pickers, clipboard/QR, tray behavior, Store upgrade and uninstall. Confirm MSIX file-system redirection does not strand existing portable-wallet data. Use encrypted backup import for migration; do not copy or submit real keys to reviewers.
- Do not add identity secrets or certificates to the repository. Do not install a test certificate into a user's trusted root store as a substitute for Store signing.
- Fill listing fields from [STORE-LISTING.md](STORE-LISTING.md), approve [PRIVACY.md](PRIVACY.md), complete content declarations and submit the actual packages in Partner Center.

Provide real screenshots captured from the release build, with test data visibly identified where applicable. Never include live seed phrases, private keys or unredeemed bearer checks. Supply at least one PC screenshot, preferably an overview plus send, NFT, staking and settings views. Capture at 1920x1080 as a practical target and confirm the active PC image rules in Partner Center. Existing small test snapshots must not be represented as final Store images. [Microsoft submission metadata guidance](https://github.com/MicrosoftDocs/windows-dev-docs/blob/docs/hub/apps/publish/faq/submit-your-app.md).

## Certification notes draft

No account registration with DC WALLET is required. A reviewer can start in watch-only mode using an address they control, or create a disposable local wallet and use Decimal testnet. Creation of a new wallet does not require payment. Network transactions require testnet funds and explicit confirmation; do not fund mainnet for certification. No reviewer credentials are supplied by this repository. Bridge is excluded. Mainnet NFT delegation may be paused by the network contract and shows a message; this does not stop other wallet pages.

Store policies can change. Recheck the cited policy and Partner Center declarations at actual submission time. No Store application or external account registration was performed by preparing these files.
