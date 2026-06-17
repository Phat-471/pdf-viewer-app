using System;
using System.Text.Json;
using PdfViewerApp;

namespace Test
{
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
