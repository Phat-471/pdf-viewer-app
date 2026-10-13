param(
    [ValidateSet("patch", "minor", "major")]
    [string]$Part = "patch",
    [Parameter(Mandatory = $true)]
    [string[]]$ChangedPaths,
    [switch]$UseCurrentVersion,
    [switch]$SkipSmokeTest
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
Set-Location $repoRoot

function Invoke-ReleaseCommand {
    param([string]$Command, [string[]]$Arguments)
    $savedPreference = $ErrorActionPreference
    try {
        # Windows PowerShell treats native stderr as ErrorRecord, even on success.
        $ErrorActionPreference = 'Continue'
        & $Command @Arguments 2>&1 | ForEach-Object { Write-Host $_ }
        $commandExitCode = $LASTEXITCODE
    } finally { $ErrorActionPreference = $savedPreference }
    if ($commandExitCode -ne 0) { throw "$Command failed with exit code $commandExitCode." }
}

Invoke-ReleaseCommand -Command gh -Arguments @('auth', 'status')
$branch = (git branch --show-current).Trim()
if ($branch -ne 'master') { throw "Release from master is required. Current branch: $branch." }
if (git diff --cached --name-only) { throw 'The index already has staged changes. Review them before releasing.' }
foreach ($path in $ChangedPaths) {
    if ((Test-Path -LiteralPath $path -PathType Container) -or [Management.Automation.WildcardPattern]::ContainsWildcardCharacters($path)) {
        throw "Pass individual file paths, not directories or wildcards: $path"
    }
    if ($path -match '(^|[/\\])(publish_settings\.json|publish_config\.json)$|^(releases|backups|vn-privacy-form-builder)([/\\]|$)') {
        throw "Path is outside the app release scope or contains local configuration: $path"
    }
}
Invoke-ReleaseCommand -Command git -Arguments @('fetch', 'origin', 'master')
Invoke-ReleaseCommand -Command git -Arguments @('merge-base', '--is-ancestor', 'origin/master', 'HEAD')

if (-not $UseCurrentVersion) {
    Write-Host "Bumping version ($Part)..." -ForegroundColor Cyan
    & (Join-Path $scriptDir "bump_version.ps1") -Part $Part
}
$version = (Get-Content VERSION.txt -Raw).Trim()
$tag = "v$version"
$remoteTag = git ls-remote origin "refs/tags/$tag"
if ($LASTEXITCODE -ne 0) { throw 'Cannot check the remote release tag.' }
if ($remoteTag -or (git tag --list $tag)) { throw "Release tag $tag already exists; use a new version." }

Write-Host "Packaging release..." -ForegroundColor Cyan
& (Join-Path $repoRoot "package_project.ps1") -SkipPublish -SkipLocalDeploy
Set-Location $repoRoot

Write-Host "Verifying update manifest..." -ForegroundColor Cyan
& (Join-Path $scriptDir "verify_update_manifest.ps1")

if (-not $SkipSmokeTest) {
    Write-Host "Running update candidate smoke test..." -ForegroundColor Cyan
    & (Join-Path $scriptDir "smoke_test_update_candidate.ps1")
}

Write-Host "Committing and pushing source and release tag..." -ForegroundColor Cyan
Set-Location $repoRoot
$metadataPaths = @('VERSION.txt', 'CHANGELOG.txt', 'src/PdfCore/Cargo.toml', 'src/PdfCore/Cargo.lock', 'src/PdfViewerApp/Properties/AssemblyInfo.cs')
$paths = @($ChangedPaths + $metadataPaths | Select-Object -Unique)
Invoke-ReleaseCommand -Command git -Arguments (@('add', '--') + $paths)
Invoke-ReleaseCommand -Command git -Arguments @('diff', '--cached', '--check')
Invoke-ReleaseCommand -Command git -Arguments @('commit', '-m', "release: update PDF Pro v$version")
Invoke-ReleaseCommand -Command git -Arguments @('tag', '-a', $tag, '-m', "PDF Pro $version")
Invoke-ReleaseCommand -Command git -Arguments @('push', '--atomic', 'origin', 'HEAD:refs/heads/master', "refs/tags/$tag")

$commit = (git rev-parse HEAD).Trim()
$runId = ''
for ($attempt = 0; $attempt -lt 20 -and -not $runId; $attempt++) {
    $runId = gh run list --workflow release.yml --commit $commit --event push --json databaseId --jq '.[0].databaseId'
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect the release workflow.' }
    if (-not $runId) { Start-Sleep -Seconds 3 }
}
if (-not $runId) { throw 'The tag was pushed, but the release workflow could not be found. Inspect GitHub Actions.' }
Invoke-ReleaseCommand -Command gh -Arguments @('run', 'watch', ([string]$runId).Trim(), '--exit-status')
Invoke-ReleaseCommand -Command gh -Arguments @('release', 'view', $tag)

Write-Host "Release $tag completed: GitHub assets and the WordPress update endpoint have been verified by CI." -ForegroundColor Green
