#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;

namespace PdfViewerApp;

// Capture the bytes once: rendering, undo, saving and printing use the same font,
// even if the installed file is later replaced or removed.
internal sealed record PdfNativeTextFont(string Name, string DataBase64, string Characters);

internal static class PdfTextFontSource
{
    private static string Key(string name)
    {
        if (name.Length > 7 && name[6] == '+' && name.Take(6).All(c => c is >= 'A' and <= 'Z')) name = name[7..];
        string key = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return key.EndsWith("regular", StringComparison.Ordinal) ? key[..^7] : key;
    }

    internal static bool Matches(string original, string fullName) => Key(original) == Key(fullName);

    internal static PdfNativeTextFont Read(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < 12 || info.Length > 16 * 1024 * 1024)
            throw new InvalidOperationException("Không đọc được font hoặc file font vượt quá 16 MB.");
        byte[] bytes = File.ReadAllBytes(path);
        if (!(bytes[0] == 0 && bytes[1] == 1 && bytes[2] == 0 && bytes[3] == 0))
            throw new InvalidOperationException("Cần font TrueType (.ttf) dạng tĩnh. Chưa hỗ trợ OTF, TTC hoặc font biến thiên.");
        int tableCount = (bytes[4] << 8) | bytes[5];
        for (int i = 0; i < tableCount; i++)
        {
            int offset = 12 + i * 16;
            if (offset + 16 > bytes.Length) throw new InvalidOperationException("File font bị hỏng.");
            if (System.Text.Encoding.ASCII.GetString(bytes, offset, 4) == "fvar")
                throw new InvalidOperationException("Hãy chọn bản TTF dạng tĩnh của font này.");
        }
        var glyph = new GlyphTypeface(new Uri(Path.GetFullPath(path)));
        string rights = glyph.EmbeddingRights.ToString();
        if (!rights.StartsWith("Installable", StringComparison.Ordinal) && !rights.StartsWith("Editable", StringComparison.Ordinal))
            throw new InvalidOperationException("Giấy phép font không cho phép nhúng để chỉnh sửa. Hãy chọn font có quyền nhúng phù hợp.");
        if (rights.Contains("Bitmap", StringComparison.Ordinal))
            throw new InvalidOperationException("Font chỉ cho phép nhúng ảnh, không dùng được để sửa chữ PDF.");
        string family = EnglishName(glyph.Win32FamilyNames);
        string face = EnglishName(glyph.Win32FaceNames);
        string name = family + (face is "Regular" or "Normal" or "Roman" ? "" : "-" + face);
        string characters = new(glyph.CharacterToGlyphMap.Where(pair => pair.Key <= char.MaxValue && pair.Value != 0)
            .Select(pair => (char)pair.Key).OrderBy(c => c).ToArray());
        return new(name, Convert.ToBase64String(bytes), characters);
    }

    private static string EnglishName(IDictionary<System.Globalization.CultureInfo, string> names) =>
        names.FirstOrDefault(pair => pair.Key.Name == "en-US").Value ?? names.Values.FirstOrDefault() ?? "Font TTF";

    internal static PdfNativeTextFont? FindMatching(string original)
    {
        string bundled = Path.Combine(AppContext.BaseDirectory, "assets", "fonts");
        if (Directory.Exists(bundled))
            foreach (string path in Directory.EnumerateFiles(bundled, "*.ttf"))
            {
                var font = Read(path);
                if (Matches(original, font.Name)) return font;
            }
        foreach (var typeface in Fonts.SystemTypefaces)
        {
            try
            {
                if (!typeface.TryGetGlyphTypeface(out var glyph) || !glyph.FontUri.IsFile ||
                    !Path.GetExtension(glyph.FontUri.LocalPath).Equals(".ttf", StringComparison.OrdinalIgnoreCase)) continue;
                string family = EnglishName(glyph.Win32FamilyNames), face = EnglishName(glyph.Win32FaceNames);
                string name = family + (face is "Regular" or "Normal" or "Roman" ? "" : "-" + face);
                if (Matches(original, name)) return Read(glyph.FontUri.LocalPath);
            }
            catch { /* Unsupported system fonts are not offered as a match. */ }
        }
        return null;
    }

    internal static void ValidateText(PdfNativeTextFont font, string text)
    {
        var supported = new HashSet<char>(font.Characters);
        string missing = new(text.Where(c => !supported.Contains(c)).Distinct().Take(12).ToArray());
        if (missing.Length > 0) throw new InvalidOperationException($"Font {font.Name} thiếu ký tự: {missing}. Hãy chọn font TTF có các ký tự này.");
    }
}
