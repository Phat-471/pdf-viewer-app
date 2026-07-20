using System;
using System.Windows.Media;

namespace PdfViewerApp;

public abstract class PdfAnnotation
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public string AnnotationGroupId { get; set; } = string.Empty;

	public int PageIndex { get; set; }

	public double X { get; set; }

	public double Y { get; set; }

	public Color StrokeColor { get; set; } = Colors.Red;

	public string FontFamily { get; set; } = "Segoe UI";

	public double FontSize { get; set; } = 14.0;

	public bool IsBold { get; set; }

	public bool IsItalic { get; set; }

	public bool IsUnderline { get; set; }

	public Color BgColor { get; set; } = Colors.Transparent;

	public double Opacity { get; set; } = 1.0;

	public bool IsStrikeout { get; set; }

	public bool IsSubscript { get; set; }

	public bool IsSuperscript { get; set; }

	public System.Windows.TextAlignment TextAlignment { get; set; } = System.Windows.TextAlignment.Left;
}
