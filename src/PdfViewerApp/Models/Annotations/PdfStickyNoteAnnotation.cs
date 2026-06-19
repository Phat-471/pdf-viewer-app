namespace PdfViewerApp;

public class PdfStickyNoteAnnotation : PdfAnnotation
{
	public string NoteText { get; set; } = "Nhập ghi chú nhanh ở đây...";

	public string ColorHex { get; set; } = "#FCD34D";
}
