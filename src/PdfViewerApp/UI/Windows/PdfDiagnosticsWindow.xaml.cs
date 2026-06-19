using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace PdfViewerApp;

public partial class PdfDiagnosticsWindow : Window
{
    private readonly string _pdfPath;
    private readonly PdfDocumentTab _activeTab;

    public PdfDiagnosticsWindow(string pdfPath, PdfDocumentTab activeTab)
    {
        InitializeComponent();
        _pdfPath = pdfPath;
        _activeTab = activeTab;

        RefreshStats();
    }

    private void RefreshStats()
    {
        try
        {
            // 1. Set File Info
            FileNameText.Text = $"Tên file: {Path.GetFileName(_pdfPath)}";
            if (File.Exists(_pdfPath))
            {
                long bytes = new FileInfo(_pdfPath).Length;
                FileSizeText.Text = FormatBytes(bytes);

                if (bytes > 50 * 1024 * 1024)
                {
                    FileSizeText.Text += " (Nặng ⚠️)";
                }
            }

            PageCountText.Text = $"{_activeTab.PageCount} trang";

            // Cache Stats
            double mbUsed = _activeTab.BitmapCacheBytes / (1024.0 * 1024.0);
            CacheStateText.Text = $"{_activeTab.BitmapCacheCount} trang ({mbUsed:0.##} MB)";

            // Cache Hit/Miss
            if (_activeTab.CacheManager != null)
            {
                long hits = _activeTab.CacheManager.CacheHits;
                long misses = _activeTab.CacheManager.CacheMisses;
                double ratio = _activeTab.CacheManager.HitRatio;
                CacheHitMissText.Text = $"{hits} / {misses} ({ratio:0.#}%)";
            }
            else
            {
                CacheHitMissText.Text = "N/A";
            }

            // 2. Parse logs for performance numbers
            string logs = PdfPerfLogger.ReadCurrentLog();

            long loadDocMs = ParseLogValue(logs, @"FPDF_LoadDocument:\s*(\d+)\s*ms");
            long getPageCountMs = ParseLogValue(logs, @"FPDF_GetPageCount:\s*(\d+)\s*ms");
            long collectDimensionsMs = ParseLogValue(logs, @"CollectPageDimensions\(\d+\):\s*(\d+)\s*ms");
            long renderMs = ParseLogValue(logs, @"RenderPdfPagesFromCacheAsync:\s*(\d+)\s*ms");

            if (renderMs == 0)
            {
                renderMs = ParseLogValue(logs, @"First page size probe:\s*(\d+)\s*ms");
            }

            long totalTimeMs = loadDocMs + getPageCountMs + collectDimensionsMs + renderMs;
            TotalLoadTimeText.Text = $"{totalTimeMs} ms";

            // Update progress bars & labels
            ValLoadDocText.Text = $"{loadDocMs} ms";
            BarLoadDoc.Value = Math.Clamp(loadDocMs, 0, BarLoadDoc.Maximum);

            ValPageCountText.Text = $"{getPageCountMs} ms";
            BarPageCount.Value = Math.Clamp(getPageCountMs, 0, BarPageCount.Maximum);

            ValDimensionsText.Text = $"{collectDimensionsMs} ms";
            BarDimensions.Value = Math.Clamp(collectDimensionsMs, 0, BarDimensions.Maximum);

            ValRenderText.Text = $"{renderMs} ms";
            BarRender.Value = Math.Clamp(renderMs, 0, BarRender.Maximum);

            // 3. Parse and Populate Render Log History
            var logList = new List<RenderLogItem>();
            string[] lines = logs.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var logRegex = new Regex(@"^\[(?<time>\d{2}:\d{2}:\d{2}\.\d{3})\]\s+(?<type>Page|Thumb)\s+(?<page>\d+)\s+(?<status>cache hit|render miss)(?<detail>.*)$", RegexOptions.IgnoreCase);

            foreach (string line in lines)
            {
                var match = logRegex.Match(line);
                if (match.Success)
                {
                    string statusStr = match.Groups["status"].Value.Equals("cache hit", StringComparison.OrdinalIgnoreCase) ? "Trúng Cache" : "Trượt Cache";
                    logList.Add(new RenderLogItem
                    {
                        Time = match.Groups["time"].Value,
                        Page = $"{match.Groups["type"].Value} {match.Groups["page"].Value}",
                        Status = statusStr,
                        Detail = match.Groups["detail"].Value.Trim()
                    });
                }
            }
            logList.Reverse();
            RenderLogList.ItemsSource = logList;

            // 4. Build recommendation advice
            System.Text.StringBuilder advice = new();
            advice.AppendLine("💡 Đánh giá hiệu năng tải tài liệu này:");

            if (totalTimeMs < 500)
            {
                advice.AppendLine("- Tốc độ tải xuất sắc! PDFium Engine và Rust Core đang hoạt động tối ưu.");
            }
            else if (totalTimeMs < 1500)
            {
                advice.AppendLine("- Tốc độ tải ở mức chấp nhận được. Có thể tối ưu thêm.");
            }
            else
            {
                advice.AppendLine("- Cảnh báo: Thời gian tải chậm! (lớn hơn 1.5 giây).");
            }

            if (loadDocMs > 800)
            {
                advice.AppendLine("- Tệp PDF có cấu trúc phức tạp hoặc được lưu trữ trên ổ đĩa chậm. Hãy cân nhắc sao chép tệp sang ổ cứng SSD cục bộ.");
            }

            if (collectDimensionsMs > 1000)
            {
                advice.AppendLine("- Quét kích thước trang tốn nhiều thời gian do tài liệu có quá nhiều trang hoặc trang chứa bản vẽ kỹ thuật nặng (CAD/Revit). Bạn nên dùng tính năng 'Tối ưu dung lượng' để nén bớt tài liệu.");
            }

            if (_activeTab.BitmapCacheBytes > 350 * 1024 * 1024)
            {
                advice.AppendLine("- Cảnh báo bộ nhớ đệm: Ứng dụng đang lưu trữ nhiều trang bitmap nặng trong RAM. Cache sẽ tự động được dọn dẹp khi đạt giới hạn 400MB.");
            }

            if (_activeTab.PageCount > 50)
            {
                advice.AppendLine("- Gợi ý: Với tài liệu dài, hãy sử dụng thanh cuộn bên trái hoặc chức năng 'Bố Cục Trang' để nhảy trang nhanh chóng thay vì cuộn trang liên tục.");
            }

            RecommendationText.Text = advice.ToString();
        }
        catch (Exception ex)
        {
            RecommendationText.Text = $"Không thể chẩn đoán: {ex.Message}";
        }
    }

    private void FlushCache_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_activeTab.CacheManager != null)
            {
                _activeTab.CacheManager.Clear();
                _activeTab.RenderPdfPages();
                PdfPerfLogger.Log("Flush Cache triggered manually from Diagnostics Window");
                RefreshStats();
                MessageBox.Show(this, "Đã giải phóng bộ nhớ đệm bitmap thành công và yêu cầu dựng lại hình.", "Dọn dẹp Cache", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Lỗi khi dọn dẹp cache: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private long ParseLogValue(string logs, string regexPattern)
    {
        try
        {
            var matches = Regex.Matches(logs, regexPattern);
            if (matches.Count > 0)
            {
                var match = matches[matches.Count - 1];
                if (match.Success && match.Groups.Count > 1)
                {
                    return long.Parse(match.Groups[1].Value);
                }
            }
        }
        catch {}
        return 0;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double value = bytes;
        int unitIndex = 0;
        while (value >= 1024.0 && unitIndex < units.Length - 1)
        {
            value /= 1024.0;
            unitIndex++;
        }
        return $"{value:0.##} {units[unitIndex]}";
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

public class RenderLogItem
{
    public string Time { get; set; }
    public string Page { get; set; }
    public string Status { get; set; }
    public string Detail { get; set; }
}
