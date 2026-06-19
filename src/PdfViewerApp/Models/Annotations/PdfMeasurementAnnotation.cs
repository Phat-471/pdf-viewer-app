using System.Collections.Generic;
using System.Windows;

namespace PdfViewerApp;

public class PdfMeasurementAnnotation : PdfAnnotation
{
	// "Distance" or "Area"
	public string MeasurementType { get; set; } = "Distance";

	// Scale factor (e.g. 100.0 means 1 unit in PDF represents 100 units in reality)
	public double Scale { get; set; } = 100.0;

	// Points normalized relative to page dimensions (X: 0 to 1, Y: 0 to 1)
	public List<Point> Points { get; set; } = new List<Point>();

	public double Thickness { get; set; } = 2.0;
}
