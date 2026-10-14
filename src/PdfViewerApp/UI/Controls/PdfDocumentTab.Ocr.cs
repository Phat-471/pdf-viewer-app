using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace PdfViewerApp;
public partial class PdfDocumentTab
{
    private PdfOcrOptions CurrentOcrOptions()
    {
        var p = AppPreferences.Load();
        string language = p.OcrLanguage == "vie+eng" ? "vie+eng" : p.OcrLanguage.StartsWith("vi", StringComparison.OrdinalIgnoreCase) ? "vie" : p.OcrLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "eng" : "vie+eng";
        return new PdfOcrOptions(language, p.OcrDpi, p.OcrPreferNative, p.OcrSparse);
    }

    public async Task PreloadOcrForSelectedPageAsync()
    {
        if (PageCount <= 0) return;
        try { var result = await EnsureOcrRegionsAsync(Math.Clamp(SelectedPageNumber, 1, PageCount)); LogStatus($"Đã đọc {result?.Count ?? 0} vùng chữ."); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { LogStatus("Không đọc được chữ: " + ex.Message); }
    }

    private Task<List<OcrTextRegion>?> EnsureOcrRegionsAsync(int pageNumber)
    {
        string path = CurrentPdfPath; var token = _ocrLifetime.Token; var options = CurrentOcrOptions();
        string key = $"{_loadGeneration}:{pageNumber}:{options}";
        if (_ocrLoadingTasks.Count > 64) _ocrLoadingTasks.Clear();
        var task = _ocrLoadingTasks.GetOrAdd(key, _ => Task.Run(async () =>
        {
            try
            {
                using var session = new PdfRenderSession(path);
                var result = await PdfOcrService.RecognizeAsync(session, pageNumber - 1, options, null, token);
                return result.Select(r => session.MapOcrToPdf(pageNumber - 1, r)).Select(r => new OcrTextRegion(r.Text, r.Left, r.Bottom, r.Width, r.Height)).ToList();
            }
            catch { _ocrLoadingTasks.TryRemove(key, out var ignored); throw; }
        }, token));
        _ = task.ContinueWith(completed => { if (completed.IsFaulted || completed.IsCanceled || completed.Result?.Count == 0) _ocrLoadingTasks.TryRemove(key, out var ignored); }, TaskScheduler.Default);
        return task;
    }

    public Task ExportOcrTextAsync() => ExportOcrAsync(searchable: false);
    public Task ExportSearchablePdfAsync() => ExportOcrAsync(searchable: true);

    private async Task ExportOcrAsync(bool searchable)
    {
        if (string.IsNullOrEmpty(CurrentPdfPath) || PageCount <= 0) { MessageBox.Show("Mở PDF trước khi đọc chữ."); return; }
        string source = CurrentPdfPath; int generation = _loadGeneration;
        var settings = new OcrOptionsDialog(_snapshotBounds.HasValue && _snapshotPageNumber > 0) { Owner = Window.GetWindow(this) };
        if (settings.ShowDialog() != true) return;
        if (searchable && settings.Scope == "Region")
        { MessageBox.Show("PDF tìm kiếm áp dụng cho cả trang hoặc tài liệu. Dùng xuất văn bản để đọc riêng vùng Snapshot."); return; }
        var save = new SaveFileDialog
        {
            Filter = searchable ? "PDF (*.pdf)|*.pdf" : "Văn bản (*.txt)|*.txt|Word (*.docx)|*.docx",
            FileName = Path.GetFileNameWithoutExtension(source) + (searchable ? "_Searchable.pdf" : "_OCR.txt"),
            Title = searchable ? "Lưu PDF có thể tìm kiếm" : "Lưu kết quả đọc chữ"
        };
        if (save.ShowDialog(Window.GetWindow(this)) != true) return;
        if (IsSamePath(source, save.FileName)) { MessageBox.Show("Chọn tên file khác với PDF nguồn."); return; }
        var pages = settings.Scope == "All" ? Enumerable.Range(1, PageCount).ToArray() : new[] { settings.Scope == "Region" ? _snapshotPageNumber : SelectedPageNumber };
        Rect? region = settings.Scope == "Region" ? _snapshotBounds : null;
        var progress = new PrintProgressDialog("OCR") { Owner = Window.GetWindow(this) };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_ocrLifetime.Token, progress.CancellationToken);
        var token = cancellation.Token;
        IProgress<PrintProgressInfo> report = new Progress<PrintProgressInfo>(progress.UpdateProgress);
        progress.Show();
        try
        {
            var text = new StringBuilder(); var data = new StringBuilder();
            await Task.Run(async () =>
            {
                using var session = new PdfRenderSession(source);
                foreach (int number in pages)
                {
                    token.ThrowIfCancellationRequested();
                    report.Report(new PrintProgressInfo($"Đang đọc trang {number}…", Array.IndexOf(pages, number), pages.Length));
                    var result = await PdfOcrService.RecognizeAsync(session, number - 1, settings.Options, region, token, report);
                    if (pages.Length > 1) text.AppendLine($"--- Trang {number} ---");
                    text.AppendLine(PdfOcrService.ToText(result)); text.AppendLine();
                    if (searchable)
                    {
                        // Original usable text is already searchable; never duplicate its text layer.
                        bool hasNative = settings.Options.PreferNative && session.ReadNativeText(number - 1, new Rect(0, 0, 1, 1), token).Sum(r => r.Text.Count(char.IsLetterOrDigit)) >= 3;
                        if (!hasNative)
                            foreach (var recognized in result) { token.ThrowIfCancellationRequested(); var word = session.MapOcrToPdf(number - 1, recognized); data.AppendLine(FormattableString.Invariant($"{number}|{word.Left:F2}|{word.Bottom:F2}|{word.Width:F2}|{word.Height:F2}|{word.Text.Replace("|", " ").Replace("\n", " ").Replace("\r", " ")}")); }
                    }
                }
            }, token);
            token.ThrowIfCancellationRequested();
            if (generation != _loadGeneration) throw new OperationCanceledException();
            if (!searchable && string.IsNullOrWhiteSpace(text.ToString())) { progress.CloseAfterSuccess(); MessageBox.Show("Không tìm thấy chữ trong phạm vi đã chọn."); return; }
            string output = save.FileName;
            await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                string temp = Path.Combine(Path.GetDirectoryName(output)!, ".PDFPro-OCR-" + Guid.NewGuid().ToString("N") + Path.GetExtension(output));
                try
                {
                    if (searchable)
                    {
                        if (data.Length == 0) File.Copy(source, temp);
                        else if (!PdfInterop.PdfCore.make_pdf_searchable(source, data.ToString(), temp)) throw new InvalidOperationException("Không ghép được lớp chữ vào PDF.");
                    }
                    else if (Path.GetExtension(output).Equals(".docx", StringComparison.OrdinalIgnoreCase)) SaveAsDocx(temp, text.ToString());
                    else File.WriteAllText(temp, text.ToString(), Encoding.UTF8);
                    token.ThrowIfCancellationRequested(); File.Move(temp, output, overwrite: true);
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }, token);
            progress.CloseAfterSuccess(); LogStatus("Đã lưu: " + Path.GetFileName(output));
            if (searchable) DocumentOpenRequested?.Invoke(this, output);
            else new OcrResultDialog(text.ToString()) { Owner = Window.GetWindow(this) }.ShowDialog();
        }
        catch (OperationCanceledException) { progress.CloseAfterSuccess(); LogStatus("Đã huỷ OCR."); }
        catch (Exception ex) { progress.MarkFailed("Không đọc được chữ: " + ex.Message); }
    }

    private void TryShowOcrTextEditOverlayAsync(Canvas canvas, Point clickPoint, int pageNumber) { }
		private static void SaveAsDocx(string filePath, string text)
		{
			using (var fileStream = new FileStream(filePath, FileMode.Create))
			using (var archive = new System.IO.Compression.ZipArchive(fileStream, System.IO.Compression.ZipArchiveMode.Create))
			{
				var contentTypesEntry = archive.CreateEntry("[Content_Types].xml");
				using (var writer = new StreamWriter(contentTypesEntry.Open(), Encoding.UTF8))
				{
					writer.Write(@"<?xml version=""1.0"" encoding=""utf-8""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""xml"" ContentType=""application/xml"" />
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml"" />
  <Override PartName=""/word/document.xml"" ContentType=""application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"" />
</Types>");
				}

				var relsEntry = archive.CreateEntry("_rels/.rels");
				using (var writer = new StreamWriter(relsEntry.Open(), Encoding.UTF8))
				{
					writer.Write(@"<?xml version=""1.0"" encoding=""utf-8""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""word/document.xml"" />
</Relationships>");
				}

				var documentEntry = archive.CreateEntry("word/document.xml");
				using (var writer = new StreamWriter(documentEntry.Open(), Encoding.UTF8))
				{
					string cleanedText = System.Security.SecurityElement.Escape(text)
						.Replace("\r\n", "\n")
						.Replace("\r", "\n");
					
					var bodyBuilder = new StringBuilder();
					foreach (var line in cleanedText.Split('\n'))
					{
						bodyBuilder.Append("<w:p><w:r><w:t>").Append(line).Append("</w:t></w:r></w:p>");
					}

					writer.Write($@"<?xml version=""1.0"" encoding=""utf-8""?>
<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
  <w:body>
    {bodyBuilder}
  </w:body>
</w:document>");
				}
			}
		}


}
