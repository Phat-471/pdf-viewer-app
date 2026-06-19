using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace PdfViewerApp;

public partial class PdfDiagnosticsWindow : Window
{
    public PdfDiagnosticsWindow(string pdfPath, PdfDocumentTab activeTab)
    {
        InitializeComponent();

        try
        {
            // 1. Set File Info
            FileNameText.Text = $"Tên file: {Path.GetFileName(pdfPath)}";
            if (File.Exists(pdfPath))
            {
                long bytes = new FileInfo(pdfPath).Length;
                FileSizeText.Text = FormatBytes(bytes);

                if (bytes > 50 * 1024 * 1024)
                {
                    FileSizeText.Text += " (Nặng ⚠️)";
                }
            }

            PageCountText.Text = $"{activeTab.PageCount} trang";

            // Cache Stats
            double mbUsed = activeTab.BitmapCacheBytes / (1024.0 * 1024.0);
            CacheStateText.Text = $"{activeTab.BitmapCacheCount} trang ({mbUsed:0.##} MB)";

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

            // 3. Build recommendation advice
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

            if (activeTab.BitmapCacheBytes > 350 * 1024 * 1024)
            {
                advice.AppendLine("- Cảnh báo bộ nhớ đệm: Ứng dụng đang lưu trữ nhiều trang bitmap nặng trong RAM. Cache sẽ tự động được dọn dẹp khi đạt giới hạn 400MB.");
            }

            if (activeTab.PageCount > 50)
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

    private long ParseLogValue(string logs, string regexPattern)
    {
        try
        {
            var matches = Regex.Matches(logs, regexPattern);
            if (matches.Count > 0)
            {
                // Get the last match corresponding to the most recent document load
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
