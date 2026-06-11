using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using PdfViewerApp.Ai;

namespace PdfViewerApp;

public partial class AiPanelControl : UserControl, IComponentConnector
{
	public event EventHandler? CloseRequested;

	public event EventHandler? SettingsChanged;

	public event EventHandler? CheckAiRequested;

	public event EventHandler? SaveAiRequested;

	public event EventHandler<string>? OpenUrlRequested;

	public AiPanelControl()
	{
		InitializeComponent();
	}

	public void ShowPanel()
	{
		Visibility = Visibility.Visible;
	}

	public void HidePanel()
	{
		Visibility = Visibility.Collapsed;
	}

	internal void LoadSettings(AiSettings settings)
	{
		SelectComboItemByTag(AiProviderModeComboBox, settings.ProviderMode);
		AiAllowOnlineCheckBox.IsChecked = settings.AllowOnlineSnapshot;
		AiEnableTelemetryCheckBox.IsChecked = settings.EnableTelemetry;
		AiEnableUpdateCheckCheckBox.IsChecked = settings.EnableUpdateCheck;
		AiGeminiApiKeyTextBox.Text = settings.GeminiApiKey;
		
		SelectComboItemByTag(AiGeminiModelComboBox, settings.GeminiModel);
		if (AiGeminiModelComboBox.SelectedItem == null)
		{
			AiGeminiModelComboBox.Text = settings.GeminiModel;
		}
	}

	internal AiSettings ReadSettings()
	{
		string geminiModel = string.Empty;
		if (AiGeminiModelComboBox.SelectedItem is ComboBoxItem selectedModelItem)
		{
			geminiModel = selectedModelItem.Tag?.ToString() ?? AiGeminiModelComboBox.Text;
		}
		else
		{
			geminiModel = AiGeminiModelComboBox.Text;
		}

		return new AiSettings
		{
			ProviderMode = GetComboTag(AiProviderModeComboBox, "Auto"),
			AllowOnlineSnapshot = (AiAllowOnlineCheckBox.IsChecked == true),
			EnableTelemetry = (AiEnableTelemetryCheckBox.IsChecked == true),
			EnableUpdateCheck = (AiEnableUpdateCheckCheckBox.IsChecked == true),
			GeminiApiKey = (AiGeminiApiKeyTextBox.Text?.Trim() ?? string.Empty),
			GeminiModel = (string.IsNullOrWhiteSpace(geminiModel) ? "gemini-3.5-flash" : geminiModel.Trim()),
			// Keep existing settings fields unchanged under the hood
		};
	}

	public void SetOutput(string text)
	{
		AiOutputTextBox.Text = text;
	}

	public string PromptText => string.IsNullOrWhiteSpace(AiPromptTextBox.Text) ? "Hay doc va giai thich vung ban ve nay." : AiPromptTextBox.Text.Trim();

	private void Close_Click(object sender, RoutedEventArgs e)
	{
		CloseRequested?.Invoke(this, EventArgs.Empty);
	}

	private void QuickPrompt_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button { Tag: string prompt })
		{
			AiPromptTextBox.Text = prompt;
		}
	}

	private void AnySettingsChanged(object sender, RoutedEventArgs e)
	{
		SettingsChanged?.Invoke(this, EventArgs.Empty);
	}

	private void AnySettingsChanged(object sender, TextChangedEventArgs e)
	{
		SettingsChanged?.Invoke(this, EventArgs.Empty);
	}

	private void AnySettingsChanged(object sender, SelectionChangedEventArgs e)
	{
		SettingsChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OpenGeminiApiKey_Click(object sender, RoutedEventArgs e)
	{
		OpenUrlRequested?.Invoke(this, "https://aistudio.google.com/app/apikey");
	}

	private void OpenOpenAiApiKey_Click(object sender, RoutedEventArgs e)
	{
		OpenUrlRequested?.Invoke(this, "https://platform.openai.com/api-keys");
	}

	private void OpenOllamaDownload_Click(object sender, RoutedEventArgs e)
	{
		OpenUrlRequested?.Invoke(this, "https://ollama.com/download");
	}

	private void CheckAi_Click(object sender, RoutedEventArgs e)
	{
		CheckAiRequested?.Invoke(this, EventArgs.Empty);
	}

	private void SaveAiSettings_Click(object sender, RoutedEventArgs e)
	{
		SaveAiRequested?.Invoke(this, EventArgs.Empty);
	}

	private static string GetComboTag(ComboBox comboBox, string fallback)
	{
		if (!(comboBox.SelectedItem is ComboBoxItem { Tag: string tag }))
		{
			return fallback;
		}
		return tag;
	}

	private static void SelectComboItemByTag(ComboBox comboBox, string tag)
	{
		foreach (object item in comboBox.Items)
		{
			if (item is ComboBoxItem { Tag: string tag2 } comboBoxItem && string.Equals(tag2, tag, StringComparison.OrdinalIgnoreCase))
			{
				comboBox.SelectedItem = comboBoxItem;
				return;
			}
		}
		comboBox.SelectedItem = null;
	}
}
