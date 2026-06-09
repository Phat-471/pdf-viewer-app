using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace PdfViewerApp;

internal static class PdfSnapshotImageRenderer
{
	public const int DefaultLongEdge = 2400;

	private const int DefaultMaxPixels = 8000000;

	public static BitmapSource RenderSnapshotToBitmap(PdfSnapshotSelection snapshot, int longEdgePixels = 2400, int maxPixels = 8000000)
	{
		PdfiumEngine.Initialize();
		lock (PdfiumEngine.SyncRoot)
		{
			nint num = PdfiumEngine.FPDF_LoadDocument(snapshot.PdfPath, null);
			if (num == IntPtr.Zero)
			{
				throw new InvalidOperationException("Unable to load PDF for snapshot.");
			}
			try
			{
				nint num2 = PdfiumEngine.FPDF_LoadPage(num, snapshot.PageIndex);
				if (num2 == IntPtr.Zero)
				{
					throw new InvalidOperationException("Unable to load PDF page for snapshot.");
				}
				try
				{
					double num3 = PdfiumEngine.FPDF_GetPageWidth(num2);
					double num4 = PdfiumEngine.FPDF_GetPageHeight(num2);
					double num5 = Math.Max(0.05, snapshot.Width * num3) / Math.Max(0.05, snapshot.Height * num4);
					int num6 = ((num5 >= 1.0) ? longEdgePixels : Math.Max(300, (int)Math.Round((double)longEdgePixels * num5)));
					int num7 = ((num5 >= 1.0) ? Math.Max(300, (int)Math.Round((double)longEdgePixels / num5)) : longEdgePixels);
					long num8 = (long)num6 * (long)num7;
					if (num8 > maxPixels)
					{
						double num9 = Math.Sqrt((double)maxPixels / (double)num8);
						num6 = Math.Max(1, (int)Math.Round((double)num6 * num9));
						num7 = Math.Max(1, (int)Math.Round((double)num7 * num9));
					}
					int num10 = Math.Max(1, (int)Math.Round((double)num6 / Math.Max(0.001, snapshot.Width)));
					int num11 = Math.Max(1, (int)Math.Round((double)num7 / Math.Max(0.001, snapshot.Height)));
					int tileX = Math.Max(0, (int)Math.Round(snapshot.X * (double)num10));
					int tileY = Math.Max(0, (int)Math.Round(snapshot.Y * (double)num11));
					return PdfiumEngine.RenderPageTileToBitmap(num, snapshot.PageIndex, num10, num11, tileX, tileY, num6, num7) ?? throw new InvalidOperationException("Unable to render snapshot image.");
				}
				finally
				{
					PdfiumEngine.FPDF_ClosePage(num2);
				}
			}
			finally
			{
				PdfiumEngine.FPDF_CloseDocument(num);
			}
		}
	}

	public static byte[] RenderSnapshotToPngBytes(PdfSnapshotSelection snapshot, int longEdgePixels = 2400, int maxPixels = 8000000)
	{
		BitmapSource source = RenderSnapshotToBitmap(snapshot, longEdgePixels, maxPixels);
		PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
		pngBitmapEncoder.Frames.Add(BitmapFrame.Create(source));
		using MemoryStream memoryStream = new MemoryStream();
		pngBitmapEncoder.Save(memoryStream);
		return memoryStream.ToArray();
	}
}
