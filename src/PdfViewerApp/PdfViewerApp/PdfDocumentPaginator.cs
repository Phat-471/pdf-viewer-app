using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PdfViewerApp;

public class PdfDocumentPaginator : DocumentPaginator
{
	private readonly nint _document;

	private readonly int _originalPageCount;

	private Size _pageSize;

	private Rect _imageableArea = Rect.Empty;

	private const int PrintTileSize = 2048;

	private const long TiledPrintPixelThreshold = 32000000L;

	public int StartPage { get; set; }

	public int EndPage { get; set; } = -1;

	public bool ReversePageOrder { get; set; }

	public double PrintDpi { get; set; } = 400.0;

	public long PrintMaxPixels { get; set; } = 500000000L;

	public bool AutoCenter { get; set; } = true;

	public bool FitToPrintableArea { get; set; } = true;

	public bool PrintTestFrame { get; set; }

	public IProgress<PrintProgressInfo>? PrintProgress { get; set; }

	public List<PdfAnnotation> Annotations { get; } = new List<PdfAnnotation>();

	public bool DriverAlreadyOffsetsPrintableArea { get; set; } = true;

	public double BottomSafetyPadding { get; set; } = 12.0;

	public double RightSafetyPadding { get; set; } = 6.0;

	public Rect ImageableArea
	{
		get
		{
			return _imageableArea;
		}
		set
		{
			_imageableArea = value;
		}
	}

	public override bool IsPageCountValid => true;

	public override int PageCount
	{
		get
		{
			if (StartPage > EndPage || StartPage < 0 || EndPage >= _originalPageCount)
			{
				return 0;
			}
			return EndPage - StartPage + 1;
		}
	}

	public override Size PageSize
	{
		get
		{
			return _pageSize;
		}
		set
		{
			_pageSize = value;
		}
	}

	public override IDocumentPaginatorSource Source => null;

	public PdfDocumentPaginator(string pdfPath)
	{
		PdfiumEngine.Initialize();
		_document = PdfiumEngine.FPDF_LoadDocument(pdfPath, null);
		if (_document == IntPtr.Zero)
		{
			throw new Exception("Unable to load document for printing.");
		}
		_originalPageCount = PdfiumEngine.FPDF_GetPageCount(_document);
		if (_originalPageCount > 0)
		{
			nint num = PdfiumEngine.FPDF_LoadPage(_document, 0);
			if (num != IntPtr.Zero)
			{
				double num2 = PdfiumEngine.FPDF_GetPageWidth(num);
				double num3 = PdfiumEngine.FPDF_GetPageHeight(num);
				_pageSize = new Size(num2 / 72.0 * 96.0, num3 / 72.0 * 96.0);
				PdfiumEngine.FPDF_ClosePage(num);
			}
			EndPage = _originalPageCount - 1;
		}
	}

	public override DocumentPage GetPage(int pageNumber)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		int actualPageNumber = (ReversePageOrder ? (EndPage - pageNumber) : (StartPage + pageNumber));
		PrintProgress?.Report(new PrintProgressInfo($"Dang render trang {actualPageNumber + 1} bang WPF Bitmap...", pageNumber, PageCount));
		if (actualPageNumber < 0 || actualPageNumber >= _originalPageCount)
		{
			throw new ArgumentOutOfRangeException("pageNumber");
		}
		nint num = PdfiumEngine.FPDF_LoadPage(_document, actualPageNumber);
		if (num == IntPtr.Zero)
		{
			return new DocumentPage(new DrawingVisual());
		}
		try
		{
			double num2 = PdfiumEngine.FPDF_GetPageWidth(num);
			double num3 = PdfiumEngine.FPDF_GetPageHeight(num);
			double num4 = num2 / 72.0 * 96.0;
			double num5 = num3 / 72.0 * 96.0;
			double num6 = ((_pageSize.Width > 0.0) ? _pageSize.Width : num4);
			double num7 = ((_pageSize.Height > 0.0) ? _pageSize.Height : num5);
			double num8 = ((!_imageableArea.IsEmpty) ? _imageableArea.X : 0.0);
			double num9 = ((!_imageableArea.IsEmpty) ? _imageableArea.Y : 0.0);
			double num10 = ((!_imageableArea.IsEmpty && _imageableArea.Width > 0.0) ? _imageableArea.Width : num6);
			double num11 = ((!_imageableArea.IsEmpty && _imageableArea.Height > 0.0) ? _imageableArea.Height : num7);
			double num12 = Math.Max(1.0, num10 - RightSafetyPadding);
			double num13 = Math.Max(1.0, num11 - BottomSafetyPadding);
			double num14 = (FitToPrintableArea ? num12 : num6);
			double num15 = Math.Min(val2: (FitToPrintableArea ? num13 : num7) / num5, val1: num14 / num4);
			double num16 = num4 * num15;
			double num17 = num5 * num15;
			Size pageSize = new Size(num6, num7);
			Rect rect = new Rect(0.0, 0.0, num6, num7);
			if (PrintTestFrame)
			{
				PdfPerfLogger.Log($"Printing test frame for page {actualPageNumber + 1}");
				DrawingVisual drawingVisual = new DrawingVisual();
				using (DrawingContext dc = drawingVisual.RenderOpen())
				{
					DrawTestFrame(dc, pageSize, num8, num9, num10, num11, num12, num13);
				}
				stopwatch.Stop();
				PdfPerfLogger.Log($"Print test frame page {actualPageNumber + 1} total: {stopwatch.ElapsedMilliseconds} ms");
				PrintProgress?.Report(new PrintProgressInfo($"Da render trang test {actualPageNumber + 1}", pageNumber + 1, PageCount));
				return new DocumentPage(drawingVisual, pageSize, rect, rect);
			}
			double num18;
			double num19;
			if (DriverAlreadyOffsetsPrintableArea)
			{
				num18 = (num12 - num16) / 2.0;
				num19 = (num13 - num17) / 2.0;
			}
			else
			{
				num18 = num8 + (num12 - num16) / 2.0;
				num19 = num9 + (num13 - num17) / 2.0;
			}
			double num20 = PrintDpi / 96.0 * num15;
			int num21 = Math.Max(1, (int)(num4 * num20));
			int num22 = Math.Max(1, (int)(num5 * num20));
			PdfPerfLogger.Log($"--- ĐANG KẾT XUẤT TRANG IN {actualPageNumber + 1} ---");
			PdfPerfLogger.Log($"Kích thước PDF gốc (Points): {num2}x{num3}");
			PdfPerfLogger.Log($"Kích thước quy đổi WPF (Source): {num4}x{num5}");
			PdfPerfLogger.Log($"Khổ giấy đích (Target Paper): {num6}x{num7}");
			PdfPerfLogger.Log($"Vùng in được của giấy: X={num8}, Y={num9}, Rộng={num10}, Cao={num11}");
			PdfPerfLogger.Log($"Cấu hình máy in: DriverAlreadyOffsetsPrintableArea={DriverAlreadyOffsetsPrintableArea}, RightSafetyPadding={RightSafetyPadding}, BottomSafetyPadding={BottomSafetyPadding}");
			PdfPerfLogger.Log($"Tỉ lệ thu phóng áp dụng (Scale): {num15} (DPI={PrintDpi})");
			PdfPerfLogger.Log($"Kích thước bản vẽ sau Zoom: {num16}x{num17}");
			PdfPerfLogger.Log($"Tọa độ vẽ bù gốc in (Draw Offset): X={num18}, Y={num19}");
			PdfPerfLogger.Log($"Độ phân giải Bitmap kết xuất: {num21}x{num22} pixels");
			bool flag = (long)num21 * (long)num22 > 32000000;
			PdfPerfLogger.Log($"Tiled bitmap print: {flag}");
			DrawingVisual drawingVisual2 = new DrawingVisual();
			using (DrawingContext drawingContext = drawingVisual2.RenderOpen())
			{
				drawingContext.DrawRectangle(Brushes.White, null, rect);
				if (flag)
				{
					RenderOptions.SetBitmapScalingMode(drawingVisual2, BitmapScalingMode.HighQuality);
					DrawPdfTiles(drawingContext, actualPageNumber, num21, num22, num18, num19, num16, num17);
				}
				else
				{
					BitmapSource bitmapSource = PdfiumEngine.RenderPageToBitmap(_document, actualPageNumber, num21, num22, PrintMaxPixels);
					if (bitmapSource != null)
					{
						RenderOptions.SetBitmapScalingMode(drawingVisual2, BitmapScalingMode.HighQuality);
						drawingContext.DrawImage(bitmapSource, new Rect(num18, num19, num16, num17));
					}
				}
				foreach (PdfAnnotation item in Annotations.Where((PdfAnnotation a) => a.PageIndex == actualPageNumber).ToList())
				{
					if (item is PdfTextBoxAnnotation pdfTextBoxAnnotation)
					{
						double num23 = pdfTextBoxAnnotation.Width * num16;
						double num24 = pdfTextBoxAnnotation.Height * num17;
						double num25 = num18 + pdfTextBoxAnnotation.X * num16;
						double num26 = num19 + pdfTextBoxAnnotation.Y * num17;
						if (item is PdfCalloutAnnotation pdfCalloutAnnotation)
						{
							Point point = new Point(num18 + pdfCalloutAnnotation.ArrowX * num16, num19 + pdfCalloutAnnotation.ArrowY * num17);
							new Point(num25 + num23 / 2.0, num26 + num24 / 2.0);
							Point point2 = FindBoxIntersection(point, new Rect(num25, num26, num23, num24));
							Brush brush = new SolidColorBrush(pdfCalloutAnnotation.StrokeColor);
							Pen pen = new Pen(brush, 2.0);
							drawingContext.DrawLine(pen, point, point2);
							DrawArrowHeadVector(drawingContext, point, point2, brush);
						}
						Brush brush2 = ((pdfTextBoxAnnotation.BgColor == Colors.Transparent) ? Brushes.Transparent : new SolidColorBrush(pdfTextBoxAnnotation.BgColor));
						Pen pen2 = new Pen(new SolidColorBrush(pdfTextBoxAnnotation.StrokeColor), 1.5);
						drawingContext.DrawRectangle(brush2, pen2, new Rect(num25, num26, num23, num24));
						Typeface typeface = new Typeface(new FontFamily(pdfTextBoxAnnotation.FontFamily), pdfTextBoxAnnotation.IsItalic ? FontStyles.Italic : FontStyles.Normal, pdfTextBoxAnnotation.IsBold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);
						FormattedText formattedText = new FormattedText(pdfTextBoxAnnotation.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, pdfTextBoxAnnotation.FontSize * num15, new SolidColorBrush(pdfTextBoxAnnotation.StrokeColor), 96.0);
						if (pdfTextBoxAnnotation.IsUnderline)
						{
							formattedText.SetTextDecorations(TextDecorations.Underline);
						}
						formattedText.MaxTextWidth = Math.Max(1.0, num23 - 8.0);
						formattedText.MaxTextHeight = Math.Max(1.0, num24 - 8.0);
						drawingContext.DrawText(formattedText, new Point(num25 + 4.0, num26 + 4.0));
					}
				}
			}
			stopwatch.Stop();
			PdfPerfLogger.Log($"Print page {actualPageNumber + 1} total: {stopwatch.ElapsedMilliseconds} ms");
			PrintProgress?.Report(new PrintProgressInfo($"Da render trang {actualPageNumber + 1}", pageNumber + 1, PageCount));
			return new DocumentPage(drawingVisual2, pageSize, rect, rect);
		}
		finally
		{
			PdfiumEngine.FPDF_ClosePage(num);
		}
	}

	private void DrawPdfTiles(DrawingContext dc, int pageIndex, int renderWidth, int renderHeight, double drawX, double drawY, double finalWidth, double finalHeight)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		double num = finalWidth / (double)Math.Max(1, renderWidth);
		double num2 = finalHeight / (double)Math.Max(1, renderHeight);
		int num3 = 0;
		for (int i = 0; i < renderHeight; i += 2048)
		{
			int num4 = Math.Min(2048, renderHeight - i);
			for (int j = 0; j < renderWidth; j += 2048)
			{
				int num5 = Math.Min(2048, renderWidth - j);
				BitmapSource bitmapSource = PdfiumEngine.RenderPageTileToBitmap(_document, pageIndex, renderWidth, renderHeight, j, i, num5, num4);
				if (bitmapSource != null)
				{
					Rect rectangle = new Rect(drawX + (double)j * num, drawY + (double)i * num2, (double)num5 * num, (double)num4 * num2);
					dc.DrawImage(bitmapSource, rectangle);
					num3++;
				}
			}
		}
		stopwatch.Stop();
		PdfPerfLogger.Log($"Tiled bitmap chunks drawn: {num3} in {stopwatch.ElapsedMilliseconds} ms");
	}

	private static void DrawTestFrame(DrawingContext dc, Size pageSize, double originX, double originY, double printableWidth, double printableHeight, double safePrintableWidth, double safePrintableHeight)
	{
		dc.DrawRectangle(Brushes.White, null, new Rect(0.0, 0.0, pageSize.Width, pageSize.Height));
		Pen pen = new Pen(Brushes.Black, 2.0);
		Pen pen2 = new Pen(Brushes.DodgerBlue, 1.5);
		Pen pen3 = new Pen(Brushes.SeaGreen, 1.5);
		Pen pen4 = new Pen(Brushes.OrangeRed, 1.25);
		dc.DrawRectangle(null, pen, new Rect(1.0, 1.0, Math.Max(1.0, pageSize.Width - 2.0), Math.Max(1.0, pageSize.Height - 2.0)));
		if (printableWidth > 0.0 && printableHeight > 0.0)
		{
			dc.DrawRectangle(null, pen2, new Rect(originX, originY, printableWidth, printableHeight));
			dc.DrawRectangle(null, pen3, new Rect(originX, originY, safePrintableWidth, safePrintableHeight));
		}
		double x = pageSize.Width / 2.0;
		double y = pageSize.Height / 2.0;
		dc.DrawLine(pen4, new Point(x, 0.0), new Point(x, pageSize.Height));
		dc.DrawLine(pen4, new Point(0.0, y), new Point(pageSize.Width, y));
		double num = 18.0;
		dc.DrawLine(pen4, new Point(0.0, 0.0), new Point(num, 0.0));
		dc.DrawLine(pen4, new Point(0.0, 0.0), new Point(0.0, num));
		dc.DrawLine(pen4, new Point(pageSize.Width, 0.0), new Point(pageSize.Width - num, 0.0));
		dc.DrawLine(pen4, new Point(pageSize.Width, 0.0), new Point(pageSize.Width, num));
		dc.DrawLine(pen4, new Point(0.0, pageSize.Height), new Point(num, pageSize.Height));
		dc.DrawLine(pen4, new Point(0.0, pageSize.Height), new Point(0.0, pageSize.Height - num));
		dc.DrawLine(pen4, new Point(pageSize.Width, pageSize.Height), new Point(pageSize.Width - num, pageSize.Height));
		dc.DrawLine(pen4, new Point(pageSize.Width, pageSize.Height), new Point(pageSize.Width, pageSize.Height - num));
		DrawTestLabel(dc, "TOP", new Point(pageSize.Width / 2.0 - 16.0, 10.0), Brushes.Black);
		DrawTestLabel(dc, "BOTTOM", new Point(pageSize.Width / 2.0 - 28.0, pageSize.Height - 24.0), Brushes.Black);
		DrawTestLabel(dc, "LEFT", new Point(10.0, pageSize.Height / 2.0 - 8.0), Brushes.Black);
		DrawTestLabel(dc, "RIGHT", new Point(pageSize.Width - 42.0, pageSize.Height / 2.0 - 8.0), Brushes.Black);
		DrawTestLabel(dc, $"Imageable: {Math.Round(printableWidth)} x {Math.Round(printableHeight)}", new Point(24.0, 24.0), Brushes.DodgerBlue);
		DrawTestLabel(dc, $"Safe: {Math.Round(safePrintableWidth)} x {Math.Round(safePrintableHeight)}", new Point(24.0, 44.0), Brushes.SeaGreen);
	}

	private static void DrawTestLabel(DrawingContext dc, string text, Point location, Brush brush)
	{
		Typeface typeface = new Typeface("Segoe UI");
		FormattedText formattedText = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11.0, brush, 96.0);
		dc.DrawText(formattedText, location);
	}

	private static Point FindBoxIntersection(Point from, Rect box)
	{
		Point point = new Point(box.X + box.Width / 2.0, box.Y + box.Height / 2.0);
		Vector vector = from - point;
		if (vector.Length == 0.0)
		{
			return from;
		}
		double val = double.MaxValue;
		double val2 = double.MaxValue;
		if (vector.X > 0.0)
		{
			val = box.Width / 2.0 / vector.X;
		}
		else if (vector.X < 0.0)
		{
			val = (0.0 - box.Width) / 2.0 / vector.X;
		}
		if (vector.Y > 0.0)
		{
			val2 = box.Height / 2.0 / vector.Y;
		}
		else if (vector.Y < 0.0)
		{
			val2 = (0.0 - box.Height) / 2.0 / vector.Y;
		}
		double num = Math.Min(val, val2);
		return point + vector * num;
	}

	private static void DrawArrowHeadVector(DrawingContext dc, Point from, Point to, Brush color)
	{
		Vector vector = to - from;
		if (vector.Length != 0.0)
		{
			vector.Normalize();
			Point point = from + vector * 12.0;
			Vector vector2 = new Vector(0.0 - vector.Y, vector.X);
			Point point2 = point + vector2 * 6.0;
			Point point3 = point - vector2 * 6.0;
			PathGeometry pathGeometry = new PathGeometry();
			PathFigure pathFigure = new PathFigure
			{
				StartPoint = from,
				IsClosed = true
			};
			pathFigure.Segments.Add(new LineSegment(point2, isStroked: true));
			pathFigure.Segments.Add(new LineSegment(point3, isStroked: true));
			pathGeometry.Figures.Add(pathFigure);
			dc.DrawGeometry(color, null, pathGeometry);
		}
	}

	~PdfDocumentPaginator()
	{
		if (_document != IntPtr.Zero)
		{
			PdfiumEngine.FPDF_CloseDocument(_document);
		}
	}
}
