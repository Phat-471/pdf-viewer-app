param(
    [Parameter(Mandatory = $true)]
    [string]$Name,

    [string[]]$Files = @(
        "src\PdfViewerApp\PrintOptionsDialog.xaml",
        "src\PdfViewerApp\PrintOptionsDialog.xaml.cs",
        "src\PdfViewerApp\PdfDocumentTab.Printing.cs",
        "src\PdfViewerApp\NativePdfPrinter.cs",
        "src\PdfViewerApp\PdfDocumentPaginator.cs",
        "README.md"
    )
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$safeName = ($Name -replace '[^a-zA-Z0-9_.-]', '_').Trim('_')
if ([string]::IsNullOrWhiteSpace($safeName)) {
    throw "Backup name is empty after sanitizing."
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupRoot = Join-Path $repoRoot "backups"
$backupDir = Join-Path $backupRoot "${timestamp}_before_$safeName"
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null

$manifestPath = Join-Path $backupDir "backup_manifest.txt"
$copied = New-Object System.Collections.Generic.List[string]
$missing = New-Object System.Collections.Generic.List[string]

foreach ($relativePath in $Files) {
    $sourcePath = Join-Path $repoRoot $relativePath
    if (-not (Test-Path $sourcePath)) {
        $missing.Add($relativePath)
        continue
    }

    $targetPath = Join-Path $backupDir $relativePath
    $targetParent = Split-Path -Parent $targetPath
    New-Item -ItemType Directory -Force -Path $targetParent | Out-Null
    Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
    $copied.Add($relativePath)
}

$manifest = @()
$manifest += "PDF Pro feature backup"
$manifest += "Created: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$manifest += "Feature: $Name"
$manifest += "Repo: $repoRoot"
$manifest += ""
$manifest += "Copied files:"
foreach ($file in $copied) {
    $manifest += "- $file"
}

if ($missing.Count -gt 0) {
    $manifest += ""
    $manifest += "Missing files:"
    foreach ($file in $missing) {
        $manifest += "- $file"
    }
}

$manifest += ""
$manifest += "Rollback example:"
$manifest += "Copy files from this backup folder back to the same relative paths in the repo."

Set-Content -LiteralPath $manifestPath -Value $manifest -Encoding UTF8

Write-Host "Backup created:"
Write-Host $backupDir
Write-Host "Copied: $($copied.Count) file(s)"
if ($missing.Count -gt 0) {
    Write-Host "Missing: $($missing.Count) file(s)"
}
