using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.Printing.Interop;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PdfViewerApp;

public partial class PrintOptionsDialog : Window, IComponentConnector
{
	[DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern int OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

	[DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern int ClosePrinter(IntPtr hPrinter);

	[DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern int DocumentProperties(IntPtr hwnd, IntPtr hPrinter, string pDeviceName, IntPtr pDevModeOutput, IntPtr pDevModeInput, int fMode);

	private const int DM_OUT_BUFFER = 2;

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct DEVMODEW
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string dmDeviceName;
		public short dmSpecVersion;
		public short dmDriverVersion;
		public short dmSize;
		public short dmDriverExtra;
		public int dmFields;
		public short dmOrientation;      // 1 = Portrait, 2 = Landscape
		public short dmPaperSize;        // 8 = A3, 9 = A4, 1 = Letter...
		public short dmPaperLength;
		public short dmPaperWidth;
		public short dmScale;
		public short dmCopies;
		public short dmDefaultSource;
		public short dmPrintQuality;
		public short dmColor;
		public short dmDuplex;
		public short dmYResolution;
		public short dmTTOption;
		public short dmCollate;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string dmFormName;
		public short dmLogPixels;
		public int dmBitsPerPel;
		public int dmPelsWidth;
		public int dmPelsHeight;
		public int dmNup;
		public int dmDisplayFrequency;
		public int dmICMMethod;
		public int dmICMIntent;
		public int dmMediaType;
		public int dmDitherType;
		public int dmReserved1;
		public int dmReserved2;
		public int dmPanningWidth;
		public int dmPanningHeight;
	}

	public static string? LastSelectedPrinterName { get; set; }

	private readonly int _pageCount;
	private readonly int _currentPageNumber;
	private int _previewPageNumber;
	private readonly string? _pdfPath;
	private bool _loadingPrinterDefaults;
	private System.Threading.CancellationTokenSource? _printerSelectionCts;
	private PdfSnapshotSelection? _snapshotSelection;
	private bool _dialogReady;
	private bool _dialogClosed;
	private bool _printerQueryInProgress;
	private CancellationTokenSource? _previewCts;
	private readonly SemaphoreSlim _previewGate = new(1, 1);
	private readonly Dictionary<PdfSnapshotSelection, BitmapSource> _previewSources = new();
	private PdfRenderSession? _previewSession;
	private PrintQueue? _selectedPrintQueue;
	[Obfuscation(Exclude = true, ApplyToMembers = true)]
	private sealed record PrinterChoice(string FullName);
	public string? SelectedPrinterName => (PrinterComboBox.SelectedItem as PrinterChoice)?.FullName ?? _selectedPrintQueue?.FullName;

	public int StartPageIndex { get; private set; }
	public int EndPageIndex { get; private set; }
	public int Copies { get; private set; } = 1;
	public double PrintDpi { get; private set; } = 600.0;
	public string PaperSizeKey { get; private set; } = "A3";
	public string OrientationKey { get; private set; } = "Landscape";
	public PrintQueue? SelectedPrintQueue
	{
		get
		{
			if (_selectedPrintQueue == null && SelectedPrinterName is string name)
				_selectedPrintQueue = new PrintQueue(new LocalPrintServer(), name, Array.Empty<string>());
			return _selectedPrintQueue;
		}
		private set => _selectedPrintQueue = value;
	}
	public PrintTicket? SelectedPrintTicket { get; private set; }
	public byte[]? NativeDevModeBytes { get; private set; }

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

	internal PdfSnapshotSelection? SnapshotSelection
	{
		get => _snapshotSelection;
		set
		{
			_snapshotSelection = value;
			if (value != null)
			{
				_previewPageNumber = Math.Clamp(value.PageIndex + 1, 1, _pageCount);
			}
			if (IsInitialized)
			{
				UpdatePreview();
			}
		}
	}

	private SnapshotPaperLayout? _snapshotLayout;
	private Size _snapshotCropPoints;

	internal void ConfigureSnapshot(PdfSnapshotSelection selection, BitmapSource? preview,
		SnapshotPaperLayout layout, Size cropPoints)
	{
		_snapshotLayout = layout;
		_snapshotCropPoints = cropPoints;
		if (preview?.IsFrozen == true) _previewSources[selection] = preview;
		SnapshotSelection = selection;
		ApplyTicketDefaultsToUi(null);
		PaperSizeComboBox.IsEnabled = OrientationComboBox.IsEnabled = false;
		FitMarginsRadio.IsEnabled = ActualSizeRadio.IsEnabled = false;
		FitMarginsRadio.IsChecked = layout.FitToPage;
		ActualSizeRadio.IsChecked = !layout.FitToPage;
		FitMarginsRadio.Content = "Vừa trang (bố cục Snapshot)";
		ActualSizeRadio.Content = $"Tỷ lệ Snapshot: {layout.ScalePercent:N1}%";
		AutoCenterCheckBox.IsChecked = true;
		AutoCenterCheckBox.IsEnabled = false;
		AllPagesRadio.IsChecked = CurrentViewRadio.IsChecked = CustomPagesRadio.IsChecked = false;
		CurrentPageRadio.IsChecked = true;
		AllPagesRadio.IsEnabled = CurrentViewRadio.IsEnabled = CustomPagesRadio.IsEnabled = false;
		PageRangeTextBox.IsEnabled = false;
		PaperSizeComboBox.ToolTip = OrientationComboBox.ToolTip = "Bố cục được chọn trong Snapshot. Đóng hộp thoại in để chỉnh lại.";
	}

	public PrintOptionsDialog(int pageCount, int currentPageNumber)
		: this(pageCount, currentPageNumber, null)
	{
	}

	public PrintOptionsDialog(int pageCount, int currentPageNumber, string? pdfPath, BitmapSource? cachedPreview = null)
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
		if (!string.IsNullOrEmpty(pdfPath))
		{
			_previewSession = new PdfRenderSession(pdfPath);
			if (cachedPreview != null && cachedPreview.IsFrozen)
				_previewSources[new PdfSnapshotSelection(pdfPath, _previewPageNumber - 1, 0, 0, 1, 1)] = cachedPreview;
		}
		ContentRendered += async (_, _) =>
		{
			if (_dialogReady) return;
			_dialogReady = true;
			UpdatePreview();
			await LoadPrintersAsync();
		};
		Closed += (_, _) =>
		{
			_dialogClosed = true;
			_previewCts?.Cancel();
			_printerSelectionCts?.Cancel();
			_previewSession?.Dispose();
		};
	}

	private static (DEVMODEW? DevModeStruct, byte[]? DevModeBytes) GetNativePrinterDevModeInfo(string printerName)
	{
		if (string.IsNullOrEmpty(printerName)) return (null, null);
		if (OpenPrinter(printerName, out IntPtr hPrinter, IntPtr.Zero) != 0 && hPrinter != IntPtr.Zero)
		{
			try
			{
				int size = DocumentProperties(IntPtr.Zero, hPrinter, printerName, IntPtr.Zero, IntPtr.Zero, 0);
				if (size > 0)
				{
					IntPtr pDevMode = Marshal.AllocHGlobal(size);
					try
					{
						if (DocumentProperties(IntPtr.Zero, hPrinter, printerName, pDevMode, IntPtr.Zero, DM_OUT_BUFFER) == 1)
						{
							DEVMODEW devMode = Marshal.PtrToStructure<DEVMODEW>(pDevMode);
							byte[] bytes = new byte[size];
							Marshal.Copy(pDevMode, bytes, 0, size);
							return (devMode, bytes);
						}
					}
					finally
					{
						Marshal.FreeHGlobal(pDevMode);
					}
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error reading Win32 DocumentProperties: {ex}");
			}
			finally
			{
				ClosePrinter(hPrinter);
			}
		}
		return (null, null);
	}

	private static string ParsePaperKeyFromDevMode(DEVMODEW devMode)
	{
		return devMode.dmPaperSize switch
		{
			8 => "A3",
			3 or 4 => "A3", // Tabloid / Ledger
			9 => "A4",
			66 => "A2",
			67 => "A1",
			68 => "A0",
			1 => "Letter",
			_ => (devMode.dmPaperWidth >= 2800 || devMode.dmPaperLength >= 4000) ? "A3" : "A4"
		};
	}

	private static string ParseOrientationKeyFromDevMode(DEVMODEW devMode)
	{
		return devMode.dmOrientation switch
		{
			2 => "Landscape",
			1 => "Portrait",
			_ => "Landscape"
		};
	}

	private async void UpdatePreview()
	{
		if (!_dialogReady || _dialogClosed) return;
		_previewCts?.Cancel();
		var request = new CancellationTokenSource();
		_previewCts = request;
		ReadPreviewSettingsFromUi();
		PageCounterOverlayText.Text = SnapshotSelection == null ? $"{_previewPageNumber} / {_pageCount}" : "Snapshot";
		PreviewInfoText.Text = $"Trang {_previewPageNumber} / {_pageCount}";
		if (string.IsNullOrEmpty(_pdfPath) || _previewSession == null) return;
		var snapshot = SnapshotSelection ?? new PdfSnapshotSelection(_pdfPath, _previewPageNumber - 1, 0, 0, 1, 1);
		bool acquired = false;
		try
		{
			if (!_previewSources.TryGetValue(snapshot, out BitmapSource? source))
			{
				PreviewPlaceholderText.Text = "Đang tạo bản xem trước…";
				PreviewPlaceholderText.Visibility = Visibility.Visible;
				await Task.Delay(150, request.Token);
				await _previewGate.WaitAsync(request.Token);
				acquired = true;
				if (!_previewSources.TryGetValue(snapshot, out source))
				{
					PdfRenderSession session = _previewSession;
					source = await Task.Run(() => session.RenderSnapshot(snapshot), request.Token);
					if (source != null && !_dialogClosed)
					{
						if (_previewSources.Count >= 2) _previewSources.Clear();
						_previewSources[snapshot] = source;
					}
				}
			}
			if (_dialogClosed || request.IsCancellationRequested || source == null) return;
			PreviewImage.Source = RenderSnapshotOnPreviewPaper(source);
			PreviewPlaceholderText.Visibility = Visibility.Collapsed;
			PrevPageButton.IsEnabled = SnapshotSelection == null && _previewPageNumber > 1;
			NextPageButton.IsEnabled = SnapshotSelection == null && _previewPageNumber < _pageCount;
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			if (!_dialogClosed && !request.IsCancellationRequested)
			{
				PreviewPlaceholderText.Text = "Không tạo được bản xem trước. Vẫn có thể chọn máy in.";
				PreviewPlaceholderText.Visibility = Visibility.Visible;
			}
			PdfPerfLogger.Log("Print preview failed: " + ex.Message);
		}
		finally
		{
			if (acquired) _previewGate.Release();
			if (ReferenceEquals(_previewCts, request)) _previewCts = null;
			request.Dispose();
		}
	}

	private void PrintPreviewSettings_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!IsInitialized) return;
		UpdatePreview();
	}

	private void ReadPreviewSettingsFromUi()
	{
		if (PaperSizeComboBox != null)
		{
			PaperSizeKey = (PaperSizeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Default";
		}
		if (OrientationComboBox != null)
		{
			OrientationKey = (OrientationComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Default";
		}
	}

	private BitmapSource RenderSnapshotOnPreviewPaper(BitmapSource snapshotBitmap)
	{
		if (_snapshotLayout != null)
			return _snapshotLayout.Compose(snapshotBitmap, _snapshotCropPoints, 1200, showGuides: true);
		(double paperWidth, double paperHeight) = GetPreviewPaperSize();
		
		bool landscape = false;
		if (OrientationKey == "Landscape")
		{
			landscape = true;
		}
		else if (OrientationKey == "Portrait")
		{
			landscape = false;
		}
		else // Default - dùng theo máy in
		{
			if (SelectedPrintTicket?.PageOrientation.HasValue == true)
			{
				landscape = SelectedPrintTicket.PageOrientation.Value == PageOrientation.Landscape;
			}
			else
			{
				landscape = paperWidth >= paperHeight;
			}
		}

		if (landscape && paperHeight > paperWidth)
		{
			(paperWidth, paperHeight) = (paperHeight, paperWidth);
		}
		else if (!landscape && paperWidth > paperHeight)
		{
			(paperWidth, paperHeight) = (paperHeight, paperWidth);
		}

		const int longEdge = 1200;
		double ratio = paperWidth / Math.Max(1.0, paperHeight);
		int pixelWidth = ratio >= 1.0 ? longEdge : Math.Max(1, (int)Math.Round(longEdge * ratio));
		int pixelHeight = ratio >= 1.0 ? Math.Max(1, (int)Math.Round(longEdge / ratio)) : longEdge;
		double margin = Math.Max(18.0, Math.Min(pixelWidth, pixelHeight) * 0.055);
		double safeWidth = Math.Max(1.0, pixelWidth - margin * 2.0);
		double safeHeight = Math.Max(1.0, pixelHeight - margin * 2.0);
		double imageScale = Math.Min(safeWidth / snapshotBitmap.PixelWidth, safeHeight / snapshotBitmap.PixelHeight);
		double imageWidth = snapshotBitmap.PixelWidth * imageScale;
		double imageHeight = snapshotBitmap.PixelHeight * imageScale;
		double imageX = (pixelWidth - imageWidth) / 2.0;
		double imageY = (pixelHeight - imageHeight) / 2.0;

		DrawingVisual visual = new DrawingVisual();
		using (DrawingContext dc = visual.RenderOpen())
		{
			dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, pixelWidth, pixelHeight));
			dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(245, 248, 252)), null, new Rect(margin, margin, safeWidth, safeHeight));
			dc.DrawImage(snapshotBitmap, new Rect(imageX, imageY, imageWidth, imageHeight));
			Pen paperPen = new Pen(new SolidColorBrush(Color.FromRgb(31, 41, 55)), 2.0);
			Pen safePen = new Pen(new SolidColorBrush(Color.FromRgb(20, 184, 166)), 2.0)
			{
				DashStyle = new DashStyle(new double[] { 6.0, 4.0 }, 0.0)
			};
			dc.DrawRectangle(null, paperPen, new Rect(1, 1, pixelWidth - 2, pixelHeight - 2));
			dc.DrawRectangle(null, safePen, new Rect(margin, margin, safeWidth, safeHeight));
		}

		RenderTargetBitmap preview = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
		preview.Render(visual);
		preview.Freeze();
		return preview;
	}

	private (double Width, double Height) GetPreviewPaperSize()
	{
		string key = PaperSizeKey;
		if (key == "Default" && SelectedPrintTicket?.PageMediaSize?.PageMediaSizeName.HasValue == true)
		{
			key = GetPaperKey(SelectedPrintTicket.PageMediaSize.PageMediaSizeName);
		}

		return key switch
		{
			"A4" => (210.0, 297.0),
			"A3" => (297.0, 420.0),
			"A2" => (420.0, 594.0),
			"A1" => (594.0, 841.0),
			"A0" => (841.0, 1189.0),
			"Letter" => (8.5 * 25.4, 11.0 * 25.4),
			_ => (297.0, 420.0),
		};
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
		if (orientationKey == "Portrait")
		{
			return PageOrientation.Portrait;
		}
		if (orientationKey == "Landscape")
		{
			return PageOrientation.Landscape;
		}
		return null;
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

	private async Task LoadPrintersAsync()
	{
		PrinterDefaultText.Text = "Đang tải danh sách máy in…";
		try
		{
			var result = await Task.Run(() =>
			{
				using var server = new LocalPrintServer();
				var names = new List<string>();
				try
				{
					using var queues = server.GetPrintQueues(new[] { "Name" }, new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections });
					foreach (PrintQueue queue in queues) { names.Add(queue.FullName); queue.Dispose(); }
				}
				catch
				{
					using var queues = server.GetPrintQueues(new[] { "Name" }, new[] { EnumeratedPrintQueueTypes.Local });
					foreach (PrintQueue queue in queues) { names.Add(queue.FullName); queue.Dispose(); }
				}
				string? defaultName = null;
				try { using var queue = LocalPrintServer.GetDefaultPrintQueue(); defaultName = queue.FullName; }
				catch { }
				return (Names: names.Distinct().OrderBy(n => n).ToArray(), Default: defaultName);
			});
			if (_dialogClosed) return;
			var choices = result.Names.Select(name => new PrinterChoice(name)).ToList();
			PrinterComboBox.ItemsSource = choices;
			PrinterComboBox.SelectedItem = choices.FirstOrDefault(q => q.FullName == LastSelectedPrinterName)
				?? choices.FirstOrDefault(q => q.FullName == result.Default) ?? choices.FirstOrDefault();
			if (choices.Count == 0) ValidationText.Text = "Không tìm thấy máy in nào được cài đặt trên máy tính.";
		}
		catch (Exception ex)
		{
			if (!_dialogClosed) ValidationText.Text = "Không đọc được danh sách máy in: " + ex.Message;
		}
	}

	private async void PrinterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_loadingPrinterDefaults || PrinterComboBox.SelectedItem is not PrinterChoice choice) return;
		_printerSelectionCts?.Cancel();
		var request = new CancellationTokenSource();
		_printerSelectionCts = request;
		_selectedPrintQueue = null;
		SelectedPrintTicket = null;
		NativeDevModeBytes = null;
		LastSelectedPrinterName = choice.FullName;
		_printerQueryInProgress = true;
		PrinterDefaultText.Text = $"Đang đọc thuộc tính {choice.FullName}…";
		try
		{
			var result = await Task.Run(() =>
			{
				request.Token.ThrowIfCancellationRequested();
				using var server = new LocalPrintServer();
				using var queue = new PrintQueue(server, choice.FullName);
				PrintTicket? ticket = queue.UserPrintTicket ?? queue.DefaultPrintTicket;
				var (devMode, bytes) = GetNativePrinterDevModeInfo(choice.FullName);
				if (bytes?.Length > 0)
				{
					try
					{
						using var converter = new PrintTicketConverter(choice.FullName, queue.ClientPrintSchemaVersion);
						ticket = converter.ConvertDevModeToPrintTicket(bytes);
					}
					catch { }
				}
				using var xml = ticket?.GetXmlStream();
				return (DevMode: devMode, Bytes: bytes, Ticket: xml?.ToArray());
			}, request.Token);
			if (_dialogClosed || request.IsCancellationRequested) return;
			NativeDevModeBytes = result.Bytes;
			SelectedPrintTicket = result.Ticket == null ? new PrintTicket() : new PrintTicket(new MemoryStream(result.Ticket));
			ApplyTicketDefaultsToUi(SelectedPrintTicket, result.DevMode);
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			if (!_dialogClosed && !request.IsCancellationRequested)
			{
				SelectedPrintTicket = new PrintTicket();
				PrinterDefaultText.Text = "Không đọc được mặc định máy in; có thể chọn khổ giấy và hướng in thủ công.";
			}
			PdfPerfLogger.Log("Printer defaults failed: " + ex.Message);
		}
		finally
		{
			if (ReferenceEquals(_printerSelectionCts, request))
			{
				_printerSelectionCts = null;
				_printerQueryInProgress = false;
			}
			request.Dispose();
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
		if (SelectedPrintQueue is PrintQueue printQueue)
		{
			printDialog.PrintQueue = printQueue;
			printDialog.PrintTicket = CloneTicket(SelectedPrintTicket ?? printQueue.UserPrintTicket ?? printQueue.DefaultPrintTicket);
		}
		if (printDialog.ShowDialog() == true)
		{
			SelectedPrintQueue = printDialog.PrintQueue;
			SelectedPrintTicket = CloneTicket(printDialog.PrintTicket);
			PrinterComboBox.SelectedItem = (PrinterComboBox.ItemsSource as IEnumerable<PrinterChoice>)?.FirstOrDefault(p => p.FullName == SelectedPrintQueue?.FullName);
			ApplyTicketDefaultsToUi(SelectedPrintTicket);
		}
	}

	private void TestPrinter_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			var sb = new System.Text.StringBuilder();
			sb.AppendLine("=== KẾT QUẢ KIỂM TRA THÔNG TIN MÁY IN NATIVE (WIN32 + PRINTTICKET) ===");
			sb.AppendLine();

			PrintQueue? queue = SelectedPrintQueue;
			if (queue == null)
			{
				try
				{
					queue = LocalPrintServer.GetDefaultPrintQueue();
				}
				catch { }
			}

			if (queue != null)
			{
				sb.AppendLine($"📌 Máy in được chọn: {queue.FullName}");
				sb.AppendLine($"   • Trạng thái: {(queue.IsOffline ? "Offline (Tắt/Mất kết nối)" : "Online (Sẵn sàng)")}");
				sb.AppendLine($"   • Tệp chờ in (Jobs): {queue.NumberOfJobs}");

				// Đọc trực tiếp từ Win32 DEVMODE Driver
				(DEVMODEW? nativeDevMode, _) = GetNativePrinterDevModeInfo(queue.FullName);
				if (nativeDevMode.HasValue)
				{
					var dev = nativeDevMode.Value;
					string devPaper = ParsePaperKeyFromDevMode(dev);
					string devOrientation = ParseOrientationKeyFromDevMode(dev);

					sb.AppendLine();
					sb.AppendLine("🖨️ Thông số thực tế đọc trực tiếp từ Win32 Driver (DEVMODE):");
					sb.AppendLine($"   • Driver Form Name: {dev.dmFormName}");
					sb.AppendLine($"   • Khổ giấy Driver (dmPaperSize={dev.dmPaperSize}): {devPaper}");
					sb.AppendLine($"   • Kích thước Driver (W x L): {dev.dmPaperWidth / 10.0:F1} mm x {dev.dmPaperLength / 10.0:F1} mm");
					sb.AppendLine($"   • Hướng in Driver (dmOrientation={dev.dmOrientation}): {devOrientation}");
				}
				
				PrintTicket? ticket = SelectedPrintTicket ?? queue.UserPrintTicket ?? queue.DefaultPrintTicket;
				if (ticket != null)
				{
					string paperKey = GetPaperKey(ticket.PageMediaSize?.PageMediaSizeName);
					string orientationKey = GetOrientationKey(ticket.PageOrientation);

					sb.AppendLine();
					sb.AppendLine("📄 Thông số đọc qua WPF PrintTicket Schema:");
					sb.AppendLine($"   • Khổ giấy PrintTicket: {paperKey} (MediaName: {ticket.PageMediaSize?.PageMediaSizeName})");
					if (ticket.PageMediaSize?.Width != null && ticket.PageMediaSize?.Height != null)
					{
						sb.AppendLine($"   • Kích thước vùng in: {ticket.PageMediaSize.Width:F1} x {ticket.PageMediaSize.Height:F1} DIPs");
					}
					sb.AppendLine($"   • Hướng in PrintTicket: {orientationKey} ({ticket.PageOrientation})");
					if (ticket.PageResolution != null)
					{
						sb.AppendLine($"   • Độ phân giải DPI: {ticket.PageResolution.X} x {ticket.PageResolution.Y} DPI");
					}
				}
			}
			else
			{
				sb.AppendLine("❌ Không tìm thấy máy in mặc định hoặc máy in được chọn.");
			}

			sb.AppendLine();
			sb.AppendLine("📋 Danh sách tất cả máy in trên hệ thống:");
			try
			{
				var allPrinters = new LocalPrintServer().GetPrintQueues(new[] 
				{ 
					EnumeratedPrintQueueTypes.Local, 
					EnumeratedPrintQueueTypes.Connections 
				});
				int index = 1;
				foreach (var p in allPrinters)
				{
					sb.AppendLine($"   {index++}. {p.FullName} {(p.IsOffline ? "[Offline]" : "[Ready]")}");
				}
			}
			catch (Exception exPrinters)
			{
				sb.AppendLine($"   • Lỗi liệt kê danh sách: {exPrinters.Message}");
			}

			MessageBox.Show(sb.ToString(), "Kiểm Tra Thuộc Tính Máy In (Win32 DEVMODE)", MessageBoxButton.OK, MessageBoxImage.Information);
		}
		catch (Exception ex)
		{
			MessageBox.Show($"Lỗi khi kiểm tra thuộc tính máy in:\n{ex.Message}", "Lỗi Kiểm Tra", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private void Ok_Click(object sender, RoutedEventArgs e)
	{
		ValidationText.Text = string.Empty;
		if (string.IsNullOrEmpty(SelectedPrinterName))
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
		PaperSizeKey = (PaperSizeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "A3";
		OrientationKey = (OrientationComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Landscape";

		if (QualityComboBox.SelectedItem is ComboBoxItem { Tag: string tag3 } && double.TryParse(tag3, out var result2))
		{
			PrintDpi = result2;
		}
		
		if (_printerQueryInProgress)
		{
			ValidationText.Text = "Đang đọc cấu hình máy in. Vui lòng đợi hoàn tất.";
			return;
		}
		SelectedPrintTicket ??= new PrintTicket();
		
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

	private void ApplyTicketDefaultsToUi(PrintTicket? ticket, DEVMODEW? devMode = null)
	{
		_loadingPrinterDefaults = true;
		try
		{
			string paperKey = devMode.HasValue ? ParsePaperKeyFromDevMode(devMode.Value) : GetPaperKey(ticket?.PageMediaSize?.PageMediaSizeName);
			string orientationKey = devMode.HasValue ? ParseOrientationKeyFromDevMode(devMode.Value) : GetOrientationKey(ticket?.PageOrientation);
			
			string value = ((paperKey == "Default") ? "theo máy in" : paperKey);
			string text = ((orientationKey == "Portrait") ? "Dọc" : ((!(orientationKey == "Landscape")) ? "theo máy in" : "Ngang"));
			string value2 = text;
			
			string value3 = (ticket?.PageResolution?.X.HasValue == true) 
				? $"{ticket.PageResolution.X}x{ticket.PageResolution.Y} dpi" 
				: "độ phân giải mặc định";

			PrinterDefaultText.Text = $"Mặc định máy in: khổ {value}, hướng {value2}, {value3}.";
			if (_snapshotLayout != null)
			{
				paperKey = _snapshotLayout.PaperKey;
				orientationKey = _snapshotLayout.Landscape ? "Landscape" : "Portrait";
				PrinterDefaultText.Text += " Bố cục in lấy từ Snapshot.";
			}

			// Tự động chọn giá trị tương ứng trong ComboBox khổ giấy & hướng in
			if (PaperSizeComboBox != null)
			{
				foreach (ComboBoxItem item in PaperSizeComboBox.Items)
				{
					if (item.Tag?.ToString() == paperKey)
					{
						PaperSizeComboBox.SelectedItem = item;
						break;
					}
				}
			}

			if (OrientationComboBox != null)
			{
				foreach (ComboBoxItem item in OrientationComboBox.Items)
				{
					if (item.Tag?.ToString() == orientationKey)
					{
						OrientationComboBox.SelectedItem = item;
						break;
					}
				}
			}

			UpdatePreview();
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
