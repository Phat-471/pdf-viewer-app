using System;
using System.IO;
using System.Text.Json;

namespace PdfViewerApp;

internal sealed class AppPreferences
{
	public bool IsDarkTheme { get; set; } = true;

	public bool AllowMultipleInstances { get; set; } = true;

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
