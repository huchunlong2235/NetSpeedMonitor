$ErrorActionPreference = 'Stop'
Get-Process NetSpeedMonitor -ErrorAction SilentlyContinue | Stop-Process -Force
$src = Join-Path $PSScriptRoot 'src/NetSpeedMonitor'
$root = $PSScriptRoot

Push-Location $src
try {
    Write-Host '>>> 打包轻量版 (framework-dependent)'
    dotnet publish -c Release -r win-x64 --self-contained false `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -o (Join-Path $root 'publish-fd')
    if ($LASTEXITCODE -ne 0) { throw '轻量版打包失败' }

    Write-Host '>>> 打包独立版 (self-contained)'
    dotnet publish -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -o (Join-Path $root 'publish')
    if ($LASTEXITCODE -ne 0) { throw '独立版打包失败' }
} finally {
    Pop-Location
}
Write-Host '>>> 完成。产物位于 publish-fd/ 与 publish/'
