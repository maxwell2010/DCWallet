# Native platform CI

The Native checks workflow runs on Windows x64/ARM64, Linux x64/ARM64 and macOS Intel/Apple Silicon. Every job runs Core and Avalonia Headless tests, publishes a self-contained binary, verifies the architectures of both the test host and the published app, and executes the final-file self-test. Linux jobs build and unpack .deb/tar packages; x64 also executes AppImage without FUSE. macOS jobs unpack the .app archive, validate Info.plist and run its binary.

No network-write secrets or wallet files are configured in Actions. Public-network tests and local fork tests remain opt-in; the default workflow does not broadcast blockchain transactions. Bridge is excluded. Mainnet NFT delegation retains its on-chain pause check and message.

The final collect job requires all six jobs and all package formats. It creates permanent asset names, DOWNLOADS.json and SHA256SUMS.txt and uploads verified-platform-downloads. Runtime JSON, TRX results and screenshots are attached separately as logs-<rid> artifacts. The workflow result is the evidence of platform success; configuration alone does not establish that all jobs passed.

## Publishing the public alpha channel

The alpha publication workflow takes a successful Native checks run ID and uses its verified-platform-downloads artifact. Publication checks the source run and commit, all required assets and their SHA-256 hashes. It does not replace native validation with cross-compilation or rebuild different source for publication. The public prerelease description records the source run and commit so downloaded files can be traced to their checks.

The moving alpha tag provides permanent download URLs under /releases/download/alpha/. Versioned files, permanent-name aliases, DOWNLOADS.json and SHA256SUMS.txt are uploaded together. The channel remains a prerelease and is separate from GitHub latest. Version tags v* retain the separate draft-prerelease workflow.

Each published version also has an immutable build/VERSION source tag, such as build/0.2.0-alpha.3. The alpha tag follows the current channel publication; the build tag preserves the checked source commit.

The binaries are unsigned; macOS notarization is not configured. Publishing alpha does not imply stable status or an independent security audit. See [release downloads](RELEASE_DOWNLOADS.md) and [release notes](RELEASE_NOTES.md).

Read-only mainnet probes require an explicit DC_MAINNET_READ_OWNER address. Private local mainnet probe addresses are not embedded in public source. The vendored SDK and compiled QA fixtures use byte-exact Git attributes to preserve their recorded hashes across operating systems. Local JSON fixture input comparisons normalize indentation line endings; exact transaction calldata and values remain checked. Test HTTP fixtures use cancellable loopback TCP transport to avoid Unix HttpListener shutdown callback races.
