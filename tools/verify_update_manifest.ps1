param(
    [string]$ManifestPath = "",
    [string]$ReleaseDir = "",
    [switch]$RequireDownloadUrl
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($ReleaseDir)) {
    $ReleaseDir = Join-Path $repoRoot "releases"
}
if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $ReleaseDir "update-manifest.json"
}

if (-not (Test-Path -LiteralPath $ManifestPath)) {
    throw "Missing update manifest: $ManifestPath"
}

$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json

function Assert-TextValue {
    param(
        [object]$Value,
        [string]$Name
    )

    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) {
        throw "Manifest field '$Name' is required."
    }
}

Assert-TextValue $manifest.version "version"
Assert-TextValue $manifest.file "file"
Assert-TextValue $manifest.sha256 "sha256"

if (($manifest.sha256 -as [string]) -notmatch '^[a-fA-F0-9]{64}$') {
    throw "Manifest field 'sha256' must be a 64-character hex SHA256 hash."
}

if ($null -eq $manifest.size -or [int64]$manifest.size -le 0) {
    throw "Manifest field 'size' must be a positive byte count."
}

if ($null -ne $manifest.release_date -and -not [string]::IsNullOrWhiteSpace([string]$manifest.release_date)) {
    try {
        [DateTimeOffset]::Parse([string]$manifest.release_date) | Out-Null
    }
    catch {
        throw "Manifest field 'release_date' is not a valid date/time value."
    }
}

if ($RequireDownloadUrl) {
    Assert-TextValue $manifest.download_url "download_url"
}

if ($null -ne $manifest.download_url -and -not [string]::IsNullOrWhiteSpace([string]$manifest.download_url)) {
    $downloadUrl = [string]$manifest.download_url
    if ($downloadUrl -notmatch '^https?://') {
        throw "Manifest field 'download_url' must start with http:// or https://."
    }
}

$zipPath = Join-Path $ReleaseDir ([string]$manifest.file)
if (-not (Test-Path -LiteralPath $zipPath)) {
    throw "ZIP referenced by manifest does not exist: $zipPath"
}

$zipItem = Get-Item -LiteralPath $zipPath
if ([int64]$manifest.size -ne $zipItem.Length) {
    throw "ZIP size mismatch. Manifest=$($manifest.size), actual=$($zipItem.Length)."
}

$actualSha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$expectedSha256 = ([string]$manifest.sha256).ToLowerInvariant()
if ($expectedSha256 -ne $actualSha256) {
    throw "ZIP SHA256 mismatch. Manifest=$expectedSha256, actual=$actualSha256."
}

Write-Host "Update manifest verified." -ForegroundColor Green
Write-Host "Version: $($manifest.version)"
Write-Host "File: $zipPath"
Write-Host "Size: $($zipItem.Length) bytes"
Write-Host "SHA256: $actualSha256"
