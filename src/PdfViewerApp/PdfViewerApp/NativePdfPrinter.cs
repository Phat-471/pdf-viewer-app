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

	public static void Print(string pdfPath, string printQueueName, byte[]? devMode, int startPageIndex, int endPageIndex, int copies, bool fitToPrintableArea, bool autoCenter, bool driverAlreadyOffsetsPrintableArea, double rightSafetyPaddingDips, double bottomSafetyPaddingDips, bool separatePageJobs, bool reversePageOrder, bool forceRasterize, IProgress<PrintProgressInfo>? progress = null, CancellationToken cancellationToken = default(CancellationToken))
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
			int flags = 2049;
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
								rendered = PreRenderPageToBitmap(num, job.pageIndex, job.copy, num14, num15, num8, num9, num10, num11, fitToPrintableArea, autoCenter, driverAlreadyOffsetsPrintableArea, flags);
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
			// Advanced Print Spooling Status Monitoring
			try
			{
				progress?.Report(new PrintProgressInfo("Dang theo doi trang thai may in...", num4, num4, IsIndeterminate: true));
				using (var localServer = new System.Printing.LocalPrintServer())
				{
					using (var queue = localServer.GetPrintQueue(printQueueName))
					{
						queue.Refresh();
						int watchTimeoutMs = 15000;
						int elapsedMs = 0;
						int pollIntervalMs = 500;

						while (elapsedMs < watchTimeoutMs && !cancellationToken.IsCancellationRequested)
						{
							queue.Refresh();
							var jobs = queue.GetPrintJobInfoCollection();
							System.Printing.PrintSystemJobInfo? activeJob = null;
							
							// Duyệt qua danh sách để tìm job tương ứng
							foreach (System.Printing.PrintSystemJobInfo job in jobs)
							{
								if (job.Name.Contains(Path.GetFileName(pdfPath)) || job.Name.Contains("PDF Pro"))
								{
									activeJob = job;
									break;
								}
							}

							if (activeJob == null)
							{
								// Nếu không tìm thấy job trong hàng đợi nữa, có nghĩa là đã in thành công xong
								break;
							}

							// Nhận trạng thái chi tiết của job in
							string statusMsg = "Dang truyen lenh in...";
							var status = activeJob.JobStatus;
							
							// Lấy số trang bằng Reflection để tương thích tối đa với WPF Target Framework
							int pagesPrinted = 0;
							try
							{
								var prop = activeJob.GetType().GetProperty("PagesPrinted");
								if (prop != null)
								{
									pagesPrinted = (int)(prop.GetValue(activeJob) ?? 0);
								}
							}
							catch {}

							if ((status & System.Printing.PrintJobStatus.Printing) != 0)
							{
								statusMsg = $"May in dang in (Trang {pagesPrinted}/{num4})...";
							}
							else if ((status & System.Printing.PrintJobStatus.Spooling) != 0)
							{
								statusMsg = $"Spooler dang chuan bi du lieu ({pagesPrinted}/{num4})...";
							}
							else if ((status & System.Printing.PrintJobStatus.Error) != 0 || 
									 (status & System.Printing.PrintJobStatus.PaperOut) != 0)
							{
								statusMsg = $"Loi may in: {status}. Vui long kiem tra giay/muc.";
							}
							else if ((status & System.Printing.PrintJobStatus.Paused) != 0)
							{
								statusMsg = "Lenh in bi tam dung.";
							}

							progress?.Report(new PrintProgressInfo(statusMsg, pagesPrinted, num4, IsIndeterminate: false));
							Thread.Sleep(pollIntervalMs);
							elapsedMs += pollIntervalMs;
						}
					}
				}
			}
			catch (Exception ex)
			{
				PdfPerfLogger.Log($"Warning: Spooler status monitor skipped: {ex.Message}");
			}

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

	private static PreRenderedPage PreRenderPageToBitmap(nint document, int pageIndex, int copy, int safeWidth, int safeHeight, int dpiX, int dpiY, int physicalOffsetX, int physicalOffsetY, bool fitToPrintableArea, bool autoCenter, bool driverAlreadyOffsetsPrintableArea, int flags)
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
			double num4 = num2 / 72.0 * (double)dpiX;
			double num5 = num3 / 72.0 * (double)dpiY;
			if (fitToPrintableArea)
			{
				double num6 = Math.Min((double)safeWidth / num4, (double)safeHeight / num5);
				num4 *= num6;
				num5 *= num6;
			}
			int num7 = Math.Max(1, (int)Math.Round(num4));
			int num8 = Math.Max(1, (int)Math.Round(num5));
			int num9 = (autoCenter ? ((safeWidth - num7) / 2) : 0);
			int num10 = (autoCenter ? ((safeHeight - num8) / 2) : 0);
			if (!driverAlreadyOffsetsPrintableArea)
			{
				num9 += physicalOffsetX;
				num10 += physicalOffsetY;
			}
			num9 = Math.Max(0, num9);
			num10 = Math.Max(0, num10);

			int stride = num7 * 4;
			byte[] array = new byte[stride * num8];
			GCHandle gCHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
			try
			{
				nint first_scan = gCHandle.AddrOfPinnedObject();
				nint num5_bmp = IntPtr.Zero;
				lock (PdfiumEngine.SyncRoot)
				{
					num5_bmp = PdfiumEngine.FPDFBitmap_CreateEx(num7, num8, 4, first_scan, stride);
					if (num5_bmp != IntPtr.Zero)
					{
						PdfiumEngine.FPDFBitmap_FillRect(num5_bmp, 0, 0, num7, num8, uint.MaxValue);
						PdfiumEngine.FPDF_RenderPageBitmap(num5_bmp, num, 0, 0, num7, num8, 0, flags);
						PdfiumEngine.FPDFBitmap_Destroy(num5_bmp);
					}
				}
			}
			finally
			{
				gCHandle.Free();
			}

			return new PreRenderedPage
			{
				PageIndex = pageIndex,
				Copy = copy,
				IsRasterized = true,
				BitmapBuffer = array,
				Width = num7,
				Height = num8,
				Stride = stride,
				DrawX = num9,
				DrawY = num10
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
					PdfPerfLogger.Log($"Native print spooling page {rendered.PageIndex + 1}, copy {rendered.Copy} (Rasterized): {rendered.Width}x{rendered.Height} at {rendered.DrawX},{rendered.DrawY}");
					GCHandle gCHandle = GCHandle.Alloc(rendered.BitmapBuffer, GCHandleType.Pinned);
					try
					{
						nint first_scan = gCHandle.AddrOfPinnedObject();
						BITMAPINFO bmi = default(BITMAPINFO);
						bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
						bmi.bmiHeader.biWidth = rendered.Width;
						bmi.bmiHeader.biHeight = -rendered.Height;
						bmi.bmiHeader.biPlanes = 1;
						bmi.bmiHeader.biBitCount = 32;
						bmi.bmiHeader.biCompression = 0; // BI_RGB
						bmi.bmiHeader.biSizeImage = (uint)(rendered.Stride * rendered.Height);

						int result = StretchDIBits(
							hdc,
							rendered.DrawX,
							rendered.DrawY,
							rendered.Width,
							rendered.Height,
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
						double num4 = num2 / 72.0 * (double)dpiX;
						double num5 = num3 / 72.0 * (double)dpiY;
						if (fitToPrintableArea)
						{
							double num6 = Math.Min((double)safeWidth / num4, (double)safeHeight / num5);
							num4 *= num6;
							num5 *= num6;
						}
						int num7 = Math.Max(1, (int)Math.Round(num4));
						int num8 = Math.Max(1, (int)Math.Round(num5));
						int num9 = (autoCenter ? ((safeWidth - num7) / 2) : 0);
						int num10 = (autoCenter ? ((safeHeight - num8) / 2) : 0);
						if (!driverAlreadyOffsetsPrintableArea)
						{
							num9 += physicalOffsetX;
							num10 += physicalOffsetY;
						}
						num9 = Math.Max(0, num9);
						num10 = Math.Max(0, num10);

						PdfPerfLogger.Log($"Native print spooling page {rendered.PageIndex + 1}, copy {rendered.Copy} (Vector): {num7}x{num8} at {num9},{num10}");
						lock (PdfiumEngine.SyncRoot)
						{
							PdfiumEngine.FPDF_RenderPage(hdc, rendered.PageHandle, num9, num10, num7, num8, 0, flags);
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
}
