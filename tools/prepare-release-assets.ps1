param(
    [Parameter(Mandatory)][string]$Version,
    [string]$Directory = (Join-Path $PSScriptRoot '../artifacts/releases'),
    [switch]$RequirePackages
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+(-[A-Za-z0-9.]+)?$') { throw 'Invalid version' }
$directoryPath = (Resolve-Path -LiteralPath $Directory).Path
$mapping = [ordered]@{}
foreach ($rid in @('win-x64','win-arm64','linux-x64','linux-arm64','osx-x64','osx-arm64')) {
    $extension = if ($rid.StartsWith('win-')) { '.exe' } else { '' }
    $mapping["DCWallet-$Version-$rid$extension"] = "DCWallet-$rid$extension"
}
$packages = [ordered]@{
    "dc-wallet_${Version}_amd64.deb" = 'dc-wallet_amd64.deb'
    "dc-wallet_${Version}_arm64.deb" = 'dc-wallet_arm64.deb'
    "DCWallet-$Version-linux-amd64.tar.gz" = 'DCWallet-linux-x64.tar.gz'
    "DCWallet-$Version-linux-arm64.tar.gz" = 'DCWallet-linux-arm64.tar.gz'
    "DCWallet-$Version-x86_64.AppImage" = 'DCWallet-x86_64.AppImage'
    "DCWallet-$Version-osx-x64.app.zip" = 'DCWallet-osx-x64.app.zip'
    "DCWallet-$Version-osx-arm64.app.zip" = 'DCWallet-osx-arm64.app.zip'
}
foreach ($entry in $packages.GetEnumerator()) {
    if ($RequirePackages -or (Test-Path -LiteralPath (Join-Path $directoryPath $entry.Key))) {
        $mapping[$entry.Key] = $entry.Value
    }
}
# Preflight every source before changing aliases. A missing platform must fail the release.
foreach ($sourceName in $mapping.Keys) {
    $source = Get-Item -LiteralPath (Join-Path $directoryPath $sourceName)
    if ($source.PSIsContainer -or $source.Length -eq 0) { throw "Empty release asset: $sourceName" }
}
$assets = @()
$checksums = @()
foreach ($entry in $mapping.GetEnumerator()) {
    $source = Join-Path $directoryPath $entry.Key
    $alias = Join-Path $directoryPath $entry.Value
    Copy-Item -LiteralPath $source -Destination $alias -Force
    $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
    if ((Get-FileHash -LiteralPath $alias -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash) {
        throw "Alias hash mismatch: $($entry.Value)"
    }
    $checksums += "$hash  $($entry.Key)", "$hash  $($entry.Value)"
    $assets += [ordered]@{ file = $entry.Value; versionedFile = $entry.Key; sha256 = $hash }
}
$manifest = [ordered]@{ version = $Version; assets = $assets }
$manifestPath = Join-Path $directoryPath 'DOWNLOADS.json'
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8
$manifestHash = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksums += "$manifestHash  DOWNLOADS.json"
$checksums | Sort-Object | Set-Content -LiteralPath (Join-Path $directoryPath 'SHA256SUMS.txt') -Encoding ascii
Write-Host "Prepared $($assets.Count) permanent asset names for $Version"
