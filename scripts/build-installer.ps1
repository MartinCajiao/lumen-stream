# Build Lumen installer (Setup.exe + optional MSI if WiX is installed)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$out = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $out | Out-Null

dotnet publish src\Lumen.Launcher\Lumen.Launcher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$out\Lumen"
dotnet publish src\Lumen.Setup\Lumen.Setup.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "$out\Setup"

Copy-Item "$out\Setup\Lumen-Setup.exe" "$out\Lumen-Setup.exe" -Force

$wix = Get-Command wix -ErrorAction SilentlyContinue
if ($wix) {
    wix build installer\Lumen.wxs -o "$out\Lumen.msi" -d LumenDir="$out\Lumen"
    Write-Host "MSI: $out\Lumen.msi"
} else {
    Write-Host "WiX no está. Tienes Setup: $out\Lumen-Setup.exe"
    Write-Host "Ese exe instala Apollo + Moonlight. Copia la carpeta dist\Lumen y el Setup juntos."
}

Write-Host "App: $out\Lumen\Lumen.exe"
Write-Host "Después del Setup, al abrir Lumen ya no pide Apollo a mano."
