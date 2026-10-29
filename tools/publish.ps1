param(
    [ValidateSet('win-x64','win-arm64','linux-x64','linux-arm64','osx-x64','osx-arm64')][string]$Runtime = 'win-x64',
    [string]$Version = '0.2.0-alpha.3'
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+(-[A-Za-z0-9.]+)?$') { throw 'Invalid version' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$publishDirectory = Join-Path $projectRoot "artifacts/publish/$Runtime"
$releaseDirectory = Join-Path $projectRoot 'artifacts/releases'
New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
dotnet publish (Join-Path $projectRoot 'src/DCWallet/DCWallet.csproj') -c Release -r $Runtime --self-contained true -p:Version=$Version -p:PublishSingleFile=true -p:PublishTrimmed=false -p:DebugType=None -p:NuGetAudit=false -o $publishDirectory
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
$extension = if ($Runtime.StartsWith('win-')) { '.exe' } else { '' }
$name = "DCWallet-$Version-$Runtime$extension"
Copy-Item -LiteralPath (Join-Path $publishDirectory "DCWallet$extension") -Destination (Join-Path $releaseDirectory $name)
$hostPlatform = if ($IsWindows) { 'win' } elseif ($IsLinux) { 'linux' } elseif ($IsMacOS) { 'osx' } else { 'unknown' }
$hostArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
if ($Runtime -eq "$hostPlatform-$hostArchitecture") {
    if ($IsWindows) {
        $checkProcess = Start-Process -FilePath (Join-Path $publishDirectory "DCWallet$extension") -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
        if ($checkProcess.ExitCode -ne 0) { throw 'Self-test failed' }
    } else {
        & (Join-Path $publishDirectory "DCWallet$extension") --self-test
        if ($LASTEXITCODE -ne 0) { throw 'Self-test failed' }
    }
}
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $releaseDirectory $name)).Hash.ToLowerInvariant()
"$hash  $name" | Set-Content -Encoding ascii -LiteralPath (Join-Path $releaseDirectory "$name.sha256")
Write-Host "Built $name"
