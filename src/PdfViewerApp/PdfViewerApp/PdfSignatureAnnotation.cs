using System.Collections.Generic;
using System.Windows;

namespace PdfViewerApp;

public class PdfSignatureAnnotation : PdfAnnotation
{
	// "Handwrite" or "Stamp"
	public string SignatureType { get; set; } = "Handwrite";

	// Text content for Stamps (e.g., "ĐÃ DUYỆT")
	public string StampText { get; set; } = "";

	// Handdrawn strokes (each stroke is a list of points relative to (X, Y) bounding box)
	public List<List<Point>> Strokes { get; set; } = new List<List<Point>>();

	public double Width { get; set; }

	public double Height { get; set; }

	public double OriginalWidth { get; set; }

	public double OriginalHeight { get; set; }

	public double Thickness { get; set; } = 3.0;
}
