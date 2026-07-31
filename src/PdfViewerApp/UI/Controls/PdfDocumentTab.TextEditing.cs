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

/// <summary>
/// Module quản lý tính năng Sửa Chữ PDF Trực Tiếp Mới (Standalone File Module).
/// Giúp dễ dàng bảo trì, nâng cấp mà không làm ảnh hưởng đến các thành phần khác.
/// </summary>
public partial class PdfDocumentTab
{
	// Danh sách các vùng chữ có thể sửa được trên các trang
	private readonly Dictionary<int, List<EditableTextRegion>> _pageEditableRegions = new();
	private bool _isHighlightEditableRegionsEnabled = false;

	/// <summary>
	/// <summary>
	/// Struct chứa thông tin chi tiết một vùng chữ có thể chỉnh sửa trên trang PDF
	/// </summary>
	public struct EditableTextRegion
	{
		public int PageNumber { get; set; }
		public string Text { get; set; }
		public Rect PdfBounds { get; set; } // Tọa độ PDF Point (Left, Bottom, Width, Height)
		public string FontName { get; set; }
		public double FontSize { get; set; }
		public bool IsBold { get; set; }
		public Color TextColor { get; set; }
		public int PdfObjectType { get; set; } // 1: Vector Text chuẩn, 2: Subset Font (CID), 3: Scanned Image Text (OCR)
		public bool IsModified { get; set; }
	}

	/// <summary>
	/// Bật / Tắt chế độ hiển thị vùng có thể sửa chữ (Highlight Editable Regions)
	/// </summary>
	public async void SetHighlightEditableRegions(bool enable)
	{
		_isHighlightEditableRegionsEnabled = enable;
		LogStatus(enable ? "Đang quét và hiển thị các vùng chữ có thể sửa..." : "Đã tắt chế độ hiển thị vùng sửa chữ.");

		if (enable && PageCount > 0)
		{
			int pageNumber = Math.Clamp(SelectedPageNumber, 1, PageCount);
			await GetEditableTextRegionsAsync(pageNumber);
		}

		RedrawAllPageAnnotations();
	}

	/// <summary>
	/// Tải danh sách các vùng chữ có thể sửa từ OCR/PDF Core cho 1 trang
	/// </summary>
	public async Task<List<EditableTextRegion>> GetEditableTextRegionsAsync(int pageNumber)
	{
		if (_pageEditableRegions.TryGetValue(pageNumber, out var existingList) && existingList.Count > 0)
		{
			return existingList;
		}

		List<EditableTextRegion> regions = new List<EditableTextRegion>();

		try
		{
			if (OperatingSystem.IsWindows() && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240))
			{
				List<OcrTextRegion>? ocrRegions = await EnsureOcrRegionsAsync(pageNumber);
				if (ocrRegions != null)
				{
					foreach (var ocr in ocrRegions)
					{
						// Tự động nhận diện in đậm dựa vào chiều cao chữ và tiêu đề
						bool isBold = ocr.Height >= 14.0 || 
									  ocr.Text.StartsWith("Invoi", StringComparison.OrdinalIgnoreCase) ||
									  ocr.Text.Equals("Your", StringComparison.OrdinalIgnoreCase) ||
									  ocr.Text.Equals("VAT", StringComparison.OrdinalIgnoreCase) ||
									  ocr.Text.Equals("ID:", StringComparison.OrdinalIgnoreCase) ||
									  ocr.Text.Equals("date:", StringComparison.OrdinalIgnoreCase) ||
									  ocr.Text.Equals("number:", StringComparison.OrdinalIgnoreCase);

						regions.Add(new EditableTextRegion
						{
							PageNumber = pageNumber,
							Text = ocr.Text,
							PdfBounds = new Rect(ocr.Left, ocr.Bottom, ocr.Width, ocr.Height),
							FontName = "Arial",
							FontSize = Math.Max(9.0, ocr.Height),
							IsBold = isBold,
							TextColor = Colors.Black,
							PdfObjectType = 3,
							IsModified = false
						});
					}
				}
			}
		}
		catch (Exception ex)
		{
			LogStatus("Lỗi quét vùng chữ: " + ex.Message);
		}

		_pageEditableRegions[pageNumber] = regions;
		return regions;
	}

	/// <summary>
	/// Vẽ các khung Highlight màu xanh nhạt bao quanh những vùng chữ có thể sửa để người dùng nhận biết
	/// </summary>
	private void DrawEditableTextRegionsHighlight(Canvas canvas, int pageNumber)
	{
		if (!_isHighlightEditableRegionsEnabled) return;

		if (_pageEditableRegions.TryGetValue(pageNumber, out var regions))
		{
			foreach (var region in regions)
			{
				double minLeft = region.PdfBounds.X;
				double minBottom = region.PdfBounds.Y;
				double maxRight = region.PdfBounds.X + region.PdfBounds.Width;
				double maxTop = region.PdfBounds.Y + region.PdfBounds.Height;

				if (TryPdfRectToCanvasRect(canvas, pageNumber, minLeft, maxRight, minBottom, maxTop, out Rect canvasRect))
				{
					// Nếu chữ đã được người dùng sửa, vẽ miếng dán màu trắng đè nền chữ cũ và vẽ chữ mới giữ nguyên định dạng in đậm
					if (region.IsModified)
					{
						Rectangle patch = new Rectangle
						{
							Width = Math.Max(8.0, canvasRect.Width + 2.0),
							Height = Math.Max(8.0, canvasRect.Height),
							Fill = Brushes.White
						};
						Canvas.SetLeft(patch, canvasRect.X - 1);
						Canvas.SetTop(patch, canvasRect.Y);
						canvas.Children.Add(patch);

						TextBlock tbModified = new TextBlock
						{
							Text = region.Text,
							FontSize = Math.Max(10.0, canvasRect.Height * 0.85),
							FontWeight = region.IsBold ? FontWeights.Bold : FontWeights.Normal,
							Foreground = Brushes.Black,
							FontFamily = new FontFamily("Segoe UI, Arial, sans-serif")
						};
						Canvas.SetLeft(tbModified, canvasRect.X);
						Canvas.SetTop(tbModified, canvasRect.Y);
						canvas.Children.Add(tbModified);
					}

					Rectangle rect = new Rectangle
					{
						Width = Math.Max(8.0, canvasRect.Width),
						Height = Math.Max(8.0, canvasRect.Height),
						Stroke = new SolidColorBrush(Color.FromArgb(160, 100, 116, 139)), // Viền xám xanh tinh tế Foxit
						StrokeThickness = 0.8,
						Fill = Brushes.Transparent,
						IsHitTestVisible = true,
						Cursor = Cursors.IBeam
					};

					rect.ToolTip = $"Click để sửa chữ: \"{region.Text}\"";

					// Gắn sự kiện click mở ô gõ chữ
					rect.MouseLeftButtonDown += (s, e) =>
					{
						e.Handled = true;
						OpenInlineTextEditor(canvas, pageNumber, region, canvasRect);
					};

					Canvas.SetLeft(rect, canvasRect.X);
					Canvas.SetTop(rect, canvasRect.Y);
					canvas.Children.Add(rect);
				}
			}
		}
	}

	/// <summary>
	/// Mở ô gõ chữ trực tiếp (Inline TextBox) ngay tại đúng vị trí khung chữ được chọn
	/// </summary>
	private void OpenInlineTextEditor(Canvas canvas, int pageNumber, EditableTextRegion region, Rect canvasRect)
	{
		TextBox tb = new TextBox
		{
			Text = region.Text,
			Width = Math.Max(canvasRect.Width + 12.0, 40.0),
			Height = Math.Max(canvasRect.Height + 4.0, 20.0),
			FontFamily = new FontFamily("Segoe UI, Arial, sans-serif"),
			FontSize = Math.Max(10.0, canvasRect.Height * 0.85),
			FontWeight = region.IsBold ? FontWeights.Bold : FontWeights.Normal, // Giữ nguyên in đậm Bold nếu là tiêu đề
			Foreground = Brushes.Black,
			Background = Brushes.White,
			BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
			BorderThickness = new Thickness(1.5),
			Padding = new Thickness(1, 0, 1, 0),
			VerticalContentAlignment = VerticalAlignment.Center
		};

		Canvas.SetLeft(tb, Math.Max(0, canvasRect.X - 2));
		Canvas.SetTop(tb, Math.Max(0, canvasRect.Y - 2));
		canvas.Children.Add(tb);

		tb.Loaded += (s, e) =>
		{
			tb.Focus();
			tb.SelectAll();
		};

		Action commit = () =>
		{
			string newText = tb.Text.Trim();
			if (canvas.Children.Contains(tb)) canvas.Children.Remove(tb);

			if (!string.IsNullOrEmpty(newText) && newText != region.Text)
			{
				// Lưu cập nhật vùng chữ và đánh dấu đã chỉnh sửa
				for (int i = 0; i < _pageEditableRegions[pageNumber].Count; i++)
				{
					var r = _pageEditableRegions[pageNumber][i];
					if (Math.Abs(r.PdfBounds.X - region.PdfBounds.X) < 1.0 && Math.Abs(r.PdfBounds.Y - region.PdfBounds.Y) < 1.0)
					{
						r.Text = newText;
						r.IsModified = true;
						_pageEditableRegions[pageNumber][i] = r;
						break;
					}
				}

				LogStatus($"[Sửa Chữ PDF] Đã cập nhật: '{region.Text}' -> '{newText}' trên trang {pageNumber}");
				RedrawAllPageAnnotations();
			}
		};

		tb.KeyDown += (s, e) =>
		{
			if (e.Key == Key.Enter)
			{
				commit();
				e.Handled = true;
			}
			else if (e.Key == Key.Escape)
			{
				if (canvas.Children.Contains(tb)) canvas.Children.Remove(tb);
				e.Handled = true;
			}
		};

		tb.LostFocus += (s, e) => commit();
	}
}
