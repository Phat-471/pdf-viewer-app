using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PdfViewerApp;

internal static class PdfDirectPrintJob
{
    internal static void Print(string sourcePath, string printerName, IReadOnlyList<int> pageIndices,
        int originalPageCount, int copies, bool reverse, IProgress<PrintProgressInfo>? progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (pageIndices.Count == 0 || pageIndices.Any(p => p < 0 || p >= originalPageCount))
            throw new ArgumentOutOfRangeException(nameof(pageIndices));
        int[] ordered = reverse ? pageIndices.Reverse().ToArray() : pageIndices.ToArray();
        string printPath = sourcePath;
        string? temporaryPath = null;
        try
        {
            // Stream an unchanged full document directly; parse only when selecting or reordering pages.
            if (!ordered.SequenceEqual(Enumerable.Range(0, originalPageCount)))
            {
                progress?.Report(new PrintProgressInfo("Đang chuẩn bị các trang để in trực tiếp…", 0, copies, true));
                using var source = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import);
                using var output = new PdfDocument();
                foreach (int index in ordered)
                {
                    token.ThrowIfCancellationRequested();
                    output.AddPage(source.Pages[index]);
                }
                token.ThrowIfCancellationRequested();
                temporaryPath = Path.Combine(Path.GetTempPath(), "PDFPro-direct-" + Guid.NewGuid().ToString("N") + ".pdf");
                output.Save(temporaryPath);
                printPath = temporaryPath;
            }
            for (int copy = 1; copy <= copies; copy++)
            {
                token.ThrowIfCancellationRequested();
                progress?.Report(new PrintProgressInfo($"Đang gửi trực tiếp PDF, bản {copy}/{copies}…", copy - 1, copies));
                NativePdfPrinter.PrintPdfDirectOptimized(printPath, printerName,
                    "PDF Pro - " + Path.GetFileName(sourcePath) + $" ({copy}/{copies})", token);
                progress?.Report(new PrintProgressInfo($"Đã gửi bản {copy}/{copies} tới máy in.", copy, copies));
            }
        }
        finally
        {
            if (temporaryPath != null)
            {
                try { File.Delete(temporaryPath); }
                catch (Exception ex) { PdfPerfLogger.Log("Direct print temp cleanup: " + ex.Message); }
            }
        }
    }
}
