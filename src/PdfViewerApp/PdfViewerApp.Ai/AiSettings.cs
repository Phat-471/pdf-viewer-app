using System;
using System.IO;
using System.Text.Json;

namespace PdfViewerApp.Ai;

internal sealed class AiSettings
{
	public string ProviderMode { get; set; } = "Auto";

	public bool AllowOnlineSnapshot { get; set; } = true;

	public string GeminiApiKey { get; set; } = string.Empty;

	public string OpenAiApiKey { get; set; } = string.Empty;

	public string GeminiModel { get; set; } = "auto";

	public string OpenAiModel { get; set; } = "gpt-4.1";

	public bool EnableTelemetry { get; set; }

	public bool EnableUpdateCheck { get; set; } = true;
	
	public bool EnableSilentUpdate { get; set; } = false;


	public static string SettingsPath
	{
		get
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PdfPro");
			Directory.CreateDirectory(text);
			return Path.Combine(text, "ai-settings.json");
		}
	}

	public static AiSettings Load()
	{
		try
		{
			string settingsPath = SettingsPath;
			if (File.Exists(settingsPath))
			{
				return JsonSerializer.Deserialize<AiSettings>(File.ReadAllText(settingsPath)) ?? new AiSettings();
			}
		}
		catch
		{
		}
		return new AiSettings();
	}

	public void Save()
	{
		JsonSerializerOptions options = new JsonSerializerOptions
		{
			WriteIndented = true
		};
		File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, options));
	}
}
