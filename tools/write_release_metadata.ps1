param(
    [string]$ReleaseDir = "",
    [string]$DownloadBaseUrl = ""
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $ReleaseDir) { $ReleaseDir = Join-Path $repoRoot 'releases' }
$version = (Get-Content -LiteralPath (Join-Path $repoRoot 'VERSION.txt') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'A stable X.Y.Z version is required.' }
if ($DownloadBaseUrl -and $DownloadBaseUrl -notmatch '^https://') { throw 'Release URLs must use HTTPS.' }

function Get-ArtifactMetadata([string]$Name) {
    $path = Join-Path $ReleaseDir $Name
    $item = Get-Item -LiteralPath $path -ErrorAction Stop
    if ($item.Length -le 0) { throw "Empty release artifact: $Name" }
    return [ordered]@{
        file = $Name
        sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        size = $item.Length
        download_url = $(if ($DownloadBaseUrl) { "$($DownloadBaseUrl.TrimEnd('/'))/$Name" } else { '' })
    }
}

$zip = Get-ArtifactMetadata "PdfViewerApp_v$version.zip"
$installer = Get-ArtifactMetadata "PDFPro_Setup_v$version.exe"
$manifest = [ordered]@{
    schema_version = 2
    version = $version
    file = $zip.file
    sha256 = $zip.sha256
    size = $zip.size
    release_date = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    download_url = $zip.download_url
    mandatory = $false
    changelog = (Get-Content -LiteralPath (Join-Path $repoRoot 'CHANGELOG.txt') -Encoding UTF8 -Raw).Trim()
    installer = $installer
}
$manifestPath = Join-Path $ReleaseDir 'update-manifest.json'
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

# Write last: the checksum of the manifest includes its final download URLs.
$lines = foreach ($name in @($zip.file, $installer.file, 'update-manifest.json')) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $ReleaseDir $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $name"
}
$lines | Set-Content -LiteralPath (Join-Path $ReleaseDir 'SHA256SUMS.txt') -Encoding ASCII
$summary = @"
# PDF Pro $version

- Install or reinstall: $($installer.file)
- In-app update package: $($zip.file) (includes the app, native DLLs and installation scripts)
- Update metadata: update-manifest.json
- Verify file integrity: SHA256SUMS.txt

The application update URL must point to the ZIP. GitHub source archives are not installation packages.

$($manifest.changelog)
"@
$summary | Set-Content -LiteralPath (Join-Path $ReleaseDir 'update-package-summary.md') -Encoding UTF8
Write-Host "Release metadata and checksums created for $version."
