using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Resources;
using System.Windows.Threading;
using ControlzEx.Theming;
using Microsoft.Win32;
using PdfViewerApp.Ai;
using PdfViewerApp.UpdateClient;
using System.Net.Http;

namespace PdfViewerApp;

public partial class MainWindow : Window, IComponentConnector
{
	private const string StartupMergeMutexName = "Local\\PdfPro.MergeStartupOwner";

	private bool _updatingStatusControls;

	private readonly DispatcherTimer _zoomSliderTimer = new DispatcherTimer();

	private double? _pendingZoomSliderPercent;

	private AiSettings _aiSettings = AiSettings.Load();

	private AiSnapshotRouter _aiSnapshotRouter;

	private bool _isDarkMode = true;

	private bool _startupArgsProcessed;

	private readonly AppPreferences _appPreferences = AppPreferences.Load();

	public static Mutex? _explorerMergeMutex;

	private WelcomeDashboard? _welcomeDashboard;

	private MainRibbon? _mainRibbon;

	private AiPanelControl? _aiPanelControl;

	private string? _lastCapturedSnapshotBase64;
	private int _lastCapturedSnapshotPageNumber;

	// Bản cập nhật đã tải ngầm, sẵn sàng cài khi đóng app
	private volatile bool _silentUpdateApplying = false;

	private string _activeTool = "Select";

	public string ActiveFontFamily { get; set; } = "Segoe UI";

	public double ActiveFontSize { get; set; } = 14.0;

	public bool ActiveIsBold { get; set; }

	public bool ActiveIsItalic { get; set; }

	public bool ActiveIsUnderline { get; set; }

	public Color ActiveStrokeColor { get; set; } = Colors.Red;

	public Color ActiveBgColor { get; set; } = Colors.Transparent;

	public double ActiveOpacity { get; set; } = 1.0;

	public bool ActiveIsStrikeout { get; set; }

	public bool ActiveIsSubscript { get; set; }

	public bool ActiveIsSuperscript { get; set; }

	public TextAlignment ActiveTextAlignment { get; set; } = TextAlignment.Left;

	public string ActiveTool
	{
		get
		{
			return _activeTool;
		}
		set
		{
			_activeTool = value;
			UpdateToolButtonStates();
		}
	}



	public MainWindow()
	{
		InitializeComponent();
		_aiSnapshotRouter = new AiSnapshotRouter(_aiSettings);
		EnsureWelcomeDashboardHost();
		EnsureAiPanelHost();
		EnsureMainRibbonHost();
		UpdateToolButtonStates();
		SetTheme(_appPreferences.ThemeName);
		ApplyAppActivationState();
		ApplyAiSettingsToUi();
		TryApplyAppLogo();
		HookDashboardEvents();
		base.Loaded += delegate
		{
			// Check license heartbeat check online ngầm
			Task.Run(async delegate
			{
				try
				{
					await ActivationLicense.CheckHeartbeatOnlineAsync(force: true);
					await base.Dispatcher.InvokeAsync(delegate
					{
						ApplyAppActivationState();
					});
				}
				catch {}
			});

			HandleStartupPdfArguments();
			UpdateTabEmptyState();
			LocalAiInstaller.StartInitializeBackground();
			RefreshRecentFilesDashboard();
			base.Dispatcher.InvokeAsync((Func<Task>)async delegate
			{
				string successFlag = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update-success.flag");
				string failedFlag = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update-failed.flag");

				if (File.Exists(successFlag))
				{
					try { File.Delete(successFlag); } catch {}
					MessageBox.Show(this, $"Cập nhật thành công ứng dụng PDF Pro lên phiên bản mới v{ActivationLicense.AppVersion}!", "Cập Nhật Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
				}
				else if (File.Exists(failedFlag))
				{
					try { File.Delete(failedFlag); } catch {}
					MessageBox.Show(this, "Quá trình cập nhật gặp sự cố nên hệ thống đã tự động khôi phục (rollback) về phiên bản hoạt động trước đó để đảm bảo ổn định.", "Cập Nhật Thất Bại", MessageBoxButton.OK, MessageBoxImage.Warning);
				}

				await AppUpdateService.TryConfirmPendingLaunchAsync();
				await RunUpdateCheckAsync();
			}, DispatcherPriority.ApplicationIdle);
		};
		LogStatus("Sẵn sàng");
		base.PreviewKeyDown += MainWindow_PreviewKeyDown;
		_zoomSliderTimer.Interval = TimeSpan.FromMilliseconds(90.0);
		_zoomSliderTimer.Tick += delegate
		{
			_zoomSliderTimer.Stop();
			if (_pendingZoomSliderPercent.HasValue)
			{
				GetActiveTab()?.SetZoomPercent(_pendingZoomSliderPercent.Value);
				_pendingZoomSliderPercent = null;
			}
		};
		base.StateChanged += MainWindow_StateChanged;

		// Tự động kiểm tra mạng và kích hoạt lại khi có mạng trở lại
		try
		{
			System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged += async (s, ev) =>
			{
				if (ev.IsAvailable)
				{
					// Đợi 2 giây để kết nối mạng ổn định hoàn toàn (tránh DHCP chưa gán IP)
					await Task.Delay(2000);
					await ActivationLicense.TriggerNetworkCheckAsync();
					if (ActivationLicense.IsInternetAvailable)
					{
						await ActivationLicense.CheckHeartbeatOnlineAsync(force: true);
						await base.Dispatcher.InvokeAsync(delegate
						{
							ApplyAppActivationState();
							LogStatus("Đã kết nối lại máy chủ bản quyền trực tuyến.");
						});
					}
				}
			};
		}
		catch {}
	}

	private async Task RunUpdateCheckAsync()
	{
		if (_aiSettings.EnableUpdateCheck)
		{
			try
			{
				var httpClient = HttpHelper.Client;
				var updateService = new AppUpdateService(httpClient, ActivationLicense.ApiUpdateUrl);
				var result = await updateService.CheckAsync();
				if (result.HasUpdate && !string.IsNullOrEmpty(result.Response.DownloadUrl))
				{
					if (_aiSettings.EnableSilentUpdate)
					{
						// Kiểm tra xem đã có bản sẵn sàng chưa (tránh tải lại)
						var existingSilent = AppUpdateService.LoadSilentUpdateState();
						if (existingSilent != null && existingSilent.TargetVersion == result.Response.LatestVersion)
						{
							// Đã tải rồi, chỉ thông báo nhẹ
							Dispatcher.Invoke(() => LogStatus(
								$"⬆ Bản cập nhật v{result.Response.LatestVersion} đã tải xong — sẽ cài khi bạn đóng ứng dụng."));
						}
						else
						{
							// Tải ngầm trong background
							Dispatcher.Invoke(() => LogStatus(
								$"⬇ Đang tải ngầm bản cập nhật v{result.Response.LatestVersion}..."));
							_ = Task.Run(async () =>
							{
								try
								{
									string resolvedUrl = result.Response.DownloadUrl;
									if (resolvedUrl.Contains("drive.google.com"))
										resolvedUrl = await ResolveGoogleDriveUrlBackgroundAsync(resolvedUrl);

									var backgroundResponse = new UpdateCheckResponse
									{
										Success = result.Response.Success,
										LatestVersion = result.Response.LatestVersion,
										DownloadUrl = resolvedUrl,
										Sha256 = result.Response.Sha256,
										FileSize = result.Response.FileSize,
										ReleaseDate = result.Response.ReleaseDate,
										Mandatory = result.Response.Mandatory,
										Changelog = result.Response.Changelog
									};

									var downloadResult = await updateService.DownloadAndVerifyAsync(backgroundResponse);

									// Lưu trạng thái để dùng khi đóng app
									AppUpdateService.SaveSilentUpdateState(new SilentUpdateReadyState
									{
										TargetVersion = result.Response.LatestVersion,
										DownloadZipPath = downloadResult.FilePath,
										Sha256 = downloadResult.Sha256,
										DownloadUrl = resolvedUrl,
										Changelog = result.Response.Changelog,
										DownloadedUtc = DateTimeOffset.UtcNow.ToString("O")
									});

									// Thông báo nhỏ trên status bar (không popup)
									Dispatcher.Invoke(() => LogStatus(
										$"✅ Đã tải xong bản cập nhật v{result.Response.LatestVersion} — sẽ cài tự động khi bạn đóng ứng dụng."));
								}
								catch
								{
									// Tải thất bại: fallback hiện dialog thủ công
									Dispatcher.Invoke(() =>
									{
										LogStatus($"Phát hiện bản mới v{result.Response.LatestVersion}. Nhấn Kiểm tra cập nhật để tải.");
									});
								}
							});
						}
					}
					else
					{
						UpdateDialog updateDialog = new UpdateDialog(result.Response, updateService);
						updateDialog.Owner = this;
						updateDialog.ShowDialog();
					}
				}
			}
			catch
			{
				// Tránh crash ứng dụng nếu lỗi mạng khi khởi động
			}
		}
	}

	private async Task<string> ResolveGoogleDriveUrlBackgroundAsync(string url)
	{
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
			var client = HttpHelper.Client;
			using HttpResponseMessage response = await client.GetAsync(url);
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
								string val = item.Groups[2].Value;
								inputs.Add(name + "=" + Uri.EscapeDataString(val));
							}
							if (inputs.Count > 0)
							{
								string separator = action.Contains("?") ? "&" : "?";
								return action + separator + string.Join("&", inputs);
							}
						}
					}
				}
			}
		}
		catch { }
		return url;
	}

	private void HideUpdateNotification_Click(object sender, RoutedEventArgs e)
	{
		UpdateNotificationBanner.Visibility = Visibility.Collapsed;
	}

	private void UpdateNotificationAction_Click(object sender, RoutedEventArgs e)
	{
		UpdateNotificationBanner.Visibility = Visibility.Collapsed;
		ManualUpdateCheck_Click(this, new RoutedEventArgs());
	}

	private async void ManualUpdateCheck_Click(object sender, RoutedEventArgs e)
	{
		LogStatus("Đang kiểm tra cập nhật...");
		try
		{
			var httpClient = HttpHelper.Client;
			var updateService = new AppUpdateService(httpClient, ActivationLicense.ApiUpdateUrl);
			var result = await updateService.CheckAsync();
			if (result.HasUpdate)
			{
				if (string.IsNullOrEmpty(result.Response.DownloadUrl))
				{
					MessageBox.Show(this, "Phát hiện phiên bản mới v" + result.LatestVersion + " trên máy chủ, nhưng quản trị viên chưa cấu hình \"Link tải bản cập nhật (Download URL)\" trong trang quản trị WordPress.", "Kiểm tra cập nhật", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				}
				else
				{
					UpdateDialog updateDialog = new UpdateDialog(result.Response, updateService);
					updateDialog.Owner = this;
					updateDialog.ShowDialog();
				}
			}
			else
			{
				MessageBox.Show(this, "Ứng dụng của bạn đã là phiên bản mới nhất (v" + result.CurrentVersion + ").", "Kiểm tra cập nhật", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
		}
		catch (Exception ex)
		{
			App.SendCrashTelemetry(ex);
			MessageBox.Show(this, "Lỗi khi kiểm tra cập nhật: " + ex.Message, "Kiểm tra cập nhật", MessageBoxButton.OK, MessageBoxImage.Error);
		}
		LogStatus("Sẵn sàng");
	}



	private Fluent.RibbonGroupBox? FindRibbonGroupBoxByHeader(string header)
	{
		foreach (var group in FindVisualChildren<Fluent.RibbonGroupBox>(this))
		{
			if (string.Equals(group.Header?.ToString(), header, StringComparison.Ordinal))
			{
				return group;
			}
		}

		return null;
	}

	private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
	{
		int count = VisualTreeHelper.GetChildrenCount(parent);
		for (int i = 0; i < count; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			if (child is T match)
			{
				yield return match;
			}

			foreach (T descendant in FindVisualChildren<T>(child))
			{
				yield return descendant;
			}
		}
	}

	private async void RestorePreviousVersion_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			UpdateRollbackState? state = AppUpdateService.LoadRollbackState();
			if (state == null || string.IsNullOrWhiteSpace(state.BackupZipPath) || !File.Exists(state.BackupZipPath))
			{
				MessageBox.Show(this, "Chưa có bản backup rollback hợp lệ để khôi phục.", "Khôi phục bản trước", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			string currentVersion = string.IsNullOrWhiteSpace(state.TargetVersion) ? "unknown" : state.TargetVersion;
			string previousVersion = string.IsNullOrWhiteSpace(state.CurrentVersion) ? "unknown" : state.CurrentVersion;
			string confirmMessage = $"Khôi phục từ v{currentVersion} về v{previousVersion}?\n\nỨng dụng hiện tại sẽ đóng trước khi quá trình restore bắt đầu.";
			if (MessageBox.Show(this, confirmMessage, "Khôi phục bản trước", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
			{
				return;
			}

			string scriptPath = Path.Combine(Path.GetTempPath(), "pdfpro_restore_previous.ps1");
			File.WriteAllText(scriptPath, BuildRestoreScript(), Encoding.UTF8);

			Process.Start(new ProcessStartInfo
			{
				FileName = "powershell.exe",
				Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\" -InstallDir \"{state.InstallDirectory}\" -BackupZip \"{state.BackupZipPath}\" -AppExe \"{state.AppExecutablePath}\" -CurrentPid {Environment.ProcessId} -TimeoutSeconds 120",
				UseShellExecute = false,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden
			});

			Application.Current.Shutdown();
		}
		catch (Exception ex)
		{
			App.SendCrashTelemetry(ex);
			MessageBox.Show(this, "Không thể khôi phục bản trước: " + ex.Message, "Khôi phục bản trước", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private static string BuildRestoreScript()
	{
		return @"param(
    [Parameter(Mandatory = $true)]
    [string]$InstallDir,
    [Parameter(Mandatory = $true)]
    [string]$BackupZip,
    [Parameter(Mandatory = $true)]
    [string]$AppExe,
    [Parameter(Mandatory = $true)]
    [int]$CurrentPid,
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'

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

if (-not (Test-Path -LiteralPath $BackupZip)) {
    throw ""Rollback backup not found: $BackupZip""
}

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while ((Get-Date) -lt $deadline) {
    try {
        Get-Process -Id $CurrentPid -ErrorAction Stop | Out-Null
        Start-Sleep -Milliseconds 500
    }
    catch {
        break
    }
}

Remove-Tree $InstallDir
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Expand-Archive -LiteralPath $BackupZip -DestinationPath $InstallDir -Force

try { Start-Process -FilePath $AppExe } catch {}
exit 0
";
	}

	private void MainWindow_StateChanged(object? sender, EventArgs e)
	{
		if (BtnMaximize != null && BtnMaximize.Template.FindName("MaxIcon", BtnMaximize) is TextBlock textBlock)
		{
			textBlock.Text = ((base.WindowState == WindowState.Maximized) ? "\ue923" : "\ue922");
		}
	}

	private void ThemeToggle_Click(object sender, RoutedEventArgs e)
	{
		var allThemes = AppThemeRegistry.All;
		int currentIndex = 0;
		for (int i = 0; i < allThemes.Count; i++)
		{
			if (string.Equals(allThemes[i].Name, _appPreferences.ThemeName, StringComparison.OrdinalIgnoreCase))
			{
				currentIndex = i;
				break;
			}
		}
		int nextIndex = (currentIndex + 1) % allThemes.Count;
		string next = allThemes[nextIndex].Name;
		SetTheme(next);
	}

	/// <summary>Áp dụng theme theo tên (Dark, Light, Midnight, Forest, Sunset, Ocean).</summary>
	private void SetTheme(string themeName)
	{
		var theme = AppThemeRegistry.Get(themeName);
		bool isDark = !theme.IsLight;
		_isDarkMode = isDark;
		_appPreferences.ThemeName = theme.Name;
		_appPreferences.Save();
		base.Tag = isDark;
		try
		{
			ThemeManager.Current.ChangeTheme(Application.Current, theme.FluentTheme);
		}
		catch
		{
		}
		if (MainRootGrid != null)
		{
			var bgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.WindowBackground));
			base.Background = bgBrush;
			MainRootGrid.Background = bgBrush;
		}
		if (TitleBarGrid != null)
		{
			TitleBarGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.TitleBarBackground));
		}
		if (RibbonHostContainer != null)
		{
			RibbonHostContainer.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.TitleBarBackground));
		}
		if (TitleBarText != null)
		{
			TitleBarText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.ForegroundPrimary));
		}
		if (TitleBarSubtitle != null)
		{
			TitleBarSubtitle.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.ForegroundSecondary));
		}
		if (TabEmptyState != null)
		{
			TabEmptyState.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.PanelBackground));
			TabEmptyState.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.BorderColor));
		}
		if (AppStatusBar != null)
		{
			var statusGrad = new LinearGradientBrush
			{
				StartPoint = new Point(0.0, 0.0),
				EndPoint = new Point(1.0, 0.0)
			};
			statusGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(theme.StatusBarStart), 0.0));
			statusGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(theme.StatusBarMid), 0.4));
			statusGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(theme.AccentDark), 1.0));
			AppStatusBar.Background = statusGrad;
			AppStatusBar.Foreground = isDark ? Brushes.White : Brushes.Black;
		}
		var fgBrush = isDark ? Brushes.White : Brushes.Black;
		if (StatusMessage != null) StatusMessage.Foreground = fgBrush;
		if (PageTotalText != null) PageTotalText.Foreground = fgBrush;
		if (ZoomIndicator != null) ZoomIndicator.Foreground = fgBrush;
		if (PdfTabControl != null)
		{
			foreach (TabItem item in (IEnumerable)PdfTabControl.Items)
			{
				if (item.Content is PdfDocumentTab pdfDocumentTab)
				{
					pdfDocumentTab.ApplyTheme(theme);
				}
			}
		}
		_welcomeDashboard?.ApplyTheme(theme);
		_mainRibbon?.ApplyTheme(theme);
		_aiPanelControl?.ApplyTheme(theme);
	}

	/// <summary>Overload tương thích ngược khi chỉ có bool.</summary>
	private void SetTheme(bool isDark) => SetTheme(AppThemeRegistry.FromLegacyBool(isDark));

	private void Minimize_Click(object sender, RoutedEventArgs e)
	{
		base.WindowState = WindowState.Minimized;
	}

	private void Maximize_Click(object sender, RoutedEventArgs e)
	{
		base.WindowState = ((base.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
	}

	private void Close_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private void BtnKeepToolsActive_Click(object sender, RoutedEventArgs e)
	{
		if (_mainRibbon != null && BtnKeepToolsActive != null)
		{
			_mainRibbon.KeepToolsActive = BtnKeepToolsActive.IsChecked == true;
		}
	}

	private void BtnToggleRibbon_Click(object sender, RoutedEventArgs e)
	{
		if (_mainRibbon?.MyRibbon != null)
		{
			_mainRibbon.MyRibbon.IsMinimized = BtnToggleRibbon.IsChecked == true;
		}
	}

	private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.K)
		{
			e.Handled = true;
			OpenCommandPalette();
			return;
		}

		if (IsTextInputFocused())
		{
			return;
		}
		if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None && ActiveTool != "Select")
		{
			ActiveTool = "Select";
			PdfDocumentTab activeTab = GetActiveTab();
			if (activeTab != null)
			{
				activeTab.ActiveTool = "Select";
			}
			LogStatus("Đã hủy chế độ vẽ/chú thích");
			e.Handled = true;
		}
		else if (e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None)
		{
			GetActiveTab()?.HandleDeleteKey();
			e.Handled = true;
		}
		else if (Keyboard.Modifiers == (ModifierKeys.Alt | ModifierKeys.Control) && e.Key == Key.V)
		{
			e.Handled = true;
			GetActiveTab()?.PasteAnnotation(inPlace: true);
		}
		else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && (e.Key == Key.OemPlus || e.Key == Key.Add))
		{
			e.Handled = true;
			GetActiveTab()?.RotateSelectedPageAsync(90);
		}
		else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Z)
		{
			e.Handled = true;
			GetActiveTab()?.Redo();
		}
		else if (Keyboard.Modifiers == ModifierKeys.Control)
		{
			switch (e.Key)
			{
			case Key.Z:
				e.Handled = true;
				GetActiveTab()?.Undo();
				break;
			case Key.Y:
				e.Handled = true;
				GetActiveTab()?.Redo();
				break;
			case Key.O:
				e.Handled = true;
				OpenPdf_Click(this, new RoutedEventArgs());
				break;
			case Key.P:
				e.Handled = true;
				PrintPdf_Click(this, new RoutedEventArgs());
				break;
			case Key.C:
			{
				e.Handled = true;
				PdfDocumentTab activeTab3 = GetActiveTab();
				if (activeTab3 != null)
				{
					if (activeTab3.ActiveTool == "SelectText")
					{
						activeTab3.CopySelectedText();
					}
					else
					{
						activeTab3.CopySelectedAnnotation();
					}
				}
				break;
			}
			case Key.V:
				e.Handled = true;
				GetActiveTab()?.PasteAnnotation(inPlace: false);
				break;
			case Key.H:
				e.Handled = true;
				GetActiveTab()?.ContextReadMode_Click(this, new RoutedEventArgs());
				break;
			case Key.R:
				e.Handled = true;
				GetActiveTab()?.ContextRulers_Click(this, new RoutedEventArgs());
				break;
			case Key.W:
				e.Handled = true;
				if (PdfTabControl.SelectedItem is TabItem removeItem)
				{
					PdfTabControl.Items.Remove(removeItem);
					if (PdfTabControl.Items.Count == 0)
					{
						LogStatus("Sẵn sàng");
						UpdatePageIndicator();
						UpdateZoomText();
					}
				}
				break;
			case Key.Add:
			case Key.OemPlus:
				e.Handled = true;
				ZoomIn_Click(this, new RoutedEventArgs());
				break;
			case Key.Subtract:
			case Key.OemMinus:
				e.Handled = true;
				ZoomOut_Click(this, new RoutedEventArgs());
				break;
			case Key.D0:
			case Key.NumPad0:
				e.Handled = true;
				FitWidth_Click(this, new RoutedEventArgs());
				break;
			case Key.D1:
			case Key.NumPad1:
				e.Handled = true;
				GetActiveTab()?.SetZoomPercent(100.0);
				break;
			case Key.D2:
			case Key.NumPad2:
				e.Handled = true;
				GetActiveTab()?.FitWidth();
				break;
			case Key.B:
				e.Handled = true;
				ToggleSidebar_Click(this, new RoutedEventArgs());
				break;
			case Key.Home:
				e.Handled = true;
				GetActiveTab()?.GoToPage(1);
				break;
			case Key.End:
			{
				e.Handled = true;
				PdfDocumentTab activeTab2 = GetActiveTab();
				activeTab2?.GoToPage(activeTab2.PageCount);
				break;
			}
			}
		}
		else
		{
			switch (e.Key)
			{
			case Key.F11:
				e.Handled = true;
				GetActiveTab()?.ContextFullScreen_Click(this, new RoutedEventArgs());
				break;
			case Key.F4:
				e.Handled = true;
				ToggleSidebar_Click(this, new RoutedEventArgs());
				break;
			case Key.Escape:
			case Key.V:
				e.Handled = true;
				SelectTool_Click(this, new RoutedEventArgs());
				break;
			case Key.T:
				e.Handled = true;
				TextBoxTool_Click(this, new RoutedEventArgs());
				break;
			case Key.C:
				e.Handled = true;
				CalloutTool_Click(this, new RoutedEventArgs());
				break;
			case Key.S:
				e.Handled = true;
				SnapshotTool_Click(this, new RoutedEventArgs());
				break;
			case Key.A:
				e.Handled = true;
				AiSnapshotTool_Click(this, new RoutedEventArgs());
				break;
			case Key.Next:
				e.Handled = true;
				GoRelativePage(1);
				break;
			case Key.Prior:
				e.Handled = true;
				GoRelativePage(-1);
				break;
			case Key.Home:
				e.Handled = true;
				GetActiveTab()?.GoToPage(1);
				break;
			case Key.End:
			{
				e.Handled = true;
				PdfDocumentTab activeTab4 = GetActiveTab();
				activeTab4?.GoToPage(activeTab4.PageCount);
				break;
			}
			}
		}
	}

	private static bool IsTextInputFocused()
	{
		for (DependencyObject dependencyObject = Keyboard.FocusedElement as DependencyObject; dependencyObject != null; dependencyObject = VisualTreeHelper.GetParent(dependencyObject))
		{
			if (dependencyObject is TextBox || dependencyObject is ComboBox)
			{
				return true;
			}
		}
		return false;
	}

	private void OpenCommandPalette()
	{
		QuickCommandPaletteWindow palette = new QuickCommandPaletteWindow(BuildQuickCommands())
		{
			Owner = this
		};
		palette.ShowDialog();
	}

	private List<QuickCommandItem> BuildQuickCommands()
	{
		bool HasDocument() => GetActiveTab() != null;
		return new List<QuickCommandItem>
		{
			new QuickCommandItem("Open PDF", "Open one or many PDF files.", "file open pdf import", () => OpenPdf_Click(this, new RoutedEventArgs())),
			new QuickCommandItem("Save PDF", "Save changes into the current PDF workflow.", "save write persist", () => SavePdf_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Save PDF As", "Save the active PDF into a new file.", "save as export file", () => SavePdfAs_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Print", "Print the active PDF document.", "print printer hardcopy", () => PrintPdf_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Merge PDFs", "Open the merge dialog.", "merge combine join pdf", () => MergeFiles_Click(this, new RoutedEventArgs())),
			new QuickCommandItem("Extract Pages", "Extract typed range or selected thumbnail pages.", "extract export split selected pages", () => ExtractPages_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Fit Width", "Fit the active document to the viewer width.", "zoom fit width", () => FitWidth_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Zoom In", "Increase zoom on the active document.", "zoom in plus", () => ZoomIn_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Zoom Out", "Decrease zoom on the active document.", "zoom out minus", () => ZoomOut_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Toggle Sidebar", "Show or hide thumbnails/navigation sidebar.", "sidebar thumbnails navigation panel", () => ToggleSidebar_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Read Mode", "Toggle distraction-free read mode.", "read mode focus hide", () => GetActiveTab()?.ContextReadMode_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Full Screen", "Toggle full screen viewer.", "fullscreen presentation f11", () => GetActiveTab()?.ContextFullScreen_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Rotate Left", "Rotate selected thumbnail pages left.", "rotate left selected pages", () => RotateLeft_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Rotate Right", "Rotate selected thumbnail pages right.", "rotate right selected pages", () => RotateRight_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Rotate All Left", "Rotate every page left.", "rotate all left", () => RotateLeftAll_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Rotate All Right", "Rotate every page right.", "rotate all right", () => RotateRightAll_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Move Selected Pages Up", "Move selected thumbnail pages up as a batch.", "move selected page up reorder", () => MovePageUp_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Move Selected Pages Down", "Move selected thumbnail pages down as a batch.", "move selected page down reorder", () => MovePageDown_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Reverse Page Order", "Reverse the current page order preview.", "reverse reorder pages", () => ReversePageOrder_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Reset Page Order", "Restore original page order preview.", "reset reorder pages original", () => ResetPageOrder_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Duplicate Selected Pages", "Duplicate selected pages into a new PDF.", "duplicate copy selected pages", () => DuplicatePage_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Delete Selected Pages", "Delete selected pages into a new output PDF.", "delete remove selected pages", () => DeletePage_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Insert Blank Page", "Insert a blank page near the active page.", "insert blank page", () => InsertBlankPage_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Split Current Page", "Export the current page as a PDF.", "split current page export", () => SplitCurrentPage_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Select Tool", "Return to the default select tool.", "select pointer tool", () => SelectTool_Click(this, new RoutedEventArgs())),
			new QuickCommandItem("Select Text Tool", "Select text from the PDF.", "select text copy tool", () => SelectTextTool_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Edit Text Tool", "Edit text overlays where supported.", "edit text tool", () => EditTextTool_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Text Box Tool", "Create a text box annotation.", "annotation textbox text", () => TextBoxTool_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Callout Tool", "Create an arrow callout annotation.", "annotation callout arrow", () => CalloutTool_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Ink Tool", "Draw freehand ink annotation.", "annotation ink draw pen", () => InkTool_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Rectangle Tool", "Draw rectangle annotation.", "annotation rectangle shape", () => RectTool_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Oval Tool", "Draw oval annotation.", "annotation oval ellipse shape", () => OvalTool_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Line Tool", "Draw line annotation.", "annotation line shape", () => LineTool_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Sticky Note Tool", "Add a sticky note annotation.", "annotation sticky note comment", () => StickyNoteTool_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Snapshot Tool", "Capture a region for copy, save or print.", "snapshot crop capture image", () => SnapshotTool_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("AI Snapshot", "Capture a region and send it to AI Copilot.", "ai snapshot copilot ask", () => AiSnapshotTool_Click(this, new RoutedEventArgs()), HasDocument),
			new QuickCommandItem("Check AI System", "Run the local AI readiness check.", "ai check system diagnostics", () => CheckAi_Click(this, new RoutedEventArgs())),
			new QuickCommandItem("Settings", "Open application settings.", "settings preferences options", () => Settings_Click(this, new RoutedEventArgs())),
			new QuickCommandItem("Check Updates", "Check for application updates.", "update version check", () => ManualUpdateCheck_Click(this, new RoutedEventArgs())),
			new QuickCommandItem("Performance Trace", "Open the performance trace report.", "performance trace diagnostics", () => ShowPerformanceTrace_Click(this, new RoutedEventArgs())),
			new QuickCommandItem("About", "Show app and license information.", "about info license", () => About_Click(this, new RoutedEventArgs())),
			new QuickCommandItem("Toggle Theme", "Switch light/dark theme.", "theme dark light", () => ThemeToggle_Click(this, new RoutedEventArgs())),
			new QuickCommandItem("Close Current Tab", "Close the active PDF tab.", "close tab document", CloseActiveTabFromCommandPalette, HasDocument)
		};
	}

	private void CloseActiveTabFromCommandPalette()
	{
		if (PdfTabControl.SelectedItem is TabItem removeItem)
		{
			PdfTabControl.Items.Remove(removeItem);
			UpdateTabEmptyState();
			if (PdfTabControl.Items.Count == 0)
			{
				LogStatus("Sẵn sàng");
				UpdatePageIndicator();
				UpdateZoomText();
			}
		}
	}

	private void GoRelativePage(int delta)
	{
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null && activeTab.PageCount > 0)
		{
			activeTab.GoToPage(activeTab.SelectedPageNumber + delta);
		}
	}

	private void TryApplyAppLogo()
	{
		try
		{
			if (Application.Current.TryFindResource("AppLogoImage") is ImageSource imageSource)
			{
				base.Icon = imageSource;
				return;
			}

			StreamResourceInfo resourceStream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/hphat_logo_1780279208636.png", UriKind.Absolute));
			if (resourceStream?.Stream != null)
			{
				BitmapImage bitmapImage = new BitmapImage();
				bitmapImage.BeginInit();
				bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
				bitmapImage.StreamSource = resourceStream.Stream;
				bitmapImage.EndInit();
				bitmapImage.Freeze();
				base.Icon = bitmapImage;
			}
		}
		catch
		{
		}
	}

	private PdfDocumentTab? GetActiveTab()
	{
		if (PdfTabControl != null && PdfTabControl.SelectedItem is TabItem tabItem)
		{
			return tabItem.Content as PdfDocumentTab;
		}
		return null;
	}

	public static bool SkipStartupMergeArgs { get; set; } = false;

	public async void HandleStartupPdfArguments()
	{
		if (SkipStartupMergeArgs)
		{
			return;
		}
		if (!_startupArgsProcessed)
		{
			_startupArgsProcessed = true;
			string[] source = Environment.GetCommandLineArgs().Skip(1).ToArray();
			bool flag = source.Any((string arg) => arg.Equals("--merge", StringComparison.OrdinalIgnoreCase));
			bool flag2 = source.Any((string arg) => arg.Equals("--exit-after-merge", StringComparison.OrdinalIgnoreCase));
			string[] array = FilterPdfFiles(source.Where((string arg) => !arg.Equals("--merge", StringComparison.OrdinalIgnoreCase) && !arg.Equals("--exit-after-merge", StringComparison.OrdinalIgnoreCase))).OrderBy((string path) => path, NaturalFilePathComparer.Instance).ToArray();
			if (flag || flag2)
			{
				Hide();
				await HandleExplorerMergeStartupAsync(array);
			}
			else if (array.Length == 1)
			{
				OpenPdfTab(array[0]);
			}
			else if (array.Length > 1)
			{
				await ShowMergeDialogAsync(array, autoStartMerge: true, sortByName: true, openMergedExternally: false);
			}
		}
	}

	private void OpenPdf_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Filter = "PDF documents (*.pdf)|*.pdf",
			Title = "MởFile PDF",
			Multiselect = true
		};
		if (openFileDialog.ShowDialog() == true)
		{
			string[] fileNames = openFileDialog.FileNames;
			foreach (string path in fileNames)
			{
				OpenPdfTab(path);
			}
		}
	}

	public void OpenPdfTab(string path)
	{
		foreach (TabItem item in (IEnumerable)PdfTabControl.Items)
		{
			if (item.Content is PdfDocumentTab pdfDocumentTab && pdfDocumentTab.CurrentPdfPath == path)
			{
				PdfTabControl.SelectedItem = item;
				RecentFilesService.Record(path);
				RefreshRecentFilesDashboard();
				return;
			}
		}
		PdfDocumentTab pdfDocumentTab2 = new PdfDocumentTab(path);
		pdfDocumentTab2.KeepToolsActive = (_mainRibbon != null && _mainRibbon.KeepToolsActive);
		pdfDocumentTab2.ApplyTheme(AppThemeRegistry.Get(_appPreferences.ThemeName));
		pdfDocumentTab2.StatusChanged += DocTab_StatusChanged;
		pdfDocumentTab2.ZoomChanged += DocTab_ZoomChanged;
		pdfDocumentTab2.PageChanged += DocTab_PageChanged;
		pdfDocumentTab2.DocumentReloaded += DocTab_DocumentReloaded;
		pdfDocumentTab2.DocumentOpenRequested += DocTab_DocumentOpenRequested;
		pdfDocumentTab2.AiSnapshotRequested += DocTab_AiSnapshotRequested;
		pdfDocumentTab2.ScaleCalibrated += DocTab_ScaleCalibrated;
		pdfDocumentTab2.SelectedAnnotationChanged += DocTab_SelectedAnnotationChanged;
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		TextBlock element = new TextBlock
		{
			Text = Path.GetFileName(path),
			Margin = new Thickness(0.0, 0.0, 10.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		Button button = new Button
		{
			Content = "X",
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			Foreground = Brushes.Red,
			FontWeight = FontWeights.Bold,
			Width = 20.0,
			Height = 20.0,
			VerticalAlignment = VerticalAlignment.Center,
			Cursor = Cursors.Hand
		};
		stackPanel.Children.Add(element);
		stackPanel.Children.Add(button);
		TabItem tabItem2 = new TabItem
		{
			Header = stackPanel,
			Content = pdfDocumentTab2
		};
		button.Click += delegate
		{
			PdfTabControl.Items.Remove(tabItem2);
			UpdateTabEmptyState();
			if (PdfTabControl.Items.Count == 0)
			{
				LogStatus("Sẵn sàng");
				UpdatePageIndicator();
				UpdateZoomText();
			}
		};
		PdfTabControl.Items.Add(tabItem2);
		PdfTabControl.SelectedItem = tabItem2;
		RecentFilesService.Record(path);
		RefreshRecentFilesDashboard();
		UpdateTabEmptyState();
	}

	private void DocTab_DocumentReloaded(object? sender, string newPath)
	{
		if (!(sender is PdfDocumentTab pdfDocumentTab))
		{
			return;
		}
		foreach (TabItem item in (IEnumerable)PdfTabControl.Items)
		{
			if (item.Content == pdfDocumentTab)
			{
				if (item.Header is StackPanel stackPanel && stackPanel.Children.Count > 0 && stackPanel.Children[0] is TextBlock textBlock)
				{
					textBlock.Text = Path.GetFileName(newPath);
				}
				break;
			}
		}
		pdfDocumentTab.LoadDocument(newPath);
	}

	private void DocTab_DocumentOpenRequested(object? sender, string path)
	{
		OpenPdfTab(path);
	}

	private void DocTab_ScaleCalibrated(object? sender, double scale)
	{
		if (sender == GetActiveTab())
		{
			_mainRibbon.SetCustomScale(scale);
		}
	}

	private void PdfTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (e.Source is TabControl)
		{
			UpdateStatusBarFromActiveTab();
			PdfDocumentTab activeTab = GetActiveTab();
			if (activeTab != null)
			{
				activeTab.KeepToolsActive = (_mainRibbon != null && _mainRibbon.KeepToolsActive);
			}
			_mainRibbon?.SetContextualTabVisibility(activeTab?.SelectedAnnotation != null);
		}
	}

	private void DocTab_PageChanged(object? sender, EventArgs e)
	{
		if (sender == GetActiveTab())
		{
			UpdatePageIndicator();
		}
	}

	private void DocTab_ZoomChanged(object? sender, EventArgs e)
	{
		if (sender == GetActiveTab())
		{
			UpdateZoomText();
		}
	}

	private void DocTab_StatusChanged(object? sender, EventArgs e)
	{
		if (sender == GetActiveTab() && sender is PdfDocumentTab pdfDocumentTab)
		{
			LogStatus(pdfDocumentTab.LastStatusMessage);
		}
	}

	private void DocTab_SelectedAnnotationChanged(object? sender, EventArgs e)
	{
		if (sender == GetActiveTab() && sender is PdfDocumentTab tab)
		{
			var ann = tab.SelectedAnnotation;
			_mainRibbon?.SetContextualTabVisibility(ann != null);
			if (ann != null)
			{
				_mainRibbon?.UpdateFormattingControls(
					ann.FontFamily,
					ann.FontSize,
					ann.IsBold,
					ann.IsItalic,
					ann.IsUnderline,
					ann.IsStrikeout,
					ann.IsSubscript,
					ann.IsSuperscript,
					ann.TextAlignment,
					ann.StrokeColor,
					ann.BgColor,
					ann.Opacity
				);
			}
		}
	}

	private void UpdateStatusBarFromActiveTab()
	{
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			LogStatus(activeTab.LastStatusMessage);
			UpdatePageIndicator();
			UpdateZoomText();
		}
	}

	private void UpdatePageIndicator()
	{
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null && activeTab.PageCount > 0)
		{
			_updatingStatusControls = true;
			PageJumpTextBox.Text = activeTab.SelectedPageNumber.ToString();
			PageTotalText.Text = $"/ {activeTab.PageCount}";
			_updatingStatusControls = false;
		}
		else
		{
			_updatingStatusControls = true;
			PageJumpTextBox.Text = string.Empty;
			PageTotalText.Text = "/ 0";
			_updatingStatusControls = false;
		}
	}

	private void UpdateZoomText()
	{
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			ZoomIndicator.Text = $"Thu phóng: {Math.Round(activeTab.CurrentZoom * 100.0)}%";
		}
		else
		{
			ZoomIndicator.Text = "Thu phóng: 100%";
		}
		UpdateZoomControls();
	}

	private void UpdateZoomControls()
	{
		PdfDocumentTab activeTab = GetActiveTab();
		_updatingStatusControls = true;
		if (activeTab != null)
		{
			double value = Math.Round(activeTab.CurrentZoom * 100.0);
			ZoomIndicator.Text = $"Thu phóng: {value}%";
			ZoomSlider.Value = Math.Clamp(value, ZoomSlider.Minimum, ZoomSlider.Maximum);
		}
		else
		{
			ZoomIndicator.Text = "Thu phóng: 100%";
			ZoomSlider.Value = 100.0;
		}
		_updatingStatusControls = false;
	}

	private void LogStatus(string message)
	{
		StatusMessage.Text = "Status: " + message;
	}

	private void PageJumpTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Return)
		{
			e.Handled = true;
			GoToTypedPage();
		}
	}

	private void PageJumpTextBox_LostFocus(object sender, RoutedEventArgs e)
	{
		GoToTypedPage();
	}

	private void GoToTypedPage()
	{
		if (_updatingStatusControls)
		{
			return;
		}
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null && activeTab.PageCount > 0)
		{
			if (int.TryParse(PageJumpTextBox.Text, out var result))
			{
				activeTab.GoToPage(result);
			}
			UpdatePageIndicator();
		}
	}

	private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_updatingStatusControls && base.IsLoaded)
		{
			_pendingZoomSliderPercent = e.NewValue;
			_zoomSliderTimer.Stop();
			_zoomSliderTimer.Start();
		}
	}

	private async void SavePdf_Click(object sender, RoutedEventArgs e)
	{
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			await activeTab.SaveDocumentAsync();
		}
		else
		{
			MessageBox.Show("Vui lòng mở một file PDF trước khi lưu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
	}

	private async void SavePdfAs_Click(object sender, RoutedEventArgs e)
	{
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab == null)
		{
			MessageBox.Show("Vui lòng mở một file PDF trước khi lưu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		SaveFileDialog saveFileDialog = new SaveFileDialog
		{
			Filter = "PDF documents (*.pdf)|*.pdf",
			Title = "Lưu file PDF dưới dạng",
			FileName = Path.GetFileNameWithoutExtension(activeTab.CurrentPdfPath) + "_edited.pdf",
			InitialDirectory = Path.GetDirectoryName(activeTab.CurrentPdfPath)
		};
		if (saveFileDialog.ShowDialog() == true)
		{
			await activeTab.SaveDocumentAsync(saveFileDialog.FileName);
		}
	}

	private void PrintPdf_Click(object sender, RoutedEventArgs e)
	{
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			activeTab.PrintPdf();
		}
		else
		{
			MessageBox.Show("Vui lòng mở một file PDF trước khi in.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
	}

	private void BatchPrint_Click(object sender, RoutedEventArgs e)
	{
		if (EnsureActivated())
		{
			BatchToolsWindow batchToolsWindow = new BatchToolsWindow();
			batchToolsWindow.Owner = this;
			batchToolsWindow.ShowDialog();
		}
	}

	private void BatchCompress_Click(object sender, RoutedEventArgs e)
	{
		if (EnsureActivated())
		{
			BatchToolsWindow batchToolsWindow = new BatchToolsWindow(3); // 3 is Nén Tối Ưu tab
			batchToolsWindow.Owner = this;
			batchToolsWindow.ShowDialog();
		}
	}

	private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
	{
		GetActiveTab()?.ToggleSidebar();
	}

	private void ZoomIn_Click(object sender, RoutedEventArgs e)
	{
		GetActiveTab()?.ChangeZoom(1.08);
	}

	private void ZoomOut_Click(object sender, RoutedEventArgs e)
	{
		GetActiveTab()?.ChangeZoom(0.9259259259259258);
	}

	private void FitWidth_Click(object sender, RoutedEventArgs e)
	{
		GetActiveTab()?.FitWidth();
	}

	private void RotateLeft_Click(object sender, RoutedEventArgs e)
	{
		GetActiveTab()?.RotateCurrentPageAsync(-90);
	}

	private void RotateLeftAll_Click(object sender, RoutedEventArgs e)
	{
		GetActiveTab()?.RotateAllPagesAsync(-90);
	}

	private void RotateRight_Click(object sender, RoutedEventArgs e)
	{
		GetActiveTab()?.RotateCurrentPageAsync(90);
	}

	private void RotateRightAll_Click(object sender, RoutedEventArgs e)
	{
		GetActiveTab()?.RotateAllPagesAsync(90);
	}

	private void MovePageUp_Click(object sender, RoutedEventArgs e)
	{
		if (EnsureActivated())
		{
			GetActiveTab()?.MoveSelectedPage(-1);
		}
	}

	private void MovePageDown_Click(object sender, RoutedEventArgs e)
	{
		if (EnsureActivated())
		{
			GetActiveTab()?.MoveSelectedPage(1);
		}
	}

	private void ReversePageOrder_Click(object sender, RoutedEventArgs e)
	{
		if (EnsureActivated())
		{
			GetActiveTab()?.ReversePageOrder();
		}
	}

	private void ResetPageOrder_Click(object sender, RoutedEventArgs e)
	{
		if (EnsureActivated())
		{
			GetActiveTab()?.ResetPageOrder();
		}
	}

	private void DeletePage_Click(object sender, RoutedEventArgs e)
	{
		if (EnsureActivated())
		{
			GetActiveTab()?.DeleteSelectedPageAsync();
		}
	}

	private void InsertBlankPage_Click(object sender, RoutedEventArgs e)
	{
		if (EnsureActivated())
		{
			GetActiveTab()?.InsertBlankPageAsync();
		}
	}

	private void DuplicatePage_Click(object sender, RoutedEventArgs e)
	{
		if (EnsureActivated())
		{
			GetActiveTab()?.DuplicateSelectedPageAsync();
		}
	}

	private void SplitCurrentPage_Click(object sender, RoutedEventArgs e)
	{
		if (EnsureActivated())
		{
			GetActiveTab()?.SplitCurrentPageAsync();
		}
	}

	private async void PageOrganizer_Click(object sender, RoutedEventArgs e)
	{
		if (EnsureActivated())
		{
			var activeTab = GetActiveTab();
			if (activeTab != null)
			{
				await activeTab.OpenPageOrganizerAsync();
			}
		}
	}


	private async void ExportOcrText_Click(object sender, RoutedEventArgs e)
	{
		if (EnsureActivated())
		{
			var tab = GetActiveTab();
			if (tab != null)
			{
				await tab.ExportOcrTextAsync();
			}
		}
	}

	private async void ExportSearchablePdf_Click(object sender, RoutedEventArgs e)
	{
		if (EnsureActivated())
		{
			var tab = GetActiveTab();
			if (tab != null)
			{
				await tab.ExportSearchablePdfAsync();
			}
		}
	}

	private void ComparePdfs_Click(object sender, RoutedEventArgs e)
	{
		if (EnsureActivated())
		{
			PdfComparisonWindow comparisonWindow = new PdfComparisonWindow();
			var activeTab = GetActiveTab();
			if (activeTab != null && !string.IsNullOrEmpty(activeTab.CurrentPdfPath))
			{
				comparisonWindow.SetInitialFileA(activeTab.CurrentPdfPath);
			}
			comparisonWindow.Owner = this;
			comparisonWindow.Show();
		}
	}

	private void CompressPdf_Click(object sender, RoutedEventArgs e)
	{
		if (EnsureActivated())
		{
			PdfDocumentTab activeTab = GetActiveTab();
			if (activeTab == null || string.IsNullOrEmpty(activeTab.CurrentPdfPath))
			{
				MessageBox.Show("Vui lòng mở một file PDF cần nén tối ưu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			CompressPdfWindow compressWindow = new CompressPdfWindow(activeTab.CurrentPdfPath);
			compressWindow.Owner = this;
			if (compressWindow.ShowDialog() == true && !string.IsNullOrEmpty(compressWindow.CompressedPdfPath))
			{
				activeTab.LoadDocument(compressWindow.CompressedPdfPath);
				LogStatus("Tối ưu dung lượng PDF thành công.");
			}
		}
	}

	private void Watermark_Click(object sender, RoutedEventArgs e)
	{
		if (EnsureActivated())
		{
			PdfDocumentTab activeTab = GetActiveTab();
			if (activeTab == null || string.IsNullOrEmpty(activeTab.CurrentPdfPath))
			{
				MessageBox.Show("Vui lòng mở một file PDF cần đóng dấu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			WatermarkDialog dialog = new WatermarkDialog(activeTab.CurrentPdfPath);
			dialog.Owner = this;
			if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.WatermarkedPdfPath))
			{
				activeTab.LoadDocument(dialog.WatermarkedPdfPath);
				LogStatus("Đóng dấu watermark PDF thành công.");
			}
		}
	}

	private void PageNumbering_Click(object sender, RoutedEventArgs e)
	{
		if (EnsureActivated())
		{
			PdfDocumentTab activeTab = GetActiveTab();
			if (activeTab == null || string.IsNullOrEmpty(activeTab.CurrentPdfPath))
			{
				MessageBox.Show("Vui lòng mở một file PDF cần đánh số trang.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			PageNumberingDialog dialog = new PageNumberingDialog(activeTab.CurrentPdfPath);
			dialog.Owner = this;
			if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.NumberedPdfPath))
			{
				activeTab.LoadDocument(dialog.NumberedPdfPath);
				LogStatus("Đánh số trang PDF thành công.");
			}
		}
	}

	private void ExtractImages_Click(object sender, RoutedEventArgs e)
	{
		if (EnsureActivated())
		{
			PdfDocumentTab activeTab = GetActiveTab();
			if (activeTab == null || string.IsNullOrEmpty(activeTab.CurrentPdfPath))
			{
				MessageBox.Show("Vui lòng mở một file PDF cần trích xuất hình ảnh.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			ExtractImagesDialog dialog = new ExtractImagesDialog(activeTab.CurrentPdfPath);
			dialog.Owner = this;
			dialog.ShowDialog();
		}
	}

	private void PdfSecurity_Click(object sender, RoutedEventArgs e)
	{
		if (EnsureActivated())
		{
			PdfDocumentTab activeTab = GetActiveTab();
			if (activeTab == null || string.IsNullOrEmpty(activeTab.CurrentPdfPath))
			{
				MessageBox.Show("Vui lòng mở một file PDF cần cài đặt bảo mật.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			PdfSecurityDialog dialog = new PdfSecurityDialog(activeTab.CurrentPdfPath);
			dialog.Owner = this;
			if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SecuredPdfPath))
			{
				activeTab.LoadDocument(dialog.SecuredPdfPath);
				LogStatus("Bảo mật PDF thành công.");
			}
		}
	}

	private void Exit_Click(object sender, RoutedEventArgs e)
	{
		Application.Current.Shutdown();
	}

	private void Paste_Click(object sender, RoutedEventArgs e)
	{
		GetActiveTab()?.PasteAnnotation(inPlace: false);
	}

	private void Cut_Click(object sender, RoutedEventArgs e)
	{
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			activeTab.CopySelectedAnnotation();
			activeTab.HandleDeleteKey();
		}
	}

	private void Copy_Click(object sender, RoutedEventArgs e)
	{
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			if (activeTab.ActiveTool == "SelectText")
			{
				activeTab.CopySelectedText();
			}
			else
			{
				activeTab.CopySelectedAnnotation();
			}
		}
	}

	private void Format_Click(object sender, RoutedEventArgs e)
	{
		LogStatus("Đã chọn công cụ Sao chép định dạng");
	}

	private async void MergeFiles_Click(object sender, RoutedEventArgs e)
	{
		if (EnsureActivated())
		{
			await ShowMergeDialogAsync(null, autoStartMerge: false, sortByName: false, openMergedExternally: false);
		}
	}

	private async void MergeFromExplorer_Click(object sender, RoutedEventArgs e)
	{
		if (!EnsureActivated())
		{
			return;
		}
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Filter = "PDF documents (*.pdf)|*.pdf",
			Title = "Chọn nhiều file PDF để gộp",
			Multiselect = true
		};
		if (openFileDialog.ShowDialog() == true)
		{
			string[] array = FilterPdfFiles(openFileDialog.FileNames);
			if (array.Length < 2)
			{
				MessageBox.Show("Vui lòng chọn ít nhất 2 file PDF để gộp.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
			else
			{
				await ShowMergeDialogAsync(array, autoStartMerge: true, sortByName: true, openMergedExternally: false);
			}
		}
	}

	private async void MainWindow_Drop(object sender, DragEventArgs e)
	{
		if (!e.Data.GetDataPresent(DataFormats.FileDrop) || !(e.Data.GetData(DataFormats.FileDrop) is string[] files))
		{
			return;
		}
		string[] array = FilterPdfFiles(files);
		if (array.Length == 0)
		{
			return;
		}
		if (array.Length == 1)
		{
			OpenPdfTab(array[0]);
		}
		else if (EnsureActivated())
		{
			string[] initialFiles = array.OrderBy((string path) => path, NaturalFilePathComparer.Instance).ToArray();
			await ShowMergeDialogAsync(initialFiles, autoStartMerge: true, sortByName: true, openMergedExternally: false);
		}
	}

	private void MainWindow_DragOver(object sender, DragEventArgs e)
	{
		e.Effects = (e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None);
		e.Handled = true;
	}

	private void CheckLibraries_Click(object sender, RoutedEventArgs e)
	{
		string report = BuildLibraryAuditReport();
		LogStatus("Library audit complete");
		ShowReportWindow("Kiểm tra thư viện", report);
	}

	private void ShowPerformanceTrace_Click(object sender, RoutedEventArgs e)
	{
		string report = BuildPerformanceTraceReport();
		LogStatus("Performance trace opened");
		ShowReportWindow("Performance Trace", report);
	}

	private void ShowPdfDiagnostics_Click(object sender, RoutedEventArgs e)
	{
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab == null || string.IsNullOrEmpty(activeTab.CurrentPdfPath))
		{
			MessageBox.Show(this, "Vui lòng mở một tài liệu PDF để thực hiện chẩn đoán.", "Chẩn đoán PDF", MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}

		PdfDiagnosticsWindow diagWindow = new PdfDiagnosticsWindow(activeTab.CurrentPdfPath, activeTab);
		diagWindow.Owner = this;
		diagWindow.ShowDialog();
	}

	private void About_Click(object sender, RoutedEventArgs e)
	{
		AboutDialog aboutDialog = new AboutDialog();
		aboutDialog.Owner = this;
		aboutDialog.ShowDialog();
		LogStatus("About dialog opened");
	}

	private void UserGuide_Click(object sender, RoutedEventArgs e)
	{
		SupportGuideWindow supportGuideWindow = new SupportGuideWindow(selectFeedbackTab: false);
		if (base.IsLoaded && base.IsVisible)
		{
			supportGuideWindow.Owner = this;
		}
		supportGuideWindow.ShowDialog();
	}

	private void Feedback_Click(object sender, RoutedEventArgs e)
	{
		SupportGuideWindow supportGuideWindow = new SupportGuideWindow(selectFeedbackTab: true);
		if (base.IsLoaded && base.IsVisible)
		{
			supportGuideWindow.Owner = this;
		}
		supportGuideWindow.ShowDialog();
	}

	private void VirtualPrinterConfig_Click(object sender, RoutedEventArgs e)
	{
		bool printerExists = false;
		try
		{
			using (var server = new System.Printing.LocalPrintServer())
			{
				foreach (var queue in server.GetPrintQueues())
				{
					if (queue.FullName.Equals("PDF Pro - HPhat Edition", StringComparison.OrdinalIgnoreCase))
					{
						printerExists = true;
						break;
					}
				}
			}
		}
		catch { }

		string msg;
		if (printerExists)
		{
			msg = "Máy in ảo 'PDF Pro - HPhat Edition' đã được cài đặt và sẵn sàng hoạt động.\n\nBạn có muốn cài đặt lại / sửa lỗi máy in không?";
		}
		else
		{
			msg = "Máy in ảo 'PDF Pro - HPhat Edition' chưa được cài đặt.\n\nBạn có muốn tiến hành cài đặt máy in ảo ngay bây giờ không? (Yêu cầu quyền Administrator)";
		}

		MessageBoxResult result = MessageBox.Show(msg, "Cấu hình Máy in ảo - PDF Pro", MessageBoxButton.YesNo, MessageBoxImage.Question);
		if (result == MessageBoxResult.Yes)
		{
			try
			{
				// 1. Write the Registry keys from the current user context
				using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\PDFPro\VirtualPrinter"))
				{
					key.SetValue("PrinterName", "PDF Pro - HPhat Edition");
					key.SetValue("AppPath", System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "");
					key.SetValue("AutoOpen", 1, Microsoft.Win32.RegistryValueKind.DWord);
				}

				// 2. Prepare the PowerShell commands to register printer port & printer (requires elevation)
				string script = @"
$printerName = 'PDF Pro - HPhat Edition'
$portName = 'PORTPROMPT:'
$driverName = 'Microsoft Print To PDF'
if (Get-Printer -Name $printerName -ErrorAction SilentlyContinue) {
    Remove-Printer -Name $printerName
}
Add-Printer -Name $printerName -DriverName $driverName -PortName $portName
";
				string base64Script = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
				
				System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo
				{
					FileName = "powershell.exe",
					Arguments = $"-NoProfile -NonInteractive -EncodedCommand {base64Script}",
					Verb = "runas", // Triggers UAC elevation
					UseShellExecute = true,
					WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
				};

				using (var process = System.Diagnostics.Process.Start(psi))
				{
					process?.WaitForExit();
					if (process != null && process.ExitCode == 0)
					{
						MessageBox.Show("Cài đặt máy in ảo thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
					}
					else
					{
						MessageBox.Show("Cài đặt máy in ảo bị hủy hoặc gặp lỗi.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("Lỗi khi cài đặt máy in ảo: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}
	}

	private void MeasureDistanceTool_Click(object sender, RoutedEventArgs e)
	{
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			activeTab.ActiveTool = "MeasureDistance";
			activeTab.CurrentMeasurementScale = _mainRibbon.GetMeasurementScale();
		}
	}

	private void CalibrateScale_Click(object sender, RoutedEventArgs e)
	{
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			activeTab.EnterCalibrateMode();
		}
	}

	private void MeasureAreaTool_Click(object sender, RoutedEventArgs e)
	{
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			activeTab.ActiveTool = "MeasureArea";
			activeTab.CurrentMeasurementScale = _mainRibbon.GetMeasurementScale();
		}
	}

	private void MeasurePerimeterTool_Click(object sender, RoutedEventArgs e)
	{
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			activeTab.ActiveTool = "MeasurePerimeter";
			activeTab.CurrentMeasurementScale = _mainRibbon.GetMeasurementScale();
		}
	}

	private void MeasureGuide_Click(object sender, RoutedEventArgs e)
	{
		string guide = "HƯỚNG DẪN ĐO ĐẠC & TÍNH TOÁN TRÊN BẢN VẼ:\n\n" +
		               "1. Thiết lập Tỷ lệ (Scale):\n" +
		               "- Sử dụng hộp chọn 'Tỷ Lệ' để chọn tỷ lệ xích thực tế tương ứng của bản vẽ (Ví dụ: 1.30 cm tương ứng 100m thực tế).\n" +
		               "- Hoặc thực hiện Hiệu chuẩn tỷ lệ tùy chỉnh bằng cách chọn 'Đo Khoảng Cách', vẽ một đoạn thẳng đã biết trước chiều dài thực tế, nhập khoảng cách thực tế và bấm Xác nhận để tự động tính tỷ lệ xích chính xác.\n\n" +
		               "2. Đo Khoảng Cách (Distance):\n" +
		               "- Chọn 'Đo Khoảng Cách', nhấp giữ chuột từ điểm bắt đầu đến điểm kết thúc rồi thả chuột. Kết quả đo thực tế sẽ hiển thị trực tiếp trên đường vẽ.\n\n" +
		               "3. Đo Diện Tích (Area) & Đo Chu Vi (Perimeter):\n" +
		               "- Chọn 'Đo Diện Tích' hoặc 'Đo Chu Vi'.\n" +
		               "- Nhấp chuột lần lượt để tạo các đỉnh của đa giác.\n" +
		               "- Để hoàn tất và đóng kín đa giác đo: Nhấp đúp chuột hoặc nhấp vào điểm đầu tiên. Kết quả sẽ tự động được tính toán và hiển thị.\n\n" +
		               "*Lưu ý: Bạn có thể nhấn phím ESC để hủy thao tác đo hoặc thoát khỏi chế độ vẽ liên tục bất kỳ lúc nào.";

		MessageBox.Show(this, guide, "Hướng Dẫn Đo Đạc Bản Vẽ", MessageBoxButton.OK, MessageBoxImage.Information);
	}

	private void HandwriteSign_Click(object sender, RoutedEventArgs e)
	{
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab == null) return;

		SignatureInputDialog dialog = new SignatureInputDialog { Owner = this };
		if (dialog.ShowDialog() == true)
		{
			activeTab.StartPlaceSignature(dialog.ResultStrokes, dialog.ResultWidth, dialog.ResultHeight, dialog.ResultColor);
		}
	}

	private void ImageSign_Click(object sender, RoutedEventArgs e)
	{
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab == null)
		{
			MessageBox.Show("Vui lòng mở một file PDF trước.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}

		var openFileDialog = new Microsoft.Win32.OpenFileDialog
		{
			Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
			Title = "Chọn hình ảnh chữ ký"
		};

		if (openFileDialog.ShowDialog() == true)
		{
			activeTab.StartPlaceImageSignature(openFileDialog.FileName);
		}
	}

	private void StampApprove_Click(object sender, RoutedEventArgs e)
	{
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab == null) return;

		ContextMenu menu = new ContextMenu();
		string[] stamps = { "ĐÃ DUYỆT", "BẢN NHÁP", "KHẨN", "MẬT", "HỎA TỐC" };
		foreach (var stamp in stamps)
		{
			MenuItem item = new MenuItem { Header = stamp };
			item.Click += (s, ev) =>
			{
				activeTab.StartPlaceStamp(stamp);
			};
			menu.Items.Add(item);
		}
		menu.IsOpen = true;
	}

	private void MeasurementScale_Changed(object sender, SelectionChangedEventArgs e)
	{
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			activeTab.CurrentMeasurementScale = _mainRibbon.GetMeasurementScale();
		}
	}

	private void Settings_Click(object sender, RoutedEventArgs e)
	{
		SettingsWindow settingsWindow = new SettingsWindow
		{
			Owner = this
		};
		settingsWindow.ShowDialog();
	}

	private void Activation_Click(object sender, RoutedEventArgs e)
	{
		ActivationDialog activationDialog = new ActivationDialog();
		activationDialog.Owner = this;
		activationDialog.ShowDialog();
		ApplyAppActivationState();
	}

	private void ApplyAppActivationState()
	{
		ActivationState activationState = ActivationLicense.LoadState();
		base.Title = (activationState.IsActivated ? (ActivationLicense.AppTitle + " - Activated") : (ActivationLicense.AppTitle + " - Not activated"));
		if (ActivationWarningBanner != null)
		{
			if (!activationState.IsActivated)
			{
				ActivationWarningBanner.Visibility = Visibility.Visible;
				if (ActivationWarningText != null)
				{
					ActivationWarningText.Text = "Ứng dụng chưa được kích hoạt bản quyền. Vui lòng kích hoạt để sử dụng đầy đủ các tính năng nâng cao.";
				}
			}
			else if (activationState.NeedsOnlineVerification)
			{
				ActivationWarningBanner.Visibility = Visibility.Visible;
				if (ActivationWarningText != null)
				{
					ActivationWarningText.Text = activationState.OfflineWarningMessage;
				}
			}
			else
			{
				ActivationWarningBanner.Visibility = Visibility.Collapsed;
			}
		}
		if (LicenseStatusMessage != null)
		{
			if (activationState.IsActivated)
			{
				LicenseStatusMessage.Text = "Bản quyền: Đã kích hoạt (Hạn: " + activationState.ExpirationText + ")";
				LicenseStatusMessage.Foreground = (Brush)new BrushConverter().ConvertFromString("#34D399");
			}
			else
			{
				LicenseStatusMessage.Text = "Bản quyền: Chưa kích hoạt";
				LicenseStatusMessage.Foreground = (Brush)new BrushConverter().ConvertFromString("#F87171");
			}
		}
		_mainRibbon?.SetActivationState(activationState.IsActivated);
	}

	private bool EnsureActivated()
	{
		if (ActivationLicense.LoadState().IsActivated)
		{
			return true;
		}
		MessageBox.Show("Tính năng này yêu cầu kích hoạt bản quyền PRO. Vui lòng kích hoạt bản quyền để tiếp tục sử dụng.", "Yêu cầu Bản Quyền PRO", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		Activation_Click(this, new RoutedEventArgs());
		return false;
	}

	private string BuildLibraryAuditReport()
	{
		string baseDirectory = AppContext.BaseDirectory;
		ActivationState activationState = ActivationLicense.LoadState();
		HashSet<string> loadedAssemblies;
		try
		{
			loadedAssemblies = (from a in AppDomain.CurrentDomain.GetAssemblies()
				select a.GetName().Name ?? string.Empty into name
				where !string.IsNullOrWhiteSpace(name)
				select name).ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
		}
		catch
		{
			loadedAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		}
		HashSet<string> loadedModules;
		try
		{
			using Process process = Process.GetCurrentProcess();
			loadedModules = (from ProcessModule m in (IEnumerable)process.Modules
				select m.ModuleName ?? string.Empty into name
				where !string.IsNullOrWhiteSpace(name)
				select name).ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
		}
		catch
		{
			loadedModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("PDF Pro - Library Audit");
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder2);
		handler.AppendLiteral("Version: ");
		handler.AppendFormatted(activationState.AppVersion);
		stringBuilder3.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(12, 1, stringBuilder2);
		handler.AppendLiteral("Activation: ");
		handler.AppendFormatted(activationState.StatusText);
		stringBuilder4.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(12, 1, stringBuilder2);
		handler.AppendLiteral("Machine ID: ");
		handler.AppendFormatted(activationState.MachineId);
		stringBuilder5.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder6 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder2);
		handler.AppendLiteral("License file: ");
		handler.AppendFormatted(activationState.LicensePath);
		stringBuilder6.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder7 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder2);
		handler.AppendLiteral("App base directory: ");
		handler.AppendFormatted(baseDirectory);
		stringBuilder7.AppendLine(ref handler);
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("Managed assemblies:");
		AppendManagedStatus(stringBuilder, loadedAssemblies, "Fluent", "Fluent.dll", "Used by the Ribbon XAML namespace 'urn:fluent-ribbon'.");
		AppendManagedStatus(stringBuilder, loadedAssemblies, "ControlzEx", "ControlzEx.dll", "Transitive dependency of Fluent.Ribbon.");
		AppendManagedStatus(stringBuilder, loadedAssemblies, "Microsoft.Xaml.Behaviors", "Microsoft.Xaml.Behaviors.dll", "Transitive dependency through ControlzEx.");
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("Native libraries:");
		AppendNativeStatus(stringBuilder, loadedModules, "pdf_core.dll", "Used by P/Invoke for merge/rotate/delete/insert blank page operations.");
		AppendNativeStatus(stringBuilder, loadedModules, "pdfium.dll", "Used by PdfiumEngine for open/render/print.");
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("Direct package audit:");
		stringBuilder.AppendLine("- Fluent.Ribbon: kept. It is used directly in XAML.");
		stringBuilder.AppendLine("- Microsoft.Xaml.Behaviors.Wpf: removed from the direct PackageReference list. It still arrives transitively through ControlzEx.");
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("What you should expect:");
		stringBuilder.AppendLine("- If a native DLL is missing from the app folder, the feature that depends on it will fail.");
		stringBuilder.AppendLine("- Native DLLs may still show as not loaded until you actually open, print, or merge a PDF. File presence is the stronger signal.");
		stringBuilder.AppendLine("- If a managed assembly is listed as transitive, it may still appear in bin even if it is not declared in the csproj.");
		return stringBuilder.ToString();
	}

	private string BuildPerformanceTraceReport()
	{
		string currentLogPath = PdfPerfLogger.CurrentLogPath;
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("PDF Pro - Performance Trace");
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder2);
		handler.AppendLiteral("Log file: ");
		handler.AppendFormatted(currentLogPath);
		stringBuilder2.AppendLine(ref handler);
		stringBuilder.AppendLine();
		stringBuilder.Append(PdfPerfLogger.ReadCurrentLog());
		return stringBuilder.ToString();
	}

	private static void AppendManagedStatus(StringBuilder sb, HashSet<string> loadedAssemblies, string assemblyName, string fileName, string note)
	{
		bool flag = File.Exists(Path.Combine(AppContext.BaseDirectory, fileName));
		bool flag2 = loadedAssemblies.Contains(assemblyName);
		StringBuilder stringBuilder = sb;
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(7, 3, stringBuilder);
		handler.AppendLiteral("- ");
		handler.AppendFormatted(fileName);
		handler.AppendLiteral(": ");
		handler.AppendFormatted(flag2 ? "loaded" : "not loaded");
		handler.AppendLiteral(", ");
		handler.AppendFormatted(flag ? "file present" : "file missing");
		handler.AppendLiteral(".");
		stringBuilder2.AppendLine(ref handler);
		stringBuilder = sb;
		StringBuilder stringBuilder3 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(2, 1, stringBuilder);
		handler.AppendLiteral("  ");
		handler.AppendFormatted(note);
		stringBuilder3.AppendLine(ref handler);
	}

	private static void AppendNativeStatus(StringBuilder sb, HashSet<string> loadedModules, string fileName, string note)
	{
		bool flag = File.Exists(Path.Combine(AppContext.BaseDirectory, fileName));
		bool flag2 = loadedModules.Contains(fileName);
		StringBuilder stringBuilder = sb;
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(7, 3, stringBuilder);
		handler.AppendLiteral("- ");
		handler.AppendFormatted(fileName);
		handler.AppendLiteral(": ");
		handler.AppendFormatted(flag2 ? "loaded" : "not loaded");
		handler.AppendLiteral(", ");
		handler.AppendFormatted(flag ? "file present" : "file missing");
		handler.AppendLiteral(".");
		stringBuilder2.AppendLine(ref handler);
		stringBuilder = sb;
		StringBuilder stringBuilder3 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(2, 1, stringBuilder);
		handler.AppendLiteral("  ");
		handler.AppendFormatted(note);
		stringBuilder3.AppendLine(ref handler);
	}

	private void ShowReportWindow(string title, string report)
	{
		Window dialog = new Window
		{
			Title = title,
			Width = 960.0,
			Height = 720.0,
			Background = Brushes.White
		};
		if (base.IsLoaded && base.IsVisible)
		{
			dialog.Owner = this;
			dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		}
		else
		{
			dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
		}
		Grid grid = new Grid
		{
			Margin = new Thickness(12.0)
		};
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		TextBlock element = new TextBlock
		{
			Text = "Kiểm tra thư viện",
			FontWeight = FontWeights.SemiBold,
			FontSize = 16.0,
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		grid.Children.Add(element);
		TextBox element2 = new TextBox
		{
			Text = report,
			IsReadOnly = true,
			AcceptsReturn = true,
			AcceptsTab = true,
			TextWrapping = TextWrapping.Wrap,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
			FontFamily = new FontFamily("Consolas"),
			FontSize = 13.0,
			BorderThickness = new Thickness(1.0),
			Padding = new Thickness(8.0),
			Background = Brushes.WhiteSmoke
		};
		Grid.SetRow(element2, 1);
		grid.Children.Add(element2);
		Button button = new Button
		{
			Content = "Đóng",
			Width = 90.0,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0),
			IsDefault = true
		};
		button.Click += delegate
		{
			dialog.Close();
		};
		Grid.SetRow(button, 2);
		grid.Children.Add(button);
		dialog.Content = grid;
		dialog.ShowDialog();
	}

	private async Task ShowMergeDialogAsync(string[]? initialFiles, bool autoStartMerge, bool sortByName, bool openMergedExternally)
	{
		MergeDialog mergeDialog = new MergeDialog(initialFiles, autoStartMerge, sortByName);
		if (base.IsLoaded && base.IsVisible)
		{
			mergeDialog.Owner = this;
		}
		if (mergeDialog.ShowDialog() != true || string.IsNullOrEmpty(mergeDialog.MergedFilePath))
		{
			return;
		}
		string tempMergedPath = mergeDialog.MergedFilePath;
		LogStatus("Đã gộp file: " + Path.GetFileName(tempMergedPath));
		if (openMergedExternally)
		{
			try
			{
				Process.Start(new ProcessStartInfo(tempMergedPath)
				{
					UseShellExecute = true
				});
			}
			catch (Exception ex)
			{
				MessageBox.Show("Không thể mở file sau khi gộp: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
			Application.Current.Shutdown();
		}
		else
		{
			// Open merged result in viewer first
			await OpenPdfTabWhenReadyAsync(tempMergedPath);

			// Then immediately prompt user to save to their chosen location
			var saveDialog = new Microsoft.Win32.SaveFileDialog
			{
				Title = "Lưu file đã gộp",
				Filter = "PDF files (*.pdf)|*.pdf",
				FileName = Path.GetFileNameWithoutExtension(tempMergedPath).Replace("_merged_" + DateTime.Now.ToString("yyyyMMdd"), "") + "_merged",
				DefaultExt = ".pdf"
			};
			if (saveDialog.ShowDialog() == true)
			{
				try
				{
					File.Copy(tempMergedPath, saveDialog.FileName, overwrite: true);
					LogStatus("Đã lưu: " + saveDialog.FileName);
					// Reload tab from saved location
					await OpenPdfTabWhenReadyAsync(saveDialog.FileName);
				}
				catch (Exception ex2)
				{
					MessageBox.Show("Không thể lưu file: " + ex2.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
				}
				finally
				{
					// Clean up temp file
					try { File.Delete(tempMergedPath); } catch { }
				}
			}
		}
	}


	private async Task HandleExplorerMergeStartupAsync(string[] initialFiles)
	{
		QueueExplorerMergeFiles(initialFiles);
		if (!TryBecomeExplorerMergeOwner())
		{
			Application.Current.Shutdown();
			return;
		}
		bool mergeSuccessful = false;
		string text = null;
		try
		{
			string[] array = await CollectQueuedExplorerMergeFilesAsync(TimeSpan.FromSeconds(5.0), TimeSpan.FromMilliseconds(600.0));
			if (array.Length < 2)
			{
				Application.Current.Shutdown();
				return;
			}
			string pathsSemicolon = string.Join(";", array);
			text = MergeDialog.CreateAutoOutputPath(array[0]);
			if (new MergeProgressWindow(pathsSemicolon, text).ShowDialog() == true)
			{
				mergeSuccessful = true;
			}
		}
		finally
		{
			ReleaseExplorerMergeOwner();
			if (!mergeSuccessful)
			{
				Application.Current.Shutdown();
			}
		}

		if (mergeSuccessful && text != null)
		{
			Show();
			await OpenPdfTabWhenReadyAsync(text);
		}
	}

	public static async Task RunExplorerMergeFlowAsync(string[] initialFiles)
	{
		QueueExplorerMergeFiles(initialFiles);
		if (!TryBecomeExplorerMergeOwner())
		{
			Environment.Exit(0);
			return;
		}
		string outputPath = null;
		bool mergeSuccessful = false;
		try
		{
			string[] array = await CollectQueuedExplorerMergeFilesAsync(TimeSpan.FromSeconds(5.0), TimeSpan.FromMilliseconds(600.0));
			if (array.Length < 2)
			{
				Environment.Exit(0);
				return;
			}
			string pathsSemicolon = string.Join(";", array);
			outputPath = MergeDialog.CreateAutoOutputPath(array[0]);
			bool? success = null;
			Application.Current.Dispatcher.Invoke(delegate
			{
				MergeProgressWindow mergeProgressWindow = new MergeProgressWindow(pathsSemicolon, outputPath);
				success = mergeProgressWindow.ShowDialog();
			});
			if (success == true)
			{
				mergeSuccessful = true;
			}
		}
		finally
		{
			ReleaseExplorerMergeOwner();
			if (!mergeSuccessful)
			{
				Environment.Exit(0);
			}
		}

		if (mergeSuccessful && outputPath != null)
		{
			App.HandlePostMergeOpen(outputPath);
		}
	}

	public static bool TryBecomeExplorerMergeOwner()
	{
		try
		{
			if (_explorerMergeMutex == null)
			{
				_explorerMergeMutex = new Mutex(initiallyOwned: false, "Local\\PdfPro.MergeStartupOwner");
			}
			try
			{
				return _explorerMergeMutex.WaitOne(0);
			}
			catch (AbandonedMutexException)
			{
				return true;
			}
		}
		catch
		{
			return false;
		}
	}

	private static void ReleaseExplorerMergeOwner()
	{
		try
		{
			_explorerMergeMutex?.ReleaseMutex();
		}
		catch
		{
		}
		finally
		{
			_explorerMergeMutex?.Dispose();
			_explorerMergeMutex = null;
		}
	}

	public static void QueueExplorerMergeFiles(IEnumerable<string> files)
	{
		string explorerMergeQueueDir = GetExplorerMergeQueueDir();
		Directory.CreateDirectory(explorerMergeQueueDir);
		foreach (string file in files)
		{
			EnqueueExplorerMergeFile(explorerMergeQueueDir, file);
		}
	}

	private static async Task<string[]> CollectQueuedExplorerMergeFilesAsync(TimeSpan timeout, TimeSpan quietPeriod)
	{
		string queueDir = GetExplorerMergeQueueDir();
		Directory.CreateDirectory(queueDir);
		List<string> files = new List<string>();
		HashSet<string> seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		DateTime startTime = DateTime.UtcNow;
		DateTime lastChangeTime = startTime;
		while (DateTime.UtcNow - startTime < timeout)
		{
			bool flag = false;
			foreach (string item in Directory.EnumerateFiles(queueDir, "*.txt").OrderBy((string path) => path, NaturalFilePathComparer.Instance))
			{
				try
				{
					string[] array = File.ReadAllLines(item);
					for (int num = 0; num < array.Length; num++)
					{
						string text = array[num].Trim();
						if (!string.IsNullOrWhiteSpace(text) && File.Exists(text) && Path.GetExtension(text).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
						{
							string fullPath = Path.GetFullPath(text);
							if (seenFiles.Add(fullPath))
							{
								files.Add(fullPath);
								flag = true;
								lastChangeTime = DateTime.UtcNow;
							}
						}
					}
				}
				catch
				{
				}
				try
				{
					File.Delete(item);
				}
				catch
				{
				}
			}
			if (files.Count >= 2 && DateTime.UtcNow - lastChangeTime >= quietPeriod)
			{
				break;
			}
			if (!flag)
			{
				await Task.Delay(150);
			}
		}
		return files.OrderBy((string path) => path, NaturalFilePathComparer.Instance).ToArray();
	}

	public static void EnqueueExplorerMergeFile(string queueDir, string filePath)
	{
		string[] array = FilterPdfFiles(new string[1] { filePath });
		if (array.Length == 0)
		{
			return;
		}
		string path = Path.Combine(queueDir, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}_{Environment.ProcessId}.txt");
		try
		{
			File.WriteAllLines(path, array);
		}
		catch
		{
		}
	}

	public static string GetExplorerMergeQueueDir()
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PdfPro", "ExplorerMergeQueue");
	}

	public static string[] FilterPdfFiles(IEnumerable<string> files)
	{
		return files.Where((string path) => !string.IsNullOrWhiteSpace(path) && File.Exists(path) && Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase)).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
	}

	private async Task OpenPdfTabWhenReadyAsync(string path)
	{
		for (int attempt = 0; attempt < 20; attempt++)
		{
			if (IsReadablePdf(path))
			{
				OpenPdfTab(path);
				return;
			}
			await Task.Delay(150);
		}
		MessageBox.Show("Đã gộp file nhưng chưa mở được file kết quả. Vui lòng mở lại file đã gộp.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
	}

	private static bool IsReadablePdf(string path)
	{
		try
		{
			if (!File.Exists(path))
			{
				return false;
			}
			using FileStream fileStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			return fileStream.Length > 0;
		}
		catch
		{
			return false;
		}
	}

	private void UpdateToolButtonStates()
	{
		_mainRibbon?.SetActiveTool(ActiveTool);
		if (ActiveToolText == null || ActiveToolIndicator == null)
		{
			return;
		}
		if (ActiveTool == "Select")
		{
			ActiveToolStatusBarItem.Visibility = Visibility.Collapsed;
			ActiveToolSeparator.Visibility = Visibility.Collapsed;
			ActiveToolOverlay.Visibility = Visibility.Collapsed;
			return;
		}
		string text = ActiveTool switch
		{
			"SelectText" => "Chọn chữ (Select Text)", 
			"EditText" => "Sửa văn bản (Edit Text)", 
			"Ink" => "Bút vẽ tự do (Ink)", 
			"ShapeRect" => "Hình chữ nhật (Rectangle)", 
			"ShapeOval" => "Hình tròn (Oval)", 
			"ShapeLine" => "Đường thẳng (Line)", 
			"TextBox" => "Hộp văn bản (Text Box)", 
			"Callout" => "Mũi tên ghi chú (Callout)", 
			"StickyNote" => "Ghi chú nhanh (Sticky Note)", 
			"Snapshot" => "Chụp vùng in (Snapshot)", 
			"AiSnapshot" => "Chụp hỏi AI (AI Copilot)", 
			_ => ActiveTool, 
		};
		ActiveToolText.Text = "Công cụ: " + text;
		ActiveToolStatusBarItem.Visibility = Visibility.Visible;
		ActiveToolSeparator.Visibility = Visibility.Visible;
		if (OverlayToolNameText != null && ActiveToolOverlay != null)
		{
			OverlayToolNameText.Text = "ĐANG SỬ DỤNG: " + text.ToUpper();
			ActiveToolOverlay.Visibility = Visibility.Visible;
		}
	}

	private void CancelMode_Click(object sender, RoutedEventArgs e)
	{
		ActiveTool = "Select";
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			activeTab.ActiveTool = "Select";
		}
		LogStatus("Đã hủy chế độ vẽ/chú thích");
	}

	private void SelectTool_Click(object sender, RoutedEventArgs e)
	{
		ActiveTool = "Select";
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			activeTab.ActiveTool = "Select";
		}
		LogStatus("Đã chuyển sang công cụ chọn để thực hiện kích hoạt");
	}

	private void InkTool_Click(object sender, RoutedEventArgs e)
	{
		ActiveTool = "Ink";
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			activeTab.ActiveTool = "Ink";
		}
		LogStatus("Đã chuyển sang công cụ Bút vẽ tự do. Hãy kéo chuột để vẽ.");
	}

	private void RectTool_Click(object sender, RoutedEventArgs e)
	{
		ActiveTool = "ShapeRect";
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			activeTab.ActiveTool = "ShapeRect";
		}
		LogStatus("Đã chuyển sang công cụ Hình chữ nhật. Hãy kéo chuột để vẽ.");
	}

	private void OvalTool_Click(object sender, RoutedEventArgs e)
	{
		ActiveTool = "ShapeOval";
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			activeTab.ActiveTool = "ShapeOval";
		}
		LogStatus("Đã chuyển sang công cụ Hình tròn. Hãy kéo chuột để vẽ.");
	}

	private void LineTool_Click(object sender, RoutedEventArgs e)
	{
		ActiveTool = "ShapeLine";
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			activeTab.ActiveTool = "ShapeLine";
		}
		LogStatus("Đã chuyển sang công cụ Đường thẳng. Hãy kéo chuột để vẽ.");
	}

	private void StickyNoteTool_Click(object sender, RoutedEventArgs e)
	{
		ActiveTool = "StickyNote";
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			activeTab.ActiveTool = "StickyNote";
		}
		LogStatus("Đã chuyển sang công cụ Ghi chú. Hãy click lên một vị trí bất kỳ trên trang để tạo.");
	}

	private void SelectTextTool_Click(object sender, RoutedEventArgs e)
	{
		ActiveTool = "SelectText";
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			activeTab.ActiveTool = "SelectText";
		}
		LogStatus("Đã chuyển sang công cụ chọn văn bản. Hãy kéo chuột đểquét chọn chữ.");
	}

	private void EditTextTool_Click(object sender, RoutedEventArgs e)
	{
		ActiveTool = "EditText";
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			activeTab.ActiveTool = "EditText";
		}
		LogStatus("Đã chuyển sang công cụ sửa chữ trực tiếp. Nhấp đúp vào dòng chữ bất kỳ để sửa.");
	}

	private void TextBoxTool_Click(object sender, RoutedEventArgs e)
	{
		ActiveTool = "TextBox";
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			activeTab.ActiveTool = "TextBox";
		}
		_mainRibbon?.SelectMeasureAndSignTab();
		LogStatus("Đã chuyển sang công cụ Hộp văn bản để thực hiện kích hoạt. Hãy kéo chuột trên trang bản vẽ để tạo.");
	}

	private void HighlightTool_Click(object sender, RoutedEventArgs e)
	{
		ActiveTool = "Highlight";
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			string selectedText = activeTab.GetSelectedTextString();
			if (!string.IsNullOrEmpty(selectedText))
			{
				activeTab.HighlightSelectedText("#FFFF00");
				LogStatus("Đã tô màu (Highlight) vùng chữ được chọn.");
				ActiveTool = "SelectText";
				activeTab.ActiveTool = "SelectText";
			}
			else
			{
				activeTab.ActiveTool = "SelectText";
				LogStatus("Hãy bôi đen văn bản để thực hiện tô màu (Highlight).");
			}
		}
	}

	private void CalloutTool_Click(object sender, RoutedEventArgs e)
	{
		ActiveTool = "Callout";
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			activeTab.ActiveTool = "Callout";
		}
		_mainRibbon?.SelectMeasureAndSignTab();
		LogStatus("Đã chuyển sang công cụ Mũi tên chệdẫn để thực hiện kích hoạt. Nhập chuỗi để tạo mũi tên, kéo để tạo ghi chú.");
	}

	private void SnapshotTool_Click(object sender, RoutedEventArgs e)
	{
		ActiveTool = "Snapshot";
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			activeTab.ActiveTool = "Snapshot";
		}
		LogStatus("Đã chuyển sang công cụ Snapshot. Kéo chọn một vùng trên bản vẽ đểin phóng ra A3.");
	}

	private void AiSnapshotTool_Click(object sender, RoutedEventArgs e)
	{
		if (EnsureActivated())
		{
			ActiveTool = "AiSnapshot";
			PdfDocumentTab activeTab = GetActiveTab();
			if (activeTab != null)
			{
				activeTab.ActiveTool = "AiSnapshot";
			}
			ShowAiPanel();
			LogStatus("Đã chuyển sang AI Snapshot. Kéo chọn một vùng bản vẽ đểhỏi AI.");
		}
	}

	private void DocTab_AiSnapshotRequested(object? sender, AiSnapshotRequest request)
	{
		ShowAiPanel();
		_lastCapturedSnapshotBase64 = request.PngBase64;
		_lastCapturedSnapshotPageNumber = request.PageNumber;

		if (_aiPanelControl != null)
		{
			string prompt = _aiPanelControl.PromptText;
			if (string.IsNullOrWhiteSpace(prompt) || prompt == "Hãy đọc và giải thích vùng bản vẽ này.")
			{
				prompt = "Hãy giải thích vùng bản vẽ này.";
			}
			AiPanel_SendChatRequested(this, prompt);
		}
	}

	private async void AiPanel_SendChatRequested(object? sender, string prompt)
	{
		if (_aiPanelControl == null) return;

		var activeTab = GetActiveTab();
		if (activeTab == null)
		{
			_aiPanelControl.AddMessage("model", "Vui lòng mở một tài liệu PDF trước khi chat.");
			return;
		}

		string scope = _aiPanelControl.SelectedScope;
		string? imageBase64 = null;
		int pageNumber = activeTab.SelectedPageNumber;
		
		var chatHistory = _aiPanelControl.GetChatHistory();
		bool isFirstMessage = (chatHistory.Count == 0);

		if (isFirstMessage)
		{
			if (scope == "Snapshot")
			{
				if (!string.IsNullOrEmpty(_lastCapturedSnapshotBase64) && _lastCapturedSnapshotPageNumber == pageNumber)
				{
					imageBase64 = _lastCapturedSnapshotBase64;
				}
				else
				{
					_aiPanelControl.AddMessage("model", "Vui lòng kéo chọn (khoanh vùng) một vùng trên bản vẽ trước, hoặc chọn chế độ 'Toàn bộ trang' để chat trực tiếp.");
					return;
				}
			}
			else
			{
				try
				{
					string pdfPath = activeTab.CurrentPdfPath;
					if (string.IsNullOrEmpty(pdfPath))
					{
						_aiPanelControl.AddMessage("model", "Tệp PDF không hợp lệ.");
						return;
					}
					
					var selection = new PdfSnapshotSelection(pdfPath, pageNumber - 1, 0.0, 0.0, 1.0, 1.0);
					imageBase64 = AiSnapshotImageRenderer.RenderSnapshotToPngBase64(selection);
				}
				catch (Exception ex)
				{
					_aiPanelControl.AddMessage("model", "Không thể chụp hình ảnh trang PDF: " + ex.Message);
					return;
				}
			}
		}

		// Add tin nhắn của user vào UI (kèm ảnh thumbnail nếu có)
		_aiPanelControl.AddMessage("user", prompt, imageBase64);

		// Tạo bản copy lịch sử chat hiện tại gửi lên API
		var historyToSend = new List<ChatMessage>(_aiPanelControl.GetChatHistory());

		// Thêm bong bóng chờ của AI
		_aiPanelControl.AddMessage("model", "Đang phân tích...");
		int aiMessageIndex = _aiPanelControl.GetChatHistory().Count - 1;

		try
		{
			var request = new AiSnapshotRequest(
				Prompt: prompt,
				PngBase64: imageBase64 ?? string.Empty,
				PageNumber: pageNumber,
				X: 0, Y: 0, Width: 1.0, Height: 1.0,
				History: historyToSend
			);

			string reply = await _aiSnapshotRouter.AskSnapshotAsync(request, CancellationToken.None);
			
			_aiPanelControl.GetChatHistory()[aiMessageIndex].Text = reply;
			UpdateLastAiMessageInUi(reply);
		}
		catch (Exception ex)
		{
			string err = "Lỗi kết nối AI: " + ex.Message;
			_aiPanelControl.GetChatHistory()[aiMessageIndex].Text = err;
			UpdateLastAiMessageInUi(err);
		}
	}

	private void UpdateLastAiMessageInUi(string text)
	{
		if (_aiPanelControl == null) return;
		try
		{
			var stackPanel = _aiPanelControl.FindName("ChatMessagesStackPanel") as StackPanel;
			if (stackPanel != null && stackPanel.Children.Count > 0)
			{
				var lastChild = stackPanel.Children[stackPanel.Children.Count - 1] as Border;
				if (lastChild != null)
				{
					var contentPanel = lastChild.Child as StackPanel;
					if (contentPanel != null)
					{
						foreach (var child in contentPanel.Children)
						{
							if (child is TextBlock textBlock)
							{
								textBlock.Text = text;
								break;
							}
						}
					}
				}
			}
		}
		catch
		{
		}
	}

	private void ShowAiPanel()
	{
		EnsureAiPanelHost();
		if (_aiPanelControl != null)
		{
			_aiPanelControl.ShowPanel();
			AiPanelColumn.Width = new GridLength(360.0);
			AiPanelHostContainer.Visibility = Visibility.Visible;
		}
	}

	private void CloseAiPanel_Click(object sender, RoutedEventArgs e)
	{
		if (_aiPanelControl != null)
		{
			_aiPanelControl.HidePanel();
		}
		AiPanelColumn.Width = new GridLength(0.0);
		AiPanelHostContainer.Visibility = Visibility.Collapsed;
	}

	private static void OpenUrl(string url)
	{
		Process.Start(new ProcessStartInfo
		{
			FileName = url,
			UseShellExecute = true
		});
	}

	private void OpenGeminiApiKey_Click(object sender, RoutedEventArgs e)
	{
		OpenUrl("https://aistudio.google.com/app/apikey");
	}

	private void OpenOpenAiApiKey_Click(object sender, RoutedEventArgs e)
	{
		OpenUrl("https://platform.openai.com/api-keys");
	}

	private void OpenOllamaDownload_Click(object sender, RoutedEventArgs e)
	{
		OpenUrl("https://ollama.com/download");
	}

	private async void CheckAi_Click(object sender, RoutedEventArgs e)
	{
		ShowAiPanel();
		if (_aiPanelControl != null)
		{
			_aiPanelControl.SetOutput("Dang kiem tra AI...");
			_aiPanelControl.SetOutput(await AiSystemCheckService.BuildReportAsync(ReadAiSettingsFromUi(), CancellationToken.None));
		}
	}

	private void SaveAiSettings_Click(object sender, RoutedEventArgs e)
	{
		_aiSettings = ReadAiSettingsFromUi();
		_aiSettings.Save();
		_aiSnapshotRouter = new AiSnapshotRouter(_aiSettings);
		_aiPanelControl?.SetOutput("Da luu cau hinh AI: " + AiSettings.SettingsPath);
	}

	private void ApplyAiSettingsToUi()
	{
		_aiPanelControl?.LoadSettings(_aiSettings);
	}

	private AiSettings ReadAiSettingsFromUi()
	{
		return _aiPanelControl?.ReadSettings() ?? _aiSettings;
	}

	private void UpdateAnnotationSettingsFromRibbon()
	{
		if (_mainRibbon == null)
		{
			return;
		}

		(string fontFamily, double fontSize, bool bold, bool italic, bool underline, bool strikeout, bool subscript, bool superscript, TextAlignment alignment, Color strokeColor, Color backgroundColor, double opacity) = _mainRibbon.ReadAnnotationSettings();
		ActiveFontFamily = fontFamily;
		ActiveFontSize = fontSize;
		ActiveIsBold = bold;
		ActiveIsItalic = italic;
		ActiveIsUnderline = underline;
		ActiveIsStrikeout = strikeout;
		ActiveIsSubscript = subscript;
		ActiveIsSuperscript = superscript;
		ActiveTextAlignment = alignment;
		ActiveStrokeColor = strokeColor;
		ActiveBgColor = backgroundColor;
		ActiveOpacity = opacity;
		ApplyStylesToActiveTab();
	}

	private void FontFamilyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		UpdateAnnotationSettingsFromRibbon();
	}

	private void FontSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		UpdateAnnotationSettingsFromRibbon();
	}

	private void BoldToggle_Click(object sender, RoutedEventArgs e)
	{
		UpdateAnnotationSettingsFromRibbon();
	}

	private void ItalicToggle_Click(object sender, RoutedEventArgs e)
	{
		UpdateAnnotationSettingsFromRibbon();
	}

	private void UnderlineToggle_Click(object sender, RoutedEventArgs e)
	{
		UpdateAnnotationSettingsFromRibbon();
	}

	private void StrokeColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		UpdateAnnotationSettingsFromRibbon();
	}

	private void BgColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		UpdateAnnotationSettingsFromRibbon();
	}

	private void OpacitySpinner_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		UpdateAnnotationSettingsFromRibbon();
	}

	private void ApplyStylesToActiveTab()
	{
		GetActiveTab()?.ApplyStylesToActiveAnnotation(
			ActiveFontFamily,
			ActiveFontSize,
			ActiveIsBold,
			ActiveIsItalic,
			ActiveIsUnderline,
			ActiveIsStrikeout,
			ActiveIsSubscript,
			ActiveIsSuperscript,
			ActiveTextAlignment,
			ActiveStrokeColor,
			ActiveBgColor,
			ActiveOpacity
		);
	}

	private void BulletList_Click(object sender, RoutedEventArgs e)
	{
		GetActiveTab()?.ApplyBulletListToActiveTextBox();
	}

	private void NumberList_Click(object sender, RoutedEventArgs e)
	{
		GetActiveTab()?.ApplyNumberListToActiveTextBox();
	}

	private Color ParseColor(string colorName)
	{
		try
		{
			object obj = ColorConverter.ConvertFromString(colorName);
			if (obj is Color)
			{
				return (Color)obj;
			}
		}
		catch
		{
		}
		return Colors.Red;
	}

	private void UpdateTabEmptyState()
	{
		if (TabEmptyState != null && PdfTabControl != null)
		{
			TabEmptyState.Visibility = ((PdfTabControl.Items.Count != 0) ? Visibility.Collapsed : Visibility.Visible);
		}
		RefreshRecentFilesDashboard();
	}

	/// <summary>Áp dụng theme từ preferences (ưu tiên ThemeName, fallback IsDarkTheme).</summary>
	public void ApplyThemeFromPreferences(bool isDark)
	{
		// Gọi SetTheme với ThemeName từ preferences để hỗ trợ multi-theme
		SetTheme(_appPreferences.ThemeName);
	}

	/// <summary>Cập nhật cấu hình và áp dụng ngay lập tức trong thời gian thực.</summary>
	public void UpdatePreferences(string themeName, bool allowMultipleInstances, string ocrLanguage, bool enhanceThinLines)
	{
		_appPreferences.ThemeName = themeName;
		_appPreferences.AllowMultipleInstances = allowMultipleInstances;
		_appPreferences.OcrLanguage = ocrLanguage;
		_appPreferences.EnhanceThinLines = enhanceThinLines;
		_appPreferences.Save();
		SetTheme(themeName);

		PdfiumEngine.EnhanceThinLines = enhanceThinLines;
		RefreshAllTabs();
	}

	public void RefreshAllTabs()
	{
		if (PdfTabControl != null)
		{
			foreach (object item in PdfTabControl.Items)
			{
				if (item is TabItem tabItem && tabItem.Content is PdfDocumentTab docTab)
				{
					docTab.ClearCacheAndRender();
				}
			}
		}
	}

	/// <summary>Preview theme ngay lập tức khi người dùng chọn trong Settings (chưa lưu).</summary>
	public void PreviewTheme(string themeName)
	{
		SetTheme(themeName);
	}

	public void ReloadAiSettings()
	{
		_aiSettings = AiSettings.Load();
		_aiSnapshotRouter = new AiSnapshotRouter(_aiSettings);
		ApplyAiSettingsToUi();
	}

	public void RefreshRecentFilesDashboard()
	{
		_welcomeDashboard?.SetRecentFiles(RecentFilesService.Load());
	}

	private void HookDashboardEvents()
	{
		if (_welcomeDashboard == null)
		{
			return;
		}

		_welcomeDashboard.OpenRequested += WelcomeDashboard_OpenRequested;
		_welcomeDashboard.MergeRequested += WelcomeDashboard_MergeRequested;
		_welcomeDashboard.PrintRequested += WelcomeDashboard_PrintRequested;
		_welcomeDashboard.AiSnapshotRequested += WelcomeDashboard_AiSnapshotRequested;
		_welcomeDashboard.SettingsRequested += WelcomeDashboard_SettingsRequested;
		_welcomeDashboard.OpenRecentRequested += WelcomeDashboard_OpenRecentRequested;
		_welcomeDashboard.CompressRequested += WelcomeDashboard_CompressRequested;
	}

	private void HookMainRibbonEvents()
	{
		if (_mainRibbon == null)
		{
			return;
		}

		_mainRibbon.OpenPdfRequested += OpenPdf_Click;
		_mainRibbon.SavePdfRequested += SavePdf_Click;
		_mainRibbon.SavePdfAsRequested += SavePdfAs_Click;
		_mainRibbon.ComparePdfsRequested += ComparePdfs_Click;
		_mainRibbon.CompressPdfRequested += CompressPdf_Click;
		_mainRibbon.BatchCompressRequested += BatchCompress_Click;
		_mainRibbon.WatermarkRequested += Watermark_Click;
		_mainRibbon.PageNumberingRequested += PageNumbering_Click;
		_mainRibbon.ExtractImagesRequested += ExtractImages_Click;
		_mainRibbon.PdfSecurityRequested += PdfSecurity_Click;
		_mainRibbon.ExitRequested += Exit_Click;
		_mainRibbon.PrintPdfRequested += PrintPdf_Click;
		_mainRibbon.BatchPrintRequested += BatchPrint_Click;
		_mainRibbon.ZoomInRequested += ZoomIn_Click;
		_mainRibbon.ZoomOutRequested += ZoomOut_Click;
		_mainRibbon.FitWidthRequested += FitWidth_Click;
		_mainRibbon.SelectTextToolRequested += SelectTextTool_Click;
		_mainRibbon.EditTextToolRequested += EditTextTool_Click;
		_mainRibbon.ExportOcrTextRequested += ExportOcrText_Click;
		_mainRibbon.ExportSearchablePdfRequested += ExportSearchablePdf_Click;
		_mainRibbon.ToggleSidebarRequested += ToggleSidebar_Click;
		_mainRibbon.ThemeToggleRequested += ThemeToggle_Click;
		_mainRibbon.SettingsRequested += Settings_Click;
		_mainRibbon.MergeFilesRequested += MergeFiles_Click;
		_mainRibbon.MergeFromExplorerRequested += MergeFromExplorer_Click;
		_mainRibbon.RotateLeftRequested += RotateLeft_Click;
		_mainRibbon.RotateLeftAllRequested += RotateLeftAll_Click;
		_mainRibbon.RotateRightRequested += RotateRight_Click;
		_mainRibbon.RotateRightAllRequested += RotateRightAll_Click;
		_mainRibbon.MovePageUpRequested += MovePageUp_Click;
		_mainRibbon.MovePageDownRequested += MovePageDown_Click;
		_mainRibbon.ReversePageOrderRequested += ReversePageOrder_Click;
		_mainRibbon.ResetPageOrderRequested += ResetPageOrder_Click;
		_mainRibbon.DeletePageRequested += DeletePage_Click;
		_mainRibbon.InsertBlankPageRequested += InsertBlankPage_Click;
		_mainRibbon.DuplicatePageRequested += DuplicatePage_Click;
		_mainRibbon.SplitCurrentPageRequested += SplitCurrentPage_Click;
		_mainRibbon.ExtractPagesRequested += ExtractPages_Click;
		_mainRibbon.SelectToolRequested += SelectTool_Click;
		_mainRibbon.InkToolRequested += InkTool_Click;
		_mainRibbon.RectToolRequested += RectTool_Click;
		_mainRibbon.OvalToolRequested += OvalTool_Click;
		_mainRibbon.LineToolRequested += LineTool_Click;
		_mainRibbon.TextBoxToolRequested += TextBoxTool_Click;
		_mainRibbon.HighlightToolRequested += HighlightTool_Click;
		_mainRibbon.CalloutToolRequested += CalloutTool_Click;
		_mainRibbon.StickyNoteToolRequested += StickyNoteTool_Click;
		_mainRibbon.SnapshotToolRequested += SnapshotTool_Click;
		_mainRibbon.AiSnapshotToolRequested += AiSnapshotTool_Click;
		_mainRibbon.ManualUpdateCheckRequested += ManualUpdateCheck_Click;
		_mainRibbon.CheckLibrariesRequested += CheckLibraries_Click;
		_mainRibbon.ActivationRequested += Activation_Click;
		_mainRibbon.ShowPerformanceTraceRequested += ShowPerformanceTrace_Click;
		_mainRibbon.ShowPdfDiagnosticsRequested += ShowPdfDiagnostics_Click;
		_mainRibbon.RestorePreviousVersionRequested += RestorePreviousVersion_Click;
		_mainRibbon.AboutRequested += About_Click;
		_mainRibbon.UserGuideRequested += UserGuide_Click;
		_mainRibbon.FeedbackRequested += Feedback_Click;
		_mainRibbon.VirtualPrinterConfigRequested += VirtualPrinterConfig_Click;
		_mainRibbon.MeasureDistanceToolRequested += MeasureDistanceTool_Click;
		_mainRibbon.MeasureAreaToolRequested += MeasureAreaTool_Click;
		_mainRibbon.MeasurePerimeterToolRequested += MeasurePerimeterTool_Click;
		_mainRibbon.MeasureGuideRequested += MeasureGuide_Click;
		_mainRibbon.CalibrateScaleRequested += CalibrateScale_Click;
		_mainRibbon.HandwriteSignRequested += HandwriteSign_Click;
		_mainRibbon.ImageSignRequested += ImageSign_Click;
		_mainRibbon.StampApproveRequested += StampApprove_Click;
		_mainRibbon.PasteRequested += Paste_Click;
		_mainRibbon.CutRequested += Cut_Click;
		_mainRibbon.CopyRequested += Copy_Click;
		_mainRibbon.FormatRequested += Format_Click;
		_mainRibbon.MeasurementScaleChanged += MeasurementScale_Changed;
		_mainRibbon.SettingsChanged += MainRibbon_SettingsChanged;
		_mainRibbon.KeepToolsActiveChanged += KeepToolsActive_Changed;
		_mainRibbon.OpenUrlRequested += OpenUrl;
		_mainRibbon.PageOrganizerRequested += PageOrganizer_Click;
		_mainRibbon.BulletListRequested += BulletList_Click;
		_mainRibbon.NumberListRequested += NumberList_Click;
	}

	private void KeepToolsActive_Changed(object? sender, RoutedEventArgs e)
	{
		bool active = _mainRibbon?.KeepToolsActive == true;
		if (BtnKeepToolsActive != null)
		{
			BtnKeepToolsActive.IsChecked = active;
		}
		PdfDocumentTab activeTab = GetActiveTab();
		if (activeTab != null)
		{
			activeTab.KeepToolsActive = active;
		}
	}

	private void MainRibbon_SettingsChanged(object? sender, EventArgs e)
	{
		UpdateAnnotationSettingsFromRibbon();
	}

	private void HookAiPanelEvents()
	{
		if (_aiPanelControl == null)
		{
			return;
		}

		_aiPanelControl.CloseRequested += AiPanel_CloseRequested;
		_aiPanelControl.SettingsChanged += AiPanel_SettingsChanged;
		_aiPanelControl.CheckAiRequested += AiPanel_CheckAiRequested;
		_aiPanelControl.SaveAiRequested += AiPanel_SaveAiRequested;
		_aiPanelControl.OpenUrlRequested += (_, url) => OpenUrl(url);
		_aiPanelControl.SendChatRequested += AiPanel_SendChatRequested;
	}

	private void EnsureWelcomeDashboardHost()
	{
		if (_welcomeDashboard != null || DashboardHostContainer == null)
		{
			return;
		}

		_welcomeDashboard = new WelcomeDashboard();
		_welcomeDashboard.ApplyTheme(AppThemeRegistry.Get(_appPreferences.ThemeName));
		DashboardHostContainer.Children.Clear();
		DashboardHostContainer.Children.Add(_welcomeDashboard);
	}

	private void EnsureAiPanelHost()
	{
		if (_aiPanelControl != null || AiPanelHostContainer == null)
		{
			return;
		}

		_aiPanelControl = new AiPanelControl
		{
			Visibility = Visibility.Collapsed
		};
		_aiPanelControl.ApplyTheme(AppThemeRegistry.Get(_appPreferences.ThemeName));
		_aiPanelControl.LoadSettings(_aiSettings);
		AiPanelHostContainer.Children.Clear();
		AiPanelHostContainer.Children.Add(_aiPanelControl);
		HookAiPanelEvents();
	}

	private void EnsureMainRibbonHost()
	{
		if (_mainRibbon != null || RibbonHostContainer == null)
		{
			return;
		}

		_mainRibbon = new MainRibbon();
		RibbonHostContainer.Children.Clear();
		RibbonHostContainer.Children.Add(_mainRibbon);
		HookMainRibbonEvents();

		// Sync initial state and listen to IsMinimized changes
		if (BtnKeepToolsActive != null)
		{
			BtnKeepToolsActive.IsChecked = _mainRibbon.KeepToolsActive;
		}
		if (BtnToggleRibbon != null)
		{
			BtnToggleRibbon.IsChecked = _mainRibbon.MyRibbon.IsMinimized;
		}

		var descriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
			Fluent.Ribbon.IsMinimizedProperty, typeof(Fluent.Ribbon));
		if (descriptor != null)
		{
			descriptor.AddValueChanged(_mainRibbon.MyRibbon, (s, ev) =>
			{
				if (BtnToggleRibbon != null)
				{
					BtnToggleRibbon.IsChecked = _mainRibbon.MyRibbon.IsMinimized;
				}
			});
		}
	}

	private void WelcomeDashboard_OpenRequested(object? sender, EventArgs e)
	{
		OpenPdf_Click(this, new RoutedEventArgs());
	}

	private async void WelcomeDashboard_MergeRequested(object? sender, EventArgs e)
	{
		await ShowMergeDialogAsync(null, autoStartMerge: false, sortByName: false, openMergedExternally: false);
	}

	private void WelcomeDashboard_PrintRequested(object? sender, EventArgs e)
	{
		PrintPdf_Click(this, new RoutedEventArgs());
	}

	private void WelcomeDashboard_AiSnapshotRequested(object? sender, EventArgs e)
	{
		AiSnapshotTool_Click(this, new RoutedEventArgs());
	}

	private void WelcomeDashboard_CompressRequested(object? sender, EventArgs e)
	{
		BatchCompress_Click(this, new RoutedEventArgs());
	}

	private void WelcomeDashboard_SettingsRequested(object? sender, EventArgs e)
	{
		Settings_Click(this, new RoutedEventArgs());
	}

	private void WelcomeDashboard_OpenRecentRequested(string path)
	{
		OpenPdfTab(path);
	}

	private void AiPanel_CloseRequested(object? sender, EventArgs e)
	{
		CloseAiPanel_Click(sender, new RoutedEventArgs());
	}

	private void AiPanel_SettingsChanged(object? sender, EventArgs e)
	{
		_aiSettings = ReadAiSettingsFromUi();
		_aiSnapshotRouter = new AiSnapshotRouter(_aiSettings);
	}

	private async void AiPanel_CheckAiRequested(object? sender, EventArgs e)
	{
		CheckAi_Click(sender, new RoutedEventArgs());
	}

	private void AiPanel_SaveAiRequested(object? sender, EventArgs e)
	{
		SaveAiSettings_Click(sender, new RoutedEventArgs());
	}

	private void ExtractPages_Click(object sender, RoutedEventArgs e)
	{
		if (!EnsureActivated())
		{
			return;
		}
		PdfDocumentTab activeTab = GetActiveTab();
		string? initialFile = activeTab?.CurrentPdfPath;

		SplitDialog splitDialog = new SplitDialog(initialFile) { Owner = this };
		if (splitDialog.ShowDialog() == true)
		{
			// If split dialog completes successfully, user might want to check the files
			// In SplitDialog we show success MessageBox and close.
		}
	}

	public static List<int> ParsePageRange(string rangeStr, int maxPage)
	{
		HashSet<int> hashSet = new HashSet<int>();
		string[] array = rangeStr.Split(new char[3] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i].Trim();
			int result3;
			if (text.Contains('-'))
			{
				string[] array2 = text.Split('-');
				if (array2.Length != 2 || !int.TryParse(array2[0], out var result) || !int.TryParse(array2[1], out var result2))
				{
					continue;
				}
				int num = Math.Min(result, result2);
				int num2 = Math.Max(result, result2);
				for (int j = num; j <= num2; j++)
				{
					if (j >= 1 && j <= maxPage)
					{
						hashSet.Add(j);
					}
				}
			}
			else if (int.TryParse(text, out result3) && result3 >= 1 && result3 <= maxPage)
			{
				hashSet.Add(result3);
			}
		}
		return hashSet.OrderBy((int p) => p).ToList();
	}

	// ─────────────────────────────────────────────────────────────────
	// Silent Auto-Update khi đóng ứng dụng
	// ─────────────────────────────────────────────────────────────────

	protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
	{
		base.OnClosing(e);

		// Không can thiệp nếu đã đang apply hoặc bị cancel từ nơi khác
		if (e.Cancel || _silentUpdateApplying)
			return;

		var silentState = AppUpdateService.LoadSilentUpdateState();
		if (silentState == null)
			return;

		// Chặn đóng app ngay lập tức để tiến hành cài đặt ngầm
		e.Cancel = true;
		_silentUpdateApplying = true;

		_ = Dispatcher.InvokeAsync(async () =>
		{
			try
			{
				await RunSilentUpdateOnCloseAsync(silentState);
			}
			catch (Exception ex)
			{
				_silentUpdateApplying = false;
				AppUpdateService.ClearSilentUpdateState();
				App.SendCrashTelemetry(ex);
				// Nếu lỗi: đóng app bình thường, không cài đặt
				Application.Current.Shutdown();
			}
		});
	}

	private async Task RunSilentUpdateOnCloseAsync(SilentUpdateReadyState silentState)
	{
		try
		{
			LogStatus($"🔄 Đang chuẩn bị cài đặt bản cập nhật v{silentState.TargetVersion}...");

			using var httpClient = new System.Net.Http.HttpClient();
			var updateService = new AppUpdateService(httpClient, ActivationLicense.ApiUpdateUrl);

			string baseDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
			var rollbackState = AppUpdateService.CreateRollbackState(
				ActivationLicense.AppVersion,
				silentState.TargetVersion,
				baseDirectory,
				silentState.DownloadZipPath);

			LogStatus("💾 Đang sao lưu bản hiện tại...");
			await updateService.CreateInstallationBackupAsync(baseDirectory, rollbackState.BackupZipPath);
			AppUpdateService.SaveRollbackState(rollbackState);

			// Xây dựng PowerShell apply-update script (tái sử dụng script từ UpdateDialog)
			string scriptPath = System.IO.Path.Combine(
				System.IO.Path.GetDirectoryName(rollbackState.StateFilePath) ?? System.IO.Path.GetTempPath(),
				"apply-update.ps1");
			System.IO.File.WriteAllText(scriptPath, BuildSilentUpdateScript(), System.Text.Encoding.UTF8);

			LogStatus($"🚀 Đang khởi chạy cài đặt tự động v{silentState.TargetVersion}...");
			await Task.Delay(600); // Cho phép UI render status cuối

			// Xóa trạng thái silent update trước khi thoát
			AppUpdateService.ClearSilentUpdateState();

			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
			{
				FileName = "powershell.exe",
				Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\" -InstallDir \"{rollbackState.InstallDirectory}\" -BackupZip \"{rollbackState.BackupZipPath}\" -UpdateZip \"{rollbackState.DownloadZipPath}\" -MarkerPath \"{rollbackState.ConfirmationMarkerPath}\" -AppExe \"{rollbackState.AppExecutablePath}\" -TargetVersion \"{silentState.TargetVersion}\" -ParentPid {Environment.ProcessId} -TimeoutSeconds 120",
				UseShellExecute = false,
				CreateNoWindow = true,
				WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
			});

			Application.Current.Shutdown();
		}
		catch
		{
			_silentUpdateApplying = false;
			AppUpdateService.ClearSilentUpdateState();
			// Thất bại → đóng app bình thường, không treo
			Application.Current.Shutdown();
		}
	}

	private static string BuildSilentUpdateScript()
	{
		// Tái sử dụng hoàn toàn script giống UpdateDialog.BuildRollbackScript()
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
$LogPath = Join-Path $AttemptDir ""apply-update.log""
Start-Transcript -Path $LogPath -Append -ErrorAction SilentlyContinue

function Remove-Tree([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        Get-ChildItem -LiteralPath $Path -Force -ErrorAction SilentlyContinue | ForEach-Object {
            if ($_.PSIsContainer) { Remove-Tree $_.FullName }
            else { try { Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue } catch {} }
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
    } catch { return $null }
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
                if (-not $parentProc.HasExited) { Stop-Process -Id $ParentPid -Force -ErrorAction SilentlyContinue }
            }
        } catch {}
    }

    Write-Host ""Stopping other PdfViewerApp instances...""
    Get-Process -Name ""PdfViewerApp"" -ErrorAction SilentlyContinue | Where-Object { $_.Id -ne $PID } | ForEach-Object {
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 1

    if (-not (Test-Path -LiteralPath $BackupZip)) { throw ""Rollback backup not found: $BackupZip"" }
    if (-not (Test-Path -LiteralPath $UpdateZip)) { throw ""Update package not found: $UpdateZip"" }

    Write-Host ""Extracting update package $UpdateZip to $InstallDir...""
    Expand-Archive -LiteralPath $UpdateZip -DestinationPath $InstallDir -Force

    $installedVersion = Get-AppFileVersion $AppExe
    if (-not $installedVersion) { throw ""Could not read installed app version after update."" }

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
        if (Test-Path -LiteralPath $MarkerPath) { $confirmed = $true; break }
        if ($proc.HasExited) { break }
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

