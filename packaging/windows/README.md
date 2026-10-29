# Microsoft Store MSIX packaging

Preview packages are unsigned CI validation artifacts with a fictitious identity. They cannot be submitted as DC WALLET's Store listing or installed by ordinary users without a trusted signature. No certificate is generated or installed by these scripts.

Publish the Store-specific self-contained build first. Preview example:

```powershell
dotnet publish src/DCWallet/DCWallet.csproj -c Release -r win-x64 --self-contained true -p:StoreDistribution=true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:DebugType=None -warnaserror -o artifacts/publish-store/win-x64
./tools/package-msix.ps1 -Runtime win-x64 -AppVersion 0.2.0 -PackageVersion 1.0.0.0 -Preview
```

The default payload directory is artifacts/publish-store/RUNTIME. The Store compile flag disables external GitHub updates; the application also detects Windows package identity at runtime. The Actions workflow executes the payload's runtime-info probe and requires the store distribution marker on the native target architecture before packing.

For a real submission, reserve the product in Partner Center and copy all identity values exactly from Product identity:

```powershell
./tools/package-msix.ps1 -Runtime win-x64 -AppVersion 0.2.0 -PackageVersion 1.0.0.0 -IdentityName '<Package/Identity/Name>' -Publisher '<Package/Identity/Publisher>' -PublisherDisplayName '<Package/Properties/PublisherDisplayName>'
```

Repeat for win-arm64 using that architecture's published payload. PackageVersion is an independent Store version, not the displayed application version. The default initial Store version is 1.0.0.0; app version 0.2.0 is not silently converted. Store requires the first component to be nonzero and the fourth component to be zero. Increment Store versions for later submissions.

Windows SDK MakeAppx performs full schema validation during packing. The checker unpacks the result and verifies identity, entry point, assets and PE architecture; a checksum is emitted. The scripts do not install the package, run Windows App Certification Kit, certify the app, or submit it. Partner Center identity, privacy/store listing material and successful WACK/Store review remain required. Microsoft signs accepted MSIX packages during Store distribution.

Source graphics are resized from the existing user-provided DC WALLET icon. No third-party logo was introduced.

References: [manual desktop packaging](https://learn.microsoft.com/en-us/windows/msix/desktop/manual-packaging-root), [Store package requirements and versions](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/package-version-numbering?pivots=store-installer-msix), [MSIX signing options](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options).
