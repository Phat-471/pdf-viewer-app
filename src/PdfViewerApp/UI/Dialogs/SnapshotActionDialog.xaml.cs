using System;
using System.Globalization;
using System.IO;
using System.Printing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace PdfViewerApp;

public partial class SnapshotActionDialog : Window
{
    private PdfSnapshotSelection _snapshot;
    private readonly PdfRenderSession _session;
    private readonly bool _ownsSession;
    private Size _cropPoints;
    private readonly Size _pageSizePoints;
    internal event Action<PdfSnapshotSelection>? SelectionChanged;
    internal bool EditRegionRequested { get; private set; }
    internal string? SourceDocumentPath { get; set; }
    private readonly CancellationTokenSource _lifetime = new();
    private BitmapSource? _previewCrop;
    private BitmapSource? _exportCrop;
    private int _exportLongEdge;
    private bool _ready;
    private bool _busy;
    private bool _closed;
    private bool _validLayout = true;

    internal bool ReselectRequested { get; private set; }
    internal BitmapSource? PreviewCrop => _previewCrop;
    internal Size CropSizePoints => _cropPoints;
    internal SnapshotPaperLayout Layout { get; private set; } = new("A3", true, true);

    internal SnapshotActionDialog(int pageNumber, PdfSnapshotSelection snapshot, Size pageSizePoints, PdfRenderSession? session)
    {
        InitializeComponent();
        _snapshot = snapshot;
        _pageSizePoints = pageSizePoints;
        _cropPoints = new Size(pageSizePoints.Width * snapshot.Width, pageSizePoints.Height * snapshot.Height);
        _session = session ?? new PdfRenderSession(snapshot.PdfPath);
        _ownsSession = session == null;
        SnapshotInfoText.Text = $"Trang {pageNumber} · Vùng chọn {_cropPoints.Width * 25.4 / 72:N1} × {_cropPoints.Height * 25.4 / 72:N1} mm";
        var preferences = AppPreferences.Load();
        foreach (ComboBoxItem item in PaperCombo.Items) if (Equals(item.Tag, preferences.SnapshotPaperKey)) PaperCombo.SelectedItem = item;
        OrientationCombo.SelectedIndex = preferences.SnapshotLandscape ? 0 : 1;
        ScaleModeCombo.SelectedIndex = preferences.SnapshotFit ? 0 : 1;
        ScaleTextBox.Text = Math.Clamp(preferences.SnapshotScale, 10, 400).ToString("0.##", CultureInfo.InvariantCulture);
        foreach (ComboBoxItem item in QualityCombo.Items) if (item.Tag?.ToString() == preferences.SnapshotQuality.ToString()) QualityCombo.SelectedItem = item;
        ExportModeCombo.SelectedIndex = preferences.SnapshotExportPaper ? 0 : 1;
        FillRegionFields();
        _ready = true;
        ContentRendered += LoadPreview;
        Closed += (_, _) =>
        {
            _closed = true;
            _lifetime.Cancel();
            if (_validLayout)
            {
                try
                {
                    var saved = AppPreferences.Load();
                    saved.SnapshotPaperKey = Layout.PaperKey; saved.SnapshotLandscape = Layout.Landscape;
                    saved.SnapshotFit = Layout.FitToPage; saved.SnapshotScale = Layout.ScalePercent;
                    saved.SnapshotQuality = int.Parse((QualityCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "2400");
                    saved.SnapshotExportPaper = ExportModeCombo.SelectedIndex == 0; saved.Save();
                }
                catch (Exception ex) { PdfPerfLogger.Log("Snapshot preferences: " + ex.Message); }
            }
            if (_ownsSession) _session.Dispose();
        };
    }

    private async void LoadPreview(object? sender, EventArgs e)
    {
        ContentRendered -= LoadPreview;
        SetBusy(true, "Đang tải vùng chọn… Có thể đóng hộp thoại trong lúc chờ.");
        try
        {
            var token = _lifetime.Token;
            _previewCrop = await Task.Run(() => _session.RenderSnapshot(_snapshot, 1200), token)
                ?? throw new InvalidOperationException("Không đọc được vùng chọn trong PDF.");
            if (_closed) return;
            PreviewPlaceholder.Visibility = Visibility.Collapsed;
            UpdateLayoutPreview();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!_closed) PreviewPlaceholder.Text = "Không tải được xem trước. Có thể thử copy/lưu hoặc chọn lại vùng.";
            if (!_closed) StatusText.Text = "Không tải được xem trước: " + ex.Message;
        }
        finally { if (!_closed) SetBusy(false); }
    }

    private void Layout_Changed(object sender, SelectionChangedEventArgs e) => UpdateLayoutPreview();
    private void Scale_TextChanged(object sender, TextChangedEventArgs e) => UpdateLayoutPreview();

    private void UpdateLayoutPreview()
    {
        if (!_ready || _closed) return;
        bool fit = ScaleModeCombo.SelectedIndex == 0;
        ScaleTextBox.IsEnabled = !fit;
        double percent = 100;
        _validLayout = fit || (double.TryParse(ScaleTextBox.Text.Replace(',', '.'), NumberStyles.Float,
            CultureInfo.InvariantCulture, out percent) && double.IsFinite(percent) && percent >= 10 && percent <= 400);
        UpdateButtons();
        if (!_validLayout)
        {
            StatusText.Text = "Nhập tỷ lệ từ 10% đến 400%.";
            return;
        }
        string paperKey = (PaperCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "A3";
        bool landscape = OrientationCombo.SelectedIndex == 0;
        Rect? printable = Layout.PaperKey == paperKey && Layout.Landscape == landscape ? Layout.PrintableAreaMm : null;
        Layout = new SnapshotPaperLayout(paperKey, landscape, fit, percent, Center: Layout.Center, PrintableAreaMm: printable);
        if (_previewCrop != null)
        {
            PreviewImage.Source = Layout.Compose(_previewCrop, _cropPoints, 1200, showGuides: true);
            var bounds = Layout.ImageBoundsMm(_cropPoints);
            double actualPercent = bounds.Width / (_cropPoints.Width * 25.4 / 72) * 100;
            StatusText.Text = Layout.IsClipped(_cropPoints)
                ? "Vùng chọn vượt khổ giấy: phần ngoài lề sẽ bị cắt. Giảm tỷ lệ hoặc chọn Vừa trang."
                : $"{Layout.PaperKey} · {(Layout.Landscape ? "Ngang" : "Dọc")} · Tỉ lệ {actualPercent:N1}% · Đường nét đứt chỉ giới hạn vùng xuất, không có trong ảnh lưu.";
        }
    }

    private void SetBusy(bool busy, string? status = null)
    {
        _busy = busy;
        BusyProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.IsEnabled = !busy;
        ReselectButton.IsEnabled = !busy;
        if (status != null) StatusText.Text = status;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        PrintButton.IsEnabled = CopyButton.IsEnabled = SaveButton.IsEnabled = OcrButton.IsEnabled = !_busy && _validLayout;
    }

    internal void RestoreLayout(SnapshotPaperLayout layout)
    {
        _ready = false;
        foreach (ComboBoxItem item in PaperCombo.Items) if (Equals(item.Tag, layout.PaperKey)) PaperCombo.SelectedItem = item;
        OrientationCombo.SelectedIndex = layout.Landscape ? 0 : 1; ScaleModeCombo.SelectedIndex = layout.FitToPage ? 0 : 1;
        ScaleTextBox.Text = layout.ScalePercent.ToString("0.##", CultureInfo.InvariantCulture);
        Layout = layout; _ready = true; UpdateLayoutPreview();
    }

    private void FillRegionFields()
    {
        RegionXBox.Text = (_snapshot.X * _pageSizePoints.Width * 25.4 / 72).ToString("0.###", CultureInfo.InvariantCulture);
        RegionYBox.Text = (_snapshot.Y * _pageSizePoints.Height * 25.4 / 72).ToString("0.###", CultureInfo.InvariantCulture);
        RegionWidthBox.Text = (_cropPoints.Width * 25.4 / 72).ToString("0.###", CultureInfo.InvariantCulture);
        RegionHeightBox.Text = (_cropPoints.Height * 25.4 / 72).ToString("0.###", CultureInfo.InvariantCulture);
    }

    private void ApplyRegion_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        double[] values = new double[4]; var boxes = new[] { RegionXBox, RegionYBox, RegionWidthBox, RegionHeightBox };
        for (int i = 0; i < 4; i++)
            if (!double.TryParse(boxes[i].Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) || !double.IsFinite(values[i]))
            { StatusText.Text = "Nhập vị trí và kích thước hợp lệ bằng mm."; return; }
        double pw = _pageSizePoints.Width * 25.4 / 72, ph = _pageSizePoints.Height * 25.4 / 72;
        if (values[0] < 0 || values[1] < 0 || values[2] < 0.5 || values[3] < 0.5 || values[0] + values[2] > pw + 0.001 || values[1] + values[3] > ph + 0.001)
        { StatusText.Text = $"Vùng phải nằm trong trang {pw:0.###} × {ph:0.###} mm và rộng/cao ít nhất 0,5 mm."; return; }
        _snapshot = _snapshot with { X = values[0] / pw, Y = values[1] / ph, Width = Math.Min(values[2] / pw, 1 - values[0] / pw), Height = Math.Min(values[3] / ph, 1 - values[1] / ph) };
        _cropPoints = new Size(_snapshot.Width * _pageSizePoints.Width, _snapshot.Height * _pageSizePoints.Height);
        _previewCrop = _exportCrop = null; _exportLongEdge = 0;
        SnapshotInfoText.Text = $"Trang {_snapshot.PageIndex + 1} · Vùng chọn {_cropPoints.Width * 25.4 / 72:N1} × {_cropPoints.Height * 25.4 / 72:N1} mm";
        SelectionChanged?.Invoke(_snapshot); FillRegionFields(); LoadPreview(this, EventArgs.Empty);
    }

    private void EditRegion_Click(object sender, RoutedEventArgs e) { EditRegionRequested = true; DialogResult = false; }

    private async void Ocr_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var options = new OcrOptionsDialog(hasRegion: true, regionOnly: true) { Owner = this };
        if (options.ShowDialog() != true) return;
        SetBusy(true, "Đang đọc chữ trong vùng chọn…");
        var progress = new PrintProgressDialog("OCR") { Owner = this }; progress.Show();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, progress.CancellationToken);
        try
        {
            var selection = _snapshot;
            var output = await Task.Run(() => PdfOcrService.RecognizeAsync(_session, selection.PageIndex, options.Options,
                new Rect(selection.X, selection.Y, selection.Width, selection.Height), cancel.Token, new Progress<PrintProgressInfo>(info => progress.Dispatcher.BeginInvoke(new Action(() => progress.UpdateProgress(info))))));
            cancel.Token.ThrowIfCancellationRequested(); progress.CloseAfterSuccess();
            if (!_closed) new OcrResultDialog(PdfOcrService.ToText(output)) { Owner = this }.ShowDialog();
        }
        catch (OperationCanceledException) { progress.CloseAfterSuccess(); }
        catch (Exception ex) { progress.MarkFailed("Không đọc được chữ: " + ex.Message); }
        finally { if (!_closed) SetBusy(false); }
    }

    private async Task<BitmapSource> RenderExportAsync()
    {
        int edge = int.Parse((QualityCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "2400", CultureInfo.InvariantCulture);
        bool paper = ExportModeCombo.SelectedIndex == 0;
        SnapshotPaperLayout layout = Layout;
        BitmapSource? crop = _exportLongEdge == edge ? _exportCrop : null;
        BitmapSource? result = null;
        var token = _lifetime.Token;
        await PrintWorker.RunAsync(() =>
        {
            token.ThrowIfCancellationRequested();
            crop ??= _session.RenderSnapshot(_snapshot, edge, 20000000)
                ?? throw new InvalidOperationException("Không đọc được vùng chọn trong PDF.");
            token.ThrowIfCancellationRequested();
            result = paper ? layout.Compose(crop, _cropPoints, edge) : crop;
        });
        token.ThrowIfCancellationRequested();
        _exportCrop = crop;
        _exportLongEdge = edge;
        return result!;
    }

    private async void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || !_validLayout) return;
        SetBusy(true, "Đang tạo ảnh để copy…");
        try
        {
            BitmapSource bitmap = await RenderExportAsync();
            for (int attempt = 0; ; attempt++)
            {
                try { Clipboard.SetImage(bitmap); break; }
                catch (ExternalException) when (attempt < 2) { await Task.Delay(100, _lifetime.Token); }
            }
            StatusText.Text = $"Đã copy ảnh {bitmap.PixelWidth:N0} × {bitmap.PixelHeight:N0} px. Có thể dán bằng Ctrl+V.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!_closed) StatusText.Text = "Không copy được ảnh. Hãy thử lại. " + ex.Message; }
        finally { if (!_closed) SetBusy(false); }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || !_validLayout) return;
        var dialog = new SaveFileDialog
        {
            Filter = "Ảnh PNG (*.png)|*.png", DefaultExt = ".png", AddExtension = true,
            Title = "Lưu ảnh Snapshot", FileName = $"{Path.GetFileNameWithoutExtension(SourceDocumentPath ?? _snapshot.PdfPath)}_p{_snapshot.PageIndex + 1}_snapshot.png",
            InitialDirectory = Path.GetDirectoryName(SourceDocumentPath ?? _snapshot.PdfPath)
        };
        if (dialog.ShowDialog(this) != true) return;
        SetBusy(true, "Đang tạo và lưu ảnh PNG…");
        try
        {
            BitmapSource bitmap = await RenderExportAsync();
            await Task.Run(() =>
            {
                _lifetime.Token.ThrowIfCancellationRequested();
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var output = File.Create(dialog.FileName);
                encoder.Save(output);
            });
            if (!_closed) StatusText.Text = $"Đã lưu PNG {bitmap.PixelWidth:N0} × {bitmap.PixelHeight:N0} px: {Path.GetFileName(dialog.FileName)}";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!_closed) StatusText.Text = "Không lưu được PNG. Hãy chọn lại thư mục và thử lại. " + ex.Message; }
        finally { if (!_closed) SetBusy(false); }
    }

    private async void Print_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || !_validLayout) return;
        var layout = Layout;
        var options = new PrintOptionsDialog(_snapshot.PageIndex + 1, _snapshot.PageIndex + 1, _snapshot.PdfPath) { Owner = this };
        options.ConfigureSnapshot(_snapshot, _previewCrop, layout, _cropPoints);
        if (options.ShowDialog() != true || string.IsNullOrEmpty(options.SelectedPrinterName)) return;
        layout = options.SnapshotLayout ?? layout;
        Layout = layout;
        _ready = false;
        foreach (ComboBoxItem item in PaperCombo.Items)
            if (Equals(item.Tag, layout.PaperKey)) PaperCombo.SelectedItem = item;
        OrientationCombo.SelectedIndex = layout.Landscape ? 0 : 1;
        ScaleModeCombo.SelectedIndex = layout.FitToPage ? 0 : 1;
        ScaleTextBox.Text = layout.ScalePercent.ToString("0.##", CultureInfo.InvariantCulture);
        _ready = true;
        UpdateLayoutPreview();
        SetBusy(true, "Đang chuẩn bị in Snapshot…");
        var progressDialog = new PrintProgressDialog { Owner = this };
        IProgress<PrintProgressInfo> progress = new Progress<PrintProgressInfo>(progressDialog.UpdateProgress);
        using var printCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, progressDialog.CancellationToken);
        progressDialog.Show();
        try
        {
            var ticket = options.SelectedPrintTicket?.Clone() ?? new PrintTicket();
            ticket.PageMediaSize = options.CreatePageMediaSize();
            ticket.PageOrientation = options.CreatePageOrientation();
            int dpi = Math.Clamp((int)Math.Round(options.PrintDpi), 72, 1200);
            ticket.PageResolution = new PageResolution(dpi, dpi);
            ticket.CopyCount = options.Copies;
            string printerName = options.SelectedPrinterName!;
            using var xml = ticket.GetXmlStream();
            byte[] ticketBytes = xml.ToArray();
            bool rasterize = options.PrintEngineMode == "WpfBitmap" || options.OptimizeCadDrawings;
            bool separateCopies = options.NativeSeparatePageJobs;
            bool testFrame = options.PrintTestFrame;
            string offsetMode = options.PrintOffsetMode;
            var token = printCancellation.Token;
            await PrintWorker.RunAsync(() =>
            {
                token.ThrowIfCancellationRequested();
                using var server = new LocalPrintServer();
                using var queue = new PrintQueue(server, printerName, Array.Empty<string>());
                var workerTicket = new PrintTicket(new MemoryStream(ticketBytes));
                var profile = PrinterPrintProfile.Resolve(queue);
                bool driverOffsets = offsetMode == "WpfOffset" ||
                    (offsetMode != "Physical" && profile.DriverAlreadyOffsetsPrintableArea);
                var settings = new SnapshotPrintSettings(rasterize, separateCopies, testFrame, driverOffsets);
                PdfSnapshotPrinter.PrintSnapshot(_snapshot, queue, workerTicket,
                    profile.RightSafetyPadding, profile.BottomSafetyPadding, layout, token, progress, settings);
            });
            progressDialog.MarkCompleted("Đã gửi lệnh in Snapshot tới máy in.");
            if (!_closed) StatusText.Text = "Đã gửi lệnh in Snapshot tới máy in. Có thể tiếp tục copy hoặc lưu ảnh.";
        }
        catch (OperationCanceledException)
        {
            progressDialog.MarkFailed("Đã huỷ lệnh in Snapshot.");
            if (!_closed) StatusText.Text = "Đã huỷ lệnh in Snapshot.";
        }
        catch (Exception ex)
        {
            progressDialog.MarkFailed("Không in được Snapshot: " + ex.Message);
            if (!_closed) StatusText.Text = "Không in được Snapshot: " + ex.Message;
        }
        finally { if (!_closed) SetBusy(false); }
    }

    private void Reselect_Click(object sender, RoutedEventArgs e)
    {
        ReselectRequested = true;
        DialogResult = false;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && Keyboard.FocusedElement is not TextBox)
        {
            Copy_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }
}
