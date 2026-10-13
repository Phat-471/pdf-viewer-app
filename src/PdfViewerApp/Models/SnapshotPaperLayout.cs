using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PdfViewerApp;

internal sealed record SnapshotPaperLayout(string PaperKey, bool Landscape, bool FitToPage,
    double ScalePercent = 100, double MarginMm = 10)
{
    internal Size PaperSizeMm
    {
        get
        {
            Size size = PaperKey switch
            {
                "A0" => new Size(841, 1189),
                "A1" => new Size(594, 841),
                "A2" => new Size(420, 594),
                "A4" => new Size(210, 297),
                "Letter" => new Size(215.9, 279.4),
                _ => new Size(297, 420)
            };
            return Landscape ? new Size(size.Height, size.Width) : size;
        }
    }

    internal Rect ImageBoundsMm(Size cropPoints)
    {
        Size paper = PaperSizeMm;
        double width = cropPoints.Width * 25.4 / 72;
        double height = cropPoints.Height * 25.4 / 72;
        double scale = FitToPage
            ? Math.Min((paper.Width - MarginMm * 2) / width, (paper.Height - MarginMm * 2) / height)
            : ScalePercent / 100;
        width *= scale;
        height *= scale;
        return new Rect((paper.Width - width) / 2, (paper.Height - height) / 2, width, height);
    }

    internal bool IsClipped(Size cropPoints)
    {
        Rect bounds = ImageBoundsMm(cropPoints);
        Size paper = PaperSizeMm;
        return bounds.Width > paper.Width - MarginMm * 2 + 0.01 || bounds.Height > paper.Height - MarginMm * 2 + 0.01;
    }

    internal BitmapSource Compose(BitmapSource crop, Size cropPoints, int longEdge, bool showGuides = false)
    {
        Size paper = PaperSizeMm;
        double pixelsPerMm = Math.Clamp(longEdge, 300, 4800) / Math.Max(paper.Width, paper.Height);
        int width = Math.Max(1, (int)Math.Round(paper.Width * pixelsPerMm));
        int height = Math.Max(1, (int)Math.Round(paper.Height * pixelsPerMm));
        Rect bounds = ImageBoundsMm(cropPoints);
        Rect clip = new Rect(MarginMm * pixelsPerMm, MarginMm * pixelsPerMm,
            (paper.Width - 2 * MarginMm) * pixelsPerMm, (paper.Height - 2 * MarginMm) * pixelsPerMm);
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            dc.PushClip(new RectangleGeometry(clip));
            dc.DrawImage(crop, new Rect(bounds.X * pixelsPerMm, bounds.Y * pixelsPerMm,
                bounds.Width * pixelsPerMm, bounds.Height * pixelsPerMm));
            dc.Pop();
            if (showGuides)
            {
                var pen = new Pen(Brushes.Teal, 1) { DashStyle = DashStyles.Dash };
                dc.DrawRectangle(null, pen, clip);
            }
        }
        // Keep the physical paper size in exported PNG/clipboard bitmap metadata.
        double dpi = pixelsPerMm * 25.4;
        var result = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        result.Render(visual);
        result.Freeze();
        var bitmap = BitmapSource.Create(width, height, dpi, dpi, PixelFormats.Pbgra32, null,
            CopyPixels(result), width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static byte[] CopyPixels(BitmapSource source)
    {
        byte[] pixels = new byte[checked(source.PixelWidth * source.PixelHeight * 4)];
        source.CopyPixels(pixels, source.PixelWidth * 4, 0);
        return pixels;
    }
}
