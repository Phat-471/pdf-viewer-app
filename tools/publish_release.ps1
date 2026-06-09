# publish_release.ps1
# Automates publishing the generated update manifest to the WordPress licensing server

$ErrorActionPreference = "Stop"
$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $toolsDir

$manifestPath = Join-Path $repoRoot "releases\update-manifest.json"
$configPath = Join-Path $toolsDir "publish_config.json"

if (-not (Test-Path -LiteralPath $manifestPath)) {
    Write-Warning "Manifest file not found: $manifestPath. Please run package_project.ps1 first."
    exit 0
}

if (-not (Test-Path -LiteralPath $configPath)) {
    # Generate template config
    $templateConfig = @{
        site_url = "https://hongmien.vn"
        publish_token = "YOUR_SECURE_PUBLISH_TOKEN_HERE"
    }
    $templateConfig | ConvertTo-Json | Out-File -FilePath $configPath -Encoding utf8
    Write-Host "Created template publish configuration at: $configPath" -ForegroundColor Yellow
    Write-Host "Please edit this file with your WordPress site URL and publish token, then re-run to publish." -ForegroundColor Yellow
    exit 0
}

# Read configuration
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$siteUrl = ($config.site_url -as [string]).TrimEnd('/')
$token = ($config.publish_token -as [string])

if ($token -eq "YOUR_SECURE_PUBLISH_TOKEN_HERE" -or [string]::IsNullOrWhiteSpace($token)) {
    Write-Warning "Please configure a valid publish_token in $configPath."
    exit 0
}

# Read and parse update-manifest.json
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json

# Construct publish payload
$payload = @{
    token = $token
    version = $manifest.version
    download_url = $manifest.download_url
    sha256 = $manifest.sha256
    size = $manifest.size
    release_date = $manifest.release_date
    mandatory = $manifest.mandatory
    changelog = $manifest.changelog
}

$publishUrl = "$siteUrl/wp-json/pdfpro/v1/update-publish"
Write-Host "Publishing release v$($manifest.version) to $publishUrl..." -ForegroundColor Cyan

try {
    $response = Invoke-RestMethod -Uri $publishUrl -Method Post -Body (ConvertTo-Json $payload) -ContentType "application/json" -TimeoutSec 15
    if ($response.success) {
        Write-Host "Successfully published! Server message: $($response.message)" -ForegroundColor Green
    } else {
        Write-Error "Publish failed: $($response.message)"
    }
} catch {
    Write-Error "Error sending request to WordPress server: $_"
}
