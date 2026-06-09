param(
    [ValidateSet("patch", "minor", "major")]
    [string]$Part = "patch"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$versionFile = Join-Path $repoRoot "VERSION.txt"

if (-not (Test-Path $versionFile)) {
    throw "Missing VERSION.txt at $versionFile"
}

$currentVersion = (Get-Content -LiteralPath $versionFile -Raw).Trim()
if ([string]::IsNullOrWhiteSpace($currentVersion)) {
    throw "VERSION.txt is empty."
}

if ($currentVersion -notmatch '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?<suffix>.*)$') {
    throw "Unsupported version format: $currentVersion. Expected X.Y.Z or X.Y.Z-suffix."
}

$major = [int]$Matches.major
$minor = [int]$Matches.minor
$patch = [int]$Matches.patch
$suffix = $Matches.suffix

switch ($Part) {
    "major" {
        $major++
        $minor = 0
        $patch = 0
    }
    "minor" {
        $minor++
        $patch = 0
    }
    "patch" {
        $patch++
    }
}

$nextVersion = "{0}.{1}.{2}{3}" -f $major, $minor, $patch, $suffix
Set-Content -LiteralPath $versionFile -Value $nextVersion -NoNewline

$assemblyInfoPath = Join-Path $repoRoot "src\PdfViewerApp\Properties\AssemblyInfo.cs"
if (Test-Path -LiteralPath $assemblyInfoPath) {
    $assemblyVersion = if ($nextVersion -match '^(?<core>\d+\.\d+\.\d+)') { $Matches.core } else { $nextVersion }
    $assemblyInfo = Get-Content -LiteralPath $assemblyInfoPath -Raw
    $assemblyInfo = $assemblyInfo -replace 'AssemblyFileVersion\(".*?"\)', "AssemblyFileVersion(`"$assemblyVersion.0`")"
    $assemblyInfo = $assemblyInfo -replace 'AssemblyInformationalVersion\(".*?"\)', "AssemblyInformationalVersion(`"$assemblyVersion`")"
    $assemblyInfo = $assemblyInfo -replace 'AssemblyVersion\(".*?"\)', "AssemblyVersion(`"$assemblyVersion.0`")"
    Set-Content -LiteralPath $assemblyInfoPath -Value $assemblyInfo -NoNewline
}

Write-Host "Updated version: $currentVersion -> $nextVersion" -ForegroundColor Green
