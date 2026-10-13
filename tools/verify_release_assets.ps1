param([string]$ReleaseDir = '')

$ErrorActionPreference = 'Stop'
if (-not $ReleaseDir) { $ReleaseDir = Join-Path (Split-Path -Parent $PSScriptRoot) 'releases' }
& (Join-Path $PSScriptRoot 'verify_update_manifest.ps1') -ReleaseDir $ReleaseDir -RequireDownloadUrl
$manifest = Get-Content -LiteralPath (Join-Path $ReleaseDir 'update-manifest.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if ($manifest.download_url -notmatch '^https://github\.com/(?<owner>[^/]+)/(?<repo>[^/]+)/releases/download/(?<tag>[^/]+)/') {
    throw 'Production release assets must be hosted on a tagged GitHub Release.'
}
$repo = "$($Matches.owner)/$($Matches.repo)"
$tag = $Matches.tag
if ($tag -ne "v$($manifest.version)") { throw 'Release tag and manifest version differ.' }
$headers = @{ 'Accept' = 'application/vnd.github+json'; 'User-Agent' = 'PDFPro-Release-Verification' }
if ($env:GH_TOKEN) { $headers.Authorization = "Bearer $env:GH_TOKEN" }
$release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/tags/$tag" -Headers $headers -TimeoutSec 30
if ($release.draft -or $release.prerelease -or $release.tag_name -ne $tag) { throw 'The stable release is not publicly published.' }
foreach ($name in @($manifest.file, $manifest.installer.file, 'update-manifest.json', 'SHA256SUMS.txt')) {
    $asset = @($release.assets | Where-Object { $_.name -ceq $name })
    if ($asset.Count -ne 1 -or $asset[0].state -ne 'uploaded') { throw "Missing or incomplete public asset: $name" }
    $path = Join-Path $ReleaseDir $name
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ([int64]$asset[0].size -ne (Get-Item -LiteralPath $path).Length -or $asset[0].digest -ne "sha256:$hash") {
        throw "Public release asset does not match the verified package: $name"
    }
    $expectedUrl = "https://github.com/$repo/releases/download/$tag/$name"
    if ($asset[0].browser_download_url -ne $expectedUrl) { throw "Unexpected public asset URL: $name" }
    if ($name -eq $manifest.installer.file -and $manifest.installer.download_url -ne $expectedUrl) { throw 'Installer download URL mismatch.' }
}
Write-Host "Public release $tag verified: all four required assets match the build." -ForegroundColor Green
