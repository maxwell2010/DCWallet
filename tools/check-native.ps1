param(
    [Parameter(Mandatory)][ValidateSet('win-x64','win-arm64','linux-x64','linux-arm64','osx-x64','osx-arm64')][string]$Runtime
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "artifacts/native/$Runtime"
New-Item -ItemType Directory -Force -Path $output | Out-Null
$expected = $Runtime.Split('-')[1]
$frameworkDll = Join-Path $root 'src/DCWallet/bin/Release/net8.0/DCWallet.dll'
$hostJson = & dotnet $frameworkDll --runtime-info
if ($LASTEXITCODE -ne 0) { throw 'Framework-dependent runtime probe failed' }
$hostJson | Set-Content -LiteralPath (Join-Path $output 'test-host-runtime.json')
$testHost = $hostJson | ConvertFrom-Json
if ($testHost.processArchitecture -ne $expected -or $testHost.osArchitecture -ne $expected) { throw 'Test host must use the native target architecture' }
$extension = if ($Runtime.StartsWith('win-')) { '.exe' } else { '' }
$binary = Join-Path $root "artifacts/publish/$Runtime/DCWallet$extension"
$runtimeFile = Join-Path $output 'published-runtime.json'
if ($IsWindows) {
    $probe = Start-Process -FilePath $binary -ArgumentList '--runtime-info' -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $runtimeFile -RedirectStandardError (Join-Path $output 'runtime-stderr.txt')
    if ($probe.ExitCode -ne 0) { throw 'Published runtime probe failed' }
    $check = Start-Process -FilePath $binary -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput (Join-Path $output 'self-test.txt') -RedirectStandardError (Join-Path $output 'self-test-stderr.txt')
    if ($check.ExitCode -ne 0) { throw 'Published self-test failed' }
} else {
    & $binary --runtime-info | Set-Content -LiteralPath $runtimeFile
    if ($LASTEXITCODE -ne 0) { throw 'Published runtime probe failed' }
    & $binary --self-test | Set-Content -LiteralPath (Join-Path $output 'self-test.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Published self-test failed' }
}
$published = Get-Content -LiteralPath $runtimeFile -Raw | ConvertFrom-Json
if ($published.processArchitecture -ne $expected -or $published.osArchitecture -ne $expected) { throw 'Published binary must run natively, without architecture emulation' }
Write-Host "Native test host and published self-test verified: $Runtime"
