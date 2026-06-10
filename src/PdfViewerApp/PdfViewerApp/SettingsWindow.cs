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
		AiAllowOnlineCheckBox.IsChecked = _aiSettings.AllowOnlineSnapshot;
		AiEnableTelemetryCheckBox.IsChecked = _aiSettings.EnableTelemetry;
		AiEnableUpdateCheckCheckBox.IsChecked = _aiSettings.EnableUpdateCheck;
		AiEnableSilentUpdateCheckBox.IsChecked = _aiSettings.EnableSilentUpdate;
		AiProviderModeComboBox.SelectedItem = FindComboItemByTag(AiProviderModeComboBox, _aiSettings.ProviderMode) ?? AiProviderModeComboBox.Items[0];
		AiGeminiApiKeyTextBox.Text = _aiSettings.GeminiApiKey;
		AiGeminiModelTextBox.Text = _aiSettings.GeminiModel;
		AiOpenAiApiKeyTextBox.Text = _aiSettings.OpenAiApiKey;
		AiOpenAiModelTextBox.Text = _aiSettings.OpenAiModel;
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

	private void Save_Click(object sender, RoutedEventArgs e)
	{
		_preferences.IsDarkTheme = DarkThemeRadio.IsChecked == true;
		_preferences.AllowMultipleInstances = AllowMultipleInstancesCheckBox.IsChecked == true;
		_preferences.Save();

		_aiSettings.ProviderMode = (AiProviderModeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Auto";
		_aiSettings.AllowOnlineSnapshot = AiAllowOnlineCheckBox.IsChecked == true;
		_aiSettings.EnableTelemetry = AiEnableTelemetryCheckBox.IsChecked == true;
		_aiSettings.EnableUpdateCheck = AiEnableUpdateCheckCheckBox.IsChecked == true;
		_aiSettings.EnableSilentUpdate = AiEnableSilentUpdateCheckBox.IsChecked == true;
		_aiSettings.GeminiApiKey = AiGeminiApiKeyTextBox.Text?.Trim() ?? string.Empty;
		_aiSettings.GeminiModel = string.IsNullOrWhiteSpace(AiGeminiModelTextBox.Text) ? "auto" : AiGeminiModelTextBox.Text.Trim();
		_aiSettings.OpenAiApiKey = AiOpenAiApiKeyTextBox.Text?.Trim() ?? string.Empty;
		_aiSettings.OpenAiModel = string.IsNullOrWhiteSpace(AiOpenAiModelTextBox.Text) ? "gpt-4.1" : AiOpenAiModelTextBox.Text.Trim();
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
