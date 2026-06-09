$ErrorActionPreference = "Stop"

$baseKey = "HKCU:\Software\Classes\SystemFileAssociations\.pdf\shell\PdfPro.Merge"

if (Test-Path $baseKey) {
    Remove-Item -Path $baseKey -Recurse -Force
    Write-Host "Removed context menu entry for .pdf files."
} else {
    Write-Host "Context menu entry was not installed."
}
