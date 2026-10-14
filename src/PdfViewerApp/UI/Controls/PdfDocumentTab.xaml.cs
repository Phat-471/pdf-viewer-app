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
using System.Windows.Controls.Primitives;
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


	private readonly record struct OcrTextRegion(string Text, double Left, double Bottom, double Width, double Height);

	private bool _isDrawing;
	private Rect? _selectedEditRectPdf = null;
	private int _selectedEditPageNumber = -1;
	private int _selectedEditCharIndex = -1;
	private bool _skipNextEditTextMouseUp;

	public bool HasOriginalFontEdits => _nativeTextEdits.Count > 0 || _inlineTextEditor?.HasDraft == true;

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

	// Snapshot display references; the bounds live in page coordinates.
	private Rectangle? _lastSnapshotOverlay;
	private Canvas? _lastSnapshotCanvas;

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

	private readonly List<int> _inherentPageRotations = new List<int>();

	private struct PageInfo
	{
		public Size Size;
		public int Rotation;
	}

	private readonly List<int> _pageOrder = new List<int>();

	private readonly HashSet<int> _selectedPages = new HashSet<int>();

	private readonly List<int> _recentPages = new List<int>();

	private readonly HashSet<int> _bookmarkedPages = new HashSet<int>();

	private readonly Services.Cache.PdfCacheManager _cacheManager;

	private readonly PriorityQueue<RenderQueueItem, (int Priority, long Sequence)> _renderQueue = new PriorityQueue<RenderQueueItem, (int, long)>();

	private readonly HashSet<string> _renderQueueKeys = new HashSet<string>(StringComparer.Ordinal);

	private bool _isSidebarVisible;

	private nint _documentHandle = IntPtr.Zero;
	private PdfRenderSession? _renderSession;
	private Task _documentCloseTask = Task.CompletedTask;
	private System.Threading.CancellationTokenSource _ocrLifetime = new();
	private System.Threading.CancellationTokenSource _renderCancellation = new();

	private Point? _pendingZoomContentPoint;

	private Point? _pendingZoomHostPoint;

	private Point? _pendingZoomViewportPoint;

	private double _pendingZoomBaseZoom = 1.0;

	private bool _resetScrollAfterRender;

	private int _renderGeneration;

	private int _loadGeneration;
	private bool _documentOpening;

	private bool _renderInProgress;

	private bool _renderAgainRequested;

	private bool _renderQueueWorkerRunning;

	private bool _zoomPreviewActive;

	private Size _zoomPreviewBaseHostSize;

	private DispatcherTimer _zoomTimer;

	private double _targetZoom = -1.0;

	private Point _smoothZoomAnchor;

	private Point _smoothZoomHostAnchor;

	private readonly Stack<DocumentEditSnapshot> _undoStack = new();

	private readonly Stack<DocumentEditSnapshot> _redoStack = new();

	private DispatcherTimer? _smoothZoomTimer;

	private double _targetVerticalOffset = -1;

	private double _targetHorizontalOffset = -1;

	private DispatcherTimer? _smoothScrollTimer;

	private readonly DispatcherTimer _viewportTimer;

	private double _baseZoomForLayout = 1.0;

	private bool _isFirstLoad = true;


	private readonly Dictionary<int, int> _pageRotations = new Dictionary<int, int>();

	private readonly ConcurrentDictionary<string, Task<List<OcrTextRegion>?>> _ocrLoadingTasks = new();


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

	private string _currentActiveTool = "Select";
	public string ActiveTool
	{
		get => _currentActiveTool;
		set
		{
			_currentActiveTool = value;
			if (_editingSnapshot && value != "Select")
			{
				_editingSnapshot = false;
				if (_lastSnapshotCanvas != null) DrawSnapshotOverlay(_lastSnapshotCanvas, _snapshotPageNumber);
			}
		}
	}

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

	public string LastStatusMessage { get; private set; } = "S蘯ｵn sﾃng";

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

		if (ActiveTool == "EditText")
		{
			FrameworkElement clickedElement = e.Source as FrameworkElement;
			while (clickedElement != null && clickedElement != canvas && !(clickedElement.Tag is PdfAnnotation))
			{
				clickedElement = VisualTreeHelper.GetParent(clickedElement) as FrameworkElement;
			}
			if (clickedElement != null && clickedElement.Tag is PdfTextBoxAnnotation clickedTextAnnotation && !string.IsNullOrEmpty(clickedTextAnnotation.Text))
			{
				SelectedAnnotation = clickedTextAnnotation;
				e.Handled = true;
				if (e.ClickCount >= 2)
				{
					ShowEditTextBoxInput(canvas, clickedTextAnnotation, num);
				}
				else
				{
					RedrawPageAnnotations(canvas, num);
				}
				return;
			}
		            if (canvas.IsMouseCaptured) canvas.ReleaseMouseCapture();
            _skipNextEditTextMouseUp = true;
            e.Handled = true;
            ShowDirectTextEditOverlay(canvas, -1, num, e.GetPosition(canvas));
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
						Point editClickPoint = e.GetPosition(canvas);
						LogToDesktop($"[EDIT TEXT] MouseDown char hit: page={num}, charIndex={charIndexAtMousePos}, canvas=({editClickPoint.X:F1},{editClickPoint.Y:F1})");
						if (canvas.IsMouseCaptured)
						{
							canvas.ReleaseMouseCapture();
						}
						_skipNextEditTextMouseUp = true;
						Dispatcher.BeginInvoke(new Action(() =>
						{
							LogToDesktop($"[EDIT TEXT] Opening editor from MouseDown dispatcher: page={num}, charIndex={charIndexAtMousePos}");
							ShowDirectTextEditOverlay(canvas, charIndexAtMousePos, num, editClickPoint);
						}), DispatcherPriority.Input);
						e.Handled = true;
						return;
					}
					else if (e.ClickCount >= 2)
					{
						// Dﾃnh cho SelectText/Highlight n蘯ｿu l盻｡ double click
						e.Handled = true;
						return;
					}
				}
				else
				{
					if (ActiveTool == "EditText" || ActiveTool == "SelectText" || ActiveTool == "Highlight")
					{
						LogStatus("Vﾃｹng nﾃy khﾃｴng cﾃｳ ch盻ｯ (PDF d蘯｡ng 蘯｣nh/scan). Hﾃ｣y th盻ｭ cﾃｴng c盻･ OCR ﾄ黛ｻ・nh蘯ｭn di盻㌻ ch盻ｯ trﾆｰ盻嫩.");
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
					LogStatus("Vﾃｹng nﾃy khﾃｴng cﾃｳ vﾄハ b蘯｣n cﾃｳ th盻・ch盻肱 (PDF d蘯｡ng 蘯｣nh/scan).");
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
				_drawStartPoint = ClampSnapshotPoint(canvas, _drawStartPoint);
				BeginSnapshotSelection(canvas);
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
				NoteText = "Nh蘯ｭp ghi chﾃｺ nhanh..."
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
				StampText = _tempStampText ?? "ﾄ静・DUY盻・",
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
				LogStatus($"ﾄ静｣ ch盻肱 ~{charSpan} kﾃｽ t盻ｱ. Nh蘯･n Ctrl+C ﾄ黛ｻ・sao chﾃｩp.");
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
			if (ActiveTool == "Snapshot" || ActiveTool == "AiSnapshot")
			{
				position6 = ClampSnapshotPoint(canvas, position6);
				_drawStartPoint = new Point(_snapshotDragStart.X * canvas.Width, _snapshotDragStart.Y * canvas.Height);
			}
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
				if (ActiveTool == "Snapshot" || ActiveTool == "AiSnapshot") UpdateSnapshotSelectionLabel(canvas, length, length2, width, height);
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
					LogStatus("ﾄ静｣ tﾃｴ mﾃu (Highlight) vﾃｹng ch盻ｯ ﾄ柁ｰ盻｣c ch盻肱.");
				}
				else
				{
					try
					{
						Clipboard.SetText(_selectedText);
						LogStatus($"ﾄ静｣ sao chﾃｩp {_selectedText.Length} kﾃｽ t盻ｱ vﾃo Clipboard (Ctrl+V ﾄ黛ｻ・dﾃ｡n).");
					}
					catch
					{
						LogStatus("ﾄ静｣ ch盻肱 vﾄハ b蘯｣n. Nh蘯･n Ctrl+C ﾄ黛ｻ・sao chﾃｩp.");
					}
				}
			}
			else
			{
				LogStatus("Khﾃｴng l蘯･y ﾄ柁ｰ盻｣c vﾄハ b蘯｣n. PDF nﾃy cﾃｳ th盻・lﾃ 蘯｣nh scan.");
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
                e.Handled = true;
                if (_skipNextEditTextMouseUp) { _skipNextEditTextMouseUp = false; return; }
                if (canvas.IsMouseCaptured) canvas.ReleaseMouseCapture();
                ShowDirectTextEditOverlay(canvas, -1, num, e.GetPosition(canvas));
                return;
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
			if (ActiveTool == "Snapshot" || ActiveTool == "AiSnapshot")
				_drawStartPoint = new Point(_snapshotDragStart.X * canvas.Width, _snapshotDragStart.Y * canvas.Height);
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
				position = ClampSnapshotPoint(canvas, position);
				double x2 = Math.Min(_drawStartPoint.X, position.X);
				double y2 = Math.Min(_drawStartPoint.Y, position.Y);
				double w2 = Math.Abs(_drawStartPoint.X - position.X);
				double h2 = Math.Abs(_drawStartPoint.Y - position.Y);
				Rectangle tempRect = _tempRect;
				_tempRect = null;
				if (!IsContinuousTool(ActiveTool))
				{
					ActiveTool = "Select";
				}
				EndMouseInteraction(canvas);
				// Replace the previous selected region.
				ClearLastSnapshotOverlay();
				// Keep the final boundary while the preview is open.
				KeepSnapshotOverlay(canvas, tempRect, x2, y2, w2, h2);
				PrintSnapshotSelection(canvas, null, x2, y2, w2, h2, num);
			}
			else if (ActiveTool == "AiSnapshot" && _tempRect != null)
			{
				position = ClampSnapshotPoint(canvas, position);
				double x3 = Math.Min(_drawStartPoint.X, position.X);
				double y3 = Math.Min(_drawStartPoint.Y, position.Y);
				double w3 = Math.Abs(_drawStartPoint.X - position.X);
				double h3 = Math.Abs(_drawStartPoint.Y - position.Y);
				Rectangle tempRect2 = _tempRect;
				_tempRect = null;
				if (!IsContinuousTool(ActiveTool))
				{
					ActiveTool = "Select";
				}
				EndMouseInteraction(canvas);
				RequestAiSnapshot(canvas, tempRect2, x3, y3, w3, h3, num);
				ClearSnapshotDragLabel();
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
					string? input = InputDialog.Show("Hieu chuan ty le", "Nhap khoang cach thuc te do duoc (met):", "5.0");
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


	private async void PrintSnapshotSelection(Canvas canvas, Rectangle? tempRect, double x, double y, double w, double h, int pageNumber)
	{
		if (tempRect != null) canvas.Children.Remove(tempRect);
		if (string.IsNullOrEmpty(CurrentPdfPath)) return;
		if (w <= 0 || h <= 0 || ((w < 10 || h < 10) && _snapshotBounds == null))
		{
			LogStatus("Vùng chọn Snapshot quá nhỏ. Hãy kéo chọn vùng lớn hơn.");
			return;
		}
		if (canvas.Width <= 0 || canvas.Height <= 0 || !TryGetPageSize(pageNumber, out Size pageSize)) return;
		double left = Math.Clamp(x / canvas.Width, 0, 1);
		double top = Math.Clamp(y / canvas.Height, 0, 1);
		double right = Math.Clamp((x + w) / canvas.Width, left, 1);
		double bottom = Math.Clamp((y + h) / canvas.Height, top, 1);
		if (right <= left || bottom <= top) return;
        string? printCopy = null;
        try
        {
            printCopy = await CreateNativeTextPrintCopyAsync();
		var snapshot = new PdfSnapshotSelection(printCopy ?? CurrentPdfPath, pageNumber - 1, left, top, right - left, bottom - top);
		var dialog = new SnapshotActionDialog(pageNumber, snapshot, pageSize, _renderSession)
		{
			Owner = Window.GetWindow(this), SourceDocumentPath = CurrentPdfPath
		};
		if (_lastSnapshotPaperLayout != null) dialog.RestoreLayout(_lastSnapshotPaperLayout);
        dialog.SelectionChanged += changed =>
        {
            _snapshotBounds = new Rect(changed.X, changed.Y, changed.Width, changed.Height);
            _snapshotPageNumber = changed.PageIndex + 1; DrawSnapshotOverlay(canvas, _snapshotPageNumber);
        };
        dialog.ShowDialog();
        _lastSnapshotPaperLayout = dialog.Layout;
        if (dialog.EditRegionRequested)
        {
            _editingSnapshot = true; ActiveTool = "Select";
            if (Window.GetWindow(this) is MainWindow main) main.ActiveTool = "Select";
            DrawSnapshotOverlay(canvas, pageNumber);
            LogStatus("Kéo vùng chọn để di chuyển, kéo góc để đổi kích thước. Chọn Xem trước để tiếp tục.");
        }
		if (dialog.ReselectRequested)
		{
			ClearLastSnapshotOverlay();
			ActiveTool = "Snapshot";
			if (Window.GetWindow(this) is MainWindow window) window.ActiveTool = "Snapshot";
			LogStatus("Kéo chọn vùng Snapshot mới.");
		}
        }
        catch (Exception ex) { MessageBox.Show("Không chuẩn bị được Snapshot: " + ex.Message, "Snapshot", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { if (printCopy != null) { try { File.Delete(printCopy); } catch { } } }
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
			Text = "Nh蘯ｭp ghi chﾃｺ...",
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
			if (!string.IsNullOrEmpty(text) && text != "Nh蘯ｭp ghi chﾃｺ...")
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
					LogStatus("ﾄ静｣ t蘯｡o h盻冪 vﾄハ b蘯｣n. Cﾃｴng c盻･ ﾄ妥｣ quay v盻・Select ﾄ黛ｻ・kﾃｩo ho蘯ｷc co giﾃ｣n khung.");
				}
			}
			RedrawPageAnnotations(canvas, pageNumber);
		};
		tbInput.PreviewKeyDown += delegate(object s, KeyEventArgs ev)
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
			Text = "Nh蘯ｭp ghi chﾃｺ...",
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
			if (!string.IsNullOrEmpty(text) && text != "Nh蘯ｭp ghi chﾃｺ...")
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
					LogStatus("ﾄ静｣ t蘯｡o mﾅｩi tﾃｪn ch盻・d蘯ｫn. Cﾃｴng c盻･ ﾄ妥｣ quay v盻・Select; mu盻創 t蘯｡o mﾅｩi tﾃｪn n盻ｯa hﾃ｣y ch盻肱 l蘯｡i.");
				}
			}
			RedrawPageAnnotations(canvas, pageNumber);
		};
		tbInput.PreviewKeyDown += delegate(object s, KeyEventArgs ev)
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
		ClearSnapshotDragLabel();
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
			Text = "Ghi chﾃｺ nhanh",
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
			Text = ((sticky.NoteText == "Nh蘯ｭp ghi chﾃｺ nhanh...") ? "" : sticky.NoteText),
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
		tbInput.PreviewKeyDown += delegate(object s, KeyEventArgs ev)
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
		tbInput.PreviewKeyDown += delegate(object s, KeyEventArgs ev)
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
			LogStatus("ﾄ静｣ sao chﾃｩp chﾃｺ thﾃｭch");
		}
	}

	public void PasteAnnotation(bool inPlace)
	{
		if (_copiedAnnotation == null)
		{
			LogStatus("Khﾃｴng cﾃｳ chﾃｺ thﾃｭch nﾃo ﾄ黛ｻ・dﾃ｡n");
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
		LogStatus(inPlace ? "Da dan chu thich tai cho" : "Da dan chu thich");
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
		pdfAnnotation.AnnotationGroupId = source.AnnotationGroupId;
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
		LogStatus(_isReadMode ? "ﾄ静｣ b蘯ｭt ch蘯ｿ ﾄ黛ｻ・ﾄ黛ｻ皇" : "ﾄ静｣ t蘯ｯt ch蘯ｿ ﾄ黛ｻ・ﾄ黛ｻ皇");
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
		LogStatus(_isFullScreen ? "ﾄ静｣ m盻・toﾃn mﾃn hﾃｬnh" : "ﾄ静｣ thoﾃ｡t toﾃn mﾃn hﾃｬnh");
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

	private void ContextFitPage_Click(object sender, RoutedEventArgs e) => FitPage();

	public void FitPage()
	{
		if (PageCount <= 0 || !TryGetPageSize(SelectedPageNumber, out Size size)) return;
		double width = size.Width, height = size.Height;
		if (_pageRotations.TryGetValue(SelectedPageNumber, out int rotation) && rotation % 180 != 0)
			(width, height) = (height, width);
		double zoom = Math.Min((DocumentScrollViewer.ViewportWidth - 40) / (width * 1.33),
			(DocumentScrollViewer.ViewportHeight - 40) / (height * 1.33));
		if (!double.IsFinite(zoom) || zoom <= 0) return;
		SetZoomPercent(zoom * 100);
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
					ClearDetailImages(grid);
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
			LogStatus("B蘯ｭt xem ﾄ黛ｺ｣o ngﾆｰ盻｣c");
		}
		else
		{
			PagesHost.Background = Brushes.Transparent;
			LogStatus("T蘯ｯt xem ﾄ黛ｺ｣o ngﾆｰ盻｣c");
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
		LogStatus(_isRulersEnabled ? "Hi盻㌻ thﾆｰ盻嫩 ﾄ双" : "蘯ｨn thﾆｰ盻嫩 ﾄ双");

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
		LogStatus(_isGuidesEnabled ? "Hi盻㌻ ﾄ柁ｰ盻拵g giﾃｳng" : "蘯ｨn ﾄ柁ｰ盻拵g giﾃｳng");
	}

	private void ContextHideNavigation_Click(object sender, RoutedEventArgs e)
	{
		ToggleSidebar();
	}

	public async void PrintPdf()
	{
		string? pdfPath = CurrentPdfPath;
		if (string.IsNullOrEmpty(pdfPath))
		{
			MessageBox.Show("Open a PDF first.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}
		if (App.IsPrinting) return;
		App.IsPrinting = true;
		string? printCopy = null;
		try
		{
			printCopy = await CreateNativeTextPrintCopyAsync();
			pdfPath = printCopy ?? pdfPath;
			var options = new PrintOptionsDialog(PageCount, SelectedPageNumber, pdfPath,
				_isReverseView ? null : _cacheManager.FindAnyCachedBitmapForPage(SelectedPageNumber, false))
			{ Owner = Window.GetWindow(this) };
			if (options.ShowDialog() != true) return;
			string printerName = options.SelectedPrinterName ?? throw new InvalidOperationException("Select a printer first.");
			PrintTicket ticket = options.SelectedPrintTicket?.Clone() ?? new PrintTicket();
			ticket.CopyCount = options.Copies;
			ticket.PageMediaSize = options.CreatePageMediaSize();
			ticket.PageOrientation = options.CreatePageOrientation();
			ApplyRequestedPageResolution(ticket, options.PrintDpi);
			using var ticketXml = ticket.GetXmlStream();
			byte[] ticketBytes = ticketXml.ToArray();
			int copies = options.Copies;
			double printDpi = options.PrintDpi;
			string engine = options.PrintEngineMode;
			string offsetMode = options.PrintOffsetMode;
			bool testFrame = options.PrintTestFrame;
			int startPage = options.StartPageIndex, endPage = options.EndPageIndex;
			bool fit = options.FitToPrintableArea, center = options.AutoCenter;
			bool separateJobs = options.NativeSeparatePageJobs, reverse = options.ReversePageOrder;
			bool rasterize = options.OptimizeCadDrawings;
			int[] pageIndices = options.PageIndices;
			int originalPageCount = PageCount;
			double scalePercent = options.ScalePercent;
			var annotations = Annotations.ToList();
			var progressDialog = new PrintProgressDialog { Owner = Window.GetWindow(this) };
			IProgress<PrintProgressInfo> progress = new Progress<PrintProgressInfo>(progressDialog.UpdateProgress);
			var cancellation = progressDialog.CancellationToken;
			progressDialog.Show();
			progress.Report(new PrintProgressInfo("Đang chuẩn bị máy in…", 0, 0, IsIndeterminate: true));
			try
			{
				await PrintWorker.RunAsync(() =>
				{
					cancellation.ThrowIfCancellationRequested();
					if (engine is "PdfDirect" or "PdfDirect_Optimized")
					{
						PdfDirectPrintJob.Print(pdfPath, printerName, pageIndices, originalPageCount, copies, reverse, progress, cancellation);
						return;
					}
					// Create all System.Printing objects on this worker, including the driver queries.
					using var server = new LocalPrintServer();
					using var queue = new PrintQueue(server, printerName, Array.Empty<string>());
					var workerTicket = new PrintTicket(new System.IO.MemoryStream(ticketBytes));
					var profile = PrinterPrintProfile.Resolve(queue);
					bool driverOffsets = offsetMode == "WpfOffset" ||
						(offsetMode != "Physical" && profile.DriverAlreadyOffsetsPrintableArea);
					LogToDesktop($"Print: {pdfPath}; printer={printerName}; engine={engine}; DPI={printDpi}; raster={rasterize}");
					if ((engine == "NativePdfium" || engine == "NativePdfium_Optimized") && !testFrame)
					{
						PrintTicket singleTicket = workerTicket.Clone();
						singleTicket.CopyCount = 1;
						using var converter = new PrintTicketConverter(printerName, queue.ClientPrintSchemaVersion);
						byte[] devMode = converter.ConvertPrintTicketToDevMode(singleTicket, BaseDevModeType.UserDefault);
						if (annotations.Count > 0) LogToDesktop("Native printing excludes app overlay annotations. WPF Bitmap includes them.");
						NativePdfPrinter.Print(pdfPath, printerName, devMode, startPage, endPage, copies, fit, center,
							driverOffsets, profile.RightSafetyPadding, profile.BottomSafetyPadding, separateJobs,
							reverse, rasterize, progress, cancellation, printDpi, scalePercent, pageIndices);
					}
					else
					{
						progress.Report(new PrintProgressInfo("Đang đọc vùng in của máy in…", 0, 0, IsIndeterminate: true));
						PrintCapabilities capabilities = queue.GetPrintCapabilities(workerTicket);
						double width = capabilities.OrientedPageMediaWidth ?? workerTicket.PageMediaSize?.Width ?? 816;
						double height = capabilities.OrientedPageMediaHeight ?? workerTicket.PageMediaSize?.Height ?? 1056;
						if (workerTicket.PageOrientation == PageOrientation.Landscape)
							(width, height) = (Math.Max(width, height), Math.Min(width, height));
						else if (workerTicket.PageOrientation == PageOrientation.Portrait)
							(width, height) = (Math.Min(width, height), Math.Max(width, height));
						using var paginator = new PdfDocumentPaginator(pdfPath)
						{
							StartPage = testFrame ? 0 : startPage, EndPage = testFrame ? 0 : endPage,
							AutoCenter = center, FitToPrintableArea = fit, PrintDpi = printDpi,
							PageIndices = testFrame ? new[] { 0 } : pageIndices, ScalePercent = scalePercent,
							ReversePageOrder = reverse, PrintProgress = progress, CancellationToken = cancellation,
							BottomSafetyPadding = profile.BottomSafetyPadding, RightSafetyPadding = profile.RightSafetyPadding,
							DriverAlreadyOffsetsPrintableArea = driverOffsets, PrintTestFrame = testFrame,
							PageSize = new Size(Math.Max(1, width), Math.Max(1, height))
						};
						paginator.Annotations.AddRange(annotations);
						if (capabilities.PageImageableArea is { } area)
							paginator.ImageableArea = new Rect(area.OriginWidth, area.OriginHeight, area.ExtentWidth, area.ExtentHeight);
						new PrintDialog { PrintQueue = queue, PrintTicket = workerTicket }
							.PrintDocument(paginator, System.IO.Path.GetFileName(pdfPath));
					}
				});
				progressDialog.MarkCompleted("Đã gửi lệnh in vào máy in.");
				LogStatus("Print job sent");
			}
			catch (OperationCanceledException)
			{
				progressDialog.MarkFailed("Đã huỷ lệnh in.");
				LogStatus("Print canceled");
			}
			catch (Exception ex)
			{
				LogToDesktop("Print failed: " + ex);
				progressDialog.MarkFailed("In thất bại: " + ex.Message);
				LogStatus("Print failed");
			}
		}
		catch (Exception ex)
		{
			LogToDesktop("Print setup failed: " + ex);
			MessageBox.Show("Không chuẩn bị được lệnh in: " + ex.Message, "Lỗi in", MessageBoxButton.OK, MessageBoxImage.Error);
		}
		finally
		{
			if (printCopy != null) { try { File.Delete(printCopy); } catch { } }
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
		LogToDesktop($"[EDIT TEXT] Hit-test start: page={pageNumber}, mouse=({mousePos.X:F1},{mousePos.Y:F1}), canvas={canvas.Width:F1}x{canvas.Height:F1}, zoom={CurrentZoom:F3}");
		nint textPage = GetTextPage(pageNumber);
		if (textPage == IntPtr.Zero)
		{
			LogToDesktop("[EDIT TEXT] Hit-test failed: text page is null.");
			return -1;
		}
		if (!TryCanvasToPdfPoint(canvas, mousePos, pageNumber, out var pdfPoint))
		{
			LogToDesktop("[EDIT TEXT] Hit-test failed: canvas point could not be converted to PDF point.");
			return -1;
		}
		// Increased tolerance from 10.0 to 20.0 for much more reliable character click detection
		double xTolerance = Math.Max(2.0, 20.0 * _pageDimensions[pageNumber - 1].Width / Math.Max(1.0, canvas.Width));
		double yTolerance = Math.Max(2.0, 20.0 * _pageDimensions[pageNumber - 1].Height / Math.Max(1.0, canvas.Height));
		lock (PdfiumEngine.SyncRoot)
		{
			int index = PdfiumEngine.FPDFText_GetCharIndexAtPos(textPage, pdfPoint.X, pdfPoint.Y, xTolerance, yTolerance);
			LogToDesktop($"[EDIT TEXT] Hit-test result: pdf=({pdfPoint.X:F2},{pdfPoint.Y:F2}), tolerance=({xTolerance:F2},{yTolerance:F2}), charIndex={index}");
			return index;
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
			LogToDesktop($"[EDIT TEXT] TryCanvasToPdfPoint failed: pageSize/canvas invalid. page={pageNumber}, canvas={canvas.Width:F1}x{canvas.Height:F1}");
			return false;
		}

		double nx = canvasPoint.X / canvas.Width;
		double ny = canvasPoint.Y / canvas.Height;

		int rotation = 0;
		int index = pageNumber - 1;
		if (index >= 0 && index < _inherentPageRotations.Count)
		{
			rotation = _inherentPageRotations[index];
		}

		rotation = ((rotation % 360) + 360) % 360;

		double x_p, y_p;
		if (rotation == 90)
		{
			double W_pdf = pageSize.Height;
			double H_pdf = pageSize.Width;
			x_p = ny * W_pdf;
			y_p = (1 - nx) * H_pdf;
		}
		else if (rotation == 180)
		{
			double W_pdf = pageSize.Width;
			double H_pdf = pageSize.Height;
			x_p = (1 - nx) * W_pdf;
			y_p = ny * H_pdf;
		}
		else if (rotation == 270)
		{
			double W_pdf = pageSize.Height;
			double H_pdf = pageSize.Width;
			x_p = (1 - ny) * W_pdf;
			y_p = nx * H_pdf;
		}
		else // 0 ho蘯ｷc gﾃｳc xoay khﾃ｡c
		{
			double W_pdf = pageSize.Width;
			double H_pdf = pageSize.Height;
			x_p = nx * W_pdf;
			y_p = (1 - ny) * H_pdf;
		}

		pdfPoint = new Point(x_p, y_p);
		LogToDesktop($"[EDIT TEXT] Canvas->PDF: rotation={rotation}, canvas=({canvasPoint.X:F1},{canvasPoint.Y:F1}), normalized=({nx:F4},{ny:F4}), pdf=({pdfPoint.X:F2},{pdfPoint.Y:F2}), pageSize={pageSize.Width:F2}x{pageSize.Height:F2}");
		return true;
	}

	private bool TryPdfRectToCanvasRect(Canvas canvas, int pageNumber, double left, double right, double bottom, double top, out Rect canvasRect)
	{
		canvasRect = Rect.Empty;
		if (!TryGetPageSize(pageNumber, out Size pageSize) || canvas.Width <= 0.0 || canvas.Height <= 0.0)
		{
			return false;
		}

		int rotation = 0;
		int index = pageNumber - 1;
		if (index >= 0 && index < _inherentPageRotations.Count)
		{
			rotation = _inherentPageRotations[index];
		}

		rotation = ((rotation % 360) + 360) % 360;

		double nx1, ny1, nx2, ny2;
		if (rotation == 90)
		{
			double W_pdf = pageSize.Height;
			double H_pdf = pageSize.Width;
			nx1 = 1 - (bottom / H_pdf);
			ny1 = left / W_pdf;
			nx2 = 1 - (top / H_pdf);
			ny2 = right / W_pdf;
		}
		else if (rotation == 180)
		{
			double W_pdf = pageSize.Width;
			double H_pdf = pageSize.Height;
			nx1 = 1 - (left / W_pdf);
			ny1 = bottom / H_pdf;
			nx2 = 1 - (right / W_pdf);
			ny2 = top / H_pdf;
		}
		else if (rotation == 270)
		{
			double W_pdf = pageSize.Height;
			double H_pdf = pageSize.Width;
			nx1 = bottom / H_pdf;
			ny1 = 1 - (left / W_pdf);
			nx2 = top / H_pdf;
			ny2 = 1 - (right / W_pdf);
		}
		else // 0 ho蘯ｷc khﾃ｡c
		{
			double W_pdf = pageSize.Width;
			double H_pdf = pageSize.Height;
			nx1 = left / W_pdf;
			ny1 = 1 - (bottom / H_pdf);
			nx2 = right / W_pdf;
			ny2 = 1 - (top / H_pdf);
		}

		double x1 = nx1 * canvas.Width;
		double y1 = ny1 * canvas.Height;
		double x2 = nx2 * canvas.Width;
		double y2 = ny2 * canvas.Height;

		double minX = Math.Min(x1, x2);
		double maxX = Math.Max(x1, x2);
		double minY = Math.Min(y1, y2);
		double maxY = Math.Max(y1, y2);

		canvasRect = new Rect(minX, minY, maxX - minX, maxY - minY);
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
		// 1. V蘯ｼ Cﾃ， KH盻蝕 HIGHLIGHT MﾃU VﾃNG ﾄ静・LﾆｯU Lﾃ劾 MﾃN Hﾃ君H
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
					Fill = new SolidColorBrush(Color.FromArgb(100, hlColor.R, hlColor.G, hlColor.B)), // Vﾃng trong su盻奏 ﾄ黛ｻ・khﾃｴng che ch盻ｯ
					IsHitTestVisible = false // B盻・qua b蘯ｯt chu盻冲
				};

				Canvas.SetLeft(rect, canvasX);
				Canvas.SetTop(rect, canvasY);

				// ﾄ雪ｺｩy l盻孅 mﾃu vﾃng xu盻創g dﾆｰ盻嬖 cﾃｹng ﾄ黛ｻ・nﾃｩt ch盻ｯ n盻品 lﾃｪn trﾃｪn
				if (canvas.Children.Count > 0) canvas.Children.Insert(0, rect);
				else canvas.Children.Add(rect);
			}
		}

		// 2. V蘯ｼ KH盻蝕 MﾃU XANH T蘯M TH盻廬 KHI ﾄ植NG GI盻ｮ CHU盻狼 Kﾃ碓 Bﾃ祢 ﾄ職N
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

	/// Vẽ khung bao (bounding box) quanh TẤT CẢ các dòng text có thể sửa được trên trang,
	/// giống Foxit: khi vào chế độ Edit Text, người dùng thấy ngay vùng nào sửa được.
	private void DrawAllEditTextBounds(Canvas canvas, int pageNumber)
	{
		nint textPage = GetTextPage(pageNumber);
		if (textPage == IntPtr.Zero) return;

		int charCount;
		lock (PdfiumEngine.SyncRoot) { charCount = PdfiumEngine.FPDFText_CountChars(textPage); }
		if (charCount <= 0) return;

		var mergedRects = new List<Rect>();
		lock (PdfiumEngine.SyncRoot)
		{
			Rect currentRect = Rect.Empty;
			for (int i = 0; i < charCount; i++)
			{
				if (PdfiumEngine.FPDFText_GetCharBox(textPage, i, out var left, out var right, out var bottom, out var top))
				{
					if (!TryPdfRectToCanvasRect(canvas, pageNumber, left, right, bottom, top, out Rect canvasRect)) continue;
					if (canvasRect.Width <= 0.0) canvasRect.Width = 6.0;
					if (canvasRect.Height <= 0.0) canvasRect.Height = 12.0;

					if (currentRect.IsEmpty)
					{
						currentRect = canvasRect;
					}
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
			System.Windows.Shapes.Rectangle border = new System.Windows.Shapes.Rectangle
			{
				Width = Math.Max(0.5, rect.Width + 2.0),
				Height = Math.Max(0.5, rect.Height + 2.0),
				Stroke = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
				StrokeThickness = 1.0,
				StrokeDashArray = new DoubleCollection(new double[] { 3, 3 }),
				Fill = new SolidColorBrush(Color.FromArgb(24, 37, 99, 235)),
				IsHitTestVisible = false
			};
			Canvas.SetLeft(border, rect.X - 1.0);
			Canvas.SetTop(border, rect.Y - 1.0);
			canvas.Children.Add(border);
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
				LogStatus("ﾄ静｣ sao chﾃｩp vﾄハ b蘯｣n vﾃo Clipboard");
			}
			catch (Exception ex)
			{
				MessageBox.Show("L盻擁 sao chﾃｩp: " + ex.Message, "L盻擁", MessageBoxButton.OK, MessageBoxImage.Hand);
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
	private string CleanPdfFontName(string rawName)
	{
		if (string.IsNullOrEmpty(rawName)) return ActiveFontFamily;

		int plusIdx = rawName.IndexOf('+');
		if (plusIdx >= 0 && plusIdx < rawName.Length - 1)
		{
			rawName = rawName.Substring(plusIdx + 1);
		}

		int commaIdx = rawName.IndexOf(',');
		if (commaIdx >= 0)
		{
			rawName = rawName.Substring(0, commaIdx);
		}

		int hyphenIdx = rawName.IndexOf('-');
		if (hyphenIdx >= 0)
		{
			string baseName = rawName.Substring(0, hyphenIdx);
			if (baseName.Equals("Arial", StringComparison.OrdinalIgnoreCase) ||
			    baseName.Equals("Helvetica", StringComparison.OrdinalIgnoreCase) ||
			    baseName.Equals("Times", StringComparison.OrdinalIgnoreCase) ||
			    baseName.Equals("Courier", StringComparison.OrdinalIgnoreCase))
			{
				rawName = baseName;
			}
		}

		if (rawName.Equals("Helvetica", StringComparison.OrdinalIgnoreCase) ||
		    rawName.Equals("ArialMT", StringComparison.OrdinalIgnoreCase) ||
		    rawName.StartsWith("Arial", StringComparison.OrdinalIgnoreCase))
		{
			return "Arial";
		}
		if (rawName.Equals("Times", StringComparison.OrdinalIgnoreCase) ||
		    rawName.Equals("TimesNewRomanPSMT", StringComparison.OrdinalIgnoreCase) ||
		    rawName.StartsWith("TimesNewRoman", StringComparison.OrdinalIgnoreCase) ||
		    rawName.Equals("TimesNewRomanPS-BoldMT", StringComparison.OrdinalIgnoreCase) ||
		    rawName.Equals("TimesNewRomanPS-ItalicMT", StringComparison.OrdinalIgnoreCase) ||
		    rawName.Equals("TimesNewRomanPS-BoldItalicMT", StringComparison.OrdinalIgnoreCase))
		{
			return "Times New Roman";
		}
		if (rawName.Equals("Courier", StringComparison.OrdinalIgnoreCase) ||
		    rawName.Equals("CourierNewPSMT", StringComparison.OrdinalIgnoreCase) ||
		    rawName.StartsWith("Courier", StringComparison.OrdinalIgnoreCase))
		{
			return "Courier New";
		}

		return rawName;
	}

	private static bool LooksLikeBrokenExtractedText(string text)
	{
		if (string.IsNullOrWhiteSpace(text)) return false;
		const string commonPunctuation = ".,;:!?'\"()[]{}-/\\&%@#*+=<>|~`^_$";
		int lettersOrDigits = 0;
		int suspicious = 0;
		int total = 0;
		foreach (char c in text)
		{
			if (char.IsWhiteSpace(c)) continue;
			total++;
			if (char.IsControl(c) || (c >= '\uFF00' && c <= '\uFFEF'))
			{
				suspicious++;
				continue;
			}
			if (char.IsLetterOrDigit(c)) { lettersOrDigits++; continue; }
			if (commonPunctuation.IndexOf(c) >= 0) continue;
			if (c == '\uFFFD' || c >= '\uE000' || c > '\uF000' || c == '\u00A4')
			{
				suspicious++;
			}
		}

		if (total == 0) return false;
		return suspicious > 0 || (double)lettersOrDigits / total < 0.35;
	}

	private static Color SamplePdfPageBackgroundColor(Canvas canvas, Rect canvasRect)
	{
		try
		{
			Image? pageImage = null;
			if (canvas.Parent is Grid grid)
			{
				pageImage = grid.Children.OfType<Image>().FirstOrDefault();
			}

			if (pageImage?.Source is not BitmapSource bitmap)
			{
				return Colors.White;
			}

			double canvasWidth = canvas.ActualWidth > 1.0 ? canvas.ActualWidth : canvas.Width;
			double canvasHeight = canvas.ActualHeight > 1.0 ? canvas.ActualHeight : canvas.Height;
			if (canvasWidth <= 1.0 || canvasHeight <= 1.0 || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
			{
				return Colors.White;
			}

			BitmapSource source = bitmap.Format == PixelFormats.Bgra32 || bitmap.Format == PixelFormats.Pbgra32
				? bitmap
				: new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0.0);

			double scaleX = source.PixelWidth / canvasWidth;
			double scaleY = source.PixelHeight / canvasHeight;
			Rect clipped = Rect.Intersect(new Rect(0.0, 0.0, canvasWidth, canvasHeight), canvasRect);
			if (clipped.IsEmpty)
			{
				return Colors.White;
			}

			int x0 = Math.Clamp((int)Math.Floor(clipped.Left * scaleX), 0, source.PixelWidth - 1);
			int y0 = Math.Clamp((int)Math.Floor(clipped.Top * scaleY), 0, source.PixelHeight - 1);
			int x1 = Math.Clamp((int)Math.Ceiling(clipped.Right * scaleX), x0 + 1, source.PixelWidth);
			int y1 = Math.Clamp((int)Math.Ceiling(clipped.Bottom * scaleY), y0 + 1, source.PixelHeight);
			int width = x1 - x0;
			int height = y1 - y0;
			int stride = width * 4;
			byte[] pixels = new byte[stride * height];
			source.CopyPixels(new Int32Rect(x0, y0, width, height), pixels, stride, 0);

			double r = 0.0, g = 0.0, b = 0.0;
			int samples = 0;
			int stepX = Math.Max(1, width / 80);
			int stepY = Math.Max(1, height / 18);
			for (int y = 0; y < height; y += stepY)
			{
				for (int x = 0; x < width; x += stepX)
				{
					int offset = y * stride + x * 4;
					byte blue = pixels[offset];
					byte green = pixels[offset + 1];
					byte red = pixels[offset + 2];
					byte alpha = pixels[offset + 3];
					if (alpha < 10) continue;
					double luminance = 0.2126 * red + 0.7152 * green + 0.0722 * blue;
					if (luminance < 170.0) continue;
					r += red;
					g += green;
					b += blue;
					samples++;
				}
			}

			if (samples < 8)
			{
				r = g = b = 0.0;
				samples = 0;
				for (int y = 0; y < height; y += stepY)
				{
					for (int x = 0; x < width; x += stepX)
					{
						int offset = y * stride + x * 4;
						byte blue = pixels[offset];
						byte green = pixels[offset + 1];
						byte red = pixels[offset + 2];
						byte alpha = pixels[offset + 3];
						if (alpha < 10) continue;
						r += red;
						g += green;
						b += blue;
						samples++;
					}
				}
			}

			if (samples == 0)
			{
				return Colors.White;
			}

			return Color.FromRgb((byte)Math.Clamp((int)Math.Round(r / samples), 0, 255),
				(byte)Math.Clamp((int)Math.Round(g / samples), 0, 255),
				(byte)Math.Clamp((int)Math.Round(b / samples), 0, 255));
		}
		catch
		{
			return Colors.White;
		}
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
		SetSidebarVisibility(true);
		RenderOptions.SetBitmapScalingMode(PagesHost, BitmapScalingMode.HighQuality);
		_zoomTimer = new DispatcherTimer();
		_zoomTimer.Interval = TimeSpan.FromMilliseconds(160.0);
		_zoomTimer.Tick += delegate
		{
			_zoomTimer.Stop();
			RenderOptions.SetBitmapScalingMode(PagesHost, BitmapScalingMode.HighQuality);
			SetImagesScalingMode(BitmapScalingMode.HighQuality);
			CommitZoomLayout();
		};
		_smoothZoomTimer = new DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render);
		_smoothZoomTimer.Interval = TimeSpan.FromMilliseconds(16.0);
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
			if (!_zoomPreviewActive) UpdateSelectedPageFromViewport();
			UpdateRulers();
		};
		_smoothScrollTimer = new DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render);
		_smoothScrollTimer.Interval = TimeSpan.FromMilliseconds(16.0);
		_smoothScrollTimer.Tick += SmoothScrollTimer_Tick;
		DocumentScrollViewer.PreviewMouseWheel += DocumentScrollViewer_PreviewMouseWheel;
		DocumentScrollViewer.PreviewMouseDown += DocumentScrollViewer_PreviewMouseDown;
		DocumentScrollViewer.PreviewMouseMove += DocumentScrollViewer_PreviewMouseMove;
		DocumentScrollViewer.PreviewMouseUp += DocumentScrollViewer_PreviewMouseUp;
		DocumentScrollViewer.ScrollChanged += DocumentScrollViewer_ScrollChanged;
		PagesHost.SizeChanged += PagesHost_SizeChanged;
		base.Loaded += delegate
		{
			if (_documentHandle == IntPtr.Zero && !_documentOpening && !string.IsNullOrEmpty(CurrentPdfPath))
			{
				if (PageCount == 0) LoadDocument(CurrentPdfPath);
				else _ = ResumeDocumentHandlesAsync();
			}
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
			_loadGeneration++;
			_renderGeneration++;
			_documentOpening = false;
			_smoothZoomTimer?.Stop();
			_zoomTimer.Stop();
			_viewportTimer.Stop();
			ResetZoomPreviewTransform();
			ClearRenderQueue();
			CloseActiveDocument();
		};
		LoadDocument(path);
	}

	public async void LoadDocument(string path, bool clearPendingTextEdits = true)
	{
		CancelSnapshotSelection();
		Stopwatch totalSw = Stopwatch.StartNew();
		int loadGeneration = ++_loadGeneration;
		_documentOpening = true;
		_renderGeneration++;
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
			ResetNativeTextEditing();
		}
		_pendingZoomContentPoint = null;
		_pendingZoomHostPoint = null;
		_pendingZoomViewportPoint = null;
		_resetScrollAfterRender = true;
		_pageDimensions.Clear();
		_inherentPageRotations.Clear();
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
			var opened = await Task.Run(() =>
			{
				lock (PdfiumEngine.SyncRoot)
				{
					PdfiumEngine.Initialize();
					nint handle = PdfiumEngine.FPDF_LoadDocument(path, null);
					if (handle == IntPtr.Zero) return (Handle: handle, Pages: new List<PageInfo>());
					try { return (Handle: handle, Pages: CollectPageInfo(handle, PdfiumEngine.FPDF_GetPageCount(handle))); }
					catch { PdfiumEngine.CloseDocument(handle); throw; }
				}
			});
			stopwatch.Stop();
			LogToDesktop($"Open document and page metadata: {stopwatch.ElapsedMilliseconds} ms");
			nint tempDoc = opened.Handle;
			if (loadGeneration != _loadGeneration)
			{
				if (tempDoc != IntPtr.Zero) _ = Task.Run(() => PdfiumEngine.CloseDocument(tempDoc));
				return;
			}
			if (tempDoc == IntPtr.Zero)
			{
				LogStatus("Failed to load PDF");
				return;
			}
			_documentHandle = tempDoc;
			_renderSession = new PdfRenderSession(path, _nativeTextEdits.ToArray());
			Stopwatch stopwatch2 = Stopwatch.StartNew();
			int pageCount = opened.Pages.Count;
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
				double width = opened.Pages[0].Size.Width;
				double height = opened.Pages[0].Size.Height;
				if (width > 0.0 && height > 0.0)
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
			List<PageInfo> collection = opened.Pages;
			dimensionSw.Stop();
			LogToDesktop($"CollectPageInfo({pageCount}): {dimensionSw.ElapsedMilliseconds} ms");
			if (loadGeneration == _loadGeneration)
			{
				_pageDimensions.Clear();
				_inherentPageRotations.Clear();
				foreach (var info in collection)
				{
					_pageDimensions.Add(info.Size);
					_inherentPageRotations.Add(info.Rotation);
				}
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
			if (loadGeneration == _loadGeneration) _documentOpening = false;
			totalSw.Stop();
			LogToDesktop($"LoadDocument total: {totalSw.ElapsedMilliseconds} ms");
		}
	}

	private async Task ResumeDocumentHandlesAsync(bool throwOnFailure = false)
	{
		int generation = _loadGeneration;
		string path = CurrentPdfPath;
		_documentOpening = true;
		var edits = _nativeTextEdits.ToArray();
		int selectedPage = SelectedPageNumber;
		_renderSession = new PdfRenderSession(path, edits);
		_detailViewportKey = string.Empty;
		try
		{
			var opened = await Task.Run(() =>
			{
				lock (PdfiumEngine.SyncRoot)
				{
					PdfiumEngine.Initialize();
					nint handle = PdfiumEngine.FPDF_LoadDocument(path, null), page = 0, text = 0;
                    if (handle == 0) throw new InvalidOperationException("Không mở lại được PDF.");
                    try
                    {
                        foreach (var group in edits.GroupBy(e => e.PageNumber))
                        {
                            nint editedPage = PdfiumEngine.FPDF_LoadPage(handle, group.Key - 1);
                            if (editedPage == 0) throw new InvalidOperationException("Không đọc được trang vừa sửa.");
                            try
                            {
                                PdfNativeTextEditor.ApplyPage(handle, editedPage, group.ToArray());
                                if (group.Key == selectedPage)
                                {
                                    page = editedPage; editedPage = 0;
                                    text = PdfiumEngine.FPDFText_LoadPage(page);
                                    if (text == 0) throw new InvalidOperationException("Không đọc lại được chữ vừa áp dụng.");
                                }
                            }
                            finally { if (editedPage != 0) PdfiumEngine.FPDF_ClosePage(editedPage); }
                        }
                        return (Handle: handle, Page: page, Text: text);
                    }
                    catch
                    {
                        if (text != 0) PdfiumEngine.FPDFText_ClosePage(text);
                        if (page != 0) PdfiumEngine.FPDF_ClosePage(page);
                        PdfiumEngine.FPDF_CloseDocument(handle); throw;
                    }
				}
			});
			if (generation != _loadGeneration)
			{
				_ = Task.Run(() =>
                {
                    lock (PdfiumEngine.SyncRoot)
                    {
                        if (opened.Text != 0) PdfiumEngine.FPDFText_ClosePage(opened.Text);
                        if (opened.Page != 0) PdfiumEngine.FPDF_ClosePage(opened.Page);
                        PdfiumEngine.FPDF_CloseDocument(opened.Handle);
                    }
                });
				return;
			}
			_documentHandle = opened.Handle;
            if (opened.Page != 0) _openedPdfPages[selectedPage] = opened.Page;
            if (opened.Text != 0) _textPages[selectedPage] = opened.Text;
			RequestViewportRefresh();
		}
		catch (Exception ex) { LogStatus("Không mở lại được tài liệu: " + ex.Message); if (throwOnFailure) throw; }
		finally { if (generation == _loadGeneration) _documentOpening = false; }
	}

	private void CloseActiveDocument(bool preserveInlineEditor = false)
	{
		if (!preserveInlineEditor) CancelInlineTextEditor();
		_ocrLifetime.Cancel();
		_ocrLifetime.Dispose();
		_ocrLifetime = new System.Threading.CancellationTokenSource();
		_ocrLoadingTasks.Clear();
		CancelDetailRendering();
		_renderCancellation.Cancel();
		_renderCancellation.Dispose();
		_renderCancellation = new System.Threading.CancellationTokenSource();
		var closedSession = _renderSession;
		closedSession?.Dispose();
		_renderSession = null;
		_detailGeneration++;
		_detailViewportKey = string.Empty;
		_smoothScrollTimer?.Stop();
		_targetVerticalOffset = -1;
		_targetHorizontalOffset = -1;
		var textPages = _textPages.Values.ToArray();
		var pages = _openedPdfPages.Values.ToArray();
		_textPages.Clear();
		_openedPdfPages.Clear();
		nint document = _documentHandle;
		_documentHandle = IntPtr.Zero;
		var closeDocument = Task.Run(() =>
		{
			lock (PdfiumEngine.SyncRoot)
			{
				foreach (nint text in textPages) if (text != IntPtr.Zero) PdfiumEngine.FPDFText_ClosePage(text);
				foreach (nint page in pages) if (page != IntPtr.Zero) PdfiumEngine.FPDF_ClosePage(page);
				if (document != IntPtr.Zero) PdfiumEngine.CloseDocument(document);
			}
		});
		_documentCloseTask = Task.WhenAll(closeDocument, closedSession?.Closed ?? Task.CompletedTask);
	}

	private static List<PageInfo> CollectPageInfo(nint document, int pageCount)
	{
		List<PageInfo> list = new List<PageInfo>(pageCount);
		for (int i = 0; i < pageCount; i++)
		{
			double width = 0;
			double height = 0;
			int rotation = 0;
			nint num = IntPtr.Zero;
			lock (PdfiumEngine.SyncRoot)
			{
				if (PdfiumEngine.TryGetPageSizeByIndex(document, i, out width, out height) && width > 0 && height > 0)
				{
					list.Add(new PageInfo { Size = new Size(width, height), Rotation = 0 });
					continue;
				}
				num = PdfiumEngine.FPDF_LoadPage(document, i);
				if (num != IntPtr.Zero)
				{
					try
					{
						width = PdfiumEngine.FPDF_GetPageWidth(num);
						height = PdfiumEngine.FPDF_GetPageHeight(num);
						rotation = PdfInterop.Pdfium.FPDFPage_GetRotation(num) * 90;
					}
					finally
					{
						PdfiumEngine.FPDF_ClosePage(num);
					}
				}
			}
			if (num != IntPtr.Zero)
			{
				list.Add(new PageInfo { Size = new Size(width, height), Rotation = rotation });
			}
			else
			{
				list.Add(new PageInfo { Size = new Size(800.0, 1100.0), Rotation = 0 });
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
		LogStatus($"ﾄ静｣ xoay trang {pageNumber} trﾃｪn mﾃn hﾃｬnh ({num}ﾂｰ).");
	}

	private async Task<bool> SaveDocumentCoreAsync(string? outputPath = null)
	{
		if (string.IsNullOrEmpty(CurrentPdfPath) || PageCount <= 0)
		{
			MessageBox.Show("Vui lòng mở PDF trước khi lưu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return false;
		}
		string sourcePath = CurrentPdfPath;
		int saveGeneration = _loadGeneration, textRevision = _textEditRevision;
		bool isOverwrite = string.IsNullOrEmpty(outputPath) ||
            System.IO.Path.GetFullPath(outputPath).Equals(System.IO.Path.GetFullPath(CurrentPdfPath), StringComparison.OrdinalIgnoreCase);
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
		var nativeTextEdits = _nativeTextEdits.ToArray();
		bool hasPendingTextEdits = nativeTextEdits.Length > 0;
		if (activeRotations.Count == 0 && !isOrderChanged && !hasPendingTextEdits && imageSigsData.Count == 0)
		{
			if (isOverwrite)
			{
				MessageBox.Show("Không có thay đổi để lưu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				return true;
			}
			try
			{
				File.Copy(CurrentPdfPath, targetPath, overwrite: true);
				MessageBox.Show("Đã lưu PDF.", "Đã lưu", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				return true;
			}
			catch (Exception ex)
			{
				MessageBox.Show("Không sao chép được PDF: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
				return false;
			}
		}
		LogStatus("Đang lưu thay đổi vào PDF…");
		string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
		try
		{
			if (nativeTextEdits.Length > 0)
				await Task.Run(() => PdfNativeTextEditor.SaveCopy(sourcePath, tempFile, nativeTextEdits));
			else File.Copy(sourcePath, tempFile, overwrite: true);
			if (!(await Task.Run(delegate
			{
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
				MessageBox.Show("Không áp dụng được thay đổi vào PDF.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
				LogStatus("Lưu thất bại");
				return false;
			}
			if (saveGeneration != _loadGeneration || textRevision != _textEditRevision)
                throw new InvalidOperationException("Tài liệu đã thay đổi trong lúc lưu. Hãy lưu lại nội dung mới nhất.");
			if (isOverwrite)
			{
				CloseActiveDocument();
				await _documentCloseTask;
			}
			if (saveGeneration != _loadGeneration || textRevision != _textEditRevision)
                throw new InvalidOperationException("Tài liệu đã thay đổi trong lúc lưu. Chưa thay file PDF gốc.");
			try
			{
				CommitSavedPdf(tempFile, targetPath);
			}
			catch (Exception ex2)
			{
				MessageBox.Show("Không ghi được file PDF: " + ex2.Message + ". File có thể đang được ứng dụng khác sử dụng.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
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
			}
			LoadDocument(targetPath);
			_pageRotations.Clear();
			RenderPdfPages();
			MessageBox.Show("Đã lưu PDF.", "Đã lưu", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			LogStatus("Đã lưu: " + System.IO.Path.GetFileName(targetPath));
			return true;
		}
		catch (Exception ex3)
		{
			try { File.Delete(tempFile); } catch { }
			MessageBox.Show("Không lưu được PDF: " + ex3.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
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
			MessageBox.Show("Vui lﾃｲng m盻・m盻冲 file PDF trﾆｰ盻嫩.", "Thﾃｴng bﾃ｡o", MessageBoxButton.OK, MessageBoxImage.Information);
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
				LogStatus("ﾄ静｣ c蘯ｭp nh蘯ｭt c蘯･u trﾃｺc trang tﾃi li盻㎡.");
			}
			catch (Exception ex)
			{
				MessageBox.Show("Khﾃｴng th盻・lﾆｰu thay ﾄ黛ｻ品 vﾃo file g盻祖: " + ex.Message, "L盻擁", MessageBoxButton.OK, MessageBoxImage.Error);
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
		string value = (optDialog.InsertBefore ? "trﾆｰ盻嫩" : "sau");
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
		RequestViewportRefresh();
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
	public void AddTextWatermark(string text, double fontSize, double angle, double opacity, string colorHex)
	{
		if (string.IsNullOrEmpty(CurrentPdfPath)) return;

		try
		{
			Color color = (Color)ColorConverter.ConvertFromString(colorHex);
			SolidColorBrush brush = new SolidColorBrush(color) { Opacity = opacity };

			Canvas? targetCanvas = _activeCanvas ?? _lastSnapshotCanvas;
			if (targetCanvas != null)
			{
				TextBlock watermarkBlock = new TextBlock
				{
					Text = text,
					FontSize = fontSize,
					FontWeight = FontWeights.Bold,
					Foreground = brush,
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center,
					IsHitTestVisible = false,
					RenderTransformOrigin = new Point(0.5, 0.5),
					RenderTransform = new RotateTransform(angle)
				};

				Canvas.SetLeft(watermarkBlock, Math.Max(10, targetCanvas.ActualWidth > 0 ? targetCanvas.ActualWidth / 4.0 : 100));
				Canvas.SetTop(watermarkBlock, Math.Max(10, targetCanvas.ActualHeight > 0 ? targetCanvas.ActualHeight / 2.0 : 200));
				targetCanvas.Children.Add(watermarkBlock);
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Failed to add text watermark: {ex}");
		}
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
		catch { /* B盻・qua l盻擁 ghi log ﾄ黛ｻ・khﾃｴng 蘯｣nh hﾆｰ盻殤g app */ }
	}
}
