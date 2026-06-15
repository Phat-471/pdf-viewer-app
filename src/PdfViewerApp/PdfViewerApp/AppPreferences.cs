using System;
using System.IO;
using System.Text.Json;

namespace PdfViewerApp;

internal sealed class AppPreferences
{
	/// <summary>
	/// Tên theme hiện tại (Dark, Light, Midnight, Forest, Sunset, Ocean).
	/// Ưu tiên cao hơn IsDarkTheme (tương thích ngược).
	/// </summary>
	public string ThemeName { get; set; } = AppThemeRegistry.Dark;

	/// <summary>
	/// Tương thích ngược với phiên bản cũ.
	/// Khi load: nếu file JSON cũ chỉ có IsDarkTheme thì dùng giá trị đó.
	/// </summary>
	public bool IsDarkTheme
	{
		get => !AppThemeRegistry.Get(ThemeName).IsLight;
		set
		{
			if (ThemeName != AppThemeRegistry.Dark && ThemeName != AppThemeRegistry.Light)
			{
				return;
			}
			ThemeName = AppThemeRegistry.FromLegacyBool(value);
		}
	}

	public bool AllowMultipleInstances { get; set; } = true;

	public string OcrLanguage { get; set; } = "";

	public static string PreferencesPath
	{
		get
		{
			string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PdfPro");
			Directory.CreateDirectory(folder);
			return Path.Combine(folder, "app-preferences.json");
		}
	}

	public static AppPreferences Load()
	{
		try
		{
			string path = PreferencesPath;
			if (File.Exists(path))
			{
				return JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(path)) ?? new AppPreferences();
			}
		}
		catch
		{
		}

		return new AppPreferences();
	}

	public void Save()
	{
		JsonSerializerOptions options = new JsonSerializerOptions
		{
			WriteIndented = true
		};
		File.WriteAllText(PreferencesPath, JsonSerializer.Serialize(this, options));
	}
}
