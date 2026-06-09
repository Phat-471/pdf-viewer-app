param(
    [Parameter(Mandatory = $true)]
    [string]$MachineId
)

$secret = "HPhat.PdfPro.LocalActivation.2026"

function Keep-AlphaNumeric([string]$value) {
    return (($value.ToUpperInvariant().ToCharArray() | Where-Object { [char]::IsLetterOrDigit($_) }) -join "")
}

function Get-Sha256Hex([string]$value) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($value)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha.ComputeHash($bytes)
        return (($hash | ForEach-Object { $_.ToString("X2") }) -join "")
    }
    finally {
        $sha.Dispose()
    }
}

function Split-Groups([string]$value, [int]$size) {
    $groups = New-Object System.Collections.Generic.List[string]
    for ($i = 0; $i -lt $value.Length; $i += $size) {
        $length = [Math]::Min($size, $value.Length - $i)
        $groups.Add($value.Substring($i, $length))
    }

    return ($groups -join "-")
}

$normalizedMachineId = Keep-AlphaNumeric $MachineId
if ([string]::IsNullOrWhiteSpace($normalizedMachineId)) {
    throw "MachineId is empty."
}

$payload = "PDFPRO-ACTIVATION-v1|$normalizedMachineId|$secret"
$hash = Get-Sha256Hex $payload
$keyBody = $hash.Substring(0, 20)

"PDFPRO-$(Split-Groups $keyBody 4)"
