namespace PdfViewerApp;

public class PdfHighlightAnnotation : PdfAnnotation
{
	public double Width { get; set; }

	public double Height { get; set; }

	public string ColorHex { get; set; } = "#FFFF00"; // Default to yellow
}
