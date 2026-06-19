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
			return new PrinterPrintProfile("Canon iX6770 profile", DriverAlreadyOffsetsPrintableArea: true, 30.0, 10.0);
		}
		return new PrinterPrintProfile("Default profile", DriverAlreadyOffsetsPrintableArea: true, 12.0, 6.0);
	}
}
