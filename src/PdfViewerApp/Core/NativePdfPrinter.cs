using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PdfViewerApp;

internal static class NativePdfPrinter
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct DOCINFO
	{
		public int cbSize;

		public string? lpszDocName;

		public string? lpszOutput;

		public string? lpszDatatype;

		public int fwType;
	}

	private class PreRenderedPage
	{
		public int PageIndex { get; set; }
		public int Copy { get; set; }
		public byte[]? BitmapBuffer { get; set; }
		public int Width { get; set; }
		public int Height { get; set; }
		public int Stride { get; set; }
		public int DrawX { get; set; }
		public int DrawY { get; set; }
		public int DestWidth { get; set; }
		public int DestHeight { get; set; }
		public bool IsRasterized { get; set; }
		public nint PageHandle { get; set; }
	}

	private const int HORZRES = 8;

	private const int VERTRES = 10;

	private const int LOGPIXELSX = 88;

	private const int LOGPIXELSY = 90;

	private const int PHYSICALOFFSETX = 112;

	private const int PHYSICALOFFSETY = 113;

	private const int WHITENESS = 16711778;

	public static void Print(string pdfPath, string printQueueName, byte[]? devMode, int startPageIndex, int endPageIndex, int copies, bool fitToPrintableArea, bool autoCenter, bool driverAlreadyOffsetsPrintableArea, double rightSafetyPaddingDips, double bottomSafetyPaddingDips, bool separatePageJobs, bool reversePageOrder, bool forceRasterize, IProgress<PrintProgressInfo>? progress = null, CancellationToken cancellationToken = default(CancellationToken), double printDpi = 600.0)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		PdfPerfLogger.Log("Native PDFium print start with Parallel Rendering");
		progress?.Report(new PrintProgressInfo("Dang chuan bi PDFium...", 0, 0, IsIndeterminate: true));
		PdfiumEngine.Initialize();
		
		cancellationToken.ThrowIfCancellationRequested();
		nint num = IntPtr.Zero;
		lock (PdfiumEngine.SyncRoot)
		{
			num = PdfiumEngine.FPDF_LoadDocument(pdfPath, null);
		}
		if (num == IntPtr.Zero)
		{
			throw new InvalidOperationException("Unable to load PDF for native printing.");
		}
		nint num2 = IntPtr.Zero;
		bool flag = false;
		try
		{
			int num3 = 0;
			lock (PdfiumEngine.SyncRoot)
			{
				num3 = PdfiumEngine.FPDF_GetPageCount(num);
			}
			if (num3 <= 0)
			{
				throw new InvalidOperationException("PDF has no printable pages.");
			}
			startPageIndex = Math.Clamp(startPageIndex, 0, num3 - 1);
			endPageIndex = Math.Clamp(endPageIndex, startPageIndex, num3 - 1);
			copies = Math.Clamp(copies, 1, 999);
			int num4 = Math.Max(1, endPageIndex - startPageIndex + 1) * copies;
			int num5 = 0;
			progress?.Report(new PrintProgressInfo("Dang tao ket noi may in...", 0, num4, IsIndeterminate: true));
			num2 = CreatePrinterDc(printQueueName, devMode);
			if (num2 == IntPtr.Zero)
			{
				throw new InvalidOperationException("Cannot create printer DC for " + printQueueName + ".");
			}
			int num6 = Math.Max(1, GetDeviceCaps(num2, 8));
			int num7 = Math.Max(1, GetDeviceCaps(num2, 10));
			int num8 = Math.Max(72, GetDeviceCaps(num2, 88));
			int num9 = Math.Max(72, GetDeviceCaps(num2, 90));
			int num10 = Math.Max(0, GetDeviceCaps(num2, 112));
			int num11 = Math.Max(0, GetDeviceCaps(num2, 113));
			int num12 = DipsToDevicePixels(rightSafetyPaddingDips, num8);
			int num13 = DipsToDevicePixels(bottomSafetyPaddingDips, num9);
			int num14 = Math.Max(1, num6 - num12);
			int num15 = Math.Max(1, num7 - num13);
			PdfPerfLogger.Log($"Native printer DC: printable={num6}x{num7}, dpi={num8}x{num9}, physicalOffset={num10},{num11}");
			PdfPerfLogger.Log($"Native safe area: {num14}x{num15}, rightPaddingPx={num12}, bottomPaddingPx={num13}");
			// FPDF_PRINTING (0x800) = tối ưu chất lượng in, FPDF_ANNOT (0x01) = hiển thị annotation
			// FPDF_LCD_TEXT (0x800 cũ tức 2048) bị gỡ vì không phù hợp với GDI printer DC
			int flags = 0x800 | 0x01; // FPDF_PRINTING | FPDF_ANNOT
			if (!separatePageJobs)
			{
				DOCINFO lpdi = CreateDocInfo(pdfPath, null);
				Stopwatch stopwatch2 = Stopwatch.StartNew();
				progress?.Report(new PrintProgressInfo("Dang mo lenh in...", num5, num4, IsIndeterminate: true));
				if (StartDoc(num2, ref lpdi) <= 0)
				{
					throw new InvalidOperationException("StartDoc failed: " + GetLastErrorMessage());
				}
				flag = true;
				stopwatch2.Stop();
				PdfPerfLogger.Log($"Native StartDoc: {stopwatch2.ElapsedMilliseconds} ms");
			}

			// Generate the list of print jobs
			var printJobs = new List<(int pageIndex, int copy)>();
			for (int i = 1; i <= copies; i++)
			{
				int num16 = ((!reversePageOrder) ? 1 : (-1));
				int num17 = (reversePageOrder ? endPageIndex : startPageIndex);
				int num18 = (reversePageOrder ? startPageIndex : endPageIndex);
				for (int j = num17; reversePageOrder ? (j >= num18) : (j <= num18); j += num16)
				{
					printJobs.Add((j, i));
				}
			}

			// Create a bounded blocking collection to limit peak memory usage (max 2 pre-rendered pages in queue)
			using (var renderQueue = new BlockingCollection<PreRenderedPage>(boundedCapacity: 2))
			{
				// Launch background producer task to load and pre-render PDF pages
				var producerTask = Task.Run(() =>
				{
					try
					{
						foreach (var job in printJobs)
						{
							if (cancellationToken.IsCancellationRequested)
							{
								break;
							}

							PreRenderedPage rendered;
							if (forceRasterize)
							{
								rendered = PreRenderPageToBitmap(num, job.pageIndex, job.copy, num14, num15, num8, num9, num10, num11, fitToPrintableArea, autoCenter, driverAlreadyOffsetsPrintableArea, flags, printDpi);
							}
							else
							{
								rendered = PrepareVectorPage(num, job.pageIndex, job.copy);
							}

							renderQueue.Add(rendered, cancellationToken);
						}
					}
					catch (OperationCanceledException)
					{
						// Normal cancellation path
					}
					catch (Exception ex)
					{
						PdfPerfLogger.Log($"Producer thread failed: {ex}");
					}
					finally
					{
						renderQueue.CompleteAdding();
					}
				});

				// Consumer: Spool pages to GDI printer context
				foreach (var rendered in renderQueue.GetConsumingEnumerable(cancellationToken))
				{
					cancellationToken.ThrowIfCancellationRequested();
					progress?.Report(new PrintProgressInfo($"Dang in trang {rendered.PageIndex + 1}, ban {rendered.Copy}...", num5, num4));
					if (separatePageJobs)
					{
						DOCINFO lpdi2 = CreateDocInfo(pdfPath, rendered.PageIndex + 1);
						Stopwatch stopwatch3 = Stopwatch.StartNew();
						progress?.Report(new PrintProgressInfo($"Dang mo job trang {rendered.PageIndex + 1}...", num5, num4));
						if (StartDoc(num2, ref lpdi2) <= 0)
						{
							throw new InvalidOperationException($"StartDoc failed on page {rendered.PageIndex + 1}: {GetLastErrorMessage()}");
						}
						flag = true;
						stopwatch3.Stop();
						PdfPerfLogger.Log($"Native StartDoc page-job {rendered.PageIndex + 1}: {stopwatch3.ElapsedMilliseconds} ms");
					}

					// Draw to GDI DC
					SpoolRenderedPage(num2, rendered, num6, num7, num14, num15, num8, num9, num10, num11, fitToPrintableArea, autoCenter, driverAlreadyOffsetsPrintableArea, flags, progress, num5, num4, cancellationToken);

					if (separatePageJobs)
					{
						Stopwatch stopwatch4 = Stopwatch.StartNew();
						progress?.Report(new PrintProgressInfo($"Dang spool trang {rendered.PageIndex + 1}...", num5, num4));
						if (EndDoc(num2) <= 0)
						{
							throw new InvalidOperationException($"EndDoc failed on page {rendered.PageIndex + 1}: {GetLastErrorMessage()}");
						}
						flag = false;
						stopwatch4.Stop();
						PdfPerfLogger.Log($"Native EndDoc page-job {rendered.PageIndex + 1} spool: {stopwatch4.ElapsedMilliseconds} ms");
					}

					num5++;
					progress?.Report(new PrintProgressInfo($"Da gui trang {rendered.PageIndex + 1} ({num5}/{num4})", num5, num4));

					GC.Collect(2, GCCollectionMode.Forced, blocking: true);
					GC.WaitForPendingFinalizers();
				}

				// Wait for producer task to finish cleanly
				producerTask.GetAwaiter().GetResult();
			}

			if (!separatePageJobs)
			{
				Stopwatch stopwatch5 = Stopwatch.StartNew();
				progress?.Report(new PrintProgressInfo("Dang ket thuc va spool lenh in...", num5, num4, IsIndeterminate: true));
				if (EndDoc(num2) <= 0)
				{
					throw new InvalidOperationException("EndDoc failed: " + GetLastErrorMessage());
				}
				flag = false;
				stopwatch5.Stop();
				PdfPerfLogger.Log($"Native EndDoc spool: {stopwatch5.ElapsedMilliseconds} ms");
			}
			// Ghi log đơn giản – KHÔNG dùng LocalPrintServer/PrintQueue trên background thread
			// vì System.Printing objects có thread affinity → gây crash "The calling thread cannot access this object"
			PdfPerfLogger.Log("Print job sent to spooler successfully.");
			progress?.Report(new PrintProgressInfo("Hoan tat gui lenh in.", num4, num4));
		}
		finally
		{
			if (flag && num2 != IntPtr.Zero)
			{
				AbortDoc(num2);
			}
			if (num2 != IntPtr.Zero)
			{
				DeleteDC(num2);
			}
			lock (PdfiumEngine.SyncRoot)
			{
				PdfiumEngine.FPDF_CloseDocument(num);
			}
			stopwatch.Stop();
			PdfPerfLogger.Log($"Native PDFium print total: {stopwatch.ElapsedMilliseconds} ms");
		}
	}

	public static void PrintOptimized(string pdfPath, string printQueueName, byte[]? devMode, int startPageIndex, int endPageIndex, int copies, bool fitToPrintableArea, bool autoCenter, bool driverAlreadyOffsetsPrintableArea, double rightSafetyPaddingDips, double bottomSafetyPaddingDips, bool separatePageJobs, bool reversePageOrder, bool forceRasterize, IProgress<PrintProgressInfo>? progress = null, CancellationToken cancellationToken = default(CancellationToken), double printDpi = 600.0)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		PdfPerfLogger.Log("Native PDFium print (Optimized) start with Parallel Rendering");
		progress?.Report(new PrintProgressInfo("Dang chuan bi PDFium...", 0, 0, IsIndeterminate: true));
		PdfiumEngine.Initialize();
		
		cancellationToken.ThrowIfCancellationRequested();
		nint num = IntPtr.Zero;
		lock (PdfiumEngine.SyncRoot)
		{
			num = PdfiumEngine.FPDF_LoadDocument(pdfPath, null);
		}
		if (num == IntPtr.Zero)
		{
			throw new InvalidOperationException("Unable to load PDF for native printing.");
		}
		nint num2 = IntPtr.Zero;
		bool flag = false;
		try
		{
			int num3 = 0;
			lock (PdfiumEngine.SyncRoot)
			{
				num3 = PdfiumEngine.FPDF_GetPageCount(num);
			}
			if (num3 <= 0)
			{
				throw new InvalidOperationException("PDF has no printable pages.");
			}
			startPageIndex = Math.Clamp(startPageIndex, 0, num3 - 1);
			endPageIndex = Math.Clamp(endPageIndex, startPageIndex, num3 - 1);
			copies = Math.Clamp(copies, 1, 999);
			int num4 = Math.Max(1, endPageIndex - startPageIndex + 1) * copies;
			int num5 = 0;
			progress?.Report(new PrintProgressInfo("Dang tao ket noi may in...", 0, num4, IsIndeterminate: true));
			num2 = CreatePrinterDc(printQueueName, devMode);
			if (num2 == IntPtr.Zero)
			{
				throw new InvalidOperationException("Cannot create printer DC for " + printQueueName + ".");
			}
			int num6 = Math.Max(1, GetDeviceCaps(num2, 8));
			int num7 = Math.Max(1, GetDeviceCaps(num2, 10));
			int num8 = Math.Max(72, GetDeviceCaps(num2, 88));
			int num9 = Math.Max(72, GetDeviceCaps(num2, 90));
			int num10 = Math.Max(0, GetDeviceCaps(num2, 112));
			int num11 = Math.Max(0, GetDeviceCaps(num2, 113));
			int num12 = DipsToDevicePixels(rightSafetyPaddingDips, num8);
			int num13 = DipsToDevicePixels(bottomSafetyPaddingDips, num9);
			int num14 = Math.Max(1, num6 - num12);
			int num15 = Math.Max(1, num7 - num13);
			PdfPerfLogger.Log($"Native printer DC: printable={num6}x{num7}, dpi={num8}x{num9}, physicalOffset={num10},{num11}");
			PdfPerfLogger.Log($"Native safe area: {num14}x{num15}, rightPaddingPx={num12}, bottomPaddingPx={num13}");
			// FPDF_PRINTING (0x800) = tối ưu chất lượng in, FPDF_ANNOT (0x01) = hiển thị annotation
			int flags = 0x800 | 0x01; // FPDF_PRINTING | FPDF_ANNOT
			if (!separatePageJobs)
			{
				DOCINFO lpdi = CreateDocInfo(pdfPath, null);
				Stopwatch stopwatch2 = Stopwatch.StartNew();
				progress?.Report(new PrintProgressInfo("Dang mo lenh in...", num5, num4, IsIndeterminate: true));
				if (StartDoc(num2, ref lpdi) <= 0)
				{
					throw new InvalidOperationException("StartDoc failed: " + GetLastErrorMessage());
				}
				flag = true;
				stopwatch2.Stop();
				PdfPerfLogger.Log($"Native StartDoc: {stopwatch2.ElapsedMilliseconds} ms");
			}

			// Generate the list of print jobs
			var printJobs = new List<(int pageIndex, int copy)>();
			for (int i = 1; i <= copies; i++)
			{
				int num16 = ((!reversePageOrder) ? 1 : (-1));
				int num17 = (reversePageOrder ? endPageIndex : startPageIndex);
				int num18 = (reversePageOrder ? startPageIndex : endPageIndex);
				for (int j = num17; reversePageOrder ? (j >= num18) : (j <= num18); j += num16)
				{
					printJobs.Add((j, i));
				}
			}

			// Create a bounded blocking collection to limit peak memory usage (max 2 pre-rendered pages in queue)
			using (var renderQueue = new BlockingCollection<PreRenderedPage>(boundedCapacity: 2))
			{
				// Launch background producer task to load and pre-render PDF pages
				var producerTask = Task.Run(() =>
				{
					try
					{
						foreach (var job in printJobs)
						{
							if (cancellationToken.IsCancellationRequested)
							{
								break;
							}

							PreRenderedPage rendered;
							if (forceRasterize)
							{
								rendered = PreRenderPageToBitmap(num, job.pageIndex, job.copy, num14, num15, num8, num9, num10, num11, fitToPrintableArea, autoCenter, driverAlreadyOffsetsPrintableArea, flags, printDpi);
							}
							else
							{
								rendered = PrepareVectorPage(num, job.pageIndex, job.copy);
							}

							renderQueue.Add(rendered, cancellationToken);
						}
					}
					catch (OperationCanceledException)
					{
						// Normal cancellation path
					}
					catch (Exception ex)
					{
						PdfPerfLogger.Log($"Producer thread failed: {ex}");
					}
					finally
					{
						renderQueue.CompleteAdding();
					}
				});

				try
				{
					// Consumer: Spool pages to GDI printer context
					foreach (var rendered in renderQueue.GetConsumingEnumerable(cancellationToken))
					{
						cancellationToken.ThrowIfCancellationRequested();
						progress?.Report(new PrintProgressInfo($"Dang in trang {rendered.PageIndex + 1}, ban {rendered.Copy}...", num5, num4));
						if (separatePageJobs)
						{
							DOCINFO lpdi2 = CreateDocInfo(pdfPath, rendered.PageIndex + 1);
							Stopwatch stopwatch3 = Stopwatch.StartNew();
							progress?.Report(new PrintProgressInfo($"Dang mo job trang {rendered.PageIndex + 1}...", num5, num4));
							if (StartDoc(num2, ref lpdi2) <= 0)
							{
								throw new InvalidOperationException($"StartDoc failed on page {rendered.PageIndex + 1}: {GetLastErrorMessage()}");
							}
							flag = true;
							stopwatch3.Stop();
							PdfPerfLogger.Log($"Native StartDoc page-job {rendered.PageIndex + 1}: {stopwatch3.ElapsedMilliseconds} ms");
						}

						// Draw to GDI DC
						SpoolRenderedPage(num2, rendered, num6, num7, num14, num15, num8, num9, num10, num11, fitToPrintableArea, autoCenter, driverAlreadyOffsetsPrintableArea, flags, progress, num5, num4, cancellationToken);

						if (separatePageJobs)
						{
							Stopwatch stopwatch4 = Stopwatch.StartNew();
							progress?.Report(new PrintProgressInfo($"Dang spool trang {rendered.PageIndex + 1}...", num5, num4));
							if (EndDoc(num2) <= 0)
							{
								throw new InvalidOperationException($"EndDoc failed on page {rendered.PageIndex + 1}: {GetLastErrorMessage()}");
							}
							flag = false;
							stopwatch4.Stop();
							PdfPerfLogger.Log($"Native EndDoc page-job {rendered.PageIndex + 1} spool: {stopwatch4.ElapsedMilliseconds} ms");
						}

						num5++;
						progress?.Report(new PrintProgressInfo($"Da gui trang {rendered.PageIndex + 1} ({num5}/{num4})", num5, num4));

						GC.Collect(2, GCCollectionMode.Forced, blocking: true);
						GC.WaitForPendingFinalizers();
					}
				}
				finally
				{
					// Dọn dẹp các PageHandle còn lại trong queue nếu in bị hủy hoặc lỗi để tránh rò rỉ bộ nhớ/tài nguyên
					while (renderQueue.TryTake(out var extraPage))
					{
						if (extraPage.PageHandle != IntPtr.Zero)
						{
							lock (PdfiumEngine.SyncRoot)
							{
								PdfiumEngine.FPDF_ClosePage(extraPage.PageHandle);
							}
							extraPage.PageHandle = IntPtr.Zero;
						}
					}
				}

				// Wait for producer task to finish cleanly
				producerTask.GetAwaiter().GetResult();
			}

			if (!separatePageJobs)
			{
				Stopwatch stopwatch5 = Stopwatch.StartNew();
				progress?.Report(new PrintProgressInfo("Dang ket thuc va spool lenh in...", num5, num4, IsIndeterminate: true));
				if (EndDoc(num2) <= 0)
				{
					throw new InvalidOperationException("EndDoc failed: " + GetLastErrorMessage());
				}
				flag = false;
				stopwatch5.Stop();
				PdfPerfLogger.Log($"Native EndDoc spool: {stopwatch5.ElapsedMilliseconds} ms");
			}

			// Ghi log đơn giản – KHÔNG dùng LocalPrintServer/PrintQueue trên background thread
			// vì System.Printing objects có thread affinity → gây crash "The calling thread cannot access this object"
			PdfPerfLogger.Log("Print job sent to spooler successfully.");
			progress?.Report(new PrintProgressInfo("Hoan tat gui lenh in.", num4, num4));
		}
		finally
		{
			if (flag && num2 != IntPtr.Zero)
			{
				AbortDoc(num2);
			}
			if (num2 != IntPtr.Zero)
			{
				DeleteDC(num2);
			}
			lock (PdfiumEngine.SyncRoot)
			{
				PdfiumEngine.FPDF_CloseDocument(num);
			}
			stopwatch.Stop();
			PdfPerfLogger.Log($"Native PDFium print total: {stopwatch.ElapsedMilliseconds} ms");
		}
	}

	private static PreRenderedPage PreRenderPageToBitmap(nint document, int pageIndex, int copy, int safeWidth, int safeHeight, int dpiX, int dpiY, int physicalOffsetX, int physicalOffsetY, bool fitToPrintableArea, bool autoCenter, bool driverAlreadyOffsetsPrintableArea, int flags, double printDpi = 600.0)
	{
		nint num = IntPtr.Zero;
		lock (PdfiumEngine.SyncRoot)
		{
			num = PdfiumEngine.FPDF_LoadPage(document, pageIndex);
		}
		if (num == IntPtr.Zero)
		{
			PdfPerfLogger.Log($"Native print page {pageIndex + 1}: FPDF_LoadPage failed.");
			return new PreRenderedPage { PageIndex = pageIndex, Copy = copy, IsRasterized = true };
		}
		try
		{
			double num2 = 0;
			double num3 = 0;
			lock (PdfiumEngine.SyncRoot)
			{
				num2 = PdfiumEngine.FPDF_GetPageWidth(num);
				num3 = PdfiumEngine.FPDF_GetPageHeight(num);
			}
			
			// 1. Calculate destination size in physical printer GDI pixels
			double physicalWidthPx = num2 / 72.0 * dpiX;
			double physicalHeightPx = num3 / 72.0 * dpiY;
			double scale = 1.0;
			if (fitToPrintableArea)
			{
				scale = Math.Min((double)safeWidth / physicalWidthPx, (double)safeHeight / physicalHeightPx);
			}
			int destWidth = Math.Max(1, (int)Math.Round(physicalWidthPx * scale));
			int destHeight = Math.Max(1, (int)Math.Round(physicalHeightPx * scale));

			// Centering draw offset in physical GDI pixels
			int drawX = (autoCenter ? ((safeWidth - destWidth) / 2) : 0);
			int drawY = (autoCenter ? ((safeHeight - destHeight) / 2) : 0);
			if (!driverAlreadyOffsetsPrintableArea)
			{
				drawX += physicalOffsetX;
				drawY += physicalOffsetY;
			}
			if (!autoCenter)
			{
				drawX = Math.Max(0, drawX);
				drawY = Math.Max(0, drawY);
			}

			// 2. Determine target rendering DPI for bitmap generation (to save memory & spool size)
			double targetDpiX = Math.Min(dpiX, printDpi);
			double targetDpiY = Math.Min(dpiY, printDpi);
			
			// Adaptive DPI: Giới hạn độ phân giải tối đa dựa trên chất lượng được chọn để tránh mất nét trên bản vẽ lớn (CAD/Revit)
			double maxPrintPixels = 16000000.0; // Mặc định 16MP cho 300 DPI
			if (printDpi >= 1200.0) maxPrintPixels = 400000000.0;      // 400MP cho Siêu nét (1200 DPI)
			else if (printDpi >= 600.0) maxPrintPixels = 100000000.0;  // 100MP cho Rất nét (600 DPI)
			else if (printDpi >= 400.0) maxPrintPixels = 36000000.0;   // 36MP cho Nét (400 DPI)

			double currentPrintPixels = (num2 / 72.0 * targetDpiX) * (num3 / 72.0 * targetDpiY);
			if (currentPrintPixels > maxPrintPixels)
			{
				double adpScale = Math.Sqrt(maxPrintPixels / currentPrintPixels);
				targetDpiX *= adpScale;
				targetDpiY *= adpScale;
				PdfPerfLogger.Log($"Native print page {pageIndex + 1} - Adaptive DPI active: scaled down resolution by {adpScale:F2}x (Target DPI: {targetDpiX:F1}x{targetDpiY:F1})");
			}

			double bmpWidth = num2 / 72.0 * targetDpiX;
			double bmpHeight = num3 / 72.0 * targetDpiY;

			if (fitToPrintableArea)
			{
				// In bitmap mode, if we fit to printable area, the bitmap only needs to be as large as the destination size at targetDpi
				double targetSafeWidth = safeWidth * (targetDpiX / dpiX);
				double targetSafeHeight = safeHeight * (targetDpiY / dpiY);
				double bmpScale = Math.Min(targetSafeWidth / bmpWidth, targetSafeHeight / bmpHeight);
				bmpWidth *= bmpScale;
				bmpHeight *= bmpScale;
			}

			int width = Math.Max(1, (int)Math.Round(bmpWidth));
			int height = Math.Max(1, (int)Math.Round(bmpHeight));

			int stride = width * 4;
			byte[] array = new byte[stride * height];
			GCHandle gCHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
			try
			{
				nint first_scan = gCHandle.AddrOfPinnedObject();
				nint num5_bmp = IntPtr.Zero;
				lock (PdfiumEngine.SyncRoot)
				{
					num5_bmp = PdfiumEngine.FPDFBitmap_CreateEx(width, height, 4, first_scan, stride);
					if (num5_bmp != IntPtr.Zero)
					{
						PdfiumEngine.FPDFBitmap_FillRect(num5_bmp, 0, 0, width, height, uint.MaxValue);
						PdfiumEngine.FPDF_RenderPageBitmap(num5_bmp, num, 0, 0, width, height, 0, flags);
						PdfiumEngine.FPDFBitmap_Destroy(num5_bmp);
					}
				}
			}
			finally
			{
				gCHandle.Free();
			}

			// Chuyển đổi từ BGRA 32-bit (4 bytes/pixel) sang BGR 24-bit (3 bytes/pixel) để giảm 25% dung lượng spool
			int stride24 = ((width * 3 + 3) / 4) * 4; // Căn chỉnh stride 4-byte cho Windows GDI
			byte[] array24 = new byte[stride24 * height];
			for (int y = 0; y < height; y++)
			{
				int srcRowOffset = y * stride;
				int destRowOffset = y * stride24;
				for (int x = 0; x < width; x++)
				{
					int srcIndex = srcRowOffset + x * 4;
					int destIndex = destRowOffset + x * 3;
					array24[destIndex] = array[srcIndex];       // B
					array24[destIndex + 1] = array[srcIndex + 1]; // G
					array24[destIndex + 2] = array[srcIndex + 2]; // R
				}
			}

			return new PreRenderedPage
			{
				PageIndex = pageIndex,
				Copy = copy,
				IsRasterized = true,
				BitmapBuffer = array24,
				Width = width,
				Height = height,
				Stride = stride24,
				DrawX = drawX,
				DrawY = drawY,
				DestWidth = destWidth,
				DestHeight = destHeight
			};
		}
		finally
		{
			lock (PdfiumEngine.SyncRoot)
			{
				PdfiumEngine.FPDF_ClosePage(num);
			}
		}
	}

	private static PreRenderedPage PrepareVectorPage(nint document, int pageIndex, int copy)
	{
		nint num = IntPtr.Zero;
		lock (PdfiumEngine.SyncRoot)
		{
			num = PdfiumEngine.FPDF_LoadPage(document, pageIndex);
		}
		return new PreRenderedPage
		{
			PageIndex = pageIndex,
			Copy = copy,
			IsRasterized = false,
			PageHandle = num
		};
	}

	private static void SpoolRenderedPage(nint hdc, PreRenderedPage rendered, int printableWidth, int printableHeight, int safeWidth, int safeHeight, int dpiX, int dpiY, int physicalOffsetX, int physicalOffsetY, bool fitToPrintableArea, bool autoCenter, bool driverAlreadyOffsetsPrintableArea, int flags, IProgress<PrintProgressInfo>? progress, int completedPages, int totalPages, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Stopwatch stopwatch = Stopwatch.StartNew();
		progress?.Report(new PrintProgressInfo($"Dang mo trang in {rendered.PageIndex + 1}...", completedPages, totalPages));
		if (StartPage(hdc) <= 0)
		{
			throw new InvalidOperationException($"StartPage failed on page {rendered.PageIndex + 1}: {GetLastErrorMessage()}");
		}
		bool flag = true;
		try
		{
			PatBlt(hdc, 0, 0, printableWidth, printableHeight, 16711778);
			if (rendered.IsRasterized)
			{
				if (rendered.BitmapBuffer != null)
				{
					PdfPerfLogger.Log($"Native print spooling page {rendered.PageIndex + 1}, copy {rendered.Copy} (Rasterized): {rendered.Width}x{rendered.Height} stretched to {rendered.DestWidth}x{rendered.DestHeight} at {rendered.DrawX},{rendered.DrawY}");
					GCHandle gCHandle = GCHandle.Alloc(rendered.BitmapBuffer, GCHandleType.Pinned);
					try
					{
						nint first_scan = gCHandle.AddrOfPinnedObject();
						BITMAPINFO bmi = default(BITMAPINFO);
						bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
						bmi.bmiHeader.biWidth = rendered.Width;
						bmi.bmiHeader.biHeight = -rendered.Height;
						bmi.bmiHeader.biPlanes = 1;
						bmi.bmiHeader.biBitCount = 24;
						bmi.bmiHeader.biCompression = 0; // BI_RGB
						bmi.bmiHeader.biSizeImage = (uint)(rendered.Stride * rendered.Height);

						int result = StretchDIBits(
							hdc,
							rendered.DrawX,
							rendered.DrawY,
							rendered.DestWidth,
							rendered.DestHeight,
							0,
							0,
							rendered.Width,
							rendered.Height,
							first_scan,
							ref bmi,
							0, // DIB_RGB_COLORS
							13369376 // SRCCOPY
						);
						if (result == -1)
						{
							PdfPerfLogger.Log($"Native StretchDIBits failed on page {rendered.PageIndex + 1}: {GetLastErrorMessage()}");
						}
					}
					finally
					{
						gCHandle.Free();
					}
				}
			}
			else
			{
				if (rendered.PageHandle != IntPtr.Zero)
				{
					try
					{
						double num2 = 0;
						double num3 = 0;
						lock (PdfiumEngine.SyncRoot)
						{
							num2 = PdfiumEngine.FPDF_GetPageWidth(rendered.PageHandle);
							num3 = PdfiumEngine.FPDF_GetPageHeight(rendered.PageHandle);
						}
						
						double physicalWidthPx = num2 / 72.0 * dpiX;
						double physicalHeightPx = num3 / 72.0 * dpiY;
						double scale = 1.0;
						if (fitToPrintableArea)
						{
							scale = Math.Min((double)safeWidth / physicalWidthPx, (double)safeHeight / physicalHeightPx);
						}
						int destWidth = Math.Max(1, (int)Math.Round(physicalWidthPx * scale));
						int destHeight = Math.Max(1, (int)Math.Round(physicalHeightPx * scale));

						int drawX = (autoCenter ? ((safeWidth - destWidth) / 2) : 0);
						int drawY = (autoCenter ? ((safeHeight - destHeight) / 2) : 0);
						if (!driverAlreadyOffsetsPrintableArea)
						{
							drawX += physicalOffsetX;
							drawY += physicalOffsetY;
						}
						if (!autoCenter)
						{
							drawX = Math.Max(0, drawX);
							drawY = Math.Max(0, drawY);
						}

						PdfPerfLogger.Log($"Native print spooling page {rendered.PageIndex + 1}, copy {rendered.Copy} (Vector): {destWidth}x{destHeight} at {drawX},{drawY}");
						lock (PdfiumEngine.SyncRoot)
						{
							PdfiumEngine.FPDF_RenderPage(hdc, rendered.PageHandle, drawX, drawY, destWidth, destHeight, 0, flags);
						}
					}
					finally
					{
						lock (PdfiumEngine.SyncRoot)
						{
							PdfiumEngine.FPDF_ClosePage(rendered.PageHandle);
						}
					}
				}
			}

			if (EndPage(hdc) <= 0)
			{
				throw new InvalidOperationException($"EndPage failed on page {rendered.PageIndex + 1}: {GetLastErrorMessage()}");
			}
			flag = false;
		}
		finally
		{
			if (flag)
			{
				AbortDoc(hdc);
			}
		}
		stopwatch.Stop();
		PdfPerfLogger.Log($"Native spool page {rendered.PageIndex + 1} done: {stopwatch.ElapsedMilliseconds} ms");
	}
	private static nint CreatePrinterDc(string printQueueName, byte[]? devMode)
	{
		GCHandle gCHandle = default(GCHandle);
		nint num = IntPtr.Zero;
		try
		{
			if (devMode != null && devMode.Length > 0)
			{
				gCHandle = GCHandle.Alloc(devMode, GCHandleType.Pinned);
				num = gCHandle.AddrOfPinnedObject();
			}
			nint num2 = CreateDC("WINSPOOL", printQueueName, null, num);
			if (num2 == IntPtr.Zero && num != IntPtr.Zero)
			{
				PdfPerfLogger.Log("Native print: CreateDC with DevMode failed (" + GetLastErrorMessage() + "), retrying with printer defaults.");
				num2 = CreateDC("WINSPOOL", printQueueName, null, IntPtr.Zero);
			}
			return num2;
		}
		finally
		{
			if (gCHandle.IsAllocated)
			{
				gCHandle.Free();
			}
		}
	}

	private static int DipsToDevicePixels(double dips, int dpi)
	{
		return Math.Max(0, (int)Math.Round(dips / 96.0 * (double)dpi));
	}

	private static DOCINFO CreateDocInfo(string pdfPath, int? pageNumber)
	{
		string fileName = Path.GetFileName(pdfPath);
		string lpszDocName = "PDF Pro - " + fileName;
		if (pageNumber.HasValue)
		{
			lpszDocName = $"PDF Pro - p{pageNumber:00} - {fileName}";
		}
		return new DOCINFO
		{
			cbSize = Marshal.SizeOf<DOCINFO>(),
			lpszDocName = lpszDocName
		};
	}

	private static string GetLastErrorMessage()
	{
		int lastWin32Error = Marshal.GetLastWin32Error();
		if (lastWin32Error != 0)
		{
			return $"{lastWin32Error}";
		}
		return "unknown error";
	}

	[DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern nint CreateDC(string? lpszDriver, string lpszDevice, string? lpszOutput, nint lpInitData);

	[DllImport("gdi32.dll", SetLastError = true)]
	private static extern bool DeleteDC(nint hdc);

	[DllImport("gdi32.dll", SetLastError = true)]
	private static extern int GetDeviceCaps(nint hdc, int index);

	[DllImport("gdi32.dll", SetLastError = true)]
	private static extern bool PatBlt(nint hdc, int x, int y, int width, int height, int rop);

	[DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "StartDocW", SetLastError = true)]
	private static extern int StartDoc(nint hdc, [In] ref DOCINFO lpdi);

	[DllImport("gdi32.dll", SetLastError = true)]
	private static extern int EndDoc(nint hdc);

	[DllImport("gdi32.dll", SetLastError = true)]
	private static extern int AbortDoc(nint hdc);

	[DllImport("gdi32.dll", SetLastError = true)]
	private static extern int StartPage(nint hdc);

	[DllImport("gdi32.dll", SetLastError = true)]
	private static extern int EndPage(nint hdc);

	[StructLayout(LayoutKind.Sequential)]
	private struct BITMAPINFOHEADER
	{
		public uint biSize;
		public int biWidth;
		public int biHeight;
		public ushort biPlanes;
		public ushort biBitCount;
		public uint biCompression;
		public uint biSizeImage;
		public int biXPelsPerMeter;
		public int biYPelsPerMeter;
		public uint biClrUsed;
		public uint biClrImportant;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BITMAPINFO
	{
		public BITMAPINFOHEADER bmiHeader;
		public uint bmiColors;
	}

	[DllImport("gdi32.dll", SetLastError = true)]
	private static extern int StretchDIBits(
		nint hdc,
		int xDest,
		int yDest,
		int destWidth,
		int destHeight,
		int xSrc,
		int ySrc,
		int srcWidth,
		int srcHeight,
		nint lpBits,
		[In] ref BITMAPINFO lpbmi,
		uint iUsage,
		uint dwRop
	);

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private class DOC_INFO_1
	{
		[MarshalAs(UnmanagedType.LPWStr)]
		public string? pDocName;
		[MarshalAs(UnmanagedType.LPWStr)]
		public string? pOutputFile;
		[MarshalAs(UnmanagedType.LPWStr)]
		public string? pDataType;
	}

	[DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool OpenPrinter(string szPrinter, out nint hPrinter, nint pd);

	[DllImport("winspool.drv", SetLastError = true)]
	private static extern bool ClosePrinter(nint hPrinter);

	[DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool StartDocPrinter(nint hPrinter, int level, [In] DOC_INFO_1 di);

	[DllImport("winspool.drv", SetLastError = true)]
	private static extern bool EndDocPrinter(nint hPrinter);

	[DllImport("winspool.drv", SetLastError = true)]
	private static extern bool StartPagePrinter(nint hPrinter);

	[DllImport("winspool.drv", SetLastError = true)]
	private static extern bool EndPagePrinter(nint hPrinter);

	[DllImport("winspool.drv", SetLastError = true)]
	private static extern bool WritePrinter(nint hPrinter, nint pBytes, int dwCount, out int dwWritten);

	public static void PrintPdfDirect(string pdfPath, string printQueueName, string docName, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		byte[] pdfBytes = File.ReadAllBytes(pdfPath);
		
		nint hPrinter = IntPtr.Zero;
		var di = new DOC_INFO_1
		{
			pDocName = docName,
			pDataType = "RAW",
			pOutputFile = null
		};

		if (!OpenPrinter(printQueueName, out hPrinter, IntPtr.Zero))
		{
			throw new InvalidOperationException($"Cannot open printer {printQueueName}. Win32 Error: {Marshal.GetLastWin32Error()}");
		}

		try
		{
			if (!StartDocPrinter(hPrinter, 1, di))
			{
				throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
			}

			try
			{
				if (!StartPagePrinter(hPrinter))
				{
					throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
				}

				try
				{
					nint pBytes = Marshal.AllocCoTaskMem(pdfBytes.Length);
					Marshal.Copy(pdfBytes, 0, pBytes, pdfBytes.Length);
					try
					{
						int bytesWritten = 0;
						if (!WritePrinter(hPrinter, pBytes, pdfBytes.Length, out bytesWritten))
						{
							throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
						}
					}
					finally
					{
						Marshal.FreeCoTaskMem(pBytes);
					}
				}
				finally
				{
					EndPagePrinter(hPrinter);
				}
			}
			finally
			{
				EndDocPrinter(hPrinter);
			}
		}
		finally
		{
			ClosePrinter(hPrinter);
		}
	}

	public static void PrintPdfDirectOptimized(string pdfPath, string printQueueName, string docName, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		
		nint hPrinter = IntPtr.Zero;
		var di = new DOC_INFO_1
		{
			pDocName = docName,
			pDataType = "RAW",
			pOutputFile = null
		};

		if (!OpenPrinter(printQueueName, out hPrinter, IntPtr.Zero))
		{
			throw new InvalidOperationException($"Cannot open printer {printQueueName}. Win32 Error: {Marshal.GetLastWin32Error()}");
		}

		try
		{
			if (!StartDocPrinter(hPrinter, 1, di))
			{
				throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
			}

			try
			{
				if (!StartPagePrinter(hPrinter))
				{
					throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
				}

				try
				{
					using (var fs = new FileStream(pdfPath, FileMode.Open, FileAccess.Read, FileShare.Read))
					{
						byte[] buffer = new byte[65536]; // 64KB buffer
						int bytesRead;
						nint pBytes = Marshal.AllocCoTaskMem(buffer.Length);
						
						try
						{
							while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
							{
								cancellationToken.ThrowIfCancellationRequested();
								
								Marshal.Copy(buffer, 0, pBytes, bytesRead);
								int bytesWritten = 0;
								if (!WritePrinter(hPrinter, pBytes, bytesRead, out bytesWritten))
								{
									throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
								}
							}
						}
						finally
						{
							Marshal.FreeCoTaskMem(pBytes);
						}
					}
				}
				finally
				{
					EndPagePrinter(hPrinter);
				}
			}
			finally
			{
				EndDocPrinter(hPrinter);
			}
		}
		finally
		{
			ClosePrinter(hPrinter);
		}
	}
}
