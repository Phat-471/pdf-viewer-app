param(
    [string]$BackupPath = (Join-Path $PSScriptRoot '../backups/explorer_menu_labels.json')
)

$ErrorActionPreference = 'Stop'
$accentE = [char]0x00e9
$accentA = [char]0x1eb1
$entries = @(
    @{ Verb = 'PdfPro.Merge'; Label = "Gh${accentE}p PDF b${accentA}ng PDF HPhat" },
    @{ Verb = 'PDFHP.Merge'; Label = "Gh${accentE}p file b${accentA}ng PDF HP" }
)
$changes = @()
foreach ($entry in $entries) {
    $key = "HKCU:\Software\Classes\SystemFileAssociations\.pdf\shell\$($entry.Verb)"
    if (Test-Path -LiteralPath $key) {
        $old = (Get-ItemProperty -LiteralPath $key -Name MUIVerb -ErrorAction SilentlyContinue).MUIVerb
        if ($old -ne $entry.Label) {
            $changes += [pscustomobject]@{ Key = $key; OldLabel = $old; NewLabel = $entry.Label }
        }
    }
}
if ($changes.Count -gt 0) {
    $directory = Split-Path -Parent ([IO.Path]::GetFullPath($BackupPath))
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    if (Test-Path -LiteralPath $BackupPath) { throw "Backup already exists: $BackupPath. Choose another backup path." }
    $changes | ConvertTo-Json | Set-Content -LiteralPath $BackupPath -Encoding UTF8
    foreach ($change in $changes) {
        Set-ItemProperty -LiteralPath $change.Key -Name MUIVerb -Value $change.NewLabel
        Write-Host "Repaired menu label: $($change.Key)"
    }
} else {
    Write-Host 'PDF menu labels are already correct.'
}
