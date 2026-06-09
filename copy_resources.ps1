# copy_resources.ps1
# Safe script to copy pdfium.dll from backup folder

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$dllSrc = Join-Path $scriptDir "old_project\build\windows\x64\runner\Release\pdfium.dll"
$libsDir = Join-Path $scriptDir "libs"

if (-not (Test-Path $libsDir)) {
    New-Item -ItemType Directory -Path $libsDir -Force
}

if (Test-Path $dllSrc) {
    Copy-Item $dllSrc -Destination (Join-Path $libsDir "pdfium.dll") -Force
    Write-Host "SUCCESS: Copied pdfium.dll to /libs/ folder" -ForegroundColor Green
} else {
    Write-Warning "COULD NOT find pdfium.dll at $dllSrc"
}
