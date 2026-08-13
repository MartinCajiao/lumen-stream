#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Set-Location (Split-Path -Parent $PSScriptRoot)
git submodule update --init --depth 1
if (Test-Path "patches/moonlight-qt/0001-first-class-high-refresh.patch") {
    git -C client apply --check ../patches/moonlight-qt/0001-first-class-high-refresh.patch 2>$null
    if ($LASTEXITCODE -eq 0) {
        git -C client apply ../patches/moonlight-qt/0001-first-class-high-refresh.patch
    }
}
Write-Host "Submodules ready. Build Apollo and Moonlight, then copy the exes to vendor/ (see docs/BUILD.md)."
