# uninstall.ps1
# PDF Pro - Automated Uninstaller for Desktop Shortcut, App Folder & Explorer Context Menu
$ErrorActionPreference = "Stop"

# 1. Require Administrator Elevation
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Dang yeu cau quyen Administrator de go cai dat..." -ForegroundColor Yellow
    try {
        Start-Process powershell -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs -Wait
    } catch {
        Write-Error "Yeu cau quyen Administrator bi tu choi!"
    }
    exit
}

Write-Host "=== Go Cai Dat PDF Pro - HPhat Edition ===" -ForegroundColor Cyan

# 2. Terminate any running PdfViewerApp process
Write-Host "`n[1/4] Dang dong cac ung dung dang chay..." -ForegroundColor Yellow
Stop-Process -Name PdfViewerApp -Force -ErrorAction SilentlyContinue
Get-Process | Where-Object {$_.Path -like "*PdfViewerApp*"} | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

# 3. Clean up Desktop Shortcut
Write-Host "`n[2/4] Dang xoa phim tat tren Desktop..." -ForegroundColor Yellow
$desktopPath = [System.IO.Path]::Combine([System.Environment]::GetFolderPath('Desktop'), "PDF Pro - HPhat Edition.lnk")
if (Test-Path $desktopPath) {
    Remove-Item $desktopPath -Force
    Write-Host "    Da xoa phim tat tren Desktop." -ForegroundColor Green
}

# 4. Clean up Registry Keys
Write-Host "`n[3/4] Dang xoa cau hinh Registry va Menu chuot phai..." -ForegroundColor Yellow
Remove-Item -Path "HKCU:\Software\Classes\SystemFileAssociations\.pdf\shell\PdfPro.Merge" -Recurse -ErrorAction SilentlyContinue
Remove-Item -Path "HKCU:\Software\Classes\Applications\PdfViewerApp.exe" -Recurse -ErrorAction SilentlyContinue
Remove-Item -Path "HKCU:\Software\Classes\PdfViewerApp.Document" -Recurse -ErrorAction SilentlyContinue
Remove-Item -Path "HKCU:\Software\Classes\PDFPro.PDF" -Recurse -ErrorAction SilentlyContinue
Remove-Item -Path "HKCU:\Software\Classes\.pdf\OpenWithList\PdfViewerApp.exe" -Recurse -ErrorAction SilentlyContinue
Remove-Item -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\PdfViewerApp.exe" -Recurse -ErrorAction SilentlyContinue
Remove-Item -Path "HKCU:\Software\PDFPro" -Recurse -ErrorAction SilentlyContinue

# Clean OpenWithProgids & RegisteredApplications
$openWithProgIdsKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.pdf\OpenWithProgids"
if (Test-Path $openWithProgIdsKey) {
    Remove-ItemProperty -Path $openWithProgIdsKey -Name "PdfViewerApp.Document" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $openWithProgIdsKey -Name "PDFPro.PDF" -ErrorAction SilentlyContinue
}
$classesPdfOpenWithProgids = "HKCU:\Software\Classes\.pdf\OpenWithProgids"
if (Test-Path $classesPdfOpenWithProgids) {
    Remove-ItemProperty -Path $classesPdfOpenWithProgids -Name "PDFPro.PDF" -ErrorAction SilentlyContinue
}
$registeredAppsKey = "HKCU:\Software\RegisteredApplications"
if (Test-Path $registeredAppsKey) {
    Remove-ItemProperty -Path $registeredAppsKey -Name "PDF Pro" -ErrorAction SilentlyContinue
}
Write-Host "    Da xoa cac khoa registry cau hinh." -ForegroundColor Green

# 5. Clean up Virtual Printer
Write-Host "`n[4/4] Dang go bo may in ao 'PDF Pro - HPhat Edition'..." -ForegroundColor Yellow
$printerName = "PDF Pro - HPhat Edition"
$oldPortName = "PDFPro_HPhat_Port:"
try {
    if (Get-Printer -Name $printerName -ErrorAction SilentlyContinue) {
        Remove-Printer -Name $printerName -ErrorAction Stop
        Write-Host "    Da go bo may in ao." -ForegroundColor Green
    }
    if (Get-PrinterPort -Name $oldPortName -ErrorAction SilentlyContinue) {
        Remove-PrinterPort -Name $oldPortName -ErrorAction Stop
        Write-Host "    Da xoa cong may in cu." -ForegroundColor Green
    }
} catch {
    Write-Host "    Loi khi go bo may in ao: $($_.Exception.Message)" -ForegroundColor DarkYellow
}

# 6. Clean up AppData Directory
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

Write-Host "`n=== GO CAI DAT HOAN TAT VA THANH CONG! ===" -ForegroundColor Cyan
Read-Host "`nNhan Enter de hoan tat..."
