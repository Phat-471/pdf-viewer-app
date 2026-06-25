using System.Printing;

namespace PdfViewerApp;

internal sealed record PrinterPrintProfile(string Name, bool DriverAlreadyOffsetsPrintableArea, double BottomSafetyPadding, double RightSafetyPadding)
{
	public static PrinterPrintProfile Resolve(PrintQueue? queue)
	{
		string text = (queue?.FullName ?? string.Empty).ToLowerInvariant();
		string text2 = (queue?.QueueDriver?.Name ?? string.Empty).ToLowerInvariant();
		string text3 = text + " " + text2;
		if (text3.Contains("ix6770") || text3.Contains("ix6700"))
		{
			// Driver Canon iX6770 đã tự tính HORZRES/VERTRES là vùng in thực tế (sau margin)
			// nên không cần padding thêm, tránh nội dung bị cắt/mất mép
			return new PrinterPrintProfile("Canon iX6770 profile", DriverAlreadyOffsetsPrintableArea: true, 0.0, 0.0);
		}
		// Hầu hết driver hiện đại đã xử lý margin nội bộ, HORZRES/VERTRES là vùng in thực
		return new PrinterPrintProfile("Default profile", DriverAlreadyOffsetsPrintableArea: true, 0.0, 0.0);
	}
}
