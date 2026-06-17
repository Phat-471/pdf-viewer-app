using System;
using System.Text.Json;

namespace TestApp
{
    internal sealed class AppPreferences
    {
        private string? _themeName = null;

        public string ThemeName
        {
            get => _themeName ?? "Dark";
            set => _themeName = value;
        }

        public bool IsDarkTheme
        {
            get => true;
            set
            {
                if (_themeName == null)
                {
                    _themeName = value ? "Dark" : "Light";
                }
            }
        }

        public bool AllowMultipleInstances { get; set; } = true;
        public string OcrLanguage { get; set; } = "";
    }

    class Program
    {
        static void Main()
        {
            var prefs = new AppPreferences { ThemeName = "Sunset" };
            var json = JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);
        }
    }
}
