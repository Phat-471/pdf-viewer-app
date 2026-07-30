using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Fluent;

namespace PdfViewerApp;

public partial class MainRibbon : UserControl
{
	// Lưu theme hiện tại để có thể re-apply sau khi Ribbon render
	private AppThemeDefinition? _currentTheme;

	// Dummy properties for compatibility with deleted Ribbon elements
	public System.Windows.Controls.ComboBox? FontFamilyCombo2 => null;
	public System.Windows.Controls.ComboBox? FontSizeCombo2 => null;
	public System.Windows.Controls.Primitives.ToggleButton? BoldToggle2 => null;
	public System.Windows.Controls.Primitives.ToggleButton? ItalicToggle2 => null;
	public System.Windows.Controls.Primitives.ToggleButton? UnderlineToggle2 => null;
	public System.Windows.Controls.Primitives.ToggleButton? StrikeToggle2 => null;
	public System.Windows.Controls.Primitives.ToggleButton? SubscriptToggle2 => null;
	public System.Windows.Controls.Button? FontGrowBtn2 => null;
	public System.Windows.Controls.Button? FontShrinkBtn2 => null;

	public bool KeepToolsActive
	{
		get => KeepToolsActiveCheckBox?.IsChecked == true;
		set
		{
			if (KeepToolsActiveCheckBox != null)
			{
				KeepToolsActiveCheckBox.IsChecked = value;
			}
		}
	}

	public event RoutedEventHandler? OpenPdfRequested;

	public event RoutedEventHandler? SavePdfRequested;

	public event RoutedEventHandler? SavePdfAsRequested;

	public event RoutedEventHandler? ExitRequested;

	public event RoutedEventHandler? PrintPdfRequested;

	public event RoutedEventHandler? BatchPrintRequested;

	public event RoutedEventHandler? ZoomInRequested;

	public event RoutedEventHandler? ZoomOutRequested;

	public event RoutedEventHandler? FitWidthRequested;

	public event RoutedEventHandler? SelectTextToolRequested;

	public event RoutedEventHandler? EditTextToolRequested;

	public event RoutedEventHandler? EditOriginalFontRequested;

	private Fluent.ToggleButton? _editOriginalFontButton;
	public event RoutedEventHandler? OcrTextRequested;

	public event RoutedEventHandler? ExportOcrTextRequested;

	public event RoutedEventHandler? ExportSearchablePdfRequested;

	public event RoutedEventHandler? ToggleSidebarRequested;

	public event RoutedEventHandler? ThemeToggleRequested;

	public event RoutedEventHandler? SettingsRequested;

	public event RoutedEventHandler? MergeFilesRequested;

	public event RoutedEventHandler? MergeFromExplorerRequested;

	public event RoutedEventHandler? ComparePdfsRequested;

	public event RoutedEventHandler? CompressPdfRequested;
	public event RoutedEventHandler? BatchCompressRequested;

	public event RoutedEventHandler? WatermarkRequested;

	public event RoutedEventHandler? PageNumberingRequested;

	public event RoutedEventHandler? ExtractImagesRequested;

	public event RoutedEventHandler? PdfSecurityRequested;

	public event RoutedEventHandler? RotateLeftRequested;

	public event RoutedEventHandler? RotateLeftAllRequested;

	public event RoutedEventHandler? RotateRightRequested;

	public event RoutedEventHandler? RotateRightAllRequested;

	public event RoutedEventHandler? PageOrganizerRequested;

	public event RoutedEventHandler? MovePageUpRequested;

	public event RoutedEventHandler? MovePageDownRequested;

	public event RoutedEventHandler? ReversePageOrderRequested;

	public event RoutedEventHandler? ResetPageOrderRequested;

	public event RoutedEventHandler? DeletePageRequested;

	public event RoutedEventHandler? InsertBlankPageRequested;

	public event RoutedEventHandler? DuplicatePageRequested;

	public event RoutedEventHandler? SplitCurrentPageRequested;

	public event RoutedEventHandler? ExtractPagesRequested;

	public event RoutedEventHandler? SelectToolRequested;

	public event RoutedEventHandler? InkToolRequested;

	public event RoutedEventHandler? RectToolRequested;

	public event RoutedEventHandler? OvalToolRequested;

	public event RoutedEventHandler? LineToolRequested;

	public event RoutedEventHandler? TextBoxToolRequested;

	public event RoutedEventHandler? CalloutToolRequested;

	public event RoutedEventHandler? StickyNoteToolRequested;

	public event RoutedEventHandler? HighlightToolRequested;

	public event RoutedEventHandler? SnapshotToolRequested;

	public event RoutedEventHandler? AiSnapshotToolRequested;

	public event RoutedEventHandler? KeepToolsActiveChanged;

	public event RoutedEventHandler? ActivationRequested;

	public event RoutedEventHandler? CheckLibrariesRequested;

	public event RoutedEventHandler? ShowPerformanceTraceRequested;

	public event RoutedEventHandler? ManualUpdateCheckRequested;

	public event RoutedEventHandler? RestorePreviousVersionRequested;

	public event RoutedEventHandler? ShowPdfDiagnosticsRequested;

	public event RoutedEventHandler? AboutRequested;

	public event RoutedEventHandler? UserGuideRequested;

	public event RoutedEventHandler? FeedbackRequested;

	public event RoutedEventHandler? VirtualPrinterConfigRequested;

	public event RoutedEventHandler? MeasureDistanceToolRequested;

	public event RoutedEventHandler? MeasureAreaToolRequested;

	public event RoutedEventHandler? MeasurePerimeterToolRequested;

	public event RoutedEventHandler? MeasureGuideRequested;

	public event RoutedEventHandler? HandwriteSignRequested;

	public event RoutedEventHandler? ImageSignRequested;

	public event RoutedEventHandler? StampApproveRequested;

	public event SelectionChangedEventHandler? MeasurementScaleChanged;

	public event RoutedEventHandler? CalibrateScaleRequested;

	public event EventHandler? SettingsChanged;

	public event Action<string>? OpenUrlRequested;

	public event RoutedEventHandler? PasteRequested;

	public event RoutedEventHandler? CutRequested;

	public event RoutedEventHandler? CopyRequested;

	public event RoutedEventHandler? FormatRequested;

	public event RoutedEventHandler? BulletListRequested;

	public event RoutedEventHandler? NumberListRequested;

	public MainRibbon()
	{
		InitializeComponent();
		InitializeEditTextContextGroup();
	}

	public void ApplyTheme(bool isDark)
	{
		ApplyTheme(AppThemeRegistry.Get(AppThemeRegistry.FromLegacyBool(isDark)));
	}

	internal void ApplyTheme(AppThemeDefinition theme)
	{
		_currentTheme = theme;
		bool isDark = !theme.IsLight;
		PdfPerfLogger.Log($"ApplyTheme: Starting theme update to {theme.Name} (isDark={isDark}, AccentColor={theme.AccentColor})");

		if (ThemeToggleIcon != null)
		{
			// Đổi font chữ sang font Emoji của Windows
			ThemeToggleIcon.FontFamily = new FontFamily("Segoe UI Emoji");
			
			// Gọi thuộc tính Icon (chứa Emoji "🌅") thay vì Glyph
			ThemeToggleIcon.Text = theme.Icon; 
			
			// Giữ nguyên dòng này hoặc bỏ đi đều được vì Emoji thường có màu sẵn
			ThemeToggleIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.ThemeBadgeIconColor));
			
			PdfPerfLogger.Log("ApplyTheme: ThemeToggleIcon updated with Emoji.");
		}

		if (ThemeIconBadge != null)
		{
			ThemeIconBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.ThemeBadgeBackground));
			ThemeIconBadge.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.ThemeBadgeBorder));
			if (ThemeIconBadge.Effect is System.Windows.Media.Effects.DropShadowEffect glow)
			{
				glow.Color = (Color)ColorConverter.ConvertFromString(theme.ThemeBadgeGlowColor);
				glow.Opacity = theme.IsLight ? 0.15 : 0.8; // Dịu nhẹ bóng đổ trên nền sáng, phát sáng mạnh trên nền tối
				glow.BlurRadius = theme.IsLight ? 6 : 8;   // Điều chỉnh bán kính mờ phù hợp cho bóng đổ/phát sáng
			}
		}
		if (ThemeToggleBtn != null)
		{
			ThemeToggleBtn.IsChecked = isDark;
			ThemeToggleBtn.Header = theme.DisplayName;
			PdfPerfLogger.Log("ApplyTheme: ThemeToggleBtn updated.");
		}

		if (MyBackstage != null)
		{
			MyBackstage.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.AccentDark));
			MyBackstage.Foreground = Brushes.White;
			PdfPerfLogger.Log("ApplyTheme: MyBackstage background updated.");
		}

		if (MyRibbon != null)
		{
			var bgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.TitleBarBackground));
			MyRibbon.Background = bgBrush;
			MyRibbon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.ForegroundPrimary));
			MyRibbon.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.BorderColor));

			// Override Fluent.Ribbon resource brushes globally and locally using correct keys
			string[] keys = new string[]
			{
				"Fluent.Ribbon.Brushes.Ribbon.Background",
				"Fluent.Ribbon.Brushes.RibbonTabControl.Background",
				"Fluent.Ribbon.Brushes.RibbonTabControl.Content.Background",
				"Fluent.Ribbon.Brushes.RibbonTabControl.TabsGrid.Background",
				"Fluent.Ribbon.Brushes.WindowBackground"
			};

			foreach (var key in keys)
			{
				try
				{
					Application.Current.Resources[key] = bgBrush;
				}
				catch (Exception ex)
				{
					PdfPerfLogger.Log($"ApplyTheme Error setting global resource key {key}: {ex.Message}");
				}
				try
				{
					MyRibbon.Resources[key] = bgBrush;
				}
				catch (Exception ex)
				{
					PdfPerfLogger.Log($"ApplyTheme Error setting local resource key {key}: {ex.Message}");
				}
			}
			PdfPerfLogger.Log("ApplyTheme: MyRibbon base colors and resources updated.");

			// CRITICAL FIX: Schedule icon color update via Dispatcher to ensure all tabs are rendered first.
			// Fluent Ribbon uses virtualization - tabs not yet activated may not have visual tree.
			var accentBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.AccentColor));
			Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
			{
				ApplyIconColors(accentBrush);
			}));
		}
	}

	/// <summary>Cập nhật màu foreground cho tất cả icon TextBlock trong Ribbon.</summary>
	private void ApplyIconColors(SolidColorBrush accentBrush)
	{
		if (MyRibbon == null) return;
		try
		{
			var textBlocks = FindVisualChildren<TextBlock>(MyRibbon).ToList();
			PdfPerfLogger.Log($"ApplyIconColors: Found {textBlocks.Count} TextBlocks in visual tree.");
			int updatedIconsCount = 0;
			foreach (var tb in textBlocks)
			{
				// Bỏ qua ThemeToggleIcon - để giữ đúng emoji màu gốc của theme
				if (tb.Name == "ThemeToggleIcon" || tb.Tag?.ToString() == "ThemeToggleIcon" || tb == ThemeToggleIcon) continue;

				string? fontSource = tb.FontFamily?.Source;
				if (fontSource == null) continue;

				bool isMdl2 = fontSource.IndexOf("Segoe MDL2 Assets", StringComparison.OrdinalIgnoreCase) >= 0;
				bool isSegoeUi = fontSource.IndexOf("Segoe UI", StringComparison.OrdinalIgnoreCase) >= 0;

				if (isMdl2)
				{
					// Segoe MDL2 Assets = all icon font, always update foreground
					tb.Foreground = accentBrush;
					updatedIconsCount++;
				}
				else if (isSegoeUi && !string.IsNullOrEmpty(tb.Text))
				{
					// Segoe UI: only update if text contains unicode symbols (code point > U+00FF)
					// Skip plain ASCII labels like "REV", "0", text descriptions, etc.
					bool hasUnicodeSymbol = false;
					foreach (char c in tb.Text) { if (c > 0xFF) { hasUnicodeSymbol = true; break; } }
					if (hasUnicodeSymbol)
					{
						tb.Foreground = accentBrush;
						updatedIconsCount++;
					}
				}
			}
			PdfPerfLogger.Log($"ApplyIconColors: Successfully updated {updatedIconsCount} icon TextBlock foregrounds.");
		}
		catch (Exception ex)
		{
			PdfPerfLogger.Log($"ApplyIconColors: Error updating TextBlock icon foregrounds: {ex.Message}");
		}
	}

	public void SelectMeasureAndSignTab()
	{
		if (MyRibbon != null)
		{
			// Tab index changes due to insertion of a new tab
			MyRibbon.SelectedTabIndex = 4;
		}
	}

	public void SetContextualTabVisibility(bool isVisible)
	{
		if (CommentFormatGroup != null)
		{
			CommentFormatGroup.Visibility = (isVisible ? Visibility.Visible : Visibility.Collapsed);
			if (isVisible && MyRibbon != null && CommentFormatTab != null)
			{
				MyRibbon.SelectedTabItem = CommentFormatTab;
			}
		}
	}

	public void SetActiveTool(string activeTool)
	{
		if (SelectTextToolBtn != null)
		{
			SelectTextToolBtn.IsChecked = activeTool == "SelectText";
		}
		if (EditTextToolBtn != null)
		{
			EditTextToolBtn.IsChecked = activeTool == "EditText";
		}
		if (_editTextContextGroup != null)
		{
			_editTextContextGroup.Visibility = (activeTool == "EditText" ? Visibility.Visible : Visibility.Collapsed);
		}
		if (SelectToolBtn != null)
		{
			SelectToolBtn.IsChecked = activeTool == "Select";
		}
		if (InkToolBtn != null)
		{
			InkToolBtn.IsChecked = activeTool == "Ink";
		}
		if (RectToolBtn != null)
		{
			RectToolBtn.IsChecked = activeTool == "ShapeRect";
		}
		if (OvalToolBtn != null)
		{
			OvalToolBtn.IsChecked = activeTool == "ShapeOval";
		}
		if (LineToolBtn != null)
		{
			LineToolBtn.IsChecked = activeTool == "ShapeLine";
		}
		if (TextBoxToolBtn != null)
		{
			TextBoxToolBtn.IsChecked = activeTool == "TextBox";
		}
		if (CalloutToolBtn != null)
		{
			CalloutToolBtn.IsChecked = activeTool == "Callout";
		}
		if (StickyNoteToolBtn != null)
		{
			StickyNoteToolBtn.IsChecked = activeTool == "StickyNote";
		}
		if (HighlightToolBtn != null)
		{
			HighlightToolBtn.IsChecked = activeTool == "Highlight";
		}
		if (SnapshotToolBtn != null)
		{
			SnapshotToolBtn.IsChecked = activeTool == "Snapshot";
		}
		if (AiSnapshotBtn != null)
		{
			AiSnapshotBtn.IsChecked = activeTool == "AiSnapshot";
		}
		if (MeasureDistanceToolBtn != null)
		{
			MeasureDistanceToolBtn.IsChecked = activeTool == "MeasureDistance";
		}
		if (MeasureAreaToolBtn != null)
		{
			MeasureAreaToolBtn.IsChecked = activeTool == "MeasureArea";
		}
		if (MeasurePerimeterToolBtn != null)
		{
			MeasurePerimeterToolBtn.IsChecked = activeTool == "MeasurePerimeter";
		}
	}

	public void SetActivationState(bool isActivated)
	{
		double opacity = (isActivated ? 1.0 : 0.4);
		string? toolTip = (isActivated ? null : "Tính năng PRO (Yêu cầu kích hoạt bản quyền)");
		
		#if DEBUG
		System.Windows.MessageBox.Show($"SetActivationState: isActivated={isActivated}\n" +
			$"MergeFilesBtn: {MergeFilesBtn != null}\n" +
			$"CompressPdfBtn: {CompressPdfBtn != null}\n" +
			$"BatchCompressBtn: {BatchCompressBtn != null}");
		#endif

		ApplyProButtonState(MergeFilesBtn, opacity, toolTip);
		ApplyProButtonState(MergeFromExplorerBtn, opacity, toolTip);
		ApplyProButtonState(MovePageUpBtn, opacity, toolTip);
		ApplyProButtonState(MovePageDownBtn, opacity, toolTip);
		ApplyProButtonState(ReversePageOrderBtn, opacity, toolTip);
		ApplyProButtonState(ResetPageOrderBtn, opacity, toolTip);
		ApplyProButtonState(DeletePageBtn, opacity, toolTip);
		ApplyProButtonState(InsertBlankPageBtn, opacity, toolTip);
		ApplyProButtonState(SplitCurrentPageBtn, opacity, toolTip);
		ApplyProButtonState(ExtractPagesBtn, opacity, toolTip);
		ApplyProButtonState(AiSnapshotBtn, opacity, toolTip);
		ApplyProButtonState(CompressPdfBtn, opacity, toolTip);
		ApplyProButtonState(BatchCompressBtn, opacity, toolTip);
	}

	public (string FontFamily, double FontSize, bool Bold, bool Italic, bool Underline, bool Strikeout, bool Subscript, bool Superscript, System.Windows.TextAlignment Alignment, Color StrokeColor, Color BackgroundColor, double Opacity) ReadAnnotationSettings()
	{
		var combo = (FontFamilyCombo2 != null && FontFamilyCombo2.SelectedIndex >= 0) ? FontFamilyCombo2 : FontFamilyCombo;
		string fontFamily = GetComboTag(combo, "Segoe UI");
		double fontSize = 14.0;
		var sizeCombo = (FontSizeCombo2 != null && FontSizeCombo2.SelectedIndex >= 0) ? FontSizeCombo2 : FontSizeCombo;
		if (sizeCombo != null && sizeCombo.SelectedItem is ComboBoxItem { Tag: string tag } && double.TryParse(tag, out var parsedFontSize))
		{
			fontSize = parsedFontSize;
		}

		bool bold = (BoldToggle2 != null && BoldToggle2.IsChecked == true) || (BoldToggle != null && BoldToggle.IsChecked == true);
		bool italic = (ItalicToggle2 != null && ItalicToggle2.IsChecked == true) || (ItalicToggle != null && ItalicToggle.IsChecked == true);
		bool underline = (UnderlineToggle2 != null && UnderlineToggle2.IsChecked == true) || (UnderlineToggle != null && UnderlineToggle.IsChecked == true);
		bool strikeout = (StrikeToggle2 != null && StrikeToggle2.IsChecked == true) || (StrikeToggle != null && StrikeToggle.IsChecked == true);
		bool subscript = (SubscriptToggle2 != null && SubscriptToggle2.IsChecked == true) || (SubscriptToggle != null && SubscriptToggle.IsChecked == true);
		bool superscript = SuperscriptToggle != null && SuperscriptToggle.IsChecked == true;

		System.Windows.TextAlignment alignment = System.Windows.TextAlignment.Left;
		if (AlignCenterToggle != null && AlignCenterToggle.IsChecked == true) alignment = System.Windows.TextAlignment.Center;
		else if (AlignRightToggle != null && AlignRightToggle.IsChecked == true) alignment = System.Windows.TextAlignment.Right;
		else if (AlignJustifyToggle != null && AlignJustifyToggle.IsChecked == true) alignment = System.Windows.TextAlignment.Justify;

		return (
			fontFamily,
			fontSize,
			bold,
			italic,
			underline,
			strikeout,
			subscript,
			superscript,
			alignment,
			ParseColor(GetComboTag(StrokeColorCombo, "Red")),
			ParseColor(GetComboTag(BgColorCombo, "Transparent")),
			OpacitySpinner.Value / 100.0
		);
	}

	private static void ApplyProButtonState(System.Windows.Controls.Control control, double opacity, object? toolTip)
	{
		if (control == null)
		{
			return;
		}

		control.Opacity = opacity;
		control.ToolTip = toolTip;
	}

	public void UpdateFormattingControls(string fontFamily, double fontSize, bool bold, bool italic, bool underline, bool strikeout, bool subscript, bool superscript, System.Windows.TextAlignment alignment, Color strokeColor, Color bgColor, double opacity)
	{
		_isSyncingSettings = true;
		try
		{
			var combo = FontFamilyCombo2 ?? FontFamilyCombo;
			if (combo != null)
			{
				for (int i = 0; i < combo.Items.Count; i++)
				{
					if (combo.Items[i] is ComboBoxItem item && item.Tag?.ToString() == fontFamily)
					{
						combo.SelectedIndex = i;
						break;
					}
				}
				if (FontFamilyCombo != null && FontFamilyCombo2 != null)
				{
					FontFamilyCombo.SelectedIndex = combo.SelectedIndex;
					FontFamilyCombo2.SelectedIndex = combo.SelectedIndex;
				}
			}

			var sizeCombo = FontSizeCombo2 ?? FontSizeCombo;
			if (sizeCombo != null)
			{
				for (int i = 0; i < sizeCombo.Items.Count; i++)
				{
					if (sizeCombo.Items[i] is ComboBoxItem item && double.TryParse(item.Tag?.ToString(), out var sizeVal) && Math.Abs(sizeVal - fontSize) < 0.1)
					{
						sizeCombo.SelectedIndex = i;
						break;
					}
				}
				if (FontSizeCombo != null && FontSizeCombo2 != null)
				{
					FontSizeCombo.SelectedIndex = sizeCombo.SelectedIndex;
					FontSizeCombo2.SelectedIndex = sizeCombo.SelectedIndex;
				}
			}

			if (BoldToggle != null) BoldToggle.IsChecked = bold;
			if (BoldToggle2 != null) BoldToggle2.IsChecked = bold;

			if (ItalicToggle != null) ItalicToggle.IsChecked = italic;
			if (ItalicToggle2 != null) ItalicToggle2.IsChecked = italic;

			if (UnderlineToggle != null) UnderlineToggle.IsChecked = underline;
			if (UnderlineToggle2 != null) UnderlineToggle2.IsChecked = underline;

			if (StrikeToggle != null) StrikeToggle.IsChecked = strikeout;
			if (StrikeToggle2 != null) StrikeToggle2.IsChecked = strikeout;

			if (SubscriptToggle != null) SubscriptToggle.IsChecked = subscript;
			if (SubscriptToggle2 != null) SubscriptToggle2.IsChecked = subscript;

			if (SuperscriptToggle != null) SuperscriptToggle.IsChecked = superscript;

			if (AlignLeftToggle != null) AlignLeftToggle.IsChecked = (alignment == System.Windows.TextAlignment.Left);
			if (AlignCenterToggle != null) AlignCenterToggle.IsChecked = (alignment == System.Windows.TextAlignment.Center);
			if (AlignRightToggle != null) AlignRightToggle.IsChecked = (alignment == System.Windows.TextAlignment.Right);
			if (AlignJustifyToggle != null) AlignJustifyToggle.IsChecked = (alignment == System.Windows.TextAlignment.Justify);

			if (StrokeColorCombo != null)
			{
				string hexStroke = strokeColor.ToString();
				for (int i = 0; i < StrokeColorCombo.Items.Count; i++)
				{
					if (StrokeColorCombo.Items[i] is ComboBoxItem item)
					{
						string? tagColor = item.Tag?.ToString();
						if (tagColor != null && (tagColor.Equals(hexStroke, StringComparison.OrdinalIgnoreCase) || ParseColor(tagColor) == strokeColor))
						{
							StrokeColorCombo.SelectedIndex = i;
							break;
						}
					}
				}
			}

			if (BgColorCombo != null)
			{
				string hexBg = bgColor.ToString();
				for (int i = 0; i < BgColorCombo.Items.Count; i++)
				{
					if (BgColorCombo.Items[i] is ComboBoxItem item)
					{
						string? tagColor = item.Tag?.ToString();
						if (tagColor != null && (tagColor.Equals(hexBg, StringComparison.OrdinalIgnoreCase) || ParseColor(tagColor) == bgColor))
						{
							BgColorCombo.SelectedIndex = i;
							break;
						}
					}
				}
			}

			if (OpacitySpinner != null)
			{
				OpacitySpinner.Value = opacity * 100.0;
			}
		}
		finally
		{
			_isSyncingSettings = false;
		}
	}

	private void OpenPdf_Click(object sender, RoutedEventArgs e) => OpenPdfRequested?.Invoke(this, e);

	private void SavePdf_Click(object sender, RoutedEventArgs e) => SavePdfRequested?.Invoke(this, e);

	private void SavePdfAs_Click(object sender, RoutedEventArgs e) => SavePdfAsRequested?.Invoke(this, e);

	private void Exit_Click(object sender, RoutedEventArgs e) => ExitRequested?.Invoke(this, e);

	private void PrintPdf_Click(object sender, RoutedEventArgs e) => PrintPdfRequested?.Invoke(this, e);

	private void BatchPrint_Click(object sender, RoutedEventArgs e) => BatchPrintRequested?.Invoke(this, e);

	private void ZoomIn_Click(object sender, RoutedEventArgs e) => ZoomInRequested?.Invoke(this, e);

	private void ZoomOut_Click(object sender, RoutedEventArgs e) => ZoomOutRequested?.Invoke(this, e);

	private void FitWidth_Click(object sender, RoutedEventArgs e) => FitWidthRequested?.Invoke(this, e);

	private void SelectTextTool_Click(object sender, RoutedEventArgs e) => SelectTextToolRequested?.Invoke(this, e);

	private void EditTextTool_Click(object sender, RoutedEventArgs e) => EditTextToolRequested?.Invoke(this, e);

	private void EditOriginalFont_Click(object sender, RoutedEventArgs e) => EditOriginalFontRequested?.Invoke(this, e);

	public void SetEditOriginalFontState(bool enabled)
	{
		if (_editOriginalFontButton != null)
		{
			_editOriginalFontButton.IsChecked = enabled;
		}
	}

	private void OcrText_Click(object sender, RoutedEventArgs e) => OcrTextRequested?.Invoke(this, e);

	private void ExportOcrText_Click(object sender, RoutedEventArgs e) => ExportOcrTextRequested?.Invoke(this, e);

	private void ExportSearchablePdf_Click(object sender, RoutedEventArgs e) => ExportSearchablePdfRequested?.Invoke(this, e);

	private void ToggleSidebar_Click(object sender, RoutedEventArgs e) => ToggleSidebarRequested?.Invoke(this, e);

	private void ThemeToggle_Click(object sender, RoutedEventArgs e) => ThemeToggleRequested?.Invoke(this, e);

	private void Settings_Click(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, e);

	private void MergeFiles_Click(object sender, RoutedEventArgs e) => MergeFilesRequested?.Invoke(this, e);

	private void MergeFromExplorer_Click(object sender, RoutedEventArgs e) => MergeFromExplorerRequested?.Invoke(this, e);

	private void ComparePdfs_Click(object sender, RoutedEventArgs e) => ComparePdfsRequested?.Invoke(this, e);

	private void CompressPdf_Click(object sender, RoutedEventArgs e) => CompressPdfRequested?.Invoke(this, e);
	private void BatchCompress_Click(object sender, RoutedEventArgs e) => BatchCompressRequested?.Invoke(this, e);

	private void Watermark_Click(object sender, RoutedEventArgs e) => WatermarkRequested?.Invoke(this, e);

	private void PageNumbering_Click(object sender, RoutedEventArgs e) => PageNumberingRequested?.Invoke(this, e);

	private void ExtractImages_Click(object sender, RoutedEventArgs e) => ExtractImagesRequested?.Invoke(this, e);

	private void PdfSecurity_Click(object sender, RoutedEventArgs e) => PdfSecurityRequested?.Invoke(this, e);

	private void RotateLeft_Click(object sender, RoutedEventArgs e) => RotateLeftRequested?.Invoke(this, e);

	private void RotateLeftAll_Click(object sender, RoutedEventArgs e) => RotateLeftAllRequested?.Invoke(this, e);

	private void RotateRight_Click(object sender, RoutedEventArgs e) => RotateRightRequested?.Invoke(this, e);

	private void RotateRightAll_Click(object sender, RoutedEventArgs e) => RotateRightAllRequested?.Invoke(this, e);

	private void PageOrganizer_Click(object sender, RoutedEventArgs e) => PageOrganizerRequested?.Invoke(this, e);

	private void MovePageUp_Click(object sender, RoutedEventArgs e) => MovePageUpRequested?.Invoke(this, e);

	private void MovePageDown_Click(object sender, RoutedEventArgs e) => MovePageDownRequested?.Invoke(this, e);

	private void ReversePageOrder_Click(object sender, RoutedEventArgs e) => ReversePageOrderRequested?.Invoke(this, e);

	private void ResetPageOrder_Click(object sender, RoutedEventArgs e) => ResetPageOrderRequested?.Invoke(this, e);

	private void DeletePage_Click(object sender, RoutedEventArgs e) => DeletePageRequested?.Invoke(this, e);

	private void InsertBlankPage_Click(object sender, RoutedEventArgs e) => InsertBlankPageRequested?.Invoke(this, e);

	private void DuplicatePage_Click(object sender, RoutedEventArgs e) => DuplicatePageRequested?.Invoke(this, e);

	private void SplitCurrentPage_Click(object sender, RoutedEventArgs e) => SplitCurrentPageRequested?.Invoke(this, e);

	private void ExtractPages_Click(object sender, RoutedEventArgs e) => ExtractPagesRequested?.Invoke(this, e);

	private void SelectTool_Click(object sender, RoutedEventArgs e) => SelectToolRequested?.Invoke(this, e);

	private void InkTool_Click(object sender, RoutedEventArgs e) => InkToolRequested?.Invoke(this, e);

	private void RectTool_Click(object sender, RoutedEventArgs e) => RectToolRequested?.Invoke(this, e);

	private void OvalTool_Click(object sender, RoutedEventArgs e) => OvalToolRequested?.Invoke(this, e);

	private void LineTool_Click(object sender, RoutedEventArgs e) => LineToolRequested?.Invoke(this, e);

	private void TextBoxTool_Click(object sender, RoutedEventArgs e) => TextBoxToolRequested?.Invoke(this, e);

	private void CalloutTool_Click(object sender, RoutedEventArgs e) => CalloutToolRequested?.Invoke(this, e);

	private void StickyNoteTool_Click(object sender, RoutedEventArgs e) => StickyNoteToolRequested?.Invoke(this, e);

	private void HighlightTool_Click(object sender, RoutedEventArgs e) => HighlightToolRequested?.Invoke(this, e);

	private void SnapshotTool_Click(object sender, RoutedEventArgs e) => SnapshotToolRequested?.Invoke(this, e);

	private void AiSnapshotTool_Click(object sender, RoutedEventArgs e) => AiSnapshotToolRequested?.Invoke(this, e);

	private bool _isSyncingSettings = false;
	private void SyncAnnotationSettings(object sender)
	{
		if (_isSyncingSettings) return;
		_isSyncingSettings = true;
		try
		{
			if (sender == FontFamilyCombo && FontFamilyCombo2 != null) FontFamilyCombo2.SelectedIndex = FontFamilyCombo.SelectedIndex;
			else if (sender == FontFamilyCombo2 && FontFamilyCombo != null) FontFamilyCombo.SelectedIndex = FontFamilyCombo2.SelectedIndex;

			if (sender == FontSizeCombo && FontSizeCombo2 != null) FontSizeCombo2.SelectedIndex = FontSizeCombo.SelectedIndex;
			else if (sender == FontSizeCombo2 && FontSizeCombo != null) FontSizeCombo.SelectedIndex = FontSizeCombo2.SelectedIndex;

			if (sender == BoldToggle && BoldToggle2 != null) BoldToggle2.IsChecked = BoldToggle.IsChecked;
			else if (sender == BoldToggle2 && BoldToggle != null) BoldToggle.IsChecked = BoldToggle2.IsChecked;

			if (sender == ItalicToggle && ItalicToggle2 != null) ItalicToggle2.IsChecked = ItalicToggle.IsChecked;
			else if (sender == ItalicToggle2 && ItalicToggle != null) ItalicToggle.IsChecked = ItalicToggle2.IsChecked;

			if (sender == UnderlineToggle && UnderlineToggle2 != null) UnderlineToggle2.IsChecked = UnderlineToggle.IsChecked;
			else if (sender == UnderlineToggle2 && UnderlineToggle != null) UnderlineToggle.IsChecked = UnderlineToggle2.IsChecked;

			if (sender == StrikeToggle && StrikeToggle2 != null) StrikeToggle2.IsChecked = StrikeToggle.IsChecked;
			else if (sender == StrikeToggle2 && StrikeToggle != null) StrikeToggle.IsChecked = StrikeToggle2.IsChecked;

			if (sender == SubscriptToggle && SubscriptToggle2 != null) SubscriptToggle2.IsChecked = SubscriptToggle.IsChecked;
			else if (sender == SubscriptToggle2 && SubscriptToggle != null) SubscriptToggle.IsChecked = SubscriptToggle2.IsChecked;

			if (sender == AlignLeftToggle || sender == AlignCenterToggle || sender == AlignRightToggle || sender == AlignJustifyToggle)
			{
				if (AlignLeftToggle != null) AlignLeftToggle.IsChecked = (sender == AlignLeftToggle);
				if (AlignCenterToggle != null) AlignCenterToggle.IsChecked = (sender == AlignCenterToggle);
				if (AlignRightToggle != null) AlignRightToggle.IsChecked = (sender == AlignRightToggle);
				if (AlignJustifyToggle != null) AlignJustifyToggle.IsChecked = (sender == AlignJustifyToggle);
			}
		}
		finally
		{
			_isSyncingSettings = false;
		}
		SettingsChanged?.Invoke(this, EventArgs.Empty);
	}

	private void FontFamilyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => SyncAnnotationSettings(sender);

	private void FontSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => SyncAnnotationSettings(sender);

	private void BoldToggle_Click(object sender, RoutedEventArgs e) => SyncAnnotationSettings(sender);

	private void ItalicToggle_Click(object sender, RoutedEventArgs e) => SyncAnnotationSettings(sender);

	private void UnderlineToggle_Click(object sender, RoutedEventArgs e) => SyncAnnotationSettings(sender);

	private void StrikeToggle_Click(object sender, RoutedEventArgs e) => SyncAnnotationSettings(sender);

	private void SubscriptToggle_Click(object sender, RoutedEventArgs e) => SyncAnnotationSettings(sender);

	private void SuperscriptToggle_Click(object sender, RoutedEventArgs e) => SyncAnnotationSettings(sender);

	private void AlignLeftToggle_Click(object sender, RoutedEventArgs e) => SyncAnnotationSettings(sender);

	private void AlignCenterToggle_Click(object sender, RoutedEventArgs e) => SyncAnnotationSettings(sender);

	private void AlignRightToggle_Click(object sender, RoutedEventArgs e) => SyncAnnotationSettings(sender);

	private void AlignJustifyToggle_Click(object sender, RoutedEventArgs e) => SyncAnnotationSettings(sender);

	private void BulletListBtn_Click(object sender, RoutedEventArgs e) => BulletListRequested?.Invoke(this, e);

	private void NumberListBtn_Click(object sender, RoutedEventArgs e) => NumberListRequested?.Invoke(this, e);

	private void FontFamilyCombo2_SelectionChanged(object sender, SelectionChangedEventArgs e) => SyncAnnotationSettings(sender);

	private void FontSizeCombo2_SelectionChanged(object sender, SelectionChangedEventArgs e) => SyncAnnotationSettings(sender);

	private void BoldToggle2_Click(object sender, RoutedEventArgs e) => SyncAnnotationSettings(sender);

	private void ItalicToggle2_Click(object sender, RoutedEventArgs e) => SyncAnnotationSettings(sender);

	private void UnderlineToggle2_Click(object sender, RoutedEventArgs e) => SyncAnnotationSettings(sender);

	private void Paste_Click(object sender, RoutedEventArgs e) => PasteRequested?.Invoke(this, e);

	private void Cut_Click(object sender, RoutedEventArgs e) => CutRequested?.Invoke(this, e);

	private void Copy_Click(object sender, RoutedEventArgs e) => CopyRequested?.Invoke(this, e);

	private void Format_Click(object sender, RoutedEventArgs e) => FormatRequested?.Invoke(this, e);

	private void StrokeColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => SettingsChanged?.Invoke(this, EventArgs.Empty);

	private void BgColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => SettingsChanged?.Invoke(this, EventArgs.Empty);

	private void OpacitySpinner_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => SettingsChanged?.Invoke(this, EventArgs.Empty);

	private void ManualUpdateCheck_Click(object sender, RoutedEventArgs e) => ManualUpdateCheckRequested?.Invoke(this, e);

	private void Activation_Click(object sender, RoutedEventArgs e) => ActivationRequested?.Invoke(this, e);

	private void CheckLibraries_Click(object sender, RoutedEventArgs e) => CheckLibrariesRequested?.Invoke(this, e);

	private void ShowPerformanceTrace_Click(object sender, RoutedEventArgs e) => ShowPerformanceTraceRequested?.Invoke(this, e);

	private void RestorePreviousVersion_Click(object sender, RoutedEventArgs e) => RestorePreviousVersionRequested?.Invoke(this, e);

	private void ShowPdfDiagnostics_Click(object sender, RoutedEventArgs e) => ShowPdfDiagnosticsRequested?.Invoke(this, e);

	private void About_Click(object sender, RoutedEventArgs e) => AboutRequested?.Invoke(this, e);

	private void UserGuide_Click(object sender, RoutedEventArgs e) => UserGuideRequested?.Invoke(this, e);

	private void Feedback_Click(object sender, RoutedEventArgs e) => FeedbackRequested?.Invoke(this, e);

	private void VirtualPrinterConfig_Click(object sender, RoutedEventArgs e) => VirtualPrinterConfigRequested?.Invoke(this, e);

	private void MeasureDistanceTool_Click(object sender, RoutedEventArgs e) => MeasureDistanceToolRequested?.Invoke(this, e);

	private void MeasureAreaTool_Click(object sender, RoutedEventArgs e) => MeasureAreaToolRequested?.Invoke(this, e);

	private void MeasurePerimeterTool_Click(object sender, RoutedEventArgs e) => MeasurePerimeterToolRequested?.Invoke(this, e);

	private void MeasureGuide_Click(object sender, RoutedEventArgs e) => MeasureGuideRequested?.Invoke(this, e);

	private void KeepToolsActiveCheckBox_Click(object sender, RoutedEventArgs e) => KeepToolsActiveChanged?.Invoke(this, e);

	private void FontGrowBtn2_Click(object sender, RoutedEventArgs e)
	{
		if (FontSizeCombo2 != null && FontSizeCombo2.SelectedIndex < FontSizeCombo2.Items.Count - 1)
		{
			FontSizeCombo2.SelectedIndex++;
		}
	}

	private void FontShrinkBtn2_Click(object sender, RoutedEventArgs e)
	{
		if (FontSizeCombo2 != null && FontSizeCombo2.SelectedIndex > 0)
		{
			FontSizeCombo2.SelectedIndex--;
		}
	}

	private void StrikeToggle2_Click(object sender, RoutedEventArgs e) => SyncAnnotationSettings(sender);
	private void SubscriptToggle2_Click(object sender, RoutedEventArgs e) => SyncAnnotationSettings(sender);

	private void HighlightColor_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Fluent.MenuItem menuItem && menuItem.Tag is string colorTag)
		{
			if (BgColorCombo != null)
			{
				foreach (ComboBoxItem item in BgColorCombo.Items)
				{
					if (item.Tag?.ToString() == colorTag)
					{
						BgColorCombo.SelectedItem = item;
						break;
					}
				}
			}
			SettingsChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	private void TextColor_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Fluent.MenuItem menuItem && menuItem.Tag is string colorTag)
		{
			if (StrokeColorCombo != null)
			{
				foreach (ComboBoxItem item in StrokeColorCombo.Items)
				{
					if (item.Tag?.ToString() == colorTag)
					{
						StrokeColorCombo.SelectedItem = item;
						break;
					}
				}
			}
			SettingsChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	private void CalibrateScale_Click(object sender, RoutedEventArgs e) => CalibrateScaleRequested?.Invoke(this, e);

	private void HandwriteSign_Click(object sender, RoutedEventArgs e) => HandwriteSignRequested?.Invoke(this, e);

	private void ImageSign_Click(object sender, RoutedEventArgs e) => ImageSignRequested?.Invoke(this, e);

	private void StampApprove_Click(object sender, RoutedEventArgs e) => StampApproveRequested?.Invoke(this, e);

	private void MeasurementScaleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => MeasurementScaleChanged?.Invoke(this, e);

	public double GetMeasurementScale()
	{
		if (MeasurementScaleCombo?.SelectedItem is ComboBoxItem { Tag: string tag } && double.TryParse(tag, out var parsedScale))
		{
			return parsedScale;
		}
		return 100.0; // Default 1:100
	}

	public void SetCustomScale(double scaleValue)
	{
		if (MeasurementScaleCombo == null) return;
		
		double rounded = Math.Round(scaleValue, 1);
		string customText = $"Tự chọn (1:{rounded})";
		string customTag = rounded.ToString(System.Globalization.CultureInfo.InvariantCulture);

		ComboBoxItem? customItem = null;
		foreach (var item in MeasurementScaleCombo.Items)
		{
			if (item is ComboBoxItem cbi && cbi.Content?.ToString()?.StartsWith("Tự chọn") == true)
			{
				customItem = cbi;
				break;
			}
		}

		if (customItem == null)
		{
			customItem = new ComboBoxItem();
			MeasurementScaleCombo.Items.Add(customItem);
		}

		customItem.Content = customText;
		customItem.Tag = customTag;
		MeasurementScaleCombo.SelectedItem = customItem;
	}

	private RibbonGroupBox? _editTextContextGroup;

	private void InitializeEditTextContextGroup()
	{
		if (_editTextContextGroup != null)
		{
			return;
		}
		RibbonTabItem? homeTab = FindVisualChildren<RibbonTabItem>(this).FirstOrDefault();
		if (homeTab == null)
		{
			return;
		}
		RibbonGroupBox group = new RibbonGroupBox
		{
			Header = "Edit Text",
			Visibility = Visibility.Collapsed
		};
		group.Items.Add(CreateEditTextButton("Select Text", "T", SelectTextTool_Click, "#106EBE"));
		group.Items.Add(CreateEditTextButton("Edit Text", "E", EditTextTool_Click, "#0F766E"));
		Fluent.ToggleButton keepFontBtn = new Fluent.ToggleButton
		{
			Header = "Sửa gốc (giữ font)",
			Margin = new Thickness(8.0, 0.0, 8.0, 0.0)
		};
		keepFontBtn.Click += EditOriginalFont_Click;
		_editOriginalFontButton = keepFontBtn;
		group.Items.Add(keepFontBtn);
		group.Items.Add(CreateEditTextButton("OCR", "OCR", OcrText_Click, "#D13438"));
		group.Items.Add(CreateEditTextButton("Xuất văn bản OCR", "TXT", ExportOcrText_Click, "#D13438"));
		group.Items.Add(CreateEditTextButton("Tạo Searchable PDF", "PDF", ExportSearchablePdf_Click, "#38BDF8"));
		group.Items.Add(CreateEditTextButton("Save", "S", SavePdf_Click, "#0F766E"));
		group.Items.Add(CreateEditTextButton("Exit", "X", SelectTool_Click, "#64748B"));
		homeTab.Groups.Add(group);
		_editTextContextGroup = group;
	}

	private static Fluent.Button CreateEditTextButton(string header, string glyph, RoutedEventHandler clickHandler, string foreground)
	{
		Fluent.Button button = new Fluent.Button
		{
			Header = header,
			Margin = new Thickness(8.0, 0.0, 8.0, 0.0)
		};
		button.Click += clickHandler;
		button.LargeIcon = new TextBlock
		{
			FontFamily = new FontFamily("Segoe UI"),
			Text = glyph,
			FontSize = 24.0,
			Foreground = (Brush)new BrushConverter().ConvertFromString(foreground),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		return button;
	}

	private void OpenGeminiApiKey_Click(object sender, RoutedEventArgs e) => OpenUrlRequested?.Invoke("https://aistudio.google.com/app/apikey");

	private void OpenOpenAiApiKey_Click(object sender, RoutedEventArgs e) => OpenUrlRequested?.Invoke("https://platform.openai.com/api-keys");

	private void OpenOllamaDownload_Click(object sender, RoutedEventArgs e) => OpenUrlRequested?.Invoke("https://ollama.com/download");

	private static string GetComboTag(System.Windows.Controls.ComboBox comboBox, string fallback)
	{
		if (!(comboBox.SelectedItem is ComboBoxItem { Tag: string tag }))
		{
			return fallback;
		}
		return tag;
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

	private static Color ParseColor(string colorName)
	{
		try
		{
			object obj = ColorConverter.ConvertFromString(colorName);
			if (obj is Color color)
			{
				return color;
			}
		}
		catch
		{
		}
		return Colors.Red;
	}

}

