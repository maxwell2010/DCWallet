# SignPath Foundation application preparation

Submitted 2026-10-29 via https://signpath.org/apply. The form displayed "Form submitted - Thank you, we'll be in touch soon." The maintainer also received the automated "Open Source Project Submitted" email. Status: review pending, not approved; no SignPath certificate or signing service is configured. Existing public binaries are unsigned. macOS is unchanged.

The official [application page](https://signpath.org/apply) embeds a dynamic HubSpot form. Project, repository, homepage, download/privacy URLs, description, reputation evidence, GitHub Actions, individual maintainer type and contact details were supplied. Required Code of Conduct and personal-data-processing consent were explicitly confirmed by the maintainer before submission. Optional marketing consent was left unchecked. Private contact details and security screenshots are not published here.

## Application content and supporting references

| Item | Prepared value |
| --- | --- |
| Project name | DC WALLET |
| Project homepage | https://github.com/maxwell2010/DCWallet |
| Source repository | https://github.com/maxwell2010/DCWallet |
| Open-source license | MIT for original wallet code and documentation; existing SDK and third-party package licenses preserved |
| License URL | https://github.com/maxwell2010/DCWallet/blob/main/LICENSE |
| Download page | https://github.com/maxwell2010/DCWallet/releases/latest |
| Current published version | 0.2.0, source tag v0.2.0 |
| Build system | GitHub Actions, .NET 8, Avalonia, self-contained Windows x64 and ARM64 executables |
| Build workflow | https://github.com/maxwell2010/DCWallet/actions/workflows/check.yml |
| Verified stable build | https://github.com/maxwell2010/DCWallet/actions/runs/40834799101 |
| Source commit of that build | b541ae72881b992b6044ddbf06f8601391781c49 |
| Proposed signed artifacts | DCWallet-win-x64.exe and DCWallet-win-arm64.exe, built from reviewed repository source; Windows MSIX packaging is being prepared separately |
| Project maintainer | Maxim Sereda, GitHub maxwell2010 |
| Code signing policy | https://github.com/maxwell2010/DCWallet/blob/main/docs/CODE-SIGNING-POLICY.md |

Project description:

> DC WALLET is a native desktop wallet for the Decimal network, developed in C# with Avalonia and a vendored Decimal .NET SDK. It supports locally encrypted wallet profiles, watch-only accounts, asset discovery, transfers and batch transfers, token conversion, staking, Decimal Safe operations, checks and transaction history. Private keys are held locally and are not sent to RPC providers. Blockchain transactions are signed locally and require explicit confirmation before broadcasting. The application also reads public release metadata from GitHub to offer updates. Network behavior and its controls are documented in the privacy policy. Bridge functionality is excluded from the current release scope; mainnet NFT delegation observes the network contract's paused state.

Build provenance and request:

> We request free Windows code signing for our public open-source project through SignPath Foundation. GitHub Actions builds and tests Windows x64 and ARM64 from the public repository. Release files are linked to the successful source run and commit and distributed with a SHA-256 manifest. Linux and macOS checks also run natively, but this request concerns Windows artifacts. The existing 0.2.0 release predates the root MIT-license addition; please assess the reviewed, newly built MIT-licensed revision proposed for signing rather than treating the historical unsigned binaries as already approved. We understand that acceptance is discretionary, the Foundation is the certificate publisher, and each signing request requires an authorized human approver. We will not claim SignPath sponsorship or a valid signature until approval and actual signature verification.

## Onboarding follow-up

- A monitored contact email was provided privately in the official form. Monitor it for the Foundation's decision or follow-up questions.
- Confirm the maintainer's name, project ownership and role assignments. Maxim Sereda is the currently identified maintainer; additional reviewers or approvers must be real authorized people, not invented identities.
- GitHub MFA was confirmed from the maintainer's supplied settings screenshot. SignPath account MFA and service permissions remain onboarding steps after access is issued.
- Review licenses of included assets and all packaged dependencies. Public source alone is not evidence of the right to relicense third-party material.
- Confirm a public privacy policy and user controls for background network access. Windows portable builds now require a saved explicit choice before startup update checks, with a Settings opt-out. Store builds disable GitHub checking. RPC/catalog queries occur while operating a selected wallet; the blanket claim that no data reaches other networked systems would be false. The [SignPath terms](https://signpath.org/terms) require disclosure and installation controls for applicable transfers.
- Add the policy link on the project homepage and download/release pages. After approval, use the required attribution in the policy without implying that older unsigned files are signed.
- Replace the historical build/run references with the final reviewed MIT-licensed build selected for signing. Supply SignPath account and artifact configuration identifiers only after they are issued.

The project application has been submitted. No signing request has been made, no signing credentials have been provisioned, and account activation or certificate approval has not been confirmed. Do not put certificate private keys, service tokens or signing credentials in this document or Git.

## Prepared signing workflow

`.github/workflows/signpath.yml` is manual-only and is not run during application preparation. It checks the selected successful Native checks run and MIT-licensed source, selects the original Windows binaries by SHA-256, uploads their input artifact and requests signing through the pinned official SignPath action. Returned files must have valid timestamped Authenticode signatures matching the expected certificate subject. Final hashes are computed after signing; the workflow does not publish a release automatically.

After acceptance, configure a GitHub environment named `signpath` with appropriate required human reviewers. Add secret `SIGNPATH_API_TOKEN` and variables `SIGNPATH_ORGANIZATION_ID`, `SIGNPATH_PROJECT_SLUG`, `SIGNPATH_SIGNING_POLICY_SLUG`, `SIGNPATH_ARTIFACT_CONFIGURATION_SLUG` and `SIGNPATH_EXPECTED_CERTIFICATE_SUBJECT` using the values issued or approved by SignPath. The service policy must retain manual approval. No values or reviewers have been invented or provisioned by this preparation.

Enforce project product/version metadata and the two expected executable paths in the SignPath artifact configuration. The selected artifact SHA/run must refer to the newly reviewed MIT-licensed build, not the historical unsigned release. Configuration and the first actual signing request require separate verification after onboarding.
