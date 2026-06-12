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
		this.Resources["DashboardBtnBg"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isDark ? "#1E293B" : "#E2E8F0"));
		this.Resources["DashboardBtnFg"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isDark ? "#F8FAFC" : "#0F172A"));
		this.Resources["DashboardBtnBorder"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isDark ? "#475569" : "#CBD5E1"));
		this.Resources["DashboardBtnHoverBg"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isDark ? "#334155" : "#CBD5E1"));
		this.Resources["DashboardBtnHoverBorder"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isDark ? "#64748B" : "#94A3B8"));
		this.Resources["DashboardBtnPressedBg"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isDark ? "#0F172A" : "#94A3B8"));

		this.Resources["RecentFileBtnBg"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isDark ? "#111827" : "#F8FAFC"));
		this.Resources["RecentFileBtnFg"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isDark ? "#E2E8F0" : "#0F172A"));
		this.Resources["RecentFileBtnBorder"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isDark ? "#1E293B" : "#E2E8F0"));
		this.Resources["RecentFileBtnHoverBg"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isDark ? "#1E293B" : "#E2E8F0"));
		this.Resources["RecentFileBtnHoverBorder"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isDark ? "#334155" : "#CBD5E1"));
		this.Resources["RecentFileBtnPressedBg"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isDark ? "#0B1220" : "#E2E8F0"));

		this.Resources["RecentFileIconBg"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isDark ? "#0B1220" : "#E2E8F0"));
		this.Resources["RecentFileIconBorder"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isDark ? "#1E293B" : "#CBD5E1"));
		this.Resources["RecentFileTextFg"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isDark ? "#F8FAFC" : "#0F172A"));
		this.Resources["RecentFileTextDescFg"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isDark ? "#94A3B8" : "#475569"));

		System.Windows.Media.Brush bgBrush;
		if (isDark)
		{
			var gradient = new System.Windows.Media.LinearGradientBrush();
			gradient.StartPoint = new System.Windows.Point(0, 0);
			gradient.EndPoint = new System.Windows.Point(1, 1);
			gradient.GradientStops.Add(new System.Windows.Media.GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0F172A"), 0));
			gradient.GradientStops.Add(new System.Windows.Media.GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E293B"), 1));
			bgBrush = gradient;
		}
		else
		{
			var gradient = new System.Windows.Media.LinearGradientBrush();
			gradient.StartPoint = new System.Windows.Point(0, 0);
			gradient.EndPoint = new System.Windows.Point(1, 1);
			gradient.GradientStops.Add(new System.Windows.Media.GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF"), 0));
			gradient.GradientStops.Add(new System.Windows.Media.GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F8FAFC"), 1));
			bgBrush = gradient;
		}

		var borderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isDark ? "#1E293B" : "#CBD5E1"));
		var innerBgBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isDark ? "#0B1220" : "#F8FAFC"));

		System.Windows.Media.Brush rightBgBrush;
		if (isDark)
		{
			var gradient = new System.Windows.Media.LinearGradientBrush();
			gradient.StartPoint = new System.Windows.Point(0, 0);
			gradient.EndPoint = new System.Windows.Point(0, 1);
			gradient.GradientStops.Add(new System.Windows.Media.GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0B1220"), 0));
			gradient.GradientStops.Add(new System.Windows.Media.GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#111827"), 1));
			rightBgBrush = gradient;
		}
		else
		{
			var gradient = new System.Windows.Media.LinearGradientBrush();
			gradient.StartPoint = new System.Windows.Point(0, 0);
			gradient.EndPoint = new System.Windows.Point(0, 1);
			gradient.GradientStops.Add(new System.Windows.Media.GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F8FAFC"), 0));
			gradient.GradientStops.Add(new System.Windows.Media.GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E2E8F0"), 1));
			rightBgBrush = gradient;
		}

		var textTitleBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isDark ? "#F8FAFC" : "#0F172A"));
		var textDescBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isDark ? "#94A3B8" : "#475569"));

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
