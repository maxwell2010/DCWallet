# Code signing policy

Status: SignPath Foundation application preparation only. DC WALLET has not been accepted by SignPath, and no published file is represented as SignPath-signed. macOS signing and notarization remain unchanged.

## Project and scope

The project is maintained at [maxwell2010/DCWallet](https://github.com/maxwell2010/DCWallet). The proposed SignPath scope is Windows x64 and ARM64 code built from this public repository. Original wallet code and documentation use MIT; SDK and dependency license notices remain in place. SignPath acceptance, certificate issuance and workflow configuration are separate from adding a license.

## Responsibilities

The currently identified maintainer is [Maxim Sereda (maxwell2010)](https://github.com/maxwell2010). The proposed author, reviewer and signing approver is this maintainer. These assignments must be confirmed during onboarding; no additional independent reviewers are claimed.

Contributions from non-committers require maintainer review. Signing requests require explicit approval by an authorized human maintainer. The proposed signing team must enable MFA for repository and signing-service access before onboarding is declared complete.

## Signing and publication

1. Build Windows artifacts using GitHub Actions from reviewed source.
2. Record the exact source SHA and successful native test run; preserve the link between source and binary.
3. Request signing only for the project's own artifacts, with enforced product and version metadata.
4. Obtain manual approval for each signing request.
5. Verify the returned Authenticode signature and timestamp before publishing.
6. Compute final package hashes after signing. Retain the source revision and signing/build evidence with the release.

Unsigned files remain explicitly unsigned. If signing is configured as required for a release and fails or is not approved, the release must not silently publish unsigned substitutes as signed files. Microsoft Store MSIX signing is a separate distribution path and does not sign the GitHub EXE automatically.

## Privacy and installation behavior

Wallet secrets are stored locally in encrypted files. Using the wallet contacts network RPC/catalog providers for balances, addresses, transaction history and simulations; confirmed signed transactions are broadcast to the selected RPC. Startup update checks contact GitHub release metadata. These transfers must be disclosed in the project's privacy policy; no blanket no-network claim is made here.

Windows portable builds ask for explicit permission before the first GitHub update request. The choice is saved and can be changed in Settings. Store builds disable the GitHub updater and use Microsoft Store updates; runtime package identity provides an additional guard. macOS and Linux update behavior is unchanged. The consent window displays local disclosure and opens the online [privacy policy](PRIVACY.md) only after an explicit click. Verify the final packaged build, privacy policy and uninstall instructions against the [SignPath Foundation conditions](https://signpath.org/terms) before application submission. Service onboarding and identity checks are not declared complete by these controls.

## Attribution after acceptance

Only after the Foundation approves the project and the signing service is operating, publish the following required credit on the homepage and download pages:

> Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).

This future attribution is quoted as an onboarding requirement and is not a statement of current sponsorship. A trusted signature identifies the publisher and provenance; it does not certify wallet correctness or guarantee the absence of SmartScreen prompts.
