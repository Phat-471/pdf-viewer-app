# Remove a user installation. Prefer the registered Inno Setup uninstaller.
param([switch]$RemoveVirtualPrinter)
$ErrorActionPreference = 'Stop'
$canonicalDir = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'PDF Pro'))
$legacyDir = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'PDFPro'))
$scriptDir = [IO.Path]::GetFullPath($PSScriptRoot)
$installDir = if ($scriptDir -in @($canonicalDir, $legacyDir)) { $scriptDir }
    elseif (Test-Path -LiteralPath $canonicalDir) { $canonicalDir }
    else { $legacyDir }
if ($installDir -notin @($canonicalDir, $legacyDir)) { throw 'Invalid installation directory.' }
if (-not (Test-Path -LiteralPath $installDir)) { Write-Host 'PDF Pro is not installed.'; return }
$innoUninstaller = Join-Path $installDir 'unins000.exe'
if (Test-Path -LiteralPath $innoUninstaller) {
    Start-Process -FilePath $innoUninstaller -Wait
    return
}
$installPrefix = $installDir.TrimEnd('\') + '\'
$manifestPath = Join-Path $installDir 'installed-files.json'
if (Test-Path -LiteralPath $manifestPath) {
    $names = @(Get-Content -LiteralPath $manifestPath -Encoding UTF8 -Raw | ConvertFrom-Json)
} else {
    # Old ZIP installations did not record payload files. Keep unknown files and user data.
    $names = @('PdfViewerApp.exe','pdf_core.dll','pdfium.dll','VCRUNTIME140.dll','VCRUNTIME140_1.dll','MSVCP140.dll','CONCRT140.dll','install.ps1','install.bat','uninstall.ps1','uninstall.bat')
}
$paths = foreach ($name in $names) {
    if ([string]::IsNullOrWhiteSpace([string]$name) -or [IO.Path]::IsPathRooted([string]$name)) { throw 'Invalid installed file list.' }
    $path = [IO.Path]::GetFullPath((Join-Path $installDir ([string]$name)))
    if (-not $path.StartsWith($installPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Uninstall path is outside the application directory.' }
    $path
}
$exePath = Join-Path $installDir 'PdfViewerApp.exe'
Get-Process -Name PdfViewerApp -ErrorAction SilentlyContinue | Where-Object {
    $_.Path -and [IO.Path]::GetFullPath($_.Path) -eq $exePath
} | Stop-Process -Force
foreach ($path in $paths) {
    if (Test-Path -LiteralPath $path -PathType Leaf) { Remove-Item -LiteralPath $path -Force }
}
if (Test-Path -LiteralPath $manifestPath) { Remove-Item -LiteralPath $manifestPath -Force }
$desktopPath = Join-Path ([Environment]::GetFolderPath('Desktop')) 'PDF Pro - HPhat Edition.lnk'
if (Test-Path -LiteralPath $desktopPath) { Remove-Item -LiteralPath $desktopPath -Force }
foreach ($key in @(
    'HKCU:\Software\Classes\SystemFileAssociations\.pdf\shell\PdfPro.Merge',
    'HKCU:\Software\Classes\Applications\PdfViewerApp.exe',
    'HKCU:\Software\Classes\PdfViewerApp.Document',
    'HKCU:\Software\Classes\PDFPro.PDF',
    'HKCU:\Software\Classes\.pdf\OpenWithList\PdfViewerApp.exe',
    'HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\PdfViewerApp.exe',
    'HKCU:\Software\PDFPro\Capabilities'
)) { if (Test-Path -LiteralPath $key) { Remove-Item -LiteralPath $key -Recurse -Force } }
foreach ($key in @('HKCU:\Software\Classes\.pdf\OpenWithProgids', 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.pdf\OpenWithProgids')) {
    foreach ($name in @('PdfViewerApp.Document','PDFPro.PDF')) { Remove-ItemProperty -LiteralPath $key -Name $name -ErrorAction SilentlyContinue }
}
Remove-ItemProperty -LiteralPath 'HKCU:\Software\RegisteredApplications' -Name 'PDF Pro' -ErrorAction SilentlyContinue
if ($RemoveVirtualPrinter) {
    try { Remove-Printer -Name 'PDF Pro - HPhat Edition' -ErrorAction Stop }
    catch { Write-Warning "Could not remove the optional virtual printer: $_" }
}
# Keep the installation folder and unknown files: activation/settings are not application payload.
Write-Host 'PDF Pro application files removed. User settings and activation data were retained.' -ForegroundColor Green
