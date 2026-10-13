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

	public static void PrintSnapshot(PdfSnapshotSelection snapshot, PrintQueue printQueue, PrintTicket printTicket,
		double rightSafetyPaddingDips, double bottomSafetyPaddingDips, SnapshotPaperLayout layout,
		System.Threading.CancellationToken cancellationToken = default, IProgress<PrintProgressInfo>? progress = null)
	{
		cancellationToken.ThrowIfCancellationRequested();
		PdfiumEngine.Initialize();
		nint document = IntPtr.Zero, page = IntPtr.Zero, dc = IntPtr.Zero;
		bool jobStarted = false;
		bool ownsEngine = false;
		GCHandle pin = default;
		try
		{
			int copies = Math.Clamp(printTicket.CopyCount ?? 1, 1, 999);
			progress?.Report(new PrintProgressInfo("Đang kết nối máy in…", 0, copies, true));
			var driverTicket = printTicket.Clone();
			driverTicket.CopyCount = 1; // Copies are submitted by this loop, not duplicated by the driver.
			dc = CreatePrinterDc(printQueue, driverTicket);
			if (dc == IntPtr.Zero) throw new InvalidOperationException("Không kết nối được máy in.");
			int dpiX = Math.Max(72, GetDeviceCaps(dc, LOGPIXELSX));
			int dpiY = Math.Max(72, GetDeviceCaps(dc, LOGPIXELSY));
			int offsetX = Math.Max(0, GetDeviceCaps(dc, PHYSICALOFFSETX));
			int offsetY = Math.Max(0, GetDeviceCaps(dc, PHYSICALOFFSETY));
			int printableWidth = Math.Max(1, GetDeviceCaps(dc, HORZRES));
			int printableHeight = Math.Max(1, GetDeviceCaps(dc, VERTRES));
			var paper = layout.PaperSizeMm;
			double actualWidthMm = GetDeviceCaps(dc, 110) * 25.4 / dpiX;
			double actualHeightMm = GetDeviceCaps(dc, 111) * 25.4 / dpiY;
			if (Math.Abs(actualWidthMm - paper.Width) > 3 || Math.Abs(actualHeightMm - paper.Height) > 3)
				throw new InvalidOperationException($"Máy in không áp dụng khổ {layout.PaperKey} hoặc hướng giấy đã chọn. Hãy chọn khổ được máy in hỗ trợ.");
			layout = layout with { PrintableAreaMm = new System.Windows.Rect(offsetX * 25.4 / dpiX, offsetY * 25.4 / dpiY,
				Math.Max(1, printableWidth - DipsToDevicePixels(rightSafetyPaddingDips, dpiX)) * 25.4 / dpiX,
				Math.Max(1, printableHeight - DipsToDevicePixels(bottomSafetyPaddingDips, dpiY)) * 25.4 / dpiY) };
			var contentArea = layout.ContentAreaMm;
			int clipLeft = Math.Max(0, (int)Math.Round(contentArea.Left * dpiX / 25.4) - offsetX);
			int clipTop = Math.Max(0, (int)Math.Round(contentArea.Top * dpiY / 25.4) - offsetY);
			int clipRight = Math.Min(printableWidth - DipsToDevicePixels(rightSafetyPaddingDips, dpiX),
				(int)Math.Round(contentArea.Right * dpiX / 25.4) - offsetX);
			int clipBottom = Math.Min(printableHeight - DipsToDevicePixels(bottomSafetyPaddingDips, dpiY),
				(int)Math.Round(contentArea.Bottom * dpiY / 25.4) - offsetY);
			if (clipRight <= clipLeft || clipBottom <= clipTop)
				throw new InvalidOperationException("Máy in không có vùng in phù hợp với lề đã chọn.");
			double pageWidth, pageHeight;
			cancellationToken.ThrowIfCancellationRequested();
			progress?.Report(new PrintProgressInfo("Đang tạo ảnh vùng Snapshot…", 0, copies, true));
			System.Threading.Monitor.Enter(PdfiumEngine.SyncRoot);
			ownsEngine = true;
			lock (PdfiumEngine.SyncRoot)
			{
				document = PdfiumEngine.FPDF_LoadDocument(snapshot.PdfPath, null);
				if (document == IntPtr.Zero) throw new InvalidOperationException("Không mở được PDF để in.");
				page = PdfiumEngine.FPDF_LoadPage(document, snapshot.PageIndex);
				if (page == IntPtr.Zero) throw new InvalidOperationException("Không đọc được trang PDF để in.");
				pageWidth = PdfiumEngine.FPDF_GetPageWidth(page);
				pageHeight = PdfiumEngine.FPDF_GetPageHeight(page);
			}
			var cropPoints = new System.Windows.Size(pageWidth * snapshot.Width, pageHeight * snapshot.Height);
			var bounds = layout.ImageBoundsMm(cropPoints);
			double scale = bounds.Width / (cropPoints.Width * 25.4 / 72);
			int destWidth = Math.Max(1, (int)Math.Round(bounds.Width * dpiX / 25.4));
			int destHeight = Math.Max(1, (int)Math.Round(bounds.Height * dpiY / 25.4));
			int drawX = (int)Math.Round(bounds.X * dpiX / 25.4) - offsetX;
			int drawY = (int)Math.Round(bounds.Y * dpiY / 25.4) - offsetY;
			double sampleWidth = pageWidth / 72 * Math.Min(dpiX, printTicket.PageResolution?.X ?? dpiX) * scale;
			double sampleHeight = pageHeight / 72 * Math.Min(dpiY, printTicket.PageResolution?.Y ?? dpiY) * scale;
			double limit = Math.Min(1, 40000 / Math.Max(sampleWidth, sampleHeight));
			limit = Math.Min(limit, Math.Sqrt(47000000 / (sampleWidth * snapshot.Width * sampleHeight * snapshot.Height)));
			int fullWidth = Math.Max(1, (int)(sampleWidth * limit));
			int fullHeight = Math.Max(1, (int)(sampleHeight * limit));
			int tileX = Math.Max(0, (int)Math.Floor(snapshot.X * fullWidth));
			int tileY = Math.Max(0, (int)Math.Floor(snapshot.Y * fullHeight));
			int width = Math.Max(1, Math.Min(fullWidth, (int)Math.Ceiling((snapshot.X + snapshot.Width) * fullWidth)) - tileX);
			int height = Math.Max(1, Math.Min(fullHeight, (int)Math.Ceiling((snapshot.Y + snapshot.Height) * fullHeight)) - tileY);
			int stride = checked((width * 3 + 3) / 4 * 4);
			byte[] pixels = new byte[checked(stride * height)];
			pin = GCHandle.Alloc(pixels, GCHandleType.Pinned);
			lock (PdfiumEngine.SyncRoot)
			{
				nint bitmap = PdfiumEngine.FPDFBitmap_CreateEx(width, height, 2, pin.AddrOfPinnedObject(), stride);
				if (bitmap == IntPtr.Zero) throw new InvalidOperationException("Không tạo được ảnh vùng in.");
				try
				{
					PdfiumEngine.FPDFBitmap_FillRect(bitmap, 0, 0, width, height, uint.MaxValue);
					PdfiumEngine.FPDF_RenderPageBitmap(bitmap, page, -tileX, -tileY, fullWidth, fullHeight, 0, 0x801);
				}
				finally { PdfiumEngine.FPDFBitmap_Destroy(bitmap); }
			}
			PdfiumEngine.FPDF_ClosePage(page);
			PdfiumEngine.FPDF_CloseDocument(document);
			page = document = IntPtr.Zero;
			System.Threading.Monitor.Exit(PdfiumEngine.SyncRoot);
			ownsEngine = false;
			cancellationToken.ThrowIfCancellationRequested();
			PdfPerfLogger.Log($"Snapshot print: {layout.PaperKey}, scale={scale:P1}, raster={width}x{height}, copies={copies}");
			BITMAPINFO info = default;
			info.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
			info.bmiHeader.biWidth = width;
			info.bmiHeader.biHeight = -height;
			info.bmiHeader.biPlanes = 1;
			info.bmiHeader.biBitCount = 24;
			info.bmiHeader.biSizeImage = (uint)pixels.Length;
			for (int copy = 0; copy < copies; copy++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				progress?.Report(new PrintProgressInfo($"Đang gửi bản in Snapshot {copy + 1}…", copy, copies));
				var docInfo = new DOCINFO { cbSize = Marshal.SizeOf<DOCINFO>(), lpszDocName = $"PDF Pro Snapshot - p{snapshot.PageIndex + 1}" };
				if (StartDoc(dc, ref docInfo) <= 0) throw new InvalidOperationException("Không bắt đầu được lệnh in: " + GetLastErrorMessage());
				jobStarted = true;
				if (StartPage(dc) <= 0) throw new InvalidOperationException("Không bắt đầu được trang in: " + GetLastErrorMessage());
				PatBlt(dc, 0, 0, printableWidth, printableHeight, WHITENESS);
				if (IntersectClipRect(dc, clipLeft, clipTop, clipRight, clipBottom) == 0)
					throw new InvalidOperationException("Không thiết lập được vùng in.");
				SetStretchBltMode(dc, 4); // HALFTONE
				int result = StretchDIBits(dc, drawX, drawY, destWidth, destHeight, 0, 0, width, height,
					pin.AddrOfPinnedObject(), ref info, 0, 13369376);
				if (result == 0 || result == -1) throw new InvalidOperationException("Không gửi được ảnh tới máy in: " + GetLastErrorMessage());
				if (EndPage(dc) <= 0) throw new InvalidOperationException("Không hoàn tất được trang in: " + GetLastErrorMessage());
				if (EndDoc(dc) <= 0) throw new InvalidOperationException("Không hoàn tất được lệnh in: " + GetLastErrorMessage());
				jobStarted = false;
				progress?.Report(new PrintProgressInfo($"Đã gửi bản in Snapshot {copy + 1}.", copy + 1, copies));
			}
		}
		finally
		{
			if (jobStarted && dc != IntPtr.Zero) AbortDoc(dc);
			if (dc != IntPtr.Zero) DeleteDC(dc);
			if (pin.IsAllocated) pin.Free();
			try
			{
				lock (PdfiumEngine.SyncRoot)
				{
					if (page != IntPtr.Zero) PdfiumEngine.FPDF_ClosePage(page);
					if (document != IntPtr.Zero) PdfiumEngine.FPDF_CloseDocument(document);
				}
			}
			finally { if (ownsEngine) System.Threading.Monitor.Exit(PdfiumEngine.SyncRoot); }
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
	private static extern int IntersectClipRect(nint hdc, int left, int top, int right, int bottom);

	[DllImport("gdi32.dll", SetLastError = true)]
	private static extern int SetStretchBltMode(nint hdc, int mode);

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
