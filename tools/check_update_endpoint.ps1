param(
    [Parameter(Mandatory = $true)]
    [string]$ApiUrl,

    [string]$CurrentVersion = "0.0.0",
    [switch]$CheckDownload,
    [int]$TimeoutSeconds = 30
)

$ErrorActionPreference = "Stop"

if ($ApiUrl -notmatch '^https?://') {
    throw "ApiUrl must start with http:// or https://."
}

$endpoint = $ApiUrl.TrimEnd("/")
if ($endpoint -notmatch '/wp-json/pdfpro/v1/update-check$') {
    $endpoint = "$endpoint/wp-json/pdfpro/v1/update-check"
}

$payload = @{
    current_version = $CurrentVersion
} | ConvertTo-Json

Write-Host "Checking update endpoint: $endpoint" -ForegroundColor Cyan
$response = Invoke-RestMethod `
    -Uri $endpoint `
    -Method Post `
    -ContentType "application/json" `
    -Body $payload `
    -TimeoutSec $TimeoutSeconds

if ($null -eq $response -or $response.success -ne $true) {
    throw "Update endpoint did not return success=true."
}

foreach ($field in @("latest_version", "download_url", "sha256", "file_size")) {
    if (-not ($response.PSObject.Properties.Name -contains $field)) {
        throw "Update endpoint missing required field '$field'."
    }
}

if (-not [string]::IsNullOrWhiteSpace([string]$response.sha256) -and ([string]$response.sha256) -notmatch '^[a-fA-F0-9]{64}$') {
    throw "Update endpoint field 'sha256' must be a 64-character hex SHA256 hash."
}

if ([int64]$response.file_size -lt 0) {
    throw "Update endpoint field 'file_size' cannot be negative."
}

if (-not [string]::IsNullOrWhiteSpace([string]$response.download_url) -and ([string]$response.download_url) -notmatch '^https?://') {
    throw "Update endpoint field 'download_url' must start with http:// or https://."
}

if ($CheckDownload) {
    if ([string]::IsNullOrWhiteSpace([string]$response.download_url)) {
        throw "Cannot check download because endpoint returned an empty download_url."
    }

    $tempFile = Join-Path $env:TEMP ("pdfpro-update-check-" + [Guid]::NewGuid().ToString("N") + ".zip")
    try {
        Write-Host "Downloading update package for verification..." -ForegroundColor Cyan
        Invoke-WebRequest -Uri ([string]$response.download_url) -OutFile $tempFile -TimeoutSec $TimeoutSeconds

        $downloadedItem = Get-Item -LiteralPath $tempFile
        if ([int64]$response.file_size -gt 0 -and $downloadedItem.Length -ne [int64]$response.file_size) {
            throw "Downloaded file size mismatch. Endpoint=$($response.file_size), actual=$($downloadedItem.Length)."
        }

        if (-not [string]::IsNullOrWhiteSpace([string]$response.sha256)) {
            $actualSha256 = (Get-FileHash -LiteralPath $tempFile -Algorithm SHA256).Hash.ToLowerInvariant()
            $expectedSha256 = ([string]$response.sha256).ToLowerInvariant()
            if ($actualSha256 -ne $expectedSha256) {
                throw "Downloaded file SHA256 mismatch. Endpoint=$expectedSha256, actual=$actualSha256."
            }
        }
    }
    finally {
        if (Test-Path -LiteralPath $tempFile) {
            Remove-Item -LiteralPath $tempFile -Force
        }
    }
}

Write-Host "Update endpoint verified." -ForegroundColor Green
Write-Host "Latest version: $($response.latest_version)"
Write-Host "Mandatory: $($response.mandatory)"
Write-Host "File size: $($response.file_size)"
Write-Host "SHA256: $($response.sha256)"
Write-Host "Download URL: $($response.download_url)"
