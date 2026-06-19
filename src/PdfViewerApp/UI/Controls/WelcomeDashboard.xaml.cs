using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace PdfViewerApp;

public partial class WelcomeDashboard : UserControl, IComponentConnector
{
	private readonly ObservableCollection<RecentFileItem> _recentFiles = new ObservableCollection<RecentFileItem>();

	public event EventHandler? OpenRequested;

	public event EventHandler? MergeRequested;

	public event EventHandler? PrintRequested;

	public event EventHandler? AiSnapshotRequested;

	public event EventHandler? SettingsRequested;

	public event Action<string>? OpenRecentRequested;

	public WelcomeDashboard()
	{
		InitializeComponent();
		RecentFilesItemsControl.ItemsSource = _recentFiles;
		RefreshEmptyState();
	}

	public void SetRecentFiles(IEnumerable<string> recentFiles)
	{
		_recentFiles.Clear();
		foreach (string path in recentFiles.Where(File.Exists))
		{
			_recentFiles.Add(new RecentFileItem(path));
		}
		RefreshEmptyState();
	}

	private void RefreshEmptyState()
	{
		if (EmptyRecentPanel == null || RecentFilesItemsControl == null)
		{
			return;
		}

		bool hasItems = _recentFiles.Count > 0;
		EmptyRecentPanel.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
		RecentFilesItemsControl.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
	}

	private void OpenFile_Click(object sender, RoutedEventArgs e)
	{
		OpenRequested?.Invoke(this, EventArgs.Empty);
	}

	private void MergeFile_Click(object sender, RoutedEventArgs e)
	{
		MergeRequested?.Invoke(this, EventArgs.Empty);
	}

	private void PrintFile_Click(object sender, RoutedEventArgs e)
	{
		PrintRequested?.Invoke(this, EventArgs.Empty);
	}

	private void AiSnapshot_Click(object sender, RoutedEventArgs e)
	{
		AiSnapshotRequested?.Invoke(this, EventArgs.Empty);
	}

	private void Settings_Click(object sender, RoutedEventArgs e)
	{
		SettingsRequested?.Invoke(this, EventArgs.Empty);
	}

	private void RecentFile_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement frameworkElement && frameworkElement.Tag is string path)
		{
			OpenRecentRequested?.Invoke(path);
		}
	}

	public void ApplyTheme(bool isDark)
	{
		ApplyTheme(AppThemeRegistry.Get(AppThemeRegistry.FromLegacyBool(isDark)));
	}

	internal void ApplyTheme(AppThemeDefinition theme)
	{
		this.Resources["DashboardBtnBg"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.PanelBackground));
		this.Resources["DashboardBtnFg"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.ForegroundPrimary));
		this.Resources["DashboardBtnBorder"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.BorderColor));
		this.Resources["DashboardBtnHoverBg"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.HoverBackground));
		this.Resources["DashboardBtnHoverBorder"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.AccentColor));
		this.Resources["DashboardBtnPressedBg"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.SurfaceBackground));

		this.Resources["RecentFileBtnBg"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.PanelBackground));
		this.Resources["RecentFileBtnFg"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.ForegroundPrimary));
		this.Resources["RecentFileBtnBorder"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.BorderColor));
		this.Resources["RecentFileBtnHoverBg"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.HoverBackground));
		this.Resources["RecentFileBtnHoverBorder"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.AccentColor));
		this.Resources["RecentFileBtnPressedBg"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.SurfaceBackground));

		this.Resources["RecentFileIconBg"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.SurfaceBackground));
		this.Resources["RecentFileIconBorder"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.BorderColor));
		this.Resources["RecentFileTextFg"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.ForegroundPrimary));
		this.Resources["RecentFileTextDescFg"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.ForegroundSecondary));

		System.Windows.Media.Brush bgBrush;
		{
			var gradient = new System.Windows.Media.LinearGradientBrush();
			gradient.StartPoint = new System.Windows.Point(0, 0);
			gradient.EndPoint = new System.Windows.Point(1, 1);
			gradient.GradientStops.Add(new System.Windows.Media.GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.WindowBackground), 0));
			gradient.GradientStops.Add(new System.Windows.Media.GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.PanelBackground), 1));
			bgBrush = gradient;
		}

		var borderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.BorderColor));
		var innerBgBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.SurfaceBackground));

		System.Windows.Media.Brush rightBgBrush;
		{
			var gradient = new System.Windows.Media.LinearGradientBrush();
			gradient.StartPoint = new System.Windows.Point(0, 0);
			gradient.EndPoint = new System.Windows.Point(0, 1);
			gradient.GradientStops.Add(new System.Windows.Media.GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.SurfaceBackground), 0));
			gradient.GradientStops.Add(new System.Windows.Media.GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.PanelBackground), 1));
			rightBgBrush = gradient;
		}

		var textTitleBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.ForegroundPrimary));
		var textDescBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.ForegroundSecondary));

		if (MainPanelBorder != null)
		{
			MainPanelBorder.Background = bgBrush;
			MainPanelBorder.BorderBrush = borderBrush;
		}
		if (LogoBorder != null)
		{
			LogoBorder.Background = innerBgBrush;
			LogoBorder.BorderBrush = borderBrush;
		}
		if (WelcomeTitleText != null)
		{
			WelcomeTitleText.Foreground = textTitleBrush;
		}
		if (WelcomeSubtitleText != null)
		{
			WelcomeSubtitleText.Foreground = textDescBrush;
		}
		if (RecentFilesBorder != null)
		{
			RecentFilesBorder.Background = rightBgBrush;
			RecentFilesBorder.BorderBrush = borderBrush;
		}
		if (RecentTitleText != null)
		{
			RecentTitleText.Foreground = textTitleBrush;
		}
		if (RecentSubtitleText != null)
		{
			RecentSubtitleText.Foreground = textDescBrush;
		}
		if (EmptyRecentPanel != null)
		{
			EmptyRecentPanel.Background = innerBgBrush;
			EmptyRecentPanel.BorderBrush = borderBrush;
		}
		if (EmptyRecentText != null)
		{
			EmptyRecentText.Foreground = textTitleBrush;
		}
		if (EmptyRecentDetailText != null)
		{
			EmptyRecentDetailText.Foreground = textDescBrush;
		}
	}

	private sealed class RecentFileItem
	{
		public RecentFileItem(string fullPath)
		{
			FullPath = fullPath;
			FileName = Path.GetFileName(fullPath);
			DirectoryName = Path.GetDirectoryName(fullPath) ?? string.Empty;
		}

		public string FullPath { get; }

		public string FileName { get; }

		public string DirectoryName { get; }
	}
}
