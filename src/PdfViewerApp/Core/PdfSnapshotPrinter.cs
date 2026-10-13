using System;
using System.Diagnostics;
using System.Printing;
using System.Printing.Interop;
using System.Runtime.InteropServices;

namespace PdfViewerApp;

internal sealed record SnapshotPrintSettings(bool Rasterize = false, bool SeparateCopies = false,
    bool TestFrame = false, bool DriverOffsets = true);

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
        System.Threading.CancellationToken cancellationToken = default, IProgress<PrintProgressInfo>? progress = null,
        SnapshotPrintSettings? settings = null)
    {
        settings ??= new SnapshotPrintSettings();
        cancellationToken.ThrowIfCancellationRequested();
        nint document = IntPtr.Zero, page = IntPtr.Zero, dc = IntPtr.Zero;
        bool jobStarted = false;
        GCHandle pin = default;
        try
        {
            int copies = Math.Clamp(printTicket.CopyCount ?? 1, 1, 999);
            progress?.Report(new PrintProgressInfo("Đang kết nối máy in…", 0, copies, true));
            var driverTicket = printTicket.Clone();
            driverTicket.CopyCount = 1;
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
            var content = layout.ContentAreaMm;
            if (content.IsEmpty) throw new InvalidOperationException("Máy in không có vùng in phù hợp với lề đã chọn.");
            int left = Math.Max(0, (int)Math.Round(content.Left * dpiX / 25.4) - offsetX);
            int top = Math.Max(0, (int)Math.Round(content.Top * dpiY / 25.4) - offsetY);
            int right = Math.Min(printableWidth, (int)Math.Round(content.Right * dpiX / 25.4) - offsetX);
            int bottom = Math.Min(printableHeight, (int)Math.Round(content.Bottom * dpiY / 25.4) - offsetY);
            if (content.IsEmpty || right <= left || bottom <= top)
                throw new InvalidOperationException("Máy in không có vùng in phù hợp với lề đã chọn.");

            BITMAPINFO info = default;
            int drawX = 0, drawY = 0, destWidth = 1, destHeight = 1;
            int renderWidth = 1, renderHeight = 1, vectorX = 0, vectorY = 0;
            int width = 1, height = 1;
            if (!settings.TestFrame)
            {
                progress?.Report(new PrintProgressInfo(settings.Rasterize ? "Đang tạo ảnh vùng Snapshot…" : "Đang chuẩn bị vùng PDF gốc…", 0, copies, true));
                lock (PdfiumEngine.SyncRoot)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    PdfiumEngine.Initialize();
                    document = PdfiumEngine.FPDF_LoadDocument(snapshot.PdfPath, null);
                    if (document == IntPtr.Zero) throw new InvalidOperationException("Không mở được PDF để in.");
                    page = PdfiumEngine.FPDF_LoadPage(document, snapshot.PageIndex);
                    if (page == IntPtr.Zero) throw new InvalidOperationException("Không đọc được trang PDF để in.");
                    double pageWidth = PdfiumEngine.FPDF_GetPageWidth(page);
                    double pageHeight = PdfiumEngine.FPDF_GetPageHeight(page);
                    var cropPoints = new System.Windows.Size(pageWidth * snapshot.Width, pageHeight * snapshot.Height);
                    var bounds = layout.ImageBoundsMm(cropPoints);
                    double scale = bounds.Width / (cropPoints.Width * 25.4 / 72);
                    destWidth = Math.Max(1, (int)Math.Round(bounds.Width * dpiX / 25.4));
                    destHeight = Math.Max(1, (int)Math.Round(bounds.Height * dpiY / 25.4));
                    drawX = (int)Math.Round(bounds.X * dpiX / 25.4) - (settings.DriverOffsets ? offsetX : 0);
                    drawY = (int)Math.Round(bounds.Y * dpiY / 25.4) - (settings.DriverOffsets ? offsetY : 0);
                    renderWidth = Math.Max(1, (int)Math.Round(pageWidth / 72 * dpiX * scale));
                    renderHeight = Math.Max(1, (int)Math.Round(pageHeight / 72 * dpiY * scale));
                    vectorX = drawX - (int)Math.Round(snapshot.X * renderWidth);
                    vectorY = drawY - (int)Math.Round(snapshot.Y * renderHeight);
                    if (settings.Rasterize)
                    {
                        double sampleWidth = pageWidth / 72 * Math.Min(dpiX, printTicket.PageResolution?.X ?? dpiX) * scale;
                        double sampleHeight = pageHeight / 72 * Math.Min(dpiY, printTicket.PageResolution?.Y ?? dpiY) * scale;
                        double limit = Math.Min(1, 40000 / Math.Max(sampleWidth, sampleHeight));
                        limit = Math.Min(limit, Math.Sqrt(47000000 / (sampleWidth * snapshot.Width * sampleHeight * snapshot.Height)));
                        int fullWidth = Math.Max(1, (int)(sampleWidth * limit));
                        int fullHeight = Math.Max(1, (int)(sampleHeight * limit));
                        int tileX = Math.Max(0, (int)Math.Floor(snapshot.X * fullWidth));
                        int tileY = Math.Max(0, (int)Math.Floor(snapshot.Y * fullHeight));
                        width = Math.Max(1, Math.Min(fullWidth, (int)Math.Ceiling((snapshot.X + snapshot.Width) * fullWidth)) - tileX);
                        height = Math.Max(1, Math.Min(fullHeight, (int)Math.Ceiling((snapshot.Y + snapshot.Height) * fullHeight)) - tileY);
                        int stride = checked((width * 3 + 3) / 4 * 4);
                        byte[] pixels = new byte[checked(stride * height)];
                        pin = GCHandle.Alloc(pixels, GCHandleType.Pinned);
                        nint bitmap = PdfiumEngine.FPDFBitmap_CreateEx(width, height, 2, pin.AddrOfPinnedObject(), stride);
                        if (bitmap == IntPtr.Zero) throw new InvalidOperationException("Không tạo được ảnh vùng in.");
                        try
                        {
                            PdfiumEngine.FPDFBitmap_FillRect(bitmap, 0, 0, width, height, uint.MaxValue);
                            PdfiumEngine.FPDF_RenderPageBitmap(bitmap, page, -tileX, -tileY, fullWidth, fullHeight, 0, 0x801);
                        }
                        finally { PdfiumEngine.FPDFBitmap_Destroy(bitmap); }
                        info.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
                        info.bmiHeader.biWidth = width;
                        info.bmiHeader.biHeight = -height;
                        info.bmiHeader.biPlanes = 1;
                        info.bmiHeader.biBitCount = 24;
                        info.bmiHeader.biSizeImage = (uint)pixels.Length;
                        PdfiumEngine.FPDF_ClosePage(page);
                        PdfiumEngine.FPDF_CloseDocument(document);
                        page = document = IntPtr.Zero;
                    }
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            PdfPerfLogger.Log($"Snapshot print: raster={settings.Rasterize}, separate={settings.SeparateCopies}, test={settings.TestFrame}, driverOffsets={settings.DriverOffsets}, copies={copies}");
            var docInfo = new DOCINFO { cbSize = Marshal.SizeOf<DOCINFO>(), lpszDocName = $"PDF Pro Snapshot - p{snapshot.PageIndex + 1}" };
            if (!settings.SeparateCopies)
            {
                if (StartDoc(dc, ref docInfo) <= 0) throw new InvalidOperationException("Không bắt đầu được lệnh in: " + GetLastErrorMessage());
                jobStarted = true;
            }
            for (int copy = 0; copy < copies; copy++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new PrintProgressInfo($"Đang gửi bản in Snapshot {copy + 1}…", copy, copies));
                if (settings.SeparateCopies)
                {
                    if (StartDoc(dc, ref docInfo) <= 0) throw new InvalidOperationException("Không bắt đầu được lệnh in: " + GetLastErrorMessage());
                    jobStarted = true;
                }
                if (StartPage(dc) <= 0) throw new InvalidOperationException("Không bắt đầu được trang in: " + GetLastErrorMessage());
                int saved = SaveDC(dc);
                if (saved == 0) throw new InvalidOperationException("Không lưu được trạng thái vùng in.");
                try
                {
                    PatBlt(dc, 0, 0, printableWidth, printableHeight, WHITENESS);
                    if (IntersectClipRect(dc, left, top, right, bottom) == 0)
                        throw new InvalidOperationException("Không thiết lập được vùng in.");
                    if (settings.TestFrame)
                    {
                        DrawTestFrame(dc, left, top, right, bottom, Math.Max(1, dpiX / 96), Math.Max(1, dpiY / 96));
                    }
                    else if (settings.Rasterize)
                    {
                        SetStretchBltMode(dc, 4);
                        int result = StretchDIBits(dc, drawX, drawY, destWidth, destHeight, 0, 0, width, height,
                            pin.AddrOfPinnedObject(), ref info, 0, 13369376);
                        if (result == 0 || result == -1) throw new InvalidOperationException("Không gửi được ảnh tới máy in: " + GetLastErrorMessage());
                    }
                    else
                    {
                        // Clip again to the selected region so adjacent PDF content cannot leak into the paper margins.
                        if (IntersectClipRect(dc, drawX, drawY, drawX + destWidth, drawY + destHeight) == 0)
                            throw new InvalidOperationException("Không thiết lập được vùng Snapshot.");
                        lock (PdfiumEngine.SyncRoot)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            PdfiumEngine.FPDF_RenderPage(dc, page, vectorX, vectorY, renderWidth, renderHeight, 0, 0x801);
                        }
                    }
                }
                finally { RestoreDC(dc, saved); }
                cancellationToken.ThrowIfCancellationRequested();
                if (EndPage(dc) <= 0) throw new InvalidOperationException("Không hoàn tất được trang in: " + GetLastErrorMessage());
                if (settings.SeparateCopies)
                {
                    if (EndDoc(dc) <= 0) throw new InvalidOperationException("Không hoàn tất được lệnh in: " + GetLastErrorMessage());
                    jobStarted = false;
                }
                progress?.Report(new PrintProgressInfo($"Đã gửi bản in Snapshot {copy + 1}.", copy + 1, copies));
            }
            if (jobStarted)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (EndDoc(dc) <= 0) throw new InvalidOperationException("Không hoàn tất được lệnh in: " + GetLastErrorMessage());
                jobStarted = false;
            }
        }
        finally
        {
            if (jobStarted && dc != IntPtr.Zero) AbortDoc(dc);
            if (dc != IntPtr.Zero) DeleteDC(dc);
            if (pin.IsAllocated) pin.Free();
            if (page != IntPtr.Zero || document != IntPtr.Zero)
            {
                lock (PdfiumEngine.SyncRoot)
                {
                    if (page != IntPtr.Zero) PdfiumEngine.FPDF_ClosePage(page);
                    if (document != IntPtr.Zero) PdfiumEngine.FPDF_CloseDocument(document);
                }
            }
        }
    }

    private static void DrawTestFrame(nint dc, int left, int top, int right, int bottom, int penX, int penY)
    {
        const int blackness = 0x42;
        if (!PatBlt(dc, left, top, right - left, penY, blackness) ||
            !PatBlt(dc, left, bottom - penY, right - left, penY, blackness) ||
            !PatBlt(dc, left, top, penX, bottom - top, blackness) ||
            !PatBlt(dc, right - penX, top, penX, bottom - top, blackness) ||
            !PatBlt(dc, (left + right) / 2, top, penX, bottom - top, blackness) ||
            !PatBlt(dc, left, (top + bottom) / 2, right - left, penY, blackness))
            throw new InvalidOperationException("Không vẽ được khung kiểm tra lề: " + GetLastErrorMessage());
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
			throw new InvalidOperationException("Không áp dụng được thiết lập máy in. Kiểm tra driver rồi thử lại.", ex);
		}
		try
		{
			if (array != null && array.Length > 0)
			{
				gCHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
				num = gCHandle.AddrOfPinnedObject();
			}
			nint num2 = CreateDC("WINSPOOL", printQueue.FullName, null, num);
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
    private static extern int SaveDC(nint hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool RestoreDC(nint hdc, int savedDc);

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
