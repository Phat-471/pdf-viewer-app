param(
    [string]$OutputDir = "",
    [switch]$IncludeBuildOutputs
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "backups"
}

$requiredPaths = @(
    "src\PdfViewerApp",
    "src\PdfViewerApp\PdfViewerApp.csproj",
    "src\PdfCore",
    "src\PdfCore\Cargo.toml",
    "VERSION.txt",
    "README.md",
    "tools"
)

foreach ($relativePath in $requiredPaths) {
    $fullPath = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath)) {
        throw "Cannot create source backup. Missing required path: $fullPath"
    }
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$version = (Get-Content -LiteralPath (Join-Path $repoRoot "VERSION.txt") -Raw).Trim()
if ([string]::IsNullOrWhiteSpace($version)) {
    $version = "unknown"
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$safeVersion = $version -replace '[^0-9A-Za-z\.\-_]', '_'
$backupName = "pdfpro-source-v$safeVersion-$timestamp.zip"
$backupPath = Join-Path $OutputDir $backupName

$tempRoot = Join-Path $env:TEMP ("pdfpro-source-backup-" + [Guid]::NewGuid().ToString("N"))
$tempProject = Join-Path $tempRoot "pdf"
New-Item -ItemType Directory -Force -Path $tempProject | Out-Null

try {
    $itemsToCopy = @(
        "src",
        "libs",
        "tools",
        "app-upgrade",
        "wp-pdfpro-licensing",
        "README.md",
        "readme_project.md",
        "VERSION.txt",
        "build_project.ps1",
        "package_project.ps1",
        "copy_resources.ps1",
        "install.ps1",
        "install.bat",
        "uninstall.ps1",
        "uninstall.bat",
        "run_test.bat"
    )

    foreach ($item in $itemsToCopy) {
        $source = Join-Path $repoRoot $item
        if (Test-Path -LiteralPath $source) {
            $destination = Join-Path $tempProject $item
            if ((Get-Item -LiteralPath $source).PSIsContainer) {
                Copy-Item -LiteralPath $source -Destination $destination -Recurse -Force
            }
            else {
                $parent = Split-Path -Parent $destination
                New-Item -ItemType Directory -Force -Path $parent | Out-Null
                Copy-Item -LiteralPath $source -Destination $destination -Force
            }
        }
    }

    if (-not $IncludeBuildOutputs) {
        Get-ChildItem -LiteralPath $tempProject -Directory -Recurse -Force |
            Where-Object { $_.Name -in @("bin", "obj", "target", ".dart_tool", "build") } |
            Sort-Object FullName -Descending |
            Remove-Item -Recurse -Force
    }

    if (Test-Path -LiteralPath $backupPath) {
        Remove-Item -LiteralPath $backupPath -Force
    }

    Compress-Archive -Path (Join-Path $tempProject "*") -DestinationPath $backupPath -Force
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}

$backupItem = Get-Item -LiteralPath $backupPath
$sha256 = (Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash.ToLowerInvariant()

Write-Host "Source backup created." -ForegroundColor Green
Write-Host "File: $($backupItem.FullName)"
Write-Host "Size: $($backupItem.Length) bytes"
Write-Host "SHA256: $sha256"
