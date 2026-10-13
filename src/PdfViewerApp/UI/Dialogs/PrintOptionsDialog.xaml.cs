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
	private bool _previewInProgress;
	private CancellationTokenSource? _previewCts;
	private readonly SemaphoreSlim _previewGate = new(1, 1);
	private readonly Dictionary<PdfSnapshotSelection, BitmapSource> _previewSources = new();
	private readonly Dictionary<int, Size> _previewPageSizes = new();
	private PdfRenderSession? _previewSession;
	private PrintQueue? _selectedPrintQueue;
	[Obfuscation(Exclude = true, ApplyToMembers = true)]
	private sealed record PrinterChoice(string FullName);
	public string? SelectedPrinterName => (PrinterComboBox.SelectedItem as PrinterChoice)?.FullName ?? _selectedPrintQueue?.FullName;

	public int StartPageIndex { get; private set; }
	public int EndPageIndex { get; private set; }
	public int Copies { get; private set; } = 1;
	public double PrintDpi { get; private set; } = 400.0;
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

	public int[] PageIndices { get; private set; } = Array.Empty<int>();
	public double ScalePercent { get; private set; } = 100;
	internal SnapshotPaperLayout? SnapshotLayout => _snapshotLayout;
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
        FitMarginsRadio.IsChecked = layout.FitToPage;
        ActualSizeRadio.IsChecked = !layout.FitToPage && Math.Abs(layout.ScalePercent - 100) < 0.001;
        CustomScaleRadio.IsChecked = !layout.FitToPage && !ActualSizeRadio.IsChecked.GetValueOrDefault();
        ScalePercentTextBox.Text = layout.ScalePercent.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        AutoCenterCheckBox.IsChecked = layout.Center;
        AllPagesRadio.IsChecked = CurrentViewRadio.IsChecked = CustomPagesRadio.IsChecked = false;
        CurrentPageRadio.IsChecked = true;
        AllPagesRadio.IsEnabled = CurrentViewRadio.IsEnabled = CustomPagesRadio.IsEnabled = false;
        PageRangeTextBox.IsEnabled = false;
        PrintScopeText.Text = "Vùng Snapshot";
        PrintEngineComboBox.IsEnabled = OptimizeCadCheckBox.IsEnabled = NativeSeparateJobsCheckBox.IsEnabled = false;
        PrintOrderComboBox.IsEnabled = PrintOffsetModeComboBox.IsEnabled = TestFrameCheckBox.IsEnabled = false;
        EngineAdviceText.Text = "Snapshot chỉ in vùng đã chọn, theo bố cục đang xem trước.";
        foreach (ComboBoxItem item in PaperSizeComboBox.Items)
            if (Equals(item.Tag, "Default")) item.IsEnabled = false;
        foreach (ComboBoxItem item in OrientationComboBox.Items)
            if (Equals(item.Tag, "Default")) item.IsEnabled = false;

	}

	public PrintOptionsDialog(int pageCount, int currentPageNumber)
		: this(pageCount, currentPageNumber, null)
	{
	}

	public PrintOptionsDialog(int pageCount, int currentPageNumber, string? pdfPath, BitmapSource? cachedPreview = null)
	{
		InitializeComponent();
		MaxHeight = Math.Max(640, SystemParameters.WorkArea.Height - 24);
		Height = Math.Min(Height, MaxHeight);
		_pageCount = Math.Max(1, pageCount);
		_currentPageNumber = Math.Clamp(currentPageNumber, 1, _pageCount);
		_previewPageNumber = _currentPageNumber;
		_pdfPath = pdfPath;
		DocumentNameText.Text = Path.GetFileName(pdfPath) ?? "Tài liệu PDF";

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
		_previewInProgress = true;
		ReadPreviewSettingsFromUi();
		UpdateSummary();
		int[] previewPages = TryGetSelectedPages(out var selectedPages) ? selectedPages : new[] { _previewPageNumber - 1 };
		if (!previewPages.Contains(_previewPageNumber - 1)) _previewPageNumber = previewPages[0] + 1;
		int previewIndex = Array.IndexOf(previewPages, _previewPageNumber - 1);
		PageCounterOverlayText.Text = SnapshotSelection == null ? $"{previewIndex + 1} / {previewPages.Length}" : "Snapshot";
		PreviewInfoText.Text = $"Trang {_previewPageNumber} / {_pageCount}";
		if (string.IsNullOrEmpty(_pdfPath) || _previewSession == null)
		{
			_previewInProgress = false;
			return;
		}
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
					source = await Task.Run(() => session.RenderSnapshot(snapshot, 1600), request.Token);
					if (source != null && !_dialogClosed)
					{
						if (_previewSources.Count >= 2) _previewSources.Clear();
						_previewSources[snapshot] = source;
					}
				}
			}
			if (_dialogClosed || request.IsCancellationRequested || source == null) return;
            Size cropPoints = _snapshotCropPoints;
            if (_snapshotLayout == null && !_previewPageSizes.TryGetValue(snapshot.PageIndex, out cropPoints))
            {
                cropPoints = await Task.Run(() => _previewSession.GetPageSizePoints(snapshot.PageIndex), request.Token);
                _previewPageSizes[snapshot.PageIndex] = cropPoints;
            }
			if (_dialogClosed || request.IsCancellationRequested) return;
			PreviewImage.Source = ComposePrintPreview(source, cropPoints, EstimatedGeometry());
			ApplyPreviewZoom();
			PreviewPlaceholderText.Visibility = Visibility.Collapsed;
			var geometry = await ReadPreviewGeometryAsync(request.Token);
            if (_dialogClosed || request.IsCancellationRequested) return;
            PreviewImage.Source = ComposePrintPreview(source, cropPoints, geometry);
            ApplyPreviewZoom();
			PreviewPlaceholderText.Visibility = Visibility.Collapsed;
			PrevPageButton.IsEnabled = SnapshotSelection == null && previewIndex > 0;
			NextPageButton.IsEnabled = SnapshotSelection == null && previewIndex < previewPages.Length - 1;
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
			if (ReferenceEquals(_previewCts, request))
			{
				_previewCts = null;
				_previewInProgress = false;
			}
			request.Dispose();
		}
	}

	private void PrintPreviewSettings_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!IsInitialized || _loadingPrinterDefaults) return;
		if (_dialogReady && ReferenceEquals(sender, PaperSizeComboBox)) _paperEdited = true;
		if (_dialogReady && ReferenceEquals(sender, OrientationComboBox)) _orientationEdited = true;
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

	private (double Width, double Height) GetPreviewPaperSize()
	{
        if (PaperSizeKey == "Default" && SelectedPrintTicket?.PageMediaSize is { Width: > 0, Height: > 0 } media)
            return (media.Width.Value * 25.4 / 96, media.Height.Value * 25.4 / 96);
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
			_ => SelectedPrintTicket?.PageMediaSize,
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
		return SelectedPrintTicket?.PageOrientation;
	}

	private void PrevPage_Click(object sender, RoutedEventArgs e)
	{
		if (TryGetSelectedPages(out var pages) && Array.IndexOf(pages, _previewPageNumber - 1) is int index && index > 0)
		{
			_previewPageNumber = pages[index - 1] + 1;
			UpdatePreview();
		}
	}

	private void NextPage_Click(object sender, RoutedEventArgs e)
	{
		if (TryGetSelectedPages(out var pages) && Array.IndexOf(pages, _previewPageNumber - 1) is int index && index >= 0 && index < pages.Length - 1)
		{
			_previewPageNumber = pages[index + 1] + 1;
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
				if (!_dialogClosed) UpdatePreview();
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
		UpdateSummary();
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
			_printerSelectionCts?.Cancel();
			_printerSelectionCts = null;
			_printerQueryInProgress = false;
			SelectedPrintQueue = printDialog.PrintQueue;
			SelectedPrintTicket = CloneTicket(printDialog.PrintTicket);
			_loadingPrinterDefaults = true;
			try
			{
				PrinterComboBox.SelectedItem = (PrinterComboBox.ItemsSource as IEnumerable<PrinterChoice>)?.FirstOrDefault(p => p.FullName == SelectedPrintQueue?.FullName);
			}
			finally { _loadingPrinterDefaults = false; }
			LastSelectedPrinterName = SelectedPrintQueue?.FullName;
			_geometryCache.Clear();
			_paperEdited = _orientationEdited = false;
			ApplyTicketDefaultsToUi(SelectedPrintTicket);
		}
	}

    private async void TestPrinter_Click(object sender, RoutedEventArgs e)
    {
        string? printer = SelectedPrinterName;
        if (string.IsNullOrEmpty(printer))
        {
            ValidationText.Text = "Chọn máy in trước khi xem thông tin.";
            return;
        }
        var button = sender as Button;
        if (button != null) button.IsEnabled = false;
        try
        {
            string report = string.Empty;
            await PrintWorker.RunAsync(() =>
            {
                using var server = new LocalPrintServer();
                using var queue = new PrintQueue(server, printer);
                var (devMode, _) = GetNativePrinterDevModeInfo(printer);
                var text = new System.Text.StringBuilder();
                text.AppendLine($"Máy in: {queue.FullName}");
                text.AppendLine($"Trạng thái: {(queue.IsOffline ? "Ngoại tuyến" : "Đang kết nối")}");
                text.AppendLine($"Lệnh đang chờ: {queue.NumberOfJobs}");
                if (devMode.HasValue)
                {
                    var value = devMode.Value;
                    text.AppendLine($"Khổ giấy mặc định: {ParsePaperKeyFromDevMode(value)}");
                    text.AppendLine($"Hướng giấy: {(value.dmOrientation == 2 ? "Ngang" : "Dọc")}");
                    text.AppendLine($"Biểu mẫu driver: {value.dmFormName}");
                }
                report = text.ToString();
            });
            if (!_dialogClosed) MessageBox.Show(this, report, "Thông tin máy in", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            if (!_dialogClosed) ValidationText.Text = "Không đọc được thông tin máy in. Kiểm tra kết nối rồi thử lại. " + ex.Message;
        }
        finally { if (!_dialogClosed && button != null) button.IsEnabled = true; }
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
        if (!TryGetSelectedPages(out var pages))
        {
            ValidationText.Text = $"Nhập trang từ 1 đến {_pageCount}. Có thể dùng dấu phẩy và khoảng trang, ví dụ: 1–3, 5.";
            return;
        }
        if (!TryReadScale(out double percent))
        {
            ValidationText.Text = "Nhập tỉ lệ từ 10% đến 400%.";
            return;
        }
        PageIndices = pages;
        StartPageIndex = pages.Min();
        EndPageIndex = pages.Max();
        ScalePercent = percent;
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
		if (_snapshotLayout != null && _previewInProgress)
		{
			ValidationText.Text = "Đang cập nhật bố cục Snapshot. Đợi bản xem trước hoàn tất rồi in.";
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
        ApplyColorToTicket(SelectedPrintTicket);
        // Scaling is applied by PDF Pro. A driver scale would apply it a second time.
        SelectedPrintTicket.PageScalingFactor = 100;
        ReadPreviewSettingsFromUi();

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
						if (!_paperEdited || _snapshotLayout != null) PaperSizeComboBox.SelectedItem = item;
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
						if (!_orientationEdited || _snapshotLayout != null) OrientationComboBox.SelectedItem = item;
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

	private static double MillimetersToDips(double millimeters)
	{
		return millimeters / 25.4 * 96.0;
	}

	private static double InchesToDips(double inches)
	{
		return inches * 96.0;
	}
}
