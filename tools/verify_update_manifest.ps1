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

$manifest = Get-Content -LiteralPath $ManifestPath -Encoding UTF8 -Raw | ConvertFrom-Json
if ([string]$manifest.version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid stable release version.' }
if ($manifest.file -ne "PdfViewerApp_v$($manifest.version).zip") { throw 'ZIP name does not match the release version.' }

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

if ($manifest.schema_version -ne 2 -or $null -eq $manifest.installer) {
    throw 'A complete release must include installer metadata (schema_version=2).'
}
$installer = $manifest.installer
if ($installer.file -ne "PDFPro_Setup_v$($manifest.version).exe") { throw 'Installer name does not match the release version.' }
if ([string]$installer.sha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'Invalid installer SHA256.' }
$installerPath = Join-Path $ReleaseDir $installer.file
$installerItem = Get-Item -LiteralPath $installerPath -ErrorAction Stop
if ($installerItem.Length -le 0 -or $installerItem.Length -ne [int64]$installer.size) { throw 'Installer size mismatch.' }
if ((Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash -ne $installer.sha256) { throw 'Installer SHA256 mismatch.' }
$info = [Diagnostics.FileVersionInfo]::GetVersionInfo($installerPath)
$binaryVersion = '{0}.{1}.{2}.{3}' -f $info.FileMajorPart, $info.FileMinorPart, $info.FileBuildPart, $info.FilePrivatePart
if ([Version]$binaryVersion -ne [Version]"$($manifest.version).0") { throw 'Installer binary version does not match the manifest.' }

foreach ($artifact in @($manifest, $installer)) {
    if ($RequireDownloadUrl -or $artifact.download_url) {
        Assert-TextValue $artifact.download_url 'download_url'
        $uri = $null
        if (-not [Uri]::TryCreate([string]$artifact.download_url, [UriKind]::Absolute, [ref]$uri) -or
            $uri.Scheme -ne 'https' -or
            [Uri]::UnescapeDataString($uri.Segments[-1]) -ne $artifact.file) { throw 'Artifact URL must be HTTPS and reference its own filename.' }
    }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $names = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
    foreach ($required in @('PdfViewerApp.exe', 'pdf_core.dll', 'pdfium.dll', 'install.bat', 'install.ps1', 'uninstall.bat', 'uninstall.ps1')) {
        if ($required -notin $names) { throw "Update ZIP is missing $required at its root." }
        if (($archive.Entries | Where-Object { $_.FullName -eq $required }).Length -le 0) { throw "Update ZIP contains empty $required." }
    }
    foreach ($name in $names) {
        if ($name -match '(^/|(^|/)\.\.(/|$)|:)' -or $name -match '(^|/)(publish_config|publish_settings)\.json$') {
            throw "Invalid update ZIP entry: $name"
        }
    }
} finally { $archive.Dispose() }

$checksumPath = Join-Path $ReleaseDir 'SHA256SUMS.txt'
$checksumLines = @(Get-Content -LiteralPath $checksumPath)
if ($checksumLines.Count -ne 3) { throw 'SHA256SUMS.txt must contain exactly the ZIP, installer and manifest.' }
foreach ($name in @($manifest.file, $installer.file, 'update-manifest.json')) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $ReleaseDir $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ("$hash  $name" -cnotin $checksumLines) { throw "Checksum list mismatch for $name." }
}
Write-Host 'Complete release verified: installer, ZIP payload, manifest and checksums.' -ForegroundColor Green
