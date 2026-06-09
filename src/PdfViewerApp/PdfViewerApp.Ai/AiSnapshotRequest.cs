namespace PdfViewerApp.Ai;

public sealed record AiSnapshotRequest(string Prompt, string PngBase64, int PageNumber, double X, double Y, double Width, double Height);
