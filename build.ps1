$ErrorActionPreference = 'Stop'
Get-Process NetSpeedMonitor -ErrorAction SilentlyContinue | Stop-Process -Force
# Wait for file handles to be released before overwriting exes
Start-Sleep -Seconds 2
$src = Join-Path $PSScriptRoot 'src/NetSpeedMonitor'
$root = $PSScriptRoot
$publishDir = Join-Path $root 'publish'
$liteTemp = Join-Path $root '.build-lite-temp'
$legacyLiteDir = Join-Path $root 'publish-fd'

# Clean stale outputs (best effort, via .NET API): dotnet publish overwrites the fixed
# exe names, so we only remove legacy folders / stale pdbs and never fail the build.
if (Test-Path $legacyLiteDir) { try { [System.IO.Directory]::Delete($legacyLiteDir, $true) } catch {} }
if (Test-Path $publishDir) {
    foreach ($f in [System.IO.Directory]::GetFiles($publishDir, '*.pdb')) {
        try { [System.IO.File]::Delete($f) } catch {}
    }
}

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
    # Copy with retry: a just-killed process may hold the file handle briefly
    $dest = Join-Path $publishDir 'NetSpeedMonitor-lite.exe'
    for ($i = 0; ; $i++) {
        try {
            if (Test-Path $dest) { [System.IO.File]::Delete($dest) }
            Copy-Item -Path (Join-Path $liteTemp 'NetSpeedMonitor.exe') -Destination $dest -Force
            break
        } catch {
            if ($i -ge 4) { throw }
            Start-Sleep -Seconds 2
        }
    }
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
