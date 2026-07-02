using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Printing;
using System.Printing.Interop;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Fluent;
using Microsoft.Win32;
using PdfViewerApp.Ai;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace PdfViewerApp;

public partial class PdfDocumentTab : UserControl, IComponentConnector
{
	private readonly record struct RenderQueueItem(int PageNumber, bool IsThumbnail, int Generation, string Key, int Priority);

	private readonly record struct PendingTextEdit(int PageNumber, string OriginalText, string ReplacementText, double Left, double Bottom, double Width, double Height, PdfTextBoxAnnotation WhiteoutAnnotation, PdfTextBoxAnnotation TextAnnotation);

	private readonly record struct OcrTextRegion(string Text, double Left, double Bottom, double Width, double Height);

	private bool _isDrawing;
	private Rect? _selectedEditRectPdf = null;
	private int _selectedEditPageNumber = -1;
	private int _selectedEditCharIndex = -1;
	private System.Windows.Controls.TextBox? _activeDirectEditTextBox = null;
	private Action? _activeDirectEditCommitAction = null;

	private Point _drawStartPoint;

	private List<List<Point>>? _tempSignatureStrokes;
	private double _tempSignatureWidth;
	private double _tempSignatureHeight;
	private Color _tempSignatureColor = Colors.Blue;
	private string? _tempStampText;
	private string? _tempSignatureImagePath;
	private PdfMeasurementAnnotation? _pendingAreaAnnotation;
	public double CurrentMeasurementScale { get; set; } = 100.0;

	private Rectangle? _tempRect;

	private Line? _tempLine;

	private Polyline? _tempPolyline;

	private Ellipse? _tempEllipse;

	private Canvas? _activeCanvas;

	private bool _isDraggingAnn;

	private bool _isDraggingArrowHandle;

	private bool _isDraggingLineStart;

	private bool _isDraggingLineEnd;

	private bool _isResizingAnn;

	private double _dragStartAnnX;

	private double _dragStartAnnY;

	private double _dragStartAnnWidth;

	private double _dragStartAnnHeight;

	private double _dragStartArrowX;

	private double _dragStartArrowY;

	private static PdfAnnotation? _copiedAnnotation;

	private bool _isReverseView;

	private bool _isRulersEnabled;

	private bool _isGuidesEnabled;

	private bool _isReadMode;

	private bool _isFullScreen;

	private bool _isSelectingText;

	private int _selectionStartPageIndex = -1;

	private int _selectionStartIndex = -1;

	private int _selectionEndPageIndex = -1;

	private int _selectionEndIndex = -1;

	private string _selectedText = "";

	private readonly Dictionary<int, nint> _textPages = new Dictionary<int, nint>();

	private readonly List<Size> _pageDimensions = new List<Size>();

	private readonly List<int> _pageOrder = new List<int>();

	private readonly HashSet<int> _selectedPages = new HashSet<int>();

	private readonly List<int> _recentPages = new List<int>();

	private readonly HashSet<int> _bookmarkedPages = new HashSet<int>();

	private readonly Services.Cache.PdfCacheManager _cacheManager;

	private readonly PriorityQueue<RenderQueueItem, (int Priority, long Sequence)> _renderQueue = new PriorityQueue<RenderQueueItem, (int, long)>();

	private readonly HashSet<string> _renderQueueKeys = new HashSet<string>(StringComparer.Ordinal);

	private bool _isSidebarVisible;

	private nint _documentHandle = IntPtr.Zero;

	private Point? _pendingZoomContentPoint;

	private Point? _pendingZoomHostPoint;

	private Point? _pendingZoomViewportPoint;

	private double _pendingZoomBaseZoom = 1.0;

	private bool _resetScrollAfterRender;

	private int _renderGeneration;

	private int _loadGeneration;

	private bool _renderInProgress;

	private bool _renderAgainRequested;

	private bool _renderQueueWorkerRunning;

	private bool _zoomPreviewActive;

	private Size _zoomPreviewBaseHostSize;

	private DispatcherTimer _zoomTimer;

	private double _targetZoom = -1.0;

	private Point _smoothZoomAnchor;

	private Point _smoothZoomHostAnchor;

	private readonly Stack<List<PdfAnnotation>> _undoStack = new();

	private readonly Stack<List<PdfAnnotation>> _redoStack = new();

	private DispatcherTimer? _smoothZoomTimer;

	private double _targetVerticalOffset = -1;

	private double _targetHorizontalOffset = -1;

	private DispatcherTimer? _smoothScrollTimer;

	private readonly DispatcherTimer _viewportTimer;

	private double _baseZoomForLayout = 1.0;

	private bool _isFirstLoad = true;


	private readonly Dictionary<int, int> _pageRotations = new Dictionary<int, int>();

	private readonly ConcurrentDictionary<int, Task<List<OcrTextRegion>?>> _ocrLoadingTasks = new();

	private readonly List<PendingTextEdit> _pendingTextEdits = new List<PendingTextEdit>();

	private const int RecentPagesLimit = 8;

	private const double MinZoom = 0.1;

	private const double MaxZoom = 4.0;

	private const double ZoomStep = 1.08;

	private const double WheelZoomStep = 1.055;

	private const int MaxLoadedPageDistance = 1;

	private static long MaxBitmapCacheBytes = 402653184L;

	private static readonly SolidColorBrush RulerBrush = CreateFrozenBrush(148, 163, 184);

	private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
	{
		var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
		brush.Freeze();
		return brush;
	}

	private const long HighCostRenderPixelThreshold = 12000000L;

	private const int NormalPrefetchDistance = 2;

	private const int HighCostPrefetchDistance = 1;

	private bool _isPanning;

	private Point _panStartPoint;

	private double _panStartHorizontalOffset;

	private double _panStartVerticalOffset;

	private readonly HashSet<int> _loadingPages = new HashSet<int>();

	private readonly HashSet<int> _loadingThumbs = new HashSet<int>();

	private bool _thumbnailLoadDeferred;

	private long _renderQueueSequence;

	private int _selectionAnchorPage = 1;

	public List<PdfAnnotation> Annotations { get; } = new List<PdfAnnotation>();

	public string ActiveTool { get; set; } = "Select";

	public bool KeepToolsActive { get; set; }

	private bool IsContinuousTool(string tool)
	{
		if (KeepToolsActive && tool != "Select" && tool != "EditText" && tool != "SelectText")
		{
			return true;
		}
		return tool == "ShapeLine" || 
		       tool == "ShapeRect" || 
		       tool == "ShapeOval" || 
		       tool == "MeasureDistance" || 
		       tool == "MeasureArea" || 
		       tool == "MeasurePerimeter" || 
		       tool == "PlaceSignature" || 
		       tool == "PlaceStamp" ||
		       tool == "StickyNote" ||
		       tool == "TextBox" ||
		       tool == "Callout" ||
		       tool == "Snapshot" ||
		       tool == "AiSnapshot" ||
		       tool == "Ink";
	}

	public event EventHandler? SelectedAnnotationChanged;

	private PdfAnnotation? _selectedAnnotation;
	public PdfAnnotation? SelectedAnnotation
	{
		get => _selectedAnnotation;
		set
		{
			if (_selectedAnnotation != value)
			{
				_selectedAnnotation = value;
				SelectedAnnotationChanged?.Invoke(this, EventArgs.Empty);
			}
		}
	}

	public string ActiveFontFamily { get; set; } = "Segoe UI";

	public double ActiveFontSize { get; set; } = 14.0;

	public bool ActiveIsBold { get; set; }

	public bool ActiveIsItalic { get; set; }

	public bool ActiveIsUnderline { get; set; }

	public Color ActiveStrokeColor { get; set; } = Colors.Red;

	public Color ActiveBgColor { get; set; } = Colors.Transparent;

	public double ActiveOpacity { get; set; } = 1.0;

	public bool ActiveIsStrikeout { get; set; }

	public bool ActiveIsSubscript { get; set; }

	public bool ActiveIsSuperscript { get; set; }

	public TextAlignment ActiveTextAlignment { get; set; } = TextAlignment.Left;

	public string? CurrentPdfPath { get; private set; }

	public int PageCount { get; private set; }

	public int SelectedPageNumber { get; private set; } = 1;

	public IReadOnlyList<int> SelectedPageNumbers
	{
		get
		{
			List<int> selected = GetSelectedPagesInOrder();
			return selected.Count == 0 ? new[] { SelectedPageNumber } : selected;
		}
	}

	public double CurrentZoom { get; private set; } = 1.0;

	public string LastStatusMessage { get; private set; } = "Sẵn sàng";

	public event EventHandler? StatusChanged;

	public event EventHandler? ZoomChanged;

	public event EventHandler? PageChanged;

	public event EventHandler<AiSnapshotRequest>? AiSnapshotRequested;

	public event EventHandler<string>? DocumentReloaded;

	public event EventHandler<string>? DocumentOpenRequested;

	public event EventHandler<double>? ScaleCalibrated;

	private void OverlayCanvas_MouseDown(object sender, MouseButtonEventArgs e)
	{
		if (!(sender is Canvas { Tag: var tag } canvas) || !(tag is int num))
		{
			return;
		}
		if (ActiveTool != "EditText" && ActiveTool != "SelectText")
		{
			FrameworkElement clickedElement = e.Source as FrameworkElement;
			while (clickedElement != null && clickedElement != canvas && !(clickedElement.Tag is PdfAnnotation))
			{
				clickedElement = VisualTreeHelper.GetParent(clickedElement) as FrameworkElement;
			}
			if (clickedElement != null && clickedElement.Tag is PdfAnnotation clickedAnnotation)
			{
				// Auto-switch to Select tool to allow moving and editing the annotation
				ActiveTool = "Select";
				if (Window.GetWindow(this) is MainWindow mainWindow)
				{
					mainWindow.ActiveTool = "Select";
				}
				SelectedAnnotation = clickedAnnotation;

				if (e.ClickCount == 2)
				{
					if (clickedAnnotation is PdfTextBoxAnnotation tb)
					{
						ShowEditTextBoxInput(canvas, tb, num);
						e.Handled = true;
						return;
					}
					else if (clickedAnnotation is PdfStickyNoteAnnotation sticky)
					{
						ShowStickyNoteEdit(canvas, sticky, num);
						e.Handled = true;
						return;
					}
				}
				else
				{
					SaveUndoState();
					_isDraggingAnn = true;
					_drawStartPoint = e.GetPosition(canvas);
					_dragStartAnnX = clickedAnnotation.X;
					_dragStartAnnY = clickedAnnotation.Y;
					canvas.CaptureMouse();
				}
				e.Handled = true;
				RedrawPageAnnotations(canvas, num);
				return;
			}
		}
		if (ActiveTool == "EditText" || ActiveTool == "SelectText" || ActiveTool == "Highlight")
		{
			if (e.ClickCount >= 1)
			{
				int charIndexAtMousePos = GetCharIndexAtMousePos(canvas, e.GetPosition(canvas), num);
				if (charIndexAtMousePos != -1)
				{
					if (ActiveTool == "EditText")
					{
						// [FIX CHỚP TẮT] Chặn luồng tại MouseDown để nhường việc mở TextBox 
						// sang sự kiện MouseUp khi click chuột đã kết thúc hoàn toàn.
						e.Handled = true;
						return;
					}
					else if (e.ClickCount >= 2)
					{
						// Dành cho SelectText/Highlight nếu lỡ double click
						e.Handled = true;
						return;
					}
				}
				else
				{
					if (ActiveTool == "EditText" || ActiveTool == "SelectText" || ActiveTool == "Highlight")
					{
						LogStatus("Vùng này không có chữ (PDF dạng ảnh/scan). Hãy thử công cụ OCR để nhận diện chữ trước.");
					}

					if (ActiveTool == "EditText")
					{
						if (_selectedEditRectPdf.HasValue)
						{
							_selectedEditRectPdf = null;
							_selectedEditPageNumber = -1;
							_selectedEditCharIndex = -1;
							RedrawAllPageAnnotations();
						}
					}
				}
			}
			
			if (ActiveTool == "SelectText" || ActiveTool == "Highlight")
			{
				int charIndexAtMousePos2 = GetCharIndexAtMousePos(canvas, e.GetPosition(canvas), num);
				if (charIndexAtMousePos2 != -1)
				{
					ClearAllTextSelectionHighlights();
					_isSelectingText = true;
					_selectionStartPageIndex = num - 1;
					_selectionStartIndex = charIndexAtMousePos2;
					_selectionEndPageIndex = num - 1;
					_selectionEndIndex = charIndexAtMousePos2;
					canvas.CaptureMouse();
					e.Handled = true;
				}
				else
				{
					LogStatus("Vùng này không có văn bản có thể chọn (PDF dạng ảnh/scan).");
				}
				return;
			}
		}
		if (ActiveTool == "Select")
		{
			FrameworkElement frameworkElement = e.Source as FrameworkElement;
			if (frameworkElement != null && (frameworkElement.Tag as string == "ResizeHandle" || frameworkElement.Tag as string == "ArrowHandle" || frameworkElement.Tag as string == "LineStartHandle" || frameworkElement.Tag as string == "LineEndHandle"))
			{
				SaveUndoState();
			}
			if (frameworkElement != null && frameworkElement.Tag as string == "ResizeHandle" && SelectedAnnotation is PdfTextBoxAnnotation pdfTextBoxAnnotation)
			{
				_isResizingAnn = true;
				_drawStartPoint = e.GetPosition(canvas);
				_dragStartAnnWidth = pdfTextBoxAnnotation.Width;
				_dragStartAnnHeight = pdfTextBoxAnnotation.Height;
				canvas.CaptureMouse();
				e.Handled = true;
				return;
			}
			if (frameworkElement != null && frameworkElement.Tag as string == "ResizeHandle" && SelectedAnnotation is PdfSignatureAnnotation pdfSignatureAnnotation)
			{
				_isResizingAnn = true;
				_drawStartPoint = e.GetPosition(canvas);
				_dragStartAnnWidth = pdfSignatureAnnotation.Width;
				_dragStartAnnHeight = pdfSignatureAnnotation.Height;
				canvas.CaptureMouse();
				e.Handled = true;
				return;
			}
			if (frameworkElement != null && frameworkElement.Tag as string == "ResizeHandle" && SelectedAnnotation is PdfShapeAnnotation { Type: not ShapeType.Line } pdfShapeAnnotation)
			{
				_isResizingAnn = true;
				_drawStartPoint = e.GetPosition(canvas);
				_dragStartAnnWidth = pdfShapeAnnotation.Width;
				_dragStartAnnHeight = pdfShapeAnnotation.Height;
				canvas.CaptureMouse();
				e.Handled = true;
				return;
			}
			if (frameworkElement != null && frameworkElement.Tag as string == "ArrowHandle" && SelectedAnnotation is PdfCalloutAnnotation pdfCalloutAnnotation)
			{
				_isDraggingArrowHandle = true;
				_drawStartPoint = e.GetPosition(canvas);
				_dragStartArrowX = pdfCalloutAnnotation.ArrowX;
				_dragStartArrowY = pdfCalloutAnnotation.ArrowY;
				canvas.CaptureMouse();
				e.Handled = true;
				return;
			}
			if (frameworkElement != null && frameworkElement.Tag as string == "LineStartHandle" && SelectedAnnotation is PdfShapeAnnotation pdfShapeAnnotation2)
			{
				_isDraggingLineStart = true;
				_drawStartPoint = e.GetPosition(canvas);
				_dragStartAnnX = pdfShapeAnnotation2.X;
				_dragStartAnnY = pdfShapeAnnotation2.Y;
				canvas.CaptureMouse();
				e.Handled = true;
				return;
			}
			if (frameworkElement != null && frameworkElement.Tag as string == "LineEndHandle" && SelectedAnnotation is PdfShapeAnnotation pdfShapeAnnotation3)
			{
				_isDraggingLineEnd = true;
				_drawStartPoint = e.GetPosition(canvas);
				_dragStartArrowX = pdfShapeAnnotation3.EndX;
				_dragStartArrowY = pdfShapeAnnotation3.EndY;
				canvas.CaptureMouse();
				e.Handled = true;
				return;
			}
			while (frameworkElement != null && !(frameworkElement.Tag is PdfAnnotation))
			{
				frameworkElement = VisualTreeHelper.GetParent(frameworkElement) as FrameworkElement;
			}
			if (frameworkElement != null && frameworkElement.Tag is PdfAnnotation pdfAnnotation)
			{
				SelectedAnnotation = pdfAnnotation;
				if (e.ClickCount == 2 && pdfAnnotation is PdfTextBoxAnnotation tb)
				{
					ShowEditTextBoxInput(canvas, tb, num);
				}
				else if (e.ClickCount == 2 && pdfAnnotation is PdfStickyNoteAnnotation sticky)
				{
					ShowStickyNoteEdit(canvas, sticky, num);
				}
				else
				{
					SaveUndoState();
					_isDraggingAnn = true;
					_drawStartPoint = e.GetPosition(canvas);
					_dragStartAnnX = pdfAnnotation.X;
					_dragStartAnnY = pdfAnnotation.Y;
					canvas.CaptureMouse();
				}
				e.Handled = true;
				RedrawPageAnnotations(canvas, num);
			}
			else
			{
				SelectedAnnotation = null;
				RedrawPageAnnotations(canvas, num);
			}
		}
		else if (ActiveTool == "TextBox" || ActiveTool == "Snapshot" || ActiveTool == "AiSnapshot" || ActiveTool == "ShapeRect" || ActiveTool == "ShapeOval")
		{
			_isDrawing = true;
			_drawStartPoint = e.GetPosition(canvas);
			_activeCanvas = canvas;
			canvas.CaptureMouse();
			if (ActiveTool == "Snapshot" || ActiveTool == "AiSnapshot")
			{
				_tempRect = new Rectangle
				{
					Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F766E")),
					StrokeThickness = 2.0,
					Fill = new SolidColorBrush(Color.FromArgb(40, 15, 118, 110))
				};
				canvas.Children.Add(_tempRect);
			}
			else if (ActiveTool == "ShapeRect" || ActiveTool == "TextBox")
			{
				_tempRect = new Rectangle
				{
					Stroke = new SolidColorBrush(ActiveStrokeColor),
					StrokeThickness = ((ActiveTool == "TextBox") ? 1.5 : 2.0),
					StrokeDashArray = ((ActiveTool == "TextBox") ? new DoubleCollection { 4.0, 4.0 } : null)
				};
				canvas.Children.Add(_tempRect);
			}
			else if (ActiveTool == "ShapeOval")
			{
				_tempEllipse = new Ellipse
				{
					Stroke = new SolidColorBrush(ActiveStrokeColor),
					StrokeThickness = 2.0
				};
				canvas.Children.Add(_tempEllipse);
			}
			e.Handled = true;
		}
		else if (ActiveTool == "Callout" || ActiveTool == "ShapeLine")
		{
			_isDrawing = true;
			_drawStartPoint = e.GetPosition(canvas);
			_activeCanvas = canvas;
			canvas.CaptureMouse();
			_tempLine = new Line
			{
				Stroke = new SolidColorBrush(ActiveStrokeColor),
				StrokeThickness = 2.0,
				X1 = _drawStartPoint.X,
				Y1 = _drawStartPoint.Y,
				X2 = _drawStartPoint.X,
				Y2 = _drawStartPoint.Y
			};
			canvas.Children.Add(_tempLine);
			e.Handled = true;
		}
		else if (ActiveTool == "Ink")
		{
			_isDrawing = true;
			_drawStartPoint = e.GetPosition(canvas);
			_activeCanvas = canvas;
			canvas.CaptureMouse();
			_tempPolyline = new Polyline
			{
				Stroke = new SolidColorBrush(ActiveStrokeColor),
				StrokeThickness = 2.0
			};
			_tempPolyline.Points.Add(_drawStartPoint);
			canvas.Children.Add(_tempPolyline);
			e.Handled = true;
		}
		else if (ActiveTool == "StickyNote")
		{
			Point position = e.GetPosition(canvas);
			PdfStickyNoteAnnotation pdfStickyNoteAnnotation = new PdfStickyNoteAnnotation
			{
				PageIndex = num - 1,
				X = position.X / canvas.Width,
				Y = position.Y / canvas.Height,
				ColorHex = "#FCD34D",
				NoteText = "Nhập ghi chú nhanh..."
			};
			SaveUndoState();
			Annotations.Add(pdfStickyNoteAnnotation);
			SelectedAnnotation = pdfStickyNoteAnnotation;
			if (!IsContinuousTool(ActiveTool))
			{
				ActiveTool = "Select";
			}
			RedrawPageAnnotations(canvas, num);
			ShowStickyNoteEdit(canvas, pdfStickyNoteAnnotation, num);
			e.Handled = true;
		}
		else if (ActiveTool == "PlaceSignature")
		{
			Point position = e.GetPosition(canvas);
			if (_tempSignatureStrokes != null)
			{
				PdfSignatureAnnotation sigAnn = new PdfSignatureAnnotation
				{
					PageIndex = num - 1,
					X = (position.X - _tempSignatureWidth / 2.0) / canvas.Width,
					Y = (position.Y - _tempSignatureHeight / 2.0) / canvas.Height,
					Width = _tempSignatureWidth / canvas.Width,
					Height = _tempSignatureHeight / canvas.Height,
					OriginalWidth = _tempSignatureWidth,
					OriginalHeight = _tempSignatureHeight,
					SignatureType = "Handwrite",
					Strokes = _tempSignatureStrokes,
					StrokeColor = _tempSignatureColor,
					Thickness = 3.0
				};
				sigAnn.X = Math.Clamp(sigAnn.X, 0.0, 1.0 - sigAnn.Width);
				sigAnn.Y = Math.Clamp(sigAnn.Y, 0.0, 1.0 - sigAnn.Height);

				SaveUndoState();
				Annotations.Add(sigAnn);
				SelectedAnnotation = sigAnn;
				if (!IsContinuousTool(ActiveTool))
				{
					ActiveTool = "Select";
					_tempSignatureStrokes = null;
				}
				RedrawPageAnnotations(canvas, num);
			}
			e.Handled = true;
		}
		else if (ActiveTool == "PlaceImageSignature")
		{
			Point position = e.GetPosition(canvas);
			if (!string.IsNullOrEmpty(_tempSignatureImagePath))
			{
				double imgWidth = 150.0;
				double imgHeight = 100.0;
				PdfSignatureAnnotation imgAnn = new PdfSignatureAnnotation
				{
					PageIndex = num - 1,
					X = (position.X - imgWidth / 2.0) / canvas.Width,
					Y = (position.Y - imgHeight / 2.0) / canvas.Height,
					Width = imgWidth / canvas.Width,
					Height = imgHeight / canvas.Height,
					OriginalWidth = imgWidth,
					OriginalHeight = imgHeight,
					SignatureType = "Image",
					ImagePath = _tempSignatureImagePath,
					Thickness = 0.0
				};
				imgAnn.X = Math.Clamp(imgAnn.X, 0.0, 1.0 - imgAnn.Width);
				imgAnn.Y = Math.Clamp(imgAnn.Y, 0.0, 1.0 - imgAnn.Height);

				SaveUndoState();
				Annotations.Add(imgAnn);
				SelectedAnnotation = imgAnn;
				if (!IsContinuousTool(ActiveTool))
				{
					ActiveTool = "Select";
					_tempSignatureImagePath = null;
				}
				RedrawPageAnnotations(canvas, num);
			}
			e.Handled = true;
		}
		else if (ActiveTool == "PlaceStamp")
		{
			Point position = e.GetPosition(canvas);
			double stampWidth = 140.0;
			double stampHeight = 60.0;
			PdfSignatureAnnotation stampAnn = new PdfSignatureAnnotation
			{
				PageIndex = num - 1,
				X = (position.X - stampWidth / 2.0) / canvas.Width,
				Y = (position.Y - stampHeight / 2.0) / canvas.Height,
				Width = stampWidth / canvas.Width,
				Height = stampHeight / canvas.Height,
				OriginalWidth = stampWidth,
				OriginalHeight = stampHeight,
				SignatureType = "Stamp",
				StampText = _tempStampText ?? "ĐÃ DUYỆT",
				StrokeColor = ActiveStrokeColor,
				Thickness = 3.0
			};
			stampAnn.X = Math.Clamp(stampAnn.X, 0.0, 1.0 - stampAnn.Width);
			stampAnn.Y = Math.Clamp(stampAnn.Y, 0.0, 1.0 - stampAnn.Height);

			SaveUndoState();
			Annotations.Add(stampAnn);
			SelectedAnnotation = stampAnn;
			if (!IsContinuousTool(ActiveTool))
			{
				ActiveTool = "Select";
				_tempStampText = null;
			}
			RedrawPageAnnotations(canvas, num);
			e.Handled = true;
		}
		else if (ActiveTool == "MeasureDistance" || ActiveTool == "MeasureCalibrate")
		{
			_isDrawing = true;
			_drawStartPoint = e.GetPosition(canvas);
			_activeCanvas = canvas;
			canvas.CaptureMouse();
			_tempLine = new Line
			{
				Stroke = new SolidColorBrush(ActiveStrokeColor),
				StrokeThickness = 2.0,
				X1 = _drawStartPoint.X,
				Y1 = _drawStartPoint.Y,
				X2 = _drawStartPoint.X,
				Y2 = _drawStartPoint.Y
			};
			canvas.Children.Add(_tempLine);
			e.Handled = true;
		}
		else if (ActiveTool == "MeasureArea" || ActiveTool == "MeasurePerimeter")
		{
			Point position = e.GetPosition(canvas);
			if (_pendingAreaAnnotation == null || _activeCanvas != canvas)
			{
				_activeCanvas = canvas;
				_pendingAreaAnnotation = new PdfMeasurementAnnotation
				{
					PageIndex = num - 1,
					MeasurementType = ActiveTool == "MeasureArea" ? "Area" : "Perimeter",
					Scale = CurrentMeasurementScale,
					StrokeColor = ActiveStrokeColor,
					Thickness = 2.0
				};
				_pendingAreaAnnotation.Points.Add(new Point(position.X / canvas.Width, position.Y / canvas.Height));
				_pendingAreaAnnotation.Points.Add(new Point(position.X / canvas.Width, position.Y / canvas.Height));
				SaveUndoState();
				Annotations.Add(_pendingAreaAnnotation);
				SelectedAnnotation = _pendingAreaAnnotation;
				_isDrawing = true;
				canvas.CaptureMouse();
			}
			else
			{
				int ptCount = _pendingAreaAnnotation.Points.Count;
				Point firstPt = new Point(_pendingAreaAnnotation.Points[0].X * canvas.Width, _pendingAreaAnnotation.Points[0].Y * canvas.Height);
				Vector diff = position - firstPt;
				if (diff.Length < 12.0 && ptCount >= 4)
				{
					_pendingAreaAnnotation.Points.RemoveAt(ptCount - 1);
					_pendingAreaAnnotation = null;
					_isDrawing = false;
					if (canvas.IsMouseCaptured) canvas.ReleaseMouseCapture();
				}
				else
				{
					_pendingAreaAnnotation.Points.Insert(ptCount - 1, new Point(position.X / canvas.Width, position.Y / canvas.Height));
				}
			}
			RedrawPageAnnotations(canvas, num);
			e.Handled = true;
		}
	}

	private void OverlayCanvas_MouseMove(object sender, MouseEventArgs e)
	{
		if (!(sender is Canvas { Tag: var tag } canvas) || !(tag is int num))
		{
			return;
		}
		if (e.LeftButton != MouseButtonState.Pressed && IsMouseInteractionActive())
		{
			EndMouseInteraction(canvas);
		}
		else if (_isSelectingText && (ActiveTool == "SelectText" || ActiveTool == "Highlight"))
		{
			int charIndexAtMousePos = GetCharIndexAtMousePos(canvas, e.GetPosition(canvas), num);
			if (charIndexAtMousePos != -1)
			{
				_selectionEndPageIndex = num - 1;
				_selectionEndIndex = charIndexAtMousePos;
				RedrawAllPageAnnotations();
				// Update status with char count
				int startPg = Math.Min(_selectionStartPageIndex, _selectionEndPageIndex);
				int endPg = Math.Max(_selectionStartPageIndex, _selectionEndPageIndex);
				int charSpan = (endPg == startPg)
					? Math.Abs(_selectionEndIndex - _selectionStartIndex) + 1
					: 999;
				LogStatus($"Đã chọn ~{charSpan} ký tự. Nhấn Ctrl+C để sao chép.");
			}
		}
		else if (_isDraggingArrowHandle && SelectedAnnotation is PdfCalloutAnnotation pdfCalloutAnnotation)
		{
			Point position = e.GetPosition(canvas);
			double num2 = (position.X - _drawStartPoint.X) / canvas.Width;
			double num3 = (position.Y - _drawStartPoint.Y) / canvas.Height;
			pdfCalloutAnnotation.ArrowX = _dragStartArrowX + num2;
			pdfCalloutAnnotation.ArrowY = _dragStartArrowY + num3;
			RedrawPageAnnotations(canvas, num);
		}
		else if (_isDraggingLineStart && SelectedAnnotation is PdfShapeAnnotation pdfShapeAnnotation)
		{
			Point position2 = e.GetPosition(canvas);
			double num4 = (position2.X - _drawStartPoint.X) / canvas.Width;
			double num5 = (position2.Y - _drawStartPoint.Y) / canvas.Height;
			pdfShapeAnnotation.X = _dragStartAnnX + num4;
			pdfShapeAnnotation.Y = _dragStartAnnY + num5;
			ClampAnnotationToCanvas(pdfShapeAnnotation);
			RedrawPageAnnotations(canvas, num);
		}
		else if (_isDraggingLineEnd && SelectedAnnotation is PdfShapeAnnotation pdfShapeAnnotation2)
		{
			Point position3 = e.GetPosition(canvas);
			double num6 = (position3.X - _drawStartPoint.X) / canvas.Width;
			double num7 = (position3.Y - _drawStartPoint.Y) / canvas.Height;
			pdfShapeAnnotation2.EndX = _dragStartArrowX + num6;
			pdfShapeAnnotation2.EndY = _dragStartArrowY + num7;
			ClampAnnotationToCanvas(pdfShapeAnnotation2);
			RedrawPageAnnotations(canvas, num);
		}
		else if (_isDraggingAnn && SelectedAnnotation != null)
		{
			Point position4 = e.GetPosition(canvas);
			double num8 = (position4.X - _drawStartPoint.X) / canvas.Width;
			double num9 = (position4.Y - _drawStartPoint.Y) / canvas.Height;
			if (SelectedAnnotation is PdfShapeAnnotation { Type: ShapeType.Line } pdfShapeAnnotation3)
			{
				double num10 = pdfShapeAnnotation3.EndX - pdfShapeAnnotation3.X;
				double num11 = pdfShapeAnnotation3.EndY - pdfShapeAnnotation3.Y;
				pdfShapeAnnotation3.X = _dragStartAnnX + num8;
				pdfShapeAnnotation3.Y = _dragStartAnnY + num9;
				pdfShapeAnnotation3.EndX = pdfShapeAnnotation3.X + num10;
				pdfShapeAnnotation3.EndY = pdfShapeAnnotation3.Y + num11;
			}
			else
			{
				SelectedAnnotation.X = _dragStartAnnX + num8;
				SelectedAnnotation.Y = _dragStartAnnY + num9;
			}
			ClampAnnotationToCanvas(SelectedAnnotation);
			RedrawPageAnnotations(canvas, num);
		}
		else if (_isResizingAnn)
		{
			Point position5 = e.GetPosition(canvas);
			double num12 = (position5.X - _drawStartPoint.X) / canvas.Width;
			double num13 = (position5.Y - _drawStartPoint.Y) / canvas.Height;
			if (SelectedAnnotation is PdfTextBoxAnnotation pdfTextBoxAnnotation)
			{
				pdfTextBoxAnnotation.Width = Math.Max(40.0 / canvas.Width, _dragStartAnnWidth + num12);
				pdfTextBoxAnnotation.Height = Math.Max(25.0 / canvas.Height, _dragStartAnnHeight + num13);
				ClampAnnotationToCanvas(pdfTextBoxAnnotation);
			}
			else if (SelectedAnnotation is PdfSignatureAnnotation pdfSignatureAnnotation)
			{
				pdfSignatureAnnotation.Width = Math.Max(20.0 / canvas.Width, _dragStartAnnWidth + num12);
				pdfSignatureAnnotation.Height = Math.Max(10.0 / canvas.Height, _dragStartAnnHeight + num13);
				ClampAnnotationToCanvas(pdfSignatureAnnotation);
			}
			else if (SelectedAnnotation is PdfShapeAnnotation pdfShapeAnnotation4)
			{
				pdfShapeAnnotation4.Width = Math.Max(10.0 / canvas.Width, _dragStartAnnWidth + num12);
				pdfShapeAnnotation4.Height = Math.Max(10.0 / canvas.Height, _dragStartAnnHeight + num13);
				ClampAnnotationToCanvas(pdfShapeAnnotation4);
			}
			RedrawPageAnnotations(canvas, num);
		}
		else if (_isDrawing)
		{
			Point position6 = e.GetPosition(canvas);
			if ((ActiveTool == "TextBox" || ActiveTool == "Snapshot" || ActiveTool == "AiSnapshot" || ActiveTool == "ShapeRect") && _tempRect != null)
			{
				double length = Math.Min(_drawStartPoint.X, position6.X);
				double length2 = Math.Min(_drawStartPoint.Y, position6.Y);
				double width = Math.Abs(_drawStartPoint.X - position6.X);
				double height = Math.Abs(_drawStartPoint.Y - position6.Y);
				Canvas.SetLeft(_tempRect, length);
				Canvas.SetTop(_tempRect, length2);
				_tempRect.Width = width;
				_tempRect.Height = height;
			}
			else if (ActiveTool == "ShapeOval" && _tempEllipse != null)
			{
				double length3 = Math.Min(_drawStartPoint.X, position6.X);
				double length4 = Math.Min(_drawStartPoint.Y, position6.Y);
				double width2 = Math.Abs(_drawStartPoint.X - position6.X);
				double height2 = Math.Abs(_drawStartPoint.Y - position6.Y);
				Canvas.SetLeft(_tempEllipse, length3);
				Canvas.SetTop(_tempEllipse, length4);
				_tempEllipse.Width = width2;
				_tempEllipse.Height = height2;
			}
			else if ((ActiveTool == "Callout" || ActiveTool == "ShapeLine" || ActiveTool == "MeasureDistance" || ActiveTool == "MeasureCalibrate") && _tempLine != null)
			{
				_tempLine.X2 = position6.X;
				_tempLine.Y2 = position6.Y;
			}
			else if ((ActiveTool == "MeasureArea" || ActiveTool == "MeasurePerimeter") && _pendingAreaAnnotation != null)
			{
				int ptCount = _pendingAreaAnnotation.Points.Count;
				if (ptCount >= 2)
				{
					_pendingAreaAnnotation.Points[ptCount - 1] = new Point(position6.X / canvas.Width, position6.Y / canvas.Height);
					RedrawPageAnnotations(canvas, num);
				}
			}
			else if (ActiveTool == "Ink" && _tempPolyline != null)
			{
				_tempPolyline.Points.Add(position6);
			}
		}
	}

	private void OverlayCanvas_MouseUp(object sender, MouseButtonEventArgs e)
	{
		if (!(sender is Canvas { Tag: var tag } canvas) || !(tag is int num))
		{
			return;
		}
		if (_isSelectingText)
		{
			_isSelectingText = false;
			canvas.ReleaseMouseCapture();
			_selectedText = GetSelectedTextString();
			if (!string.IsNullOrEmpty(_selectedText))
			{
				if (ActiveTool == "Highlight")
				{
					HighlightSelectedText("#FFFF00");
					LogStatus("Đã tô màu (Highlight) vùng chữ được chọn.");
				}
				else
				{
					try
					{
						Clipboard.SetText(_selectedText);
						LogStatus($"Đã sao chép {_selectedText.Length} ký tự vào Clipboard (Ctrl+V để dán).");
					}
					catch
					{
						LogStatus("Đã chọn văn bản. Nhấn Ctrl+C để sao chép.");
					}
				}
			}
			else
			{
				LogStatus("Không lấy được văn bản. PDF này có thể là ảnh scan.");
			}
			e.Handled = true;
		}
		else if (_isDraggingArrowHandle || _isDraggingLineStart || _isDraggingLineEnd || _isDraggingAnn || _isResizingAnn)
		{
			EndMouseInteraction(canvas);
			e.Handled = true;
		}
		else
		{
			if (ActiveTool == "EditText")
			{
				LogToDesktop("[EDIT DEBUG] 1. MouseUp: Bắt đầu xử lý click EditText.");
				
				// [RẤT QUAN TRỌNG] Phải ép Canvas nhả chuột ra thì TextBox mới nhận được tiêu điểm
				if (canvas.IsMouseCaptured)
				{
					LogToDesktop("[EDIT DEBUG] 1.1 MouseUp: Đang ép Canvas ReleaseMouseCapture().");
					canvas.ReleaseMouseCapture();
				}

				int charIndexAtMousePos = GetCharIndexAtMousePos(canvas, e.GetPosition(canvas), num);
				LogToDesktop($"[EDIT DEBUG] 2. MouseUp: charIndexAtMousePos = {charIndexAtMousePos}");

				if (charIndexAtMousePos != -1)
				{
					ShowDirectTextEditOverlay(canvas, charIndexAtMousePos, num);
					e.Handled = true;
					return;
				}
				else
				{
					LogToDesktop("[EDIT DEBUG] 2.1 MouseUp: Không tìm thấy ký tự, có thể là PDF ảnh.");
					LogStatus("Vùng này không có chữ (PDF dạng ảnh/scan). Hãy thử công cụ OCR để nhận diện chữ trước.");
					e.Handled = true;
					return;
				}
			}

			if (!_isDrawing)
			{
				return;
			}
			if (ActiveTool == "MeasureArea" || ActiveTool == "MeasurePerimeter")
			{
				return;
			}
			_isDrawing = false;
			Point position = e.GetPosition(canvas);
			if (ActiveTool == "TextBox" && _tempRect != null)
			{
				canvas.Children.Remove(_tempRect);
				double x = Math.Min(_drawStartPoint.X, position.X);
				double y = Math.Min(_drawStartPoint.Y, position.Y);
				double w = Math.Max(40.0, Math.Abs(_drawStartPoint.X - position.X));
				double h = Math.Max(25.0, Math.Abs(_drawStartPoint.Y - position.Y));
				_tempRect = null;
				ShowTextBoxInput(canvas, x, y, w, h, num);
			}
			else if (ActiveTool == "Snapshot" && _tempRect != null)
			{
				double x2 = Math.Min(_drawStartPoint.X, position.X);
				double y2 = Math.Min(_drawStartPoint.Y, position.Y);
				double w2 = Math.Max(10.0, Math.Abs(_drawStartPoint.X - position.X));
				double h2 = Math.Max(10.0, Math.Abs(_drawStartPoint.Y - position.Y));
				Rectangle tempRect = _tempRect;
				_tempRect = null;
				if (!IsContinuousTool(ActiveTool))
				{
					ActiveTool = "Select";
				}
				EndMouseInteraction(canvas);
				PrintSnapshotSelection(canvas, tempRect, x2, y2, w2, h2, num);
			}
			else if (ActiveTool == "AiSnapshot" && _tempRect != null)
			{
				double x3 = Math.Min(_drawStartPoint.X, position.X);
				double y3 = Math.Min(_drawStartPoint.Y, position.Y);
				double w3 = Math.Max(10.0, Math.Abs(_drawStartPoint.X - position.X));
				double h3 = Math.Max(10.0, Math.Abs(_drawStartPoint.Y - position.Y));
				Rectangle tempRect2 = _tempRect;
				_tempRect = null;
				if (!IsContinuousTool(ActiveTool))
				{
					ActiveTool = "Select";
				}
				EndMouseInteraction(canvas);
				RequestAiSnapshot(canvas, tempRect2, x3, y3, w3, h3, num);
			}
			else if (ActiveTool == "ShapeRect" && _tempRect != null)
			{
				canvas.Children.Remove(_tempRect);
				double num2 = Math.Min(_drawStartPoint.X, position.X);
				double num3 = Math.Min(_drawStartPoint.Y, position.Y);
				double num4 = Math.Max(10.0, Math.Abs(_drawStartPoint.X - position.X));
				double num5 = Math.Max(10.0, Math.Abs(_drawStartPoint.Y - position.Y));
				_tempRect = null;
				PdfShapeAnnotation pdfShapeAnnotation = new PdfShapeAnnotation
				{
					PageIndex = num - 1,
					Type = ShapeType.Rectangle,
					X = num2 / canvas.Width,
					Y = num3 / canvas.Height,
					Width = num4 / canvas.Width,
					Height = num5 / canvas.Height,
					StrokeColor = ActiveStrokeColor,
					BgColor = ActiveBgColor,
					Opacity = ActiveOpacity,
					Thickness = 2.0
				};
				SaveUndoState();
				Annotations.Add(pdfShapeAnnotation);
				SelectedAnnotation = pdfShapeAnnotation;
				if (!IsContinuousTool(ActiveTool))
				{
					ActiveTool = "Select";
				}
				RedrawPageAnnotations(canvas, num);
			}
			else if (ActiveTool == "ShapeOval" && _tempEllipse != null)
			{
				canvas.Children.Remove(_tempEllipse);
				double num6 = Math.Min(_drawStartPoint.X, position.X);
				double num7 = Math.Min(_drawStartPoint.Y, position.Y);
				double num8 = Math.Max(10.0, Math.Abs(_drawStartPoint.X - position.X));
				double num9 = Math.Max(10.0, Math.Abs(_drawStartPoint.Y - position.Y));
				_tempEllipse = null;
				PdfShapeAnnotation pdfShapeAnnotation2 = new PdfShapeAnnotation
				{
					PageIndex = num - 1,
					Type = ShapeType.Oval,
					X = num6 / canvas.Width,
					Y = num7 / canvas.Height,
					Width = num8 / canvas.Width,
					Height = num9 / canvas.Height,
					StrokeColor = ActiveStrokeColor,
					BgColor = ActiveBgColor,
					Opacity = ActiveOpacity,
					Thickness = 2.0
				};
				SaveUndoState();
				Annotations.Add(pdfShapeAnnotation2);
				SelectedAnnotation = pdfShapeAnnotation2;
				if (!IsContinuousTool(ActiveTool))
				{
					ActiveTool = "Select";
				}
				RedrawPageAnnotations(canvas, num);
			}
			else if (ActiveTool == "Callout" && _tempLine != null)
			{
				canvas.Children.Remove(_tempLine);
				Point drawStartPoint = _drawStartPoint;
				double x4 = position.X;
				double y4 = position.Y;
				double w4 = 150.0;
				double h4 = 60.0;
				_tempLine = null;
				ShowCalloutTextBoxInput(canvas, drawStartPoint, x4, y4, w4, h4, num);
			}
			else if (ActiveTool == "ShapeLine" && _tempLine != null)
			{
				canvas.Children.Remove(_tempLine);
				_tempLine = null;
				PdfShapeAnnotation pdfShapeAnnotation3 = new PdfShapeAnnotation
				{
					PageIndex = num - 1,
					Type = ShapeType.Line,
					X = _drawStartPoint.X / canvas.Width,
					Y = _drawStartPoint.Y / canvas.Height,
					EndX = position.X / canvas.Width,
					EndY = position.Y / canvas.Height,
					StrokeColor = ActiveStrokeColor,
					Opacity = ActiveOpacity,
					Thickness = 2.0
				};
				SaveUndoState();
				Annotations.Add(pdfShapeAnnotation3);
				SelectedAnnotation = pdfShapeAnnotation3;
				if (!IsContinuousTool(ActiveTool))
				{
					ActiveTool = "Select";
				}
				RedrawPageAnnotations(canvas, num);
			}
			else if (ActiveTool == "MeasureDistance" && _tempLine != null)
			{
				canvas.Children.Remove(_tempLine);
				_tempLine = null;
				PdfMeasurementAnnotation measureAnn = new PdfMeasurementAnnotation
				{
					PageIndex = num - 1,
					MeasurementType = "Distance",
					Scale = CurrentMeasurementScale,
					X = _drawStartPoint.X / canvas.Width,
					Y = _drawStartPoint.Y / canvas.Height,
					StrokeColor = ActiveStrokeColor,
					Opacity = ActiveOpacity,
					Thickness = 2.0
				};
				measureAnn.Points.Add(new Point(_drawStartPoint.X / canvas.Width, _drawStartPoint.Y / canvas.Height));
				measureAnn.Points.Add(new Point(position.X / canvas.Width, position.Y / canvas.Height));

				SaveUndoState();
				Annotations.Add(measureAnn);
				SelectedAnnotation = measureAnn;
				if (!IsContinuousTool(ActiveTool))
				{
					ActiveTool = "Select";
				}
				RedrawPageAnnotations(canvas, num);
			}
			else if (ActiveTool == "MeasureCalibrate" && _tempLine != null)
			{
				canvas.Children.Remove(_tempLine);
				_tempLine = null;
				ActiveTool = "Select";

				Size pageSize = _pageDimensions[num - 1];
				double dxPoints = ((position.X - _drawStartPoint.X) / canvas.Width) * pageSize.Width;
				double dyPoints = ((position.Y - _drawStartPoint.Y) / canvas.Height) * pageSize.Height;
				double distPoints = Math.Sqrt(dxPoints * dxPoints + dyPoints * dyPoints);
				double distMm = distPoints * 0.352777;

				if (distPoints > 0)
				{
					string? input = InputDialog.Show("Hiệu chuẩn tỷ lệ", "Nhập khoảng cách thực tế đo được (mét):", "5.0");
					if (input != null && double.TryParse(input, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double realMeters) && realMeters > 0)
					{
						double calculatedScale = (realMeters * 1000.0) / distMm;
						CurrentMeasurementScale = calculatedScale;
						ScaleCalibrated?.Invoke(this, calculatedScale);
					}
				}
				RedrawPageAnnotations(canvas, num);
			}
			else if (ActiveTool == "Ink" && _tempPolyline != null)
			{
				canvas.Children.Remove(_tempPolyline);
				List<string> list = new List<string>();
				foreach (Point point in _tempPolyline.Points)
				{
					list.Add($"{point.X / canvas.Width:F4},{point.Y / canvas.Height:F4}");
				}
				_tempPolyline = null;
				if (list.Count > 1)
				{
					PdfInkAnnotation pdfInkAnnotation = new PdfInkAnnotation
					{
						PageIndex = num - 1,
						Points = string.Join(";", list),
						StrokeColor = ActiveStrokeColor,
						Opacity = ActiveOpacity,
						Thickness = 2.0
					};
					SaveUndoState();
					Annotations.Add(pdfInkAnnotation);
					SelectedAnnotation = pdfInkAnnotation;
				}
				if (!IsContinuousTool(ActiveTool))
				{
					ActiveTool = "Select";
				}
				RedrawPageAnnotations(canvas, num);
			}
			e.Handled = true;
			EndMouseInteraction(canvas);
		}
	}

	private void PrintSnapshotSelection(Canvas canvas, Rectangle? tempRect, double x, double y, double w, double h, int pageNumber)
	{
		if (string.IsNullOrEmpty(CurrentPdfPath))
		{
			if (tempRect != null)
			{
				canvas.Children.Remove(tempRect);
			}
			return;
		}
		if (w < 10.0 || h < 10.0)
		{
			if (tempRect != null)
			{
				canvas.Children.Remove(tempRect);
			}
			LogStatus("Snapshot selection is too small.");
			return;
		}
		PdfSnapshotSelection snapshot = new PdfSnapshotSelection(CurrentPdfPath, pageNumber - 1, Math.Clamp(x / canvas.Width, 0.0, 1.0), Math.Clamp(y / canvas.Height, 0.0, 1.0), Math.Clamp(w / canvas.Width, 0.001, 1.0), Math.Clamp(h / canvas.Height, 0.001, 1.0));
		SnapshotActionDialog snapshotActionDialog = new SnapshotActionDialog(pageNumber, snapshot)
		{
			Owner = Window.GetWindow(this)
		};
		bool? flag = snapshotActionDialog.ShowDialog();
		if (tempRect != null)
		{
			canvas.Children.Remove(tempRect);
		}
		if (flag != true)
		{
			LogStatus("Snapshot selected");
			return;
		}
		if (snapshotActionDialog.SelectedAction == SnapshotAction.CopyImage)
		{
			CopySnapshotImage(snapshot);
			return;
		}
		if (snapshotActionDialog.SelectedAction == SnapshotAction.SavePng)
		{
			SaveSnapshotPng(snapshot, pageNumber);
			return;
		}
		if (snapshotActionDialog.SelectedAction != SnapshotAction.Print)
		{
			LogStatus("Snapshot selected");
			return;
		}
		PrintOptionsDialog printOptionsDialog = new PrintOptionsDialog(1, 1, CurrentPdfPath)
		{
			Owner = Window.GetWindow(this),
			SnapshotSelection = snapshot
		};
		if (printOptionsDialog.ShowDialog() != true || printOptionsDialog.SelectedPrintQueue == null)
		{
			return;
		}
		try
		{
			PrintTicket printTicket = printOptionsDialog.SelectedPrintTicket ?? printOptionsDialog.SelectedPrintQueue.UserPrintTicket ?? printOptionsDialog.SelectedPrintQueue.DefaultPrintTicket ?? new PrintTicket();
			PageMediaSize pageMediaSize = printOptionsDialog.CreatePageMediaSize();
			if (pageMediaSize != null)
			{
				printTicket.PageMediaSize = pageMediaSize;
			}
			PageOrientation? pageOrientation = printOptionsDialog.CreatePageOrientation();
			if (pageOrientation.HasValue)
			{
				printTicket.PageOrientation = pageOrientation;
			}
			int num = Math.Clamp((int)Math.Round(printOptionsDialog.PrintDpi), 72, 1200);
			printTicket.PageResolution = new PageResolution(num, num);
			printTicket.CopyCount = printOptionsDialog.Copies;
			PrinterPrintProfile printerPrintProfile = PrinterPrintProfile.Resolve(printOptionsDialog.SelectedPrintQueue);
			PdfSnapshotPrinter.PrintSnapshot(snapshot, printOptionsDialog.SelectedPrintQueue, printTicket, printerPrintProfile.RightSafetyPadding, printerPrintProfile.BottomSafetyPadding);
			LogStatus("Snapshot print job sent");
		}
		catch (Exception ex)
		{
			MessageBox.Show("Snapshot print failed: " + ex.Message, "Snapshot", MessageBoxButton.OK, MessageBoxImage.Hand);
			LogStatus("Snapshot print failed");
		}
	}

	private void CopySnapshotImage(PdfSnapshotSelection snapshot)
	{
		try
		{
			LogToDesktop("Snapshot copy image start");
			BitmapSource bitmapSource = PdfSnapshotImageRenderer.RenderSnapshotToBitmap(snapshot);
			Clipboard.SetImage(bitmapSource);
			LogToDesktop($"Snapshot copy image done: {bitmapSource.PixelWidth}x{bitmapSource.PixelHeight}");
			LogStatus("Snapshot image copied to clipboard");
		}
		catch (Exception ex)
		{
			MessageBox.Show("Copy snapshot failed: " + ex.Message, "Snapshot", MessageBoxButton.OK, MessageBoxImage.Hand);
			LogStatus("Snapshot copy failed");
		}
	}

	private void SaveSnapshotPng(PdfSnapshotSelection snapshot, int pageNumber)
	{
		SaveFileDialog saveFileDialog = new SaveFileDialog
		{
			Filter = "PNG image (*.png)|*.png",
			Title = "Save snapshot as PNG",
			FileName = $"{System.IO.Path.GetFileNameWithoutExtension(CurrentPdfPath)}_p{pageNumber}_snapshot.png",
			InitialDirectory = (string.IsNullOrWhiteSpace(CurrentPdfPath) ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) : System.IO.Path.GetDirectoryName(CurrentPdfPath))
		};
		if (saveFileDialog.ShowDialog() != true)
		{
			LogStatus("Snapshot selected");
			return;
		}
		try
		{
			LogToDesktop("Snapshot save PNG start: " + saveFileDialog.FileName);
			byte[] array = PdfSnapshotImageRenderer.RenderSnapshotToPngBytes(snapshot);
			File.WriteAllBytes(saveFileDialog.FileName, array);
			LogToDesktop($"Snapshot save PNG done: {array.Length:N0} bytes");
			LogStatus("Snapshot PNG saved");
		}
		catch (Exception ex)
		{
			MessageBox.Show("Save snapshot failed: " + ex.Message, "Snapshot", MessageBoxButton.OK, MessageBoxImage.Hand);
			LogStatus("Snapshot save failed");
		}
	}

	private void RequestAiSnapshot(Canvas canvas, Rectangle? tempRect, double x, double y, double w, double h, int pageNumber)
	{
		if (string.IsNullOrEmpty(CurrentPdfPath))
		{
			if (tempRect != null)
			{
				canvas.Children.Remove(tempRect);
			}
			return;
		}
		if (w < 10.0 || h < 10.0)
		{
			if (tempRect != null)
			{
				canvas.Children.Remove(tempRect);
			}
			LogStatus("AI snapshot selection is too small.");
			return;
		}
		try
		{
			PdfSnapshotSelection pdfSnapshotSelection = new PdfSnapshotSelection(CurrentPdfPath, pageNumber - 1, Math.Clamp(x / canvas.Width, 0.0, 1.0), Math.Clamp(y / canvas.Height, 0.0, 1.0), Math.Clamp(w / canvas.Width, 0.001, 1.0), Math.Clamp(h / canvas.Height, 0.001, 1.0));
			string pngBase = AiSnapshotImageRenderer.RenderSnapshotToPngBase64(pdfSnapshotSelection);
			AiSnapshotRequest e = new AiSnapshotRequest(string.Empty, pngBase, pageNumber, pdfSnapshotSelection.X, pdfSnapshotSelection.Y, pdfSnapshotSelection.Width, pdfSnapshotSelection.Height);
			this.AiSnapshotRequested?.Invoke(this, e);
			LogStatus("AI snapshot captured");
		}
		catch (Exception ex)
		{
			MessageBox.Show("AI snapshot failed: " + ex.Message, "AI Snapshot", MessageBoxButton.OK, MessageBoxImage.Hand);
			LogStatus("AI snapshot failed");
		}
		finally
		{
			if (tempRect != null)
			{
				canvas.Children.Remove(tempRect);
			}
		}
	}

	private void ShowTextBoxInput(Canvas canvas, double x, double y, double w, double h, int pageNumber)
	{
		System.Windows.Controls.TextBox tbInput = new System.Windows.Controls.TextBox
		{
			Width = w,
			Height = h,
			Text = "Nhập ghi chú...",
			FontFamily = new FontFamily(ActiveFontFamily),
			FontSize = ActiveFontSize,
			FontWeight = (ActiveIsBold ? FontWeights.Bold : FontWeights.Normal),
			FontStyle = (ActiveIsItalic ? FontStyles.Italic : FontStyles.Normal),
			Foreground = new SolidColorBrush(ActiveStrokeColor),
			TextWrapping = TextWrapping.Wrap,
			AcceptsReturn = true,
			BorderBrush = Brushes.DodgerBlue,
			BorderThickness = new Thickness(1.5)
		};
		Canvas.SetLeft(tbInput, x);
		Canvas.SetTop(tbInput, y);
		canvas.Children.Add(tbInput);
		tbInput.Focus();
		tbInput.SelectAll();
		tbInput.LostFocus += delegate
		{
			string text = tbInput.Text.Trim();
			canvas.Children.Remove(tbInput);
			if (!string.IsNullOrEmpty(text) && text != "Nhập ghi chú...")
			{
				PdfTextBoxAnnotation pdfTextBoxAnnotation = new PdfTextBoxAnnotation
				{
					PageIndex = pageNumber - 1,
					X = x / canvas.Width,
					Y = y / canvas.Height,
					Width = w / canvas.Width,
					Height = h / canvas.Height,
					Text = text,
					FontFamily = ActiveFontFamily,
					FontSize = ActiveFontSize,
					IsBold = ActiveIsBold,
					IsItalic = ActiveIsItalic,
					IsUnderline = ActiveIsUnderline,
					StrokeColor = ActiveStrokeColor,
					BgColor = ActiveBgColor,
					Opacity = ActiveOpacity
				};
				SaveUndoState();
				Annotations.Add(pdfTextBoxAnnotation);
				SelectedAnnotation = pdfTextBoxAnnotation;
				if (!IsContinuousTool(ActiveTool))
				{
					ActiveTool = "Select";
					LogStatus("Đã tạo hộp văn bản. Công cụ đã quay về Select để kéo hoặc co giãn khung.");
				}
			}
			RedrawPageAnnotations(canvas, pageNumber);
		};
		tbInput.KeyDown += delegate(object s, KeyEventArgs ev)
		{
			if (ev.Key == Key.Return && Keyboard.Modifiers != ModifierKeys.Shift)
			{
				tbInput.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
			}
			else if (ev.Key == Key.Escape)
			{
				canvas.Children.Remove(tbInput);
				ActiveTool = "Select";
				RedrawPageAnnotations(canvas, pageNumber);
			}
		};
	}

	private void ShowCalloutTextBoxInput(Canvas canvas, Point arrowTip, double x, double y, double w, double h, int pageNumber)
	{
		System.Windows.Controls.TextBox tbInput = new System.Windows.Controls.TextBox
		{
			Width = w,
			Height = h,
			Text = "Nhập ghi chú...",
			FontFamily = new FontFamily(ActiveFontFamily),
			FontSize = ActiveFontSize,
			FontWeight = (ActiveIsBold ? FontWeights.Bold : FontWeights.Normal),
			FontStyle = (ActiveIsItalic ? FontStyles.Italic : FontStyles.Normal),
			Foreground = new SolidColorBrush(ActiveStrokeColor),
			TextWrapping = TextWrapping.Wrap,
			AcceptsReturn = true,
			BorderBrush = Brushes.DodgerBlue,
			BorderThickness = new Thickness(1.5)
		};
		Canvas.SetLeft(tbInput, x);
		Canvas.SetTop(tbInput, y);
		canvas.Children.Add(tbInput);
		tbInput.Focus();
		tbInput.SelectAll();
		tbInput.LostFocus += delegate
		{
			string text = tbInput.Text.Trim();
			canvas.Children.Remove(tbInput);
			if (!string.IsNullOrEmpty(text) && text != "Nhập ghi chú...")
			{
				PdfCalloutAnnotation pdfCalloutAnnotation = new PdfCalloutAnnotation
				{
					PageIndex = pageNumber - 1,
					X = x / canvas.Width,
					Y = y / canvas.Height,
					Width = w / canvas.Width,
					Height = h / canvas.Height,
					Text = text,
					ArrowX = arrowTip.X / canvas.Width,
					ArrowY = arrowTip.Y / canvas.Height,
					FontFamily = ActiveFontFamily,
					FontSize = ActiveFontSize,
					IsBold = ActiveIsBold,
					IsItalic = ActiveIsItalic,
					IsUnderline = ActiveIsUnderline,
					StrokeColor = ActiveStrokeColor,
					BgColor = ActiveBgColor,
					Opacity = ActiveOpacity
				};
				SaveUndoState();
				Annotations.Add(pdfCalloutAnnotation);
				SelectedAnnotation = pdfCalloutAnnotation;
				if (!IsContinuousTool(ActiveTool))
				{
					ActiveTool = "Select";
					LogStatus("Đã tạo mũi tên chỉ dẫn. Công cụ đã quay về Select; muốn tạo mũi tên nữa hãy chọn lại.");
				}
			}
			RedrawPageAnnotations(canvas, pageNumber);
		};
		tbInput.KeyDown += delegate(object s, KeyEventArgs ev)
		{
			if (ev.Key == Key.Return && Keyboard.Modifiers != ModifierKeys.Shift)
			{
				tbInput.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
			}
			else if (ev.Key == Key.Escape)
			{
				canvas.Children.Remove(tbInput);
				ActiveTool = "Select";
				RedrawPageAnnotations(canvas, pageNumber);
			}
		};
	}

	private bool IsMouseInteractionActive()
	{
		if (!_isDraggingAnn && !_isDraggingArrowHandle && !_isDraggingLineStart && !_isDraggingLineEnd && !_isResizingAnn)
		{
			return _isDrawing;
		}
		return true;
	}

	private void EndMouseInteraction(Canvas canvas)
	{
		_isDraggingAnn = false;
		_isDraggingArrowHandle = false;
		_isDraggingLineStart = false;
		_isDraggingLineEnd = false;
		_isResizingAnn = false;
		if (_isDrawing)
		{
			if (_tempRect != null)
			{
				canvas.Children.Remove(_tempRect);
				_tempRect = null;
			}
			if (_tempLine != null)
			{
				canvas.Children.Remove(_tempLine);
				_tempLine = null;
			}
			if (_tempPolyline != null)
			{
				canvas.Children.Remove(_tempPolyline);
				_tempPolyline = null;
			}
			if (_tempEllipse != null)
			{
				canvas.Children.Remove(_tempEllipse);
				_tempEllipse = null;
			}
		}
		_isDrawing = false;
		_activeCanvas = null;
		if (canvas.IsMouseCaptured)
		{
			canvas.ReleaseMouseCapture();
		}
	}

	private static void ClampAnnotationToCanvas(PdfAnnotation annotation)
	{
		if (annotation is PdfTextBoxAnnotation pdfTextBoxAnnotation)
		{
			pdfTextBoxAnnotation.Width = Math.Clamp(pdfTextBoxAnnotation.Width, 0.001, 1.0);
			pdfTextBoxAnnotation.Height = Math.Clamp(pdfTextBoxAnnotation.Height, 0.001, 1.0);
			annotation.X = Math.Clamp(annotation.X, 0.0, Math.Max(0.0, 1.0 - pdfTextBoxAnnotation.Width));
			annotation.Y = Math.Clamp(annotation.Y, 0.0, Math.Max(0.0, 1.0 - pdfTextBoxAnnotation.Height));
		}
		else if (annotation is PdfShapeAnnotation { Type: not ShapeType.Line } pdfShapeAnnotation)
		{
			pdfShapeAnnotation.Width = Math.Clamp(pdfShapeAnnotation.Width, 0.001, 1.0);
			pdfShapeAnnotation.Height = Math.Clamp(pdfShapeAnnotation.Height, 0.001, 1.0);
			annotation.X = Math.Clamp(annotation.X, 0.0, Math.Max(0.0, 1.0 - pdfShapeAnnotation.Width));
		}
		else if (annotation is PdfSignatureAnnotation pdfSignatureAnnotation)
		{
			pdfSignatureAnnotation.Width = Math.Clamp(pdfSignatureAnnotation.Width, 0.001, 1.0);
			pdfSignatureAnnotation.Height = Math.Clamp(pdfSignatureAnnotation.Height, 0.001, 1.0);
			annotation.X = Math.Clamp(annotation.X, 0.0, Math.Max(0.0, 1.0 - pdfSignatureAnnotation.Width));
			annotation.Y = Math.Clamp(annotation.Y, 0.0, Math.Max(0.0, 1.0 - pdfSignatureAnnotation.Height));
		}
		else if (annotation is PdfShapeAnnotation { Type: ShapeType.Line } pdfShapeAnnotation2)
		{
			annotation.X = Math.Clamp(annotation.X, 0.0, 1.0);
			annotation.Y = Math.Clamp(annotation.Y, 0.0, 1.0);
			pdfShapeAnnotation2.EndX = Math.Clamp(pdfShapeAnnotation2.EndX, 0.0, 1.0);
			pdfShapeAnnotation2.EndY = Math.Clamp(pdfShapeAnnotation2.EndY, 0.0, 1.0);
		}
		else
		{
			annotation.X = Math.Clamp(annotation.X, 0.0, 1.0);
			annotation.Y = Math.Clamp(annotation.Y, 0.0, 1.0);
		}
	}

	private void ShowStickyNoteEdit(Canvas canvas, PdfStickyNoteAnnotation sticky, int pageNumber)
	{
		double num = 240.0;
		double num2 = 120.0;
		double num3 = sticky.X * canvas.Width;
		double num4 = sticky.Y * canvas.Height;
		if (num3 + num > canvas.Width)
		{
			num3 = canvas.Width - num - 10.0;
		}
		if (num4 + num2 > canvas.Height)
		{
			num4 = canvas.Height - num2 - 10.0;
		}
		num3 = Math.Max(10.0, num3);
		num4 = Math.Max(10.0, num4);
		Border editorBorder = new Border
		{
			Width = num,
			Height = num2,
			Background = (Brush)new BrushConverter().ConvertFromString("#FEF3C7"),
			BorderBrush = (Brush)new BrushConverter().ConvertFromString("#D97706"),
			BorderThickness = new Thickness(1.5),
			CornerRadius = new CornerRadius(4.0),
			Tag = "StickyNoteEditor"
		};
		StackPanel stackPanel = new StackPanel();
		editorBorder.Child = stackPanel;
		TextBlock element = new TextBlock
		{
			Text = "Ghi chú nhanh",
			FontWeight = FontWeights.Bold,
			FontSize = 11.0,
			Foreground = (Brush)new BrushConverter().ConvertFromString("#B45309"),
			Margin = new Thickness(6.0, 4.0, 6.0, 2.0)
		};
		stackPanel.Children.Add(element);
		System.Windows.Controls.TextBox tbInput = new System.Windows.Controls.TextBox
		{
			Width = num - 12.0,
			Height = num2 - 32.0,
			Text = ((sticky.NoteText == "Nhập ghi chú nhanh...") ? "" : sticky.NoteText),
			FontFamily = new FontFamily("Segoe UI"),
			FontSize = 12.0,
			TextWrapping = TextWrapping.Wrap,
			AcceptsReturn = true,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			BorderThickness = new Thickness(0.0),
			Background = Brushes.Transparent,
			Margin = new Thickness(6.0, 0.0, 6.0, 6.0)
		};
		stackPanel.Children.Add(tbInput);
		Canvas.SetLeft(editorBorder, num3);
		Canvas.SetTop(editorBorder, num4);
		canvas.Children.Add(editorBorder);
		tbInput.Focus();
		tbInput.SelectAll();
		tbInput.LostFocus += delegate
		{
			string text = tbInput.Text.Trim();
			canvas.Children.Remove(editorBorder);
			if (!string.IsNullOrEmpty(text))
			{
				SaveUndoState();
				sticky.NoteText = text;
			}
			RedrawPageAnnotations(canvas, pageNumber);
		};
		tbInput.KeyDown += delegate(object s, KeyEventArgs ev)
		{
			if (ev.Key == Key.Escape)
			{
				canvas.Children.Remove(editorBorder);
				RedrawPageAnnotations(canvas, pageNumber);
			}
		};
	}

	private void ShowEditTextBoxInput(Canvas canvas, PdfTextBoxAnnotation tb, int pageNumber)
	{
		double width = tb.Width * canvas.Width;
		double height = tb.Height * canvas.Height;
		double length = tb.X * canvas.Width;
		double length2 = tb.Y * canvas.Height;
		System.Windows.Controls.TextBox tbInput = new System.Windows.Controls.TextBox
		{
			Width = width,
			Height = height,
			Text = tb.Text,
			FontFamily = new FontFamily(tb.FontFamily),
			FontSize = tb.FontSize,
			FontWeight = (tb.IsBold ? FontWeights.Bold : FontWeights.Normal),
			FontStyle = (tb.IsItalic ? FontStyles.Italic : FontStyles.Normal),
			Foreground = new SolidColorBrush(tb.StrokeColor),
			TextWrapping = TextWrapping.Wrap,
			AcceptsReturn = true,
			BorderBrush = Brushes.DodgerBlue,
			BorderThickness = new Thickness(1.5)
		};
		Canvas.SetLeft(tbInput, length);
		Canvas.SetTop(tbInput, length2);
		canvas.Children.Add(tbInput);
		tbInput.Focus();
		tbInput.SelectAll();
		tbInput.LostFocus += delegate
		{
			SaveUndoState();
			tb.Text = tbInput.Text.Trim();
			canvas.Children.Remove(tbInput);
			RedrawPageAnnotations(canvas, pageNumber);
		};
		tbInput.KeyDown += delegate(object s, KeyEventArgs ev)
		{
			if (ev.Key == Key.Return && Keyboard.Modifiers != ModifierKeys.Shift)
			{
				tbInput.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
			}
			else if (ev.Key == Key.Escape)
			{
				canvas.Children.Remove(tbInput);
				RedrawPageAnnotations(canvas, pageNumber);
			}
		};
	}

	public void CopySelectedAnnotation()
	{
		if (SelectedAnnotation != null)
		{
			_copiedAnnotation = CloneAnnotation(SelectedAnnotation);
			LogStatus("Đã sao chép chú thích");
		}
	}

	public void PasteAnnotation(bool inPlace)
	{
		if (_copiedAnnotation == null)
		{
			LogStatus("Không có chú thích nào để dán");
			return;
		}
		PdfAnnotation pdfAnnotation = CloneAnnotation(_copiedAnnotation);
		if (pdfAnnotation == null)
		{
			return;
		}
		pdfAnnotation.PageIndex = SelectedPageNumber - 1;
		if (!inPlace)
		{
			pdfAnnotation.X = Math.Clamp(pdfAnnotation.X + 0.02, 0.0, 0.9);
			pdfAnnotation.Y = Math.Clamp(pdfAnnotation.Y + 0.02, 0.0, 0.9);
			if (pdfAnnotation is PdfCalloutAnnotation pdfCalloutAnnotation)
			{
				pdfCalloutAnnotation.ArrowX = Math.Clamp(pdfCalloutAnnotation.ArrowX + 0.02, 0.0, 0.9);
				pdfCalloutAnnotation.ArrowY = Math.Clamp(pdfCalloutAnnotation.ArrowY + 0.02, 0.0, 0.9);
			}
		}
		SaveUndoState();
		Annotations.Add(pdfAnnotation);
		SelectedAnnotation = pdfAnnotation;
		RedrawAllPageAnnotations();
		LogStatus(inPlace ? "Đã dán chú thích tại chỗ" : "Đã dán chú thích");
	}

	private PdfAnnotation? CloneAnnotation(PdfAnnotation source)
	{
		PdfAnnotation pdfAnnotation;
		if (source is PdfCalloutAnnotation pdfCalloutAnnotation)
		{
			pdfAnnotation = new PdfCalloutAnnotation
			{
				Width = pdfCalloutAnnotation.Width,
				Height = pdfCalloutAnnotation.Height,
				Text = pdfCalloutAnnotation.Text,
				ArrowX = pdfCalloutAnnotation.ArrowX,
				ArrowY = pdfCalloutAnnotation.ArrowY
			};
		}
		else if (source is PdfTextBoxAnnotation pdfTextBoxAnnotation)
		{
			pdfAnnotation = new PdfTextBoxAnnotation
			{
				Width = pdfTextBoxAnnotation.Width,
				Height = pdfTextBoxAnnotation.Height,
				Text = pdfTextBoxAnnotation.Text
			};
		}
		else if (source is PdfInkAnnotation pdfInkAnnotation)
		{
			pdfAnnotation = new PdfInkAnnotation
			{
				Points = pdfInkAnnotation.Points,
				Thickness = pdfInkAnnotation.Thickness
			};
		}
		else if (source is PdfShapeAnnotation pdfShapeAnnotation)
		{
			pdfAnnotation = new PdfShapeAnnotation
			{
				Type = pdfShapeAnnotation.Type,
				Width = pdfShapeAnnotation.Width,
				Height = pdfShapeAnnotation.Height,
				Thickness = pdfShapeAnnotation.Thickness,
				EndX = pdfShapeAnnotation.EndX,
				EndY = pdfShapeAnnotation.EndY
			};
		}
		else
		{
			if (!(source is PdfStickyNoteAnnotation pdfStickyNoteAnnotation))
			{
				return null;
			}
			pdfAnnotation = new PdfStickyNoteAnnotation
			{
				NoteText = pdfStickyNoteAnnotation.NoteText,
				ColorHex = pdfStickyNoteAnnotation.ColorHex
			};
		}
		pdfAnnotation.X = source.X;
		pdfAnnotation.Y = source.Y;
		pdfAnnotation.PageIndex = source.PageIndex;
		pdfAnnotation.StrokeColor = source.StrokeColor;
		pdfAnnotation.FontFamily = source.FontFamily;
		pdfAnnotation.FontSize = source.FontSize;
		pdfAnnotation.IsBold = source.IsBold;
		pdfAnnotation.IsItalic = source.IsItalic;
		pdfAnnotation.IsUnderline = source.IsUnderline;
		pdfAnnotation.BgColor = source.BgColor;
		pdfAnnotation.Opacity = source.Opacity;
		return pdfAnnotation;
	}

	private void StoreBitmap(string key, BitmapSource bitmap)
	{
		_cacheManager.StoreBitmap(key, bitmap);
	}

	private void DocContextMenu_Opened(object sender, RoutedEventArgs e)
	{
		MenuFitWidth.IsChecked = Math.Abs(CurrentZoom - _baseZoomForLayout) < 0.01;
		MenuRulers.IsChecked = _isRulersEnabled;
		MenuGuides.IsChecked = _isGuidesEnabled;
		MenuReverseView.IsChecked = _isReverseView;
		MenuReadMode.IsChecked = _isReadMode;
		MenuFullScreen.IsChecked = _isFullScreen;
		MenuHideNavigation.IsChecked = !_isSidebarVisible;
	}

	private void ContextPaste_Click(object sender, RoutedEventArgs e)
	{
		PasteAnnotation(inPlace: false);
	}

	private void ContextPasteInPlace_Click(object sender, RoutedEventArgs e)
	{
		PasteAnnotation(inPlace: true);
	}

	public void ContextReadMode_Click(object sender, RoutedEventArgs e)
	{
		_isReadMode = !_isReadMode;
		LogStatus(_isReadMode ? "Đã bật chế độ đọc" : "Đã tắt chế độ đọc");
		if (Window.GetWindow(this) is MainWindow mainWindow)
		{
			UIElement uIElement = (mainWindow.FindName("RibbonBar") as UIElement) ?? (mainWindow.Template.FindName("RibbonBar", mainWindow) as UIElement);
			if (uIElement == null)
			{
				uIElement = FindVisualChild<Ribbon>(mainWindow);
			}
			if (uIElement != null)
			{
				uIElement.Visibility = (_isReadMode ? Visibility.Collapsed : Visibility.Visible);
			}
		}
	}

	private T? FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
	{
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(obj, i);
			if (child is T result)
			{
				return result;
			}
			T val = FindVisualChild<T>(child);
			if (val != null)
			{
				return val;
			}
		}
		return null;
	}

	public void ContextFullScreen_Click(object sender, RoutedEventArgs e)
	{
		_isFullScreen = !_isFullScreen;
		Window window = Window.GetWindow(this);
		if (window != null)
		{
			if (_isFullScreen)
			{
				window.WindowStyle = WindowStyle.None;
				window.WindowState = WindowState.Maximized;
			}
			else
			{
				window.WindowStyle = WindowStyle.SingleBorderWindow;
				window.WindowState = WindowState.Maximized;
			}
		}
		LogStatus(_isFullScreen ? "Đã mở toàn màn hình" : "Đã thoát toàn màn hình");
	}

	private void ContextZoomIn_Click(object sender, RoutedEventArgs e)
	{
		ChangeZoom(1.08);
	}

	private void ContextZoomOut_Click(object sender, RoutedEventArgs e)
	{
		ChangeZoom(0.9259259259259258);
	}

	private void ContextActualSize_Click(object sender, RoutedEventArgs e)
	{
		SetZoomPercent(100.0);
	}

	private void ContextFitPage_Click(object sender, RoutedEventArgs e)
	{
		if (PageCount > 0 && _pageDimensions.Count != 0)
		{
			double value = (DocumentScrollViewer.ViewportHeight - 60.0) / (_pageDimensions[0].Height * 1.33);
			CurrentZoom = Math.Clamp(value, 0.1, 4.0);
			_baseZoomForLayout = CurrentZoom;
			_targetZoom = CurrentZoom;
			_smoothZoomTimer?.Stop();
			ResetZoomPreviewTransform();
			_pendingZoomContentPoint = null;
			_pendingZoomHostPoint = null;
			_pendingZoomViewportPoint = null;
			_resetScrollAfterRender = true;
			ReportZoomChanged();
			RenderPdfPages();
			LogStatus("Vừa khít trang");
		}
	}

	private void ContextFitWidth_Click(object sender, RoutedEventArgs e)
	{
		FitWidth();
	}

	private void ContextRotateRight_Click(object sender, RoutedEventArgs e)
	{
		RotateSelectedPageAsync(90);
	}

	private void ForceRedrawAllPages()
	{
		_renderGeneration++;
		ClearRenderQueue();

		// Clear main pages
		if (PagesHost.Children.Count > 0 && PagesHost.Children[0] is StackPanel stackPanel)
		{
			foreach (UIElement child in stackPanel.Children)
			{
				if (child is Border border && border.Child is Grid grid)
				{
					Image image = grid.Children.OfType<Image>().FirstOrDefault();
					if (image != null)
					{
						image.Source = null;
					}
				}
			}
		}

		// Clear thumbnails
		foreach (UIElement child in ThumbnailContainer.Children)
		{
			if (child is Border border && border.Child is Image image)
			{
				image.Source = null;
			}
		}

		UpdateSelectedPageFromViewport();
	}

	private void ContextReverseView_Click(object sender, RoutedEventArgs e)
	{
		_isReverseView = !_isReverseView;
		if (_isReverseView)
		{
			SolidColorBrush background = new SolidColorBrush(Color.FromRgb(24, 24, 24));
			PagesHost.Background = background;
			LogStatus("Bật xem đảo ngược");
		}
		else
		{
			PagesHost.Background = Brushes.Transparent;
			LogStatus("Tắt xem đảo ngược");
		}
		ForceRedrawAllPages();
	}

	private void ContextPrint_Click(object sender, RoutedEventArgs e)
	{
		PrintPdf();
	}

	public void ContextRulers_Click(object sender, RoutedEventArgs e)
	{
		_isRulersEnabled = !_isRulersEnabled;
		LogStatus(_isRulersEnabled ? "Hiện thước đo" : "Ẩn thước đo");

		Visibility visibility = _isRulersEnabled ? Visibility.Visible : Visibility.Collapsed;
		if (HorizontalRulerBorder != null) HorizontalRulerBorder.Visibility = visibility;
		if (VerticalRulerBorder != null) VerticalRulerBorder.Visibility = visibility;
		if (RulerCornerBlock != null) RulerCornerBlock.Visibility = visibility;

		if (_isRulersEnabled)
		{
			UpdateRulers();
		}
	}

	private void Ruler_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		UpdateRulers();
	}

	private void PagesHost_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		UpdateRulers();
	}

	private void UpdateRulers()
	{
		if (!_isRulersEnabled || HorizontalRuler == null || VerticalRuler == null || PagesHost == null || DocumentScrollViewer == null)
		{
			return;
		}

		try
		{
			Point pageOrigin = PagesHost.TranslatePoint(new Point(0, 0), DocumentScrollViewer);
			DrawHorizontalRuler(pageOrigin.X);
			DrawVerticalRuler(pageOrigin.Y);
		}
		catch
		{
			// Safe guard against TranslatePoint when visual is not connected to source yet
		}
	}

	public void ContextGuides_Click(object sender, RoutedEventArgs e)
	{
		_isGuidesEnabled = !_isGuidesEnabled;
		LogStatus(_isGuidesEnabled ? "Hiện đường gióng" : "Ẩn đường gióng");
	}

	private void ContextHideNavigation_Click(object sender, RoutedEventArgs e)
	{
		ToggleSidebar();
	}

	public async void PrintPdf()
	{
		if (string.IsNullOrEmpty(CurrentPdfPath))
		{
			MessageBox.Show("Open a PDF first.", "Info", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		App.IsPrinting = true;
		try
		{
			PrintOptionsDialog optionsDialog = new PrintOptionsDialog(PageCount, SelectedPageNumber, CurrentPdfPath)
		{
			Owner = Window.GetWindow(this)
		};
		if (optionsDialog.ShowDialog() != true)
		{
			return;
		}
		LogStatus("Preparing print...");
		PrintProgressDialog progressDialog = new PrintProgressDialog
		{
			Owner = Window.GetWindow(this)
		};
		IProgress<PrintProgressInfo> printProgress = new Progress<PrintProgressInfo>(progressDialog.UpdateProgress);
		progressDialog.Show();
		printProgress.Report(new PrintProgressInfo("Dang chuan bi lenh in...", 0, 0, IsIndeterminate: true));
		await base.Dispatcher.InvokeAsync(delegate
		{
		}, DispatcherPriority.Background);
		try
		{
			var selectedQueue = optionsDialog.SelectedPrintQueue;
			if (selectedQueue == null)
			{
				throw new InvalidOperationException("Vui lòng chọn máy in hợp lệ.");
			}

			// Capture options from UI thread
			var baseTicket = optionsDialog.SelectedPrintTicket ?? selectedQueue.UserPrintTicket ?? selectedQueue.DefaultPrintTicket ?? new PrintTicket();
			int copies = optionsDialog.Copies;
			PageMediaSize pageMediaSize = optionsDialog.CreatePageMediaSize();
			PageOrientation? pageOrientation = optionsDialog.CreatePageOrientation();
			double printDpi = optionsDialog.PrintDpi;
			string printEngineMode = optionsDialog.PrintEngineMode;
			string printOffsetMode = optionsDialog.PrintOffsetMode;
			bool printTestFrame = optionsDialog.PrintTestFrame;
			int startPageIndex = optionsDialog.StartPageIndex;
			int endPageIndex = optionsDialog.EndPageIndex;
			bool fitToPrintableArea = optionsDialog.FitToPrintableArea;
			bool autoCenter = optionsDialog.AutoCenter;
			bool separatePageJobs = optionsDialog.NativeSeparatePageJobs;
			bool reversePageOrder = optionsDialog.ReversePageOrder;
			bool forceRasterize = optionsDialog.OptimizeCadDrawings;
			var annotationsList = Annotations.ToList();

			// Capture print settings/objects on UI thread to avoid thread affinity exceptions on background thread
			var printerProfile = PrinterPrintProfile.Resolve(selectedQueue);
			string printerName = selectedQueue.FullName;
			int clientSchemaVersion = selectedQueue.ClientPrintSchemaVersion;
			var cancellationToken = progressDialog.CancellationToken;

			// Get Capabilities on UI thread to avoid invalid printer name exceptions for network/shared printers on the background thread
			printProgress.Report(new PrintProgressInfo("Đang truy vấn cấu hình máy in...", 0, 0, IsIndeterminate: true));
			PrintCapabilities printCapabilities = selectedQueue.GetPrintCapabilities(baseTicket);

			// 1. Cấu hình PrintTicket trên UI thread để tránh Thread Affinity exceptions
			PrintTicket printTicket = baseTicket.Clone();
			printTicket.CopyCount = copies;
			if (pageMediaSize != null)
			{
				printTicket.PageMediaSize = pageMediaSize;
			}
			if (pageOrientation.HasValue)
			{
				printTicket.PageOrientation = pageOrientation;
			}
			ApplyRequestedPageResolution(printTicket, printDpi);

			// Capture pageMediaSize values safely to avoid accessing the object on the background thread
			string pageMediaSizeName = pageMediaSize?.PageMediaSizeName?.ToString() ?? "Unknown";
			double pageMediaSizeWidth = pageMediaSize?.Width ?? 0.0;
			double pageMediaSizeHeight = pageMediaSize?.Height ?? 0.0;
			int? resolutionX = printTicket.PageResolution?.X;
			int? resolutionY = printTicket.PageResolution?.Y;

			// Calculate oriented page dimensions on UI thread
			double orientedWidth = printCapabilities.OrientedPageMediaWidth ?? pageMediaSize?.Width ?? 8.5 * 96.0;
			double orientedHeight = printCapabilities.OrientedPageMediaHeight ?? pageMediaSize?.Height ?? 11.0 * 96.0;
			if (pageOrientation == PageOrientation.Landscape)
			{
				double num3 = Math.Max(orientedWidth, orientedHeight);
				double num4 = Math.Min(orientedWidth, orientedHeight);
				orientedWidth = num3;
				orientedHeight = num4;
			}
			else if (pageOrientation == PageOrientation.Portrait)
			{
				double num5 = Math.Min(orientedWidth, orientedHeight);
				double num6 = Math.Max(orientedWidth, orientedHeight);
				orientedWidth = num5;
				orientedHeight = num6;
			}

			// Pre-convert PrintTicket to DevMode on UI thread for native engines
			byte[] devModeBytes = null;
			if (printEngineMode == "NativePdfium" || printEngineMode == "NativePdfium_Optimized")
			{
				try
				{
					PrintTicket singleTicket = printTicket.Clone();
					singleTicket.CopyCount = 1;
					using PrintTicketConverter printTicketConverter = new PrintTicketConverter(printerName, clientSchemaVersion);
					devModeBytes = printTicketConverter.ConvertPrintTicketToDevMode(singleTicket, BaseDevModeType.UserDefault);
				}
				catch (Exception ex)
				{
					LogToDesktop("Warning: Failed to convert PrintTicket to DevMode on UI thread: " + ex.Message + ". Using default printer settings.");
				}
			}

			await Task.Run(async delegate
			{
				printProgress.Report(new PrintProgressInfo("Đang cấu hình máy in...", 0, 0, IsIndeterminate: true));

				LogToDesktop("Profile: " + printerProfile.Name);

				bool driverAlreadyOffsetsPrintableArea = printOffsetMode == "WpfOffset" || 
					(!(printOffsetMode == "Physical") && printerProfile.DriverAlreadyOffsetsPrintableArea);

				LogToDesktop("\n=================== BẮT ĐẦU CHẨN ĐOÁN LỆNH IN ===================");
				LogToDesktop("Tệp đang in: " + CurrentPdfPath);
				LogToDesktop("Máy in mục tiêu: " + printerName);
				LogToDesktop($"Số bản in (Copies): {copies}");
				LogToDesktop($"Trang bắt đầu: {startPageIndex + 1}, Trang kết thúc: {endPageIndex + 1}");
				LogToDesktop($"Tự động căn giữa (AutoCenter): {autoCenter}, Khớp khổ giấy (FitToPrintableArea): {fitToPrintableArea}");
				LogToDesktop($"Hướng xoay giấy: {pageOrientation}");
				LogToDesktop($"Khổ giấy đã chọn: {pageMediaSizeName} (Rộng: {pageMediaSizeWidth} x Cao: {pageMediaSizeHeight})");
				LogToDesktop($"DPI in đã chọn: {printDpi}; PrintTicket.PageResolution={resolutionX}x{resolutionY}");
				LogToDesktop("Chế độ in đã chọn: " + printEngineMode);
				LogToDesktop($"Native separate page jobs: {separatePageJobs}");
				LogToDesktop($"Reverse page order: {reversePageOrder}");

				if (printEngineMode == "NativePdfium" && !printTestFrame)
				{
					if (annotationsList.Count > 0)
					{
						LogToDesktop("Native PDFium print note: app overlay annotations are not rendered by the native printer path. Use WPF Bitmap if those annotations must be printed.");
					}

					Stopwatch nativeSubmitSw = Stopwatch.StartNew();
					NativePdfPrinter.Print(CurrentPdfPath, printerName, devModeBytes, startPageIndex, endPageIndex, copies, fitToPrintableArea, autoCenter, driverAlreadyOffsetsPrintableArea, printerProfile.RightSafetyPadding, printerProfile.BottomSafetyPadding, separatePageJobs, reversePageOrder, forceRasterize, printProgress, cancellationToken, printDpi);
					nativeSubmitSw.Stop();
					LogToDesktop($"Native print submit total: {nativeSubmitSw.ElapsedMilliseconds} ms");
					progressDialog.MarkCompleted("Da gui lenh in vao may in.");
					LogStatus("Print job sent");
				}
				else if (printEngineMode == "NativePdfium_Optimized" && !printTestFrame)
				{
					if (annotationsList.Count > 0)
					{
						LogToDesktop("Native PDFium print note: app overlay annotations are not rendered by the native printer path. Use WPF Bitmap if those annotations must be printed.");
					}

					Stopwatch nativeSubmitSw = Stopwatch.StartNew();
					NativePdfPrinter.PrintOptimized(CurrentPdfPath, printerName, devModeBytes, startPageIndex, endPageIndex, copies, fitToPrintableArea, autoCenter, driverAlreadyOffsetsPrintableArea, printerProfile.RightSafetyPadding, printerProfile.BottomSafetyPadding, separatePageJobs, reversePageOrder, forceRasterize, printProgress, cancellationToken, printDpi);
					nativeSubmitSw.Stop();
					LogToDesktop($"Native print (Optimized) submit total: {nativeSubmitSw.ElapsedMilliseconds} ms");
					progressDialog.MarkCompleted("Da gui lenh in (Toi uu) vao may in.");
					LogStatus("Print job sent");
				}
				else if (printEngineMode == "PdfDirect")
				{
					string docName = "PDF Pro - " + System.IO.Path.GetFileName(CurrentPdfPath);
					progressDialog.UpdateProgress(new PrintProgressInfo("Dang in truc tiep PDF...", 0, 1, IsIndeterminate: true));
					NativePdfPrinter.PrintPdfDirect(CurrentPdfPath, printerName, docName, cancellationToken);
					progressDialog.MarkCompleted("Da gui truc tiep file PDF vao may in.");
					LogStatus("Print job sent");
				}
				else if (printEngineMode == "PdfDirect_Optimized")
				{
					string docName = "PDF Pro - " + System.IO.Path.GetFileName(CurrentPdfPath);
					progressDialog.UpdateProgress(new PrintProgressInfo("Dang in truc tiep PDF (Toi uu)...", 0, 1, IsIndeterminate: true));
					NativePdfPrinter.PrintPdfDirectOptimized(CurrentPdfPath, printerName, docName, cancellationToken);
					progressDialog.MarkCompleted("Da gui truc tiep file PDF (Toi uu) vao may in.");
					LogStatus("Print job sent");
				}
				else
				{
					// WPF Paginator requires UI thread for Visuals creation
					await base.Dispatcher.InvokeAsync(delegate
					{
						PdfDocumentPaginator paginator = new PdfDocumentPaginator(CurrentPdfPath);
						paginator.Annotations.AddRange(annotationsList);
						paginator.StartPage = startPageIndex;
						paginator.EndPage = endPageIndex;
						paginator.AutoCenter = autoCenter;
						paginator.FitToPrintableArea = fitToPrintableArea;
						paginator.PrintDpi = printDpi;
						paginator.ReversePageOrder = reversePageOrder;
						paginator.PrintProgress = printProgress;
						paginator.BottomSafetyPadding = printerProfile.BottomSafetyPadding;
						paginator.RightSafetyPadding = printerProfile.RightSafetyPadding;
						paginator.DriverAlreadyOffsetsPrintableArea = driverAlreadyOffsetsPrintableArea;
						paginator.PrintTestFrame = printTestFrame;

						if (printTestFrame)
						{
							paginator.StartPage = 0;
							paginator.EndPage = 0;
							LogToDesktop("Print test frame enabled: forcing a single diagnostic page.");
						}

						paginator.PageSize = new Size(Math.Max(1.0, orientedWidth), Math.Max(1.0, orientedHeight));
						LogToDesktop($"Kích thước trang đích (PageSize): {paginator.PageSize.Width}x{paginator.PageSize.Height}");

						if (printCapabilities.PageImageableArea != null)
						{
							double originWidth = printCapabilities.PageImageableArea.OriginWidth;
							double originHeight = printCapabilities.PageImageableArea.OriginHeight;
							double value = Math.Max(0.0, orientedWidth - originWidth - printCapabilities.PageImageableArea.ExtentWidth);
							double value2 = Math.Max(0.0, orientedHeight - originHeight - printCapabilities.PageImageableArea.ExtentHeight);
							paginator.ImageableArea = new Rect(originWidth, originHeight, printCapabilities.PageImageableArea.ExtentWidth, printCapabilities.PageImageableArea.ExtentHeight);
							LogToDesktop($"Vùng in được của máy in (Raw PageImageableArea): Gốc=({originWidth}, {originHeight}) Kích thước=({printCapabilities.PageImageableArea.ExtentWidth}x{printCapabilities.PageImageableArea.ExtentHeight})");
							LogToDesktop($"Khoảng lề biên kéo giấy tính toán: Phải={value}, Dưới={value2}");
							LogToDesktop($"Tọa độ vùng in truyền cho Paginator (ImageableArea): Gốc=({paginator.ImageableArea.X}, {paginator.ImageableArea.Y}) Kích thước=({paginator.ImageableArea.Width}x{paginator.ImageableArea.Height})");
						}

						LogToDesktop("Using WPF Bitmap print pipeline.");
						Stopwatch printSubmitSw = Stopwatch.StartNew();
						printProgress.Report(new PrintProgressInfo("Dang gui lenh in WPF Bitmap...", 0, Math.Max(1, paginator.PageCount), IsIndeterminate: true));
						
						PrintDialog printDialog = new PrintDialog();
						printDialog.PrintQueue = selectedQueue;
						printDialog.PrintTicket = printTicket;
						printDialog.PrintDocument(paginator, System.IO.Path.GetFileName(CurrentPdfPath));
						printSubmitSw.Stop();
						LogToDesktop($"PrintDocument submit total: {printSubmitSw.ElapsedMilliseconds} ms");
						progressDialog.MarkCompleted("Da gui lenh in vao may in.");
						LogStatus("Print job sent");
					});
				}
			});
		}
		catch (OperationCanceledException)
		{
			LogToDesktop("Print canceled by user.");
			progressDialog.MarkFailed("Da huy lenh in.");
			LogStatus("Print canceled");
		}
		catch (Exception ex3)
		{
			LogToDesktop($"Print failed: {ex3}");
			progressDialog.MarkFailed("In that bai: " + ex3.Message);
			MessageBox.Show("Error while printing: " + ex3.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			LogStatus("Print failed");
		}
		}
		finally
		{
			App.IsPrinting = false;
			App.ResetPrintBusyNotification();
			App.OpenPendingFiles();
		}
	}

	private static void ApplyRequestedPageResolution(PrintTicket printTicket, double dpi)
	{
		int num = Math.Clamp((int)Math.Round(dpi), 72, 1200);
		printTicket.PageResolution = new PageResolution(num, num);
	}

	private void CloseTextPages()
	{
		lock (PdfiumEngine.SyncRoot)
		{
			foreach (nint value in _textPages.Values)
			{
				if (value != IntPtr.Zero)
				{
					PdfiumEngine.FPDFText_ClosePage(value);
				}
			}
			_textPages.Clear();
		}
	}

	private int GetCharIndexAtMousePos(Canvas canvas, Point mousePos, int pageNumber)
	{
		nint textPage = GetTextPage(pageNumber);
		if (textPage == IntPtr.Zero)
		{
			return -1;
		}
		if (!TryCanvasToPdfPoint(canvas, mousePos, pageNumber, out var pdfPoint))
		{
			return -1;
		}
		// Increased tolerance from 10.0 to 20.0 for much more reliable character click detection
		double xTolerance = Math.Max(2.0, 20.0 * _pageDimensions[pageNumber - 1].Width / Math.Max(1.0, canvas.Width));
		double yTolerance = Math.Max(2.0, 20.0 * _pageDimensions[pageNumber - 1].Height / Math.Max(1.0, canvas.Height));
		lock (PdfiumEngine.SyncRoot)
		{
			return PdfiumEngine.FPDFText_GetCharIndexAtPos(textPage, pdfPoint.X, pdfPoint.Y, xTolerance, yTolerance);
		}
	}

	private static string MapPdfFontToSystemFont(string pdfFontName)
	{
		if (string.IsNullOrEmpty(pdfFontName)) return "Segoe UI";

		string clean = pdfFontName.ToLowerInvariant();
		int plusIndex = clean.IndexOf('+');
		if (plusIndex >= 0 && plusIndex < clean.Length - 1)
		{
			clean = clean.Substring(plusIndex + 1);
		}

		if (clean.Contains("times") || clean.Contains("roman") || clean.Contains("serif"))
		{
			return "Times New Roman";
		}
		if (clean.Contains("courier") || clean.Contains("mono"))
		{
			return "Courier New";
		}
		if (clean.Contains("arial") || clean.Contains("helvetica") || clean.Contains("sans"))
		{
			if (clean.Contains("noto")) return "Noto Sans";
			if (clean.Contains("dejavu")) return "DejaVu Sans";
			if (clean.Contains("open")) return "Open Sans";
			return "Arial";
		}
		if (clean.Contains("calibri"))
		{
			return "Calibri";
		}
		if (clean.Contains("segoe"))
		{
			return "Segoe UI";
		}
		if (clean.Contains("georgia"))
		{
			return "Georgia";
		}
		if (clean.Contains("verdana"))
		{
			return "Verdana";
		}
		if (clean.Contains("tahoma"))
		{
			return "Tahoma";
		}

		string originalClean = pdfFontName;
		if (plusIndex >= 0 && plusIndex < pdfFontName.Length - 1)
		{
			originalClean = pdfFontName.Substring(plusIndex + 1);
		}
		
		string[] suffixes = new[] { "-Bold", "-Italic", "-BoldItalic", "-Regular", "Bold", "Italic", "MT", "PS", "Regular", "Oblique" };
		foreach (var suffix in suffixes)
		{
			if (originalClean.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
			{
				originalClean = originalClean.Substring(0, originalClean.Length - suffix.Length);
			}
		}
		originalClean = originalClean.Trim('-', ' ');

		try
		{
			var fontFamily = new System.Windows.Media.FontFamily(originalClean);
			return originalClean;
		}
		catch
		{
			return "Arial";
		}
	}

	private bool TryCanvasToPdfPoint(Canvas canvas, Point canvasPoint, int pageNumber, out Point pdfPoint)
	{
		pdfPoint = default;
		if (!TryGetPageSize(pageNumber, out Size pageSize) || canvas.Width <= 0.0 || canvas.Height <= 0.0)
		{
			return false;
		}

		double x = canvasPoint.X * pageSize.Width / canvas.Width;
		double y = (canvas.Height - canvasPoint.Y) * pageSize.Height / canvas.Height;
		pdfPoint = new Point(x, y);
		return true;
	}

	private bool TryPdfRectToCanvasRect(Canvas canvas, int pageNumber, double left, double right, double bottom, double top, out Rect canvasRect)
	{
		canvasRect = Rect.Empty;
		if (!TryGetPageSize(pageNumber, out Size pageSize) || canvas.Width <= 0.0 || canvas.Height <= 0.0)
		{
			return false;
		}

		double x = left * canvas.Width / pageSize.Width;
		double y = (pageSize.Height - top) * canvas.Height / pageSize.Height;
		double width = (right - left) * canvas.Width / pageSize.Width;
		double height = (top - bottom) * canvas.Height / pageSize.Height;
		canvasRect = new Rect(x, y, width, height);
		return true;
	}

	private bool TryGetPageSize(int pageNumber, out Size pageSize)
	{
		pageSize = default;
		int index = pageNumber - 1;
		if (index < 0 || index >= _pageDimensions.Count)
		{
			return false;
		}

		pageSize = _pageDimensions[index];
		return pageSize.Width > 0.0 && pageSize.Height > 0.0;
	}

	private void DrawTextSelectionHighlights(Canvas canvas, int pageNumber)
	{
		// 1. VẼ CÁC KHỐI HIGHLIGHT MÀU VÀNG ĐÃ LƯU LÊN MÀN HÌNH
		foreach (var ann in Annotations)
		{
			if (ann is PdfHighlightAnnotation hl && hl.PageIndex == pageNumber - 1)
			{
				double canvasX = hl.X * canvas.Width;
				double canvasY = hl.Y * canvas.Height;
				double canvasW = hl.Width * canvas.Width;
				double canvasH = hl.Height * canvas.Height;

				Color hlColor = Colors.Yellow;
				try { hlColor = (Color)ColorConverter.ConvertFromString(hl.ColorHex); } catch {}

				System.Windows.Shapes.Rectangle rect = new System.Windows.Shapes.Rectangle
				{
					Width = Math.Max(1.0, canvasW),
					Height = Math.Max(1.0, canvasH),
					Fill = new SolidColorBrush(Color.FromArgb(100, hlColor.R, hlColor.G, hlColor.B)), // Vàng trong suốt để không che chữ
					IsHitTestVisible = false // Bỏ qua bắt chuột
				};
				
				Canvas.SetLeft(rect, canvasX);
				Canvas.SetTop(rect, canvasY);
				
				// Đẩy lớp màu vàng xuống dưới cùng để nét chữ nổi lên trên
				if (canvas.Children.Count > 0) canvas.Children.Insert(0, rect);
				else canvas.Children.Add(rect);
			}
		}

		// 2. VẼ KHỐI MÀU XANH TẠM THỜI KHI ĐANG GIỮ CHUỘT KÉO BÔI ĐEN
		if (_selectionStartPageIndex == -1 || _selectionEndPageIndex == -1) return;

		int num = pageNumber - 1;
		int num2 = Math.Min(_selectionStartPageIndex, _selectionEndPageIndex);
		int num3 = Math.Max(_selectionStartPageIndex, _selectionEndPageIndex);
		if (num < num2 || num > num3) return;

		nint textPage = GetTextPage(pageNumber);
		if (textPage == IntPtr.Zero) return;

		int num4;
		lock (PdfiumEngine.SyncRoot) { num4 = PdfiumEngine.FPDFText_CountChars(textPage); }
		if (num4 <= 0) return;

		int num5 = 0;
		int num6 = num4 - 1;
		if (num == _selectionStartPageIndex && num == _selectionEndPageIndex)
		{
			num5 = Math.Min(_selectionStartIndex, _selectionEndIndex);
			num6 = Math.Max(_selectionStartIndex, _selectionEndIndex);
		}
		else if (num == _selectionStartPageIndex)
		{
			if (_selectionStartPageIndex < _selectionEndPageIndex) num5 = _selectionStartIndex;
			else num6 = _selectionStartIndex;
		}
		else if (num == _selectionEndPageIndex)
		{
			if (_selectionStartPageIndex < _selectionEndPageIndex) num6 = _selectionEndIndex;
			else num5 = _selectionEndIndex;
		}

		if (num5 < 0) num5 = 0;
		if (num6 >= num4) num6 = num4 - 1;

		SolidColorBrush fill = new SolidColorBrush(Color.FromArgb(90, 51, 153, byte.MaxValue));
		fill.Freeze();

		var mergedRects = new List<Rect>();
		lock (PdfiumEngine.SyncRoot)
		{
			Rect currentRect = Rect.Empty;
			for (int i = num5; i <= num6; i++)
			{
				if (PdfiumEngine.FPDFText_GetCharBox(textPage, i, out var left, out var right, out var bottom, out var top))
				{
					if (!TryPdfRectToCanvasRect(canvas, pageNumber, left, right, bottom, top, out Rect canvasRect)) continue;
					if (canvasRect.Width <= 0.0) canvasRect.Width = 6.0;
					if (canvasRect.Height <= 0.0) canvasRect.Height = 12.0;

					if (currentRect.IsEmpty) currentRect = canvasRect;
					else
					{
						double verticalDistance = Math.Abs(canvasRect.Y - currentRect.Y);
						double heightDifference = Math.Abs(canvasRect.Height - currentRect.Height);
						double horizontalGap = canvasRect.X - (currentRect.X + currentRect.Width);
						double heightThreshold = Math.Max(currentRect.Height, canvasRect.Height);
						if (verticalDistance < heightThreshold * 0.4 && heightDifference < heightThreshold * 0.4 && horizontalGap >= -2.0 && horizontalGap < heightThreshold * 3.0)
						{
							double minX = Math.Min(currentRect.X, canvasRect.X);
							double maxX = Math.Max(currentRect.X + currentRect.Width, canvasRect.X + canvasRect.Width);
							double minY = Math.Min(currentRect.Y, canvasRect.Y);
							double maxY = Math.Max(currentRect.Y + currentRect.Height, canvasRect.Y + canvasRect.Height);
							currentRect = new Rect(minX, minY, maxX - minX, maxY - minY);
						}
						else
						{
							mergedRects.Add(currentRect);
							currentRect = canvasRect;
						}
					}
				}
			}
			if (!currentRect.IsEmpty) mergedRects.Add(currentRect);
		}

		foreach (var rect in mergedRects)
		{
			System.Windows.Shapes.Rectangle element = new System.Windows.Shapes.Rectangle
			{
				Width = Math.Max(0.5, rect.Width),
				Height = Math.Max(0.5, rect.Height),
				Fill = fill,
				IsHitTestVisible = false
			};
			Canvas.SetLeft(element, rect.X);
			Canvas.SetTop(element, rect.Y);
			canvas.Children.Add(element);
		}
	}

	private void DrawEditTextSelectionBorder(Canvas canvas, int pageNumber)
	{
		if (_selectedEditPageNumber != pageNumber || !_selectedEditRectPdf.HasValue)
		{
			return;
		}

		var rectPdf = _selectedEditRectPdf.Value;
		if (!TryPdfRectToCanvasRect(canvas, pageNumber, rectPdf.X, rectPdf.X + rectPdf.Width, rectPdf.Y, rectPdf.Y + rectPdf.Height, out Rect canvasRect))
		{
			return;
		}

		// Draw selection border (dashed blue line)
		System.Windows.Shapes.Rectangle borderRect = new System.Windows.Shapes.Rectangle
		{
			Width = canvasRect.Width + 4.0,
			Height = canvasRect.Height + 4.0,
			Stroke = new SolidColorBrush(Color.FromRgb(37, 99, 235)), // Modern soft blue border
			StrokeThickness = 1.5,
			StrokeDashArray = new DoubleCollection(new double[] { 4, 4 }),
			IsHitTestVisible = false
		};
		Canvas.SetLeft(borderRect, canvasRect.X - 2.0);
		Canvas.SetTop(borderRect, canvasRect.Y - 2.0);
		canvas.Children.Add(borderRect);

		// Draw 4 resize handles (thumbs) at the corners
		double handleSize = 6.0;
		Point[] corners = new Point[]
		{
			new Point(canvasRect.X - 2.0, canvasRect.Y - 2.0), // Top-Left
			new Point(canvasRect.X + canvasRect.Width + 2.0, canvasRect.Y - 2.0), // Top-Right
			new Point(canvasRect.X - 2.0, canvasRect.Y + canvasRect.Height + 2.0), // Bottom-Left
			new Point(canvasRect.X + canvasRect.Width + 2.0, canvasRect.Y + canvasRect.Height + 2.0) // Bottom-Right
		};

		foreach (var pt in corners)
		{
			System.Windows.Shapes.Rectangle handle = new System.Windows.Shapes.Rectangle
			{
				Width = handleSize,
				Height = handleSize,
				Fill = Brushes.White,
				Stroke = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
				StrokeThickness = 1.5,
				IsHitTestVisible = false
			};
			Canvas.SetLeft(handle, pt.X - handleSize / 2.0);
			Canvas.SetTop(handle, pt.Y - handleSize / 2.0);
			canvas.Children.Add(handle);
		}
	}

	public string GetSelectedTextString()
	{
		if (_selectionStartPageIndex == -1 || _selectionEndPageIndex == -1)
		{
			return "";
		}
		int num = Math.Min(_selectionStartPageIndex, _selectionEndPageIndex);
		int num2 = Math.Max(_selectionStartPageIndex, _selectionEndPageIndex);
		StringBuilder stringBuilder = new StringBuilder();
		lock (PdfiumEngine.SyncRoot)
		{
			for (int i = num; i <= num2; i++)
			{
				nint textPage = GetTextPage(i + 1);
				if (textPage == IntPtr.Zero)
				{
					continue;
				}
				int num3 = PdfiumEngine.FPDFText_CountChars(textPage);
				if (num3 <= 0)
				{
					continue;
				}
				int num4 = 0;
				int num5 = num3 - 1;
				if (i == _selectionStartPageIndex && i == _selectionEndPageIndex)
				{
					num4 = Math.Min(_selectionStartIndex, _selectionEndIndex);
					num5 = Math.Max(_selectionStartIndex, _selectionEndIndex);
				}
				else if (i == _selectionStartPageIndex)
				{
					if (_selectionStartPageIndex < _selectionEndPageIndex)
					{
						num4 = _selectionStartIndex;
					}
					else
					{
						num5 = _selectionStartIndex;
					}
				}
				else if (i == _selectionEndPageIndex)
				{
					if (_selectionStartPageIndex < _selectionEndPageIndex)
					{
						num5 = _selectionEndIndex;
					}
					else
					{
						num4 = _selectionEndIndex;
					}
				}
				if (num4 < 0)
				{
					num4 = 0;
				}
				if (num5 >= num3)
				{
					num5 = num3 - 1;
				}
				int num6 = num5 - num4 + 1;
				if (num6 > 0)
				{
					StringBuilder stringBuilder2 = new StringBuilder(num6 + 2);
					if (PdfiumEngine.FPDFText_GetText(textPage, num4, num6, stringBuilder2) > 0)
					{
						stringBuilder.Append(stringBuilder2.ToString());
					}
				}
				if (i < num2)
				{
					stringBuilder.AppendLine();
				}
			}
		}
		return stringBuilder.ToString();
	}

	public void CopySelectedText()
	{
		if (string.IsNullOrEmpty(_selectedText))
		{
			_selectedText = GetSelectedTextString();
		}

		if (!string.IsNullOrEmpty(_selectedText))
		{
			try
			{
				Clipboard.SetText(_selectedText);
				LogStatus("Đã sao chép văn bản vào Clipboard");
			}
			catch (Exception ex)
			{
				MessageBox.Show("Lỗi sao chép: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}
	}

	public void ClearAllTextSelectionHighlights()
	{
		_selectionStartPageIndex = -1;
		_selectionStartIndex = -1;
		_selectionEndPageIndex = -1;
		_selectionEndIndex = -1;
		_selectedText = "";
		RedrawAllPageAnnotations();
	}
	private void ShowDirectTextEditOverlay(Canvas canvas, int charIndex, int pageNumber)
	{
		LogToDesktop($"[EDIT DEBUG] 3. Khởi chạy ShowDirectTextEditOverlay (CharIndex: {charIndex}, Page: {pageNumber})");
		
		// [QUAN TRỌNG] Nếu đang có TextBox biên tập khác, commit và đóng nó trước
		if (_activeDirectEditCommitAction != null)
		{
			try
			{
				var tempAction = _activeDirectEditCommitAction;
				_activeDirectEditTextBox = null; // Đánh dấu để bypass guard < 500ms
				_activeDirectEditCommitAction = null;
				tempAction();
			}
			catch (Exception ex)
			{
				LogToDesktop($"[DEBUG] Error committing active edit: {ex.Message}");
			}
		}

		nint textPage = GetTextPage(pageNumber);
		if (textPage == IntPtr.Zero) return;
		
		int num = 0;
		lock (PdfiumEngine.SyncRoot) { num = PdfiumEngine.FPDFText_CountChars(textPage); }
		if (num <= 0) return;

		// 1. Tính toán layout
		double estFontSize = 12.0;
		double initTop = 0;
		lock (PdfiumEngine.SyncRoot)
		{
			if (PdfiumEngine.FPDFText_GetCharBox(textPage, charIndex, out double cL, out double cR, out double cB, out double cT))
			{
				estFontSize = cT - cB;
				initTop = cT;
			}
		}

		int num2;
		double lastTop = initTop;
		for (num2 = charIndex; num2 > 0; num2--)
		{
			lock (PdfiumEngine.SyncRoot)
			{
				if (PdfiumEngine.FPDFText_GetCharBox(textPage, num2 - 1, out double l, out double r, out double b, out double t))
				{
					if (Math.Abs(lastTop - t) > estFontSize * 1.8) break;
					lastTop = t;
				}
				else break;
			}
		}

		int num3;
		lastTop = initTop;
		for (num3 = charIndex; num3 < num - 1; num3++)
		{
			lock (PdfiumEngine.SyncRoot)
			{
				if (PdfiumEngine.FPDFText_GetCharBox(textPage, num3 + 1, out double l, out double r, out double b, out double t))
				{
					if (Math.Abs(lastTop - t) > estFontSize * 1.8) break;
					lastTop = t;
				}
				else break;
			}
		}

		double minLeft = double.MaxValue, maxRight = double.MinValue, minBottom = double.MaxValue, maxTop = double.MinValue;
		bool flag = false;
		lock (PdfiumEngine.SyncRoot)
		{
			for (int num4 = num2; num4 <= num3; num4++)
			{
				if (PdfiumEngine.FPDFText_GetCharBox(textPage, num4, out var left, out var right, out var bottom, out var top) && right > left && top > bottom)
				{
					minLeft = Math.Min(minLeft, left); maxRight = Math.Max(maxRight, right);
					minBottom = Math.Min(minBottom, bottom); maxTop = Math.Max(maxTop, top);
					flag = true;
				}
			}
		}

		if (!flag || !TryGetPageSize(pageNumber, out Size pageSize) || !TryPdfRectToCanvasRect(canvas, pageNumber, minLeft, maxRight, minBottom, maxTop, out Rect editRect)) return;

		int num9 = num3 - num2 + 1;
		string existingText = "";
		if (num9 > 0)
		{
			StringBuilder stringBuilder = new StringBuilder(num9 + 2);
			lock (PdfiumEngine.SyncRoot) { if (PdfiumEngine.FPDFText_GetText(textPage, num2, num9, stringBuilder) > 0) existingText = stringBuilder.ToString(); }
		}

		// 2. Tạo UI TextBox
		System.Windows.Controls.TextBox tbInput = new System.Windows.Controls.TextBox
		{
			Width = Math.Max(50.0, editRect.Width + 12.0),
			Height = Math.Max(20.0, editRect.Height + 6.0),
			Text = existingText,
			FontFamily = new FontFamily(ActiveFontFamily),
			FontSize = Math.Max(8.0, (estFontSize > 0 ? estFontSize : 12.0) * canvas.Height / pageSize.Height),
			Foreground = Brushes.Black,
			TextWrapping = TextWrapping.Wrap,
			AcceptsReturn = true,
			BorderBrush = new SolidColorBrush(Color.FromRgb(153, 193, 241)),
			BorderThickness = new Thickness(1.0),
			Background = Brushes.White,
			Padding = new Thickness(2.0, 1.0, 2.0, 1.0)
		};
		
		Canvas.SetLeft(tbInput, editRect.X - 2.0);
		Canvas.SetTop(tbInput, editRect.Y - 3.0);
		canvas.Children.Add(tbInput);
		_activeDirectEditTextBox = tbInput;

		// 3. Tính CaretIndex và xử lý Focus an toàn
		int countBefore = charIndex - num2;
		int caretPos = 0;
		if (countBefore > 0)
		{
			StringBuilder sbBefore = new StringBuilder(countBefore + 2);
			lock (PdfiumEngine.SyncRoot) { if (PdfiumEngine.FPDFText_GetText(textPage, num2, countBefore, sbBefore) > 0) caretPos = sbBefore.ToString().Length; }
		}

		// Gọi Focus trực tiếp thông qua Dispatcher thay vì chờ Loaded
		Dispatcher.BeginInvoke(new Action(() => {
			tbInput.Focus();
			Keyboard.Focus(tbInput);
			if (caretPos >= 0 && caretPos <= tbInput.Text.Length) tbInput.CaretIndex = caretPos;
		}), System.Windows.Threading.DispatcherPriority.Input);

		long creationTime = Environment.TickCount;
		bool editCommitted = false;
		Action commitEdit = delegate
		{
			if (editCommitted) return;
			
			// [QUAN TRỌNG] Chỉ chặn LostFocus nếu là do sự kiện trôi tiêu điểm tự động của TextBox hiện tại
			if (Environment.TickCount - creationTime < 500 && _activeDirectEditTextBox == tbInput)
			{
				LogToDesktop("[DEBUG] Bỏ qua LostFocus do thời gian sống quá ngắn (tránh trôi focus sau click). Refocus TextBox.");
				Dispatcher.BeginInvoke(new Action(() => {
					if (_activeDirectEditTextBox == tbInput)
					{
						tbInput.Focus();
						Keyboard.Focus(tbInput);
					}
				}), System.Windows.Threading.DispatcherPriority.Input);
				return;
			}

			editCommitted = true;
			if (_activeDirectEditTextBox == tbInput)
			{
				_activeDirectEditTextBox = null;
				_activeDirectEditCommitAction = null;
			}
			
			string text = tbInput.Text;
			if (canvas.Children.Contains(tbInput)) canvas.Children.Remove(tbInput);

			if (text != existingText)
			{
				PdfTextBoxAnnotation whiteout = new PdfTextBoxAnnotation { PageIndex = pageNumber - 1, X = minLeft / pageSize.Width, Y = (pageSize.Height - maxTop) / pageSize.Height, Width = (maxRight - minLeft) / pageSize.Width, Height = (maxTop - minBottom) / pageSize.Height, Text = "", BgColor = Colors.White, StrokeColor = Colors.Transparent, Opacity = 1.0 };
				PdfTextBoxAnnotation replacement = new PdfTextBoxAnnotation { PageIndex = pageNumber - 1, X = minLeft / pageSize.Width, Y = (pageSize.Height - maxTop) / pageSize.Height, Width = (maxRight - minLeft) / pageSize.Width, Height = (maxTop - minBottom) / pageSize.Height, Text = text, BgColor = Colors.Transparent, StrokeColor = Colors.Black, FontFamily = ActiveFontFamily, FontSize = estFontSize, Opacity = 1.0 };
				
				try { SaveUndoState(); } catch {}
				Annotations.Add(whiteout); Annotations.Add(replacement); 
				_pendingTextEdits.Add(new PendingTextEdit(pageNumber, existingText, text, minLeft, minBottom, maxRight - minLeft, maxTop - minBottom, whiteout, replacement));
				RedrawPageAnnotations(canvas, pageNumber);
			}
		};
		_activeDirectEditCommitAction = commitEdit;

		tbInput.LostFocus += (s, ev) =>
		{
			// [QUAN TRỌNG] Trì hoãn việc kiểm tra Focus bằng Dispatcher.
			// Nếu TextBox thực sự mất focus (click ra ngoài), nó mới gọi commitEdit().
			Dispatcher.BeginInvoke(new Action(() =>
			{
				if (!tbInput.IsFocused && !tbInput.IsKeyboardFocusWithin)
				{
					commitEdit();
				}
			}), System.Windows.Threading.DispatcherPriority.Input);
		};

		tbInput.KeyDown += delegate(object s, KeyEventArgs ev)
		{
			if (ev.Key == Key.Return && Keyboard.Modifiers != ModifierKeys.Shift)
			{
				commitEdit();
				ev.Handled = true;
			}
			else if (ev.Key == Key.Escape)
			{
				if (_activeDirectEditTextBox == tbInput)
				{
					_activeDirectEditTextBox = null;
					_activeDirectEditCommitAction = null;
				}
				if (canvas.Children.Contains(tbInput)) canvas.Children.Remove(tbInput);
				ev.Handled = true;
			}
		};
	}
	private void ShowDirectTextEditOverlayFromBounds(Canvas canvas, int pageNumber, double minLeft, double minBottom, double maxRight, double maxTop, string existingText)
	{
		if (!TryGetPageSize(pageNumber, out Size pageSize) || !TryPdfRectToCanvasRect(canvas, pageNumber, minLeft, maxRight, minBottom, maxTop, out Rect editRect)) return;

		double fontSizePoints = maxTop - minBottom;
		if (fontSizePoints <= 0.0) fontSizePoints = 12.0;

		System.Windows.Controls.TextBox tbInput = new System.Windows.Controls.TextBox
		{
			Width = Math.Max(50.0, editRect.Width + 30.0),
			Height = Math.Max(20.0, editRect.Height + 6.0),
			Text = existingText,
			FontFamily = new FontFamily(ActiveFontFamily),
			FontSize = Math.Max(8.0, fontSizePoints * canvas.Height / pageSize.Height),
			Foreground = Brushes.Black,
			TextWrapping = TextWrapping.NoWrap,
			AcceptsReturn = false,
			BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
			BorderThickness = new Thickness(1.5),
			Background = Brushes.White,
			Padding = new Thickness(2.0, 1.0, 2.0, 1.0)
		};
		
		Canvas.SetLeft(tbInput, editRect.X - 2.0);
		Canvas.SetTop(tbInput, editRect.Y - 3.0);
		canvas.Children.Add(tbInput);

		// VÁ LỖI MẤT CON TRỎ:
		tbInput.Loaded += (s, e) =>
		{
			tbInput.Focus();
			Keyboard.Focus(tbInput);
			tbInput.SelectAll();
			tbInput.CaretIndex = tbInput.Text.Length;
		};
		long creationTime = Environment.TickCount; 

		bool editCommitted = false;
		Action commitEdit = delegate
		{
			if (editCommitted) return;		
			if (tbInput.IsFocused)
			{
				LogToDesktop("[DEBUG] Bỏ qua LostFocus vì TextBox vẫn đang được Focus (Focus giả).");
				return;
			}

			long aliveTime = Environment.TickCount - creationTime;
			LogToDesktop($"[DEBUG] Đóng TextBox thật sự. Thời gian sống: {aliveTime}ms");

			editCommitted = true;
			
			string text = tbInput.Text;
			if (canvas.Children.Contains(tbInput)) canvas.Children.Remove(tbInput);

			if (text != existingText)
			{
				PdfTextBoxAnnotation whiteout = new PdfTextBoxAnnotation
				{
					PageIndex = pageNumber - 1,
					X = minLeft / pageSize.Width,
					Y = (pageSize.Height - maxTop) / pageSize.Height,
					Width = (maxRight - minLeft) / pageSize.Width,
					Height = (maxTop - minBottom) / pageSize.Height,
					Text = "",
					BgColor = Colors.White,
					StrokeColor = Colors.Transparent,
					Opacity = 1.0
				};
				PdfTextBoxAnnotation replacement = new PdfTextBoxAnnotation
				{
					PageIndex = pageNumber - 1,
					X = minLeft / pageSize.Width,
					Y = (pageSize.Height - maxTop) / pageSize.Height,
					Width = (maxRight - minLeft) / pageSize.Width,
					Height = (maxTop - minBottom) / pageSize.Height,
					Text = text,
					BgColor = Colors.Transparent,
					StrokeColor = Colors.Black,
					FontFamily = ActiveFontFamily,
					FontSize = fontSizePoints,
					Opacity = 1.0
				};
				
				try { SaveUndoState(); } catch {}
				Annotations.Add(whiteout);     
				Annotations.Add(replacement); 
				_pendingTextEdits.Add(new PendingTextEdit(pageNumber, existingText, text, minLeft, minBottom, maxRight - minLeft, maxTop - minBottom, whiteout, replacement));
				RedrawPageAnnotations(canvas, pageNumber);
				LogStatus("Staged text replacement. Save the PDF to apply the actual content change.");
			}
		};

		tbInput.PreviewLostKeyboardFocus += (s, ev) => 
		{ 
			// Nếu người dùng click vào một cái gì đó hợp lệ (như thanh Ribbon), 
			// thì mới cho đóng TextBox. Nếu click vào canvas, vẫn giữ TextBox.
			if (Keyboard.FocusedElement is DependencyObject focused && 
			   (focused is Fluent.Button || focused is Fluent.MenuItem))
			{
				commitEdit();
			}
			else if (Keyboard.FocusedElement == null)
			{
				// Click vào vùng trống, chặn đóng
				ev.Handled = true; 
			}
		};
	}
	private string RenderReplacementOverlayImage(PendingTextEdit pendingTextEdit)
	{
		double dpi = 192.0;
		int pixelWidth = Math.Max(1, (int)Math.Ceiling(pendingTextEdit.Width * dpi / 72.0));
		int pixelHeight = Math.Max(1, (int)Math.Ceiling(pendingTextEdit.Height * dpi / 72.0));
		double padding = Math.Max(2.0, dpi / 36.0);
		Border border = new Border
		{
			Width = pixelWidth,
			Height = pixelHeight,
			Background = Brushes.White
		};
		TextBlock textBlock = new TextBlock
		{
			Text = pendingTextEdit.TextAnnotation.Text,
			FontFamily = new FontFamily(pendingTextEdit.TextAnnotation.FontFamily),
			FontSize = Math.Max(8.0, pendingTextEdit.TextAnnotation.FontSize * dpi / 72.0),
			FontWeight = (pendingTextEdit.TextAnnotation.IsBold ? FontWeights.Bold : FontWeights.Normal),
			FontStyle = (pendingTextEdit.TextAnnotation.IsItalic ? FontStyles.Italic : FontStyles.Normal),
			TextDecorations = (pendingTextEdit.TextAnnotation.IsUnderline ? TextDecorations.Underline : null),
			Foreground = new SolidColorBrush(pendingTextEdit.TextAnnotation.StrokeColor),
			TextWrapping = TextWrapping.Wrap,
			Padding = new Thickness(padding),
			Background = Brushes.White
		};
		border.Child = textBlock;
		border.Measure(new Size(pixelWidth, pixelHeight));
		border.Arrange(new Rect(0.0, 0.0, pixelWidth, pixelHeight));
		border.UpdateLayout();
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(border);
		PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
		pngBitmapEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(renderTargetBitmap));
		string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
		using FileStream fileStream = File.Create(path);
		pngBitmapEncoder.Save(fileStream);
		return path;
	}




	public PdfDocumentTab(string path)
	{
		try
		{
			var info = GC.GetGCMemoryInfo();
			long available = info.TotalAvailableMemoryBytes;
			if (available > 0)
			{
				long tenPercent = available / 10;
				MaxBitmapCacheBytes = Math.Clamp(tenPercent, 268435456L, 1073741824L);
			}
		}
		catch { }

		_cacheManager = new Services.Cache.PdfCacheManager(MaxBitmapCacheBytes);
		InitializeComponent();
		RenderOptions.SetBitmapScalingMode(PagesHost, BitmapScalingMode.HighQuality);
		_zoomTimer = new DispatcherTimer();
		_zoomTimer.Interval = TimeSpan.FromMilliseconds(120.0);
		_zoomTimer.Tick += delegate
		{
			_zoomTimer.Stop();
			RenderOptions.SetBitmapScalingMode(PagesHost, BitmapScalingMode.HighQuality);
			SetImagesScalingMode(BitmapScalingMode.HighQuality);
			RenderPdfPages();
		};
		_smoothZoomTimer = new DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render);
		_smoothZoomTimer.Interval = TimeSpan.FromMilliseconds(10.0);
		_smoothZoomTimer.Tick += delegate
		{
			if (_targetZoom <= 0.0)
			{
				_smoothZoomTimer.Stop();
				return;
			}
			double delta = _targetZoom - CurrentZoom;
			if (Math.Abs(delta) < 0.001)
			{
				CurrentZoom = _targetZoom;
				_smoothZoomTimer.Stop();
				_zoomTimer.Stop();
				_zoomTimer.Start();
			}
			else
			{
				CurrentZoom += delta * 0.25;
			}
			ReportZoomChanged();
			double ratio = CurrentZoom / _baseZoomForLayout;
			ApplyZoomPreviewTransform(ratio);
			ScrollToKeepHostPointAtViewport(_smoothZoomHostAnchor, _smoothZoomAnchor, ratio, updateLayout: false);
		};
		_viewportTimer = new DispatcherTimer();
		_viewportTimer.Interval = TimeSpan.FromMilliseconds(100.0);
		_viewportTimer.Tick += delegate
		{
			_viewportTimer.Stop();
			UpdateSelectedPageFromViewport();
		};
		_smoothScrollTimer = new DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render);
		_smoothScrollTimer.Interval = TimeSpan.FromMilliseconds(10.0);
		_smoothScrollTimer.Tick += SmoothScrollTimer_Tick;
		DocumentScrollViewer.PreviewMouseWheel += DocumentScrollViewer_PreviewMouseWheel;
		DocumentScrollViewer.PreviewMouseDown += DocumentScrollViewer_PreviewMouseDown;
		DocumentScrollViewer.PreviewMouseMove += DocumentScrollViewer_PreviewMouseMove;
		DocumentScrollViewer.PreviewMouseUp += DocumentScrollViewer_PreviewMouseUp;
		DocumentScrollViewer.ScrollChanged += DocumentScrollViewer_ScrollChanged;
		PagesHost.SizeChanged += PagesHost_SizeChanged;
		base.Loaded += delegate
		{
			if (PageCount > 0 && _isFirstLoad)
			{
				_isFirstLoad = false;
				FitWidth();
			}
			else if (PageCount > 0 && _pageDimensions.Count == PageCount && PagesHost.Children.Count == 0)
			{
				RenderPdfPages();
			}
			if (ThumbnailContainer.Parent is ScrollViewer scrollViewer)
			{
				scrollViewer.ScrollChanged -= ThumbScroll_ScrollChanged;
				scrollViewer.ScrollChanged += ThumbScroll_ScrollChanged;
			}
		};
		base.Unloaded += delegate
		{
			CloseActiveDocument();
		};
		LoadDocument(path);
	}

	public async void LoadDocument(string path, bool clearPendingTextEdits = true)
	{
		Stopwatch totalSw = Stopwatch.StartNew();
		int loadGeneration = ++_loadGeneration;
		CurrentPdfPath = path;
		PageCount = 0;
		SelectedPageNumber = 1;
		CurrentZoom = 1.0;
		_baseZoomForLayout = 1.0;
		_targetZoom = 1.0;
		_smoothZoomTimer?.Stop();
		_smoothScrollTimer?.Stop();
		_targetVerticalOffset = -1;
		_targetHorizontalOffset = -1;
		_isFirstLoad = true;
		if (clearPendingTextEdits)
		{
			_pendingTextEdits.Clear();
		}
		_pendingZoomContentPoint = null;
		_pendingZoomHostPoint = null;
		_pendingZoomViewportPoint = null;
		_resetScrollAfterRender = true;
		_pageDimensions.Clear();
		_pageOrder.Clear();
		_selectedPages.Clear();
		_selectionAnchorPage = 1;
		_pageRotations.Clear();
		_ocrLoadingTasks.Clear();
		_recentPages.Clear();
		_bookmarkedPages.Clear();
		RefreshRecentPagesPanel();
		RefreshBookmarksPanel();
		UpdateBookmarkControlsState();
		ClearBitmapCache();
		ClearRenderQueue();
		CloseActiveDocument();
		ReportZoomChanged();
		LogStatus("Opening file: " + System.IO.Path.GetFileName(path));
		LogToDesktop("LoadDocument start: " + System.IO.Path.GetFileName(path));
		try
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			nint tempDoc = PdfiumEngine.FPDF_LoadDocument(path, null);
			stopwatch.Stop();
			LogToDesktop($"FPDF_LoadDocument: {stopwatch.ElapsedMilliseconds} ms");
			if (tempDoc == IntPtr.Zero)
			{
				MessageBox.Show("Unable to load the selected PDF file.", "Load error", MessageBoxButton.OK, MessageBoxImage.Hand);
				LogStatus("Failed to load PDF");
				LogToDesktop("LoadDocument failed: document handle is null");
				return;
			}
			_documentHandle = tempDoc;
			Stopwatch stopwatch2 = Stopwatch.StartNew();
			int pageCount = PdfiumEngine.FPDF_GetPageCount(tempDoc);
			stopwatch2.Stop();
			LogToDesktop($"FPDF_GetPageCount: {stopwatch2.ElapsedMilliseconds} ms (pages={pageCount})");
			if (pageCount < 0)
			{
				MessageBox.Show("Unable to load the selected PDF file.", "Load error", MessageBoxButton.OK, MessageBoxImage.Hand);
				LogStatus("Failed to load PDF");
				LogToDesktop("LoadDocument failed: invalid page count");
				CloseActiveDocument();
				return;
			}
			PageCount = pageCount;
			_pageOrder.Clear();
			_pageOrder.AddRange(Enumerable.Range(1, pageCount));
			LoadPersistedNavigationState(path);
			SelectedPageNumber = 1;
			_selectedPages.Clear();
			_selectedPages.Add(SelectedPageNumber);
			_selectionAnchorPage = SelectedPageNumber;
			ReportPageChanged();
			EmptyStateText.Visibility = Visibility.Collapsed;
			if (_isFirstLoad && DocumentScrollViewer.ViewportWidth > 100.0 && pageCount > 0)
			{
				Stopwatch stopwatch3 = Stopwatch.StartNew();
				if (PdfiumEngine.TryGetPageSizeByIndex(tempDoc, 0, out var width, out var height) && width > 0.0 && height > 0.0)
				{
					double val = (DocumentScrollViewer.ViewportWidth - 60.0) / (width * 1.33);
					double val2 = (DocumentScrollViewer.ViewportHeight - 60.0) / (height * 1.33);
					double value = Math.Min(val, val2);
					CurrentZoom = Math.Clamp(value, 0.1, 4.0);
					_baseZoomForLayout = CurrentZoom;
					_targetZoom = CurrentZoom;
					ResetZoomPreviewTransform();
					_pendingZoomContentPoint = null;
					_pendingZoomHostPoint = null;
					_pendingZoomViewportPoint = null;
					ReportZoomChanged();
				}
				stopwatch3.Stop();
				LogToDesktop($"First page size probe: {stopwatch3.ElapsedMilliseconds} ms");
				_isFirstLoad = false;
			}
			Stopwatch dimensionSw = Stopwatch.StartNew();
			List<Size> collection = await Task.Run(() => CollectPageDimensions(tempDoc, pageCount));
			dimensionSw.Stop();
			LogToDesktop($"CollectPageDimensions({pageCount}): {dimensionSw.ElapsedMilliseconds} ms");
			if (loadGeneration == _loadGeneration)
			{
				_pageDimensions.Clear();
				_pageDimensions.AddRange(collection);
				if (base.IsLoaded)
				{
					LogToDesktop("LoadDocument triggering initial render");
					FitWidth();
				}
			}
		}
		catch (DllNotFoundException)
		{
			MessageBox.Show("pdf_core.dll was not found. Make sure the Rust core has been built and copied next to the app.", "Missing dependency", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			CloseActiveDocument();
		}
		catch (Exception ex2)
		{
			MessageBox.Show("Unexpected error: " + ex2.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			LogToDesktop("LoadDocument exception: " + ex2.Message);
			CloseActiveDocument();
		}
		finally
		{
			totalSw.Stop();
			LogToDesktop($"LoadDocument total: {totalSw.ElapsedMilliseconds} ms");
		}
	}

	private void CloseActiveDocument()
	{
		CloseTextPages();
		_smoothScrollTimer?.Stop();
		_targetVerticalOffset = -1;
		_targetHorizontalOffset = -1;
		if (_documentHandle != IntPtr.Zero)
		{
			PdfiumEngine.CloseDocument(_documentHandle);
			_documentHandle = IntPtr.Zero;
			LogToDesktop("CloseActiveDocument closed the cached document handle.");
		}
	}

	private static List<Size> CollectPageDimensions(nint document, int pageCount)
	{
		List<Size> list = new List<Size>(pageCount);
		for (int i = 0; i < pageCount; i++)
		{
			double width = 0;
			double height = 0;
			bool success = false;
			lock (PdfiumEngine.SyncRoot)
			{
				success = PdfiumEngine.TryGetPageSizeByIndex(document, i, out width, out height);
			}
			if (success)
			{
				list.Add(new Size(width, height));
				continue;
			}
			nint num = IntPtr.Zero;
			lock (PdfiumEngine.SyncRoot)
			{
				num = PdfiumEngine.FPDF_LoadPage(document, i);
				if (num != IntPtr.Zero)
				{
					try
					{
						width = PdfiumEngine.FPDF_GetPageWidth(num);
						height = PdfiumEngine.FPDF_GetPageHeight(num);
					}
					finally
					{
						PdfiumEngine.FPDF_ClosePage(num);
					}
				}
			}
			if (num != IntPtr.Zero)
			{
				list.Add(new Size(width, height));
			}
			else
			{
				list.Add(new Size(800.0, 1100.0));
			}
		}
		return list;
	}

	private bool TryApplyInitialZoom()
	{
		if (!_isFirstLoad || PageCount <= 0 || _pageDimensions.Count == 0)
		{
			return true;
		}
		if (DocumentScrollViewer.ViewportWidth <= 100.0 || DocumentScrollViewer.ViewportHeight <= 100.0)
		{
			base.Dispatcher.BeginInvoke(new Action(RenderPdfPages), DispatcherPriority.Loaded);
			return false;
		}
		double width = _pageDimensions[0].Width;
		double height = _pageDimensions[0].Height;
		if (width <= 0.0 || height <= 0.0)
		{
			return true;
		}
		double val = (DocumentScrollViewer.ViewportWidth - 60.0) / (width * 1.33);
		double val2 = (DocumentScrollViewer.ViewportHeight - 60.0) / (height * 1.33);
		double value = Math.Min(val, val2);
		CurrentZoom = Math.Clamp(value, 0.1, 4.0);
		_baseZoomForLayout = CurrentZoom;
		_targetZoom = CurrentZoom;
		ResetZoomPreviewTransform();
		_pendingZoomContentPoint = null;
		_pendingZoomHostPoint = null;
		_pendingZoomViewportPoint = null;
		ReportZoomChanged();
		_isFirstLoad = false;
		return true;
	}

	public async Task RotateSelectedPageAsync(int deltaDegrees)
	{
		await Task.Yield();
		List<int> pages = GetSelectedPagesInOrder();
		if (pages.Count == 0)
		{
			pages.Add(Math.Clamp(SelectedPageNumber, 1, PageCount));
		}
		foreach (int pageNumber in pages)
		{
			RotatePageVisual(pageNumber, deltaDegrees);
		}
		LogStatus(pages.Count > 1 ? $"Rotated {pages.Count} selected pages." : $"Rotated page {pages[0]}.");
	}

	public async Task RotateCurrentPageAsync(int deltaDegrees)
	{
		await Task.Yield();
		if (PageCount > 0)
		{
			int pageNumber = Math.Clamp(SelectedPageNumber, 1, PageCount);
			RotatePageVisual(pageNumber, deltaDegrees);
			LogStatus($"Rotated page {pageNumber}.");
		}
	}

	public async Task RotateAllPagesAsync(int deltaDegrees)
	{
		await Task.Yield();
		for (int i = 1; i <= PageCount; i++)
		{
			RotatePageVisual(i, deltaDegrees);
		}
	}

	public void RotatePageVisual(int pageNumber, int deltaDegrees)
	{
		if (pageNumber <= 0 || pageNumber > PageCount)
		{
			return;
		}
		int value = 0;
		_pageRotations.TryGetValue(pageNumber, out value);
		int num = (value + deltaDegrees) % 360;
		if (num < 0)
		{
			num += 360;
		}
		_pageRotations[pageNumber] = num;
		StackPanel stackPanel = PagesHost.Children.OfType<StackPanel>().FirstOrDefault();
		if (stackPanel != null)
		{
			foreach (object child in stackPanel.Children)
			{
				if (!(child is Border { Tag: var tag } border) || !(tag is int num2) || num2 != pageNumber)
				{
					continue;
				}
				if (border.Child is Grid grid)
				{
					if (num == 0)
					{
						grid.LayoutTransform = null;
					}
					else
					{
						grid.LayoutTransform = new RotateTransform(num);
					}
				}
				break;
			}
		}
		foreach (object child2 in ThumbnailContainer.Children)
		{
			if (!(child2 is Border { Tag: var tag2 } border2) || !(tag2 is int num3) || num3 != pageNumber)
			{
				continue;
			}
			if (border2.Child is Image image)
			{
				if (num == 0)
				{
					image.LayoutTransform = null;
				}
				else
				{
					image.LayoutTransform = new RotateTransform(num);
				}
			}
			break;
		}
		LogStatus($"Đã xoay trang {pageNumber} trên màn hình ({num}°).");
	}

	public async Task<bool> SaveDocumentAsync(string? outputPath = null)
	{
		if (string.IsNullOrEmpty(CurrentPdfPath) || PageCount <= 0)
		{
			MessageBox.Show("Vui lòng mở một file PDF trước.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return false;
		}
		bool isOverwrite = string.IsNullOrEmpty(outputPath);
		string targetPath = outputPath ?? CurrentPdfPath;
		List<(int PageNumber, string ImagePath, double Left, double Bottom, double Width, double Height)> imageSigsData = new();
		foreach (var ann in Annotations.OfType<PdfSignatureAnnotation>())
		{
			if (ann.SignatureType == "Image" && !string.IsNullOrEmpty(ann.ImagePath) && File.Exists(ann.ImagePath))
			{
				int pageNum = ann.PageIndex + 1;
				Size pageSize = _pageDimensions[ann.PageIndex];
				double left = ann.X * pageSize.Width;
				double widthPoints = ann.Width * pageSize.Width;
				double heightPoints = ann.Height * pageSize.Height;
				double bottom = (1.0 - ann.Y - ann.Height) * pageSize.Height;
				imageSigsData.Add((pageNum, ann.ImagePath, left, bottom, widthPoints, heightPoints));
			}
		}

		bool isOrderChanged = false;
		for (int i = 0; i < _pageOrder.Count; i++)
		{
			if (_pageOrder[i] != i + 1)
			{
				isOrderChanged = true;
				break;
			}
		}
		List<KeyValuePair<int, int>> activeRotations = _pageRotations.Where((KeyValuePair<int, int> r) => r.Value != 0).ToList();
		bool hasPendingTextEdits = _pendingTextEdits.Count > 0;
		if (activeRotations.Count == 0 && !isOrderChanged && !hasPendingTextEdits && imageSigsData.Count == 0)
		{
			if (isOverwrite)
			{
				MessageBox.Show("Không có thay đổi nào để lưu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				return true;
			}
			try
			{
				File.Copy(CurrentPdfPath, targetPath, overwrite: true);
				MessageBox.Show("Lưu file thành công.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				return true;
			}
			catch (Exception ex)
			{
				MessageBox.Show("Lỗi khi sao chép file: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
				return false;
			}
		}
		LogStatus("Đang lưu các thay đổi...");
		string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
		List<(PendingTextEdit Edit, string ImagePath)> pendingTextEditImages = new List<(PendingTextEdit Edit, string ImagePath)>();
		try
		{
			File.Copy(CurrentPdfPath, tempFile, overwrite: true);
			if (hasPendingTextEdits)
			{
				foreach (PendingTextEdit pendingTextEdit in _pendingTextEdits)
				{
					pendingTextEditImages.Add((pendingTextEdit, RenderReplacementOverlayImage(pendingTextEdit)));
				}
			}
			if (!(await Task.Run(delegate
			{
				if (hasPendingTextEdits)
				{
					List<(PendingTextEdit Edit, string ImagePath)> pendingTextEdits = pendingTextEditImages.ToList();
					string workingFile = tempFile;
					foreach (var pendingTextEdit in pendingTextEdits)
					{
						string text = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
						bool flag = PdfInterop.PdfCore.overlay_pdf_image(workingFile, pendingTextEdit.Edit.PageNumber, pendingTextEdit.ImagePath, pendingTextEdit.Edit.Left, pendingTextEdit.Edit.Bottom, pendingTextEdit.Edit.Width, pendingTextEdit.Edit.Height, text);
						try
						{
							File.Delete(workingFile);
						}
						catch
						{
						}
						if (!flag)
						{
							try
							{
								File.Delete(text);
							}
							catch
							{
							}
							try
							{
								File.Delete(pendingTextEdit.ImagePath);
							}
							catch
							{
							}
							return false;
						}
						try
						{
							File.Delete(pendingTextEdit.ImagePath);
						}
						catch
						{
						}
						workingFile = text;
					}
					tempFile = workingFile;
				}

				if (imageSigsData.Count > 0)
				{
					string workingFile = tempFile;
					foreach (var sig in imageSigsData)
					{
						string text = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
						bool flag = PdfInterop.PdfCore.overlay_pdf_image(workingFile, sig.PageNumber, sig.ImagePath, sig.Left, sig.Bottom, sig.Width, sig.Height, text);
						try
						{
							File.Delete(workingFile);
						}
						catch
						{
						}
						if (!flag)
						{
							try
							{
								File.Delete(text);
							}
							catch
							{
							}
							return false;
						}
						workingFile = text;
					}
					tempFile = workingFile;
				}
				foreach (KeyValuePair<int, int> item in activeRotations)
				{
					int key = item.Key;
					int value = item.Value;
					string text = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
					bool flag = PdfInterop.PdfCore.rotate_pdf_page(tempFile, key, value, text);
					try
					{
						File.Delete(tempFile);
					}
					catch
					{
					}
					if (!flag)
					{
						return false;
					}
					tempFile = text;
				}
				if (isOrderChanged)
				{
					string text2 = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
					string orderSemicolon = string.Join(";", _pageOrder);
					bool flag2 = PdfInterop.PdfCore.reorder_pdf_pages(tempFile, orderSemicolon, text2);
					try
					{
						File.Delete(tempFile);
					}
					catch
					{
					}
					if (!flag2)
					{
						return false;
					}
					tempFile = text2;
				}
				return true;
			})))
			{
				MessageBox.Show("Không thể áp dụng các thay đổi vào file PDF.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
				LogStatus("Lưu thất bại");
				return false;
			}
			if (isOverwrite)
			{
				CloseActiveDocument();
			}
			try
			{
				File.Copy(tempFile, targetPath, overwrite: true);
			}
			catch (Exception ex2)
			{
				MessageBox.Show("Không thể ghi file đầu ra: " + ex2.Message + ". Có thể file đang mở bởi ứng dụng khác.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
				LogStatus("Lưu thất bại");
				if (isOverwrite)
				{
					LoadDocument(CurrentPdfPath, clearPendingTextEdits: false);
				}
				return false;
			}
			finally
			{
				try
				{
					File.Delete(tempFile);
				}
				catch
				{
				}
				foreach (var pendingTextEdit in pendingTextEditImages)
				{
					try
					{
						File.Delete(pendingTextEdit.ImagePath);
					}
					catch
					{
					}
				}
			}
			if (hasPendingTextEdits)
			{
				foreach (PendingTextEdit pendingTextEdit in _pendingTextEdits)
				{
					Annotations.Remove(pendingTextEdit.WhiteoutAnnotation);
					Annotations.Remove(pendingTextEdit.TextAnnotation);
				}
				_pendingTextEdits.Clear();
			}
			LoadDocument(targetPath);
			_pageRotations.Clear();
			RenderPdfPages();
			MessageBox.Show("Lưu file thành công.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			LogStatus("Đã lưu: " + System.IO.Path.GetFileName(targetPath));
			return true;
		}
		catch (Exception ex3)
		{
			MessageBox.Show("Đã xảy ra lỗi khi lưu: " + ex3.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
			LogStatus("Lưu thất bại");
			return false;
		}
	}

	public void MoveSelectedPage(int direction)
	{
		if (string.IsNullOrEmpty(CurrentPdfPath) || PageCount <= 0)
		{
			MessageBox.Show("Open a PDF first.", "Info", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}

		EnsurePageOrderInitialized();
		List<int> pages = GetSelectedPagesInOrder();
		if (pages.Count == 0)
		{
			pages.Add(Math.Clamp(SelectedPageNumber, 1, PageCount));
		}

		HashSet<int> selectedSet = new HashSet<int>(pages);
		bool moved = direction < 0 ? MoveSelectedPagesUp(selectedSet) : MoveSelectedPagesDown(selectedSet);
		if (!moved)
		{
			LogStatus(direction < 0 ? "Selected pages are already first." : "Selected pages are already last.");
			return;
		}

		SetSelectedPage(Math.Clamp(SelectedPageNumber, 1, PageCount));
		ThumbnailContainer.Children.Clear();
		RenderPdfPages();
		LogStatus(pages.Count == 1 ? $"Moved page {pages[0]} {(direction < 0 ? "up" : "down")}." : $"Moved {pages.Count} selected pages {(direction < 0 ? "up" : "down")}. Save the PDF to apply the new order.");
	}

	public void ReversePageOrder()
	{
		if (string.IsNullOrEmpty(CurrentPdfPath) || PageCount <= 0)
		{
			MessageBox.Show("Open a PDF first.", "Info", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}

		if (PageCount <= 1)
		{
			LogStatus("Cannot reverse a single-page PDF.");
			return;
		}

		if (_pageOrder.Count != PageCount)
		{
			_pageOrder.Clear();
			_pageOrder.AddRange(Enumerable.Range(1, PageCount));
		}

		int pageNumber = Math.Clamp(SelectedPageNumber, 1, PageCount);
		_pageOrder.Reverse();
		SetSelectedPage(pageNumber);
		ThumbnailContainer.Children.Clear();
		RenderPdfPages();
		LogStatus("Reversed page order. Save the PDF to apply the new order.");
	}

	public void ResetPageOrder()
	{
		if (string.IsNullOrEmpty(CurrentPdfPath) || PageCount <= 0)
		{
			MessageBox.Show("Open a PDF first.", "Info", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}

		bool isOrderChanged = _pageOrder.Count != PageCount;
		for (int i = 0; !isOrderChanged && i < _pageOrder.Count; i++)
		{
			isOrderChanged = _pageOrder[i] != i + 1;
		}

		if (!isOrderChanged)
		{
			LogStatus("Page order is already original.");
			return;
		}

		int pageNumber = Math.Clamp(SelectedPageNumber, 1, PageCount);
		_pageOrder.Clear();
		_pageOrder.AddRange(Enumerable.Range(1, PageCount));
		SetSelectedPage(pageNumber);
		ThumbnailContainer.Children.Clear();
		RenderPdfPages();
		LogStatus("Restored original page order.");
	}

	public async Task DeleteSelectedPageAsync()
	{
		if (string.IsNullOrEmpty(CurrentPdfPath) || PageCount <= 0)
		{
			MessageBox.Show("Open a PDF first.", "Info", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		if (PageCount <= 1)
		{
			MessageBox.Show("Cannot delete the last page.", "Info", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		List<int> pagesToDelete = GetSelectedPagesInOrder();
		if (pagesToDelete.Count == 0)
		{
			pagesToDelete.Add(Math.Clamp(SelectedPageNumber, 1, PageCount));
		}
		if (pagesToDelete.Count >= PageCount)
		{
			MessageBox.Show("Cannot delete all pages.", "Info", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}

		string suffix = pagesToDelete.Count == 1 ? $"without_page_{pagesToDelete[0]}" : $"without_{pagesToDelete.Count}_pages";
		string outputPath = PromptForOutputPath($"{System.IO.Path.GetFileNameWithoutExtension(CurrentPdfPath)}_{suffix}.pdf");
		if (outputPath == null)
		{
			return;
		}
		if (IsSamePath(CurrentPdfPath, outputPath))
		{
			MessageBox.Show("Choose an output file different from the source file.", "Info", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		LogStatus(pagesToDelete.Count == 1 ? $"Deleting page {pagesToDelete[0]}..." : $"Deleting {pagesToDelete.Count} selected pages...");
		bool success;
		HashSet<int> deleteSet = new HashSet<int>(pagesToDelete);
		List<int> currentOrder = _pageOrder.Count == PageCount ? _pageOrder.ToList() : Enumerable.Range(1, PageCount).ToList();
		List<int> pagesToKeepInOrder = currentOrder.Where(page => !deleteSet.Contains(page)).ToList();
		bool isOrderChanged = currentOrder.Select((page, index) => page != index + 1).Any(changed => changed);
		if (pagesToDelete.Count == 1 && !isOrderChanged)
		{
			int pageNumber = pagesToDelete[0];
			success = await Task.Run(() => PdfInterop.PdfCore.delete_pdf_page(CurrentPdfPath, pageNumber, outputPath));
		}
		else
		{
			string pagesToKeep = string.Join(";", pagesToKeepInOrder);
			success = await Task.Run(() => PdfInterop.PdfCore.reorder_pdf_pages(CurrentPdfPath, pagesToKeep, outputPath));
		}

		if (success)
		{
			MessageBox.Show(pagesToDelete.Count == 1 ? $"Page {pagesToDelete[0]} deleted successfully." : $"{pagesToDelete.Count} pages deleted successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			LogStatus("Saved updated PDF: " + System.IO.Path.GetFileName(outputPath));
			this.DocumentReloaded?.Invoke(this, outputPath);
		}
		else
		{
			MessageBox.Show("Unable to delete the selected page.", "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			LogStatus("Delete failed");
		}
	}

	public async Task OpenPageOrganizerAsync()
	{
		if (string.IsNullOrEmpty(CurrentPdfPath) || PageCount <= 0)
		{
			MessageBox.Show("Vui lòng mở một file PDF trước.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}

		PageOrganizerWindow organizer = new PageOrganizerWindow(CurrentPdfPath, _documentHandle);
		organizer.Owner = Window.GetWindow(this);
		if (organizer.ShowDialog() == true && !string.IsNullOrEmpty(organizer.SavedPdfPath))
		{
			try
			{
				string originalPath = CurrentPdfPath;
				CloseActiveDocument();
				File.Copy(organizer.SavedPdfPath, originalPath, true);
				LoadDocument(originalPath);
				LogStatus("Đã cập nhật cấu trúc trang tài liệu.");
			}
			catch (Exception ex)
			{
				MessageBox.Show("Không thể lưu thay đổi vào file gốc: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}
	}

	public async Task InsertBlankPageAsync()
	{
		if (string.IsNullOrEmpty(CurrentPdfPath))
		{
			MessageBox.Show("Open a PDF first.", "Info", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		Window window = Window.GetWindow(this);
		InsertPageOptionDialog optDialog = new InsertPageOptionDialog(SelectedPageNumber)
		{
			Owner = window
		};
		optDialog.ShowDialog();
		if (!optDialog.IsConfirmed)
		{
			return;
		}
		string value = (optDialog.InsertBefore ? "trước" : "sau");
		string outputPath = PromptForOutputPath(System.IO.Path.GetFileNameWithoutExtension(CurrentPdfPath) + "_with_blank_page.pdf");
		if (outputPath == null)
		{
			return;
		}
		if (IsSamePath(CurrentPdfPath, outputPath))
		{
			MessageBox.Show("Choose an output file different from the source file.", "Info", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		LogStatus($"Inserting a blank page {value} page {SelectedPageNumber}...");
		if (await Task.Run(() => PdfInterop.PdfCore.insert_blank_page(CurrentPdfPath, SelectedPageNumber, optDialog.InsertBefore, outputPath)))
		{
			MessageBox.Show("Blank page inserted successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			LogStatus("Saved updated PDF: " + System.IO.Path.GetFileName(outputPath));
			this.DocumentReloaded?.Invoke(this, outputPath);
		}
		else
		{
			MessageBox.Show("Unable to insert a blank page.", "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			LogStatus("Insert blank page failed");
		}
	}

	public async Task DuplicateSelectedPageAsync()
	{
		if (string.IsNullOrEmpty(CurrentPdfPath) || PageCount <= 0)
		{
			MessageBox.Show("Open a PDF first.", "Info", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}

		List<int> pages = GetSelectedPagesInOrder();
		if (pages.Count == 0)
		{
			pages.Add(Math.Clamp(SelectedPageNumber, 1, PageCount));
		}

		string suffix = pages.Count == 1 ? $"copy_page_{pages[0]}" : $"copy_{pages.Count}_pages";
		string outputPath = PromptForOutputPath($"{System.IO.Path.GetFileNameWithoutExtension(CurrentPdfPath)}_{suffix}.pdf");
		if (outputPath == null)
		{
			return;
		}

		if (IsSamePath(CurrentPdfPath, outputPath))
		{
			MessageBox.Show("Choose an output file different from the source file.", "Info", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}

		HashSet<int> duplicateSet = new HashSet<int>(pages);
		List<int> currentOrder = _pageOrder.Count == PageCount ? _pageOrder.ToList() : Enumerable.Range(1, PageCount).ToList();
		List<int> pageOrder = new List<int>();
		foreach (int page in currentOrder)
		{
			pageOrder.Add(page);
			if (duplicateSet.Contains(page))
			{
				pageOrder.Add(page);
			}
		}
		string orderSemicolon = string.Join(";", pageOrder);

		LogStatus(pages.Count == 1 ? $"Duplicating page {pages[0]}..." : $"Duplicating {pages.Count} selected pages...");
		if (await Task.Run(() => PdfInterop.PdfCore.reorder_pdf_pages(CurrentPdfPath, orderSemicolon, outputPath)))
		{
			MessageBox.Show(pages.Count == 1 ? $"Page {pages[0]} duplicated successfully." : $"{pages.Count} pages duplicated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			LogStatus("Saved updated PDF: " + System.IO.Path.GetFileName(outputPath));
			this.DocumentReloaded?.Invoke(this, outputPath);
		}
		else
		{
			MessageBox.Show("Unable to duplicate the selected page.", "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			LogStatus("Duplicate page failed");
		}
	}

	public async Task SplitCurrentPageAsync()
	{
		if (string.IsNullOrEmpty(CurrentPdfPath) || PageCount <= 0)
		{
			MessageBox.Show("Open a PDF first.", "Info", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}

		int pageNumber = Math.Clamp(SelectedPageNumber, 1, PageCount);
		string outputPath = PromptForOutputPath($"{System.IO.Path.GetFileNameWithoutExtension(CurrentPdfPath)}_page_{pageNumber}.pdf");
		if (outputPath == null)
		{
			return;
		}

		if (IsSamePath(CurrentPdfPath, outputPath))
		{
			MessageBox.Show("Choose an output file different from the source file.", "Info", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}

		LogStatus($"Splitting page {pageNumber}...");
		if (await Task.Run(() => PdfInterop.PdfCore.extract_pdf_pages(CurrentPdfPath, pageNumber.ToString(), outputPath)))
		{
			MessageBox.Show($"Page {pageNumber} exported successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			LogStatus("Saved split page: " + System.IO.Path.GetFileName(outputPath));
			this.DocumentReloaded?.Invoke(this, outputPath);
		}
		else
		{
			MessageBox.Show("Unable to split the selected page.", "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			LogStatus("Split page failed");
		}
	}

	private async Task ExtractSelectedPagesAsync()
	{
		if (string.IsNullOrEmpty(CurrentPdfPath) || PageCount <= 0)
		{
			MessageBox.Show("Open a PDF first.", "Info", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}

		List<int> pages = GetSelectedPagesInOrder();
		if (pages.Count == 0)
		{
			pages.Add(Math.Clamp(SelectedPageNumber, 1, PageCount));
		}

		string suffix = pages.Count == 1 ? $"page_{pages[0]}" : $"selected_{pages.Count}_pages";
		string outputPath = PromptForOutputPath($"{System.IO.Path.GetFileNameWithoutExtension(CurrentPdfPath)}_{suffix}.pdf");
		if (outputPath == null)
		{
			return;
		}

		if (IsSamePath(CurrentPdfPath, outputPath))
		{
			MessageBox.Show("Choose an output file different from the source file.", "Info", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}

		string pagesSemicolon = string.Join(";", pages);
		LogStatus(pages.Count == 1 ? $"Extracting page {pages[0]}..." : $"Extracting {pages.Count} selected pages...");
		if (await Task.Run(() => PdfInterop.PdfCore.extract_pdf_pages(CurrentPdfPath, pagesSemicolon, outputPath)))
		{
			MessageBox.Show(pages.Count == 1 ? $"Page {pages[0]} exported successfully." : $"{pages.Count} pages exported successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			LogStatus("Saved extracted PDF: " + System.IO.Path.GetFileName(outputPath));
			this.DocumentOpenRequested?.Invoke(this, outputPath);
		}
		else
		{
			MessageBox.Show("Unable to extract the selected pages.", "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			LogStatus("Extract selected pages failed");
		}
	}

	private string? PromptForOutputPath(string suggestedFileName)
	{
		if (string.IsNullOrEmpty(CurrentPdfPath))
		{
			return null;
		}
		SaveFileDialog saveFileDialog = new SaveFileDialog
		{
			Filter = "PDF documents (*.pdf)|*.pdf",
			Title = "Choose output file",
			FileName = suggestedFileName,
			InitialDirectory = System.IO.Path.GetDirectoryName(CurrentPdfPath)
		};
		if (saveFileDialog.ShowDialog() != true)
		{
			return null;
		}
		return saveFileDialog.FileName;
	}

	private bool IsSamePath(string left, string right)
	{
		return string.Equals(System.IO.Path.GetFullPath(left), System.IO.Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
	}





	private Border CreateMeasurementLabel(string text, Color color)
	{
		return new Border
		{
			Background = new SolidColorBrush(Color.FromArgb(220, 15, 23, 42)),
			BorderBrush = new SolidColorBrush(color),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(3.0),
			Padding = new Thickness(4.0, 2.0, 4.0, 2.0),
			IsHitTestVisible = false,
			Child = new TextBlock
			{
				Text = text,
				Foreground = Brushes.White,
				FontSize = 10.0,
				FontWeight = FontWeights.Bold
			}
		};
	}

	private double CalculatePolygonArea(List<Point> points, Size pageSize, double scale)
	{
		int n = points.Count;
		if (n < 3) return 0.0;
		double area = 0.0;
		for (int i = 0; i < n; i++)
		{
			Point p1 = points[i];
			Point p2 = points[(i + 1) % n];

			double x1 = p1.X * pageSize.Width * (25.4 / 72.0) / 1000.0 * scale;
			double y1 = p1.Y * pageSize.Height * (25.4 / 72.0) / 1000.0 * scale;
			double x2 = p2.X * pageSize.Width * (25.4 / 72.0) / 1000.0 * scale;
			double y2 = p2.Y * pageSize.Height * (25.4 / 72.0) / 1000.0 * scale;

			area += (x1 * y2) - (x2 * y1);
		}
		return Math.Abs(area / 2.0);
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

	private static void DrawArrowHeadOnCanvas(Canvas canvas, Point tip, Point target, Brush brush)
	{
		Vector vector = target - tip;
		if (vector.Length != 0.0)
		{
			vector.Normalize();
			Point point = tip + vector * 12.0;
			Vector vector2 = new Vector(0.0 - vector.Y, vector.X);
			Point value = point + vector2 * 6.0;
			Point value2 = point - vector2 * 6.0;
			Polygon element = new Polygon
			{
				Fill = brush,
				Points = new PointCollection { tip, value, value2 }
			};
			canvas.Children.Add(element);
		}
	}

	public void ApplyTheme(bool isDark)
	{
		ApplyTheme(AppThemeRegistry.Get(AppThemeRegistry.FromLegacyBool(isDark)));
	}

	internal void ApplyTheme(AppThemeDefinition theme)
	{
		base.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.WindowBackground));
		if (SidebarBorder != null)
		{
			SidebarBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.PanelBackground));
		}
		if (SidebarHeaderBorder != null)
		{
			SidebarHeaderBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.SurfaceBackground));
		}
		if (SidebarHeaderText != null)
		{
			SidebarHeaderText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.ForegroundPrimary));
		}
		if (CanvasBorder != null)
		{
			CanvasBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.SurfaceBackground));
		}
		if (EmptyStateText != null)
		{
			EmptyStateText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.ForegroundSecondary));
		}
	}

	private void LogStatus(string message)
	{
		LastStatusMessage = message;
		if (Dispatcher.CheckAccess())
		{
			this.StatusChanged?.Invoke(this, EventArgs.Empty);
		}
		else
		{
			Dispatcher.BeginInvoke(new Action(() => this.StatusChanged?.Invoke(this, EventArgs.Empty)));
		}
	}

	private void ReportZoomChanged()
	{
		this.ZoomChanged?.Invoke(this, EventArgs.Empty);
		UpdateRulers();
	}

	private void ReportPageChanged()
	{
		this.PageChanged?.Invoke(this, EventArgs.Empty);
	}

	public int BitmapCacheCount => _cacheManager?.Count ?? 0;
	public long BitmapCacheBytes => _cacheManager?.Bytes ?? 0;
	public Services.Cache.PdfCacheManager CacheManager => _cacheManager;

	public void ClearCacheAndRender()
	{
		ClearBitmapCache();
		RenderPdfPages();
	}
	private void LogToDesktop(string message)
	{
		try
		{
			string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
			string logPath = System.IO.Path.Combine(desktopPath, "debug_log.txt");
			string logMessage = $"{DateTime.Now:HH:mm:ss.fff} - {message}{Environment.NewLine}";
			File.AppendAllText(logPath, logMessage);
		}
		catch { /* Bỏ qua lỗi ghi log để không ảnh hưởng app */ }
	}
}