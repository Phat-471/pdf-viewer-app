# Install the prebuilt ZIP for the current Windows user. No SDK or elevation required.
param([switch]$RegisterVirtualPrinter)
$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceDir = $scriptDir
if (-not (Test-Path -LiteralPath (Join-Path $sourceDir 'PdfViewerApp.exe'))) {
    $sourceDir = Join-Path $scriptDir 'src/PdfViewerApp/bin/Release/net8.0-windows10.0.26100.0/win-x64/publish'
}
foreach ($name in @('PdfViewerApp.exe', 'pdf_core.dll', 'pdfium.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourceDir $name) -PathType Leaf)) { throw "Incomplete package: $name. Build with package_project.ps1 or download the complete release ZIP." }
}
$canonicalDir = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'PDF Pro'))
$legacyDir = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'PDFPro'))
$installDir = if ((Test-Path -LiteralPath $legacyDir) -and -not (Test-Path -LiteralPath $canonicalDir)) { $legacyDir } else { $canonicalDir }
if ($installDir -notin @($canonicalDir, $legacyDir)) { throw 'Invalid installation directory.' }
$sourceDir = [IO.Path]::GetFullPath($sourceDir)
if ($sourceDir.TrimEnd('\') -eq $installDir.TrimEnd('\')) { throw 'This package is already in the installation directory. Run the application or use Setup.exe.' }
$exePath = Join-Path $installDir 'PdfViewerApp.exe'
$files = @(Get-ChildItem -LiteralPath $sourceDir -File -Recurse | Where-Object { $_.Name -notlike '*.pdb' })
$sourcePrefix = $sourceDir.TrimEnd('\') + '\'
$installPrefix = $installDir.TrimEnd('\') + '\'
# Validate every destination before stopping this installation or replacing files.
$payload = foreach ($file in $files) {
    $relative = $file.FullName.Substring($sourcePrefix.Length)
    $dest = [IO.Path]::GetFullPath((Join-Path $installDir $relative))
    if (-not $dest.StartsWith($installPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Package path is outside the installation directory.' }
    [PSCustomObject]@{ Source = $file.FullName; Relative = $relative; Destination = $dest }
}
Get-Process -Name PdfViewerApp -ErrorAction SilentlyContinue | Where-Object {
    $_.Path -and [IO.Path]::GetFullPath($_.Path) -eq $exePath
} | Stop-Process -Force
$backupDir = [IO.Path]::GetFullPath((Join-Path $env:TEMP ('pdfpro-install-backup-' + [Guid]::NewGuid().ToString('N'))))
$tempPrefix = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
if (-not $backupDir.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Backup directory is outside TEMP.' }
$replaced = [Collections.Generic.List[object]]::new()
try {
    foreach ($file in $payload) {
        $backupPath = Join-Path $backupDir $file.Relative
        $existed = Test-Path -LiteralPath $file.Destination
        if ($existed) {
            New-Item -ItemType Directory -Path (Split-Path -Parent $backupPath) -Force | Out-Null
            Copy-Item -LiteralPath $file.Destination -Destination $backupPath -Force
        }
        $replaced.Add([PSCustomObject]@{ Destination = $file.Destination; Backup = $backupPath; Existed = $existed })
        New-Item -ItemType Directory -Path (Split-Path -Parent $file.Destination) -Force | Out-Null
        Copy-Item -LiteralPath $file.Source -Destination $file.Destination -Force
    }
} catch {
    foreach ($file in $replaced) {
        if ($file.Existed) { Copy-Item -LiteralPath $file.Backup -Destination $file.Destination -Force }
        elseif (Test-Path -LiteralPath $file.Destination) { Remove-Item -LiteralPath $file.Destination -Force }
    }
    throw
}
if (Test-Path -LiteralPath $backupDir) { Remove-Item -LiteralPath $backupDir -Recurse -Force }
ConvertTo-Json -InputObject @($payload | ForEach-Object { $_.Relative }) | Set-Content -LiteralPath (Join-Path $installDir 'installed-files.json') -Encoding UTF8
Write-Host "Installed the complete package to $installDir" -ForegroundColor Green

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

$supportedTypes = 'HKCU:\Software\Classes\Applications\PdfViewerApp.exe\SupportedTypes'
New-Item -Path $supportedTypes -Force | Out-Null
New-ItemProperty -Path $supportedTypes -Name '.pdf' -Value '' -PropertyType String -Force | Out-Null
$openWithProgIdsKey = 'HKCU:\Software\Classes\.pdf\OpenWithProgids'
New-Item -Path $openWithProgIdsKey -Force | Out-Null
New-ItemProperty -Path $openWithProgIdsKey -Name 'PDFPro.PDF' -Value ([byte[]]@()) -PropertyType Binary -Force | Out-Null

$progIdKey = "HKCU:\Software\Classes\PDFPro.PDF"
New-Item -Path "$progIdKey\shell\open\command" -Force | Out-Null
New-ItemProperty -Path $progIdKey -Name "(default)" -Value "PDF Pro - HPhat Edition" -PropertyType String -Force | Out-Null
New-ItemProperty -Path $progIdKey -Name "FriendlyTypeName" -Value "PDF Pro - HPhat Edition" -PropertyType String -Force | Out-Null
Set-ItemProperty -Path "$progIdKey\shell\open\command" -Name "(default)" -Value "`"$exePath`" `"%1`""

Write-Host "    Hiep hoi tep tin va Menu chuot phai hoan tat!" -ForegroundColor Green

$appPaths = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\PdfViewerApp.exe'
New-Item -Path $appPaths -Force | Out-Null
Set-ItemProperty -LiteralPath $appPaths -Name '(default)' -Value $exePath
New-ItemProperty -LiteralPath $appPaths -Name 'Path' -Value $installDir -PropertyType String -Force | Out-Null
$capabilities = 'HKCU:\Software\PDFPro\Capabilities'
New-Item -Path "$capabilities\FileAssociations" -Force | Out-Null
New-ItemProperty -LiteralPath $capabilities -Name 'ApplicationName' -Value 'PDF Pro' -PropertyType String -Force | Out-Null
New-ItemProperty -LiteralPath "$capabilities\FileAssociations" -Name '.pdf' -Value 'PDFPro.PDF' -PropertyType String -Force | Out-Null
New-Item -Path 'HKCU:\Software\RegisteredApplications' -Force | Out-Null
New-ItemProperty -LiteralPath 'HKCU:\Software\RegisteredApplications' -Name 'PDF Pro' -Value 'Software\PDFPro\Capabilities' -PropertyType String -Force | Out-Null

if ($RegisterVirtualPrinter) {
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) { Write-Warning 'Virtual printer registration requires an administrator. Application installation is complete.' }
    else {
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

    }
}
Write-Host 'PDF Pro installation completed.' -ForegroundColor Green
