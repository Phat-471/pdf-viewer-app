param(
    [Parameter(Mandatory = $true)][string]$InstallDir,
    [Parameter(Mandatory = $true)][string]$BackupZip,
    [Parameter(Mandatory = $true)][string]$AppExe,
    [string]$UpdateZip = '',
    [string]$MarkerPath = '',
    [string]$TargetVersion = '',
    [string]$ExpectedSha256 = '',
    [int]$ParentPid = 0,
    [int]$CurrentPid = 0,
    [int]$TimeoutSeconds = 120,
    [switch]$RestoreOnly
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$AttemptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Start-Transcript -Path (Join-Path $AttemptDir 'update-powershell.log') -Append -ErrorAction SilentlyContinue | Out-Null
$InstallDir = [IO.Path]::GetFullPath($InstallDir).TrimEnd('\', '/')
$AppExe = [IO.Path]::GetFullPath($AppExe)
$InstallPrefix = $InstallDir + '\'
if ($AppExe -ne (Join-Path $InstallDir 'PdfViewerApp.exe') -or $InstallDir -eq [IO.Path]::GetPathRoot($InstallDir).TrimEnd('\')) {
    throw 'Invalid application installation directory.'
}
$RestoreOnly = $RestoreOnly -or [string]::IsNullOrWhiteSpace($UpdateZip)
if ($CurrentPid -gt 0) { $ParentPid = $CurrentPid }
$sha = [Security.Cryptography.SHA256]::Create()
try { $key = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($InstallDir.ToLowerInvariant())))).Replace('-', '').Substring(0, 16).ToLowerInvariant() }
finally { $sha.Dispose() }
$InstallerMutex = [Threading.Mutex]::new($false, ('Local\PdfPro.UpdateInstaller.' + $key))
$OwnMutex = $false
$TransactionDir = [IO.Path]::GetFullPath((Join-Path $InstallDir ('.pdfpro-update-' + [Guid]::NewGuid().ToString('N'))))
$PayloadDir = Join-Path $TransactionDir 'payload'
$OldFilesDir = Join-Path $TransactionDir 'previous'
$ChangedFiles = [Collections.Generic.List[object]]::new()
$LaunchedProcess = $null
$RolledBack = $false

function Normalize-Version([string]$Value) {
    $v = [version]($Value.Trim().TrimStart('v', 'V'))
    return "$($v.Major).$($v.Minor).$($v.Build)"
}
function Invoke-WithRetry([scriptblock]$Action, [int]$Seconds = 60) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    while ($true) {
        try { & $Action; return }
        catch {
            if ([DateTime]::UtcNow -ge $deadline) { throw }
            Write-Host ('File operation waiting: ' + $_.Exception.Message)
            Start-Sleep -Milliseconds 500
        }
    }
}
function Get-ThisInstallationProcesses {
    return @(Get-Process -Name PdfViewerApp -ErrorAction SilentlyContinue | Where-Object {
        try { $_.Path -and [IO.Path]::GetFullPath($_.Path) -eq $AppExe } catch { $false }
    })
}
function Wait-ForApplicationExit {
    if ($ParentPid -gt 0) {
        $parent = Get-Process -Id $ParentPid -ErrorAction SilentlyContinue
        if ($parent -and -not $parent.WaitForExit(60000)) {
            throw 'The application did not close. No files have been replaced.'
        }
    }
    $others = Get-ThisInstallationProcesses
    foreach ($other in $others) { $null = $other.CloseMainWindow() }
    foreach ($other in $others) {
        if (-not $other.WaitForExit(15000)) { throw 'Another window is still open. Close it before retrying the update.' }
    }
}
function Assert-InstallPath([string]$Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    if (-not $resolved.StartsWith($InstallPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'File is outside the installation directory.' }
    return $resolved
}
function Expand-Payload([string]$ZipPath) {
    if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) { throw 'Installation archive is missing.' }
    $archive = [IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName.Replace('/', '\')
            if ([IO.Path]::IsPathRooted($name) -or $name.Contains(':') -or ($name.Split('\') -contains '..')) { throw 'Invalid archive entry.' }
        }
    } finally { $archive.Dispose() }
    New-Item -ItemType Directory -Path $PayloadDir -Force | Out-Null
    [IO.Compression.ZipFile]::ExtractToDirectory($ZipPath, $PayloadDir)
    foreach ($required in @('PdfViewerApp.exe', 'pdf_core.dll', 'pdfium.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $PayloadDir $required) -PathType Leaf)) { throw "Incomplete package: $required" }
    }
    $version = Normalize-Version ([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $PayloadDir 'PdfViewerApp.exe')).FileVersion)
    if ($TargetVersion -and $version -ne (Normalize-Version $TargetVersion)) { throw 'The staged application version does not match the requested version.' }
    return $version
}
function Install-Payload {
    $prefix = $PayloadDir.TrimEnd('\') + '\'
    # Install the EXE last. A manual launch during installation sees a complete
    # old EXE; the startup mutex prevents it from opening mixed native DLLs.
    $files = @(Get-ChildItem -LiteralPath $PayloadDir -File -Recurse | Sort-Object @{ Expression = { $_.Name -eq 'PdfViewerApp.exe' } }, FullName)
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($prefix.Length)
        $target = Assert-InstallPath (Join-Path $InstallDir $relative)
        $backup = Assert-InstallPath (Join-Path $OldFilesDir $relative)
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        New-Item -ItemType Directory -Path (Split-Path -Parent $backup) -Force | Out-Null
        $existed = Test-Path -LiteralPath $target -PathType Leaf
        if ($existed) {
            Invoke-WithRetry { [IO.File]::Replace($file.FullName, $target, $backup, $true) }
        } else {
            Invoke-WithRetry { [IO.File]::Move($file.FullName, $target) }
        }
        $ChangedFiles.Add([pscustomobject]@{ Target = $target; Backup = $backup; Existed = $existed })
    }
}
function Start-Application {
    $script:LaunchedProcess = $null
    Invoke-WithRetry {
        # CreateProcess reports a retryable error to the helper, rather than
        # displaying Explorer's "Another program is currently using this file".
        $info = [Diagnostics.ProcessStartInfo]::new()
        $info.FileName = $AppExe
        $info.WorkingDirectory = $InstallDir
        $info.UseShellExecute = $false
        $info.Arguments = '--pdfpro-update-launch'
        $script:LaunchedProcess = [Diagnostics.Process]::Start($info)
        if (-not $script:LaunchedProcess) { throw 'The application process was not created.' }
    }
}
function Restore-ChangedFiles {
    # Stop only the executable just launched by this installation transaction.
    if ($LaunchedProcess -and -not $LaunchedProcess.HasExited) {
        $LaunchedProcess.Kill()
        if (-not $LaunchedProcess.WaitForExit(15000)) { throw 'The new process has not exited; rollback was not started.' }
    }
    if ((Get-ThisInstallationProcesses).Count -gt 0) { throw 'An application window is still open; rollback files were retained for recovery.' }
    for ($i = $ChangedFiles.Count - 1; $i -ge 0; $i--) {
        $changed = $ChangedFiles[$i]
        $null = Assert-InstallPath $changed.Target
        $null = Assert-InstallPath $changed.Backup
        if ($changed.Existed) {
            Invoke-WithRetry {
                if ([IO.File]::Exists($changed.Target)) { [IO.File]::Replace($changed.Backup, $changed.Target, $null, $true) }
                else { [IO.File]::Move($changed.Backup, $changed.Target) }
            }
        } else { Invoke-WithRetry { [IO.File]::Delete($changed.Target) } }
    }
    $script:RolledBack = $true
}
try {
    try { $OwnMutex = $InstallerMutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $OwnMutex = $true }
    if (-not $OwnMutex) { throw 'Another update or restore operation is already in progress.' }
    if (-not (Test-Path -LiteralPath $BackupZip -PathType Leaf)) { throw 'Rollback backup is missing.' }
    if (-not $RestoreOnly -and $ExpectedSha256) {
        if ((Get-FileHash -LiteralPath $UpdateZip -Algorithm SHA256).Hash -ne $ExpectedSha256) { throw 'Update archive SHA-256 mismatch.' }
    }
    Wait-ForApplicationExit
    $zip = if ($RestoreOnly) { $BackupZip } else { $UpdateZip }
    $installedVersion = Expand-Payload $zip
    if ($MarkerPath -and (Test-Path -LiteralPath $MarkerPath)) { Remove-Item -LiteralPath $MarkerPath -Force }
    Write-Host 'Applying staged files with atomic replacement...'
    Install-Payload
    foreach ($flag in @('update-success.flag', 'update-failed.flag')) {
        Remove-Item -LiteralPath (Join-Path $InstallDir $flag) -Force -ErrorAction SilentlyContinue
    }
    Start-Application
    if (-not $RestoreOnly) {
        $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        $confirmed = $false
        while ([DateTime]::UtcNow -lt $deadline) {
            if ($MarkerPath -and (Test-Path -LiteralPath $MarkerPath)) {
                try {
                    $confirmation = Get-Content -LiteralPath $MarkerPath -Raw -Encoding UTF8 | ConvertFrom-Json
                    $attemptId = Split-Path -Leaf $AttemptDir
                    $confirmed = $confirmation.attempt_id -eq $attemptId -and $confirmation.version -eq $installedVersion -and
                        [int]$confirmation.process_id -eq $LaunchedProcess.Id -and $confirmation.exe_path -eq $AppExe -and -not $LaunchedProcess.HasExited
                } catch { $confirmed = $false }
                if ($confirmed) { break }
            }
            if ($LaunchedProcess.HasExited) { break }
            Start-Sleep -Milliseconds 500
        }
        if (-not $confirmed) { throw 'The new application did not confirm a successful startup.' }
        New-Item -ItemType File -Path (Join-Path $InstallDir 'update-success.flag') -Force | Out-Null
        Remove-Item -LiteralPath $MarkerPath -Force -ErrorAction SilentlyContinue
    }
    Write-Host "Installation confirmed: $installedVersion"
    exit 0
} catch {
    Write-Host ('Installation failed: ' + $_.Exception.Message)
    if ($ChangedFiles.Count -gt 0) {
        try {
            Restore-ChangedFiles
            New-Item -ItemType File -Path (Join-Path $InstallDir 'update-failed.flag') -Force | Out-Null
            Start-Application
            Write-Host 'Previous files restored successfully.'
        } catch { Write-Host ('Rollback failed; recovery files retained in ' + $TransactionDir + ': ' + $_.Exception.Message) }
    } elseif ($OwnMutex -and (Test-Path -LiteralPath $AppExe -PathType Leaf)) {
        try {
            if ((Get-ThisInstallationProcesses).Count -eq 0 -and
                ($ParentPid -le 0 -or -not (Get-Process -Id $ParentPid -ErrorAction SilentlyContinue))) {
                New-Item -ItemType File -Path (Join-Path $InstallDir 'update-failed.flag') -Force | Out-Null
                Start-Application
                Write-Host 'The previous application was reopened without replacing files.'
            }
        } catch { Write-Host ('Could not reopen the previous application: ' + $_.Exception.Message) }
    }
    exit 2
} finally {
    # Keep recovery files after an incomplete rollback. Never delete the install
    # directory or unrelated user files as part of recovery.
    if ($ChangedFiles.Count -eq 0 -or $RolledBack -or ($RestoreOnly -and $LaunchedProcess) -or $confirmed) {
        try {
            $resolved = Assert-InstallPath $TransactionDir
            if ([IO.Path]::GetFileName($resolved).StartsWith('.pdfpro-update-') -and (Test-Path -LiteralPath $resolved)) {
                Remove-Item -LiteralPath $resolved -Recurse -Force
            }
        } catch { Write-Host ('Temporary files retained: ' + $_.Exception.Message) }
    }
    if ($OwnMutex) { $InstallerMutex.ReleaseMutex() }
    $InstallerMutex.Dispose()
    Stop-Transcript -ErrorAction SilentlyContinue | Out-Null
}
