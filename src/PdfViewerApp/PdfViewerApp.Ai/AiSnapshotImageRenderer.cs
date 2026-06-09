using System;

namespace PdfViewerApp.Ai;

internal static class AiSnapshotImageRenderer
{
	private const int MaxSnapshotPixels = 3000000;

	public static string RenderSnapshotToPngBase64(PdfSnapshotSelection snapshot)
	{
		return Convert.ToBase64String(PdfSnapshotImageRenderer.RenderSnapshotToPngBytes(snapshot, 1800, 3000000));
	}
}
