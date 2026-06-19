using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;

using PdfViewerApp.UpdateClient;

namespace PdfViewerApp;

public partial class UpdateDialog : Window, IComponentConnector
{
	private readonly string _downloadUrl;
	private readonly string _latestVersion;
	private readonly UpdateCheckResponse _updateResponse;
	private readonly AppUpdateService _updateService;

	private static readonly HttpClient HttpClient = new HttpClient();

	public UpdateDialog(UpdateCheckResponse updateResponse, AppUpdateService updateService)
	{
		InitializeComponent();
		_updateResponse = updateResponse;
		_updateService = updateService;
		_latestVersion = updateResponse.LatestVersion;
		_downloadUrl = updateResponse.DownloadUrl;
		VersionInfoTextBlock.Text = $"Phiên bản mới v{_latestVersion} đã sẵn sàng để tải về (Bản hiện tại: v{ActivationLicense.AppVersion}).";
		ChangelogTextBox.Text = string.IsNullOrEmpty(updateResponse.Changelog)
			? "Không có thông tin chi tiết về bản cập nhật này."
			: updateResponse.Changelog;
	}

	private void Later_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private async void Update_Click(object sender, RoutedEventArgs e)
	{
		UpdateButton.IsEnabled = false;
		LaterButton.IsEnabled = false;
		ProgressPanel.Visibility = Visibility.Visible;
		try
		{
			StatusTextBlock.Text = "Đang kiểm tra liên kết tải xuống...";
			string url = await ResolveGoogleDriveUrlAsync(_downloadUrl);

			var resolvedResponse = new UpdateCheckResponse
			{
				Success = _updateResponse.Success,
				LatestVersion = _updateResponse.LatestVersion,
				DownloadUrl = url,
				Sha256 = _updateResponse.Sha256,
				FileSize = _updateResponse.FileSize,
				ReleaseDate = _updateResponse.ReleaseDate,
				Mandatory = _updateResponse.Mandatory,
				Changelog = _updateResponse.Changelog
			};

			StatusTextBlock.Text = "Đang tải xuống bản cập nhật...";

			var progressReporter = new Progress<double>(percent =>
			{
				Dispatcher.Invoke(() =>
				{
					double progressPercentage = percent * 100.0;
					DownloadProgressBar.Value = progressPercentage;
					PercentTextBlock.Text = $"{Math.Round(progressPercentage)}%";
					if (_updateResponse.FileSize > 0)
					{
						long currentRead = (long)(percent * _updateResponse.FileSize);
						StatusTextBlock.Text = "Đang tải xuống: " + FormatBytes(currentRead) + " / " + FormatBytes(_updateResponse.FileSize);
					}
					else
					{
						StatusTextBlock.Text = "Đang tải xuống...";
					}
				});
			});

			var result = await _updateService.DownloadAndVerifyAsync(resolvedResponse, progressReporter);

			byte[] header = new byte[2];
			using (FileStream fileStream = new FileStream(result.FilePath, FileMode.Open, FileAccess.Read))
			{
				if (fileStream.Length >= 2)
				{
					fileStream.Read(header, 0, 2);
				}
			}

			bool isZip = header[0] == 80 && header[1] == 75; // PK
			bool isExe = header[0] == 77 && header[1] == 90; // MZ
			if (!isZip && !isExe)
			{
				throw new Exception("Định dạng tệp tải về không hợp lệ (không phải ZIP hoặc EXE).");
			}

			if (isZip)
			{
				string baseDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
				var rollbackState = AppUpdateService.CreateRollbackState(
					ActivationLicense.AppVersion,
					_latestVersion,
					baseDirectory,
					result.FilePath);

				StatusTextBlock.Text = "Đang sao lưu bản hiện tại để có thể rollback...";
				var backupProgress = new Progress<double>(percent =>
				{
					Dispatcher.Invoke(() =>
					{
						double progressPercentage = percent * 100.0;
						DownloadProgressBar.Value = progressPercentage;
						PercentTextBlock.Text = "BKP " + Math.Round(progressPercentage) + "%";
					});
				});

				await _updateService.CreateInstallationBackupAsync(baseDirectory, rollbackState.BackupZipPath, backupProgress);
				AppUpdateService.SaveRollbackState(rollbackState);

				string scriptPath = Path.Combine(Path.GetDirectoryName(rollbackState.StateFilePath) ?? Path.GetTempPath(), "apply-update.ps1");
				File.WriteAllText(scriptPath, BuildRollbackScript(), Encoding.UTF8);

				StatusTextBlock.Text = "Đang khởi chạy bộ cài có rollback...";
				await Task.Delay(500);
				Process.Start(new ProcessStartInfo
				{
					FileName = "powershell.exe",
					Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\" -InstallDir \"{rollbackState.InstallDirectory}\" -BackupZip \"{rollbackState.BackupZipPath}\" -UpdateZip \"{rollbackState.DownloadZipPath}\" -MarkerPath \"{rollbackState.ConfirmationMarkerPath}\" -AppExe \"{rollbackState.AppExecutablePath}\" -TargetVersion \"{_latestVersion}\" -ParentPid {Environment.ProcessId} -TimeoutSeconds 120",
					UseShellExecute = false,
					CreateNoWindow = true,
					WindowStyle = ProcessWindowStyle.Hidden
				});
				Application.Current.Shutdown();
			}
			else
			{
				string targetExe = Path.Combine(Path.GetTempPath(), "PdfProSetup_v" + _latestVersion + ".exe");
				if (File.Exists(targetExe))
				{
					File.Delete(targetExe);
				}

				File.Copy(result.FilePath, targetExe, true);
				StatusTextBlock.Text = "Tải xuống hoàn tất! Đang khởi động trình cài đặt...";
				await Task.Delay(1000);
				Process.Start(new ProcessStartInfo
				{
					FileName = targetExe,
					UseShellExecute = true
				});
				Application.Current.Shutdown();
			}
		}
		catch (Exception ex)
		{
			App.SendCrashTelemetry(ex);
			MessageBox.Show(this, "Lỗi khi tải hoặc cài đặt bản cập nhật: " + ex.Message, "Cập nhật Thất Bại", MessageBoxButton.OK, MessageBoxImage.Hand);
			UpdateButton.IsEnabled = true;
			LaterButton.IsEnabled = true;
			ProgressPanel.Visibility = Visibility.Collapsed;
		}
	}

	private async Task<string> ResolveGoogleDriveUrlAsync(string url)
	{
		if (!url.Contains("drive.google.com"))
		{
			return url;
		}

		if (url.Contains("/file/d/"))
		{
			Match match = Regex.Match(url, "drive\\.google\\.com/file/d/([^/?#]+)");
			if (match.Success)
			{
				string value = match.Groups[1].Value;
				url = "https://drive.google.com/uc?export=download&id=" + value;
			}
		}

		try
		{
			using HttpResponseMessage response = await HttpClient.GetAsync(url);
			if (response.IsSuccessStatusCode)
			{
				string mediaType = response.Content.Headers.ContentType?.MediaType;
				if (mediaType != null && mediaType.Contains("html"))
				{
					string html = await response.Content.ReadAsStringAsync();
					if (html.Contains("id=\"download-form\""))
					{
						Match formMatch = Regex.Match(html, "<form\\s+[^>]*id=\"download-form\"\\s+[^>]*action=\"([^\"]+)\"");
						if (formMatch.Success)
						{
							string action = formMatch.Groups[1].Value;
							if (action.StartsWith("/"))
							{
								action = "https://drive.google.com" + action;
							}

							MatchCollection inputMatches = Regex.Matches(html, "<input\\s+[^>]*type=\"hidden\"\\s+[^>]*name=\"([^\"]+)\"\\s+[^>]*value=\"([^\"]+)\"");
							List<string> inputs = new List<string>();
							foreach (Match item in inputMatches)
							{
								string name = item.Groups[1].Value;
								string value = item.Groups[2].Value;
								inputs.Add(name + "=" + Uri.EscapeDataString(value));
							}

							if (inputs.Count > 0)
							{
								string separator = action.Contains("?") ? "&" : "?";
								return action + separator + string.Join("&", inputs);
							}
						}
					}

					if (html.Contains("Google Drive - Access Denied") || html.Contains("sign in") || html.Contains("request access"))
					{
						throw new Exception("Không thể truy cập Google Drive. Vui lòng kiểm tra quyền chia sẻ của tệp.");
					}
				}
			}
		}
		catch (Exception ex) when (!ex.Message.Contains("Không thể truy cập Google Drive"))
		{
		}

		return url;
	}

	private static string FormatBytes(long bytes)
	{
		string[] units = new string[4] { "B", "KB", "MB", "GB" };
		double value = bytes;
		int unitIndex = 0;
		while (value >= 1024.0 && unitIndex < units.Length - 1)
		{
			value /= 1024.0;
			unitIndex++;
		}

		return $"{value:0.##} {units[unitIndex]}";
	}

	private static string BuildRollbackScript()
	{
		return @"param(
    [Parameter(Mandatory = $true)]
    [string]$InstallDir,
    [Parameter(Mandatory = $true)]
    [string]$BackupZip,
    [Parameter(Mandatory = $true)]
    [string]$UpdateZip,
    [Parameter(Mandatory = $true)]
    [string]$MarkerPath,
    [Parameter(Mandatory = $true)]
    [string]$AppExe,
    [Parameter(Mandatory = $true)]
    [string]$TargetVersion,
    [int]$ParentPid = 0,
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'

$AttemptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$LogPath = Join-Path $AttemptDir ""update-powershell.log""
Start-Transcript -Path $LogPath -Append -ErrorAction SilentlyContinue

function Remove-Tree([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        Get-ChildItem -LiteralPath $Path -Force -ErrorAction SilentlyContinue | ForEach-Object {
            if ($_.PSIsContainer) {
                Remove-Tree $_.FullName
            }
            else {
                try { Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue } catch {}
            }
        }
        try { Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue } catch {}
    }
}

function Get-AppFileVersion([string]$Path) {
    try {
        if (Test-Path -LiteralPath $Path) {
            return ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($Path)).FileVersion
        }
    } catch {}
    return $null
}

function Normalize-Version([string]$Value) {
    try {
        $parsed = [version](($Value -replace '^v', '').Trim())
        return ""$($parsed.Major).$($parsed.Minor).$($parsed.Build)""
    } catch {
        return $null
    }
}

function Restore-PreviousInstall {
    Write-Host ""Restoring previous installation due to failure...""
    Remove-Tree $InstallDir
    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    Expand-Archive -LiteralPath $BackupZip -DestinationPath $InstallDir -Force

    try { Remove-Item -LiteralPath $MarkerPath -Force -ErrorAction SilentlyContinue } catch {}
    New-Item -ItemType File -Path (Join-Path $InstallDir ""update-failed.flag"") -Force | Out-Null
    try { Start-Process -FilePath $AppExe } catch {}
    Stop-Transcript -ErrorAction SilentlyContinue
    exit 2
}

try {
    if ($ParentPid -gt 0) {
        Write-Host ""Waiting for parent process $ParentPid to exit...""
        try {
            $parentProc = Get-Process -Id $ParentPid -ErrorAction SilentlyContinue
            if ($parentProc) {
                $parentProc.WaitForExit(5000)
                if (-not $parentProc.HasExited) {
                    Write-Host ""Forcing parent process to exit...""
                    Stop-Process -Id $ParentPid -Force -ErrorAction SilentlyContinue
                }
            }
        } catch {}
    }

    Write-Host ""Stopping other PdfViewerApp instances...""
    Get-Process -Name ""PdfViewerApp"" -ErrorAction SilentlyContinue | Where-Object { $_.Id -ne $PID } | ForEach-Object {
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 1

    if (-not (Test-Path -LiteralPath $BackupZip)) {
        throw ""Rollback backup not found: $BackupZip""
    }

    if (-not (Test-Path -LiteralPath $UpdateZip)) {
        throw ""Update package not found: $UpdateZip""
    }

    Write-Host ""Extracting update package $UpdateZip to $InstallDir...""
    Expand-Archive -LiteralPath $UpdateZip -DestinationPath $InstallDir -Force

    $installedVersion = Get-AppFileVersion $AppExe
    if (-not $installedVersion) {
        throw ""Could not read installed app version after update.""
    }

    $normalizedInstalled = Normalize-Version $installedVersion
    $normalizedTarget = Normalize-Version $TargetVersion
    if ($normalizedInstalled -ne $normalizedTarget) {
        throw ""Version mismatch after update. Installed=$installedVersion Target=$TargetVersion""
    }

    Write-Host ""Starting updated executable $AppExe...""
    $proc = Start-Process -FilePath $AppExe -PassThru
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $confirmed = $false
    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $MarkerPath) {
            $confirmed = $true
            break
        }
        if ($proc.HasExited) {
            break
        }
        Start-Sleep -Milliseconds 500
    }

    if ($confirmed) {
        Write-Host ""Update confirmed successfully!""
        try { Remove-Item -LiteralPath $MarkerPath -Force -ErrorAction SilentlyContinue } catch {}
        New-Item -ItemType File -Path (Join-Path $InstallDir ""update-success.flag"") -Force | Out-Null
        Stop-Transcript -ErrorAction SilentlyContinue
        exit 0
    } else {
        throw ""Update confirmation timeout or process exited.""
    }
}
catch {
    Write-Host ""Error encountered: $_""
    Restore-PreviousInstall
}
";
	}
}
