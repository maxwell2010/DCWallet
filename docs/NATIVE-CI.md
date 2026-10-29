# Native platform CI

The Native checks workflow runs on Windows x64/ARM64, Linux x64/ARM64 and macOS Intel/Apple Silicon. Every job runs Core and Avalonia Headless tests, publishes a self-contained binary, verifies the architectures of both the test host and the published app, and executes the final-file self-test. Linux jobs build and unpack .deb/tar packages; x64 also executes AppImage without FUSE. macOS jobs unpack the .app archive, validate Info.plist and run its binary.

No network-write secrets or wallet files are configured in Actions. Public-network tests and local fork tests remain opt-in; the default workflow does not broadcast blockchain transactions. Bridge is excluded. Mainnet NFT delegation retains its on-chain pause check and message.

The final collect job requires all six jobs and all package formats. It creates permanent asset names, DOWNLOADS.json and SHA256SUMS.txt and uploads verified-platform-downloads. These are Actions artifacts, not a public GitHub Release. Runtime JSON, TRX results and screenshots are attached separately as logs-<rid> artifacts.

Read-only mainnet probes require an explicit DC_MAINNET_READ_OWNER address. Private local mainnet probe addresses are not embedded in public source. The vendored SDK and compiled QA fixtures use byte-exact Git attributes to preserve their recorded hashes across operating systems.
