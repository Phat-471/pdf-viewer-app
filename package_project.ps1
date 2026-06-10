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
dotnet clean -c Release -r win-x64
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=false
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

Write-Host "`n=== Packaging completed successfully! ===" -ForegroundColor Green
Write-Host "Zip package created at: $zipPath" -ForegroundColor White
Write-Host "Manifest created at: $manifestPath" -ForegroundColor White
Write-Host "Summary created at: $packageSummaryPath" -ForegroundColor White
Write-Host "SHA256: $sha256" -ForegroundColor White
