# Sources and attribution

- Decimal C# SDK: local working-tree snapshot from the upstream `decimal-web3-sdk/csharp` working tree. Original license preserved in `vendor/LICENSE`; file hashes in `vendor/SOURCE.json`. The referenced Git commit is the base of that working tree, not a claim that local changes are committed upstream.
- Graphics: existing DC WALLET assets in `decimalconnect/apps/dc-extension/public` and `decimalconnect/apps/dc-mobile/src/icons.ts`, reused at the user's direction. `app.ico` wraps the same PNG, with no redrawing. `DcIcon.cs` preserves SVG path geometry.
- Cryptopunk colors: the user's MintCandy console `console/src/styles.css` from MintCandy, `body[data-theme="neon"]` rules, reused at the user's request from the chat «Продолжить доработки MintCandy».
- MinterWallet: MIT-licensed architectural reference at `d52034111eb4df5d63c4b8a38befc92ad5b140ab`; implementation is separate. No Minter protocol code is used.
- Dependencies: Avalonia 11.3.9, QRCoder 1.6.0, Nethereum 7.0.0 and their transitive dependencies retain their package licenses. See NuGet package metadata for their authors and licenses.
