using System;
using System.Diagnostics;
using System.Printing;
using System.Printing.Interop;
using System.Runtime.InteropServices;

namespace PdfViewerApp;

internal static class PdfSnapshotPrinter
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

	private const int WHITENESS = 16711778;

	private const int PHYSICALOFFSETX = 112;

	private const int PHYSICALOFFSETY = 113;

	public static void PrintSnapshot(PdfSnapshotSelection snapshot, PrintQueue printQueue, PrintTicket printTicket, double rightSafetyPaddingDips, double bottomSafetyPaddingDips)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		PdfPerfLogger.Log("Snapshot native print start");
		PdfiumEngine.Initialize();
		lock (PdfiumEngine.SyncRoot)
		{
			nint num = PdfiumEngine.FPDF_LoadDocument(snapshot.PdfPath, null);
			if (num == IntPtr.Zero)
			{
				throw new InvalidOperationException("Unable to load PDF for snapshot printing.");
			}
			nint num2 = IntPtr.Zero;
			bool flag = false;
			try
			{
				num2 = CreatePrinterDc(printQueue, printTicket);
				if (num2 == IntPtr.Zero)
				{
					throw new InvalidOperationException("Cannot create printer DC for " + printQueue.FullName + ".");
				}
				int num3 = Math.Max(1, GetDeviceCaps(num2, HORZRES));
				int num4 = Math.Max(1, GetDeviceCaps(num2, VERTRES));
				int num5 = Math.Max(72, GetDeviceCaps(num2, LOGPIXELSX));
				int num6 = Math.Max(72, GetDeviceCaps(num2, LOGPIXELSY));
				int offsetX = Math.Max(0, GetDeviceCaps(num2, PHYSICALOFFSETX));
				int offsetY = Math.Max(0, GetDeviceCaps(num2, PHYSICALOFFSETY));
				bool isLandscape = num3 > num4;

				int num7 = Math.Max(1, num3 - DipsToDevicePixels(rightSafetyPaddingDips, num5));
				int num8 = Math.Max(1, num4 - DipsToDevicePixels(bottomSafetyPaddingDips, num6));
				PdfPerfLogger.Log($"Snapshot printer DC: printable={num3}x{num4} (landscape={isLandscape}), dpi={num5}x{num6}, offset={offsetX}x{offsetY}, safe={num7}x{num8}");
				PdfPerfLogger.Log($"Snapshot source: page={snapshot.PageIndex + 1}, rect=({snapshot.X},{snapshot.Y},{snapshot.Width},{snapshot.Height})");
				nint num9 = PdfiumEngine.FPDF_LoadPage(num, snapshot.PageIndex);
				if (num9 == IntPtr.Zero)
				{
					throw new InvalidOperationException("Unable to load snapshot page.");
				}
				try
				{
					double pagePdfWidth = PdfiumEngine.FPDF_GetPageWidth(num9);
					double pagePdfHeight = PdfiumEngine.FPDF_GetPageHeight(num9);
					
					double cropPdfWidth = Math.Max(0.001, snapshot.Width * pagePdfWidth);
					double cropPdfHeight = Math.Max(0.001, snapshot.Height * pagePdfHeight);
					
					double cropPixelWidthAtDpi = cropPdfWidth / 72.0 * (double)num5;
					double cropPixelHeightAtDpi = cropPdfHeight / 72.0 * (double)num6;
					
					// Scale để vùng snapshot vừa khít trang in (giữ tỷ lệ khung hình)
					double scale = Math.Min((double)num7 / cropPixelWidthAtDpi, (double)num8 / cropPixelHeightAtDpi);
					
					int printWidth = Math.Max(1, (int)Math.Round(cropPixelWidthAtDpi * scale));
					int printHeight = Math.Max(1, (int)Math.Round(cropPixelHeightAtDpi * scale));
					
					// Căn giữa trên trang in
					int drawX = Math.Max(0, (num7 - printWidth) / 2);
					int drawY = Math.Max(0, (num8 - printHeight) / 2);
					
					int fullWidth = Math.Max(1, (int)Math.Round((pagePdfWidth / 72.0 * (double)num5) * scale));
					int fullHeight = Math.Max(1, (int)Math.Round((pagePdfHeight / 72.0 * (double)num6) * scale));
					
					int tileX = Math.Max(0, (int)Math.Round(snapshot.X * (double)fullWidth));
					int tileY = Math.Max(0, (int)Math.Round(snapshot.Y * (double)fullHeight));
					int tileWidth = Math.Min(printWidth, fullWidth - tileX);
					int tileHeight = Math.Min(printHeight, fullHeight - tileY);

					PdfPerfLogger.Log($"Snapshot render: fullPage={fullWidth}x{fullHeight}, printArea={printWidth}x{printHeight}, tile=({tileX},{tileY},{tileWidth},{tileHeight}), draw=({drawX},{drawY})");

					// ─── FIX: In đủ số bản theo Copies ───
					int copyCount = Math.Max(1, printTicket.CopyCount ?? 1);
					for (int copyIndex = 0; copyIndex < copyCount; copyIndex++)
					{
						DOCINFO lpdi = new DOCINFO
						{
							cbSize = Marshal.SizeOf<DOCINFO>(),
							lpszDocName = $"PDF Pro Snapshot - p{snapshot.PageIndex + 1}"
						};
						Stopwatch stopwatch2 = Stopwatch.StartNew();
						if (StartDoc(num2, ref lpdi) <= 0)
						{
							throw new InvalidOperationException("Snapshot StartDoc failed: " + GetLastErrorMessage());
						}
						flag = true;
						stopwatch2.Stop();
						PdfPerfLogger.Log($"Snapshot StartDoc (copy {copyIndex + 1}/{copyCount}): {stopwatch2.ElapsedMilliseconds} ms");
						Stopwatch stopwatch3 = Stopwatch.StartNew();
						if (StartPage(num2) <= 0)
						{
							throw new InvalidOperationException("Snapshot StartPage failed: " + GetLastErrorMessage());
						}
						PatBlt(num2, 0, 0, num3, num4, WHITENESS);
						
						int stride = tileWidth * 4;
						byte[] array = new byte[stride * tileHeight];
						GCHandle gCHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
						try
						{
							nint first_scan = gCHandle.AddrOfPinnedObject();
							nint num5_bmp = PdfiumEngine.FPDFBitmap_CreateEx(tileWidth, tileHeight, 4, first_scan, stride);
							if (num5_bmp != IntPtr.Zero)
							{
								PdfiumEngine.FPDFBitmap_FillRect(num5_bmp, 0, 0, tileWidth, tileHeight, uint.MaxValue);
								PdfiumEngine.FPDF_RenderPageBitmap(num5_bmp, num9, -tileX, -tileY, fullWidth, fullHeight, 0, 2049);
								PdfiumEngine.FPDFBitmap_Destroy(num5_bmp);
							}
						}
						finally
						{
							gCHandle.Free();
						}
						
						int stride24 = ((tileWidth * 3 + 3) / 4) * 4;
						byte[] array24 = new byte[stride24 * tileHeight];
						for (int y = 0; y < tileHeight; y++)
						{
							int srcRowOffset = y * stride;
							int destRowOffset = y * stride24;
							for (int x = 0; x < tileWidth; x++)
							{
								int srcIndex = srcRowOffset + x * 4;
								int destIndex = destRowOffset + x * 3;
								array24[destIndex] = array[srcIndex];       // B
								array24[destIndex + 1] = array[srcIndex + 1]; // G
								array24[destIndex + 2] = array[srcIndex + 2]; // R
							}
						}
						
						GCHandle gCHandle24 = GCHandle.Alloc(array24, GCHandleType.Pinned);
						try
						{
							nint first_scan24 = gCHandle24.AddrOfPinnedObject();
							BITMAPINFO bmi = default(BITMAPINFO);
							bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
							bmi.bmiHeader.biWidth = tileWidth;
							bmi.bmiHeader.biHeight = -tileHeight;
							bmi.bmiHeader.biPlanes = 1;
							bmi.bmiHeader.biBitCount = 24;
							bmi.bmiHeader.biCompression = 0; // BI_RGB
							bmi.bmiHeader.biSizeImage = (uint)(stride24 * tileHeight);

							int result = StretchDIBits(
								num2,
								drawX,
								drawY,
								printWidth,  // Dùng printWidth thay vì tileWidth để kéo giãn vừa đúng trang in
								printHeight, // Dùng printHeight thay vì tileHeight để kéo giãn vừa đúng trang in
								0,
								0,
								tileWidth,
								tileHeight,
								first_scan24,
								ref bmi,
								0, // DIB_RGB_COLORS
								13369376 // SRCCOPY
							);
							if (result == -1)
							{
								PdfPerfLogger.Log($"Snapshot StretchDIBits failed: {GetLastErrorMessage()}");
							}
						}
						finally
						{
							gCHandle24.Free();
						}
						
						if (EndPage(num2) <= 0)
						{
							throw new InvalidOperationException("Snapshot EndPage failed: " + GetLastErrorMessage());
						}
						stopwatch3.Stop();
						PdfPerfLogger.Log($"Snapshot page render+EndPage (copy {copyIndex + 1}): {stopwatch3.ElapsedMilliseconds} ms");
						Stopwatch stopwatch4 = Stopwatch.StartNew();
						if (EndDoc(num2) <= 0)
						{
							throw new InvalidOperationException("Snapshot EndDoc failed: " + GetLastErrorMessage());
						}
						flag = false;
						stopwatch4.Stop();
						PdfPerfLogger.Log($"Snapshot EndDoc spool (copy {copyIndex + 1}): {stopwatch4.ElapsedMilliseconds} ms");
					} // end copy loop
				}
				finally
				{
					PdfiumEngine.FPDF_ClosePage(num9);
				}
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
				PdfPerfLogger.Log($"Snapshot native print total: {stopwatch.ElapsedMilliseconds} ms");
			}
		}
	}


	private static nint CreatePrinterDc(PrintQueue printQueue, PrintTicket printTicket)
	{
		byte[] array = null;
		GCHandle gCHandle = default(GCHandle);
		nint num = IntPtr.Zero;
		try
		{
			using PrintTicketConverter printTicketConverter = new PrintTicketConverter(printQueue.FullName, printQueue.ClientPrintSchemaVersion);
			array = printTicketConverter.ConvertPrintTicketToDevMode(printTicket, BaseDevModeType.UserDefault);
		}
		catch (Exception ex)
		{
			PdfPerfLogger.Log("Snapshot print: ConvertPrintTicketToDevMode failed, using defaults. " + ex.Message);
		}
		try
		{
			if (array != null && array.Length > 0)
			{
				gCHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
				num = gCHandle.AddrOfPinnedObject();
			}
			nint num2 = CreateDC("WINSPOOL", printQueue.FullName, null, num);
			if (num2 == IntPtr.Zero && num != IntPtr.Zero)
			{
				num2 = CreateDC("WINSPOOL", printQueue.FullName, null, IntPtr.Zero);
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

	private static string GetLastErrorMessage()
	{
		int lastWin32Error = Marshal.GetLastWin32Error();
		if (lastWin32Error != 0)
		{
			return $"{lastWin32Error}";
		}
		return "unknown error";
	}

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
}
