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

	public event RoutedEventHandler? OcrTextRequested;

	public event RoutedEventHandler? ToggleSidebarRequested;

	public event RoutedEventHandler? ThemeToggleRequested;

	public event RoutedEventHandler? SettingsRequested;

	public event RoutedEventHandler? MergeFilesRequested;

	public event RoutedEventHandler? MergeFromExplorerRequested;

	public event RoutedEventHandler? RotateLeftRequested;

	public event RoutedEventHandler? RotateLeftAllRequested;

	public event RoutedEventHandler? RotateRightRequested;

	public event RoutedEventHandler? RotateRightAllRequested;

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

	public event RoutedEventHandler? SnapshotToolRequested;

	public event RoutedEventHandler? AiSnapshotToolRequested;

	public event RoutedEventHandler? ActivationRequested;

	public event RoutedEventHandler? CheckLibrariesRequested;

	public event RoutedEventHandler? ShowPerformanceTraceRequested;

	public event RoutedEventHandler? ManualUpdateCheckRequested;

	public event RoutedEventHandler? RestorePreviousVersionRequested;

	public event RoutedEventHandler? AboutRequested;

	public event RoutedEventHandler? UserGuideRequested;

	public event RoutedEventHandler? FeedbackRequested;

	public event RoutedEventHandler? VirtualPrinterConfigRequested;

	public event RoutedEventHandler? MeasureDistanceToolRequested;

	public event RoutedEventHandler? MeasureAreaToolRequested;

	public event RoutedEventHandler? HandwriteSignRequested;

	public event RoutedEventHandler? StampApproveRequested;

	public event SelectionChangedEventHandler? MeasurementScaleChanged;

	public event RoutedEventHandler? CalibrateScaleRequested;

	public event EventHandler? SettingsChanged;

	public event Action<string>? OpenUrlRequested;

	public MainRibbon()
	{
		InitializeComponent();
		InitializeEditTextContextGroup();
	}

	public void ApplyTheme(bool isDark)
	{
		if (ThemeToggleIcon != null)
		{
			ThemeToggleIcon.Text = (isDark ? "\ue706" : "\ue708");
			ThemeToggleIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isDark ? "#38BDF8" : "#0F766E"));
		}

		if (ThemeToggleBtn != null)
		{
			ThemeToggleBtn.IsChecked = isDark;
			ThemeToggleBtn.Header = (isDark ? "Tối" : "Sáng");
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
	}

	public void SetActivationState(bool isActivated)
	{
		double opacity = (isActivated ? 1.0 : 0.4);
		string? toolTip = (isActivated ? null : "Tính năng PRO (Yêu cầu kích hoạt bản quyền)");
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
	}

	public (string FontFamily, double FontSize, bool Bold, bool Italic, bool Underline, Color StrokeColor, Color BackgroundColor, double Opacity) ReadAnnotationSettings()
	{
		string fontFamily = GetComboTag(FontFamilyCombo, "Segoe UI");
		double fontSize = 14.0;
		if (FontSizeCombo.SelectedItem is ComboBoxItem { Tag: string tag } && double.TryParse(tag, out var parsedFontSize))
		{
			fontSize = parsedFontSize;
		}

		return (
			fontFamily,
			fontSize,
			BoldToggle.IsChecked == true,
			ItalicToggle.IsChecked == true,
			UnderlineToggle.IsChecked == true,
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

	private void OcrText_Click(object sender, RoutedEventArgs e) => OcrTextRequested?.Invoke(this, e);

	private void ToggleSidebar_Click(object sender, RoutedEventArgs e) => ToggleSidebarRequested?.Invoke(this, e);

	private void ThemeToggle_Click(object sender, RoutedEventArgs e) => ThemeToggleRequested?.Invoke(this, e);

	private void Settings_Click(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, e);

	private void MergeFiles_Click(object sender, RoutedEventArgs e) => MergeFilesRequested?.Invoke(this, e);

	private void MergeFromExplorer_Click(object sender, RoutedEventArgs e) => MergeFromExplorerRequested?.Invoke(this, e);

	private void RotateLeft_Click(object sender, RoutedEventArgs e) => RotateLeftRequested?.Invoke(this, e);

	private void RotateLeftAll_Click(object sender, RoutedEventArgs e) => RotateLeftAllRequested?.Invoke(this, e);

	private void RotateRight_Click(object sender, RoutedEventArgs e) => RotateRightRequested?.Invoke(this, e);

	private void RotateRightAll_Click(object sender, RoutedEventArgs e) => RotateRightAllRequested?.Invoke(this, e);

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

	private void SnapshotTool_Click(object sender, RoutedEventArgs e) => SnapshotToolRequested?.Invoke(this, e);

	private void AiSnapshotTool_Click(object sender, RoutedEventArgs e) => AiSnapshotToolRequested?.Invoke(this, e);

	private void FontFamilyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => SettingsChanged?.Invoke(this, EventArgs.Empty);

	private void FontSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => SettingsChanged?.Invoke(this, EventArgs.Empty);

	private void BoldToggle_Click(object sender, RoutedEventArgs e) => SettingsChanged?.Invoke(this, EventArgs.Empty);

	private void ItalicToggle_Click(object sender, RoutedEventArgs e) => SettingsChanged?.Invoke(this, EventArgs.Empty);

	private void UnderlineToggle_Click(object sender, RoutedEventArgs e) => SettingsChanged?.Invoke(this, EventArgs.Empty);

	private void StrokeColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => SettingsChanged?.Invoke(this, EventArgs.Empty);

	private void BgColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => SettingsChanged?.Invoke(this, EventArgs.Empty);

	private void OpacitySpinner_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => SettingsChanged?.Invoke(this, EventArgs.Empty);

	private void ManualUpdateCheck_Click(object sender, RoutedEventArgs e) => ManualUpdateCheckRequested?.Invoke(this, e);

	private void Activation_Click(object sender, RoutedEventArgs e) => ActivationRequested?.Invoke(this, e);

	private void CheckLibraries_Click(object sender, RoutedEventArgs e) => CheckLibrariesRequested?.Invoke(this, e);

	private void ShowPerformanceTrace_Click(object sender, RoutedEventArgs e) => ShowPerformanceTraceRequested?.Invoke(this, e);

	private void RestorePreviousVersion_Click(object sender, RoutedEventArgs e) => RestorePreviousVersionRequested?.Invoke(this, e);

	private void About_Click(object sender, RoutedEventArgs e) => AboutRequested?.Invoke(this, e);

	private void UserGuide_Click(object sender, RoutedEventArgs e) => UserGuideRequested?.Invoke(this, e);

	private void Feedback_Click(object sender, RoutedEventArgs e) => FeedbackRequested?.Invoke(this, e);

	private void VirtualPrinterConfig_Click(object sender, RoutedEventArgs e) => VirtualPrinterConfigRequested?.Invoke(this, e);

	private void MeasureDistanceTool_Click(object sender, RoutedEventArgs e) => MeasureDistanceToolRequested?.Invoke(this, e);

	private void MeasureAreaTool_Click(object sender, RoutedEventArgs e) => MeasureAreaToolRequested?.Invoke(this, e);

	private void CalibrateScale_Click(object sender, RoutedEventArgs e) => CalibrateScaleRequested?.Invoke(this, e);

	private void HandwriteSign_Click(object sender, RoutedEventArgs e) => HandwriteSignRequested?.Invoke(this, e);

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
		group.Items.Add(CreateEditTextButton("OCR", "OCR", OcrText_Click, "#D13438"));
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

