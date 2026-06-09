using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

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
		PdfPerfLogger.Log("Native PDFium print start");
		progress?.Report(new PrintProgressInfo("Dang chuan bi PDFium...", 0, 0, IsIndeterminate: true));
		PdfiumEngine.Initialize();
		lock (PdfiumEngine.SyncRoot)
		{
			cancellationToken.ThrowIfCancellationRequested();
			nint num = PdfiumEngine.FPDF_LoadDocument(pdfPath, null);
			if (num == IntPtr.Zero)
			{
				throw new InvalidOperationException("Unable to load PDF for native printing.");
			}
			nint num2 = IntPtr.Zero;
			bool flag = false;
			try
			{
				int num3 = PdfiumEngine.FPDF_GetPageCount(num);
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
				for (int i = 1; i <= copies; i++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					int num16 = ((!reversePageOrder) ? 1 : (-1));
					int num17 = (reversePageOrder ? endPageIndex : startPageIndex);
					int num18 = (reversePageOrder ? startPageIndex : endPageIndex);
					for (int j = num17; reversePageOrder ? (j >= num18) : (j <= num18); j += num16)
					{
						cancellationToken.ThrowIfCancellationRequested();
						progress?.Report(new PrintProgressInfo($"Dang render trang {j + 1}, ban {i}...", num5, num4));
						if (separatePageJobs)
						{
							DOCINFO lpdi2 = CreateDocInfo(pdfPath, j + 1);
							Stopwatch stopwatch3 = Stopwatch.StartNew();
							progress?.Report(new PrintProgressInfo($"Dang mo job trang {j + 1}...", num5, num4));
							if (StartDoc(num2, ref lpdi2) <= 0)
							{
								throw new InvalidOperationException($"StartDoc failed on page {j + 1}: {GetLastErrorMessage()}");
							}
							flag = true;
							stopwatch3.Stop();
							PdfPerfLogger.Log($"Native StartDoc page-job {j + 1}: {stopwatch3.ElapsedMilliseconds} ms");
						}
						PrintPage(num2, num, j, i, num6, num7, num14, num15, num8, num9, num10, num11, fitToPrintableArea, autoCenter, driverAlreadyOffsetsPrintableArea, flags, forceRasterize, progress, num5, num4, cancellationToken);
						if (separatePageJobs)
						{
							Stopwatch stopwatch4 = Stopwatch.StartNew();
							progress?.Report(new PrintProgressInfo($"Dang spool trang {j + 1}...", num5, num4));
							if (EndDoc(num2) <= 0)
							{
								throw new InvalidOperationException($"EndDoc failed on page {j + 1}: {GetLastErrorMessage()}");
							}
							flag = false;
							stopwatch4.Stop();
							PdfPerfLogger.Log($"Native EndDoc page-job {j + 1} spool: {stopwatch4.ElapsedMilliseconds} ms");
						}
						num5++;
						progress?.Report(new PrintProgressInfo($"Da gui trang {j + 1} ({num5}/{num4})", num5, num4));
					}
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
				PdfiumEngine.FPDF_CloseDocument(num);
				stopwatch.Stop();
				PdfPerfLogger.Log($"Native PDFium print total: {stopwatch.ElapsedMilliseconds} ms");
			}
		}
	}

	private static void PrintPage(nint hdc, nint document, int pageIndex, int copy, int printableWidth, int printableHeight, int safeWidth, int safeHeight, int dpiX, int dpiY, int physicalOffsetX, int physicalOffsetY, bool fitToPrintableArea, bool autoCenter, bool driverAlreadyOffsetsPrintableArea, int flags, bool forceRasterize, IProgress<PrintProgressInfo>? progress, int completedPages, int totalPages, CancellationToken cancellationToken)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		cancellationToken.ThrowIfCancellationRequested();
		nint num = PdfiumEngine.FPDF_LoadPage(document, pageIndex);
		if (num == IntPtr.Zero)
		{
			PdfPerfLogger.Log($"Native print page {pageIndex + 1}: skipped, FPDF_LoadPage failed.");
			return;
		}
		try
		{
			Stopwatch stopwatch2 = Stopwatch.StartNew();
			progress?.Report(new PrintProgressInfo($"Dang bat dau trang {pageIndex + 1}...", completedPages, totalPages));
			if (StartPage(hdc) <= 0)
			{
				throw new InvalidOperationException($"StartPage failed on page {pageIndex + 1}: {GetLastErrorMessage()}");
			}
			stopwatch2.Stop();
			PdfPerfLogger.Log($"Native StartPage {pageIndex + 1}: {stopwatch2.ElapsedMilliseconds} ms");
			bool flag = true;
			try
			{
				PatBlt(hdc, 0, 0, printableWidth, printableHeight, 16711778);
				double num2 = PdfiumEngine.FPDF_GetPageWidth(num);
				double num3 = PdfiumEngine.FPDF_GetPageHeight(num);
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
				PdfPerfLogger.Log($"Native print page {pageIndex + 1}, copy {copy}: pdf={num2}x{num3}pt, draw={num9},{num10},{num7}x{num8}, flags={flags}, forceRasterize={forceRasterize}");
				Stopwatch stopwatch3 = Stopwatch.StartNew();
				progress?.Report(new PrintProgressInfo($"Dang ve trang {pageIndex + 1} vao may in...", completedPages, totalPages));
				cancellationToken.ThrowIfCancellationRequested();
				if (forceRasterize)
				{
					int stride = num7 * 4;
					byte[] array = new byte[stride * num8];
					GCHandle gCHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
					try
					{
						nint first_scan = gCHandle.AddrOfPinnedObject();
						nint num5_bmp = PdfiumEngine.FPDFBitmap_CreateEx(num7, num8, 4, first_scan, stride);
						if (num5_bmp != IntPtr.Zero)
						{
							PdfiumEngine.FPDFBitmap_FillRect(num5_bmp, 0, 0, num7, num8, uint.MaxValue);
							PdfiumEngine.FPDF_RenderPageBitmap(num5_bmp, num, 0, 0, num7, num8, 0, flags);
							PdfiumEngine.FPDFBitmap_Destroy(num5_bmp);
						}
						
						BITMAPINFO bmi = default(BITMAPINFO);
						bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
						bmi.bmiHeader.biWidth = num7;
						bmi.bmiHeader.biHeight = -num8;
						bmi.bmiHeader.biPlanes = 1;
						bmi.bmiHeader.biBitCount = 32;
						bmi.bmiHeader.biCompression = 0; // BI_RGB
						bmi.bmiHeader.biSizeImage = (uint)(stride * num8);
						
						int result = StretchDIBits(
							hdc,
							num9,
							num10,
							num7,
							num8,
							0,
							0,
							num7,
							num8,
							first_scan,
							ref bmi,
							0, // DIB_RGB_COLORS
							13369376 // SRCCOPY (0xCC0020)
						);
						if (result == -1)
						{
							PdfPerfLogger.Log($"Native StretchDIBits failed on page {pageIndex + 1}: {GetLastErrorMessage()}");
						}
					}
					finally
					{
						gCHandle.Free();
					}
				}
				else
				{
					PdfiumEngine.FPDF_RenderPage(hdc, num, num9, num10, num7, num8, 0, flags);
				}
				stopwatch3.Stop();
				PdfPerfLogger.Log($"Native draw {pageIndex + 1} done: {stopwatch3.ElapsedMilliseconds} ms");
				Stopwatch stopwatch4 = Stopwatch.StartNew();
				progress?.Report(new PrintProgressInfo($"Dang gui trang {pageIndex + 1} vao spooler...", completedPages, totalPages));
				if (EndPage(hdc) <= 0)
				{
					throw new InvalidOperationException($"EndPage failed on page {pageIndex + 1}: {GetLastErrorMessage()}");
				}
				stopwatch4.Stop();
				PdfPerfLogger.Log($"Native EndPage {pageIndex + 1}: {stopwatch4.ElapsedMilliseconds} ms");
				flag = false;
			}
			finally
			{
				if (flag)
				{
					AbortDoc(hdc);
				}
			}
		}
		finally
		{
			PdfiumEngine.FPDF_ClosePage(num);
			stopwatch.Stop();
			PdfPerfLogger.Log($"Native print page {pageIndex + 1} total: {stopwatch.ElapsedMilliseconds} ms");
		}
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
