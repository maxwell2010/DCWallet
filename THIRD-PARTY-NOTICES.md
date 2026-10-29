# Sources and attribution

Original DC WALLET code and documentation are released under the [MIT License](LICENSE), copyright 2026 Maxim Sereda (maxwell2010), at the repository owner's direction. This grant does not replace the licenses or copyright notices of third-party code and packages. Existing DC WALLET artwork and MintCandy styling are reused at the owner's direction; this notice does not assert original authorship of third-party material or grant trademark rights.

- Decimal C# SDK: local working-tree snapshot from the upstream `decimal-web3-sdk/csharp` working tree. Original license preserved in `vendor/LICENSE`; file hashes in `vendor/SOURCE.json`. The referenced Git commit is the base of that working tree, not a claim that local changes are committed upstream.
- Graphics: existing DC WALLET assets in `decimalconnect/apps/dc-extension/public` and `decimalconnect/apps/dc-mobile/src/icons.ts`, reused at the user's direction. `app.ico` wraps the same PNG, with no redrawing. `DcIcon.cs` preserves SVG path geometry.
- Cryptopunk colors: the user's MintCandy console `console/src/styles.css` from MintCandy, `body[data-theme="neon"]` rules, reused at the user's request from the chat «Продолжить доработки MintCandy».
- MinterWallet: MIT-licensed architectural reference at `d52034111eb4df5d63c4b8a38befc92ad5b140ab`; implementation is separate. No Minter protocol code is used.
- Dependencies: Avalonia 11.3.9, QRCoder 1.6.0, Nethereum 7.0.0 and their transitive dependencies retain their package licenses. See NuGet package metadata for their authors and licenses.
- Security dependency overrides: Newtonsoft.Json 13.0.3 and Tmds.DBus.Protocol 0.21.3 are selected by the wallet build. Their upstream package licenses remain applicable. No vendored SDK file was relicensed or modified when adding the root MIT license.
