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
				int num3 = Math.Max(1, GetDeviceCaps(num2, 8));
				int num4 = Math.Max(1, GetDeviceCaps(num2, 10));
				int num5 = Math.Max(72, GetDeviceCaps(num2, 88));
				int num6 = Math.Max(72, GetDeviceCaps(num2, 90));
				int num7 = Math.Max(1, num3 - DipsToDevicePixels(rightSafetyPaddingDips, num5));
				int num8 = Math.Max(1, num4 - DipsToDevicePixels(bottomSafetyPaddingDips, num6));
				PdfPerfLogger.Log($"Snapshot printer DC: printable={num3}x{num4}, dpi={num5}x{num6}, safe={num7}x{num8}");
				PdfPerfLogger.Log($"Snapshot source: page={snapshot.PageIndex + 1}, rect=({snapshot.X},{snapshot.Y},{snapshot.Width},{snapshot.Height})");
				nint num9 = PdfiumEngine.FPDF_LoadPage(num, snapshot.PageIndex);
				if (num9 == IntPtr.Zero)
				{
					throw new InvalidOperationException("Unable to load snapshot page.");
				}
				try
				{
					double num10 = PdfiumEngine.FPDF_GetPageWidth(num9);
					double num11 = PdfiumEngine.FPDF_GetPageHeight(num9);
					double num12 = num10 / 72.0 * (double)num5;
					double num13 = num11 / 72.0 * (double)num6;
					double num14 = Math.Max(1.0, num12 * snapshot.Width);
					double num15 = Math.Max(1.0, num13 * snapshot.Height);
					double num16 = Math.Min((double)num7 / num14, (double)num8 / num15);
					int num17 = Math.Max(1, (int)Math.Round(num12 * num16));
					int num18 = Math.Max(1, (int)Math.Round(num13 * num16));
					int num19 = (int)Math.Round(snapshot.X * (double)num17);
					int num20 = (int)Math.Round(snapshot.Y * (double)num18);
					int num21 = (int)Math.Round(snapshot.Width * (double)num17);
					int num22 = (int)Math.Round(snapshot.Height * (double)num18);
					int num23 = Math.Max(0, (num7 - num21) / 2);
					int num24 = Math.Max(0, (num8 - num22) / 2);
					int num25 = num23 - num19;
					int num26 = num24 - num20;
					PdfPerfLogger.Log($"Snapshot render: fullPage={num17}x{num18}, cropTarget={num21}x{num22}, start={num25},{num26}, scale={num16}");
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
					PdfPerfLogger.Log($"Snapshot StartDoc: {stopwatch2.ElapsedMilliseconds} ms");
					Stopwatch stopwatch3 = Stopwatch.StartNew();
					if (StartPage(num2) <= 0)
					{
						throw new InvalidOperationException("Snapshot StartPage failed: " + GetLastErrorMessage());
					}
					PatBlt(num2, 0, 0, num3, num4, 16711778);
					PdfiumEngine.FPDF_RenderPage(num2, num9, num25, num26, num17, num18, 0, 2049);
					if (EndPage(num2) <= 0)
					{
						throw new InvalidOperationException("Snapshot EndPage failed: " + GetLastErrorMessage());
					}
					stopwatch3.Stop();
					PdfPerfLogger.Log($"Snapshot page render+EndPage: {stopwatch3.ElapsedMilliseconds} ms");
					Stopwatch stopwatch4 = Stopwatch.StartNew();
					if (EndDoc(num2) <= 0)
					{
						throw new InvalidOperationException("Snapshot EndDoc failed: " + GetLastErrorMessage());
					}
					flag = false;
					stopwatch4.Stop();
					PdfPerfLogger.Log($"Snapshot EndDoc spool: {stopwatch4.ElapsedMilliseconds} ms");
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
			array = new PrintTicketConverter(printQueue.FullName, PrintTicketConverter.MaxPrintSchemaVersion).ConvertPrintTicketToDevMode(printTicket, BaseDevModeType.UserDefault);
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
