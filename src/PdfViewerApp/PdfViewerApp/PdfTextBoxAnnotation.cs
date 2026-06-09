namespace PdfViewerApp;

public class PdfTextBoxAnnotation : PdfAnnotation
{
	public double Width { get; set; }

	public double Height { get; set; }

	public string Text { get; set; } = "Nhập ghi chú...";
}
