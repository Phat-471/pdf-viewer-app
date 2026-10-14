using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PdfViewerApp;

public partial class PdfDocumentTab
{
    private readonly Dictionary<int, List<PdfNativeTextRegion>> _pageEditableRegions = new();
    private readonly Dictionary<int, Task<List<PdfNativeTextRegion>>> _textRegionLoads = new();
    private readonly List<PdfNativeTextEdit> _nativeTextEdits = new();
    private bool _isHighlightEditableRegionsEnabled;
    private bool _textEditDialogOpen;
    private int _textEditRevision;
    private bool _savingDocument;
    private sealed record DocumentEditSnapshot(List<PdfAnnotation> Annotations, PdfNativeTextEdit[] TextEdits);

    public async void SetHighlightEditableRegions(bool enable)
    {
        _isHighlightEditableRegionsEnabled = enable;
        if (enable && PageCount > 0)
        {
            try
            {
                var regions = await GetEditableTextRegionsAsync(Math.Clamp(SelectedPageNumber, 1, PageCount));
                LogStatus(regions.Count == 0 ? "Trang không có chữ PDF để sửa. OCR nhận dạng chữ trong ảnh; font gốc không thể khôi phục từ ảnh."
                    : "Chọn khung chữ → nhập nội dung → xem trước → áp dụng → lưu PDF. Khung xanh là chữ có thể sửa.");
            }
            catch (Exception ex) { LogStatus("Không đọc được vùng chữ: " + ex.Message); }
        }
        RedrawAllPageAnnotations();
    }

    private Task<List<PdfNativeTextRegion>> GetEditableTextRegionsAsync(int pageNumber)
    {
        if (_pageEditableRegions.TryGetValue(pageNumber, out var saved)) return Task.FromResult(saved);
        if (_textRegionLoads.TryGetValue(pageNumber, out var loading)) return loading;
        string path = CurrentPdfPath;
        int generation = _loadGeneration, revision = _textEditRevision;
        var edits = _nativeTextEdits.ToArray();
        async Task<List<PdfNativeTextRegion>> LoadAsync()
        {
            try
            {
                var regions = await Task.Run(() => PdfNativeTextEditor.ReadRegions(path, pageNumber, edits));
                if (generation == _loadGeneration && revision == _textEditRevision)
                    _pageEditableRegions[pageNumber] = regions;
                return regions;
            }
            finally { if (generation == _loadGeneration && revision == _textEditRevision) _textRegionLoads.Remove(pageNumber); }
        }
        var task = LoadAsync(); _textRegionLoads[pageNumber] = task; return task;
    }

    private async void DrawEditableTextRegionsHighlight(Canvas canvas, int pageNumber)
    {
        if (!_isHighlightEditableRegionsEnabled || ActiveTool != "EditText") return;
        if (!_pageEditableRegions.TryGetValue(pageNumber, out var regions))
        {
            int generation = _loadGeneration, revision = _textEditRevision;
            try
            {
                await GetEditableTextRegionsAsync(pageNumber);
                if (generation == _loadGeneration && revision == _textEditRevision && ActiveTool == "EditText") RedrawAllPageAnnotations();
            }
            catch (Exception ex) { LogStatus("Không đọc được vùng chữ: " + ex.Message); }
            return;
        }
        foreach (var region in regions)
        {
            if (canvas.Width <= 0 || canvas.Height <= 0) continue;
            var rect = RegionCanvasBounds(canvas, region);
            var border = new Rectangle { Width = Math.Max(4, rect.Width), Height = Math.Max(4, rect.Height),
                Stroke = region.UnsupportedReason == null ? Brushes.DodgerBlue : Brushes.DarkOrange, StrokeThickness = 1,
                Fill = new SolidColorBrush(Color.FromArgb(12, 56, 189, 248)), Cursor = Cursors.IBeam,
                ToolTip = region.UnsupportedReason ?? $"{region.Text}\n{region.FontName} · {region.DisplayFontSize:0.###} pt" };
            Canvas.SetLeft(border, rect.Left); Canvas.SetTop(border, rect.Top); canvas.Children.Add(border);
            border.MouseLeftButtonDown += (_, e) => { e.Handled = true; _skipNextEditTextMouseUp = true; OpenNativeTextEditor(region); };
        }
    }

    private async void ShowDirectTextEditOverlay(Canvas canvas, int charIndex, int pageNumber, Point? clickPoint = null)
    {
        if (_textEditDialogOpen) return;
        int generation = _loadGeneration, revision = _textEditRevision;
        try
        {
            var regions = await GetEditableTextRegionsAsync(pageNumber);
            if (generation != _loadGeneration || revision != _textEditRevision || ActiveTool != "EditText") return;
            if (clickPoint is Point point)
            {
                var hit = regions.Where(r => RegionCanvasBounds(canvas, r).Contains(point)).OrderBy(r => r.Bounds.Width * r.Bounds.Height).FirstOrDefault();
                if (hit != null) { OpenNativeTextEditor(hit); return; }
            }
            LogStatus("Chọn một khung chữ để sửa nội dung gốc.");
        }
        catch (Exception ex) { LogStatus("Không mở được vùng chữ: " + ex.Message); }
    }

    private async void OpenNativeTextEditor(PdfNativeTextRegion region)
    {
        if (_textEditDialogOpen || _documentOpening || _savingDocument) return;
        if (region.UnsupportedReason != null)
        {
            MessageBox.Show(Window.GetWindow(this), region.UnsupportedReason, "Chưa thể sửa vùng chữ này", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _textEditDialogOpen = true;
        string path = CurrentPdfPath;
        int generation = _loadGeneration, revision = _textEditRevision;
        var pending = _nativeTextEdits.ToArray();
        var previous = pending.FirstOrDefault(e => e.PageNumber == region.PageNumber && e.ObjectIndex == region.ObjectIndex);
        PdfNativeTextEdit[] Candidate(string text, PdfNativeTextFont? font)
        {
            var others = pending.Where(e => e.PageNumber != region.PageNumber || e.ObjectIndex != region.ObjectIndex);
            return text == region.OriginalText && font == null ? others.ToArray()
                : others.Append(new PdfNativeTextEdit(region.PageNumber, region.ObjectIndex, region.OriginalText, text, font)).ToArray();
        }
        try
        {
            var dialog = new PdfTextEditDialog(region, previous?.Font,
                (text, font) => Task.Run(() => PdfNativeTextEditor.Preview(path, region, Candidate(text, font))))
                { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() != true || (dialog.ReplacementText == region.Text && Equals(dialog.ReplacementFont, previous?.Font))) return;
            if (generation != _loadGeneration || revision != _textEditRevision || path != CurrentPdfPath) return;
            SaveUndoState();
            _nativeTextEdits.RemoveAll(e => e.PageNumber == region.PageNumber && e.ObjectIndex == region.ObjectIndex);
            if (dialog.ReplacementText != region.OriginalText || dialog.ReplacementFont != null)
                _nativeTextEdits.Add(new(region.PageNumber, region.ObjectIndex, region.OriginalText, dialog.ReplacementText, dialog.ReplacementFont));
            await RefreshNativeTextRenderingAsync();
            LogStatus("Đã áp dụng sửa chữ. Nhấn Lưu hoặc Lưu dưới dạng để ghi vào PDF; Ctrl+Z để hoàn tác.");
        }
        catch (Exception ex) { MessageBox.Show("Không áp dụng được sửa chữ: " + ex.Message, "Sửa chữ PDF", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { _textEditDialogOpen = false; }
    }

    private static Rect RegionCanvasBounds(Canvas canvas, PdfNativeTextRegion region) =>
        new(region.DisplayBounds.X * canvas.Width, region.DisplayBounds.Y * canvas.Height,
            region.DisplayBounds.Width * canvas.Width, region.DisplayBounds.Height * canvas.Height);

    private async Task RefreshNativeTextRenderingAsync()
    {
        _textEditRevision++; _loadGeneration++; _renderGeneration++;
        _pageEditableRegions.Clear(); _textRegionLoads.Clear();
        ClearBitmapCache(); ClearRenderQueue(); CloseActiveDocument();
        await _documentCloseTask;
        await ResumeDocumentHandlesAsync(throwOnFailure: true);
        ForceRedrawAllPages(); RedrawAllPageAnnotations();
    }

    private async void RestoreNativeTextEdits(PdfNativeTextEdit[] edits)
    {
        if (_nativeTextEdits.SequenceEqual(edits)) return;
        _nativeTextEdits.Clear(); _nativeTextEdits.AddRange(edits);
        try { await RefreshNativeTextRenderingAsync(); }
        catch (Exception ex) { LogStatus("Không làm mới được chữ: " + ex.Message); }
    }

    private void ResetNativeTextEditing()
    {
        _textEditRevision++; _pageEditableRegions.Clear(); _textRegionLoads.Clear(); _nativeTextEdits.Clear();
        _undoStack.Clear(); _redoStack.Clear();
    }

    private async Task<string?> CreateNativeTextPrintCopyAsync()
    {
        if (_nativeTextEdits.Count == 0) return null;
        string source = CurrentPdfPath;
        var edits = _nativeTextEdits.ToArray();
        string copy = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pdfpro-text-{Guid.NewGuid():N}.pdf");
        await Task.Run(() => PdfNativeTextEditor.SaveCopy(source, copy, edits));
        return copy;
    }

    private static void CommitSavedPdf(string source, string target)
    {
        string staged = System.IO.Path.GetFullPath(target) + $".pdfpro-{Guid.NewGuid():N}.tmp";
        try
        {
            System.IO.File.Copy(source, staged, false);
            if (System.IO.File.Exists(target)) System.IO.File.Replace(staged, target, null);
            else System.IO.File.Move(staged, target);
        }
        finally { try { System.IO.File.Delete(staged); } catch { } }
    }

    public async Task<bool> SaveDocumentAsync(string? outputPath = null)
    {
        if (_savingDocument || _documentOpening) { LogStatus("Đang xử lý tài liệu. Vui lòng chờ trước khi lưu."); return false; }
        _savingDocument = true;
        try { return await SaveDocumentCoreAsync(outputPath); }
        finally { _savingDocument = false; }
    }
}
