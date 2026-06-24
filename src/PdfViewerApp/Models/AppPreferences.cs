using System;
using System.IO;
using System.Text.Json;

namespace PdfViewerApp;

internal sealed class AppPreferences
{
	private string? _themeName = null;

	/// <summary>
	/// Tên theme hiện tại (Dark, Light, Midnight, Forest, Sunset, Ocean).
	/// </summary>
	public string ThemeName
	{
		get => _themeName ?? AppThemeRegistry.Dark;
		set => _themeName = value;
	}

	/// <summary>
	/// Tương thích ngược với phiên bản cũ.
	/// Khi load: nếu file JSON cũ chỉ có IsDarkTheme thì dùng giá trị đó.
	/// </summary>
	public bool IsDarkTheme
	{
		get => !AppThemeRegistry.Get(ThemeName).IsLight;
		set
		{
			if (_themeName == null)
			{
				_themeName = AppThemeRegistry.FromLegacyBool(value);
			}
		}
	}

	public bool AllowMultipleInstances { get; set; } = false;

	public string OcrLanguage { get; set; } = "";

	public bool EnhanceThinLines { get; set; } = true;


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
