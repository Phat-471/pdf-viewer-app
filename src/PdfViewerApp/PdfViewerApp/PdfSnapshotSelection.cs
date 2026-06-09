namespace PdfViewerApp;

internal sealed record PdfSnapshotSelection(string PdfPath, int PageIndex, double X, double Y, double Width, double Height);
