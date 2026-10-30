param(
    [Parameter(Mandatory)][ValidateSet('win-x64','win-arm64')][string]$Runtime,
    [string]$AppVersion = '0.2.2',
    [string]$PackageVersion = '1.0.0.0',
    [string]$IdentityName,
    [string]$Publisher,
    [string]$PublisherDisplayName,
    [switch]$Preview,
    [string]$MakeAppxPath,
    [string]$PublishDirectory,
    [string]$OutputDirectory,
    [switch]$StageOnly
)
$ErrorActionPreference = 'Stop'
if (!$IsWindows) { throw 'MSIX packaging requires Windows' }
if ($AppVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'AppVersion must be a stable semantic version' }
if ($PackageVersion -notmatch '^[1-9]\d*\.\d+\.\d+\.0$' -or @($PackageVersion.Split('.') | Where-Object { [long]$_ -gt 65535 }).Count) { throw 'Store PackageVersion requires four components, first 1-65535, last 0' }
if ($Preview) {
    if ($IdentityName -or $Publisher -or $PublisherDisplayName) { throw 'Do not mix preview and Store identities' }
    $IdentityName = 'DCWallet.Preview'; $Publisher = 'CN=DCWallet Preview'; $PublisherDisplayName = 'DC WALLET Preview'
} elseif (!$IdentityName -or !$Publisher -or !$PublisherDisplayName) { throw 'Copy IdentityName, Publisher and PublisherDisplayName from Partner Center, or use -Preview for CI only' }
if ($IdentityName -notmatch '^[A-Za-z0-9.-]{3,50}$' -or !$Publisher.StartsWith('CN=') -or $PublisherDisplayName.Length -gt 256) { throw 'Invalid package identity' }
$root = Split-Path -Parent $PSScriptRoot
if (!$PublishDirectory) { $PublishDirectory = Join-Path $root "artifacts/publish-store/$Runtime" }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts/msix' }
$binary = Join-Path $PublishDirectory 'DCWallet.exe'
if (!(Test-Path -LiteralPath $binary -PathType Leaf)) { throw 'Publish the self-contained app first' }
$stream = [IO.File]::OpenRead($binary)
try { $reader = [IO.BinaryReader]::new($stream); $stream.Position=0x3c; $offset=$reader.ReadInt32(); $stream.Position=$offset; if ($reader.ReadUInt32() -ne 0x4550) { throw 'Invalid PE signature' }; $machine=$reader.ReadUInt16() } finally { $stream.Dispose() }
$expected = if ($Runtime -eq 'win-x64') { 0x8664 } else { 0xaa64 }
if ($machine -ne $expected) { throw 'Published executable architecture does not match Runtime' }
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$output = (Resolve-Path -LiteralPath $OutputDirectory).Path
$stage = Join-Path $output ("stage-$Runtime-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force (Join-Path $stage 'Assets') | Out-Null
# Copy only published app payload, not a build tree or local wallet data.
Get-ChildItem -LiteralPath $PublishDirectory -File | Where-Object { $_.Extension -in '.exe','.dll','.json','.dat','.config' } | Copy-Item -Destination $stage
Add-Type -AssemblyName System.Drawing
$image = [Drawing.Image]::FromFile((Join-Path $root 'src/DCWallet/Assets/icon.png'))
try {
    foreach ($asset in @(@('StoreLogo.png',50),@('Square44x44Logo.png',44),@('Square150x150Logo.png',150))) {
        $bitmap=[Drawing.Bitmap]::new([int]$asset[1],[int]$asset[1]); $graphics=[Drawing.Graphics]::FromImage($bitmap)
        try { $graphics.Clear([Drawing.Color]::Transparent); $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic; $graphics.DrawImage($image,0,0,[int]$asset[1],[int]$asset[1]); $bitmap.Save((Join-Path $stage ('Assets/'+$asset[0])),[Drawing.Imaging.ImageFormat]::Png) } finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
} finally { $image.Dispose() }
$manifest = Get-Content (Join-Path $root 'packaging/windows/AppxManifest.xml.template') -Raw
$values = @{ IdentityName=$IdentityName; Publisher=$Publisher; PublisherDisplayName=$PublisherDisplayName; PackageVersion=$PackageVersion; Architecture=$Runtime.Substring(4) }
foreach ($key in $values.Keys) { $manifest=$manifest.Replace('{{'+$key+'}}',[Security.SecurityElement]::Escape($values[$key])) }
[xml]$manifest | Out-Null
$manifest | Set-Content -LiteralPath (Join-Path $stage 'AppxManifest.xml') -Encoding utf8
foreach ($name in @('LICENSE','THIRD-PARTY-NOTICES.md')) { if (Test-Path (Join-Path $root $name)) { Copy-Item (Join-Path $root $name) $stage } }
Copy-Item -LiteralPath (Join-Path $root 'vendor/LICENSE') -Destination (Join-Path $stage 'LICENSE.Decimal.SDK.txt')
Copy-Item -LiteralPath (Join-Path $root 'docs/PRIVACY.md') -Destination (Join-Path $stage 'PRIVACY.md')
$mode = if ($Preview) { 'preview' } else { 'store' }
$package=Join-Path $output "DCWallet-$AppVersion-$Runtime-$mode.msix"
[ordered]@{ appVersion=$AppVersion; packageVersion=$PackageVersion; architecture=$Runtime.Substring(4); identityName=$IdentityName; publisher=$Publisher; preview=[bool]$Preview; storeIdentityProvided=(!$Preview); signed=$false; stage=$stage; package=$package } | ConvertTo-Json | Set-Content (Join-Path $output "$Runtime-$mode.json") -Encoding utf8
if ($StageOnly) { Write-Host "Staged $stage (not a packed MSIX)"; return }
if (!$MakeAppxPath) {
    $command=Get-Command makeappx.exe -ErrorAction SilentlyContinue
    if ($command) { $MakeAppxPath=$command.Source } else {
        $sdkRoot=Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
        $MakeAppxPath=Get-ChildItem -LiteralPath $sdkRoot -Filter makeappx.exe -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Directory.Name -eq 'x64' } | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    }
}
if (!$MakeAppxPath -or !(Test-Path -LiteralPath $MakeAppxPath)) { throw 'Install Windows SDK MakeAppx.exe, or provide -MakeAppxPath' }
& $MakeAppxPath pack /d $stage /p $package /o /h SHA256
if ($LASTEXITCODE -ne 0) { throw 'MakeAppx schema validation or packaging failed' }
& (Join-Path $PSScriptRoot 'check-msix.ps1') -Package $package -Runtime $Runtime -PackageVersion $PackageVersion -IdentityName $IdentityName -Publisher $Publisher -MakeAppxPath $MakeAppxPath
Write-Host "Unsigned $mode MSIX: $package"
