# build_project.ps1
# Automated build script for PDF Pro: compiles Rust core and C# WPF app

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

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

Write-Host "=== PDF Pro - Tu dong bien dich WPF and Rust Core ===" -ForegroundColor Cyan

# 1. Compile Rust Core dynamic library
Write-Host "`n[1/3] Bien dich Rust Core (PdfCore)..." -ForegroundColor Yellow
$rustDir = Join-Path $scriptDir "src\PdfCore"
Set-Location $rustDir

& cargo build --release
if ($LASTEXITCODE -ne 0) {
    Write-Error "Bien dich Rust core that bai!"
    exit 1
}
Write-Host "    Rust core bien dich thanh cong!" -ForegroundColor Green

# 2. Copy compiled dll to WPF output folder and libs
Write-Host "`n[2/3] Cau hinh va lien ket DLL..." -ForegroundColor Yellow
$dllSrc = Join-Path $rustDir "target\release\pdf_core.dll"
$libsDir = Join-Path $scriptDir "libs"
$wpfDir = Join-Path $scriptDir "src\PdfViewerApp"

if (-not (Test-Path $libsDir)) {
    New-Item -ItemType Directory -Path $libsDir -Force | Out-Null
}

Copy-Item $dllSrc -Destination (Join-Path $libsDir "pdf_core.dll") -Force
Write-Host "    Da sao chep pdf_core.dll vao thuc muc /libs/" -ForegroundColor Green

$wpfBin = Join-Path $wpfDir "bin\Release\net8.0-windows10.0.26100.0"
if (-not (Test-Path $wpfBin)) {
    New-Item -ItemType Directory -Path $wpfBin -Force | Out-Null
}
Copy-Item $dllSrc -Destination (Join-Path $wpfBin "pdf_core.dll") -Force
if (Test-Path (Join-Path $scriptDir "libs\pdfium.dll")) {
    Copy-Item (Join-Path $scriptDir "libs\pdfium.dll") -Destination (Join-Path $wpfBin "pdfium.dll") -Force
}
Write-Host "    Da sao chep pdf_core.dll va pdfium.dll vao thu muc dau ra cua C# WPF!" -ForegroundColor Green

# 3. Build WPF Application in Release mode
Write-Host "`n[3/3] Bien dich ung dung WPF (C#)..." -ForegroundColor Yellow
Set-Location $wpfDir
& dotnet build PdfViewerApp.csproj -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "Bien dich WPF that bai!"
    exit 2
}
Write-Host "    Bien dich WPF hoan tat!" -ForegroundColor Green

Write-Host "`n=== HOAN TAT BIEN DICH TOAN BO HE THONG! ===" -ForegroundColor Cyan
Write-Host "Duong dan chay: $wpfBin\PdfViewerApp.exe" -ForegroundColor White
