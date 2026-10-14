#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PdfViewerApp;

internal static partial class PdfNativeTextEditor
{
    [DllImport(Dll)] private static extern bool FPDFText_GetCharOrigin(nint textPage, int index, out double x, out double y);
    [DllImport(Dll)] private static extern int FPDFText_IsGenerated(nint textPage, int index);
    [DllImport(Dll)] private static extern uint FPDFText_GetUnicode(nint textPage, int index);
    [DllImport(Dll)] private static extern bool FPDFText_SetPositions(nint obj, float[] positions, nuint count);
    [DllImport(Dll)] private static extern nint FPDFTextObj_GetRenderedBitmap(nint doc, nint page, nint obj, float scale);
    [DllImport(Dll)] private static extern int FPDFBitmap_GetWidth(nint bitmap);
    [DllImport(Dll)] private static extern int FPDFBitmap_GetHeight(nint bitmap);
    [DllImport(Dll)] private static extern int FPDFBitmap_GetStride(nint bitmap);
    [DllImport(Dll)] private static extern int FPDFBitmap_GetFormat(nint bitmap);
    [DllImport(Dll)] private static extern nint FPDFBitmap_GetBuffer(nint bitmap);
    [DllImport(Dll)] private static extern void FPDFBitmap_Destroy(nint bitmap);

    // Only unambiguous horizontal, one-codepoint-per-glyph sequences are preserved.
    // A rotated horizontal line is still horizontal in its own text coordinates.
    private static float[]? ReadPositions(nint obj, nint textPage, string text)
    {
        if (text.Length <= 1 || !FPDFPageObj_GetMatrix(obj, out var m)) return null;
        double determinant = m.A * (double)m.D - m.B * (double)m.C;
        if (Math.Abs(determinant) < 1e-10) return null;
        var origins = new List<(double X, double Y)>();
        var chars = new System.Text.StringBuilder();
        for (int i = 0; i < PdfiumEngine.FPDFText_CountChars(textPage); i++)
        {
            if (FPDFText_GetTextObject(textPage, i) != obj || FPDFText_IsGenerated(textPage, i) != 0) continue;
            uint unicode = FPDFText_GetUnicode(textPage, i);
            if (unicode > char.MaxValue || !FPDFText_GetCharOrigin(textPage, i, out double x, out double y)) return null;
            chars.Append((char)unicode);
            x -= m.E; y -= m.F;
            origins.Add(((m.D * x - m.C * y) / determinant, (-m.B * x + m.A * y) / determinant));
        }
        if (chars.ToString() != text || origins.Count != text.Length || Math.Abs(origins[0].X) > .01) return null;
        if (origins.Any(p => !double.IsFinite(p.X) || Math.Abs(p.Y - origins[0].Y) > .01)) return null;
        return origins.Skip(1).Select(p => (float)p.X).ToArray();
    }

    private static void SetPositions(nint obj, float[]? positions)
    {
        if (positions is { Length: > 0 } && !FPDFText_SetPositions(obj, positions, (nuint)positions.Length))
            throw new InvalidOperationException("Không giữ được khoảng cách ký tự gốc. Chưa áp dụng thay đổi.");
    }

    internal sealed class PreviewSession : IDisposable
    {
        private nint _doc, _page, _original;
        private readonly int _index;
        private readonly string _text;
        private readonly float[]? _positions;
        private readonly PdfNativeTextRegion _region;
        private readonly Dictionary<PdfNativeTextFont, nint> _fonts = new();

        internal PreviewSession(string path, PdfNativeTextRegion region, IReadOnlyList<PdfNativeTextEdit> edits)
        {
            _region = region; _text = region.OriginalText;
            lock (PdfiumEngine.SyncRoot)
            {
                try
                {
                    PdfiumEngine.Initialize();
                    _doc = PdfiumEngine.FPDF_LoadDocument(path, null);
                    if (_doc == 0) throw new InvalidOperationException("Không mở được PDF để sửa chữ.");
                    string? restriction = DocumentRestriction(_doc);
                    if (restriction != null) throw new InvalidOperationException(restriction);
                    _page = PdfiumEngine.FPDF_LoadPage(_doc, region.PageNumber - 1);
                    if (_page == 0) throw new InvalidOperationException("Không đọc được trang PDF.");
                    _original = FPDFPage_GetObject(_page, region.ObjectIndex);
                    if (_original == 0 || FPDFPageObj_GetType(_original) != 1) throw new InvalidOperationException("Vùng chữ đã thay đổi.");
                    nint tp = PdfiumEngine.FPDFText_LoadPage(_page);
                    if (tp == 0) throw new InvalidOperationException("Không đọc được chữ gốc.");
                    try
                    {
                        if (ObjectText(_original, tp) != _text) throw new InvalidOperationException("Chữ gốc đã thay đổi. Hãy mở lại PDF.");
                        _positions = ReadPositions(_original, tp, _text);
                    }
                    finally { PdfiumEngine.FPDFText_ClosePage(tp); }
                    var others = edits.Where(e => e.PageNumber == region.PageNumber && e.ObjectIndex != region.ObjectIndex).ToArray();
                    ApplyPage(_doc, _page, others);
                    _index = region.ObjectIndex - others.Count(e => e.ObjectIndex < region.ObjectIndex && e.ReplacementText.Length == 0);
                }
                catch { Dispose(); throw; }
            }
        }

        internal PdfTextEditPreview Preview(string text, PdfNativeTextFont? source)
        {
            lock (PdfiumEngine.SyncRoot)
            {
                ObjectDisposedException.ThrowIf(_doc == 0, this);
                if (text.Any(c => char.IsControl(c) || char.IsSurrogate(c)))
                    throw new InvalidOperationException("Vùng chữ này chỉ nhận một dòng, không có ký tự điều khiển hoặc emoji.");
                if (text.Length == 0) return new(null, _region.Bounds, "Đối tượng chữ sẽ được xóa. Nhấn Áp dụng để xác nhận.");
                nint clip = FPDFPageObj_GetClipPath(_original);
                int mode = FPDFTextObj_GetTextRenderMode(_original);
                if ((clip != 0 && FPDFClipPath_CountPaths(clip) > 0) || FPDFPageObj_HasTransparency(_original) || mode is not (0 or 3))
                    throw new InvalidOperationException("Vùng chữ có hiệu ứng hoặc lớp cắt. Cần sửa trong file nguồn để giữ đúng hiệu ứng.");
                nint font = FPDFTextObj_GetFont(_original);
                if (source != null)
                {
                    PdfTextFontSource.ValidateText(source, text);
                    if (!_fonts.TryGetValue(source, out font))
                    {
                        byte[] data = Convert.FromBase64String(source.DataBase64);
                        font = FPDFText_LoadFont(_doc, data, (uint)data.Length, 2, 1);
                        if (font == 0) throw new InvalidOperationException("Không nhúng được font đã chọn.");
                        _fonts.Add(source, font);
                    }
                }
                if (!FPDFTextObj_GetFontSize(_original, out float size) || !FPDFPageObj_GetMatrix(_original, out var matrix))
                    throw new InvalidOperationException("Không đọc được định dạng gốc.");
                nint candidate = FPDFPageObj_CreateTextObj(_doc, font, size);
                bool inserted = false, detached = false;
                try
                {
                    if (candidate == 0 || !FPDFText_SetText(candidate, text)) throw new PdfTextEncodingException("Font gốc không mã hóa được nội dung mới.");
                    if (!FPDFPageObj_SetMatrix(candidate, ref matrix) || !FPDFTextObj_SetTextRenderMode(candidate, mode) ||
                        !FPDFPageObj_GetFillColor(_original, out uint r, out uint g, out uint b, out uint a) || !FPDFPageObj_SetFillColor(candidate, r, g, b, a))
                        throw new InvalidOperationException("Không giữ được định dạng gốc.");
                    if (text.Length == _text.Length) SetPositions(candidate, _positions);
                    for (uint i = 0; i < FPDFPageObj_CountMarks(_original); i++)
                        if (!FPDFPageObj_AddExistingMark(candidate, FPDFPageObj_GetMark(_original, i)))
                            throw new InvalidOperationException("Không giữ được thẻ của chữ gốc.");
                    if (source != null) CheckTrailingLayer(_page, candidate, _index);
                    if (!FPDFPage_RemoveObject(_page, _original)) throw new InvalidOperationException("Không dựng được bản xem trước.");
                    detached = true;
                    nint owned = candidate; candidate = 0; // PDFium takes ownership even on failure.
                    if (!FPDFPage_InsertObjectAtIndex(_page, owned, (nuint)_index)) throw new InvalidOperationException("Không chèn được chữ xem trước.");
                    candidate = owned; inserted = true;
                    nint tp = PdfiumEngine.FPDFText_LoadPage(_page);
                    if (tp == 0) throw new InvalidOperationException("Không kiểm tra được nội dung mới.");
                    try
                    {
                        if (ObjectText(candidate, tp) != text) throw new PdfTextEncodingException("Font nhúng thiếu ký tự hoặc bảng mã không hỗ trợ chữ mới.");
                    }
                    finally { PdfiumEngine.FPDFText_ClosePage(tp); }
                    if (!FPDFPageObj_GetBounds(candidate, out float l, out float btm, out float right, out float top))
                        throw new InvalidOperationException("Không xác định được vùng chữ mới.");
                    var bounds = new Rect(l, btm, Math.Max(0, right - l), Math.Max(0, top - btm));
                    float scale = (float)Math.Min(4, Math.Min(1600 / Math.Max(1, bounds.Width), 250 / Math.Max(1, bounds.Height)));
                    nint bitmap = FPDFTextObj_GetRenderedBitmap(_doc, _page, candidate, scale);
                    if (bitmap == 0) throw new InvalidOperationException("Không tạo được bản xem trước chữ PDF.");
                    BitmapSource image;
                    try
                    {
                        int w = FPDFBitmap_GetWidth(bitmap), h = FPDFBitmap_GetHeight(bitmap), stride = FPDFBitmap_GetStride(bitmap), format = FPDFBitmap_GetFormat(bitmap);
                        if (w <= 0 || h <= 0 || w > 1800 || h > 400 || stride <= 0 || format is not (2 or 3 or 4))
                            throw new InvalidOperationException("Ảnh xem trước vượt giới hạn hoặc có định dạng chưa hỗ trợ.");
                        var bytes = new byte[checked(stride * h)];
                        Marshal.Copy(FPDFBitmap_GetBuffer(bitmap), bytes, 0, bytes.Length);
                        image = BitmapSource.Create(w, h, 96, 96, format == 2 ? PixelFormats.Bgr24 : format == 3 ? PixelFormats.Bgr32 : PixelFormats.Bgra32, null, bytes, stride);
                        image.Freeze();
                    }
                    finally { FPDFBitmap_Destroy(bitmap); }
                    string spacing = text.Length == _text.Length && _positions != null ? "Giữ vị trí từng ký tự gốc." : "Giữ cỡ chữ; khoảng cách theo font đã chọn.";
                    string warning = bounds.Width > _region.Bounds.Width * 1.05 ? " Chữ mới rộng hơn vùng gốc; cần kiểm tra chữ lân cận." : "";
                    return new(image, bounds, $"{(source?.Name ?? _region.SourceFontName)} · {_region.DisplayFontSize:0.###} pt. {spacing}{warning}");
                }
                finally
                {
                    if (inserted && candidate != 0 && !FPDFPage_RemoveObject(_page, candidate))
                    {
                        candidate = 0;
                        FPDFPageObj_Destroy(_original); _original = 0;
                        Dispose();
                    }
                    if (candidate != 0) FPDFPageObj_Destroy(candidate);
                    if (detached && _page != 0)
                    {
                        if (!FPDFPage_InsertObjectAtIndex(_page, _original, (nuint)_index)) { _original = 0; Dispose(); }
                    }
                }
            }
        }

        public void Dispose()
        {
            lock (PdfiumEngine.SyncRoot)
            {
                foreach (nint font in _fonts.Values) FPDFFont_Close(font);
                _fonts.Clear();
                if (_page != 0) PdfiumEngine.FPDF_ClosePage(_page);
                if (_doc != 0) PdfiumEngine.FPDF_CloseDocument(_doc);
                _page = _doc = _original = 0;
            }
        }
    }

    private static void CheckTrailingLayer(nint page, nint candidate, int index)
    {
        if (!FPDFPageObj_GetBounds(candidate, out float l, out float b, out float r, out float t))
            throw new InvalidOperationException("Không xác định được vị trí chữ mới.");
        var bounds = new Rect(l, b, Math.Max(0, r - l), Math.Max(0, t - b));
        for (int i = index + 1; i < FPDFPage_CountObjects(page); i++)
        {
            nint later = FPDFPage_GetObject(page, i);
            if (FPDFPageObj_GetBounds(later, out l, out b, out r, out t) && r > l && t > b && bounds.IntersectsWith(new Rect(l, b, r - l, t - b)))
                throw new InvalidOperationException("Chữ mới chồng lên đối tượng ở lớp phía trên. Hãy rút ngắn nội dung để giữ đúng thứ tự lớp.");
        }
    }
}
