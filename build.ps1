$ErrorActionPreference = 'Stop'
Get-Process NetSpeedMonitor -ErrorAction SilentlyContinue | Stop-Process -Force
$src = Join-Path $PSScriptRoot 'src/NetSpeedMonitor'
$root = $PSScriptRoot
$publishDir = Join-Path $root 'publish'
$liteTemp = Join-Path $root '.build-lite-temp'
$legacyLiteDir = Join-Path $root 'publish-fd'

# Clean previous outputs so only the two current exes remain in publish/
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
if (Test-Path $legacyLiteDir) { Remove-Item $legacyLiteDir -Recurse -Force }

Push-Location $src
try {
    # Lite build: publish to a temp folder, then place it into publish/ under a distinct name
    Write-Host '>>> Building lite (framework-dependent) build'
    if (Test-Path $liteTemp) { Remove-Item $liteTemp -Recurse -Force }
    dotnet publish -c Release -r win-x64 --self-contained false `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=none `
        -o $liteTemp
    if ($LASTEXITCODE -ne 0) { throw 'Lite publish failed' }

    if (-not (Test-Path $publishDir)) { New-Item -ItemType Directory -Path $publishDir | Out-Null }
    Copy-Item -Path (Join-Path $liteTemp 'NetSpeedMonitor.exe') `
              -Destination (Join-Path $publishDir 'NetSpeedMonitor-lite.exe') -Force
    Remove-Item $liteTemp -Recurse -Force

    # Standard build: self-contained single file, same publish/ folder
    Write-Host '>>> Building standard (self-contained) build'
    dotnet publish -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=none `
        -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw 'Standard publish failed' }
} finally {
    Pop-Location
    if (Test-Path $liteTemp) { Remove-Item $liteTemp -Recurse -Force }
}
Write-Host '>>> Done. Output: publish/ -> NetSpeedMonitor.exe (standard), NetSpeedMonitor-lite.exe (lite)'
