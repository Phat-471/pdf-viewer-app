# uninstall.ps1
# PDF Pro - Automated Uninstaller for Desktop Shortcut, App Folder & Explorer Context Menu
$ErrorActionPreference = "Stop"

Write-Host "=== Go Cai Dat PDF Pro - HPhat Edition ===" -ForegroundColor Cyan

# 1. Clean up Desktop Shortcut
$desktopPath = [System.IO.Path]::Combine([System.Environment]::GetFolderPath('Desktop'), "PDF Pro - HPhat Edition.lnk")
if (Test-Path $desktopPath) {
    Remove-Item $desktopPath -Force
    Write-Host "    Da xoa phim tat tren Desktop." -ForegroundColor Green
}

# 2. Clean up Registry Keys
Write-Host "    Dang xoa cau hinh Registry va Menu chuot phai..." -ForegroundColor Yellow
Remove-Item -Path "HKCU:\Software\Classes\SystemFileAssociations\.pdf\shell\PdfPro.Merge" -Recurse -ErrorAction SilentlyContinue
Remove-Item -Path "HKCU:\Software\Classes\Applications\PdfViewerApp.exe" -Recurse -ErrorAction SilentlyContinue
Remove-Item -Path "HKCU:\Software\PDFPro" -Recurse -ErrorAction SilentlyContinue
Write-Host "    Da xoa cac khoa cau hinh registry." -ForegroundColor Green

# 3. Clean up Virtual Printer
Write-Host "    Dang go bo may in ao 'PDF Pro - HPhat Edition'..." -ForegroundColor Yellow
$printerName = "PDF Pro - HPhat Edition"
$portName    = "PDFPro_HPhat_Port:"
try {
    if (Get-Printer -Name $printerName -ErrorAction SilentlyContinue) {
        Remove-Printer -Name $printerName -ErrorAction Stop
        Write-Host "    Da go bo may in ao." -ForegroundColor Green
    }
    if (Get-PrinterPort -Name $portName -ErrorAction SilentlyContinue) {
        Remove-PrinterPort -Name $portName -ErrorAction Stop
        Write-Host "    Da xoa cong may in." -ForegroundColor Green
    }
} catch {
    Write-Host "    Luu y: Khong the go bo may in ao tu dong (yeu cau quyen Admin)." -ForegroundColor DarkYellow
}

# 4. Clean up AppData Directory
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
    Write-Host "    Da len lich xoa thu muc cai dat sach se sau khi dong cua so." -ForegroundColor Green
}

Write-Host "`n=== GO CAI DAT THANH CONG! ===" -ForegroundColor Cyan
Read-Host "`nNhan Enter de hoan tat..."
