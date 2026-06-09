param(
    [string]$AppProjectDir = "",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$templateDir = Join-Path $repoRoot "app-upgrade\UpdateClient"

if ([string]::IsNullOrWhiteSpace($AppProjectDir)) {
    $AppProjectDir = Join-Path $repoRoot "src\PdfViewerApp"
}

if (-not (Test-Path -LiteralPath $templateDir)) {
    throw "Missing update client template folder: $templateDir"
}

if (-not (Test-Path -LiteralPath $AppProjectDir)) {
    throw "Missing app project folder: $AppProjectDir. Restore src\PdfViewerApp before installing the update client."
}

$projectFile = Join-Path $AppProjectDir "PdfViewerApp.csproj"
if (-not (Test-Path -LiteralPath $projectFile)) {
    throw "Missing app project file: $projectFile"
}

$targetDir = Join-Path $AppProjectDir "Services\Update"
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

foreach ($file in @("AppUpdateModels.cs", "AppUpdateService.cs")) {
    $source = Join-Path $templateDir $file
    $target = Join-Path $targetDir $file

    if ((Test-Path -LiteralPath $target) -and -not $Force) {
        throw "Target already exists: $target. Re-run with -Force to overwrite."
    }

    Copy-Item -LiteralPath $source -Destination $target -Force:$Force
    Write-Host "Installed $target" -ForegroundColor Green
}

Write-Host "Update client installed into $targetDir" -ForegroundColor Green
Write-Host "Next: add a WPF button/startup check that calls AppUpdateService.CheckAsync()."
