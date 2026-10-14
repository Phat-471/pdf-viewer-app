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
    private PdfInlineTextEditor? _inlineTextEditor;
    private PdfNativeTextRegion? _inlineTextRegion;
    private Task<PdfNativeTextEditor.PreviewSession>? _inlinePreviewSession;
    private Canvas? _inlineTextCanvas;
    internal bool HasInlineTextEditor => _inlineTextEditor != null;
    internal bool HasNativeFontPopupOpen => _inlineTextEditor?.IsFontPopupOpen == true;
    internal bool HandleInlineTextKey(KeyEventArgs e)
    {
        if (_inlineTextEditor == null) return false;
        if (_textEditDialogOpen) { e.Handled = true; return true; }
        if (e.Key == Key.Escape) { CancelInlineTextEditor(); LogStatus("Đã hủy nội dung đang nhập."); e.Handled = true; return true; }
        if (e.Key == Key.Enter && (_inlineTextEditor.IsInputFocused || Keyboard.Modifiers == ModifierKeys.Control))
            { _ = ApplyInlineTextAsync(); e.Handled = true; return true; }
        return false;
    }
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
                    : "Chọn khung chữ → sửa trực tiếp trên trang → áp dụng → lưu PDF. Ctrl+Enter áp dụng; Esc hủy.");
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
            border.MouseLeftButtonDown += (_, e) => { e.Handled = true; _skipNextEditTextMouseUp = true; OpenNativeTextEditor(region, canvas, e.GetPosition(canvas)); };
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
                if (hit != null) { OpenNativeTextEditor(hit, canvas, point); return; }
            }
            LogStatus("Chọn một khung chữ để sửa nội dung gốc.");
        }
        catch (Exception ex) { LogStatus("Không mở được vùng chữ: " + ex.Message); }
    }

    private void OpenNativeTextEditor(PdfNativeTextRegion region, Canvas canvas, Point? click = null)
    {
        if (_textEditDialogOpen || _documentOpening || _savingDocument) return;
        if (_inlineTextEditor != null)
        {
            LogStatus("Áp dụng hoặc hủy vùng chữ đang sửa trước khi chọn vùng khác.");
            return;
        }
        if (region.UnsupportedReason != null) { LogStatus(region.UnsupportedReason); return; }
        string path = CurrentPdfPath;
        var pending = _nativeTextEdits.ToArray();
        var previous = pending.FirstOrDefault(e => e.PageNumber == region.PageNumber && e.ObjectIndex == region.ObjectIndex);
        _inlineTextRegion = region;
        _inlinePreviewSession = Task.Run(() => new PdfNativeTextEditor.PreviewSession(path, region, pending));
        var session = _inlinePreviewSession;
        _inlineTextEditor = new PdfInlineTextEditor(region, previous?.Font, async (text, font) =>
        {
            var loaded = await session;
            return await Task.Run(() => loaded.Preview(text, font));
        });
        _inlineTextEditor.ApplyRequested += async () => await ApplyInlineTextAsync();
        _inlineTextEditor.CancelRequested += () => { CancelInlineTextEditor(); LogStatus("Đã hủy nội dung đang nhập."); };
        AttachInlineTextEditor(canvas, region.PageNumber, click);
        LogStatus("Đặt con trỏ, chọn ký tự và nhập chữ trên trang. Enter/Ctrl+Enter áp dụng; Esc hủy.");
    }

    private async Task<bool> ApplyInlineTextAsync()
    {
        if (_textEditDialogOpen) return false;
        var editor = _inlineTextEditor;
        var region = _inlineTextRegion;
        if (editor == null || region == null) return true;
        if (!editor.CanApply) { LogStatus("Chờ bản xem trước hợp lệ trước khi áp dụng hoặc lưu."); return false; }
        string text = editor.ReplacementText;
        var font = editor.ReplacementFont;
        var previous = _nativeTextEdits.FirstOrDefault(e => e.PageNumber == region.PageNumber && e.ObjectIndex == region.ObjectIndex);
        bool changed = text != region.Text || !Equals(font, previous?.Font);
        if (!changed) { CancelInlineTextEditor(); return true; }
        var originalEdits = _nativeTextEdits.ToArray();
        _textEditDialogOpen = true;
        editor.SetApplying(true);
        try
        {
            _nativeTextEdits.RemoveAll(e => e.PageNumber == region.PageNumber && e.ObjectIndex == region.ObjectIndex);
            if (text != region.OriginalText || font != null)
                _nativeTextEdits.Add(new(region.PageNumber, region.ObjectIndex, region.OriginalText, text, font));
            await RefreshNativeTextRenderingAsync(preserveInlineEditor: true);
            var appliedEdits = _nativeTextEdits.ToArray();
            _nativeTextEdits.Clear(); _nativeTextEdits.AddRange(originalEdits);
            SaveUndoState();
            _nativeTextEdits.Clear(); _nativeTextEdits.AddRange(appliedEdits);
            CancelInlineTextEditor();
            LogStatus("Đã áp dụng sửa chữ. Nhấn Lưu để ghi vào PDF; Ctrl+Z để hoàn tác.");
            return true;
        }
        catch (Exception ex)
        {
            _nativeTextEdits.Clear(); _nativeTextEdits.AddRange(originalEdits);
            try { await RefreshNativeTextRenderingAsync(preserveInlineEditor: true); }
            catch (Exception restoreError) { LogStatus("Không mở lại được trang: " + restoreError.Message); }
            editor.ReportApplyFailure("Chưa áp dụng thay đổi: " + ex.Message + " Nội dung đang nhập được giữ lại.");
            LogStatus("Chưa áp dụng thay đổi. Nội dung đang nhập được giữ lại; kiểm tra thông báo trong ô sửa.");
            return false;
        }
        finally { _textEditDialogOpen = false; editor.SetApplying(false); }
    }

    private void AttachInlineTextEditor(Canvas canvas, int pageNumber, Point? click = null)
    {
        if (_inlineTextEditor == null || _inlineTextRegion?.PageNumber != pageNumber) return;
        bool restoreFocus = _inlineTextEditor.IsInputFocused;
        if (_inlineTextCanvas != null) _inlineTextCanvas.Children.Remove(_inlineTextEditor);
        _inlineTextCanvas = canvas;
        canvas.Children.Add(_inlineTextEditor);
        _inlineTextEditor.Place(canvas, RegionCanvasBounds(canvas, _inlineTextRegion), click);
        if (restoreFocus && click == null) _inlineTextEditor.FocusInput();
    }

    private void CancelInlineTextEditor()
    {
        if (_inlineTextEditor != null) { _inlineTextCanvas?.Children.Remove(_inlineTextEditor); _inlineTextEditor.Dispose(); }
        _inlineTextEditor = null; _inlineTextRegion = null; _inlineTextCanvas = null;
        var session = _inlinePreviewSession; _inlinePreviewSession = null;
        if (session != null) _ = session.ContinueWith(t =>
        {
            if (t.Status == TaskStatus.RanToCompletion) t.Result.Dispose();
            else if (t.IsFaulted) _ = t.Exception;
        }, TaskScheduler.Default);
    }

    private static Rect RegionCanvasBounds(Canvas canvas, PdfNativeTextRegion region) =>
        new(region.DisplayBounds.X * canvas.Width, region.DisplayBounds.Y * canvas.Height,
            region.DisplayBounds.Width * canvas.Width, region.DisplayBounds.Height * canvas.Height);

    private async Task RefreshNativeTextRenderingAsync(bool preserveInlineEditor = false)
    {
        _textEditRevision++; _loadGeneration++; _renderGeneration++;
        _pageEditableRegions.Clear(); _textRegionLoads.Clear();
        ClearBitmapCache(); ClearRenderQueue(); CloseActiveDocument(preserveInlineEditor);
        await _documentCloseTask;
        await ResumeDocumentHandlesAsync(throwOnFailure: true);
        ForceRedrawAllPages(); RedrawAllPageAnnotations();
    }

    private async void RestoreNativeTextEdits(PdfNativeTextEdit[] edits)
    {
        CancelInlineTextEditor();
        if (_nativeTextEdits.SequenceEqual(edits)) return;
        _nativeTextEdits.Clear(); _nativeTextEdits.AddRange(edits);
        try { await RefreshNativeTextRenderingAsync(); }
        catch (Exception ex) { LogStatus("Không làm mới được chữ: " + ex.Message); }
    }

    private void ResetNativeTextEditing()
    {
        CancelInlineTextEditor();
        _textEditRevision++; _pageEditableRegions.Clear(); _textRegionLoads.Clear(); _nativeTextEdits.Clear();
        _undoStack.Clear(); _redoStack.Clear();
    }

    private async Task<string?> CreateNativeTextPrintCopyAsync()
    {
        if (!await ApplyInlineTextAsync()) throw new InvalidOperationException("Cần áp dụng hoặc hủy vùng chữ đang sửa trước khi in.");
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
        if (_savingDocument || _documentOpening || _textEditDialogOpen) { LogStatus("Đang xử lý tài liệu. Vui lòng chờ trước khi lưu."); return false; }
        if (!await ApplyInlineTextAsync()) return false;
        _savingDocument = true;
        try { return await SaveDocumentCoreAsync(outputPath); }
        finally { _savingDocument = false; }
    }
}
