using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PdfViewerApp.Services;

namespace PdfViewerApp;

public partial class PdfDocumentTab
{
    private readonly Dictionary<int, List<PdfNativeTextRegion>> _pageEditableRegions = new();
    private readonly Dictionary<int, Task<List<PdfNativeTextRegion>>> _textRegionLoads = new();
    private readonly List<PdfNativeTextEdit> _nativeTextEdits = new();
    private ulong _textEditSessionId;
    private string? _workingPdfPath;
    private bool _isHighlightEditableRegionsEnabled;
    private bool _textEditDialogOpen;
    private PdfInlineTextEditor? _inlineTextEditor;
    private PdfNativeTextRegion? _inlineTextRegion;
    private Canvas? _inlineTextCanvas;
    private int _textEditRevision;
    private bool _savingDocument;
    private sealed record DocumentEditSnapshot(List<PdfAnnotation> Annotations, PdfNativeTextEdit[] TextEdits);

    internal bool HasInlineTextEditor => _inlineTextEditor != null;
    internal bool HasNativeFontPopupOpen => false;

    internal bool HandleInlineTextKey(KeyEventArgs e)
    {
        if (_inlineTextEditor == null) return false;
        if (_textEditDialogOpen) { e.Handled = true; return true; }
        if (e.Key == Key.Escape) { CancelInlineTextEditor(); LogStatus("Đã hủy nội dung đang nhập."); e.Handled = true; return true; }
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            _ = ApplyInlineTextAsync();
            e.Handled = true;
            return true;
        }
        return false;
    }

    private void EnsureTextEditSession()
    {
        if (_textEditSessionId != 0 && !string.IsNullOrEmpty(_workingPdfPath) && File.Exists(_workingPdfPath))
            return;

        string source = CurrentPdfPath;
        if (string.IsNullOrEmpty(source) || !File.Exists(source)) return;

        if (string.IsNullOrEmpty(_workingPdfPath))
        {
            _workingPdfPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pdfpro-work-{Guid.NewGuid():N}.pdf");
            File.Copy(source, _workingPdfPath, true);
        }

        if (_textEditSessionId == 0)
        {
            _textEditSessionId = PdfCoreInterop.pdf_session_open(_workingPdfPath);
        }
    }

    public async void SetHighlightEditableRegions(bool enable)
    {
        _isHighlightEditableRegionsEnabled = enable;
        if (enable && PageCount > 0)
        {
            try
            {
                var regions = await GetEditableTextRegionsAsync(Math.Clamp(SelectedPageNumber, 1, PageCount));
                LogStatus(regions.Count == 0 ? "Trang không có chữ PDF để sửa (dạng ảnh scan). Hãy dùng OCR trước."
                    : "Sửa văn bản (PdfCraft): Bấm vào chữ để sửa trực tiếp trên trang · Enter hoặc bấm ra ngoài để lưu · Esc để hủy");
            }
            catch (Exception ex) { LogStatus("Không đọc được vùng chữ: " + ex.Message); }
        }
        RedrawAllPageAnnotations();
    }

    private Task<List<PdfNativeTextRegion>> GetEditableTextRegionsAsync(int pageNumber)
    {
        if (_pageEditableRegions.TryGetValue(pageNumber, out var saved)) return Task.FromResult(saved);
        if (_textRegionLoads.TryGetValue(pageNumber, out var loading)) return loading;

        EnsureTextEditSession();
        ulong session = _textEditSessionId;
        string path = _workingPdfPath ?? CurrentPdfPath;
        int generation = _loadGeneration, revision = _textEditRevision;

        async Task<List<PdfNativeTextRegion>> LoadAsync()
        {
            try
            {
                double pw = 595.0, ph = 842.0;
                lock (PdfiumEngine.SyncRoot)
                {
                    if (_documentHandle != IntPtr.Zero)
                    {
                        PdfiumEngine.TryGetPageSizeByIndex(_documentHandle, pageNumber - 1, out pw, out ph);
                    }
                }

                var regions = await Task.Run(() => PdfNativeTextEditor.ReadRegions(session, path, pageNumber, pw, ph));
                if (generation == _loadGeneration && revision == _textEditRevision)
                    _pageEditableRegions[pageNumber] = regions;
                return regions;
            }
            finally
            {
                if (generation == _loadGeneration && revision == _textEditRevision)
                    _textRegionLoads.Remove(pageNumber);
            }
        }
        var task = LoadAsync();
        _textRegionLoads[pageNumber] = task;
        return task;
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
        var transparentBrush = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
        var hoverStroke = new SolidColorBrush(Color.FromArgb(210, 56, 189, 248)); // #38BDF8
        var hoverFill = new SolidColorBrush(Color.FromArgb(28, 56, 189, 248));
        var warningStroke = new SolidColorBrush(Color.FromArgb(210, 245, 158, 11)); // Amber

        foreach (var region in regions)
        {
            if (canvas.Width <= 0 || canvas.Height <= 0) continue;
            var rect = RegionCanvasBounds(canvas, region);
            var border = new Rectangle
            {
                Width = Math.Max(4, rect.Width),
                Height = Math.Max(4, rect.Height),
                RadiusX = 2,
                RadiusY = 2,
                Stroke = Brushes.Transparent,
                StrokeThickness = 1,
                Fill = transparentBrush,
                Cursor = Cursors.IBeam,
                ToolTip = region.UnsupportedReason ?? (region.Text.Length > 80 ? region.Text.Substring(0, 77) + "..." : region.Text)
            };

            border.MouseEnter += (_, _) =>
            {
                border.Stroke = region.UnsupportedReason == null ? hoverStroke : warningStroke;
                border.Fill = hoverFill;
            };

            border.MouseLeave += (_, _) =>
            {
                border.Stroke = Brushes.Transparent;
                border.Fill = transparentBrush;
            };

            Canvas.SetLeft(border, rect.Left);
            Canvas.SetTop(border, rect.Top);
            canvas.Children.Add(border);

            border.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                _skipNextEditTextMouseUp = true;
                OpenNativeTextEditor(region, canvas, e.GetPosition(canvas));
            };
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
            // Clicked outside on blank space: auto-commit current edit if open (PdfCraft click-away style)
            if (_inlineTextEditor != null)
            {
                await ApplyInlineTextAsync();
            }
        }
        catch (Exception ex) { LogStatus("Không mở được vùng chữ: " + ex.Message); }
    }

    private async void OpenNativeTextEditor(PdfNativeTextRegion region, Canvas canvas, Point? click = null)
    {
        if (_textEditDialogOpen || _documentOpening || _savingDocument) return;
        if (_inlineTextEditor != null)
        {
            if (_inlineTextRegion == region) return;
            // Auto-commit existing edit before opening the new one (PdfCraft seamless workflow)
            bool ok = await ApplyInlineTextAsync();
            if (!ok || ActiveTool != "EditText") return;
        }
        if (region.UnsupportedReason != null) { LogStatus(region.UnsupportedReason); return; }

        _inlineTextRegion = region;
        _inlineTextEditor = new PdfInlineTextEditor(region);
        _inlineTextEditor.ApplyRequested += async () => await ApplyInlineTextAsync();
        _inlineTextEditor.CancelRequested += () => { CancelInlineTextEditor(); LogStatus("Đã hủy sửa văn bản."); };
        AttachInlineTextEditor(canvas, region.PageNumber, click);
        LogStatus("Sửa trực tiếp (PdfCraft): Nhập chữ · Enter hoặc bấm ra ngoài để lưu · Esc để hủy");
    }

    private async Task<bool> ApplyInlineTextAsync()
    {
        if (_textEditDialogOpen) return false;
        var editor = _inlineTextEditor;
        var region = _inlineTextRegion;
        if (editor == null || region == null) return true;
        string text = editor.ReplacementText;
        bool changed = text != region.Text;
        if (!changed) { CancelInlineTextEditor(); return true; }

        _textEditDialogOpen = true;
        editor.SetApplying(true);
        try
        {
            EnsureTextEditSession();
            if (_textEditSessionId == 0) throw new InvalidOperationException("Chưa khởi tạo phiên làm việc PdfCraft.");

            // 1. Commit trực tiếp vào Document Session của PdfCraft trong RAM
            bool committed = await Task.Run(() =>
                PdfNativeTextEditor.CommitBlock(_textEditSessionId, region.PageNumber, region.ObjectIndex, text, 0));

            if (!committed)
                throw new InvalidOperationException("PdfCraft không áp dụng được nội dung chữ.");

            // 2. Xuất ra tệp làm việc PDF
            bool exported = await Task.Run(() =>
                PdfNativeTextEditor.Export(_textEditSessionId, _workingPdfPath!, false));

            if (!exported)
                throw new InvalidOperationException("Không lưu được tệp PDF xem trước.");

            // 3. Cập nhật danh sách edits để theo dõi trạng thái chưa lưu
            _nativeTextEdits.RemoveAll(e => e.PageNumber == region.PageNumber && e.ObjectIndex == region.ObjectIndex);
            _nativeTextEdits.Add(new(region.PageNumber, region.ObjectIndex, region.OriginalText, text));

            // 4. Xóa cache khối chữ để lần bấm sửa tiếp theo sẽ đọc lại dữ liệu mới nhất từ PdfCraft
            _pageEditableRegions.Clear();
            _textRegionLoads.Clear();

            // 5. Làm mới hiển thị PDFium
            await RefreshNativeTextRenderingAsync(preserveInlineEditor: true);

            CancelInlineTextEditor();
            LogStatus("Đã áp dụng sửa chữ (PdfCraft). Nhấn Lưu để ghi vào PDF; Ctrl+Z để hoàn tác.");
            return true;
        }
        catch (Exception ex)
        {
            editor.ReportApplyFailure("Chưa áp dụng thay đổi: " + ex.Message);
            LogStatus("Chưa áp dụng thay đổi: " + ex.Message);
            return false;
        }
        finally
        {
            _textEditDialogOpen = false;
            editor.SetApplying(false);
        }
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
        if (_textEditSessionId != 0 && PdfNativeTextEditor.Undo(_textEditSessionId))
        {
            PdfNativeTextEditor.Export(_textEditSessionId, _workingPdfPath!, false);
            try { await RefreshNativeTextRenderingAsync(); }
            catch (Exception ex) { LogStatus("Không làm mới được chữ: " + ex.Message); }
        }
    }

    private void ResetNativeTextEditing()
    {
        CancelInlineTextEditor();
        _textEditRevision++; _pageEditableRegions.Clear(); _textRegionLoads.Clear(); _nativeTextEdits.Clear();
        if (_textEditSessionId != 0)
        {
            PdfNativeTextEditor.CloseSession(_textEditSessionId);
            _textEditSessionId = 0;
        }
        if (!string.IsNullOrEmpty(_workingPdfPath))
        {
            try { File.Delete(_workingPdfPath); } catch { }
            _workingPdfPath = null;
        }
    }

    private async Task<string?> CreateNativeTextPrintCopyAsync()
    {
        if (!await ApplyInlineTextAsync()) throw new InvalidOperationException("Cần áp dụng hoặc hủy vùng chữ đang sửa trước khi in.");
        if (_textEditSessionId == 0 && _nativeTextEdits.Count == 0) return null;
        return _workingPdfPath ?? CurrentPdfPath;
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
