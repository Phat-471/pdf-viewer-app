# install.ps1
# PDF Pro - Automated Installer for Desktop Shortcut & Explorer Context Menu
$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

Write-Host "=== Cai dat PDF Pro - HPhat Edition ===" -ForegroundColor Cyan

$installDir = Join-Path $env:LOCALAPPDATA "PDFPro"
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

$wpfProjectDir = Join-Path $scriptDir "src\PdfViewerApp"
if (Test-Path $wpfProjectDir) {
    # 1. Developer Mode: Build and Publish
    Write-Host "`n[1/4] Bien dich va xuat ban ung dung tu ma nguon..." -ForegroundColor Yellow
    Set-Location $wpfProjectDir
    & dotnet publish -c Release -r win-x64 --self-contained true
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Bien dich that bai!"
        exit 1
    }
    Set-Location $scriptDir

    # Copy native dependencies
    Copy-Item "libs\pdfium.dll" -Destination "src\PdfViewerApp\bin\Release\net8.0-windows\win-x64\publish\pdfium.dll" -Force
    Copy-Item "libs\pdf_core.dll" -Destination "src\PdfViewerApp\bin\Release\net8.0-windows\win-x64\publish\pdf_core.dll" -Force

    $publishDir = Join-Path $scriptDir "src\PdfViewerApp\bin\Release\net8.0-windows\win-x64\publish"
    Write-Host "`n[2/4] Thiet lap thu muc ung dung..." -ForegroundColor Yellow
    Copy-Item -Path "$publishDir\*" -Destination $installDir -Recurse -Force
} else {
    # 2. Standalone Mode: Install directly from extracted files
    Write-Host "`n[1/4] Phat hien che do cai dat nhanh di dong..." -ForegroundColor Yellow
    Write-Host "`n[2/4] Sao chep tep ung dung..." -ForegroundColor Yellow
    Copy-Item -Path "$scriptDir\*" -Destination $installDir -Exclude "install.ps1", "*.zip" -Recurse -Force
}

Write-Host "    Da cai dat tep ung dung vao: $installDir" -ForegroundColor Green

# 3. Create Desktop Shortcut
Write-Host "`n[3/4] Tao phim tat tren Desktop (Shortcut)..." -ForegroundColor Yellow
$exePath = Join-Path $installDir "PdfViewerApp.exe"
$desktopPath = [System.IO.Path]::Combine([System.Environment]::GetFolderPath('Desktop'), "PDF Pro - HPhat Edition.lnk")

$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut($desktopPath)
$Shortcut.TargetPath = $exePath
$Shortcut.WorkingDirectory = $installDir
$Shortcut.IconLocation = "$exePath,0"
$Shortcut.Description = "PDF Pro - HPhat Edition"
$Shortcut.Save()
Write-Host "    Da tao shortcut tren Desktop!" -ForegroundColor Green

# 4. Install Registry Explorer Context Menu
Write-Host "`n[4/4] Dang ky Menu chuot phai (Right-Click Context Menu) & Mo mac dinh..." -ForegroundColor Yellow

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

# Register FriendlyAppName so "Open with" shows correct app display name
$appRootKey = "HKCU:\Software\Classes\Applications\PdfViewerApp.exe"
New-ItemProperty -Path $appRootKey -Name "FriendlyAppName" -Value "PDF Pro - HPhat Edition" -PropertyType String -Force | Out-Null
New-ItemProperty -Path $appRootKey -Name "FriendlyTypeName" -Value "PDF Pro - HPhat Edition" -PropertyType String -Force | Out-Null

# Register app description for .pdf Open With list
$appShellKey = "HKCU:\Software\Classes\Applications\PdfViewerApp.exe\shell\open"
New-ItemProperty -Path $appShellKey -Name "FriendlyAppName" -Value "PDF Pro - HPhat Edition" -PropertyType String -Force | Out-Null

# Register as available OpenWithProgIds for .pdf so it appears in Open With list with proper name
$openWithKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.pdf\OpenWithList"
if (-not (Test-Path $openWithKey)) {
    New-Item -Path $openWithKey -Force | Out-Null
}
$existingValues = (Get-Item $openWithKey -ErrorAction SilentlyContinue).Property | Where-Object { $_ -ne 'MRUList' }
$nextLetter = 'a'
if ($existingValues) {
    $usedLetters = $existingValues
    $nextLetter = [char](([int][char]($usedLetters | Sort-Object | Select-Object -Last 1)) + 1)
}
New-ItemProperty -Path $openWithKey -Name $nextLetter -Value "PdfViewerApp.exe" -PropertyType String -Force | Out-Null

# Register as OpenWithProgIds
$openWithProgIdsKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.pdf\OpenWithProgids"
if (-not (Test-Path $openWithProgIdsKey)) {
    New-Item -Path $openWithProgIdsKey -Force | Out-Null
}
New-ItemProperty -Path $openWithProgIdsKey -Name "PdfViewerApp.Document" -Value ([byte[]]@()) -PropertyType Binary -Force | Out-Null

# Register app ProgID with display name
$progIdKey = "HKCU:\Software\Classes\PdfViewerApp.Document"
New-Item -Path "$progIdKey\shell\open\command" -Force | Out-Null
New-ItemProperty -Path $progIdKey -Name "(default)" -Value "PDF Pro - HPhat Edition" -PropertyType String -Force | Out-Null
New-ItemProperty -Path $progIdKey -Name "FriendlyTypeName" -Value "PDF Pro - HPhat Edition" -PropertyType String -Force | Out-Null
Set-ItemProperty -Path "$progIdKey\shell\open\command" -Name "(default)" -Value "`"$exePath`" `"%1`""

Write-Host "    Dang ky Menu chuot phai hoan tat!" -ForegroundColor Green

# 5. Register Virtual PDF Printer (Print to PDF Pro)
Write-Host "`n[5/5] Dang ky may in ao 'PDF Pro - HPhat Edition'..." -ForegroundColor Yellow
$printerName = "PDF Pro - HPhat Edition"
$portName    = "PDFPro_HPhat_Port:"
$driverName  = "Microsoft Print To PDF"

try {
    # Remove old printer if exists
    if (Get-Printer -Name $printerName -ErrorAction SilentlyContinue) {
        Remove-Printer -Name $printerName -ErrorAction SilentlyContinue
        Write-Host "    Da xoa may in cu." -ForegroundColor DarkGray
    }

    # Remove old port if exists
    if (Get-PrinterPort -Name $portName -ErrorAction SilentlyContinue) {
        Remove-PrinterPort -Name $portName -ErrorAction SilentlyContinue
    }

    # Check if Microsoft Print To PDF driver is available
    $driver = Get-PrinterDriver -Name $driverName -ErrorAction SilentlyContinue
    if ($driver -eq $null) {
        Write-Host "    Khong tim thay driver '$driverName'. Bo qua cai may in ao." -ForegroundColor DarkYellow
    } else {
        # Add a new FILE: port for the virtual printer
        Add-PrinterPort -Name $portName -ErrorAction SilentlyContinue

        # Add the virtual printer
        Add-Printer -Name $printerName -DriverName $driverName -PortName $portName -ErrorAction Stop
        Write-Host "    Da cai may in ao: $printerName" -ForegroundColor Green

        # Register redirect: when user prints to this printer, open saved PDF in our app
        # This sets a registry key that our app reads when PDF is produced by the print port
        $printRegKey = "HKCU:\Software\PDFPro\VirtualPrinter"
        New-Item -Path $printRegKey -Force | Out-Null
        New-ItemProperty -Path $printRegKey -Name "PrinterName" -Value $printerName -PropertyType String -Force | Out-Null
        New-ItemProperty -Path $printRegKey -Name "AppPath" -Value $exePath -PropertyType String -Force | Out-Null
        New-ItemProperty -Path $printRegKey -Name "AutoOpen" -Value 1 -PropertyType DWord -Force | Out-Null

        Write-Host "    May in ao da san sang! In tu bat ky ung dung nao, chon '$printerName' de luu PDF." -ForegroundColor Green
    }
} catch {
    Write-Host "    Luu y: Khong the cai may in ao tu dong (can quyen Admin). Ban co the cai thu cong sau." -ForegroundColor DarkYellow
    Write-Host "    Loi: $($_.Exception.Message)" -ForegroundColor DarkGray
}

Write-Host "`n=== CAI DAT THANH CONG! ===" -ForegroundColor Cyan
Write-Host "Ban co the dong cua so nay, nhap dup vao bieu tuong tren Desktop de chay,"
Write-Host "hoac chon nhieu tep PDF, nhap chuot phai va chon 'Ghep PDF bang PDF HPhat'!" -ForegroundColor Yellow
Write-Host "De luu PDF tu app khac: khi in, chon may in '$printerName'." -ForegroundColor Cyan

Read-Host "`nNhan Enter de hoan tat..."
