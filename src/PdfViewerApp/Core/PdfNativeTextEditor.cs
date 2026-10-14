using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PdfViewerApp;

internal sealed record PdfNativeTextEdit(int PageNumber, int ObjectIndex, string OriginalText, string ReplacementText);
internal sealed record PdfNativeTextRegion(int PageNumber, int ObjectIndex, string OriginalText, string Text,
    Rect Bounds, Rect DisplayBounds, string FontName, double FontSize, bool Embedded, string? UnsupportedReason);
internal sealed record PdfTextEditPreview(BitmapSource? Image, Rect Bounds, string Message);

// All borrowed PDFium objects live only inside the document/page scope and global native lock.
internal static class PdfNativeTextEditor
{
    private const string Dll = "pdfium.dll";
    [DllImport(Dll)] private static extern int FPDFPage_CountObjects(nint page);
    [DllImport(Dll)] private static extern nint FPDFPage_GetObject(nint page, int index);
    [DllImport(Dll)] private static extern int FPDFPageObj_GetType(nint obj);
    [DllImport(Dll)] private static extern nint FPDFText_GetTextObject(nint textPage, int index);
    [DllImport(Dll)] private static extern uint FPDFTextObj_GetText(nint obj, nint textPage, [Out] byte[]? buffer, uint length);
    [DllImport(Dll)] private static extern nint FPDFTextObj_GetFont(nint obj);
    [DllImport(Dll)] private static extern bool FPDFTextObj_GetFontSize(nint obj, out float size);
    [DllImport(Dll)] private static extern nuint FPDFFont_GetBaseFontName(nint font, [Out] byte[]? buffer, nuint length);
    [DllImport(Dll)] private static extern int FPDFFont_GetIsEmbedded(nint font);
    [DllImport(Dll, CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern bool FPDFText_SetText(nint obj, string text);
    [DllImport(Dll)] private static extern bool FPDFPage_RemoveObject(nint page, nint obj);
    [DllImport(Dll)] private static extern void FPDFPageObj_Destroy(nint obj);
    [DllImport(Dll)] private static extern bool FPDFPage_GenerateContent(nint page);
    [DllImport(Dll)] private static extern bool FPDF_SaveAsCopy(nint document, ref FileWrite writer, uint flags);
    [DllImport(Dll)] private static extern uint FPDF_GetDocPermissions(nint document);
    [DllImport(Dll)] private static extern int FPDF_GetSignatureCount(nint document);
    [DllImport(Dll)] private static extern bool FPDFPageObj_GetBounds(nint obj, out float left, out float bottom, out float right, out float top);
    [DllImport(Dll)] private static extern bool FPDF_PageToDevice(nint page, int x, int y, int width, int height, int rotate,
        double pageX, double pageY, out int deviceX, out int deviceY);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int WriteBlock(nint writer, nint data, uint size);
    [StructLayout(LayoutKind.Sequential)] private struct FileWrite { public int Version; public nint Callback; }

    private static string ObjectText(nint obj, nint textPage)
    {
        uint length = FPDFTextObj_GetText(obj, textPage, null, 0);
        if (length < 2) return "";
        if (length > 2000000) throw new InvalidOperationException("Đối tượng chữ quá lớn để sửa.");
        var bytes = new byte[length];
        FPDFTextObj_GetText(obj, textPage, bytes, length);
        return Encoding.Unicode.GetString(bytes).TrimEnd('\0');
    }

    private static string FontName(nint font)
    {
        nuint size = FPDFFont_GetBaseFontName(font, null, 0);
        if (size == 0 || size > 4096) return "Font PDF";
        var bytes = new byte[(int)size];
        FPDFFont_GetBaseFontName(font, bytes, size);
        return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
    }

    private static void WithPage(string path, int pageNumber, Action<nint, nint> action)
    {
        lock (PdfiumEngine.SyncRoot)
        {
            PdfiumEngine.Initialize();
            nint doc = PdfiumEngine.FPDF_LoadDocument(path, null);
            if (doc == 0) throw new InvalidOperationException("Không mở được PDF để sửa chữ.");
            nint page = 0;
            try
            {
                page = PdfiumEngine.FPDF_LoadPage(doc, pageNumber - 1);
                if (page == 0) throw new InvalidOperationException("Không đọc được trang PDF.");
                action(doc, page);
            }
            finally { if (page != 0) PdfiumEngine.FPDF_ClosePage(page); PdfiumEngine.FPDF_CloseDocument(doc); }
        }
    }

    private static string? DocumentRestriction(nint doc)
    {
        if ((FPDF_GetDocPermissions(doc) & 8) == 0) return "PDF không cho phép sửa nội dung. Cần bản PDF có quyền chỉnh sửa.";
        if (FPDF_GetSignatureCount(doc) > 0) return "PDF có chữ ký số. Cần bản chưa ký để sửa chữ mà không làm mất hiệu lực chữ ký.";
        return null;
    }

    internal static List<PdfNativeTextRegion> ReadRegions(string path, int pageNumber, IReadOnlyList<PdfNativeTextEdit> edits)
    {
        var result = new List<PdfNativeTextRegion>();
        WithPage(path, pageNumber, (doc, page) =>
        {
            var indices = new Dictionary<nint, int>();
            int count = FPDFPage_CountObjects(page);
            for (int i = 0; i < count; i++)
            {
                nint obj = FPDFPage_GetObject(page, i);
                if (obj != 0 && FPDFPageObj_GetType(obj) == 1) indices[obj] = i;
            }
            ApplyPage(doc, page, edits.Where(e => e.PageNumber == pageNumber).ToArray());
            string? restriction = DocumentRestriction(doc);
            nint textPage = PdfiumEngine.FPDFText_LoadPage(page);
            if (textPage == 0) return;
            try
            {
                var boxes = new Dictionary<nint, Rect>();
                for (int i = 0; i < PdfiumEngine.FPDFText_CountChars(textPage); i++)
                {
                    nint obj = FPDFText_GetTextObject(textPage, i);
                    if (obj == 0 || !PdfiumEngine.FPDFText_GetCharBox(textPage, i, out double l, out double r, out double b, out double t) || r <= l || t <= b) continue;
                    var rect = new Rect(l, b, r - l, t - b);
                    if (boxes.TryGetValue(obj, out var old)) rect.Union(old);
                    boxes[obj] = rect;
                }
                foreach (var item in boxes)
                {
                    string text = ObjectText(item.Key, textPage);
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    bool topLevel = indices.TryGetValue(item.Key, out int index);
                    var edit = topLevel ? edits.FirstOrDefault(e => e.PageNumber == pageNumber && e.ObjectIndex == index) : null;
                    nint font = FPDFTextObj_GetFont(item.Key);
                    FPDFTextObj_GetFontSize(item.Key, out float size);
                    Rect pdfBox = item.Value;
                    FPDF_PageToDevice(page, 0, 0, 100000, 100000, 0, pdfBox.Left, pdfBox.Top, out int x1, out int y1);
                    FPDF_PageToDevice(page, 0, 0, 100000, 100000, 0, pdfBox.Right, pdfBox.Bottom, out int x2, out int y2);
                    var displayBox = new Rect(Math.Min(x1, x2) / 100000.0, Math.Min(y1, y2) / 100000.0,
                        Math.Abs(x2 - x1) / 100000.0, Math.Abs(y2 - y1) / 100000.0);
                    result.Add(new(pageNumber, topLevel ? index : -1, edit?.OriginalText ?? text, text, pdfBox, displayBox,
                        FontName(font), size, FPDFFont_GetIsEmbedded(font) == 1,
                        restriction ?? (!topLevel ? "Chữ nằm trong khối PDF dùng chung. Xuất lại từ file nguồn để sửa riêng vùng này." : null)));
                }
            }
            finally { PdfiumEngine.FPDFText_ClosePage(textPage); }
        });
        return result;
    }

    // Only the text bytes change. Font resource, point size, color, matrix and paint order stay on the original object.
    internal static void ApplyPage(nint doc, nint page, IReadOnlyList<PdfNativeTextEdit> edits)
    {
        if (edits.Count == 0) return;
        string? restriction = DocumentRestriction(doc);
        if (restriction != null) throw new InvalidOperationException(restriction);
        var targets = edits.Select(e => (Edit: e, Object: FPDFPage_GetObject(page, e.ObjectIndex))).ToArray();
        foreach (var target in targets)
        {
            if (target.Object == 0 || FPDFPageObj_GetType(target.Object) != 1) throw new InvalidOperationException("Đối tượng chữ đã thay đổi. Hãy mở lại PDF.");
            nint textPage = PdfiumEngine.FPDFText_LoadPage(page);
            if (textPage == 0) throw new InvalidOperationException("Không đọc được nội dung chữ.");
            string original;
            try { original = ObjectText(target.Object, textPage); }
            finally { PdfiumEngine.FPDFText_ClosePage(textPage); }
            if (original != target.Edit.OriginalText) throw new InvalidOperationException("Chữ gốc đã thay đổi. Chưa ghi thay đổi vào PDF.");
            string replacement = target.Edit.ReplacementText;
            if (replacement.Any(c => char.IsControl(c) || char.IsSurrogate(c)))
                throw new InvalidOperationException("Vùng chữ này chỉ nhận một dòng, không có ký tự điều khiển hoặc emoji.");
            if (replacement.Length == 0)
            {
                if (!FPDFPage_RemoveObject(page, target.Object)) throw new InvalidOperationException("Không xóa được đối tượng chữ.");
                FPDFPageObj_Destroy(target.Object);
                continue;
            }
            if (!FPDFText_SetText(target.Object, replacement)) throw new InvalidOperationException("Font gốc không mã hóa được chữ mới.");
            textPage = PdfiumEngine.FPDFText_LoadPage(page);
            if (textPage == 0) throw new InvalidOperationException("Không đọc lại được chữ mới.");
            try
            {
                if (ObjectText(target.Object, textPage) != replacement)
                    throw new InvalidOperationException("Font gốc thiếu ký tự hoặc bảng mã không hỗ trợ chữ mới. Không tự thay font; hãy dùng ký tự khác hoặc sửa từ file nguồn.");
            }
            finally { PdfiumEngine.FPDFText_ClosePage(textPage); }
        }
        if (!FPDFPage_GenerateContent(page)) throw new InvalidOperationException("Không tạo được nội dung PDF sau khi sửa chữ.");
    }

    internal static PdfTextEditPreview Preview(string path, PdfNativeTextRegion region, IReadOnlyList<PdfNativeTextEdit> edits)
    {
        PdfTextEditPreview? result = null;
        WithPage(path, region.PageNumber, (doc, page) =>
        {
            nint obj = FPDFPage_GetObject(page, region.ObjectIndex);
            ApplyPage(doc, page, edits.Where(e => e.PageNumber == region.PageNumber).ToArray());
            Rect box = region.Bounds;
            bool removed = edits.Any(e => e.PageNumber == region.PageNumber && e.ObjectIndex == region.ObjectIndex && e.ReplacementText.Length == 0);
            if (!removed && FPDFPageObj_GetBounds(obj, out float l, out float b, out float r, out float t) && r > l && t > b)
                box.Union(new Rect(l, b, r - l, t - b));
            Rect selected = box;
            box.Inflate(Math.Max(8, region.Bounds.Height), Math.Max(8, region.Bounds.Height));
            double w = PdfiumEngine.FPDF_GetPageWidth(page), h = PdfiumEngine.FPDF_GetPageHeight(page);
            int fullW = Math.Max(1, (int)Math.Round(w * 4)), fullH = Math.Max(1, (int)Math.Round(h * 4));
            if (!FPDF_PageToDevice(page, 0, 0, fullW, fullH, 0, box.Left, box.Top, out int x1, out int y1) ||
                !FPDF_PageToDevice(page, 0, 0, fullW, fullH, 0, box.Right, box.Bottom, out int x2, out int y2))
                throw new InvalidOperationException("Không xác định được vùng xem trước trên trang PDF.");
            int x = Math.Clamp(Math.Min(x1, x2), 0, fullW - 1), y = Math.Clamp(Math.Min(y1, y2), 0, fullH - 1);
            int width = Math.Clamp(Math.Abs(x2 - x1), 1, fullW - x), height = Math.Clamp(Math.Abs(y2 - y1), 1, fullH - y);
            double scale = Math.Min(1, Math.Min(1600.0 / width, 600.0 / height));
            var image = PdfiumEngine.RenderLoadedPageToBitmap(page, Math.Max(1, (int)(fullW * scale)), Math.Max(1, (int)(fullH * scale)),
                (int)(x * scale), (int)(y * scale), Math.Max(1, (int)(width * scale)), Math.Max(1, (int)(height * scale)), false);
            if (image == null) throw new InvalidOperationException("Không tạo được bản xem trước.");
            result = new(image, selected, removed ? "Đối tượng chữ sẽ được xóa khỏi PDF." : selected.Width > region.Bounds.Width * 1.05 || selected.Height > region.Bounds.Height * 1.05
                ? "Chữ mới rộng hơn vùng gốc. Kiểm tra phần chữ lân cận; cỡ chữ vẫn giữ nguyên." : "Xem trước bằng font PDF gốc. Nhấn Áp dụng, sau đó Lưu để ghi vào file.");
        });
        return result!;
    }

    internal static void SaveCopy(string source, string destination, IReadOnlyList<PdfNativeTextEdit> edits)
    {
        if (Path.GetFullPath(source).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Phải ghi PDF vào file tạm trước khi thay file gốc.");
        lock (PdfiumEngine.SyncRoot)
        {
            PdfiumEngine.Initialize();
            nint doc = PdfiumEngine.FPDF_LoadDocument(source, null);
            if (doc == 0) throw new InvalidOperationException("Không mở được PDF để lưu chữ.");
            try
            {
                int pageCount = PdfiumEngine.FPDF_GetPageCount(doc);
                var expected = new Dictionary<int, List<(string Text, string Font, float Size)>>();
                foreach (var group in edits.GroupBy(e => e.PageNumber))
                {
                    nint page = PdfiumEngine.FPDF_LoadPage(doc, group.Key - 1);
                    if (page == 0) throw new InvalidOperationException("Không đọc được trang cần lưu.");
                    try { ApplyPage(doc, page, group.ToArray()); expected[group.Key] = ReadTextObjects(page); }
                    finally { PdfiumEngine.FPDF_ClosePage(page); }
                }
                using var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                Exception? writeError = null;
                WriteBlock callback = (_, data, count) =>
                {
                    try
                    {
                        var buffer = new byte[checked((int)count)];
                        Marshal.Copy(data, buffer, 0, buffer.Length); stream.Write(buffer, 0, buffer.Length); return 1;
                    }
                    catch (Exception ex) { writeError = ex; return 0; }
                };
                var writer = new FileWrite { Version = 1, Callback = Marshal.GetFunctionPointerForDelegate(callback) };
                bool saved = FPDF_SaveAsCopy(doc, ref writer, 2); // FPDF_NO_INCREMENTAL
                GC.KeepAlive(callback);
                if (!saved || writeError != null) throw new IOException("Không ghi được PDF đã sửa.", writeError);
                stream.Flush(true);
                stream.Dispose();
                nint check = PdfiumEngine.FPDF_LoadDocument(destination, null);
                if (check == 0) throw new IOException("Không mở lại được PDF vừa ghi. Chưa thay file gốc.");
                try
                {
                    if (PdfiumEngine.FPDF_GetPageCount(check) != pageCount) throw new IOException("Số trang PDF vừa ghi không khớp.");
                    foreach (var item in expected)
                    {
                        nint page = PdfiumEngine.FPDF_LoadPage(check, item.Key - 1);
                        if (page == 0) throw new IOException("Không mở lại được trang vừa sửa.");
                        try
                        {
                            var actual = ReadTextObjects(page);
                            if (actual.Count != item.Value.Count || actual.Where((v, i) => v.Text != item.Value[i].Text ||
                                v.Font != item.Value[i].Font || Math.Abs(v.Size - item.Value[i].Size) > 0.001).Any())
                                throw new IOException("Chữ hoặc định dạng trong PDF vừa ghi không khớp bản xem trước. Chưa thay file gốc.");
                        }
                        finally { PdfiumEngine.FPDF_ClosePage(page); }
                    }
                }
                finally { PdfiumEngine.FPDF_CloseDocument(check); }
            }
            catch { try { File.Delete(destination); } catch { } throw; }
            finally { PdfiumEngine.FPDF_CloseDocument(doc); }
        }
    }

    private static List<(string Text, string Font, float Size)> ReadTextObjects(nint page)
    {
        var result = new List<(string Text, string Font, float Size)>();
        nint textPage = PdfiumEngine.FPDFText_LoadPage(page);
        if (textPage == 0) throw new IOException("Không đọc được chữ để đối chiếu file PDF.");
        try
        {
            int count = FPDFPage_CountObjects(page);
            for (int i = 0; i < count; i++)
            {
                nint obj = FPDFPage_GetObject(page, i);
                if (obj == 0 || FPDFPageObj_GetType(obj) != 1) continue;
                FPDFTextObj_GetFontSize(obj, out float size);
                result.Add((ObjectText(obj, textPage), FontName(FPDFTextObj_GetFont(obj)), size));
            }
        }
        finally { PdfiumEngine.FPDFText_ClosePage(textPage); }
        return result;
    }
}
