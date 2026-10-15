#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PdfViewerApp;

internal sealed record PdfNativeTextEdit(int PageNumber, int ObjectIndex, string OriginalText, string ReplacementText,
    PdfNativeTextFont? Font = null);
internal sealed record PdfNativeTextRegion(int PageNumber, int ObjectIndex, string OriginalText, string Text,
    Rect Bounds, Rect DisplayBounds, string FontName, double FontSize, bool Embedded, string? UnsupportedReason)
{
    internal double TextScale { get; init; } = 1;
    internal double DisplayFontSize => FontSize * TextScale;
    internal string SourceFontName { get; init; } = FontName;
    internal double Angle { get; init; }
    internal uint TextColor { get; init; } = 0xFF000000;
}
internal sealed record PdfTextEditPreview(BitmapSource? Image, Rect Bounds, string Message);
internal sealed class PdfTextEncodingException : InvalidOperationException
{
    internal PdfTextEncodingException(string message) : base(message) { }
}

// All borrowed PDFium objects live only inside the document/page scope and global native lock.
internal static partial class PdfNativeTextEditor
{
    private const string Dll = "pdfium.dll";
    [DllImport(Dll)] private static extern int FPDFPage_CountObjects(nint page);
    [DllImport(Dll)] private static extern nint FPDFPage_GetObject(nint page, int index);
    [DllImport(Dll)] private static extern int FPDFPageObj_GetType(nint obj);
    [DllImport(Dll)] private static extern nint FPDFText_GetTextObject(nint textPage, int index);
    [DllImport(Dll)] private static extern nint FPDFTextObj_GetFont(nint obj);
    [DllImport(Dll)] private static extern bool FPDFTextObj_GetFontSize(nint obj, out float size);
    [DllImport(Dll)] private static extern nuint FPDFFont_GetBaseFontName(nint font, [Out] byte[]? buffer, nuint length);
    [DllImport(Dll)] private static extern int FPDFFont_GetIsEmbedded(nint font);
    [DllImport(Dll, CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern bool FPDFText_SetText(nint obj, string text);
    [DllImport(Dll)] private static extern bool FPDFPage_RemoveObject(nint page, nint obj);
    [DllImport(Dll)] private static extern bool FPDFPage_InsertObjectAtIndex(nint page, nint obj, nuint index);
    [DllImport(Dll, CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern nint FPDFText_LoadCidType2Font(nint doc, byte[] data, uint size, string toUnicode, byte[] cidToGid, uint cidToGidSize);
    [DllImport(Dll)] private static extern void FPDFFont_Close(nint font);
    [DllImport(Dll)] private static extern nint FPDFPageObj_CreateTextObj(nint doc, nint font, float size);
    [DllImport(Dll)] private static extern bool FPDFPageObj_GetMatrix(nint obj, out Matrix matrix);
    [DllImport(Dll)] private static extern bool FPDFPageObj_SetMatrix(nint obj, ref Matrix matrix);
    [DllImport(Dll)] private static extern bool FPDFPageObj_GetFillColor(nint obj, out uint r, out uint g, out uint b, out uint a);
    [DllImport(Dll)] private static extern bool FPDFPageObj_SetFillColor(nint obj, uint r, uint g, uint b, uint a);
    [DllImport(Dll)] private static extern int FPDFTextObj_GetTextRenderMode(nint obj);
    [DllImport(Dll)] private static extern bool FPDFTextObj_SetTextRenderMode(nint obj, int mode);
    [DllImport(Dll)] private static extern bool FPDFPageObj_HasTransparency(nint obj);
    [DllImport(Dll)] private static extern nint FPDFPageObj_GetClipPath(nint obj);
    [DllImport(Dll)] private static extern int FPDFClipPath_CountPaths(nint clip);
    [DllImport(Dll)] private static extern int FPDFPageObj_CountMarks(nint obj);
    [DllImport(Dll)] private static extern nint FPDFPageObj_GetMark(nint obj, uint index);
    [DllImport(Dll)] private static extern bool FPDFPageObj_AddExistingMark(nint obj, nint mark);
    [DllImport(Dll)] private static extern bool FPDFPageObjMark_GetName(nint mark, [Out] byte[]? buffer, uint length, out uint size);
    [DllImport(Dll)] private static extern int FPDFPageObjMark_CountParams(nint mark);
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
    [StructLayout(LayoutKind.Sequential)] private struct Matrix { public float A, B, C, D, E, F; }
    private sealed record TextState(string Text, string Font, float Size, Matrix Placement,
        uint Red, uint Green, uint Blue, uint Alpha, int Mode, string Marks, float[]? Positions)
    {
        public bool SameFormat(TextState other) => Math.Abs(Size - other.Size) < 0.001 &&
            Math.Abs(Placement.A - other.Placement.A) < 0.001 && Math.Abs(Placement.B - other.Placement.B) < 0.001 &&
            Math.Abs(Placement.C - other.Placement.C) < 0.001 && Math.Abs(Placement.D - other.Placement.D) < 0.001 &&
            Math.Abs(Placement.E - other.Placement.E) < 0.001 && Math.Abs(Placement.F - other.Placement.F) < 0.001 &&
            Red == other.Red && Green == other.Green && Blue == other.Blue && Alpha == other.Alpha && Mode == other.Mode && Marks == other.Marks &&
            (Positions == null ? other.Positions == null : other.Positions != null && Positions.Length == other.Positions.Length &&
                Positions.Zip(other.Positions).All(p => Math.Abs(p.First - p.Second) < .01));
    }

    private static string ObjectText(nint obj, nint textPage)
    {
        // GetTextByObject also appends whitespace inferred from neighbouring
        // objects. Only compare characters actually owned by this object.
        var text = new StringBuilder();
        int count = PdfiumEngine.FPDFText_CountChars(textPage);
        for (int i = 0; i < count; i++)
        {
            if (FPDFText_GetTextObject(textPage, i) != obj || FPDFText_IsGenerated(textPage, i) != 0) continue;
            uint value = FPDFText_GetUnicode(textPage, i);
            if (value > 0x10FFFF || value is >= 0xD800 and <= 0xDFFF) throw new InvalidOperationException("PDF có mã ký tự không hợp lệ.");
            text.Append(char.ConvertFromUtf32((int)value));
            if (text.Length > 1000000) throw new InvalidOperationException("Đối tượng chữ quá lớn để sửa.");
        }
        return text.ToString();
    }

    private static void VerifyObjectText(nint obj, nint textPage, string expected, PdfNativeTextFont? source)
    {
        string actual = ObjectText(obj, textPage);
        if (actual == expected) return;
        if (source == null) throw new PdfTextEncodingException("Font nhúng không mã hóa đúng nội dung mới.");
        int index = 0;
        while (index < actual.Length && index < expected.Length && actual[index] == expected[index]) index++;
        string wanted = index < expected.Length ? $"U+{(int)expected[index]:X4}" : "hết dòng";
        string received = index < actual.Length ? $"U+{(int)actual[index]:X4}" : "hết dòng";
        throw new InvalidOperationException($"Đã nạp {source.Name}, nhưng chữ PDF đọc lại khác tại ký tự {index + 1}: cần {wanted}, nhận {received}. Chưa áp dụng; hãy chọn TTF khác.");
    }

    private static nint LoadUnicodeFont(nint doc, PdfNativeTextFont source)
    {
        byte[] data = Convert.FromBase64String(source.DataBase64);
        var parsed = new PdfTrueTypeFont(data);
        parsed.CheckEmbeddingRights();
        var glyphs = parsed.ReadGlyphMap();
        // Give every Unicode its own CID, even when SPACE and NBSP share a
        // glyph. FPDFText_LoadFont's automatic glyph->Unicode map is ambiguous
        // for aliases and can change extracted text despite correct glyphs.
        var entries = glyphs.ToArray();
        // Dense CIDs keep font widths and the glyph map small even for fonts
        // with sparse Unicode coverage near the end of the BMP.
        var cidToGid = new byte[(entries.Length + 1) * 2];
        var cmap = new StringBuilder("/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n/CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> def\n/CMapName /PDFPro-Unicode def\n/CMapType 2 def\n1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n");
        for (int start = 0; start < entries.Length; start += 100)
        {
            int count = Math.Min(100, entries.Length - start);
            cmap.Append(count).Append(" beginbfchar\n");
            for (int i = start; i < start + count; i++)
            {
                int cid = i + 1, code = entries[i].Key, glyph = entries[i].Value;
                cidToGid[cid * 2] = (byte)(glyph >> 8);
                cidToGid[cid * 2 + 1] = (byte)glyph;
                cmap.Append('<').Append(cid.ToString("X4", System.Globalization.CultureInfo.InvariantCulture))
                    .Append("> <").Append(code.ToString("X4", System.Globalization.CultureInfo.InvariantCulture)).Append(">\n");
            }
            cmap.Append("endbfchar\n");
        }
        cmap.Append("endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n");
        nint font = FPDFText_LoadCidType2Font(doc, data, checked((uint)data.Length), cmap.ToString(), cidToGid, checked((uint)cidToGid.Length));
        if (font == 0) throw new InvalidOperationException($"Không nhúng được bảng mã Unicode cho {source.Name}. Hãy chọn TTF khác.");
        return font;
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
            var sourceFonts = new Dictionary<int, string>();
            var remaining = new HashSet<nint>();
            nint originalTextPage = PdfiumEngine.FPDFText_LoadPage(page);
            if (originalTextPage == 0) throw new InvalidOperationException("Không đọc được vùng chữ.");
            try
            {
                for (int c = 0; c < PdfiumEngine.FPDFText_CountChars(originalTextPage); c++)
                {
                    nint obj = FPDFText_GetTextObject(originalTextPage, c);
                    if (obj != 0) remaining.Add(obj);
                }
            }
            finally { PdfiumEngine.FPDFText_ClosePage(originalTextPage); }
            int count = FPDFPage_CountObjects(page);
            // Revit commonly appends text after millions of drawing objects.
            // Stop when every text object has been located; nested form text
            // deliberately keeps the search running and remains unsupported.
            for (int i = count - 1; i >= 0 && remaining.Count > 0; i--)
            {
                nint obj = FPDFPage_GetObject(page, i);
                if (remaining.Remove(obj)) { indices[obj] = i; sourceFonts[i] = FontName(FPDFTextObj_GetFont(obj)); }
            }
            var replacements = ApplyPage(doc, page, edits.Where(e => e.PageNumber == pageNumber).ToArray());
            foreach (var pair in replacements)
                if (indices.Remove(pair.Key, out int index) && pair.Value != 0) indices[pair.Value] = index;
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
                    double textScale = FPDFPageObj_GetMatrix(item.Key, out var placement)
                        ? Math.Sqrt(placement.C * (double)placement.C + placement.D * (double)placement.D) : 1;
                    Rect pdfBox = item.Value;
                    FPDF_PageToDevice(page, 0, 0, 100000, 100000, 0, pdfBox.Left, pdfBox.Top, out int x1, out int y1);
                    FPDF_PageToDevice(page, 0, 0, 100000, 100000, 0, pdfBox.Right, pdfBox.Bottom, out int x2, out int y2);
                    var displayBox = new Rect(Math.Min(x1, x2) / 100000.0, Math.Min(y1, y2) / 100000.0,
                        Math.Abs(x2 - x1) / 100000.0, Math.Abs(y2 - y1) / 100000.0);
                    FPDFPageObj_GetFillColor(item.Key, out uint red, out uint green, out uint blue, out uint alpha);
                    result.Add(new(pageNumber, topLevel ? index : -1, edit?.OriginalText ?? text, text, pdfBox, displayBox,
                        FontName(font), size, FPDFFont_GetIsEmbedded(font) == 1,
                        restriction ?? (!topLevel ? "Chữ nằm trong khối PDF dùng chung. Xuất lại từ file nguồn để sửa riêng vùng này." : null))
                        { TextScale = textScale, SourceFontName = topLevel ? sourceFonts[index] : FontName(font),
                          Angle = -Math.Atan2(placement.B, placement.A) * 180 / Math.PI,
                          TextColor = (alpha << 24) | (red << 16) | (green << 8) | blue });
                }
            }
            finally { PdfiumEngine.FPDFText_ClosePage(textPage); }
        });
        return result;
    }

    // Font resolution is captured by the UI; all subsequent paths use those exact bytes.
    internal static Dictionary<nint, nint> ApplyPage(nint doc, nint page, IReadOnlyList<PdfNativeTextEdit> edits)
    {
        var replacements = new Dictionary<nint, nint>();
        if (edits.Count == 0) return replacements;
        string? restriction = DocumentRestriction(doc);
        if (restriction != null) throw new InvalidOperationException(restriction);
        var targets = edits.Select(e => (Edit: e, Object: FPDFPage_GetObject(page, e.ObjectIndex))).ToArray();
        int deleted = 0;
        foreach (var target in targets.OrderBy(t => t.Edit.ObjectIndex))
        {
            if (target.Object == 0 || FPDFPageObj_GetType(target.Object) != 1) throw new InvalidOperationException("Đối tượng chữ đã thay đổi. Hãy mở lại PDF.");
            nint textPage = PdfiumEngine.FPDFText_LoadPage(page);
            if (textPage == 0) throw new InvalidOperationException("Không đọc được nội dung chữ.");
            string original;
            float[]? positions;
            try { original = ObjectText(target.Object, textPage); positions = ReadPositions(target.Object, textPage, original); }
            finally { PdfiumEngine.FPDFText_ClosePage(textPage); }
            if (original != target.Edit.OriginalText) throw new InvalidOperationException("Chữ gốc đã thay đổi. Chưa ghi thay đổi vào PDF.");
            string replacement = target.Edit.ReplacementText;
            if (replacement.Any(c => char.IsControl(c) || char.IsSurrogate(c)))
                throw new InvalidOperationException("Vùng chữ này chỉ nhận một dòng, không có ký tự điều khiển hoặc emoji.");
            if (replacement.Length == 0)
            {
                if (!FPDFPage_RemoveObject(page, target.Object)) throw new InvalidOperationException("Không xóa được đối tượng chữ.");
                FPDFPageObj_Destroy(target.Object);
                replacements[target.Object] = 0;
                deleted++;
                continue;
            }
            nint current = target.Object;
            if (target.Edit.Font != null)
            {
                current = ReplaceWithFullFont(doc, page, target.Object, target.Edit.ObjectIndex - deleted, replacement, target.Edit.Font,
                    replacement.Length == original.Length ? positions : null);
                replacements[target.Object] = current;
            }
            else if (!FPDFText_SetText(current, replacement))
                throw new PdfTextEncodingException("Font nhúng không mã hóa được chữ mới.");
            if (target.Edit.Font == null && replacement.Length == original.Length) SetPositions(current, positions);
            textPage = PdfiumEngine.FPDFText_LoadPage(page);
            if (textPage == 0) throw new InvalidOperationException("Không đọc lại được chữ mới.");
            try
            {
                VerifyObjectText(current, textPage, replacement, target.Edit.Font);
            }
            finally { PdfiumEngine.FPDFText_ClosePage(textPage); }
        }
        if (!FPDFPage_GenerateContent(page)) throw new InvalidOperationException("Không tạo được nội dung PDF sau khi sửa chữ.");
        return replacements;
    }

    private static nint ReplaceWithFullFont(nint doc, nint page, nint original, int index, string text, PdfNativeTextFont source, float[]? positions)
    {
        PdfTextFontSource.ValidateText(source, text);
        // Clipping and transparency cannot be reproduced by the public API.
        nint clip = FPDFPageObj_GetClipPath(original);
        if ((clip != 0 && FPDFClipPath_CountPaths(clip) != 0) || FPDFPageObj_HasTransparency(original))
            throw new InvalidOperationException("Vùng chữ có lớp cắt hoặc độ trong suốt. Chưa thể nạp font đầy đủ cho vùng này; hãy dùng font nhúng hoặc sửa file nguồn.");
        int mode = FPDFTextObj_GetTextRenderMode(original);
        if (mode is not (0 or 3)) throw new InvalidOperationException("Chưa hỗ trợ nạp font cho chữ có nét viền hoặc dùng làm vùng cắt.");
        if (!FPDFTextObj_GetFontSize(original, out float size) || !FPDFPageObj_GetMatrix(original, out var matrix))
            throw new InvalidOperationException("Không đọc được định dạng chữ gốc.");
        nint font = LoadUnicodeFont(doc, source);
        nint created = 0;
        try
        {
            created = FPDFPageObj_CreateTextObj(doc, font, size);
            if (created == 0 || !FPDFText_SetText(created, text) || !FPDFPageObj_SetMatrix(created, ref matrix) ||
                !FPDFTextObj_SetTextRenderMode(created, mode)) throw new InvalidOperationException("Không tạo được chữ bằng font đầy đủ.");
            SetPositions(created, positions);
            if (!FPDFPageObj_GetFillColor(original, out uint r, out uint g, out uint b, out uint a) ||
                !FPDFPageObj_SetFillColor(created, r, g, b, a)) throw new InvalidOperationException("Không giữ được màu chữ gốc.");
            // Retain entire dictionaries/references in the same document, including
            // Revit viewport metadata. Copy before destroying the source object.
            int markCount = FPDFPageObj_CountMarks(original);
            if (markCount < 0) throw new InvalidOperationException("Không đọc được thẻ của chữ gốc.");
            for (uint i = 0; i < markCount; i++)
            {
                nint mark = FPDFPageObj_GetMark(original, i);
                if (mark == 0 || !FPDFPageObj_AddExistingMark(created, mark))
                    throw new InvalidOperationException("Không giữ được thẻ của chữ gốc. Chưa thay nội dung PDF.");
            }
            int count = FPDFPage_CountObjects(page);
            if (index < 0 || FPDFPage_GetObject(page, index) != original || !FPDFPageObj_GetBounds(created, out float left, out float bottom, out float right, out float top))
                throw new InvalidOperationException("Không xác định được vị trí chữ mới.");
            var bounds = new Rect(left, bottom, Math.Max(0, right - left), Math.Max(0, top - bottom));
            // PDFium saves newly created objects in a trailing stream. Reject
            // overlaps with later paint so the layer change cannot alter output.
            for (int i = index + 1; i < count; i++)
            {
                nint later = FPDFPage_GetObject(page, i);
                if (FPDFPageObj_GetBounds(later, out left, out bottom, out right, out top) && right > left && top > bottom &&
                    bounds.IntersectsWith(new Rect(left, bottom, right - left, top - bottom)))
                    throw new InvalidOperationException("Chữ mới chồng lên đối tượng khác. Hãy rút ngắn nội dung hoặc dùng font nhúng để giữ đúng thứ tự lớp.");
            }
            if (!FPDFPage_RemoveObject(page, original)) throw new InvalidOperationException("Không thay được đối tượng chữ.");
            nint result = created; created = 0; // Insert takes ownership even on failure.
            if (!FPDFPage_InsertObjectAtIndex(page, result, (nuint)index))
            {
                FPDFPageObj_Destroy(original);
                throw new InvalidOperationException("Không chèn được chữ mới.");
            }
            FPDFPageObj_Destroy(original);
            return result;
        }
        finally { if (created != 0) FPDFPageObj_Destroy(created); FPDFFont_Close(font); }
    }

    internal static PdfTextEditPreview Preview(string path, PdfNativeTextRegion region, IReadOnlyList<PdfNativeTextEdit> edits)
    {
        PdfTextEditPreview? result = null;
        WithPage(path, region.PageNumber, (doc, page) =>
        {
            nint obj = FPDFPage_GetObject(page, region.ObjectIndex);
            var replacements = ApplyPage(doc, page, edits.Where(e => e.PageNumber == region.PageNumber).ToArray());
            if (replacements.TryGetValue(obj, out nint replaced)) obj = replaced;
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
            string message = removed ? "Đối tượng chữ sẽ được xóa khỏi PDF." : selected.Width > region.Bounds.Width * 1.05 || selected.Height > region.Bounds.Height * 1.05
                ? "Chữ mới rộng hơn vùng gốc. Kiểm tra phần chữ lân cận; cỡ chữ vẫn giữ nguyên." : "Nhấn Áp dụng, sau đó Lưu để ghi vào file.";
            var edit = edits.FirstOrDefault(e => e.PageNumber == region.PageNumber && e.ObjectIndex == region.ObjectIndex);
            if (!removed) message = (edit?.Font == null ? "Xem trước bằng font nhúng gốc. " : $"Xem trước bằng {edit.Font.Name} đầy đủ · {region.DisplayFontSize:0.###} pt. ") + message;
            result = new(image, selected, message);
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
                var expected = new Dictionary<int, List<TextState>>();
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
                            // New font objects can be serialized to a trailing stream.
                            // Count duplicate strings and check their full visual state.
                            var remaining = item.Value.GroupBy(v => (v.Text, v.Font)).ToDictionary(g => g.Key, g => g.ToList());
                            bool matches = actual.Count == item.Value.Count;
                            foreach (var value in actual)
                            {
                                if (!remaining.TryGetValue((value.Text, value.Font), out var candidates)) { matches = false; break; }
                                int index = candidates.FindIndex(value.SameFormat);
                                if (index < 0) { matches = false; break; }
                                candidates.RemoveAt(index);
                            }
                            if (!matches) throw new IOException("Chữ hoặc định dạng trong PDF vừa ghi không khớp bản xem trước. Chưa thay file gốc.");
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

    private static List<TextState> ReadTextObjects(nint page)
    {
        var result = new List<TextState>();
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
                if (!FPDFPageObj_GetMatrix(obj, out var matrix) || !FPDFPageObj_GetFillColor(obj, out uint r, out uint g, out uint b, out uint a))
                    throw new IOException("Không đọc được định dạng chữ để đối chiếu PDF.");
                string text = ObjectText(obj, textPage);
                result.Add(new(text, FontName(FPDFTextObj_GetFont(obj)), size, matrix, r, g, b, a,
                    FPDFTextObj_GetTextRenderMode(obj), MarkIdentity(obj), ReadPositions(obj, textPage, text)));
            }
        }
        finally { PdfiumEngine.FPDFText_ClosePage(textPage); }
        return result;
    }

    private static string MarkIdentity(nint obj)
    {
        var result = new StringBuilder();
        int count = FPDFPageObj_CountMarks(obj);
        if (count < 0) throw new IOException("Không đọc được thẻ của chữ PDF.");
        for (uint i = 0; i < count; i++)
        {
            nint mark = FPDFPageObj_GetMark(obj, i);
            if (mark == 0 || !FPDFPageObjMark_GetName(mark, null, 0, out uint length) || length > 65536)
                throw new IOException("Không đọc được tên thẻ của chữ PDF.");
            var bytes = new byte[length];
            if (!FPDFPageObjMark_GetName(mark, bytes, length, out _)) throw new IOException("Không đọc được tên thẻ của chữ PDF.");
            result.Append(Convert.ToBase64String(bytes)).Append(':').Append(FPDFPageObjMark_CountParams(mark)).Append(';');
        }
        return result.ToString();
    }
}
