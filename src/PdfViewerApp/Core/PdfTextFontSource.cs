#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Windows.Media;

namespace PdfViewerApp;

// Capture the bytes once: rendering, undo, saving and printing use the same font,
// even if the installed file is later replaced or removed.
internal sealed record PdfNativeTextFont(string Name, string DataBase64, string Characters);

internal static class PdfTextFontSource
{
    private static readonly ConcurrentDictionary<string, PdfNativeTextFont> MatchesCache = new();
    private static string Key(string name)
    {
        if (name.Length > 7 && name[6] == '+' && name.Take(6).All(c => c is >= 'A' and <= 'Z')) name = name[7..];
        string key = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        key = key.Replace("timesnewromanps", "timesnewroman", StringComparison.Ordinal);
        if (key.EndsWith("psmt", StringComparison.Ordinal)) key = key[..^4];
        else if ((key.StartsWith("arial", StringComparison.Ordinal) || key.StartsWith("timesnewroman", StringComparison.Ordinal)) && key.EndsWith("mt", StringComparison.Ordinal)) key = key[..^2];
        foreach (string suffix in new[] { "regular", "normal" })
            if (key.EndsWith(suffix, StringComparison.Ordinal)) return key[..^suffix.Length];
        if (key != "timesnewroman" && key.EndsWith("roman", StringComparison.Ordinal)) return key[..^5];
        return key.EndsWith("oblique", StringComparison.Ordinal) ? key[..^7] + "italic" : key;
    }

    internal static bool Matches(string original, string fullName) => Key(original) == Key(fullName);

    internal static PdfNativeTextFont ReadVietnameseFont(string original)
    {
        string key = Key(original);
        bool bold = key.Contains("bold"), italic = key.Contains("italic") || key.Contains("oblique");
        string face = bold ? (italic ? "BoldItalic" : "Bold") : (italic ? "Italic" : "Regular");
        return Read(Path.Combine(AppContext.BaseDirectory, "assets", "fonts", $"NotoSans-{face}.ttf"));
    }

    internal static PdfNativeTextFont Read(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < 12 || info.Length > 16 * 1024 * 1024)
            throw new InvalidOperationException("Không đọc được font hoặc file font vượt quá 16 MB.");
        byte[] bytes = File.ReadAllBytes(path);
        var parsed = new PdfTrueTypeFont(bytes);
        parsed.CheckEmbeddingRights();
        return new(parsed.Name, Convert.ToBase64String(bytes), parsed.ReadCharacters());
    }

    internal static PdfNativeTextFont? FindMatching(string original)
    {
        string key = Key(original);
        if (MatchesCache.TryGetValue(key, out var cached)) return cached;
        string? unusable = null;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        PdfNativeTextFont? TryFile(string path)
        {
            if (!visited.Add(path)) return null;
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length < 12 || info.Length > 16 * 1024 * 1024) return null;
                var parsed = new PdfTrueTypeFont(File.ReadAllBytes(path));
                if (!Matches(original, parsed.Name) && !Matches(original, parsed.PostScriptName)) return null;
                try
                {
                    var font = Read(path);
                    MatchesCache[key] = font;
                    return font;
                }
                catch (Exception ex) { unusable ??= ex.Message; }
            }
            catch { /* An unrelated unsupported font must not abort the search. */ }
            return null;
        }
        string bundled = Path.Combine(AppContext.BaseDirectory, "assets", "fonts");
        foreach (string folder in new[] { bundled, Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts") })
        {
            if (!Directory.Exists(folder)) continue;
            foreach (string path in Directory.EnumerateFiles(folder, "*.ttf").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                var font = TryFile(path);
                if (font != null) return font;
            }
        }
        // Retain WPF enumeration only to locate fonts registered outside the
        // normal folders. Identity and coverage still come from the file bytes.
        foreach (var typeface in Fonts.SystemTypefaces)
        {
            try
            {
                if (!typeface.TryGetGlyphTypeface(out var glyph) || !glyph.FontUri.IsFile ||
                    !Path.GetExtension(glyph.FontUri.LocalPath).Equals(".ttf", StringComparison.OrdinalIgnoreCase)) continue;
                var font = TryFile(glyph.FontUri.LocalPath);
                if (font != null) return font;
            }
            catch { /* Unsupported system fonts are not offered as a match. */ }
        }
        if (unusable != null) throw new InvalidOperationException("Đã tìm thấy font cùng tên nhưng chưa dùng được: " + unusable);
        return null;
    }

    internal static void ValidateText(PdfNativeTextFont font, string text)
    {
        string missing = MissingCharacters(font, text);
        if (missing.Length > 0) throw new InvalidOperationException($"Font {font.Name} thiếu ký tự: {missing}. Hãy chọn font TTF có các ký tự này.");
    }

    internal static string MissingCharacters(PdfNativeTextFont font, string text)
    {
        var supported = new HashSet<char>(font.Characters);
        return new(text.Where(c => !supported.Contains(c)).Distinct().Take(12).ToArray());
    }
}
