param(
    [string]$ZipPath = "",
    [string]$ManifestPath = "",
    [int]$StartupSeconds = 8,
    [switch]$KeepExtracted
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$smokeRoot = Join-Path $repoRoot "_smoke"

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $repoRoot "releases\update-manifest.json"
}

if ([string]::IsNullOrWhiteSpace($ZipPath)) {
    if (-not (Test-Path -LiteralPath $ManifestPath)) {
        throw "ZipPath was not provided and manifest was not found: $ManifestPath"
    }

    $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    if ($null -eq $manifest.file -or [string]::IsNullOrWhiteSpace([string]$manifest.file)) {
        throw "Manifest field 'file' is required to locate the update ZIP."
    }

    $ZipPath = Join-Path (Split-Path -Parent $ManifestPath) ([string]$manifest.file)
}

if (-not (Test-Path -LiteralPath $ZipPath)) {
    throw "Update ZIP does not exist: $ZipPath"
}

if ($StartupSeconds -lt 3) {
    throw "StartupSeconds must be at least 3."
}

New-Item -ItemType Directory -Force -Path $smokeRoot | Out-Null

$zipItem = Get-Item -LiteralPath $ZipPath
$safeName = [IO.Path]::GetFileNameWithoutExtension($zipItem.Name) -replace '[^0-9A-Za-z\.\-_]', '_'
$extractDir = Join-Path $smokeRoot "UpdateCandidate_$safeName"
$resolvedSmokeRoot = (Resolve-Path $smokeRoot).Path

if (-not $extractDir.StartsWith($resolvedSmokeRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe smoke test extract path: $extractDir"
}

if (Test-Path -LiteralPath $extractDir) {
    Remove-Item -LiteralPath $extractDir -Recurse -Force
}
New-Item -ItemType Directory -Path $extractDir | Out-Null

Write-Host "Extracting update candidate..." -ForegroundColor Cyan
Expand-Archive -LiteralPath $ZipPath -DestinationPath $extractDir -Force

$requiredFiles = @(
    "PdfViewerApp.exe",
    "pdf_core.dll",
    "pdfium.dll"
)

foreach ($file in $requiredFiles) {
    $fullPath = Join-Path $extractDir $file
    if (-not (Test-Path -LiteralPath $fullPath)) {
        throw "Smoke test failed. Missing required file in ZIP: $file"
    }
}

$exePath = Join-Path $extractDir "PdfViewerApp.exe"
$process = $null

try {
    Write-Host "Launching app for $StartupSeconds seconds..." -ForegroundColor Cyan
    $process = Start-Process -FilePath $exePath -WorkingDirectory $extractDir -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds $StartupSeconds

    if ($process.HasExited) {
        throw "Smoke test failed. App exited during startup. ExitCode=$($process.ExitCode)"
    }

    Write-Host "App startup smoke test passed." -ForegroundColor Green
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        try {
            $null = $process.CloseMainWindow()
            Start-Sleep -Seconds 2
        }
        catch {
        }

        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }
    }

    if (-not $KeepExtracted) {
        Remove-Item -LiteralPath $extractDir -Recurse -Force
    }
}

Write-Host "Smoke test completed." -ForegroundColor Green
Write-Host "ZIP: $($zipItem.FullName)"
Write-Host "ExtractedTo: $extractDir"
