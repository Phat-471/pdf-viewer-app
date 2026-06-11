using System;
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

	public SettingsWindow()
	{
		InitializeComponent();
		_preferences = AppPreferences.Load();
		_aiSettings = AiSettings.Load();
		RefreshUi();
	}

	private void RefreshUi()
	{
		AppVersionTextBlock.Text = "v" + ActivationLicense.AppVersion;
		ActivationState activationState = ActivationLicense.LoadState();
		LicenseStateTextBlock.Text = activationState.IsActivated ? "Đã kích hoạt" : "Chưa kích hoạt";
		LicenseStateTextBlock.Foreground = activationState.IsActivated ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 211, 153)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 113, 133));
		DarkThemeRadio.IsChecked = _preferences.IsDarkTheme;
		LightThemeRadio.IsChecked = !_preferences.IsDarkTheme;
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
		_preferences.IsDarkTheme = DarkThemeRadio.IsChecked == true;
		_preferences.AllowMultipleInstances = AllowMultipleInstancesCheckBox.IsChecked == true;
		_preferences.OcrLanguage = (OcrLanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
		_preferences.Save();

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

		if (Application.Current.MainWindow is MainWindow mainWindow)
		{
			mainWindow.ApplyThemeFromPreferences(_preferences.IsDarkTheme);
			mainWindow.ReloadAiSettings();
			mainWindow.RefreshRecentFilesDashboard();
		}

		try
		{
			ThemeManager.Current.ChangeTheme(Application.Current, _preferences.IsDarkTheme ? "Dark.Blue" : "Light.Blue");
		}
		catch
		{
		}

		DialogResult = true;
		Close();
	}
}
