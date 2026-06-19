namespace PdfViewerApp;

public sealed record PrintProgressInfo(string Message, int CurrentPage, int TotalPages, bool IsIndeterminate = false);
