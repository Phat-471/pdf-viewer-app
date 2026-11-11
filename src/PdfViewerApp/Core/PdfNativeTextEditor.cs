#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using PdfViewerApp.Services;

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

internal sealed record PdfTextEditPreview(System.Windows.Media.Imaging.BitmapSource? Image, Rect Bounds, string Message);

internal sealed class PdfTextEncodingException : InvalidOperationException
{
    internal PdfTextEncodingException(string message) : base(message) { }
}

/// <summary>
/// Động cơ Biên tập Văn bản PDF Kế thừa 100% từ pdfcraft (PdfCraft Document Session):
/// - Sử dụng pdfcraft-cos + pdfcraft-edit chạy trực tiếp trên bộ nhớ RAM
/// - Nhóm dòng thành TextBlock chính xác, tự động reflow văn bản
/// - Tự động tạo Type 3 vector glyph fallback font cho tiếng Việt
/// - Hỗ trợ sửa liên tiếp vô hạn các vị trí trên trang mà không bị lỗi lệch index hay văng exception
/// - Không còn phụ thuộc vào các API can thiệp đối tượng mỏng manh của PDFium
/// </summary>
internal static partial class PdfNativeTextEditor
{
    /// <summary>
    /// Đọc danh sách các khối đoạn văn bản trên trang thông qua phiên làm việc Rust PdfCore
    /// </summary>
    internal static List<PdfNativeTextRegion> ReadRegions(ulong sessionId, string path, int pageNumber, double pageWidth, double pageHeight)
    {
        var result = new List<PdfNativeTextRegion>();
        if (pageNumber < 1 || pageWidth <= 0 || pageHeight <= 0)
            return result;

        List<PdfCoreInterop.PdfBlockDto> blocks;
        if (sessionId != 0)
        {
            blocks = PdfCoreInterop.GetSessionBlocks(sessionId, pageNumber);
        }
        else
        {
            blocks = PdfCoreInterop.GetTextBlocks(path, pageNumber);
        }

        foreach (var b in blocks)
        {
            if (b.Rect == null || b.Rect.Length < 4) continue;
            double minX = b.Rect[0];
            double minY = b.Rect[1];
            double maxX = b.Rect[2];
            double maxY = b.Rect[3];

            // Tọa độ PDF chuẩn: (0,0) ở góc dưới bên trái, trục Y hướng lên trên
            // Tọa độ Canvas/Màn hình WPF: (0,0) ở góc trên bên trái, trục Y hướng xuống dưới
            double normX = Math.Clamp(minX / pageWidth, 0.0, 1.0);
            double normY = Math.Clamp((pageHeight - maxY) / pageHeight, 0.0, 1.0);
            double normW = Math.Clamp((maxX - minX) / pageWidth, 0.0, 1.0);
            double normH = Math.Clamp((maxY - minY) / pageHeight, 0.0, 1.0);

            var bounds = new Rect(minX, minY, Math.Max(1.0, maxX - minX), Math.Max(1.0, maxY - minY));
            var displayBounds = new Rect(normX, normY, Math.Max(0.005, normW), Math.Max(0.005, normH));

            uint textColor = 0xFF000000;
            if (b.ColorRgb != null && b.ColorRgb.Length >= 3)
            {
                byte r = (byte)Math.Clamp((int)(b.ColorRgb[0] * 255), 0, 255);
                byte g = (byte)Math.Clamp((int)(b.ColorRgb[1] * 255), 0, 255);
                byte bCol = (byte)Math.Clamp((int)(b.ColorRgb[2] * 255), 0, 255);
                textColor = 0xFF000000 | ((uint)r << 16) | ((uint)g << 8) | bCol;
            }

            string text = !string.IsNullOrEmpty(b.EditorText) ? b.EditorText : b.Text;
            result.Add(new PdfNativeTextRegion(
                PageNumber: pageNumber,
                ObjectIndex: b.Index, // Index của TextBlock trong pdfcraft
                OriginalText: text,
                Text: text,
                Bounds: bounds,
                DisplayBounds: displayBounds,
                FontName: b.FontName,
                FontSize: b.FontSize > 0 ? b.FontSize : 12,
                Embedded: true,
                UnsupportedReason: null
            )
            {
                Angle = b.Rotation,
                TextColor = textColor
            });
        }

        return result;
    }

    /// <summary>
    /// Ghi nhận thay đổi một khối đoạn văn bản trực tiếp vào session pdfcraft
    /// </summary>
    internal static bool CommitBlock(ulong sessionId, int pageNumber, int blockIndex, string newText, double newWidth = 0)
    {
        if (sessionId == 0 || pageNumber < 1 || blockIndex < 0) return false;
        return PdfCoreInterop.pdf_session_commit_block(sessionId, pageNumber, blockIndex, newText, newWidth);
    }

    /// <summary>
    /// Xóa một khối đoạn văn bản trong session pdfcraft
    /// </summary>
    internal static bool DeleteBlock(ulong sessionId, int pageNumber, int blockIndex)
    {
        if (sessionId == 0 || pageNumber < 1 || blockIndex < 0) return false;
        return PdfCoreInterop.pdf_session_delete_block(sessionId, pageNumber, blockIndex);
    }

    /// <summary>
    /// Xuất tài liệu hiện tại của session ra file PDF trên đĩa
    /// </summary>
    internal static bool Export(ulong sessionId, string outputPath, bool incremental = false)
    {
        if (sessionId == 0 || string.IsNullOrEmpty(outputPath)) return false;
        return PdfCoreInterop.pdf_session_export(sessionId, outputPath, incremental);
    }

    /// <summary>
    /// Hoàn tác bước sửa gần nhất trong session pdfcraft
    /// </summary>
    internal static bool Undo(ulong sessionId) => sessionId != 0 && PdfCoreInterop.pdf_session_undo(sessionId);

    /// <summary>
    /// Làm lại bước sửa đã hoàn tác trong session pdfcraft
    /// </summary>
    internal static bool Redo(ulong sessionId) => sessionId != 0 && PdfCoreInterop.pdf_session_redo(sessionId);

    /// <summary>
    /// Đóng và giải phóng session pdfcraft
    /// </summary>
    internal static void CloseSession(ulong sessionId)
    {
        if (sessionId != 0)
        {
            PdfCoreInterop.pdf_session_close(sessionId);
        }
    }
}
