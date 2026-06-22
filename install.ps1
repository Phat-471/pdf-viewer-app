# install.ps1
# PDF Pro - Automated Installer for Desktop Shortcut & Explorer Context Menu
$ErrorActionPreference = "Stop"

# 1. Require Administrator Elevation
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Dang yeu cau quyen Administrator de cai dat..." -ForegroundColor Yellow
    try {
        Start-Process powershell -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs -Wait
    } catch {
        Write-Error "Yeu cau quyen Administrator bi tu choi!"
    }
    exit
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

Write-Host "=== Bat dau qua trinh cai dat PDF Pro - HPhat Edition ===" -ForegroundColor Cyan

# 2. Terminate any running PdfViewerApp process
Write-Host "`n[1/7] Dang dong cac ung dung dang chay de tranh khoa tap tin..." -ForegroundColor Yellow
Stop-Process -Name PdfViewerApp -Force -ErrorAction SilentlyContinue
Get-Process | Where-Object {$_.Path -like "*PdfViewerApp*"} | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

# 3. Clean previous builds and caches
Write-Host "`n[2/7] Dang xoa cac ban build cu va lam sach cache..." -ForegroundColor Yellow
$wpfProjectDir = Join-Path $scriptDir "src\PdfViewerApp"
$rustProjectDir = Join-Path $scriptDir "src\PdfCore"

# Shutdown MSBuild server to release any lock
dotnet build-server shutdown | Out-Null

if (Test-Path "src\PdfViewerApp\bin") { Remove-Item -Path "src\PdfViewerApp\bin" -Recurse -Force -ErrorAction SilentlyContinue }
if (Test-Path "src\PdfViewerApp\obj") { Remove-Item -Path "src\PdfViewerApp\obj" -Recurse -Force -ErrorAction SilentlyContinue }
if (Test-Path "src\PdfCore\target") { Remove-Item -Path "src\PdfCore\target" -Recurse -Force -ErrorAction SilentlyContinue }

try {
    dotnet nuget locals all --clear | Out-Null
    Write-Host "    Da lam sach cache NuGet thanh cong!" -ForegroundColor Green
} catch {
    Write-Host "    Khong the lam sach cache NuGet, tiep tuc..." -ForegroundColor DarkYellow
}

# 4. Compile and Publish Project
Write-Host "`n[3/7] Bien dich va xuat ban tu ma nguon..." -ForegroundColor Yellow
$installDir = Join-Path $env:LOCALAPPDATA "PDFPro"
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

if (Test-Path $wpfProjectDir) {
    # Compile Rust core first
    if (Test-Path $rustProjectDir) {
        Write-Host "    Dang bien dich Rust Core (PdfCore)..." -ForegroundColor Yellow
        Set-Location $rustProjectDir
        & cargo build --release
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Bien dich Rust core that bai!"
            exit 1
        }
        Set-Location $scriptDir
    }

    # Publish C# WPF
    Write-Host "    Dang xuat ban C# WPF Application..." -ForegroundColor Yellow
    Set-Location $wpfProjectDir
    & dotnet publish -c Release -r win-x64 --self-contained true
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Xuat ban WPF that bai!"
        exit 1
    }
    Set-Location $scriptDir

    # Copy native dependencies
    $publishDir = Join-Path $scriptDir "src\PdfViewerApp\bin\Release\net8.0-windows10.0.26100.0\win-x64\publish"
    
    if (Test-Path "libs\pdfium.dll") {
        Copy-Item "libs\pdfium.dll" -Destination "$publishDir\pdfium.dll" -Force
    }
    
    $rustDll = Join-Path $rustProjectDir "target\release\pdf_core.dll"
    if (Test-Path $rustDll) {
        Copy-Item $rustDll -Destination "$publishDir\pdf_core.dll" -Force
    } elseif (Test-Path "libs\pdf_core.dll") {
        Copy-Item "libs\pdf_core.dll" -Destination "$publishDir\pdf_core.dll" -Force
    }

    # Clear target install directory first to avoid old files, but preserve configuration and license (*.json) files
    if (Test-Path $installDir) {
        Get-ChildItem -Path $installDir -Exclude "*.json" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    }
    Copy-Item -Path "$publishDir\*" -Destination $installDir -Recurse -Force
} else {
    Write-Host "`n[3/7] Che do cai dat nhanh tu file dung san..." -ForegroundColor Yellow
    if (Test-Path $installDir) {
        Get-ChildItem -Path $installDir -Exclude "*.json" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    }
    Copy-Item -Path "$scriptDir\*" -Destination $installDir -Exclude "install.ps1", "install.bat", "uninstall.ps1", "uninstall.bat", "*.zip", ".git" -Recurse -Force
}

Write-Host "    Da thiet lap thu muc ung dung tai: $installDir" -ForegroundColor Green

# 5. Create Desktop Shortcut
Write-Host "`n[5/7] Tao phim tat tren Desktop (Shortcut)..." -ForegroundColor Yellow
$exePath = Join-Path $installDir "PdfViewerApp.exe"
$desktopPath = [System.IO.Path]::Combine([System.Environment]::GetFolderPath('Desktop'), "PDF Pro - HPhat Edition.lnk")

$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut($desktopPath)
$Shortcut.TargetPath = $exePath
$Shortcut.WorkingDirectory = $installDir
$Shortcut.IconLocation = "$exePath,0"
$Shortcut.Description = "PDF Pro - HPhat Edition"
$Shortcut.Save()
Write-Host "    Da tao phim tat tren Desktop!" -ForegroundColor Green

# 6. Install Registry Context Menu & File Association
Write-Host "`n[6/7] Dang ky Menu chuot phai & Hiep hoi tep tin..." -ForegroundColor Yellow

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

$appRegKey = "HKCU:\Software\Classes\Applications\PdfViewerApp.exe\shell\open\command"
New-Item -Path $appRegKey -Force | Out-Null
Set-ItemProperty -Path $appRegKey -Name "(default)" -Value "`"$exePath`" `"%1`""

$appRootKey = "HKCU:\Software\Classes\Applications\PdfViewerApp.exe"
New-ItemProperty -Path $appRootKey -Name "FriendlyAppName" -Value "PDF Pro - HPhat Edition" -PropertyType String -Force | Out-Null
New-ItemProperty -Path $appRootKey -Name "FriendlyTypeName" -Value "PDF Pro - HPhat Edition" -PropertyType String -Force | Out-Null

$appShellKey = "HKCU:\Software\Classes\Applications\PdfViewerApp.exe\shell\open"
New-ItemProperty -Path $appShellKey -Name "FriendlyAppName" -Value "PDF Pro - HPhat Edition" -PropertyType String -Force | Out-Null

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

$openWithProgIdsKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.pdf\OpenWithProgids"
if (-not (Test-Path $openWithProgIdsKey)) {
    New-Item -Path $openWithProgIdsKey -Force | Out-Null
}
New-ItemProperty -Path $openWithProgIdsKey -Name "PdfViewerApp.Document" -Value ([byte[]]@()) -PropertyType Binary -Force | Out-Null

$progIdKey = "HKCU:\Software\Classes\PdfViewerApp.Document"
New-Item -Path "$progIdKey\shell\open\command" -Force | Out-Null
New-ItemProperty -Path $progIdKey -Name "(default)" -Value "PDF Pro - HPhat Edition" -PropertyType String -Force | Out-Null
New-ItemProperty -Path $progIdKey -Name "FriendlyTypeName" -Value "PDF Pro - HPhat Edition" -PropertyType String -Force | Out-Null
Set-ItemProperty -Path "$progIdKey\shell\open\command" -Name "(default)" -Value "`"$exePath`" `"%1`""

Write-Host "    Hiep hoi tep tin va Menu chuot phai hoan tat!" -ForegroundColor Green

# 7. Register Virtual PDF Printer
Write-Host "`n[7/7] Dang ky may in ao 'PDF Pro - HPhat Edition'..." -ForegroundColor Yellow
$printerName = "PDF Pro - HPhat Edition"
$portName    = "PORTPROMPT:"
$driverName  = "Microsoft Print To PDF"

try {
    if (Get-Printer -Name $printerName -ErrorAction SilentlyContinue) {
        Remove-Printer -Name $printerName -ErrorAction SilentlyContinue
    }

    $driver = Get-PrinterDriver -Name $driverName -ErrorAction SilentlyContinue
    if ($driver -eq $null) {
        Write-Host "    Khong tim thay driver '$driverName'. Bo qua may in ao." -ForegroundColor DarkYellow
    } else {
        Add-Printer -Name $printerName -DriverName $driverName -PortName $portName -ErrorAction Stop
        Write-Host "    Da cai dat may in ao: $printerName" -ForegroundColor Green

        $printRegKey = "HKCU:\Software\PDFPro\VirtualPrinter"
        New-Item -Path $printRegKey -Force | Out-Null
        New-ItemProperty -Path $printRegKey -Name "PrinterName" -Value $printerName -PropertyType String -Force | Out-Null
        New-ItemProperty -Path $printRegKey -Name "AppPath" -Value $exePath -PropertyType String -Force | Out-Null
        New-ItemProperty -Path $printRegKey -Name "AutoOpen" -Value 1 -PropertyType DWord -Force | Out-Null
    }
} catch {
    Write-Host "    Loi khi cai dat may in ao: $($_.Exception.Message)" -ForegroundColor DarkYellow
}

Write-Host "`n=== CAI DAT HOAN TAT VA THANH CONG! ===" -ForegroundColor Cyan
Write-Host "Ban co the chay ung dung tu shortcut tren Desktop" -ForegroundColor Green
# Read-Host "`nNhan Enter de thoat..."
