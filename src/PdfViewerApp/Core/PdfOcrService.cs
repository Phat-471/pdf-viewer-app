using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using BitmapFrame = System.Windows.Media.Imaging.BitmapFrame;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace PdfViewerApp;

internal sealed record PdfOcrRegion(string Text, double Left, double Bottom, double Width, double Height);
internal sealed record PdfOcrOptions(string Language = "vie+eng", int Dpi = 300, bool PreferNative = true, bool Sparse = true);

internal static class PdfOcrService
{
    internal static async Task<List<PdfOcrRegion>> RecognizeAsync(PdfRenderSession session, int pageIndex,
        PdfOcrOptions options, Rect? selectedRegion, CancellationToken token, IProgress<PrintProgressInfo>? progress = null)
    {
        token.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240)) throw new InvalidOperationException("OCR cần Windows 10 trở lên.");
        Rect region = selectedRegion ?? new Rect(0, 0, 1, 1);
        if (region.IsEmpty || !double.IsFinite(region.X + region.Y + region.Width + region.Height) ||
            region.Width <= 0 || region.Height <= 0 || !new Rect(0, 0, 1, 1).Contains(region))
            throw new ArgumentOutOfRangeException(nameof(selectedRegion));
        int dpi = Math.Clamp(options.Dpi, 150, 600);
        string tessdata = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
        string modelStamp = Directory.Exists(tessdata) ? string.Join(";", Directory.GetFiles(tessdata, "*.traineddata").OrderBy(p => p).Select(p => new FileInfo(p)).Select(f => $"{f.Name}:{f.Length}:{f.LastWriteTimeUtc.Ticks}")) : "windows";
        string key = FormattableString.Invariant($"ocr-v4:{session.Fingerprint}:{pageIndex}:{region.X},{region.Y},{region.Width},{region.Height}:{options}:{modelStamp}:{PdfiumEngine.EnhanceThinLines}");
        try
        {
            string? cached = PdfPersistentCache.ReadText(key);
            if (cached != null && JsonSerializer.Deserialize<List<PdfOcrRegion>>(cached) is { } saved)
            { progress?.Report(new PrintProgressInfo("Đã lấy kết quả OCR từ cache.", 1, 1)); return saved; }
        }
        catch (JsonException) { }
        token.ThrowIfCancellationRequested();
        var size = session.GetPageSizePoints(pageIndex);
        if (options.PreferNative)
        {
            progress?.Report(new PrintProgressInfo("Đang đọc lớp chữ gốc…", 0, 0, true));
            var native = session.ReadNativeText(pageIndex, region, token);
            if (native.Sum(r => r.Text.Count(char.IsLetterOrDigit)) >= 3)
            { native = Sort(native); PdfPersistentCache.WriteText(key, JsonSerializer.Serialize(native)); return native; }
        }
        Tesseract.TesseractEngine? tesseract = null;
        OcrEngine? windows = null;
        try
        {
            string[] requested = options.Language.Split('+');
            if (requested.All(l => l is "eng" or "vie") && requested.All(l => File.Exists(Path.Combine(tessdata, l + ".traineddata"))))
            {
                try { tesseract = new Tesseract.TesseractEngine(tessdata, options.Language, Tesseract.EngineMode.Default); tesseract.SetVariable("user_defined_dpi", dpi); }
                catch (Exception ex) { PdfPerfLogger.Log("OCR engine: " + ex.Message); }
            }
            if (tesseract == null)
            {
                // Windows OCR cannot combine recognizers. Do not silently substitute a different language.
                if (options.Language.Contains('+')) throw new InvalidOperationException("Thiếu dữ liệu OCR Việt + Anh. Cài đủ vie.traineddata và eng.traineddata trong thư mục tessdata, hoặc chọn một ngôn ngữ đã có.");
                windows = OcrEngine.TryCreateFromLanguage(new Language(options.Language == "vie" ? "vi" : "en"));
                if (windows == null) throw new InvalidOperationException("Chưa có dữ liệu OCR cho ngôn ngữ đã chọn. Cài gói ngôn ngữ hoặc dữ liệu Tesseract rồi thử lại.");
            }
            double requiredWidth = Math.Ceiling(size.Width / 72 * dpi), requiredHeight = Math.Ceiling(size.Height / 72 * dpi);
            if (!double.IsFinite(requiredWidth + requiredHeight) || requiredWidth > 40000 || requiredHeight > 40000)
                throw new InvalidOperationException("Trang vượt giới hạn 40.000 pixel ở DPI này. Giảm DPI rồi thử lại.");
            int fullWidth = Math.Max(1, (int)requiredWidth), fullHeight = Math.Max(1, (int)requiredHeight);
            int tileSize = tesseract != null ? 2048 : Math.Min(2048, (int)OcrEngine.MaxImageDimension);
            int step = tileSize - 160;
            if (step <= 0) throw new InvalidOperationException("Windows OCR không hỗ trợ kích thước ảnh cần dùng.");
            int left = (int)Math.Floor(region.X * fullWidth), top = (int)Math.Floor(region.Y * fullHeight);
            int right = Math.Min(fullWidth, (int)Math.Ceiling(region.Right * fullWidth)), bottom = Math.Min(fullHeight, (int)Math.Ceiling(region.Bottom * fullHeight));
            int total = ((right - left + step - 1) / step) * ((bottom - top + step - 1) / step), completed = 0;
            var results = new List<PdfOcrRegion>();
            for (int y = top; y < bottom; y += step)
                for (int x = left; x < right; x += step)
                {
                    token.ThrowIfCancellationRequested();
                    int w = Math.Min(tileSize, right - x), h = Math.Min(tileSize, bottom - y);
                    progress?.Report(new PrintProgressInfo($"Đang đọc ô {completed + 1}/{total} · {dpi} DPI…", completed, total));
                    var bitmap = session.Render(pageIndex, fullWidth, fullHeight, x, y, w, h, false, token)
                        ?? throw new InvalidOperationException("Không dựng được vùng ảnh để OCR.");
                    var words = tesseract != null ? RecognizeTesseract(bitmap, tesseract, options.Sparse, token) : await RecognizeWindowsAsync(bitmap, windows!, token);
                    foreach (var word in words)
                    {
                        double px = x + word.Left, py = y + word.Bottom;
                        double centerX = px + word.Width / 2, centerY = py + word.Height / 2;
                        // Ownership divides overlapping tiles halfway through their shared band.
                        double ownLeft = x == left ? left : x + 80, ownTop = y == top ? top : y + 80;
                        double ownRight = x + step < right ? x + step + 80 : right;
                        double ownBottom = y + step < bottom ? y + step + 80 : bottom;
                        if (centerX < ownLeft || centerX >= ownRight || centerY < ownTop || centerY >= ownBottom) continue;
                        var mapped = new PdfOcrRegion(word.Text, px * size.Width / fullWidth,
                            size.Height - (py + word.Height) * size.Height / fullHeight,
                            word.Width * size.Width / fullWidth, word.Height * size.Height / fullHeight);
                        if (!results.Any(r => r.Text == mapped.Text && Overlap(r, mapped) > 0.6)) results.Add(mapped);
                    }
                    progress?.Report(new PrintProgressInfo($"Đã đọc ô {++completed}/{total}.", completed, total));
                    await Task.Yield();
                }
            token.ThrowIfCancellationRequested();
            results = Sort(results);
            // Failed/cancelled and empty recognition must be retryable.
            if (results.Count > 0) PdfPersistentCache.WriteText(key, JsonSerializer.Serialize(results));
            return results;
        }
        finally { tesseract?.Dispose(); }
    }

    private static List<PdfOcrRegion> Sort(List<PdfOcrRegion> result) => result.OrderByDescending(r => Math.Round((r.Bottom + r.Height) / 4)).ThenBy(r => r.Left).ToList();
    internal static string ToText(IReadOnlyList<PdfOcrRegion> words)
    {
        var text = new System.Text.StringBuilder(); double? lineTop = null;
        foreach (var word in words)
        {
            double top = word.Bottom + word.Height;
            if (lineTop.HasValue && Math.Abs(top - lineTop.Value) > Math.Max(3, word.Height * 0.6)) { text.AppendLine(); lineTop = null; }
            else if (text.Length > 0 && lineTop.HasValue) text.Append(' ');
            text.Append(word.Text); lineTop ??= top;
        }
        return text.ToString();
    }
    private static double Overlap(PdfOcrRegion a, PdfOcrRegion b)
    {
        double area = Math.Max(0, Math.Min(a.Left + a.Width, b.Left + b.Width) - Math.Max(a.Left, b.Left)) *
            Math.Max(0, Math.Min(a.Bottom + a.Height, b.Bottom + b.Height) - Math.Max(a.Bottom, b.Bottom));
        return area / Math.Max(0.001, Math.Min(a.Width * a.Height, b.Width * b.Height));
    }

    // The Bottom field here is a top-down pixel Y; mapping to PDF points happens in the caller.
    private static List<PdfOcrRegion> RecognizeTesseract(BitmapSource bitmap, Tesseract.TesseractEngine engine, bool sparse, CancellationToken token)
    {
        using var buffer = new MemoryStream(); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(buffer);
        using var pix = Tesseract.Pix.LoadFromMemory(buffer.ToArray());
        using var page = engine.Process(pix, sparse ? Tesseract.PageSegMode.SparseText : Tesseract.PageSegMode.Auto);
        using var iterator = page.GetIterator(); iterator.Begin();
        var result = new List<PdfOcrRegion>();
        do
        {
            token.ThrowIfCancellationRequested();
            string text = iterator.GetText(Tesseract.PageIteratorLevel.Word)?.Trim() ?? "";
            if (text.Length > 0 && iterator.TryGetBoundingBox(Tesseract.PageIteratorLevel.Word, out var box))
                result.Add(new PdfOcrRegion(text.Normalize(), box.X1, box.Y1, box.Width, box.Height));
        } while (iterator.Next(Tesseract.PageIteratorLevel.Word));
        return result;
    }

    private static async Task<List<PdfOcrRegion>> RecognizeWindowsAsync(BitmapSource bitmap, OcrEngine engine, CancellationToken token)
    {
        using var buffer = new MemoryStream(); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(buffer); buffer.Position = 0;
        using var random = buffer.AsRandomAccessStream();
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(random).AsTask(token);
        using var original = await decoder.GetSoftwareBitmapAsync().AsTask(token);
        using var converted = SoftwareBitmap.Convert(original, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
        var result = await engine.RecognizeAsync(converted).AsTask(token);
        return result.Lines.SelectMany(l => l.Words).Select(w => new PdfOcrRegion(w.Text.Normalize(), w.BoundingRect.X, w.BoundingRect.Y, w.BoundingRect.Width, w.BoundingRect.Height)).ToList();
    }
}
