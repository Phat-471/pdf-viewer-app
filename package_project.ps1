# package_project.ps1
# Automates the build and zipping of PDF Pro using VERSION.txt as the release source of truth

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

$versionFile = Join-Path $scriptDir "VERSION.txt"
if (-not (Test-Path $versionFile)) {
    throw "Missing VERSION.txt at $versionFile"
}

$version = (Get-Content -LiteralPath $versionFile -Raw).Trim()
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "VERSION.txt is empty."
}

function Sync-AssemblyInfoVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [Parameter(Mandatory = $true)]
        [string]$VersionText
    )

    $assemblyInfoPath = Join-Path $RepoRoot "src\PdfViewerApp\Properties\AssemblyInfo.cs"
    if (-not (Test-Path -LiteralPath $assemblyInfoPath)) {
        return
    }

    $assemblyVersion = if ($VersionText -match '^(?<core>\d+\.\d+\.\d+)') { $Matches.core } else { $VersionText }
    $assemblyInfo = Get-Content -LiteralPath $assemblyInfoPath -Raw
    $assemblyInfo = $assemblyInfo -replace 'AssemblyFileVersion\(".*?"\)', "AssemblyFileVersion(`"$assemblyVersion.0`")"
    $assemblyInfo = $assemblyInfo -replace 'AssemblyInformationalVersion\(".*?"\)', "AssemblyInformationalVersion(`"$assemblyVersion`")"
    $assemblyInfo = $assemblyInfo -replace 'AssemblyVersion\(".*?"\)', "AssemblyVersion(`"$assemblyVersion.0`")"
    Set-Content -LiteralPath $assemblyInfoPath -Value $assemblyInfo -NoNewline
}

Sync-AssemblyInfoVersion -RepoRoot $scriptDir -VersionText $version

$safeVersion = $version -replace '[^0-9A-Za-z\.\-_]', '_'
$releaseDir = Join-Path $scriptDir "releases"

$requiredPaths = @(
    "src\PdfCore",
    "src\PdfCore\Cargo.toml",
    "src\PdfViewerApp",
    "src\PdfViewerApp\PdfViewerApp.csproj",
    "libs\pdfium.dll"
)

foreach ($relativePath in $requiredPaths) {
    $fullPath = Join-Path $scriptDir $relativePath
    if (-not (Test-Path -LiteralPath $fullPath)) {
        throw "Missing required build path: $fullPath. Restore the app source and required DLLs before packaging."
    }
}

Write-Host "=== PDF HPhat: Packaging Version $version ===" -ForegroundColor Cyan

# 1. Compile Rust core
Write-Host "`n[1/4] Compiling Rust core (src/PdfCore)..." -ForegroundColor Yellow
cd src/PdfCore
cargo build --release
if ($LASTEXITCODE -ne 0) {
    Write-Error "Rust core compilation failed!"
    exit 1
}
cd $scriptDir

# 2. Setup libs and copy DLLs
Write-Host "`n[2/4] Syncing DLLs..." -ForegroundColor Yellow
if (-not (Test-Path "libs")) {
    New-Item -ItemType Directory -Path "libs" -Force
}

Copy-Item "src/PdfCore/target/release/pdf_core.dll" -Destination "libs/pdf_core.dll" -Force

# 3. Publish C# WPF App (self-contained, win-x64)
Write-Host "`n[3/4] Publishing WPF application..." -ForegroundColor Yellow
cd src/PdfViewerApp
dotnet clean PdfViewerApp.csproj -c Release -r win-x64
dotnet restore PdfViewerApp.csproj -r win-x64
dotnet publish PdfViewerApp.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=false
if ($LASTEXITCODE -ne 0) {
    Write-Error "WPF application publish failed!"
    exit 2
}

# The publish output path
$publishOut = Join-Path $scriptDir "src/PdfViewerApp/bin/Release/net8.0-windows10.0.26100.0/win-x64/publish"

# Copy pdf_core.dll and pdfium.dll directly to publish output folder to ensure they package together
Copy-Item "$scriptDir/libs/pdf_core.dll" -Destination "$publishOut/pdf_core.dll" -Force
Copy-Item "$scriptDir/libs/pdfium.dll" -Destination "$publishOut/pdfium.dll" -Force

function Copy-RuntimeDllIfPresent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourcePath,
        [Parameter(Mandatory = $true)]
        [string]$DestinationDirectory
    )

    if (Test-Path -LiteralPath $SourcePath) {
        Copy-Item -LiteralPath $SourcePath -Destination $DestinationDirectory -Force
        Write-Host "Copied runtime DLL: $(Split-Path -Leaf $SourcePath)" -ForegroundColor DarkGray
    }
}

# Ensure native C/C++ runtime dependencies travel with the ZIP so clean machines can launch the app.
$nativeRuntimeSources = @(
    Join-Path $env:WINDIR "System32\VCRUNTIME140.dll"
    Join-Path $env:WINDIR "System32\VCRUNTIME140_1.dll"
    Join-Path $env:WINDIR "System32\MSVCP140.dll"
    Join-Path $env:WINDIR "System32\CONCRT140.dll"
)

foreach ($runtimePath in $nativeRuntimeSources) {
    Copy-RuntimeDllIfPresent -SourcePath $runtimePath -DestinationDirectory $publishOut
}

# 4. Packaging into ZIP
Write-Host "`n[4/4] Creating zip archive..." -ForegroundColor Yellow
if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
}

$zipFileName = "PdfViewerApp_v$safeVersion.zip"
$zipPath = Join-Path $releaseDir $zipFileName
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

# Copy install and uninstall scripts into the publish folder so they are included in the zip
Copy-Item -LiteralPath "$scriptDir/install.bat" -Destination "$publishOut/install.bat" -Force
Copy-Item -LiteralPath "$scriptDir/install.ps1" -Destination "$publishOut/install.ps1" -Force
Copy-Item -LiteralPath "$scriptDir/uninstall.bat" -Destination "$publishOut/uninstall.bat" -Force
Copy-Item -LiteralPath "$scriptDir/uninstall.ps1" -Destination "$publishOut/uninstall.ps1" -Force

# Compress the publish folder contents to ZIP
Compress-Archive -Path "$publishOut/*" -DestinationPath $zipPath -Force

$zipItem = Get-Item -LiteralPath $zipPath
$sha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$releaseDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
$manifestPath = Join-Path $releaseDir "update-manifest.json"

$changelogText = ""
$changelogFile = Join-Path $scriptDir "CHANGELOG.txt"
if (Test-Path $changelogFile) {
    $changelogText = (Get-Content -LiteralPath $changelogFile -Encoding UTF8 -Raw).Trim()
}

$manifest = [ordered]@{
    version = $version
    file = $zipFileName
    sha256 = $sha256
    size = $zipItem.Length
    release_date = $releaseDate
    download_url = ""
    mandatory = $false
    changelog = $changelogText
}

$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

$packageSummaryPath = Join-Path $releaseDir "update-package-summary.md"
$manifestJson = $manifest | ConvertTo-Json -Depth 4
$summaryTemplate = @'
# Thong tin ban dong goi moi (v__VERSION__) de cap nhat len Host

- Tep ZIP dong goi: `releases/__ZIP_FILE__`
- Dung luong thuc te (Size): `__SIZE__` bytes
- Ma bam SHA256: `__SHA256__`
- Tep Manifest cap nhat: `releases/update-manifest.json`

Ban su dung noi dung cau hinh JSON duoi day de dua len server cap nhat nhe:

```json
__MANIFEST_JSON__
```
'@

$summary = $summaryTemplate.
    Replace('__VERSION__', $version).
    Replace('__ZIP_FILE__', $zipFileName).
    Replace('__SIZE__', $zipItem.Length.ToString()).
    Replace('__SHA256__', $sha256).
    Replace('__MANIFEST_JSON__', $manifestJson)

$summary | Set-Content -LiteralPath $packageSummaryPath -Encoding UTF8

# 4.5. Optional Auto-Publish to WordPress REST API
$publishSettingsFile = Join-Path $scriptDir "publish_settings.json"
if (Test-Path $publishSettingsFile) {
    Write-Host "`n[Auto-Publish] Found publish_settings.json. Proceeding to sync with WordPress API..." -ForegroundColor Yellow
    $pubSettings = Get-Content -LiteralPath $publishSettingsFile | ConvertFrom-Json
    $token = $pubSettings.publish_token
    $apiDomain = $pubSettings.api_domain
    $downloadUrlPrefix = $pubSettings.download_url_prefix

    if ([string]::IsNullOrWhiteSpace($token)) {
        Write-Warning "[Auto-Publish] publish_token is empty. Skipping auto-publish."
    } else {
        $downloadUrl = "$downloadUrlPrefix$zipFileName"
        
        # Update manifest object in memory and re-save
        $manifest.download_url = $downloadUrl
        $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

        $body = @{
            token = $token
            latest_version = $version
            download_url = $downloadUrl
            sha256 = $sha256
            file_size = $zipItem.Length
            release_date = $releaseDate
            mandatory = $false
            changelog = $changelogText
        } | ConvertTo-Json

        try {
            $publishUrl = "$apiDomain/wp-json/pdfpro/v1/update-publish"
            Write-Host "[Auto-Publish] Sending update metadata to $publishUrl..." -ForegroundColor DarkGray
            $response = Invoke-RestMethod -Uri $publishUrl -Method Post -ContentType "application/json" -Body $body
            if ($response.success) {
                Write-Host "[Auto-Publish] SUCCESS: $($response.message)" -ForegroundColor Green
            } else {
                Write-Warning "[Auto-Publish] Failed to publish update: $($response.message)"
            }
        } catch {
            Write-Warning "[Auto-Publish] API Request failed: $_"
        }
    }
} else {
    Write-Host "`n[Auto-Publish] publish_settings.json not found. Copy publish_settings.json.template to publish_settings.json and fill in your token to enable auto-publishing." -ForegroundColor DarkYellow
}

# 5. Optional: Build Inno Setup Installer (Setup.exe)
Write-Host "`n[5/5] Building Inno Setup Installer (Optional)..." -ForegroundColor Yellow
$isccPath = ""
if (Test-Path "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") {
    $isccPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
} elseif (Get-Command iscc -ErrorAction SilentlyContinue) {
    $isccPath = (Get-Command iscc).Source
}

if ($isccPath -ne "") {
    Write-Host "    Found Inno Setup Compiler: $isccPath" -ForegroundColor DarkGray
    Write-Host "    Running Inno Setup compiler..." -ForegroundColor Yellow
    $absoluteIssPath = Join-Path $scriptDir "installer.iss"
    & $isccPath "/dMyAppVersion=$version" $absoluteIssPath
    if ($LASTEXITCODE -eq 0) {
        Write-Host "    Inno Setup package created successfully!" -ForegroundColor Green
    } else {
        Write-Warning "Inno Setup compilation failed (Optional step). Resetting exit code to avoid failing the build."
        $global:LASTEXITCODE = 0
    }
} else {
    Write-Host "    Inno Setup compiler (ISCC.exe) not found. Skipping Setup.exe creation." -ForegroundColor DarkYellow
}

Write-Host "`n=== Packaging completed successfully! ===" -ForegroundColor Green
Write-Host "Zip package created at: $zipPath" -ForegroundColor White
Write-Host "Manifest created at: $manifestPath" -ForegroundColor White
Write-Host "Summary created at: $packageSummaryPath" -ForegroundColor White
Write-Host "SHA256: $sha256" -ForegroundColor White

# 6. Local Hot-Deploy: Automatically sync DLL and EXE to local install directory
# This ensures the developer's local installation always matches the freshly built output.
Write-Host "`n[6/6] Local Hot-Deploy..." -ForegroundColor Yellow

$localInstallDir = "$env:LOCALAPPDATA\PDF Pro"
$newDll = Join-Path $scriptDir "src\PdfCore\target\release\pdf_core.dll"
$newExe = Join-Path $publishOut "PdfViewerApp.exe"

if (Test-Path $localInstallDir) {
    # Stop app if running
    $proc = Get-Process -Name "PdfViewerApp" -ErrorAction SilentlyContinue
    if ($proc) {
        Write-Host "    Stopping running PdfViewerApp..." -ForegroundColor DarkGray
        Stop-Process -Name "PdfViewerApp" -Force
        Start-Sleep -Seconds 1
    }

    # Copy pdf_core.dll
    if (Test-Path $newDll) {
        Copy-Item -LiteralPath $newDll -Destination "$localInstallDir\pdf_core.dll" -Force
        Write-Host "    [OK] pdf_core.dll -> $localInstallDir" -ForegroundColor Green
    }

    # Copy PdfViewerApp.exe
    if (Test-Path $newExe) {
        Copy-Item -LiteralPath $newExe -Destination "$localInstallDir\PdfViewerApp.exe" -Force
        Write-Host "    [OK] PdfViewerApp.exe -> $localInstallDir" -ForegroundColor Green
    }

    Write-Host "    Local install synced to v$version" -ForegroundColor Green
} else {
    Write-Host "    Local install dir not found ($localInstallDir). Skipping hot-deploy." -ForegroundColor DarkYellow
    Write-Host "    Run the installer first: releases\PDFPro_Setup_v$safeVersion.exe" -ForegroundColor DarkYellow
}
