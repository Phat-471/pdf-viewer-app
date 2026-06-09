namespace PdfViewerApp;

public class PdfInkAnnotation : PdfAnnotation
{
	public string Points { get; set; } = string.Empty;

	public double Thickness { get; set; } = 2.0;
}
