namespace PdfViewerApp;

public class PdfShapeAnnotation : PdfAnnotation
{
	public ShapeType Type { get; set; }

	public double Width { get; set; }

	public double Height { get; set; }

	public double Thickness { get; set; } = 2.0;

	public double EndX { get; set; }

	public double EndY { get; set; }
}
