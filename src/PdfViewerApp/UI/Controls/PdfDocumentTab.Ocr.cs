using System;
#pragma warning disable CA1416
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Windows.Globalization;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;

namespace PdfViewerApp
{
	public partial class PdfDocumentTab
	{
		private static Windows.Foundation.Rect UnionRect(Windows.Foundation.Rect a, Windows.Foundation.Rect b)
		{
			double left = Math.Min(a.X, b.X);
			double top = Math.Min(a.Y, b.Y);
			double right = Math.Max(a.X + a.Width, b.X + b.Width);
			double bottom = Math.Max(a.Y + a.Height, b.Y + b.Height);
			return new Windows.Foundation.Rect(left, top, right - left, bottom - top);
		}

		public async Task PreloadOcrForSelectedPageAsync()
		{
			if (!OperatingSystem.IsWindows() || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240))
			{
				return;
			}

			if (PageCount <= 0)
			{
				return;
			}
			int pageNumber = Math.Clamp(SelectedPageNumber, 1, PageCount);
			List<OcrTextRegion>? regions = await EnsureOcrRegionsAsync(pageNumber);
			if (regions != null && regions.Count > 0)
			{
				LogStatus($"OCR ready for page {pageNumber} ({regions.Count} regions).");
			}
			else
			{
				LogStatus($"OCR found no text on page {pageNumber}.");
			}
		}

		public async Task ExportOcrTextAsync()
		{
			if (!OperatingSystem.IsWindows() || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240))
			{
				MessageBox.Show("Tính năng OCR chỉ hỗ trợ từ Windows 10 trở lên.", "Không hỗ trợ", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			if (string.IsNullOrEmpty(CurrentPdfPath) || PageCount <= 0)
			{
				MessageBox.Show("Vui lòng mở một file PDF trước.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			MessageBoxResult scopeChoice = MessageBox.Show(
				"Bạn có muốn chạy OCR và xuất toàn bộ tài liệu không?\n\n- Click YES để xuất TOÀN BỘ tài liệu.\n- Click NO để chỉ xuất TRANG HIỆN TẠI.\n- Click CANCEL để hủy.",
				"Phạm vi xuất OCR",
				MessageBoxButton.YesNoCancel,
				MessageBoxImage.Question
			);

			if (scopeChoice == MessageBoxResult.Cancel)
			{
				return;
			}

			SaveFileDialog saveFileDialog = new SaveFileDialog
			{
				Filter = "Text Files (*.txt)|*.txt|Word Documents (*.docx)|*.docx",
				Title = "Lưu kết quả OCR",
				FileName = System.IO.Path.GetFileNameWithoutExtension(CurrentPdfPath) + "_OCR"
			};

			if (saveFileDialog.ShowDialog() != true)
			{
				return;
			}

			string outputPath = saveFileDialog.FileName;
			bool isDocx = System.IO.Path.GetExtension(outputPath).Equals(".docx", StringComparison.OrdinalIgnoreCase);

			LogStatus("Đang chạy nhận diện OCR...");

			try
			{
				StringBuilder fullTextBuilder = new StringBuilder();
				List<int> pagesToProcess = new List<int>();

				if (scopeChoice == MessageBoxResult.Yes)
				{
					for (int i = 1; i <= PageCount; i++) pagesToProcess.Add(i);
				}
				else
				{
					pagesToProcess.Add(Math.Clamp(SelectedPageNumber, 1, PageCount));
				}

				await Task.Run(async () =>
				{
					for (int idx = 0; idx < pagesToProcess.Count; idx++)
					{
						int pageNum = pagesToProcess[idx];
						base.Dispatcher.Invoke(() => LogStatus($"Đang chạy OCR trang {pageNum}/{PageCount}..."));

						List<OcrTextRegion>? regions = await EnsureOcrRegionsAsync(pageNum);
						if (regions != null && regions.Count > 0)
						{
							string pageText = string.Join(" ", regions.Select(r => r.Text));
							
							lock (fullTextBuilder)
							{
								if (scopeChoice == MessageBoxResult.Yes)
								{
									fullTextBuilder.AppendLine($"--- Trang {pageNum} ---");
								}
								fullTextBuilder.AppendLine(pageText);
								fullTextBuilder.AppendLine();
							}
						}
					}
				});

				string resultText = fullTextBuilder.ToString();
				if (string.IsNullOrWhiteSpace(resultText))
				{
					MessageBox.Show("Không nhận diện được văn bản nào trong phạm vi đã chọn.", "Không có kết quả", MessageBoxButton.OK, MessageBoxImage.Warning);
					LogStatus("Nhận diện OCR không có kết quả");
					return;
				}

				if (isDocx)
				{
					SaveAsDocx(outputPath, resultText);
				}
				else
				{
					File.WriteAllText(outputPath, resultText, Encoding.UTF8);
				}

				MessageBox.Show("Xuất kết quả OCR thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
				LogStatus("Đã lưu kết quả OCR tại: " + System.IO.Path.GetFileName(outputPath));
			}
			catch (Exception ex)
			{
				MessageBox.Show("Có lỗi xảy ra khi xuất kết quả OCR: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
				LogStatus("Lỗi xuất OCR: " + ex.Message);
			}
		}

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

		public async Task ExportSearchablePdfAsync()
		{
			if (!OperatingSystem.IsWindows() || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240))
			{
				MessageBox.Show("Tính năng OCR chỉ hỗ trợ từ Windows 10 trở lên.", "Không hỗ trợ", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			if (string.IsNullOrEmpty(CurrentPdfPath) || PageCount <= 0)
			{
				MessageBox.Show("Vui lòng mở một file PDF trước.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			SaveFileDialog saveFileDialog = new SaveFileDialog
			{
				Filter = "PDF Documents (*.pdf)|*.pdf",
				Title = "Lưu Searchable PDF",
				FileName = System.IO.Path.GetFileNameWithoutExtension(CurrentPdfPath) + "_Searchable"
			};

			if (saveFileDialog.ShowDialog() != true)
			{
				return;
			}

			string outputPath = saveFileDialog.FileName;
			if (IsSamePath(CurrentPdfPath, outputPath))
			{
				MessageBox.Show("Tên file đích phải khác với file nguồn.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			LogStatus("Đang chuyển đổi thành Searchable PDF...");

			try
			{
				StringBuilder ocrDataBuilder = new StringBuilder();

				await Task.Run(async () =>
				{
					for (int pageNum = 1; pageNum <= PageCount; pageNum++)
					{
						base.Dispatcher.Invoke(() => LogStatus($"Đang nhận diện văn bản trang {pageNum}/{PageCount}..."));

						List<OcrTextRegion>? regions = await EnsureOcrRegionsAsync(pageNum);
						if (regions != null && regions.Count > 0)
						{
							foreach (var region in regions)
							{
								string line = $"{pageNum}|{region.Left:F2}|{region.Bottom:F2}|{region.Width:F2}|{region.Height:F2}|{region.Text}";
								lock (ocrDataBuilder)
								{
									ocrDataBuilder.AppendLine(line);
								}
							}
						}
					}
				});

				string ocrRawData = ocrDataBuilder.ToString();
				if (string.IsNullOrWhiteSpace(ocrRawData))
				{
					MessageBox.Show("Không tìm thấy văn bản nào để tạo lớp tìm kiếm. PDF sẽ chỉ lưu lại như cũ.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
				}

				LogStatus("Đang ghép lớp văn bản vào file PDF...");
				bool success = await Task.Run(() => PdfInterop.PdfCore.make_pdf_searchable(CurrentPdfPath, ocrRawData, outputPath));

				if (success)
				{
					MessageBox.Show("Tạo Searchable PDF thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
					LogStatus("Đã lưu Searchable PDF tại: " + System.IO.Path.GetFileName(outputPath));
					this.DocumentOpenRequested?.Invoke(this, outputPath);
				}
				else
				{
					MessageBox.Show("Không thể tạo Searchable PDF. Kiểm tra lại tệp PDF nguồn.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
					LogStatus("Lỗi tạo Searchable PDF");
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("Có lỗi xảy ra: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
				LogStatus("Lỗi tạo Searchable PDF: " + ex.Message);
			}
		}

		private async void TryShowOcrTextEditOverlayAsync(Canvas canvas, Point clickPoint, int pageNumber)
		{
			if (!OperatingSystem.IsWindows() || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240))
			{
				return;
			}

			if (!TryCanvasToPdfPoint(canvas, clickPoint, pageNumber, out Point pdfPoint))
			{
				return;
			}
			List<OcrTextRegion>? regions = await EnsureOcrRegionsAsync(pageNumber);
			if (regions == null || regions.Count == 0)
			{
				LogStatus("OCR could not detect editable text on this page.");
				return;
			}
			OcrTextRegion? match = null;
			foreach (OcrTextRegion region in regions)
			{
				if (pdfPoint.X >= region.Left && pdfPoint.X <= region.Left + region.Width && pdfPoint.Y >= region.Bottom && pdfPoint.Y <= region.Bottom + region.Height)
				{
					match = region;
					break;
				}
			}
			if (match == null)
			{
				OcrTextRegion best = regions[0];
				double bestDistance = double.MaxValue;
				foreach (OcrTextRegion region in regions)
				{
					double centerX = region.Left + region.Width / 2.0;
					double centerY = region.Bottom + region.Height / 2.0;
					double distance = Math.Abs(centerX - pdfPoint.X) + Math.Abs(centerY - pdfPoint.Y);
					if (distance < bestDistance)
					{
						bestDistance = distance;
						best = region;
					}
				}
				match = best;
			}
			if (match != null && !string.IsNullOrWhiteSpace(match.Value.Text))
			{
				ShowDirectTextEditOverlayFromBounds(canvas, pageNumber, match.Value.Left, match.Value.Bottom, match.Value.Left + match.Value.Width, match.Value.Bottom + match.Value.Height, match.Value.Text);
				LogStatus($"OCR edit ready on page {pageNumber}.");
			}
		}

		[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.10240.0")]
		private Task<List<OcrTextRegion>?> EnsureOcrRegionsAsync(int pageNumber)
		{
			return _ocrLoadingTasks.GetOrAdd(pageNumber, page => RecognizeOcrRegionsAsync(page));
		}

		[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.10240.0")]
		private async Task<List<OcrTextRegion>?> RecognizeOcrRegionsAsync(int pageNumber)
		{
			if (_documentHandle == IntPtr.Zero || !TryGetPageSize(pageNumber, out Size pageSize))
			{
				return null;
			}
			int targetWidth = 1800;
			int targetHeight = Math.Max(1, (int)Math.Round(targetWidth * pageSize.Height / Math.Max(1.0, pageSize.Width)));
			BitmapSource? pageBitmap = await Task.Run(() => PdfiumEngine.RenderPageToBitmap(_documentHandle, pageNumber - 1, targetWidth, targetHeight));
			if (pageBitmap == null)
			{
				return null;
			}
			string imagePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
			try
			{
				PngBitmapEncoder encoder = new PngBitmapEncoder();
				encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(pageBitmap));
				using (FileStream fileStream = File.Create(imagePath))
				{
					encoder.Save(fileStream);
				}
				AppPreferences preferences = AppPreferences.Load();
				OcrEngine? engine = null;
				if (!string.IsNullOrEmpty(preferences.OcrLanguage))
				{
					try
					{
						engine = OcrEngine.TryCreateFromLanguage(new Language(preferences.OcrLanguage));
					}
					catch
					{
					}
				}
				if (engine == null)
				{
					engine = OcrEngine.TryCreateFromUserProfileLanguages() ?? OcrEngine.TryCreateFromLanguage(new Language("en"));
				}
				if (engine == null)
				{
					return null;
				}
				using (FileStream inputStream = File.OpenRead(imagePath))
				{
					IRandomAccessStream randomAccessStream = inputStream.AsRandomAccessStream();
					Windows.Graphics.Imaging.BitmapDecoder decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(randomAccessStream).AsTask();
					SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync().AsTask();
					if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Ignore)
					{
						softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
					}
					OcrResult result = await engine.RecognizeAsync(softwareBitmap).AsTask();
					List<OcrTextRegion> regions = new List<OcrTextRegion>();
					double scaleX = pageSize.Width / Math.Max(1.0, pageBitmap.PixelWidth);
					double scaleY = pageSize.Height / Math.Max(1.0, pageBitmap.PixelHeight);
					foreach (var line in result.Lines)
					{
						string text = line.Text?.Trim() ?? "";
						if (string.IsNullOrWhiteSpace(text))
						{
							continue;
						}
						Windows.Foundation.Rect? rect = null;
						foreach (var word in line.Words)
						{
							Windows.Foundation.Rect wordRect = word.BoundingRect;
							rect = rect == null ? wordRect : UnionRect(rect.Value, wordRect);
						}
						if (rect == null)
						{
							continue;
						}
						double left = rect.Value.X * scaleX;
						double right = (rect.Value.X + rect.Value.Width) * scaleX;
						double top = pageSize.Height - rect.Value.Y * scaleY;
						double bottom = pageSize.Height - (rect.Value.Y + rect.Value.Height) * scaleY;
						regions.Add(new OcrTextRegion(text, left, bottom, right - left, top - bottom));
					}
					return regions;
				}
			}
			catch (Exception ex)
			{
				PdfPerfLogger.Log("OCR failed: " + ex.Message);
				return null;
			}
			finally
			{
				try
				{
					File.Delete(imagePath);
				}
				catch
				{
				}
			}
		}
	}
}
