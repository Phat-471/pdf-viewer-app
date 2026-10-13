# package_project.ps1
# Automates the build and zipping of PDF Pro using VERSION.txt as the release source of truth

param(
    [switch]$SkipPublish,
    [switch]$SkipLocalDeploy,
    [string]$InstallerCompiler = $env:PDFPRO_ISCC_PATH,
    [string]$DownloadBaseUrl = ""
)

$ErrorActionPreference = "Stop"

function Invoke-PackageCommand {
    param([string]$Command, [string[]]$Arguments)
    $savedPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & $Command @Arguments 2>&1 | ForEach-Object { Write-Host $_ }
        $commandExitCode = $LASTEXITCODE
    } finally { $ErrorActionPreference = $savedPreference }
    if ($commandExitCode -ne 0) { throw "$Command failed with exit code $commandExitCode." }
}
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

# Resolve the compiler before doing any expensive build work.
if (-not $InstallerCompiler) {
    $candidates = @(
        "${env:ProgramFiles}\Inno Setup 7\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    )
    $InstallerCompiler = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $InstallerCompiler) {
        $command = Get-Command iscc -ErrorAction SilentlyContinue
        if ($command) { $InstallerCompiler = $command.Source }
    }
}
if (-not $InstallerCompiler -or -not (Test-Path -LiteralPath $InstallerCompiler -PathType Leaf)) {
    throw 'Inno Setup compiler is required. Install Inno Setup or set PDFPRO_ISCC_PATH; no release will be created without Setup.exe.'
}

Write-Host "=== PDF HPhat: Packaging Version $version ===" -ForegroundColor Cyan

# 1. Compile Rust core
Write-Host "`n[1/4] Compiling Rust core (src/PdfCore)..." -ForegroundColor Yellow
cd src/PdfCore
Invoke-PackageCommand -Command cargo -Arguments @('build', '--release')
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
Invoke-PackageCommand -Command dotnet -Arguments @('restore', 'PdfViewerApp.csproj', '-r', 'win-x64')
Invoke-PackageCommand -Command dotnet -Arguments @('clean', 'PdfViewerApp.csproj', '-c', 'Release', '-r', 'win-x64')
# Remove only this project's resolved publish directory to avoid stale dependencies.
$publishOut = [IO.Path]::GetFullPath((Join-Path $scriptDir 'src/PdfViewerApp/bin/Release/net8.0-windows10.0.26100.0/win-x64/publish'))
$expectedParent = [IO.Path]::GetFullPath((Join-Path $scriptDir 'src/PdfViewerApp/bin/Release')) + [IO.Path]::DirectorySeparatorChar
if (-not $publishOut.StartsWith($expectedParent, [StringComparison]::OrdinalIgnoreCase)) { throw 'Publish path is outside the project build directory.' }
if (Test-Path -LiteralPath $publishOut) { Remove-Item -LiteralPath $publishOut -Recurse -Force }
Invoke-PackageCommand -Command dotnet -Arguments @('publish', 'PdfViewerApp.csproj', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '/p:PublishSingleFile=true', '/p:PublishReadyToRun=false')
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

# Both installer and ZIP are required. Never announce a partially built release.
$installerFile = "PDFPro_Setup_v$safeVersion.exe"
$installerPath = Join-Path $releaseDir $installerFile
if (Test-Path -LiteralPath $installerPath) { Remove-Item -LiteralPath $installerPath -Force }
Write-Host "Building required Inno Setup installer..." -ForegroundColor Yellow
Invoke-PackageCommand -Command $InstallerCompiler -Arguments @("/dMyAppVersion=$version", (Join-Path $scriptDir 'installer.iss'))
foreach ($artifact in @((Join-Path $publishOut 'PdfViewerApp.exe'), $installerPath)) {
    $info = [Diagnostics.FileVersionInfo]::GetVersionInfo($artifact)
    $binaryVersion = '{0}.{1}.{2}.{3}' -f $info.FileMajorPart, $info.FileMinorPart, $info.FileBuildPart, $info.FilePrivatePart
    if ([Version]$binaryVersion -ne [Version]"$version.0") { throw "Artifact version mismatch: $artifact ($binaryVersion)." }
}
Set-Location $scriptDir
& (Join-Path $scriptDir 'tools/write_release_metadata.ps1') -DownloadBaseUrl $DownloadBaseUrl
& (Join-Path $scriptDir 'tools/verify_update_manifest.ps1')
Write-Host "Packaging completed: installer, update ZIP, manifest and SHA256SUMS.txt." -ForegroundColor Green
# Packaging never publishes metadata or replaces a running local installation.
# SkipPublish/SkipLocalDeploy are retained for compatibility with existing callers.
Write-Host "Publish with tools/release.ps1; GitHub Actions verifies all assets before notifying WordPress."
