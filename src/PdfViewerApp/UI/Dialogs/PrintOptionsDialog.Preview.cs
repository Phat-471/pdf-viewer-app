using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Printing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PdfViewerApp;

public partial class PrintOptionsDialog
{
    private sealed record PreviewGeometry(Size PaperDips, Rect SafeArea, bool Estimated, bool DriverOffsets);
    private readonly Dictionary<string, PreviewGeometry> _geometryCache = new();
    private readonly SemaphoreSlim _geometryGate = new(1, 1);
    private double _previewZoom = 1;
    private double _effectiveScale = 100;
    private bool _paperEdited;
    private bool _orientationEdited;

    private void UpdateEngineControls()
    {
        bool direct = IsDirectPdf;
        LayoutSettingsPanel.Visibility = HeavyFilePanel.Visibility = PrintOffsetPanel.Visibility =
            NativeSeparateJobsCheckBox.Visibility = TestFrameCheckBox.Visibility = direct ? Visibility.Collapsed : Visibility.Visible;
        DirectPrintNoticeText.Visibility = direct ? Visibility.Visible : Visibility.Collapsed;
        ShowMarginsCheckBox.Visibility = direct ? Visibility.Collapsed : Visibility.Visible;
        EngineAdviceText.Text = direct
            ? "Gửi PDF theo từng khối dữ liệu, không tạo ảnh. Chỉ dùng khi máy in có hỗ trợ PDF; các trang đã chọn và số bản vẫn được áp dụng."
            : SnapshotSelection != null
                ? "Snapshot chỉ in vùng đã chọn. Có thể in PDF gốc hoặc ảnh. Snapshot có một trang nên không cần đảo thứ tự trang."
                : "In PDF gốc giữ nét vector. Chọn in ảnh để kèm chú thích trong ứng dụng.";
        if (!direct && OptimizeCadDrawings)
            EngineAdviceText.Text = "Đang tối ưu CAD/Revit: vùng PDF sẽ được chuyển thành ảnh theo DPI đã chọn trước khi gửi tới driver.";
    }

    private bool TryReadScale(out double percent)
    {
        percent = 100;
        return CustomScaleRadio.IsChecked != true ||
            (double.TryParse(ScalePercentTextBox.Text.Replace(',', '.'), NumberStyles.Float,
                CultureInfo.InvariantCulture, out percent) && double.IsFinite(percent) && percent >= 10 && percent <= 400);
    }

    private bool TryGetSelectedPages(out int[] pages)
    {
        if (CurrentPageRadio.IsChecked == true || CurrentViewRadio.IsChecked == true || SnapshotSelection != null)
        {
            pages = new[] { _currentPageNumber - 1 };
            return true;
        }
        if (AllPagesRadio.IsChecked == true)
        {
            pages = Enumerable.Range(0, _pageCount).ToArray();
            return true;
        }
        var selected = new SortedSet<int>();
        pages = Array.Empty<int>();
        string text = PageRangeTextBox.Text.Replace('–', '-').Replace('—', '-');
        foreach (string part in text.Split(','))
        {
            string[] range = part.Trim().Split('-');
            if (range.Length is < 1 or > 2 || !int.TryParse(range[0].Trim(), out int first)) return false;
            int last = first;
            if (range.Length == 2 && !int.TryParse(range[1].Trim(), out last)) return false;
            if (first < 1 || last < first || last > _pageCount) return false;
            for (int page = first; page <= last; page++) selected.Add(page - 1);
        }
        pages = selected.ToArray();
        return pages.Length > 0;
    }

    private void Settings_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized || !_dialogReady || _loadingPrinterDefaults) return;
        UpdatePreview();
    }

    private void SettingsText_Changed(object sender, TextChangedEventArgs e)
    {
        if (IsInitialized && _dialogReady) UpdateSummary();
    }

    private void ScalePercent_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsInitialized || !_dialogReady || _loadingPrinterDefaults) return;
        if (ScalePercentTextBox.IsKeyboardFocusWithin) CustomScaleRadio.IsChecked = true;
        UpdatePreview();
    }

    private void ApplyColorToTicket(PrintTicket ticket)
    {
        string? color = (ColorComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (color == "Color") ticket.OutputColor = OutputColor.Color;
        else if (color == "Grayscale") ticket.OutputColor = OutputColor.Grayscale;
    }

    private bool PreviewIsGrayscale => (ColorComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "Grayscale" ||
        ((ColorComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "Default" &&
            SelectedPrintTicket?.OutputColor is OutputColor.Grayscale or OutputColor.Monochrome);

    private PreviewGeometry EstimatedGeometry()
    {
        (double w, double h) = GetPreviewPaperSize();
        bool landscape = CreatePageOrientation() == PageOrientation.Landscape;
        if (landscape && h > w || !landscape && w > h) (w, h) = (h, w);
        Size paper = new(w * 96 / 25.4, h * 96 / 25.4);
        double margin = 10 * 96 / 25.4;
        return new PreviewGeometry(paper, new Rect(margin, margin,
            Math.Max(1, paper.Width - 2 * margin), Math.Max(1, paper.Height - 2 * margin)), true, true);
    }

    private async Task<PreviewGeometry> ReadPreviewGeometryAsync(CancellationToken token)
    {
        PreviewGeometry fallback = EstimatedGeometry();
        string? printer = SelectedPrinterName;
        if (string.IsNullOrEmpty(printer) || _printerQueryInProgress) return fallback;
        PrintTicket ticket = SelectedPrintTicket?.Clone() ?? new PrintTicket();
        ticket.PageMediaSize = CreatePageMediaSize();
        ticket.PageOrientation = CreatePageOrientation();
        ticket.PageScalingFactor = 100;
        if (QualityComboBox.SelectedItem is ComboBoxItem { Tag: string dpiText } && int.TryParse(dpiText, out int dpi))
            ticket.PageResolution = new PageResolution(dpi, dpi);
        ApplyColorToTicket(ticket);
        using var xml = ticket.GetXmlStream();
        byte[] bytes = xml.ToArray();
        string offsetMode = PrintOffsetMode;
        string key = printer + "|" + offsetMode + "|" + Convert.ToBase64String(bytes);
        if (_geometryCache.TryGetValue(key, out var cached)) return cached;
        await _geometryGate.WaitAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();
            if (_geometryCache.TryGetValue(key, out cached)) return cached;
            PreviewGeometry? geometry = null;
            await PrintWorker.RunAsync(() =>
            {
                token.ThrowIfCancellationRequested();
                using var server = new LocalPrintServer();
                using var queue = new PrintQueue(server, printer, Array.Empty<string>());
                var workerTicket = new PrintTicket(new MemoryStream(bytes));
                var caps = queue.GetPrintCapabilities(workerTicket);
                var profile = PrinterPrintProfile.Resolve(queue);
                double width = caps.OrientedPageMediaWidth ?? workerTicket.PageMediaSize?.Width ?? fallback.PaperDips.Width;
                double height = caps.OrientedPageMediaHeight ?? workerTicket.PageMediaSize?.Height ?? fallback.PaperDips.Height;
                if (workerTicket.PageOrientation == PageOrientation.Landscape)
                    (width, height) = (Math.Max(width, height), Math.Min(width, height));
                else if (workerTicket.PageOrientation == PageOrientation.Portrait)
                    (width, height) = (Math.Min(width, height), Math.Max(width, height));
                var area = caps.PageImageableArea;
                bool offsets = offsetMode == "WpfOffset" || (offsetMode != "Physical" && profile.DriverAlreadyOffsetsPrintableArea);
                geometry = area == null ? fallback : new PreviewGeometry(new Size(width, height),
                    new Rect(area.OriginWidth, area.OriginHeight,
                        Math.Max(1, area.ExtentWidth - profile.RightSafetyPadding),
                        Math.Max(1, area.ExtentHeight - profile.BottomSafetyPadding)), false, offsets);
            });
            token.ThrowIfCancellationRequested();
            if (_geometryCache.Count >= 12) _geometryCache.Clear();
            _geometryCache[key] = geometry ?? fallback;
            return geometry ?? fallback;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            PdfPerfLogger.Log("Print preview margins unavailable: " + ex.Message);
            return fallback;
        }
        finally { _geometryGate.Release(); }
    }

    private BitmapSource ComposePrintPreview(BitmapSource source, Size cropPoints, PreviewGeometry geometry)
    {
        bool validScale = TryReadScale(out double percent);
        ScalePercent = validScale ? percent : 100;
        ScaleAdviceText.Text = FitToPrintableArea ? "Vừa vùng in sẽ thay đổi tỉ lệ bản vẽ."
            : "Tỉ lệ in tính theo kích thước PDF gốc. Kiểm tra đơn vị trên bản vẽ.";
        Size paper = geometry.PaperDips;
        Rect safe = geometry.SafeArea;
        Rect imageBounds;
        bool clipped;
        if (_snapshotLayout != null)
        {
            Rect? printableMm = geometry.Estimated
                ? (_snapshotLayout.PaperKey == PaperSizeKey && _snapshotLayout.Landscape == (CreatePageOrientation() == PageOrientation.Landscape)
                    ? _snapshotLayout.PrintableAreaMm : null)
                : new Rect(safe.X * 25.4 / 96, safe.Y * 25.4 / 96, safe.Width * 25.4 / 96, safe.Height * 25.4 / 96);
            _snapshotLayout = new SnapshotPaperLayout(PaperSizeKey,
                CreatePageOrientation() == PageOrientation.Landscape, FitToPrintableArea, ScalePercent,
                _snapshotLayout.MarginMm, AutoCenter, printableMm);
            paper = new Size(_snapshotLayout.PaperSizeMm.Width * 96 / 25.4, _snapshotLayout.PaperSizeMm.Height * 96 / 25.4);
            Rect content = _snapshotLayout.ContentAreaMm;
            safe = new Rect(content.X * 96 / 25.4, content.Y * 96 / 25.4, content.Width * 96 / 25.4, content.Height * 96 / 25.4);
            Rect mm = _snapshotLayout.ImageBoundsMm(cropPoints);
            imageBounds = new Rect(mm.X * 96 / 25.4, mm.Y * 96 / 25.4, mm.Width * 96 / 25.4, mm.Height * 96 / 25.4);
            if (!geometry.DriverOffsets) imageBounds.Offset(geometry.SafeArea.X, geometry.SafeArea.Y);
            clipped = safe.IsEmpty || !safe.Contains(imageBounds);
        }
        else
        {
            double sourceWidth = cropPoints.Width * 96 / 72, sourceHeight = cropPoints.Height * 96 / 72;
            double scale = FitToPrintableArea ? Math.Min(safe.Width / sourceWidth, safe.Height / sourceHeight) : ScalePercent / 100;
            double width = sourceWidth * scale, height = sourceHeight * scale;
            double x = safe.X + (AutoCenter ? (safe.Width - width) / 2 : 0);
            double y = safe.Y + (AutoCenter ? (safe.Height - height) / 2 : 0);
            if (!geometry.DriverOffsets) { x += safe.X; y += safe.Y; }
            imageBounds = new Rect(x, y, width, height);
            clipped = !safe.Contains(imageBounds);
        }
        _effectiveScale = imageBounds.Width / (cropPoints.Width * 96 / 72) * 100;
        (double requestedWidth, double requestedHeight) = GetPreviewPaperSize();
        bool paperMismatch = !geometry.Estimated && (Math.Abs(Math.Min(paper.Width, paper.Height) * 25.4 / 96 - Math.Min(requestedWidth, requestedHeight)) > 3 ||
            Math.Abs(Math.Max(paper.Width, paper.Height) * 25.4 / 96 - Math.Max(requestedWidth, requestedHeight)) > 3);
        double pixelsPerDip = 1600 / Math.Max(paper.Width, paper.Height);
        int pixelWidth = Math.Max(1, (int)Math.Round(paper.Width * pixelsPerDip));
        int pixelHeight = Math.Max(1, (int)Math.Round(paper.Height * pixelsPerDip));
        if (PreviewIsGrayscale)
        {
            var gray = new FormatConvertedBitmap(source, PixelFormats.Gray8, null, 0);
            gray.Freeze();
            source = gray;
        }
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(pixelsPerDip, pixelsPerDip));
            dc.DrawRectangle(Brushes.White, null, new Rect(new Point(), paper));
            if (!safe.IsEmpty)
            {
                dc.PushClip(new RectangleGeometry(safe));
                if (PrintTestFrame)
                {
                    dc.DrawRectangle(null, new Pen(Brushes.Black, 1), safe);
                    double centerX = _snapshotLayout != null ? safe.X + safe.Width / 2 : paper.Width / 2;
                    double centerY = _snapshotLayout != null ? safe.Y + safe.Height / 2 : paper.Height / 2;
                    Brush ink = _snapshotLayout != null ? Brushes.Black : Brushes.OrangeRed;
                    dc.DrawLine(new Pen(ink, 1), new Point(centerX, 0), new Point(centerX, paper.Height));
                    dc.DrawLine(new Pen(ink, 1), new Point(0, centerY), new Point(paper.Width, centerY));
                }
                else dc.DrawImage(source, imageBounds);
                dc.Pop();
                if (ShowMarginsCheckBox.IsChecked == true)
                    dc.DrawRectangle(null, new Pen(Brushes.Teal, 1.3 / pixelsPerDip) { DashStyle = DashStyles.Dash }, safe);
            }
            dc.Pop();
        }
        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        PreviewInfoText.Text = $"Trang {_previewPageNumber} · {paper.Width * 25.4 / 96:N1} × {paper.Height * 25.4 / 96:N1} mm · Tỉ lệ in {_effectiveScale:N1}%";
        PreviewWarningText.Text = !validScale ? "Nhập tỉ lệ từ 10% đến 400%."
            : paperMismatch ? "Driver trả về khổ giấy khác lựa chọn. Kiểm tra khổ được máy in hỗ trợ."
            : PrintTestFrame ? "Chỉ in khung kiểm tra lề; không in nội dung bản vẽ."
            : clipped ? "Nội dung vượt vùng in và sẽ bị cắt. Giảm tỉ lệ hoặc chọn Vừa vùng in."
            : geometry.Estimated && _snapshotLayout == null ? "Vùng in tạm tính với lề 10 mm; đang chờ thông tin máy in."
            : geometry.Estimated ? "Lề Snapshot 10 mm; chưa xác định được giới hạn của máy in."
            : "Đường nét đứt là giới hạn vùng in, không xuất hiện trên bản in.";
        if (_snapshotLayout == null && PrintEngineMode == "WpfBitmap" && !PrintTestFrame)
            PreviewWarningText.Text += " Bản xem trước chưa hiển thị chú thích trong ứng dụng.";
        UpdateSummary();
        return bitmap;
    }

    private void UpdateSummary()
    {
        if (!_dialogReady || PrintSummaryText == null) return;
        bool validPages = TryGetSelectedPages(out int[] pages);
        int count = PrintTestFrame ? 1 : validPages ? pages.Length : 0;
        string copies = int.TryParse(CopiesTextBox.Text, out int value) && value is >= 1 and <= 999 ? value.ToString() : "?";
        string paper = PaperSizeKey == "Default" ? "Theo máy in" : PaperSizeKey;
        string orientation = CreatePageOrientation() == PageOrientation.Landscape ? "Ngang" : "Dọc";
        PrintSummaryText.Text = IsDirectPdf ? $"In trực tiếp PDF · {count} trang · {copies} bản · Máy in tự xử lý bố cục"
            : $"{paper} · {orientation} · {count} trang · {copies} bản · {_effectiveScale:N1}%";
        PrintButton.Content = count > 0 ? $"In {count} trang" : "In";
    }

    private void ApplyPreviewZoom()
    {
        if (PreviewImage?.Source is not BitmapSource bitmap || PreviewScroll == null) return;
        double availableWidth = Math.Max(1, PreviewScroll.ViewportWidth - 16);
        double availableHeight = Math.Max(1, PreviewScroll.ViewportHeight - 16);
        double fit = Math.Min(availableWidth / bitmap.PixelWidth, availableHeight / bitmap.PixelHeight);
        if (!double.IsFinite(fit) || fit <= 0) return;
        PreviewImage.Width = bitmap.PixelWidth * fit * _previewZoom;
        PreviewImage.Height = bitmap.PixelHeight * fit * _previewZoom;
        PreviewZoomButton.Content = Math.Abs(_previewZoom - 1) < 0.001 ? "Vừa khung" : $"{_previewZoom * 100:N0}%";
    }

    private void PreviewScroll_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyPreviewZoom();
    private void PreviewScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0) ApplyPreviewZoom();
    }
    private void PreviewZoomOut_Click(object sender, RoutedEventArgs e) { _previewZoom = Math.Max(1, _previewZoom / 1.25); ApplyPreviewZoom(); }
    private void PreviewZoomIn_Click(object sender, RoutedEventArgs e) { _previewZoom = Math.Min(4, _previewZoom * 1.25); ApplyPreviewZoom(); }
    private void PreviewFit_Click(object sender, RoutedEventArgs e) { _previewZoom = 1; ApplyPreviewZoom(); }
}
