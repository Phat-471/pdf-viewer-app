using System;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace PdfViewerApp;

public partial class PrintOptionsDialog : Window, IComponentConnector
{
	public static string? LastSelectedPrinterName { get; set; }

	private readonly int _pageCount;
	private readonly int _currentPageNumber;
	private int _previewPageNumber;
	private readonly string? _pdfPath;
	private bool _loadingPrinterDefaults;

	public int StartPageIndex { get; private set; }

	public int EndPageIndex { get; private set; }

	public int Copies { get; private set; } = 1;

	public double PrintDpi { get; private set; } = 600.0;

	public string PaperSizeKey { get; private set; } = "A3";

	public string OrientationKey { get; private set; } = "Landscape";

	public PrintQueue? SelectedPrintQueue { get; private set; }

	public PrintTicket? SelectedPrintTicket { get; private set; }

	public bool AutoCenter => AutoCenterCheckBox.IsChecked == true;

	public bool FitToPrintableArea => FitMarginsRadio.IsChecked == true;

	public bool PrintTestFrame => TestFrameCheckBox.IsChecked == true;

	public bool NativeSeparatePageJobs => NativeSeparateJobsCheckBox.IsChecked == true;

	public bool OptimizeCadDrawings => OptimizeCadCheckBox.IsChecked == true;

	public bool ReversePageOrder
	{
		get
		{
			if (PrintOrderComboBox.SelectedItem is ComboBoxItem { Tag: string tag })
			{
				return tag == "Reverse";
			}
			return false;
		}
	}

	public string PrintEngineMode
	{
		get
		{
			if (PrintEngineComboBox.SelectedItem is ComboBoxItem { Tag: string tag })
			{
				return tag;
			}
			return "NativePdfium";
		}
	}

	public string PrintOffsetMode
	{
		get
		{
			if (PrintOffsetModeComboBox.SelectedItem is ComboBoxItem { Tag: string tag })
			{
				return tag;
			}
			return "Auto";
		}
	}

	internal PdfSnapshotSelection? SnapshotSelection { get; set; }

	public PrintOptionsDialog(int pageCount, int currentPageNumber)
		: this(pageCount, currentPageNumber, null)
	{
	}

	public PrintOptionsDialog(int pageCount, int currentPageNumber, string? pdfPath)
	{
		InitializeComponent();
		_pageCount = Math.Max(1, pageCount);
		_currentPageNumber = Math.Clamp(currentPageNumber, 1, _pageCount);
		_previewPageNumber = _currentPageNumber;
		_pdfPath = pdfPath;

		CurrentPageRadio.Content = $"Trang hiện tại ({_currentPageNumber})";
		CurrentViewRadio.Content = $"Vùng đang xem (trang {_currentPageNumber})";
		PageCountText.Text = $"/ {_pageCount}";
		PageRangeTextBox.Text = $"1-{_pageCount}";
		StartPageIndex = 0;
		EndPageIndex = _pageCount - 1;
		LoadPrinters();
		UpdatePreview();
	}

	private void UpdatePreview()
	{
		PageCounterOverlayText.Text = $"{_previewPageNumber} / {_pageCount}";
		PreviewInfoText.Text = $"Trang {_previewPageNumber} / {_pageCount}";

		if (string.IsNullOrEmpty(_pdfPath) || !System.IO.File.Exists(_pdfPath))
		{
			PreviewPlaceholderText.Visibility = Visibility.Visible;
			PreviewImage.Source = null;
			return;
		}

		try
		{
			// Render snapshot of the cropped area if available, else render the entire page
			var snapshot = SnapshotSelection ?? new PdfSnapshotSelection(_pdfPath, _previewPageNumber - 1, 0, 0, 1, 1);
			var bitmap = PdfSnapshotImageRenderer.RenderSnapshotToBitmap(snapshot, 600, 1000000); // Fast thumbnail size
			PreviewImage.Source = bitmap;
			PreviewPlaceholderText.Visibility = Visibility.Collapsed;
		}
		catch (Exception ex)
		{
			PreviewPlaceholderText.Text = "ERR";
			PreviewPlaceholderText.Visibility = Visibility.Visible;
			PreviewImage.Source = null;
			System.Diagnostics.Debug.WriteLine($"Failed to render print preview: {ex}");
		}
	}

	private void PrevPage_Click(object sender, RoutedEventArgs e)
	{
		if (_previewPageNumber > 1)
		{
			_previewPageNumber--;
			UpdatePreview();
		}
	}

	private void NextPage_Click(object sender, RoutedEventArgs e)
	{
		if (_previewPageNumber < _pageCount)
		{
			_previewPageNumber++;
			UpdatePreview();
		}
	}

	public PageMediaSize? CreatePageMediaSize()
	{
		return PaperSizeKey switch
		{
			"A4" => new PageMediaSize(PageMediaSizeName.ISOA4, MillimetersToDips(210.0), MillimetersToDips(297.0)), 
			"A3" => new PageMediaSize(PageMediaSizeName.ISOA3, MillimetersToDips(297.0), MillimetersToDips(420.0)), 
			"A2" => new PageMediaSize(PageMediaSizeName.ISOA2, MillimetersToDips(420.0), MillimetersToDips(594.0)), 
			"A1" => new PageMediaSize(PageMediaSizeName.ISOA1, MillimetersToDips(594.0), MillimetersToDips(841.0)), 
			"A0" => new PageMediaSize(PageMediaSizeName.ISOA0, MillimetersToDips(841.0), MillimetersToDips(1189.0)), 
			"Letter" => new PageMediaSize(PageMediaSizeName.NorthAmericaLetter, InchesToDips(8.5), InchesToDips(11.0)), 
			_ => null, 
		};
	}

	public PageOrientation? CreatePageOrientation()
	{
		string orientationKey = OrientationKey;
		if (!(orientationKey == "Portrait"))
		{
			if (orientationKey == "Landscape")
			{
				return PageOrientation.Landscape;
			}
			return null;
		}
		return PageOrientation.Portrait;
	}

	private void LoadPrinters()
	{
		List<PrintQueue> list = new List<PrintQueue>();
		try
		{
			list = new LocalPrintServer().GetPrintQueues(new[] 
			{ 
				EnumeratedPrintQueueTypes.Local, 
				EnumeratedPrintQueueTypes.Connections 
			}).OrderBy(q => q.FullName).ToList();
		}
		catch
		{
			// Fallback 1: Try local only
			try
			{
				list = new LocalPrintServer().GetPrintQueues(new[] 
				{ 
					EnumeratedPrintQueueTypes.Local 
				}).OrderBy(q => q.FullName).ToList();
			}
			catch
			{
				// Fallback 2: Just try to get the default printer
				try
				{
					PrintQueue defaultQueue = LocalPrintServer.GetDefaultPrintQueue();
					if (defaultQueue != null)
					{
						list.Add(defaultQueue);
					}
				}
				catch (Exception ex)
				{
					ValidationText.Text = "Không đọc được danh sách máy in: " + ex.Message;
				}
			}
		}

		if (list.Count > 0)
		{
			PrinterComboBox.ItemsSource = list;
			if (!string.IsNullOrEmpty(LastSelectedPrinterName))
			{
				PrintQueue? lastQueue = list.FirstOrDefault((PrintQueue q) => q.FullName == LastSelectedPrinterName);
				if (lastQueue != null)
				{
					PrinterComboBox.SelectedItem = lastQueue;
					return;
				}
			}
			try
			{
				PrintQueue defaultQueue = LocalPrintServer.GetDefaultPrintQueue();
				PrinterComboBox.SelectedItem = list.FirstOrDefault((PrintQueue q) => q.FullName == defaultQueue.FullName) ?? list.FirstOrDefault();
			}
			catch
			{
				PrinterComboBox.SelectedItem = list.FirstOrDefault();
			}
		}
		else
		{
			ValidationText.Text = "Không tìm thấy máy in nào được cài đặt trên máy tính.";
		}
	}

	private void PrinterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_loadingPrinterDefaults && PrinterComboBox.SelectedItem is PrintQueue printQueue)
		{
			SelectedPrintQueue = printQueue;
			SelectedPrintTicket = CloneTicket(printQueue.UserPrintTicket ?? printQueue.DefaultPrintTicket);
			LastSelectedPrinterName = printQueue.FullName;
			ApplyTicketDefaultsToUi(SelectedPrintTicket);
		}
	}

	private void PageRangeTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (CustomPagesRadio != null)
		{
			TextBox pageRangeTextBox = PageRangeTextBox;
			if (pageRangeTextBox != null && pageRangeTextBox.IsKeyboardFocusWithin)
			{
				CustomPagesRadio.IsChecked = true;
			}
		}
	}

	private void PrinterProperties_Click(object sender, RoutedEventArgs e)
	{
		PrintDialog printDialog = new PrintDialog();
		if (PrinterComboBox.SelectedItem is PrintQueue printQueue)
		{
			printDialog.PrintQueue = printQueue;
			printDialog.PrintTicket = CloneTicket(SelectedPrintTicket ?? printQueue.UserPrintTicket ?? printQueue.DefaultPrintTicket);
		}
		if (printDialog.ShowDialog() == true)
		{
			SelectedPrintQueue = printDialog.PrintQueue;
			SelectedPrintTicket = CloneTicket(printDialog.PrintTicket);
			PrinterComboBox.SelectedItem = SelectedPrintQueue;
			ApplyTicketDefaultsToUi(SelectedPrintTicket);
		}
	}

	private void Ok_Click(object sender, RoutedEventArgs e)
	{
		ValidationText.Text = string.Empty;
		if (!(PrinterComboBox.SelectedItem is PrintQueue printQueue))
		{
			ValidationText.Text = "Vui lòng chọn máy in.";
			return;
		}
		if (!int.TryParse(CopiesTextBox.Text, out var result) || result < 1 || result > 999)
		{
			ValidationText.Text = "Số bản in phải từ 1 đến 999.";
			return;
		}
		if (AllPagesRadio.IsChecked == true)
		{
			StartPageIndex = 0;
			EndPageIndex = _pageCount - 1;
		}
		else if (CurrentPageRadio.IsChecked == true || CurrentViewRadio.IsChecked == true)
		{
			StartPageIndex = _currentPageNumber - 1;
			EndPageIndex = _currentPageNumber - 1;
		}
		else
		{
			if (!TryParsePageRange(PageRangeTextBox.Text, out var start, out var end))
			{
				ValidationText.Text = $"Nhập số trang hợp lệ từ 1 đến {_pageCount}. Ví dụ: 1 hoặc 5-12.";
				return;
			}
			StartPageIndex = start - 1;
			EndPageIndex = end - 1;
		}
		// Đọc giá trị từ Combobox hoặc fallback về mặc định A3/Landscape
		PaperSizeKey = (PaperSizeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "A3";
		OrientationKey = (OrientationComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Landscape";

		if (QualityComboBox.SelectedItem is ComboBoxItem { Tag: string tag3 } && double.TryParse(tag3, out var result2))
		{
			PrintDpi = result2;
		}
		
		SelectedPrintQueue = printQueue;
		
		// Đảm bảo SelectedPrintTicket được khởi tạo và ghi đè trực tiếp cấu hình giấy/hướng
		if (SelectedPrintTicket == null)
		{
			SelectedPrintTicket = CloneTicket(printQueue.UserPrintTicket ?? printQueue.DefaultPrintTicket) ?? new PrintTicket();
		}
		
		PageMediaSize pageMediaSize = CreatePageMediaSize();
		if (pageMediaSize != null)
		{
			SelectedPrintTicket.PageMediaSize = pageMediaSize;
		}
		PageOrientation? pageOrientation = CreatePageOrientation();
		if (pageOrientation.HasValue)
		{
			SelectedPrintTicket.PageOrientation = pageOrientation;
		}
		SelectedPrintTicket.CopyCount = result;

		Copies = result;
		base.DialogResult = true;
	}

	private void ApplyTicketDefaultsToUi(PrintTicket? ticket)
	{
		_loadingPrinterDefaults = true;
		try
		{
			string paperKey = GetPaperKey(ticket?.PageMediaSize?.PageMediaSizeName);
			string orientationKey = GetOrientationKey(ticket?.PageOrientation);
			string value = ((paperKey == "Default") ? "theo máy in" : paperKey);
			string text = ((orientationKey == "Portrait") ? "Dọc" : ((!(orientationKey == "Landscape")) ? "theo máy in" : "Ngang"));
			string value2 = text;
			object obj;
			if (ticket != null)
			{
				PageResolution pageResolution = ticket.PageResolution;
				if (pageResolution != null && pageResolution.X.HasValue && ticket.PageResolution.Y.HasValue)
				{
					obj = $"{ticket.PageResolution.X}x{ticket.PageResolution.Y} dpi";
					goto IL_0129;
				}
			}
			obj = "độ phân giải mặc định";
			goto IL_0129;
			IL_0129:
			string value3 = (string)obj;
			PrinterDefaultText.Text = $"Mặc định máy in: khổ {value}, hướng {value2}, {value3}.";
		}
		finally
		{
			_loadingPrinterDefaults = false;
		}
	}

	private static PrintTicket? CloneTicket(PrintTicket? ticket)
	{
		return ticket?.Clone();
	}

	private static string GetPaperKey(PageMediaSizeName? sizeName)
	{
		return sizeName switch
		{
			PageMediaSizeName.ISOA4 => "A4", 
			PageMediaSizeName.ISOA3 => "A3", 
			PageMediaSizeName.ISOA2 => "A2", 
			PageMediaSizeName.ISOA1 => "A1", 
			PageMediaSizeName.ISOA0 => "A0", 
			PageMediaSizeName.NorthAmericaLetter => "Letter", 
			_ => "Default", 
		};
	}

	private static string GetOrientationKey(PageOrientation? orientation)
	{
		return orientation switch
		{
			PageOrientation.Portrait => "Portrait", 
			PageOrientation.Landscape => "Landscape", 
			_ => "Default", 
		};
	}

	private bool TryParsePageRange(string input, out int start, out int end)
	{
		start = 0;
		end = 0;
		string text = (input ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		string[] array = text.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (array.Length == 1)
		{
			if (!int.TryParse(array[0], out start))
			{
				return false;
			}
			end = start;
		}
		else
		{
			if (array.Length != 2)
			{
				return false;
			}
			if (!int.TryParse(array[0], out start) || !int.TryParse(array[1], out end))
			{
				return false;
			}
		}
		if (start >= 1 && end >= start)
		{
			return end <= _pageCount;
		}
		return false;
	}

	private static double MillimetersToDips(double millimeters)
	{
		return millimeters / 25.4 * 96.0;
	}

	private static double InchesToDips(double inches)
	{
		return inches * 96.0;
	}
}
