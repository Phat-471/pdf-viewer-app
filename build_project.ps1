# build_project.ps1
# Automated build script for PDF Pro: compiles Rust core and C# WPF app

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

$versionFile = Join-Path $scriptDir "VERSION.txt"
$version = if (Test-Path $versionFile) { (Get-Content -LiteralPath $versionFile -Raw).Trim() } else { "unknown" }

$requiredPaths = @(
    "src\PdfCore",
    "src\PdfCore\Cargo.toml",
    "src\PdfViewerApp",
    "src\PdfViewerApp\PdfViewerApp.csproj"
)

foreach ($relativePath in $requiredPaths) {
    $fullPath = Join-Path $scriptDir $relativePath
    if (-not (Test-Path -LiteralPath $fullPath)) {
        throw "Missing required source path: $fullPath. Restore the app source before building."
    }
}

Write-Host "=== PDF HPhat - Biﾃｪn d盻議h t盻ｱ ﾄ黛ｻ冢g WPF & Rust Core ===" -ForegroundColor Cyan

# 1. Compile Rust Core dynamic library
Write-Host "`n[1/3] Biﾃｪn d盻議h lﾃｵi Rust (PdfCore)..." -ForegroundColor Yellow
$rustDir = Join-Path $scriptDir "src\PdfCore"
Set-Location $rustDir

# Execute cargo build in release mode
& cargo build --release
if ($LASTEXITCODE -ne 0) {
    Write-Error "Biﾃｪn d盻議h Rust th蘯･t b蘯｡i!"
    exit 1
}
Write-Host "    Lﾃｵi Rust compiled thﾃnh cﾃｴng!" -ForegroundColor Green

# 2. Copy compiled dll to WPF output folder
Write-Host "`n[2/3] C蘯･u hﾃｬnh vﾃ liﾃｪn k蘯ｿt DLL..." -ForegroundColor Yellow
$dllSrc = Join-Path $rustDir "target\release\pdf_core.dll"
$libsDir = Join-Path $scriptDir "libs"
$wpfDir = Join-Path $scriptDir "src\PdfViewerApp"

if (-not (Test-Path $libsDir)) {
    New-Item -ItemType Directory -Path $libsDir -Force
}

# Copy to libs folder
Copy-Item $dllSrc -Destination (Join-Path $libsDir "pdf_core.dll") -Force
Write-Host "    ﾄ静｣ sao chﾃｩp pdf_core.dll vﾃo thﾆｰ m盻･c /libs/" -ForegroundColor Green

# Copy directly to WPF bin folder (we will place it in output)
$wpfBin = Join-Path $wpfDir "bin\Release\net8.0-windows"
if (-not (Test-Path $wpfBin)) {
    New-Item -ItemType Directory -Path $wpfBin -Force
}
Copy-Item $dllSrc -Destination (Join-Path $wpfBin "pdf_core.dll") -Force
Write-Host "    ﾄ静｣ sao chﾃｩp pdf_core.dll vﾃo thﾆｰ m盻･c ﾄ黛ｺｧu ra c盻ｧa C# WPF!" -ForegroundColor Green

# 3. Build WPF Application
Write-Host "`n[3/3] Biﾃｪn d盻議h 盻ｩng d盻･ng giao di盻㌻ WPF (C#)..." -ForegroundColor Yellow
Set-Location $wpfDir
& dotnet build PdfViewerApp.csproj -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "Biên dịch WPF thất bại!"
    exit 2
}
Write-Host "    Biﾃｪn d盻議h WPF hoﾃn t蘯･t thﾃnh cﾃｴng!" -ForegroundColor Green

Write-Host "`n=== Hoﾃn t蘯･t biﾃｪn d盻議h toﾃn b盻・h盻・th盻創g! ===" -ForegroundColor Cyan
Write-Host "V盻・trﾃｭ ch蘯｡y: (Join-Path $wpfBin 'PdfViewerApp.exe')" -ForegroundColor White
