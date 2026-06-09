param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$coreDir = Join-Path $repoRoot "src\PdfCore"
$appProject = Join-Path $repoRoot "src\PdfViewerApp\PdfViewerApp.csproj"
$libsDir = Join-Path $repoRoot "libs"
$versionFile = Join-Path $repoRoot "VERSION.txt"
$version = if (Test-Path $versionFile) { (Get-Content -LiteralPath $versionFile -Raw).Trim() } else { "unknown" }

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "dist\PdfPro-$version-$Runtime"
}

Write-Host "Building Rust core for version $version..."
Push-Location $coreDir
try {
    cargo build --release
}
finally {
    Pop-Location
}

New-Item -ItemType Directory -Force -Path $libsDir | Out-Null
Copy-Item -LiteralPath (Join-Path $coreDir "target\release\pdf_core.dll") -Destination (Join-Path $libsDir "pdf_core.dll") -Force

if (-not (Test-Path (Join-Path $libsDir "pdfium.dll"))) {
    throw "Missing libs\pdfium.dll. Put pdfium.dll in the libs folder before publishing."
}

if (Test-Path $OutputDir) {
    Remove-Item -LiteralPath $OutputDir -Recurse -Force
}

Write-Host "Publishing WPF app as single exe..."
dotnet publish $appProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $OutputDir

$exePath = Join-Path $OutputDir "PdfViewerApp.exe"
if (-not (Test-Path $exePath)) {
    throw "Publish failed: PdfViewerApp.exe was not created."
}

$sizeMb = [Math]::Round((Get-Item $exePath).Length / 1MB, 2)
Write-Host ""
Write-Host "Single exe ready:"
Write-Host $exePath
Write-Host "Size: $sizeMb MB"
