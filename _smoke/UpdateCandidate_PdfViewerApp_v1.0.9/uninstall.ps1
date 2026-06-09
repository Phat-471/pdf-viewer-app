# uninstall.ps1
# PDF Pro - Automated Uninstaller for Desktop Shortcut, App Folder & Explorer Context Menu
$ErrorActionPreference = "Stop"

Write-Host "=== G盻｡ Cﾃi ﾄ雪ｺｷt PDF Pro - HPhat Edition ===" -ForegroundColor Cyan

# 1. Clean up Desktop Shortcut
$desktopPath = [System.IO.Path]::Combine([System.Environment]::GetFolderPath('Desktop'), "PDF Pro - HPhat Edition.lnk")
if (Test-Path $desktopPath) {
    Remove-Item $desktopPath -Force
    Write-Host "    ﾄ静｣ xﾃｳa phﾃｭm t蘯ｯt trﾃｪn Desktop." -ForegroundColor Green
}

# 2. Clean up Registry Keys
Write-Host "    ﾄ紳ng xﾃｳa c蘯･u hﾃｬnh Menu chu盻冲 ph蘯｣i..." -ForegroundColor Yellow
Remove-Item -Path "HKCU:\Software\Classes\SystemFileAssociations\.pdf\shell\PdfPro.Merge" -Recurse -ErrorAction SilentlyContinue
Remove-Item -Path "HKCU:\Software\Classes\Applications\PdfViewerApp.exe" -Recurse -ErrorAction SilentlyContinue
Write-Host "    ﾄ静｣ xﾃｳa Menu chu盻冲 ph蘯｣i thﾃnh cﾃｴng." -ForegroundColor Green

# 3. Clean up AppData Directory
$installDir = Join-Path $env:LOCALAPPDATA "PDFPro"
if (Test-Path $installDir) {
    # If the uninstaller is running from AppData/Local/PDFPro, we copy a helper batch file to Temp to delete the folder after this script closes.
    $tempBatch = Join-Path $env:TEMP "pdfpro_uninstaller.bat"
    $batchContent = @"
@echo off
timeout /t 2 /nobreak > nul
if exist "$installDir" rd /s /q "$installDir"
del "%~f0"
"@
    $batchContent = $batchContent.Replace('"$installDir"', "`"$installDir`"")
    [System.IO.File]::WriteAllText($tempBatch, $batchContent, [System.Text.Encoding]::Default)

    # Start the helper batch file
    Start-Process -FilePath $tempBatch -WindowStyle Hidden
    Write-Host "    ﾄ静｣ lﾃｪn l盻議h xﾃｳa thﾆｰ m盻･c cﾃi ﾄ黛ｺｷt s蘯｡ch s蘯ｽ sau khi ﾄ妥ｳng c盻ｭa s盻・" -ForegroundColor Green
}

Write-Host "`n=== G盻 CﾃI ﾄ雪ｺｶT THﾃNH Cﾃ年G! ===" -ForegroundColor Cyan
Read-Host "`nNh蘯･n Enter ﾄ黛ｻ・hoﾃn t蘯･t..."
