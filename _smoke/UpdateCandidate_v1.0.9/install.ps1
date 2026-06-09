# install.ps1
# PDF Pro - Automated Installer for Desktop Shortcut & Explorer Context Menu
$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

Write-Host "=== Cài đặt PDF Pro - HPhat Edition ===" -ForegroundColor Cyan

$installDir = Join-Path $env:LOCALAPPDATA "PDFPro"
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

$wpfProjectDir = Join-Path $scriptDir "src\PdfViewerApp"
if (Test-Path $wpfProjectDir) {
    # 1. Developer Mode: Build and Publish
    Write-Host "`n[1/4] Biên dịch và xuất bản ứng dụng từ mã nguồn..." -ForegroundColor Yellow
    Set-Location $wpfProjectDir
    & dotnet publish -c Release -r win-x64 --self-contained true
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Biên dịch thất bại!"
        exit 1
    }
    Set-Location $scriptDir

    # Copy native dependencies
    Copy-Item "libs\pdfium.dll" -Destination "src\PdfViewerApp\bin\Release\net8.0-windows\win-x64\publish\pdfium.dll" -Force
    Copy-Item "libs\pdf_core.dll" -Destination "src\PdfViewerApp\bin\Release\net8.0-windows\win-x64\publish\pdf_core.dll" -Force

    $publishDir = Join-Path $scriptDir "src\PdfViewerApp\bin\Release\net8.0-windows\win-x64\publish"
    Write-Host "`n[2/4] Thiết lập thư mục ứng dụng..." -ForegroundColor Yellow
    Copy-Item -Path "$publishDir\*" -Destination $installDir -Recurse -Force
} else {
    # 2. Standalone Mode: Install directly from extracted files
    Write-Host "`n[1/4] Phát hiện chế độ cài đặt nhanh di động..." -ForegroundColor Yellow
    Write-Host "`n[2/4] Sao chép tệp ứng dụng..." -ForegroundColor Yellow
    Copy-Item -Path "$scriptDir\*" -Destination $installDir -Exclude "install.ps1", "*.zip" -Recurse -Force
}

Write-Host "    Đã cài đặt tệp ứng dụng vào: $installDir" -ForegroundColor Green

# 3. Create Desktop Shortcut
Write-Host "`n[3/4] Tạo phím tắt trên Desktop (Shortcut)..." -ForegroundColor Yellow
$exePath = Join-Path $installDir "PdfViewerApp.exe"
$desktopPath = [System.IO.Path]::Combine([System.Environment]::GetFolderPath('Desktop'), "PDF Pro - HPhat Edition.lnk")

$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut($desktopPath)
$Shortcut.TargetPath = $exePath
$Shortcut.WorkingDirectory = $installDir
$Shortcut.IconLocation = "$exePath,0"
$Shortcut.Description = "PDF Pro - HPhat Edition"
$Shortcut.Save()
Write-Host "    Đã tạo shortcut trên Desktop!" -ForegroundColor Green

# 4. Install Registry Explorer Context Menu
Write-Host "`n[4/4] Đăng ký Menu chuột phải (Right-Click Context Menu) & Mở mặc định..." -ForegroundColor Yellow

# Register right-click merge
$baseKey = "HKCU:\Software\Classes\SystemFileAssociations\.pdf\shell\PdfPro.Merge"
$commandKey = Join-Path $baseKey "command"
New-Item -Path $commandKey -Force | Out-Null

$muiVerbValue = "Gh" + [char]0x00E9 + "p PDF b" + [char]0x1EB1 + "ng PDF HPhat"
New-ItemProperty -Path $baseKey -Name "MUIVerb" -Value $muiVerbValue -PropertyType String -Force | Out-Null
New-ItemProperty -Path $baseKey -Name "Icon" -Value $exePath -PropertyType String -Force | Out-Null
New-ItemProperty -Path $baseKey -Name "MultiSelectModel" -Value "Player" -PropertyType String -Force | Out-Null
New-ItemProperty -Path $baseKey -Name "Position" -Value "Top" -PropertyType String -Force | Out-Null

$command = "`"$exePath`" `"%1`" --merge --exit-after-merge"
Set-ItemProperty -Path $commandKey -Name "(default)" -Value $command

# Register Default PDF Handler association capability
$appRegKey = "HKCU:\Software\Classes\Applications\PdfViewerApp.exe\shell\open\command"
New-Item -Path $appRegKey -Force | Out-Null
Set-ItemProperty -Path $appRegKey -Name "(default)" -Value "`"$exePath`" `"%1`""

Write-Host "    Đăng ký Menu chuột phải hoàn tất!" -ForegroundColor Green

Write-Host "`n=== CÀI ĐẶT THÀNH CÔNG! ===" -ForegroundColor Cyan
Write-Host "Bạn có thể đóng cửa sổ này, nhấp đúp vào biểu tượng trên Desktop để chạy,"
Write-Host "hoặc chọn nhiều tệp PDF, nhấp chuột phải và chọn 'Ghép PDF bằng PDF HPhat'!" -ForegroundColor Yellow

Read-Host "`nNhấn Enter để hoàn tất..."
