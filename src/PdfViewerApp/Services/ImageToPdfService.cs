using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace PdfViewerApp.Services;

/// <summary>
/// Dịch vụ chuyển đổi tệp hình ảnh (.png, .jpg, .jpeg, .bmp, .webp, .tiff, ...) thành tài liệu PDF.
/// Hỗ trợ cả chuyển đổi 1 ảnh đơn lẻ hoặc ghép nhiều ảnh thành file PDF nhiều trang.
/// </summary>
public static class ImageToPdfService
{
	public static readonly string[] SupportedImageExtensions = new[]
	{
		".png", ".jpg", ".jpeg", ".bmp", ".webp", ".tiff", ".tif", ".gif", ".ico"
	};

	public static bool IsSupportedImage(string? filePath)
	{
		if (string.IsNullOrWhiteSpace(filePath))
		{
			return false;
		}

		try
		{
			string ext = Path.GetExtension(filePath);
			return SupportedImageExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Lấy danh sách các tệp hình ảnh hợp lệ và tồn tại.
	/// </summary>
	public static string[] FilterImageFiles(IEnumerable<string> files)
	{
		return files
			.Where(f => !string.IsNullOrWhiteSpace(f) && File.Exists(f) && IsSupportedImage(f))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
	}

	/// <summary>
	/// Thư mục lưu tệp PDF tạm thời khi chuyển đổi từ ảnh.
	/// </summary>
	public static string GetConvertedImagesTempDir()
	{
		string tempDir = Path.Combine(Path.GetTempPath(), "PdfPro", "ConvertedImages");
		if (!Directory.Exists(tempDir))
		{
			Directory.CreateDirectory(tempDir);
		}
		return tempDir;
	}

	/// <summary>
	/// Chuyển đổi 1 file ảnh thành 1 tài liệu PDF.
	/// Kích thước trang PDF tự động căn chỉnh vừa khít kích thước pixel và DPI của ảnh gốc.
	/// </summary>
	public static string ConvertImageToPdf(string imagePath, string? outputPath = null)
	{
		return ConvertMultipleImagesToPdf(new[] { imagePath }, outputPath);
	}

	/// <summary>
	/// Chuyển đổi một hoặc nhiều file ảnh thành 1 tài liệu PDF hoàn chỉnh (mỗi ảnh là 1 trang).
	/// </summary>
	public static string ConvertMultipleImagesToPdf(IEnumerable<string> imagePaths, string? outputPath = null)
	{
		var validImages = imagePaths.Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p)).ToList();
		if (validImages.Count == 0)
		{
			throw new FileNotFoundException("Không tìm thấy tệp ảnh hợp lệ để chuyển đổi sang PDF.");
		}

		if (string.IsNullOrWhiteSpace(outputPath))
		{
			string firstFileName = Path.GetFileNameWithoutExtension(validImages[0]);
			string tempDir = GetConvertedImagesTempDir();
			string suffix = validImages.Count > 1 ? $"_{validImages.Count}pages" : string.Empty;
			outputPath = Path.Combine(tempDir, $"{firstFileName}{suffix}_{Guid.NewGuid():N}.pdf");
		}
		else
		{
			string? dir = Path.GetDirectoryName(outputPath);
			if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}
		}

		using var doc = new PdfDocument();
		doc.Info.Title = Path.GetFileNameWithoutExtension(validImages[0]);
		doc.Info.Creator = "PDF Pro - Image to PDF Converter";

		foreach (var imgPath in validImages)
		{
			AddImagePageToDocument(doc, imgPath);
		}

		doc.Save(outputPath);
		return outputPath;
	}

	private static void AddImagePageToDocument(PdfDocument doc, string imgPath)
	{
		XImage? xImage = null;
		MemoryStream? fallbackStream = null;

		try
		{
			// Bước 1: Thử nạp trực tiếp qua PdfSharp XImage
			try
			{
				xImage = XImage.FromFile(imgPath);
			}
			catch
			{
				// XImage.FromFile có thể không hỗ trợ WebP hoặc một số format TIFF đặc thù
				xImage = null;
			}

			// Bước 2: Fallback nạp qua Windows Imaging Component (WIC / WPF BitmapDecoder)
			if (xImage == null)
			{
				using var fileStream = File.OpenRead(imgPath);
				var decoder = BitmapDecoder.Create(fileStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
				if (decoder.Frames.Count > 0)
				{
					var frame = decoder.Frames[0];
					var encoder = new PngBitmapEncoder();
					encoder.Frames.Add(BitmapFrame.Create(frame));
					fallbackStream = new MemoryStream();
					encoder.Save(fallbackStream);
					fallbackStream.Position = 0;
					xImage = XImage.FromStream(fallbackStream);
				}
			}

			if (xImage == null)
			{
				throw new InvalidOperationException($"Không thể giải mã dữ liệu hình ảnh: {imgPath}");
			}

			// Tính toán kích thước trang theo tỉ lệ điểm ảnh chuẩn (72 points/inch)
			double dpiX = xImage.HorizontalResolution > 0 ? xImage.HorizontalResolution : 96.0;
			double dpiY = xImage.VerticalResolution > 0 ? xImage.VerticalResolution : 96.0;

			double widthPt = xImage.PixelWidth * 72.0 / dpiX;
			double heightPt = xImage.PixelHeight * 72.0 / dpiY;

			if (widthPt <= 0 || heightPt <= 0)
			{
				widthPt = xImage.PointWidth > 0 ? xImage.PointWidth : 595.0;
				heightPt = xImage.PointHeight > 0 ? xImage.PointHeight : 842.0;
			}

			var page = doc.AddPage();
			page.Width = XUnit.FromPoint(widthPt);
			page.Height = XUnit.FromPoint(heightPt);

			using var gfx = XGraphics.FromPdfPage(page);
			gfx.DrawImage(xImage, 0, 0, widthPt, heightPt);
		}
		finally
		{
			xImage?.Dispose();
			fallbackStream?.Dispose();
		}
	}
}
