$ErrorActionPreference = 'Stop'
Get-Process NetSpeedMonitor -ErrorAction SilentlyContinue | Stop-Process -Force
$src = Join-Path $PSScriptRoot 'src/NetSpeedMonitor'
$root = $PSScriptRoot

Push-Location $src
try {
    Write-Host '>>> Building framework-dependent build'
    dotnet publish -c Release -r win-x64 --self-contained false `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -o (Join-Path $root 'publish-fd')
    if ($LASTEXITCODE -ne 0) { throw 'Framework-dependent publish failed' }

    Write-Host '>>> Building self-contained build'
    dotnet publish -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -o (Join-Path $root 'publish')
    if ($LASTEXITCODE -ne 0) { throw 'Self-contained publish failed' }
} finally {
    Pop-Location
}
Write-Host '>>> Done. Output: publish-fd/ and publish/'
