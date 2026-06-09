param(
    [ValidateSet("patch", "minor", "major")]
    [string]$Part = "patch",
    [switch]$SkipSmokeTest
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "Bumping version ($Part)..." -ForegroundColor Cyan
& (Join-Path $scriptDir "bump_version.ps1") -Part $Part

Write-Host "Packaging release..." -ForegroundColor Cyan
& (Join-Path $scriptDir "..\package_project.ps1")

Write-Host "Verifying update manifest..." -ForegroundColor Cyan
& (Join-Path $scriptDir "verify_update_manifest.ps1")

if (-not $SkipSmokeTest) {
    Write-Host "Running update candidate smoke test..." -ForegroundColor Cyan
    & (Join-Path $scriptDir "smoke_test_update_candidate.ps1")
}

Write-Host "Publishing release to WordPress..." -ForegroundColor Cyan
& (Join-Path $scriptDir "publish_release.ps1")

Write-Host "Release completed." -ForegroundColor Green
