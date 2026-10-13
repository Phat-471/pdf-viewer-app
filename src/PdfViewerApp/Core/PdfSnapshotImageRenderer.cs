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
		using var session = new PdfRenderSession(snapshot.PdfPath);
		return session.RenderSnapshot(snapshot, longEdgePixels, maxPixels)
			?? throw new InvalidOperationException("Unable to render snapshot image.");
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
