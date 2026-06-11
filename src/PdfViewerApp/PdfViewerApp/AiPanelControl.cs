using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using PdfViewerApp.Ai;

namespace PdfViewerApp;

public class ChatMessage
{
	public string Role { get; set; } = string.Empty; // "user" hoặc "model"
	public string Text { get; set; } = string.Empty;
	public string? ImageBase64 { get; set; }
}

public partial class AiPanelControl : UserControl, IComponentConnector
{
	public event EventHandler? CloseRequested;

	public event EventHandler? SettingsChanged;

	public event EventHandler? CheckAiRequested;

	public event EventHandler? SaveAiRequested;

	public event EventHandler<string>? OpenUrlRequested;

	public event EventHandler<string>? SendChatRequested;

	private readonly List<ChatMessage> _chatHistory = new();

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

	public string SelectedScope => (ScopeWholePageRadio?.IsChecked == true) ? "WholePage" : "Snapshot";

	public List<ChatMessage> GetChatHistory() => _chatHistory;

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
		};
	}

	public void AddMessage(string role, string text, string? imageBase64 = null)
	{
		_chatHistory.Add(new ChatMessage { Role = role, Text = text, ImageBase64 = imageBase64 });

		bool isUser = string.Equals(role, "user", StringComparison.OrdinalIgnoreCase);

		Border bubbleBorder = new Border
		{
			CornerRadius = new CornerRadius(10),
			Padding = new Thickness(10, 8, 10, 8),
			Margin = new Thickness(isUser ? 40 : 0, 0, isUser ? 0 : 40, 8),
			HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
			Background = isUser ? new SolidColorBrush(Color.FromRgb(2, 132, 199)) : new SolidColorBrush(Color.FromRgb(30, 41, 59)),
			BorderBrush = isUser ? Brushes.Transparent : new SolidColorBrush(Color.FromRgb(51, 65, 85)),
			BorderThickness = new Thickness(1)
		};

		StackPanel contentPanel = new StackPanel();

		// Add thumbnail if present
		if (!string.IsNullOrEmpty(imageBase64))
		{
			try
			{
				byte[] binaryData = Convert.FromBase64String(imageBase64);
				BitmapImage bitmap = new BitmapImage();
				bitmap.BeginInit();
				bitmap.StreamSource = new MemoryStream(binaryData);
				bitmap.EndInit();

				Image img = new Image
				{
					Source = bitmap,
					MaxWidth = 180,
					MaxHeight = 120,
					Margin = new Thickness(0, 0, 0, 6),
					Stretch = Stretch.Uniform,
					HorizontalAlignment = HorizontalAlignment.Center
				};
				contentPanel.Children.Add(img);
			}
			catch
			{
			}
		}

		TextBlock textBlock = new TextBlock
		{
			Text = text,
			Foreground = Brushes.White,
			FontSize = 12,
			TextWrapping = TextWrapping.Wrap,
			LineHeight = 16
		};
		contentPanel.Children.Add(textBlock);

		bubbleBorder.Child = contentPanel;
		ChatMessagesStackPanel.Children.Add(bubbleBorder);
		ChatScrollViewer.ScrollToEnd();
	}

	public void SetOutput(string text)
	{
		AddMessage("model", text);
	}

	public string PromptText => string.IsNullOrWhiteSpace(AiPromptTextBox.Text) ? "Hãy đọc và giải thích vùng bản vẽ này." : AiPromptTextBox.Text.Trim();

	private void Close_Click(object sender, RoutedEventArgs e)
	{
		CloseRequested?.Invoke(this, EventArgs.Empty);
	}

	private void ToggleConfig_Click(object sender, RoutedEventArgs e)
	{
		if (AiConfigPanel.Visibility == Visibility.Visible)
		{
			AiConfigPanel.Visibility = Visibility.Collapsed;
		}
		else
		{
			AiConfigPanel.Visibility = Visibility.Visible;
		}
	}

	private void ClearChat_Click(object sender, RoutedEventArgs e)
	{
		_chatHistory.Clear();
		ChatMessagesStackPanel.Children.Clear();
		
		Border welcome = new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
			CornerRadius = new CornerRadius(8),
			Padding = new Thickness(12),
			Margin = new Thickness(0, 0, 0, 10)
		};
		welcome.Child = new TextBlock
		{
			Text = "Chào bạn! Tôi là trợ lý AI. Bạn có thể chọn phạm vi phân tích bên dưới, sau đó chụp một vùng bản vẽ hoặc hỏi trực tiếp về toàn bộ trang PDF đang mở.",
			Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
			FontSize = 12.5,
			TextWrapping = TextWrapping.Wrap,
			LineHeight = 17
		};
		ChatMessagesStackPanel.Children.Add(welcome);
	}

	private void Send_Click(object sender, RoutedEventArgs e)
	{
		string prompt = PromptText;
		if (string.IsNullOrWhiteSpace(prompt)) return;
		
		SendChatRequested?.Invoke(this, prompt);
		AiPromptTextBox.Text = string.Empty;
	}

	private void AiPromptTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
	{
		if (e.Key == System.Windows.Input.Key.Enter)
		{
			if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control) || 
				!System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
			{
				e.Handled = true;
				Send_Click(sender, new RoutedEventArgs());
			}
		}
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
