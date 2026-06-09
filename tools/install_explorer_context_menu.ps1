param(
    [Parameter(Mandatory = $true)]
    [string]$AppExePath
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $AppExePath)) {
    throw "App exe not found: $AppExePath"
}

$baseKey = "HKCU:\Software\Classes\SystemFileAssociations\.pdf\shell\PdfPro.Merge"
$commandKey = Join-Path $baseKey "command"

New-Item -Path $commandKey -Force | Out-Null
New-ItemProperty -Path $baseKey -Name "MUIVerb" -Value "Merge PDF with PDF Hphat" -PropertyType String -Force | Out-Null
$iconPath = Join-Path (Split-Path $AppExePath) "Assets\hphat_logo_1780279208636.png"
if (Test-Path -LiteralPath $iconPath) {
    New-ItemProperty -Path $baseKey -Name "Icon" -Value $iconPath -PropertyType String -Force | Out-Null
} else {
    New-ItemProperty -Path $baseKey -Name "Icon" -Value $AppExePath -PropertyType String -Force | Out-Null
}
New-ItemProperty -Path $baseKey -Name "MultiSelectModel" -Value "Player" -PropertyType String -Force | Out-Null
New-ItemProperty -Path $baseKey -Name "Position" -Value "Top" -PropertyType String -Force | Out-Null

# Explorer appends the selected files to the command line for this verb.
$command = "`"$AppExePath`" `"%1`" --merge --exit-after-merge"
Set-ItemProperty -Path $commandKey -Name "(default)" -Value $command

Write-Host "Installed context menu entry for .pdf files."
Write-Host "If the menu does not appear immediately, restart Explorer or sign out/in."
