using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ControlzEx.Theming;
using PdfViewerApp.Ai;

namespace PdfViewerApp;

public partial class SettingsWindow : Window, IComponentConnector
{
	private readonly AppPreferences _preferences;

	private AiSettings _aiSettings;

	/// <summary>Tên theme đang được chọn tạm thời trong UI (chưa lưu).</summary>
	private string _selectedThemeName;

	public SettingsWindow()
	{
		InitializeComponent();
		_preferences = AppPreferences.Load();
		_aiSettings = AiSettings.Load();
		_selectedThemeName = _preferences.ThemeName;
		RefreshUi();
	}

	private void RefreshUi()
	{
		AppVersionTextBlock.Text = "v" + ActivationLicense.AppVersion;
		ActivationState activationState = ActivationLicense.LoadState();
		LicenseStateTextBlock.Text = activationState.IsActivated ? "Đã kích hoạt" : "Chưa kích hoạt";
		LicenseStateTextBlock.Foreground = activationState.IsActivated ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 211, 153)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 113, 133));

		// Chọn đúng RadioButton theme card theo ThemeName
		SelectThemeRadio(_selectedThemeName);

		AllowMultipleInstancesCheckBox.IsChecked = _preferences.AllowMultipleInstances;

		OcrLanguageComboBox.Items.Clear();
		ComboBoxItem defaultItem = new ComboBoxItem { Content = "Mặc định hệ thống", Tag = "" };
		OcrLanguageComboBox.Items.Add(defaultItem);
		OcrLanguageComboBox.SelectedItem = defaultItem;

		try
		{
			foreach (var lang in Windows.Media.Ocr.OcrEngine.AvailableRecognizerLanguages)
			{
				ComboBoxItem item = new ComboBoxItem { Content = $"{lang.DisplayName} ({lang.LanguageTag})", Tag = lang.LanguageTag };
				OcrLanguageComboBox.Items.Add(item);
				if (string.Equals(lang.LanguageTag, _preferences.OcrLanguage, StringComparison.OrdinalIgnoreCase))
				{
					OcrLanguageComboBox.SelectedItem = item;
				}
			}
		}
		catch
		{
		}

		AiAllowOnlineCheckBox.IsChecked = _aiSettings.AllowOnlineSnapshot;
		AiEnableTelemetryCheckBox.IsChecked = _aiSettings.EnableTelemetry;
		AiEnableUpdateCheckCheckBox.IsChecked = _aiSettings.EnableUpdateCheck;
		AiEnableSilentUpdateCheckBox.IsChecked = _aiSettings.EnableSilentUpdate;
		AiProviderModeComboBox.SelectedItem = FindComboItemByTag(AiProviderModeComboBox, _aiSettings.ProviderMode) ?? AiProviderModeComboBox.Items[0];
		AiGeminiApiKeyTextBox.Text = _aiSettings.GeminiApiKey;
		
		ComboBoxItem? matchedModel = FindComboItemByTag(AiGeminiModelComboBox, _aiSettings.GeminiModel);
		if (matchedModel != null)
		{
			AiGeminiModelComboBox.SelectedItem = matchedModel;
		}
		else
		{
			AiGeminiModelComboBox.Text = _aiSettings.GeminiModel;
		}
	}

	/// <summary>Chọn RadioButton đúng theo tên theme.</summary>
	private void SelectThemeRadio(string themeName)
	{
		if (ThemeDarkRadio != null)    ThemeDarkRadio.IsChecked    = themeName == AppThemeRegistry.Dark;
		if (ThemeLightRadio != null)   ThemeLightRadio.IsChecked   = themeName == AppThemeRegistry.Light;
		if (ThemeMidnightRadio != null) ThemeMidnightRadio.IsChecked = themeName == AppThemeRegistry.Midnight;
		if (ThemeForestRadio != null)  ThemeForestRadio.IsChecked  = themeName == AppThemeRegistry.Forest;
		if (ThemeSunsetRadio != null)  ThemeSunsetRadio.IsChecked  = themeName == AppThemeRegistry.Sunset;
		if (ThemeOceanRadio != null)   ThemeOceanRadio.IsChecked   = themeName == AppThemeRegistry.Ocean;
		if (ThemeSakuraRadio != null)  ThemeSakuraRadio.IsChecked  = themeName == AppThemeRegistry.Sakura;
		if (ThemeMintRadio != null)    ThemeMintRadio.IsChecked    = themeName == AppThemeRegistry.Mint;
	}

	/// <summary>Xử lý khi người dùng click một theme card — preview ngay lập tức.</summary>
	private void ThemeRadio_Click(object sender, RoutedEventArgs e)
	{
		if (sender is RadioButton rb && rb.Tag is string tagName)
		{
			_selectedThemeName = tagName;
			// Preview ngay lập tức để người dùng thấy kết quả trước khi nhấn Lưu
			var mainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
			if (mainWindow != null)
			{
				mainWindow.PreviewTheme(tagName);
			}
		}
	}

	private static ComboBoxItem? FindComboItemByTag(ComboBox comboBox, string tag)
	{
		foreach (object item in comboBox.Items)
		{
			if (item is ComboBoxItem comboBoxItem && string.Equals(comboBoxItem.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
			{
				return comboBoxItem;
			}
		}

		return null;
	}

	private void OpenGeminiApiKey_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
			{
				FileName = "https://aistudio.google.com/app/apikey",
				UseShellExecute = true
			});
		}
		catch
		{
		}
	}

	private void Save_Click(object sender, RoutedEventArgs e)
	{
		// Lưu các thiết lập khác qua MainWindow
		bool allowInstances = AllowMultipleInstancesCheckBox.IsChecked == true;
		string ocrLang = (OcrLanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
		
		_aiSettings.ProviderMode = (AiProviderModeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Auto";
		_aiSettings.AllowOnlineSnapshot = AiAllowOnlineCheckBox.IsChecked == true;
		_aiSettings.EnableTelemetry = AiEnableTelemetryCheckBox.IsChecked == true;
		_aiSettings.EnableUpdateCheck = AiEnableUpdateCheckCheckBox.IsChecked == true;
		_aiSettings.EnableSilentUpdate = AiEnableSilentUpdateCheckBox.IsChecked == true;
		_aiSettings.GeminiApiKey = AiGeminiApiKeyTextBox.Text?.Trim() ?? string.Empty;
		
		string geminiModel = string.Empty;
		if (AiGeminiModelComboBox.SelectedItem is ComboBoxItem selectedModelItem)
		{
			geminiModel = selectedModelItem.Tag?.ToString() ?? AiGeminiModelComboBox.Text;
		}
		else
		{
			geminiModel = AiGeminiModelComboBox.Text;
		}
		_aiSettings.GeminiModel = string.IsNullOrWhiteSpace(geminiModel) ? "gemini-3.5-flash" : geminiModel.Trim();
		_aiSettings.Save();

		var mainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
		if (mainWindow != null)
		{
			mainWindow.UpdatePreferences(_selectedThemeName, allowInstances, ocrLang);
			mainWindow.ReloadAiSettings();
			mainWindow.RefreshRecentFilesDashboard();
		}

		try
		{
			var theme = AppThemeRegistry.Get(_selectedThemeName);
			ThemeManager.Current.ChangeTheme(Application.Current, theme.FluentTheme);
		}
		catch
		{
		}

		DialogResult = true;
		Close();
	}
}

