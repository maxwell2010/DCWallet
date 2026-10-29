# Native platform CI

Native checks runs on Windows x64/ARM64, Linux x64/ARM64 and macOS Intel/Apple Silicon. Each job executes Core and Avalonia Headless tests, publishes a self-contained binary, verifies both test-host and app architectures and runs the final-file self-test. Linux jobs unpack and run .deb/tar packages; x64 also executes AppImage without FUSE. macOS jobs unpack the .app archive, validate Info.plist and run its binary.

NuGet auditing includes direct and transitive dependencies. Known vulnerability warnings fail the checked build. This checks published advisories, not every possible vulnerability or application security property.

No network-write secrets or wallet files are configured in Actions. Network and fork tests remain opt-in; the default workflow does not broadcast blockchain transactions. Bridge is excluded. Mainnet NFT delegation retains its on-chain pause check and message.

The collect job requires all six platform jobs and all package formats. It creates permanent names, DOWNLOADS.json and SHA256SUMS.txt and uploads verified-platform-downloads. Runtime JSON, TRX results and screenshots are attached as logs-<rid>. The workflow result is the evidence of platform success; configuration alone is not evidence that jobs passed.

## Stable publication

The stable publication workflow takes a successful Native checks run ID from main. It validates the source workflow, commit, seven required successful jobs, complete packages and SHA-256 hashes. It uses the checked artifact without rebuilding source. The exact run URL and source SHA are appended to release notes.

Version 0.2.0 is published under immutable v0.2.0 as a public stable release and Latest only after these gates. The same checked commit is retained as build/0.2.0. Permanent user downloads use /releases/latest/download/. The alpha prerelease channel mirrors the same files with title DC WALLET 0.2.0 so installed alpha updaters can offer the stable upgrade. Its moving tag does not replace the immutable version tag.

Each release contains 13 versioned files, 13 permanent aliases, DOWNLOADS.json and SHA256SUMS.txt. Publication results, rather than this description, determine whether the new public version is available.

## Scope and source integrity

The binaries have no publisher signing certificates; macOS notarization is not configured. Stable marks the accepted consumer-wallet scope, not independent security certification. See [capabilities](CAPABILITIES.md), [readiness](PUBLICATION_READINESS.md) and [downloads](RELEASE_DOWNLOADS.md).

Read-only mainnet probes require explicit DC_MAINNET_READ_OWNER. Private local probe addresses are not embedded in source. SDK and compiled QA fixtures use byte-exact Git attributes to preserve recorded hashes. Catalog input comparisons normalize JSON indentation line endings while preserving exact calldata/value comparisons. Test HTTP fixtures use cancellable loopback TCP transport to avoid Unix HttpListener shutdown races.

The historical alpha run [40832718387](https://github.com/maxwell2010/DCWallet/actions/runs/40832718387) passed all six native platforms. Stable publication requires its own successful run for the final checked commit; it does not reuse that historical run as evidence for changed source.
