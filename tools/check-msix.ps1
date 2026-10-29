param(
 [Parameter(Mandatory)][string]$Package,
 [Parameter(Mandatory)][ValidateSet('win-x64','win-arm64')][string]$Runtime,
 [Parameter(Mandatory)][string]$PackageVersion,
 [Parameter(Mandatory)][string]$IdentityName,
 [Parameter(Mandatory)][string]$Publisher,
 [Parameter(Mandatory)][string]$MakeAppxPath
)
$ErrorActionPreference='Stop'
$packagePath=(Resolve-Path -LiteralPath $Package).Path
$unpacked=Join-Path (Split-Path -Parent $packagePath) ('check-'+[guid]::NewGuid().ToString('N'))
& $MakeAppxPath unpack /p $packagePath /d $unpacked /o
if ($LASTEXITCODE -ne 0) { throw 'Cannot unpack MSIX' }
[xml]$manifest=Get-Content (Join-Path $unpacked 'AppxManifest.xml') -Raw
$identity=$manifest.Package.Identity
if ($identity.Name -ne $IdentityName -or $identity.Publisher -ne $Publisher -or $identity.Version -ne $PackageVersion -or $identity.ProcessorArchitecture -ne $Runtime.Substring(4)) { throw 'MSIX identity mismatch' }
$application=$manifest.Package.Applications.Application
if ($application.Executable -ne 'DCWallet.exe' -or $application.EntryPoint -ne 'Windows.FullTrustApplication') { throw 'MSIX desktop entry point mismatch' }
if (@($manifest.Package.Capabilities.ChildNodes | Where-Object { $_.LocalName -eq 'Capability' -and $_.GetAttribute('Name') -eq 'runFullTrust' }).Count -ne 1) { throw 'Missing runFullTrust capability' }
foreach ($asset in @('Assets/StoreLogo.png','Assets/Square44x44Logo.png','Assets/Square150x150Logo.png','DCWallet.exe')) { if (!(Test-Path -LiteralPath (Join-Path $unpacked $asset))) { throw "Missing package asset: $asset" } }
$archive=[IO.Compression.ZipFile]::OpenRead($packagePath)
try { foreach ($footprint in @('AppxBlockMap.xml','[Content_Types].xml')) { if (!$archive.GetEntry($footprint)) { throw "Missing MSIX footprint: $footprint" } } } finally { $archive.Dispose() }
$stream=[IO.File]::OpenRead((Join-Path $unpacked 'DCWallet.exe'))
try { $reader=[IO.BinaryReader]::new($stream); $stream.Position=0x3c; $offset=$reader.ReadInt32(); $stream.Position=$offset; if ($reader.ReadUInt32() -ne 0x4550) { throw 'Invalid packaged PE' }; $machine=$reader.ReadUInt16() } finally { $stream.Dispose() }
$expected=if ($Runtime -eq 'win-x64') {0x8664} else {0xaa64}
if ($machine -ne $expected) { throw 'Packaged payload architecture mismatch' }
$hash=(Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($packagePath))" | Set-Content -LiteralPath ($packagePath+'.sha256') -Encoding ascii
Write-Host "MSIX unpacked identity, payload and architecture verified: $Runtime"
