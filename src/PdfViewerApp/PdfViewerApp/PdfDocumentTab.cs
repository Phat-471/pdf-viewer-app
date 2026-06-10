using System;
using System.Collections.Generic;
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

	private Point _drawStartPoint;

	private List<List<Point>>? _tempSignatureStrokes;
	private double _tempSignatureWidth;
	private double _tempSignatureHeight;
	private Color _tempSignatureColor = Colors.Blue;
	private string? _tempStampText;
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

	private readonly Dictionary<string, BitmapSource> _bitmapCache = new Dictionary<string, BitmapSource>(StringComparer.Ordinal);

	private readonly LinkedList<string> _bitmapCacheOrder = new LinkedList<string>();

	private readonly Dictionary<string, LinkedListNode<string>> _bitmapCacheNodes = new Dictionary<string, LinkedListNode<string>>(StringComparer.Ordinal);

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

	private DispatcherTimer? _smoothZoomTimer;

	private readonly DispatcherTimer _viewportTimer;

	private double _baseZoomForLayout = 1.0;

	private bool _isFirstLoad = true;

	private long _bitmapCacheBytes;

	private readonly Dictionary<int, int> _pageRotations = new Dictionary<int, int>();

	private readonly Dictionary<int, List<OcrTextRegion>> _ocrTextRegions = new Dictionary<int, List<OcrTextRegion>>();

	private readonly HashSet<int> _ocrPagesLoading = new HashSet<int>();

	private readonly List<PendingTextEdit> _pendingTextEdits = new List<PendingTextEdit>();

	private const int RecentPagesLimit = 8;

	private const double MinZoom = 0.1;

	private const double MaxZoom = 4.0;

	private const double ZoomStep = 1.08;

	private const double WheelZoomStep = 1.055;

	private const int MaxLoadedPageDistance = 1;

	private const long MaxBitmapCacheBytes = 402653184L;

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

	public void EnterCalibrateMode()
	{
		ActiveTool = "MeasureCalibrate";
	}

	public PdfAnnotation? SelectedAnnotation { get; set; }

	public string ActiveFontFamily { get; set; } = "Segoe UI";

	public double ActiveFontSize { get; set; } = 14.0;

	public bool ActiveIsBold { get; set; }

	public bool ActiveIsItalic { get; set; }

	public bool ActiveIsUnderline { get; set; }

	public Color ActiveStrokeColor { get; set; } = Colors.Red;

	public Color ActiveBgColor { get; set; } = Colors.Transparent;

	public double ActiveOpacity { get; set; } = 1.0;

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
		if (ActiveTool == "EditText" || ActiveTool == "SelectText")
		{
			if (e.ClickCount == 2)
			{
				int charIndexAtMousePos = GetCharIndexAtMousePos(canvas, e.GetPosition(canvas), num);
				if (charIndexAtMousePos != -1)
				{
					ShowDirectTextEditOverlay(canvas, charIndexAtMousePos, num);
					e.Handled = true;
					return;
				}
				if (ActiveTool == "EditText")
				{
					LogStatus("Đang nhận diện văn bản bằng OCR...");
					TryShowOcrTextEditOverlayAsync(canvas, e.GetPosition(canvas), num);
					e.Handled = true;
					return;
				}
			}
			if (ActiveTool == "SelectText")
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
					LogStatus("Vùng này không có văn bản có thể chọn (PDF dạng ảnh/scan). Hãy thử công cụ Sửa Trực Tiếp để dùng OCR.");
				}
				return;
			}
		}
		if (ActiveTool == "Select")
		{
			FrameworkElement frameworkElement = e.Source as FrameworkElement;
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
			Annotations.Add(pdfStickyNoteAnnotation);
			SelectedAnnotation = pdfStickyNoteAnnotation;
			ActiveTool = "Select";
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

				Annotations.Add(sigAnn);
				SelectedAnnotation = sigAnn;
				ActiveTool = "Select";
				_tempSignatureStrokes = null;
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

			Annotations.Add(stampAnn);
			SelectedAnnotation = stampAnn;
			ActiveTool = "Select";
			_tempStampText = null;
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
		else if (ActiveTool == "MeasureArea")
		{
			Point position = e.GetPosition(canvas);
			if (_pendingAreaAnnotation == null || _activeCanvas != canvas)
			{
				_activeCanvas = canvas;
				_pendingAreaAnnotation = new PdfMeasurementAnnotation
				{
					PageIndex = num - 1,
					MeasurementType = "Area",
					Scale = CurrentMeasurementScale,
					StrokeColor = ActiveStrokeColor,
					Thickness = 2.0
				};
				_pendingAreaAnnotation.Points.Add(new Point(position.X / canvas.Width, position.Y / canvas.Height));
				_pendingAreaAnnotation.Points.Add(new Point(position.X / canvas.Width, position.Y / canvas.Height));
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
		else if (_isSelectingText && ActiveTool == "SelectText")
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
			else if (ActiveTool == "MeasureArea" && _pendingAreaAnnotation != null)
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
			if (!_isDrawing)
			{
				return;
			}
			if (ActiveTool == "MeasureArea")
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
				ActiveTool = "Select";
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
				ActiveTool = "Select";
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
				Annotations.Add(pdfShapeAnnotation);
				SelectedAnnotation = pdfShapeAnnotation;
				ActiveTool = "Select";
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
				Annotations.Add(pdfShapeAnnotation2);
				SelectedAnnotation = pdfShapeAnnotation2;
				ActiveTool = "Select";
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
				Annotations.Add(pdfShapeAnnotation3);
				SelectedAnnotation = pdfShapeAnnotation3;
				ActiveTool = "Select";
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

				Annotations.Add(measureAnn);
				SelectedAnnotation = measureAnn;
				ActiveTool = "Select";
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
					Annotations.Add(pdfInkAnnotation);
					SelectedAnnotation = pdfInkAnnotation;
				}
				ActiveTool = "Select";
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
			Owner = Window.GetWindow(this)
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
			PdfPerfLogger.Log("Snapshot copy image start");
			BitmapSource bitmapSource = PdfSnapshotImageRenderer.RenderSnapshotToBitmap(snapshot);
			Clipboard.SetImage(bitmapSource);
			PdfPerfLogger.Log($"Snapshot copy image done: {bitmapSource.PixelWidth}x{bitmapSource.PixelHeight}");
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
			PdfPerfLogger.Log("Snapshot save PNG start: " + saveFileDialog.FileName);
			byte[] array = PdfSnapshotImageRenderer.RenderSnapshotToPngBytes(snapshot);
			File.WriteAllBytes(saveFileDialog.FileName, array);
			PdfPerfLogger.Log($"Snapshot save PNG done: {array.Length:N0} bytes");
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
				Annotations.Add(pdfTextBoxAnnotation);
				SelectedAnnotation = pdfTextBoxAnnotation;
				ActiveTool = "Select";
				LogStatus("Đã tạo hộp văn bản. Công cụ đã quay về Select để kéo hoặc co giãn khung.");
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
				Annotations.Add(pdfCalloutAnnotation);
				SelectedAnnotation = pdfCalloutAnnotation;
				ActiveTool = "Select";
				LogStatus("Đã tạo mũi tên chỉ dẫn. Công cụ đã quay về Select; muốn tạo mũi tên nữa hãy chọn lại.");
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

	private static string BuildPageCacheKey(int pageNumber, int width, int height, bool isThumbnail)
	{
		if (!isThumbnail)
		{
			return $"page:{pageNumber}:{width}x{height}";
		}
		return $"thumb:{pageNumber}:{width}x{height}";
	}

	private bool TryGetCachedBitmap(string key, out BitmapSource? bitmap)
	{
		if (_bitmapCache.TryGetValue(key, out BitmapSource value))
		{
			bitmap = value;
			if (_bitmapCacheNodes.TryGetValue(key, out LinkedListNode<string> value2))
			{
				_bitmapCacheOrder.Remove(value2);
				_bitmapCacheOrder.AddFirst(value2);
			}
			return true;
		}
		bitmap = null;
		return false;
	}

	private void StoreBitmap(string key, BitmapSource bitmap)
	{
		if (_bitmapCache.ContainsKey(key))
		{
			if (_bitmapCacheNodes.TryGetValue(key, out LinkedListNode<string> value))
			{
				_bitmapCacheOrder.Remove(value);
				_bitmapCacheOrder.AddFirst(value);
			}
			_bitmapCache[key] = bitmap;
		}
		else
		{
			_bitmapCache[key] = bitmap;
			LinkedListNode<string> value2 = _bitmapCacheOrder.AddFirst(key);
			_bitmapCacheNodes[key] = value2;
			_bitmapCacheBytes += EstimateBitmapBytes(bitmap);
			TrimBitmapCache();
		}
	}

	private void TrimBitmapCache()
	{
		while (_bitmapCacheBytes > 402653184 && _bitmapCacheOrder.Last != null)
		{
			string value = _bitmapCacheOrder.Last.Value;
			_bitmapCacheOrder.RemoveLast();
			if (_bitmapCache.TryGetValue(value, out BitmapSource value2))
			{
				_bitmapCacheBytes = Math.Max(0L, _bitmapCacheBytes - EstimateBitmapBytes(value2));
				_bitmapCache.Remove(value);
			}
			_bitmapCacheNodes.Remove(value);
		}
	}

	private void ClearBitmapCache()
	{
		_bitmapCache.Clear();
		_bitmapCacheOrder.Clear();
		_bitmapCacheNodes.Clear();
		_bitmapCacheBytes = 0L;
	}

	private static long EstimateBitmapBytes(BitmapSource bitmap)
	{
		return (long)Math.Max(1, bitmap.PixelWidth) * (long)Math.Max(1, bitmap.PixelHeight) * 4;
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
	}

	private void ContextPrint_Click(object sender, RoutedEventArgs e)
	{
		PrintPdf();
	}

	public void ContextRulers_Click(object sender, RoutedEventArgs e)
	{
		_isRulersEnabled = !_isRulersEnabled;
		LogStatus(_isRulersEnabled ? "Hiện thước đo" : "Ẩn thước đo");
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
			PrintDialog printDialog = new PrintDialog();
			if (optionsDialog.SelectedPrintQueue != null)
			{
				printDialog.PrintQueue = optionsDialog.SelectedPrintQueue;
			}
			printDialog.PrintTicket = optionsDialog.SelectedPrintTicket ?? printDialog.PrintQueue.UserPrintTicket ?? printDialog.PrintQueue.DefaultPrintTicket ?? new PrintTicket();
			printDialog.PrintTicket.CopyCount = optionsDialog.Copies;
			PageMediaSize pageMediaSize = optionsDialog.CreatePageMediaSize();
			if (pageMediaSize != null)
			{
				printDialog.PrintTicket.PageMediaSize = pageMediaSize;
			}
			PageOrientation? pageOrientation = optionsDialog.CreatePageOrientation();
			if (pageOrientation.HasValue)
			{
				printDialog.PrintTicket.PageOrientation = pageOrientation;
			}
			ApplyRequestedPageResolution(printDialog.PrintTicket, optionsDialog.PrintDpi);
			PrintCapabilities printCapabilities = printDialog.PrintQueue.GetPrintCapabilities(printDialog.PrintTicket);
			PrinterPrintProfile printerProfile = PrinterPrintProfile.Resolve(printDialog.PrintQueue);
			PdfPerfLogger.Log("Profile: " + printerProfile.Name);
			string printOffsetMode = optionsDialog.PrintOffsetMode;
			bool flag = printOffsetMode == "WpfOffset" || (!(printOffsetMode == "Physical") && printerProfile.DriverAlreadyOffsetsPrintableArea);
			bool driverAlreadyOffsetsPrintableArea = flag;
			PdfPerfLogger.Log("\n=================== BẮT ĐẦU CHẨN ĐOÁN LỆNH IN ===================");
			PdfPerfLogger.Log("Tệp đang in: " + CurrentPdfPath);
			PdfPerfLogger.Log("Máy in mục tiêu: " + printDialog.PrintQueue.FullName);
			PdfPerfLogger.Log($"Số bản in (Copies): {optionsDialog.Copies}");
			PdfPerfLogger.Log($"Trang bắt đầu: {optionsDialog.StartPageIndex + 1}, Trang kết thúc: {optionsDialog.EndPageIndex + 1}");
			PdfPerfLogger.Log($"Tự động căn giữa (AutoCenter): {optionsDialog.AutoCenter}, Khớp khổ giấy (FitToPrintableArea): {optionsDialog.FitToPrintableArea}");
			PdfPerfLogger.Log($"Hướng xoay giấy: {pageOrientation}");
			PdfPerfLogger.Log($"Khổ giấy đã chọn: {pageMediaSize?.PageMediaSizeName} (Rộng: {pageMediaSize?.Width} x Cao: {pageMediaSize?.Height})");
			PdfPerfLogger.Log($"DPI in đã chọn: {optionsDialog.PrintDpi}; PrintTicket.PageResolution={printDialog.PrintTicket.PageResolution?.X}x{printDialog.PrintTicket.PageResolution?.Y}");
			PdfPerfLogger.Log("Chế độ in đã chọn: " + optionsDialog.PrintEngineMode);
			PdfPerfLogger.Log($"Native separate page jobs: {optionsDialog.NativeSeparatePageJobs}");
			PdfPerfLogger.Log($"Reverse page order: {optionsDialog.ReversePageOrder}");
			PdfDocumentPaginator paginator = new PdfDocumentPaginator(CurrentPdfPath);
			paginator.Annotations.AddRange(Annotations);
			paginator.StartPage = optionsDialog.StartPageIndex;
			paginator.EndPage = optionsDialog.EndPageIndex;
			paginator.AutoCenter = optionsDialog.AutoCenter;
			paginator.FitToPrintableArea = optionsDialog.FitToPrintableArea;
			paginator.PrintDpi = optionsDialog.PrintDpi;
			paginator.ReversePageOrder = optionsDialog.ReversePageOrder;
			paginator.PrintProgress = printProgress;
			paginator.BottomSafetyPadding = printerProfile.BottomSafetyPadding;
			paginator.RightSafetyPadding = printerProfile.RightSafetyPadding;
			paginator.DriverAlreadyOffsetsPrintableArea = driverAlreadyOffsetsPrintableArea;
			paginator.PrintTestFrame = optionsDialog.PrintTestFrame;
			if (optionsDialog.PrintTestFrame)
			{
				paginator.StartPage = 0;
				paginator.EndPage = 0;
				PdfPerfLogger.Log("Print test frame enabled: forcing a single diagnostic page.");
			}
			double num = printCapabilities.OrientedPageMediaWidth ?? pageMediaSize?.Width ?? printDialog.PrintableAreaWidth;
			double num2 = printCapabilities.OrientedPageMediaHeight ?? pageMediaSize?.Height ?? printDialog.PrintableAreaHeight;
			if (pageOrientation == PageOrientation.Landscape)
			{
				double num3 = Math.Max(num, num2);
				double num4 = Math.Min(num, num2);
				num = num3;
				num2 = num4;
			}
			else if (pageOrientation == PageOrientation.Portrait)
			{
				double num5 = Math.Min(num, num2);
				double num6 = Math.Max(num, num2);
				num = num5;
				num2 = num6;
			}
			paginator.PageSize = new Size(Math.Max(1.0, num), Math.Max(1.0, num2));
			PdfPerfLogger.Log($"Kích thước trang đích (PageSize): {paginator.PageSize.Width}x{paginator.PageSize.Height}");
			if (printCapabilities.PageImageableArea != null)
			{
				double originWidth = printCapabilities.PageImageableArea.OriginWidth;
				double originHeight = printCapabilities.PageImageableArea.OriginHeight;
				double value = Math.Max(0.0, num - originWidth - printCapabilities.PageImageableArea.ExtentWidth);
				double value2 = Math.Max(0.0, num2 - originHeight - printCapabilities.PageImageableArea.ExtentHeight);
				paginator.ImageableArea = new Rect(originWidth, originHeight, printCapabilities.PageImageableArea.ExtentWidth, printCapabilities.PageImageableArea.ExtentHeight);
				PdfPerfLogger.Log($"Vùng in được của máy in (Raw PageImageableArea): Gốc=({originWidth}, {originHeight}) Kích thước=({printCapabilities.PageImageableArea.ExtentWidth}x{printCapabilities.PageImageableArea.ExtentHeight})");
				PdfPerfLogger.Log($"Khoảng lề biên kéo giấy tính toán: Phải={value}, Dưới={value2}");
				PdfPerfLogger.Log($"Tọa độ vùng in truyền cho Paginator (ImageableArea): Gốc=({paginator.ImageableArea.X}, {paginator.ImageableArea.Y}) Kích thước=({paginator.ImageableArea.Width}x{paginator.ImageableArea.Height})");
			}
			if (optionsDialog.PrintEngineMode == "NativePdfium" && !optionsDialog.PrintTestFrame)
			{
				if (Annotations.Count > 0)
				{
					PdfPerfLogger.Log("Native PDFium print note: app overlay annotations are not rendered by the native printer path. Use WPF Bitmap if those annotations must be printed.");
				}
				PrintTicket printTicket = printDialog.PrintTicket.Clone();
				printTicket.CopyCount = 1;
				string queueName = printDialog.PrintQueue.FullName;
				byte[] devModeBytes = null;
				try
				{
					using PrintTicketConverter printTicketConverter = new PrintTicketConverter(printDialog.PrintQueue.FullName, printDialog.PrintQueue.ClientPrintSchemaVersion);
					devModeBytes = printTicketConverter.ConvertPrintTicketToDevMode(printTicket, BaseDevModeType.UserDefault);
				}
				catch (Exception ex)
				{
					PdfPerfLogger.Log("Warning: Failed to convert PrintTicket to DevMode: " + ex.Message + ". Using default printer settings.");
				}
				int startPageIndex = optionsDialog.StartPageIndex;
				int endPageIndex = optionsDialog.EndPageIndex;
				int copies = optionsDialog.Copies;
				bool fitToPrintableArea = optionsDialog.FitToPrintableArea;
				bool autoCenter = optionsDialog.AutoCenter;
				bool separatePageJobs = optionsDialog.NativeSeparatePageJobs;
				bool reversePageOrder = optionsDialog.ReversePageOrder;
				bool forceRasterize = optionsDialog.OptimizeCadDrawings;
				Stopwatch nativeSubmitSw = Stopwatch.StartNew();
				await Task.Run(delegate
				{
					NativePdfPrinter.Print(CurrentPdfPath, queueName, devModeBytes, startPageIndex, endPageIndex, copies, fitToPrintableArea, autoCenter, driverAlreadyOffsetsPrintableArea, printerProfile.RightSafetyPadding, printerProfile.BottomSafetyPadding, separatePageJobs, reversePageOrder, forceRasterize, printProgress, progressDialog.CancellationToken);
				});
				nativeSubmitSw.Stop();
				PdfPerfLogger.Log($"Native print submit total: {nativeSubmitSw.ElapsedMilliseconds} ms");
				progressDialog.MarkCompleted("Da gui lenh in vao may in.");
				LogStatus("Print job sent");
			}
			else
			{
				PdfPerfLogger.Log("Using WPF Bitmap print pipeline.");
				Stopwatch printSubmitSw = Stopwatch.StartNew();
				printProgress.Report(new PrintProgressInfo("Dang gui lenh in WPF Bitmap...", 0, Math.Max(1, paginator.PageCount), IsIndeterminate: true));
				await base.Dispatcher.InvokeAsync(delegate
				{
				}, DispatcherPriority.Background);
				printDialog.PrintDocument(paginator, System.IO.Path.GetFileName(CurrentPdfPath));
				printSubmitSw.Stop();
				PdfPerfLogger.Log($"PrintDocument submit total: {printSubmitSw.ElapsedMilliseconds} ms");
				progressDialog.MarkCompleted("Da gui lenh in vao may in.");
				LogStatus("Print job sent");
			}
		}
		catch (OperationCanceledException)
		{
			PdfPerfLogger.Log("Print canceled by user.");
			progressDialog.MarkFailed("Da huy lenh in.");
			LogStatus("Print canceled");
		}
		catch (Exception ex3)
		{
			PdfPerfLogger.Log($"Print failed: {ex3}");
			progressDialog.MarkFailed("In that bai: " + ex3.Message);
			MessageBox.Show("Error while printing: " + ex3.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			LogStatus("Print failed");
		}
	}

	private static void ApplyRequestedPageResolution(PrintTicket printTicket, double dpi)
	{
		int num = Math.Clamp((int)Math.Round(dpi), 72, 1200);
		printTicket.PageResolution = new PageResolution(num, num);
	}

	private nint GetTextPage(int pageNumber)
	{
		if (_textPages.TryGetValue(pageNumber, out var value))
		{
			return value;
		}
		if (_documentHandle == IntPtr.Zero)
		{
			return IntPtr.Zero;
		}
		lock (PdfiumEngine.SyncRoot)
		{
			nint num = PdfiumEngine.FPDF_LoadPage(_documentHandle, pageNumber - 1);
			if (num == IntPtr.Zero)
			{
				return IntPtr.Zero;
			}
			value = PdfiumEngine.FPDFText_LoadPage(num);
			PdfiumEngine.FPDF_ClosePage(num);
			_textPages[pageNumber] = value;
			return value;
		}
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
		double xTolerance = Math.Max(2.0, 10.0 * _pageDimensions[pageNumber - 1].Width / Math.Max(1.0, canvas.Width));
		double yTolerance = Math.Max(2.0, 10.0 * _pageDimensions[pageNumber - 1].Height / Math.Max(1.0, canvas.Height));
		lock (PdfiumEngine.SyncRoot)
		{
			return PdfiumEngine.FPDFText_GetCharIndexAtPos(textPage, pdfPoint.X, pdfPoint.Y, xTolerance, yTolerance);
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
		if (_selectionStartPageIndex == -1 || _selectionEndPageIndex == -1)
		{
			return;
		}
		int num = pageNumber - 1;
		int num2 = Math.Min(_selectionStartPageIndex, _selectionEndPageIndex);
		int num3 = Math.Max(_selectionStartPageIndex, _selectionEndPageIndex);
		if (num < num2 || num > num3)
		{
			return;
		}
		nint textPage = GetTextPage(pageNumber);
		if (textPage == IntPtr.Zero)
		{
			return;
		}
		int num4;
		lock (PdfiumEngine.SyncRoot)
		{
			num4 = PdfiumEngine.FPDFText_CountChars(textPage);
		}
		if (num4 <= 0)
		{
			return;
		}
		int num5 = 0;
		int num6 = num4 - 1;
		if (num == _selectionStartPageIndex && num == _selectionEndPageIndex)
		{
			num5 = Math.Min(_selectionStartIndex, _selectionEndIndex);
			num6 = Math.Max(_selectionStartIndex, _selectionEndIndex);
		}
		else if (num == _selectionStartPageIndex)
		{
			if (_selectionStartPageIndex < _selectionEndPageIndex)
			{
				num5 = _selectionStartIndex;
			}
			else
			{
				num6 = _selectionStartIndex;
			}
		}
		else if (num == _selectionEndPageIndex)
		{
			if (_selectionStartPageIndex < _selectionEndPageIndex)
			{
				num6 = _selectionEndIndex;
			}
			else
			{
				num5 = _selectionEndIndex;
			}
		}
		if (num5 < 0)
		{
			num5 = 0;
		}
		if (num6 >= num4)
		{
			num6 = num4 - 1;
		}
		SolidColorBrush fill = new SolidColorBrush(Color.FromArgb(90, 51, 153, byte.MaxValue));
		lock (PdfiumEngine.SyncRoot)
		{
			for (int i = num5; i <= num6; i++)
			{
				if (PdfiumEngine.FPDFText_GetCharBox(textPage, i, out var left, out var right, out var bottom, out var top))
				{
					if (!TryPdfRectToCanvasRect(canvas, pageNumber, left, right, bottom, top, out Rect canvasRect))
					{
						continue;
					}
					double num7 = canvasRect.Width;
					double num8 = canvasRect.Height;
					if (num7 <= 0.0)
					{
						num7 = 6.0;
					}
					if (num8 <= 0.0)
					{
						num8 = 12.0;
					}
					Rectangle element = new Rectangle
					{
						Width = Math.Max(0.5, num7),
						Height = Math.Max(0.5, num8),
						Fill = fill,
						IsHitTestVisible = false
					};
					Canvas.SetLeft(element, canvasRect.X);
					Canvas.SetTop(element, canvasRect.Y);
					canvas.Children.Add(element);
				}
			}
		}
	}

	private string GetSelectedTextString()
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
		nint textPage = GetTextPage(pageNumber);
		if (textPage == IntPtr.Zero)
		{
			return;
		}
		int num = 0;
		lock (PdfiumEngine.SyncRoot)
		{
			num = PdfiumEngine.FPDFText_CountChars(textPage);
		}
		if (num <= 0)
		{
			return;
		}
		Func<int, char> func = delegate(int idx)
		{
			StringBuilder stringBuilder2 = new StringBuilder(2);
			lock (PdfiumEngine.SyncRoot)
			{
				if (PdfiumEngine.FPDFText_GetText(textPage, idx, 1, stringBuilder2) > 0 && stringBuilder2.Length > 0)
				{
					return stringBuilder2[0];
				}
			}
			return '\0';
		};
		int num2;
		for (num2 = charIndex; num2 > 0; num2--)
		{
			char c = func(num2 - 1);
			if (c == '\r' || c == '\n')
			{
				break;
			}
		}
		int num3;
		for (num3 = charIndex; num3 < num - 1; num3++)
		{
			char c2 = func(num3 + 1);
			if (c2 == '\r' || c2 == '\n')
			{
				break;
			}
		}
		double minLeft = double.MaxValue;
		double maxRight = double.MinValue;
		double minBottom = double.MaxValue;
		double maxTop = double.MinValue;
		bool flag = false;
		lock (PdfiumEngine.SyncRoot)
		{
			for (int num4 = num2; num4 <= num3; num4++)
			{
				if (PdfiumEngine.FPDFText_GetCharBox(textPage, num4, out var left, out var right, out var bottom, out var top) && right > left && top > bottom)
				{
					minLeft = Math.Min(minLeft, left);
					maxRight = Math.Max(maxRight, right);
					minBottom = Math.Min(minBottom, bottom);
					maxTop = Math.Max(maxTop, top);
					flag = true;
				}
			}
		}
		if (!flag)
		{
			return;
		}
		if (!TryGetPageSize(pageNumber, out Size pageSize) || !TryPdfRectToCanvasRect(canvas, pageNumber, minLeft, maxRight, minBottom, maxTop, out Rect editRect))
		{
			return;
		}
		int num9 = num3 - num2 + 1;
		string existingText = "";
		if (num9 > 0)
		{
			StringBuilder stringBuilder = new StringBuilder(num9 + 2);
			lock (PdfiumEngine.SyncRoot)
			{
				if (PdfiumEngine.FPDFText_GetText(textPage, num2, num9, stringBuilder) > 0)
				{
					existingText = stringBuilder.ToString().Trim('\r', '\n');
				}
			}
		}
		double fontSizePoints = maxTop - minBottom;
		if (fontSizePoints <= 0.0)
		{
			fontSizePoints = 12.0;
		}
		System.Windows.Controls.TextBox tbInput = new System.Windows.Controls.TextBox
		{
			Width = Math.Max(50.0, editRect.Width + 12.0),
			Height = Math.Max(20.0, editRect.Height + 6.0),
			Text = existingText,
			FontFamily = new FontFamily(ActiveFontFamily),
			FontSize = Math.Max(8.0, fontSizePoints * canvas.Height / pageSize.Height),
			FontWeight = FontWeights.Normal,
			FontStyle = FontStyles.Normal,
			Foreground = Brushes.Black,
			TextWrapping = TextWrapping.Wrap,
			AcceptsReturn = false,
			BorderBrush = new SolidColorBrush(Color.FromRgb(15, 118, 110)),
			BorderThickness = new Thickness(2.0),
			Background = Brushes.White,
			Padding = new Thickness(2.0)
		};
		Canvas.SetLeft(tbInput, editRect.X - 2.0);
		Canvas.SetTop(tbInput, editRect.Y - 3.0);
		canvas.Children.Add(tbInput);
		tbInput.Focus();
		tbInput.SelectAll();
		bool editCommitted = false;
		Action commitEdit = delegate
		{
			if (editCommitted)
			{
				return;
			}
			editCommitted = true;
			string text = tbInput.Text.Trim();
			canvas.Children.Remove(tbInput);
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
				Annotations.Add(whiteout);
				Annotations.Add(replacement);
				_pendingTextEdits.Add(new PendingTextEdit(pageNumber, existingText, text, minLeft, minBottom, maxRight - minLeft, maxTop - minBottom, whiteout, replacement));
				RedrawPageAnnotations(canvas, pageNumber);
				LogStatus("Staged text replacement. Save the PDF to apply the actual content change.");
			}
		};
		tbInput.LostFocus += delegate
		{
			commitEdit();
		};
		tbInput.KeyDown += delegate(object s, KeyEventArgs ev)
		{
			if (ev.Key == Key.Return)
			{
				commitEdit();
				ev.Handled = true;
			}
			else if (ev.Key == Key.Escape)
			{
				canvas.Children.Remove(tbInput);
				ev.Handled = true;
			}
		};
	}

	private void ShowDirectTextEditOverlayFromBounds(Canvas canvas, int pageNumber, double minLeft, double minBottom, double maxRight, double maxTop, string existingText)
	{
		if (!TryGetPageSize(pageNumber, out Size pageSize) || !TryPdfRectToCanvasRect(canvas, pageNumber, minLeft, maxRight, minBottom, maxTop, out Rect editRect))
		{
			return;
		}
		double fontSizePoints = maxTop - minBottom;
		if (fontSizePoints <= 0.0)
		{
			fontSizePoints = 12.0;
		}
		System.Windows.Controls.TextBox tbInput = new System.Windows.Controls.TextBox
		{
			Width = Math.Max(50.0, editRect.Width + 12.0),
			Height = Math.Max(20.0, editRect.Height + 6.0),
			Text = existingText,
			FontFamily = new FontFamily(ActiveFontFamily),
			FontSize = Math.Max(8.0, fontSizePoints * canvas.Height / pageSize.Height),
			FontWeight = FontWeights.Normal,
			FontStyle = FontStyles.Normal,
			Foreground = Brushes.Black,
			TextWrapping = TextWrapping.Wrap,
			AcceptsReturn = false,
			BorderBrush = new SolidColorBrush(Color.FromRgb(15, 118, 110)),
			BorderThickness = new Thickness(2.0),
			Background = Brushes.White,
			Padding = new Thickness(2.0)
		};
		Canvas.SetLeft(tbInput, editRect.X - 2.0);
		Canvas.SetTop(tbInput, editRect.Y - 3.0);
		canvas.Children.Add(tbInput);
		tbInput.Focus();
		tbInput.SelectAll();
		bool editCommitted = false;
		Action commitEdit = delegate
		{
			if (editCommitted)
			{
				return;
			}
			editCommitted = true;
			string text = tbInput.Text.Trim();
			canvas.Children.Remove(tbInput);
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
				Annotations.Add(whiteout);
				Annotations.Add(replacement);
				_pendingTextEdits.Add(new PendingTextEdit(pageNumber, existingText, text, minLeft, minBottom, maxRight - minLeft, maxTop - minBottom, whiteout, replacement));
				RedrawPageAnnotations(canvas, pageNumber);
				LogStatus("Staged text replacement. Save the PDF to apply the actual content change.");
			}
		};
		tbInput.LostFocus += delegate
		{
			commitEdit();
		};
		tbInput.KeyDown += delegate(object s, KeyEventArgs ev)
		{
			if (ev.Key == Key.Return)
			{
				commitEdit();
				ev.Handled = true;
			}
			else if (ev.Key == Key.Escape)
			{
				canvas.Children.Remove(tbInput);
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

	private static Windows.Foundation.Rect UnionRect(Windows.Foundation.Rect a, Windows.Foundation.Rect b)
	{
		double left = Math.Min(a.X, b.X);
		double top = Math.Min(a.Y, b.Y);
		double right = Math.Max(a.X + a.Width, b.X + b.Width);
		double bottom = Math.Max(a.Y + a.Height, b.Y + b.Height);
		return new Windows.Foundation.Rect(left, top, right - left, bottom - top);
	}

	public async Task PreloadOcrForSelectedPageAsync()
	{
		if (PageCount <= 0)
		{
			return;
		}
		int pageNumber = Math.Clamp(SelectedPageNumber, 1, PageCount);
		List<OcrTextRegion>? regions = await EnsureOcrRegionsAsync(pageNumber);
		if (regions != null && regions.Count > 0)
		{
			LogStatus($"OCR ready for page {pageNumber} ({regions.Count} regions).");
		}
		else
		{
			LogStatus($"OCR found no text on page {pageNumber}.");
		}
	}

	private async void TryShowOcrTextEditOverlayAsync(Canvas canvas, Point clickPoint, int pageNumber)
	{
		if (!TryCanvasToPdfPoint(canvas, clickPoint, pageNumber, out Point pdfPoint))
		{
			return;
		}
		List<OcrTextRegion>? regions = await EnsureOcrRegionsAsync(pageNumber);
		if (regions == null || regions.Count == 0)
		{
			LogStatus("OCR could not detect editable text on this page.");
			return;
		}
		OcrTextRegion? match = null;
		foreach (OcrTextRegion region in regions)
		{
			if (pdfPoint.X >= region.Left && pdfPoint.X <= region.Left + region.Width && pdfPoint.Y >= region.Bottom && pdfPoint.Y <= region.Bottom + region.Height)
			{
				match = region;
				break;
			}
		}
		if (match == null)
		{
			OcrTextRegion best = regions[0];
			double bestDistance = double.MaxValue;
			foreach (OcrTextRegion region in regions)
			{
				double centerX = region.Left + region.Width / 2.0;
				double centerY = region.Bottom + region.Height / 2.0;
				double distance = Math.Abs(centerX - pdfPoint.X) + Math.Abs(centerY - pdfPoint.Y);
				if (distance < bestDistance)
				{
					bestDistance = distance;
					best = region;
				}
			}
			match = best;
		}
		if (match != null && !string.IsNullOrWhiteSpace(match.Value.Text))
		{
			ShowDirectTextEditOverlayFromBounds(canvas, pageNumber, match.Value.Left, match.Value.Bottom, match.Value.Left + match.Value.Width, match.Value.Bottom + match.Value.Height, match.Value.Text);
			LogStatus($"OCR edit ready on page {pageNumber}.");
		}
	}

	private async Task<List<OcrTextRegion>?> EnsureOcrRegionsAsync(int pageNumber)
	{
		if (_ocrTextRegions.TryGetValue(pageNumber, out List<OcrTextRegion>? cached))
		{
			return cached;
		}
		while (_ocrPagesLoading.Contains(pageNumber))
		{
			await Task.Delay(100);
			if (_ocrTextRegions.TryGetValue(pageNumber, out cached))
			{
				return cached;
			}
		}
		_ocrPagesLoading.Add(pageNumber);
		try
		{
			List<OcrTextRegion>? recognized = await RecognizeOcrRegionsAsync(pageNumber);
			if (recognized == null)
			{
				return null;
			}
			_ocrTextRegions[pageNumber] = recognized;
			return recognized;
		}
		finally
		{
			_ocrPagesLoading.Remove(pageNumber);
		}
	}

	private async Task<List<OcrTextRegion>?> RecognizeOcrRegionsAsync(int pageNumber)
	{
		if (_documentHandle == IntPtr.Zero || !TryGetPageSize(pageNumber, out Size pageSize))
		{
			return null;
		}
		int targetWidth = 1800;
		int targetHeight = Math.Max(1, (int)Math.Round(targetWidth * pageSize.Height / Math.Max(1.0, pageSize.Width)));
		BitmapSource? pageBitmap = await Task.Run(() => PdfiumEngine.RenderPageToBitmap(_documentHandle, pageNumber - 1, targetWidth, targetHeight));
		if (pageBitmap == null)
		{
			return null;
		}
		string imagePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
		try
		{
			PngBitmapEncoder encoder = new PngBitmapEncoder();
			encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(pageBitmap));
			using (FileStream fileStream = File.Create(imagePath))
			{
				encoder.Save(fileStream);
			}
			OcrEngine? engine = OcrEngine.TryCreateFromUserProfileLanguages() ?? OcrEngine.TryCreateFromLanguage(new Language("en"));
			if (engine == null)
			{
				return null;
			}
			using (FileStream inputStream = File.OpenRead(imagePath))
			{
				IRandomAccessStream randomAccessStream = inputStream.AsRandomAccessStream();
				Windows.Graphics.Imaging.BitmapDecoder decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(randomAccessStream).AsTask();
				SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync().AsTask();
				if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Ignore)
				{
					softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
				}
				OcrResult result = await engine.RecognizeAsync(softwareBitmap).AsTask();
				List<OcrTextRegion> regions = new List<OcrTextRegion>();
				double scaleX = pageSize.Width / Math.Max(1.0, pageBitmap.PixelWidth);
				double scaleY = pageSize.Height / Math.Max(1.0, pageBitmap.PixelHeight);
				foreach (var line in result.Lines)
				{
					string text = line.Text?.Trim() ?? "";
					if (string.IsNullOrWhiteSpace(text))
					{
						continue;
					}
					Windows.Foundation.Rect? rect = null;
					foreach (var word in line.Words)
					{
						Windows.Foundation.Rect wordRect = word.BoundingRect;
						rect = rect == null ? wordRect : UnionRect(rect.Value, wordRect);
					}
					if (rect == null)
					{
						continue;
					}
					double left = rect.Value.X * scaleX;
					double right = (rect.Value.X + rect.Value.Width) * scaleX;
					double top = pageSize.Height - rect.Value.Y * scaleY;
					double bottom = pageSize.Height - (rect.Value.Y + rect.Value.Height) * scaleY;
					regions.Add(new OcrTextRegion(text, left, bottom, right - left, top - bottom));
				}
				return regions;
			}
		}
		catch (Exception ex)
		{
			PdfPerfLogger.Log("OCR failed: " + ex.Message);
			return null;
		}
		finally
		{
			try
			{
				File.Delete(imagePath);
			}
			catch
			{
			}
		}
	}

	public void SetSidebarVisibility(bool isVisible)
	{
		_isSidebarVisible = isVisible;
		SidebarBorder.Visibility = ((!isVisible) ? Visibility.Collapsed : Visibility.Visible);
		SidebarColumn.Width = (isVisible ? new GridLength(260.0) : new GridLength(0.0));
		SplitterColumn.Width = (isVisible ? new GridLength(4.0) : new GridLength(0.0));
		if (isVisible && PageCount > 0)
		{
			_thumbnailLoadDeferred = false;
			RequestViewportRefresh();
		}
	}

	public void ToggleSidebar()
	{
		SetSidebarVisibility(!_isSidebarVisible);
	}

	public void ChangeZoom(double factor, Point? viewportAnchor = null)
	{
		if (PageCount <= 0)
		{
			return;
		}
		if (_targetZoom <= 0.0)
		{
			_targetZoom = CurrentZoom;
		}
		double nextTarget = Math.Clamp(_targetZoom * factor, 0.1, 4.0);
		if (Math.Abs(nextTarget - CurrentZoom) < 0.0001)
		{
			return;
		}
		_targetZoom = nextTarget;
		_smoothZoomAnchor = viewportAnchor ?? GetViewportCenter();
		_pendingZoomBaseZoom = _baseZoomForLayout;
		_pendingZoomViewportPoint = _smoothZoomAnchor;
		_pendingZoomContentPoint = null;
		_resetScrollAfterRender = false;
		ClearRenderQueue();
		_renderGeneration++;
		_loadingPages.Clear();
		_zoomTimer.Stop();
		_smoothZoomTimer?.Start();
	}

	public void SetZoomPercent(double zoomPercent)
	{
		if (!(CurrentZoom <= 0.0))
		{
			double num = Math.Clamp(zoomPercent / 100.0, 0.1, 4.0);
			ChangeZoom(num / CurrentZoom);
		}
	}

	public void GoToPage(int pageNumber)
	{
		if (PageCount <= 0)
		{
			return;
		}
		int num = Math.Clamp(pageNumber, 1, PageCount);
		SetSelectedPage(num);
		if (PagesHost.Children.Count == 0 || !(PagesHost.Children[0] is StackPanel stackPanel))
		{
			return;
		}
		foreach (UIElement child in stackPanel.Children)
		{
			if (child is Border { Tag: var tag } border && tag is int num2 && num2 == num)
			{
				border.BringIntoView();
				break;
			}
		}
	}

	public void FitWidth()
	{
		if (PageCount > 0 && _pageDimensions.Count != 0)
		{
			double num = DocumentScrollViewer.ViewportWidth;
			if (num <= 100.0)
			{
				num = DocumentScrollViewer.ActualWidth;
			}
			if (num <= 100.0)
			{
				num = 800.0;
			}
			double width = _pageDimensions[0].Width;
			if (width > 0.0)
			{
				double value = (num - 50.0) / (width * 1.33);
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
				LogStatus("Fit width applied");
			}
		}
	}

	private Point GetViewportCenter()
	{
		double viewportWidth = DocumentScrollViewer.ViewportWidth;
		double viewportHeight = DocumentScrollViewer.ViewportHeight;
		if (viewportWidth <= 0.0 || viewportHeight <= 0.0)
		{
			return new Point(0.0, 0.0);
		}
		return new Point(viewportWidth / 2.0, viewportHeight / 2.0);
	}

	private void ApplyZoomPreviewTransform(double ratio)
	{
		ratio = Math.Max(0.01, ratio);
		if (!_zoomPreviewActive)
		{
			double num = ((PagesHost.ActualWidth > 1.0) ? PagesHost.ActualWidth : PagesHost.RenderSize.Width);
			double num2 = ((PagesHost.ActualHeight > 1.0) ? PagesHost.ActualHeight : PagesHost.RenderSize.Height);
			if (num <= 1.0)
			{
				num = PagesHost.DesiredSize.Width;
			}
			if (num2 <= 1.0)
			{
				num2 = PagesHost.DesiredSize.Height;
			}
			_zoomPreviewBaseHostSize = new Size(Math.Max(1.0, num), Math.Max(1.0, num2));
			_zoomPreviewActive = true;
		}
		RenderOptions.SetBitmapScalingMode(PagesHost, BitmapScalingMode.LowQuality);
		PagesHost.LayoutTransform = null;
		PagesHost.RenderTransform = null;
		UIElement zoomPreviewContent = GetZoomPreviewContent();
		if (zoomPreviewContent != null)
		{
			zoomPreviewContent.RenderTransformOrigin = new Point(0.0, 0.0);
			zoomPreviewContent.RenderTransform = new ScaleTransform(ratio, ratio);
		}
		PagesHost.Width = _zoomPreviewBaseHostSize.Width * ratio;
		PagesHost.Height = _zoomPreviewBaseHostSize.Height * ratio;
	}

	private void ResetZoomPreviewTransform()
	{
		_zoomPreviewActive = false;
		_zoomPreviewBaseHostSize = default(Size);
		PagesHost.LayoutTransform = null;
		PagesHost.RenderTransform = null;
		UIElement zoomPreviewContent = GetZoomPreviewContent();
		if (zoomPreviewContent != null)
		{
			zoomPreviewContent.RenderTransform = null;
		}
		PagesHost.Width = double.NaN;
		PagesHost.Height = double.NaN;
		RenderOptions.SetBitmapScalingMode(PagesHost, BitmapScalingMode.HighQuality);
	}

	private void ApplyPendingZoomScroll()
	{
		if (_pendingZoomViewportPoint.HasValue)
		{
			if (_pendingZoomHostPoint.HasValue)
			{
				double hostScale = CurrentZoom / Math.Max(0.0001, _pendingZoomBaseZoom);
				ScrollToKeepHostPointAtViewport(_pendingZoomHostPoint.Value, _pendingZoomViewportPoint.Value, hostScale, updateLayout: true);
				_pendingZoomHostPoint = null;
				_pendingZoomViewportPoint = null;
				_pendingZoomContentPoint = null;
			}
			else if (_pendingZoomContentPoint.HasValue)
			{
				double num = Math.Max(0.0001, _pendingZoomBaseZoom);
				double num2 = CurrentZoom / num;
				Point value = _pendingZoomContentPoint.Value;
				Point value2 = _pendingZoomViewportPoint.Value;
				double left = PagesHost.Margin.Left;
				double top = PagesHost.Margin.Top;
				double num3 = Math.Max(0.0, value.X - left);
				double num4 = Math.Max(0.0, value.Y - top);
				double val = left + num3 * num2 - value2.X;
				double val2 = top + num4 * num2 - value2.Y;
				double val3 = Math.Max(0.0, DocumentScrollViewer.ExtentWidth - DocumentScrollViewer.ViewportWidth);
				double val4 = Math.Max(0.0, DocumentScrollViewer.ExtentHeight - DocumentScrollViewer.ViewportHeight);
				DocumentScrollViewer.ScrollToHorizontalOffset(Math.Max(0.0, Math.Min(val, val3)));
				DocumentScrollViewer.ScrollToVerticalOffset(Math.Max(0.0, Math.Min(val2, val4)));
				_pendingZoomContentPoint = null;
				_pendingZoomViewportPoint = null;
			}
		}
	}

	private UIElement? GetZoomPreviewContent()
	{
		if (PagesHost.Children.Count <= 0)
		{
			return null;
		}
		return PagesHost.Children[0];
	}

	private Point GetZoomHostAnchor(Point viewportPoint, double currentScale)
	{
		try
		{
			Point point = DocumentScrollViewer.TranslatePoint(viewportPoint, PagesHost);
			currentScale = Math.Max(0.0001, currentScale);
			return new Point(point.X / currentScale, point.Y / currentScale);
		}
		catch
		{
			currentScale = Math.Max(0.0001, currentScale);
			return new Point(Math.Max(0.0, DocumentScrollViewer.HorizontalOffset + viewportPoint.X - PagesHost.Margin.Left) / currentScale, Math.Max(0.0, DocumentScrollViewer.VerticalOffset + viewportPoint.Y - PagesHost.Margin.Top) / currentScale);
		}
	}

	private void ScrollToKeepHostPointAtViewport(Point hostPoint, Point viewportPoint, double hostScale, bool updateLayout)
	{
		if (double.IsNaN(hostPoint.X) || double.IsNaN(hostPoint.Y) || double.IsInfinity(hostPoint.X) || double.IsInfinity(hostPoint.Y))
		{
			return;
		}
		hostScale = Math.Max(0.0001, hostScale);
		if (updateLayout)
		{
			try
			{
				DocumentScrollViewer.UpdateLayout();
			}
			catch
			{
			}
		}
		Point point;
		try
		{
			point = PagesHost.TransformToAncestor(DocumentScrollViewer).Transform(new Point(0.0, 0.0));
		}
		catch
		{
			point = new Point(PagesHost.Margin.Left, PagesHost.Margin.Top);
		}
		double num = DocumentScrollViewer.HorizontalOffset + point.X;
		double num2 = DocumentScrollViewer.VerticalOffset + point.Y;
		double val = num + hostPoint.X * hostScale - viewportPoint.X;
		double val2 = num2 + hostPoint.Y * hostScale - viewportPoint.Y;
		double val3 = Math.Max(0.0, DocumentScrollViewer.ExtentWidth - DocumentScrollViewer.ViewportWidth);
		double val4 = Math.Max(0.0, DocumentScrollViewer.ExtentHeight - DocumentScrollViewer.ViewportHeight);
		DocumentScrollViewer.ScrollToHorizontalOffset(Math.Max(0.0, Math.Min(val, val3)));
		DocumentScrollViewer.ScrollToVerticalOffset(Math.Max(0.0, Math.Min(val2, val4)));
	}

	private void DocumentScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.None)
		{
			e.Handled = true;
			if (PageCount > 0)
			{
				double factor = Math.Pow(1.055, (double)e.Delta / 120.0);
				ChangeZoom(factor, e.GetPosition(DocumentScrollViewer));
			}
		}
	}

	private void DocumentScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
	{
		bool num = e.ChangedButton == MouseButton.Middle;
		bool flag = e.ChangedButton == MouseButton.Left && Keyboard.IsKeyDown(Key.Space);
		if (num || flag)
		{
			_isPanning = true;
			_panStartPoint = e.GetPosition(DocumentScrollViewer);
			_panStartHorizontalOffset = DocumentScrollViewer.HorizontalOffset;
			_panStartVerticalOffset = DocumentScrollViewer.VerticalOffset;
			DocumentScrollViewer.Cursor = Cursors.SizeAll;
			DocumentScrollViewer.CaptureMouse();
			e.Handled = true;
		}
	}

	private void DocumentScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
	{
		if (_isPanning)
		{
			if (e.MiddleButton != MouseButtonState.Pressed && e.LeftButton != MouseButtonState.Pressed)
			{
				EndPan();
				return;
			}
			Vector vector = e.GetPosition(DocumentScrollViewer) - _panStartPoint;
			DocumentScrollViewer.ScrollToHorizontalOffset(_panStartHorizontalOffset - vector.X);
			DocumentScrollViewer.ScrollToVerticalOffset(_panStartVerticalOffset - vector.Y);
			e.Handled = true;
		}
	}

	private void DocumentScrollViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
	{
		if (_isPanning && (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Left))
		{
			EndPan();
			e.Handled = true;
		}
	}

	private void EndPan()
	{
		_isPanning = false;
		DocumentScrollViewer.Cursor = Cursors.Arrow;
		if (DocumentScrollViewer.IsMouseCaptured)
		{
			DocumentScrollViewer.ReleaseMouseCapture();
		}
	}

	private void DocumentScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
	{
		RequestViewportRefresh();
	}

	private void ThumbScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
	{
		RequestViewportRefresh();
	}

	private void RequestViewportRefresh()
	{
		_viewportTimer.Stop();
		_viewportTimer.Start();
	}

	[DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern bool rotate_pdf_page([MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath, int pageNumber, int rotationDelta, [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

	[DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern bool delete_pdf_page([MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath, int pageNumber, [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

	[DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern bool insert_blank_page([MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath, int targetPage, bool insertBefore, [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

	[DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern bool reorder_pdf_pages([MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath, [MarshalAs(UnmanagedType.LPUTF8Str)] string orderSemicolon, [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

	[DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern bool extract_pdf_pages([MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath, [MarshalAs(UnmanagedType.LPUTF8Str)] string pagesSemicolon, [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

	[DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern bool replace_pdf_text([MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath, int pageNumber, [MarshalAs(UnmanagedType.LPUTF8Str)] string originalText, [MarshalAs(UnmanagedType.LPUTF8Str)] string replacementText, [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

	[DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern bool overlay_pdf_image([MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath, int pageNumber, [MarshalAs(UnmanagedType.LPUTF8Str)] string imagePath, double x, double y, double width, double height, [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

	public PdfDocumentTab(string path)
	{
		InitializeComponent();
		RenderOptions.SetBitmapScalingMode(PagesHost, BitmapScalingMode.HighQuality);
		_zoomTimer = new DispatcherTimer();
		_zoomTimer.Interval = TimeSpan.FromMilliseconds(120.0);
		_zoomTimer.Tick += delegate
		{
			_zoomTimer.Stop();
			RenderOptions.SetBitmapScalingMode(PagesHost, BitmapScalingMode.HighQuality);
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
			double currentScale = CurrentZoom / Math.Max(0.0001, _baseZoomForLayout);
			Point hostPoint = GetZoomHostAnchor(_smoothZoomAnchor, currentScale);
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
			ScrollToKeepHostPointAtViewport(hostPoint, _smoothZoomAnchor, ratio, updateLayout: false);
		};
		_viewportTimer = new DispatcherTimer();
		_viewportTimer.Interval = TimeSpan.FromMilliseconds(50.0);
		_viewportTimer.Tick += delegate
		{
			_viewportTimer.Stop();
			UpdateSelectedPageFromViewport();
		};
		DocumentScrollViewer.PreviewMouseWheel += DocumentScrollViewer_PreviewMouseWheel;
		DocumentScrollViewer.PreviewMouseDown += DocumentScrollViewer_PreviewMouseDown;
		DocumentScrollViewer.PreviewMouseMove += DocumentScrollViewer_PreviewMouseMove;
		DocumentScrollViewer.PreviewMouseUp += DocumentScrollViewer_PreviewMouseUp;
		DocumentScrollViewer.ScrollChanged += DocumentScrollViewer_ScrollChanged;
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
		PdfPerfLogger.Log("LoadDocument start: " + System.IO.Path.GetFileName(path));
		try
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			nint tempDoc = PdfiumEngine.FPDF_LoadDocument(path, null);
			stopwatch.Stop();
			PdfPerfLogger.Log($"FPDF_LoadDocument: {stopwatch.ElapsedMilliseconds} ms");
			if (tempDoc == IntPtr.Zero)
			{
				MessageBox.Show("Unable to load the selected PDF file.", "Load error", MessageBoxButton.OK, MessageBoxImage.Hand);
				LogStatus("Failed to load PDF");
				PdfPerfLogger.Log("LoadDocument failed: document handle is null");
				return;
			}
			_documentHandle = tempDoc;
			Stopwatch stopwatch2 = Stopwatch.StartNew();
			int pageCount = PdfiumEngine.FPDF_GetPageCount(tempDoc);
			stopwatch2.Stop();
			PdfPerfLogger.Log($"FPDF_GetPageCount: {stopwatch2.ElapsedMilliseconds} ms (pages={pageCount})");
			if (pageCount < 0)
			{
				MessageBox.Show("Unable to load the selected PDF file.", "Load error", MessageBoxButton.OK, MessageBoxImage.Hand);
				LogStatus("Failed to load PDF");
				PdfPerfLogger.Log("LoadDocument failed: invalid page count");
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
				PdfPerfLogger.Log($"First page size probe: {stopwatch3.ElapsedMilliseconds} ms");
				_isFirstLoad = false;
			}
			Stopwatch dimensionSw = Stopwatch.StartNew();
			List<Size> collection = await Task.Run(() => CollectPageDimensions(tempDoc, pageCount));
			dimensionSw.Stop();
			PdfPerfLogger.Log($"CollectPageDimensions({pageCount}): {dimensionSw.ElapsedMilliseconds} ms");
			if (loadGeneration == _loadGeneration)
			{
				_pageDimensions.Clear();
				_pageDimensions.AddRange(collection);
				if (base.IsLoaded)
				{
					PdfPerfLogger.Log("LoadDocument triggering initial render");
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
			PdfPerfLogger.Log("LoadDocument exception: " + ex2.Message);
			CloseActiveDocument();
		}
		finally
		{
			totalSw.Stop();
			PdfPerfLogger.Log($"LoadDocument total: {totalSw.ElapsedMilliseconds} ms");
		}
	}

	private void CloseActiveDocument()
	{
		CloseTextPages();
		if (_documentHandle != IntPtr.Zero)
		{
			PdfiumEngine.CloseDocument(_documentHandle);
			_documentHandle = IntPtr.Zero;
			PdfPerfLogger.Log("CloseActiveDocument closed the cached document handle.");
		}
	}

	public async void RenderPdfPages()
	{
		if (_renderInProgress)
		{
			_renderAgainRequested = true;
			_renderGeneration++;
			return;
		}
		_renderInProgress = true;
		try
		{
			do
			{
				_renderAgainRequested = false;
				await RenderPdfPagesCoreAsync(++_renderGeneration);
			}
			while (_renderAgainRequested);
		}
		finally
		{
			_renderInProgress = false;
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

	private async Task RenderPdfPagesFromCacheAsync(int renderGeneration, bool rebuildThumbnails)
	{
		Stopwatch renderSw = Stopwatch.StartNew();
		int pageCount = PageCount;
		if (pageCount <= 0 || _pageDimensions.Count != pageCount)
		{
			PdfPerfLogger.Log($"RenderPdfPagesFromCacheAsync skipped: pageCount={pageCount}, dimensions={_pageDimensions.Count}");
		}
		else
		{
			if (!TryApplyInitialZoom())
			{
				return;
			}
			if (renderGeneration != _renderGeneration)
			{
				PdfPerfLogger.Log("RenderPdfPagesFromCacheAsync aborted: generation changed");
				return;
			}
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Vertical,
				HorizontalAlignment = HorizontalAlignment.Center
			};
			List<UIElement> list = new List<UIElement>();
			_loadingPages.Clear();
			_loadingThumbs.Clear();
			_thumbnailLoadDeferred = rebuildThumbnails;
			for (int i = 0; i < _pageOrder.Count; i++)
			{
				if (renderGeneration != _renderGeneration)
				{
					return;
				}
				int pageNumber = _pageOrder[i];
				double width = _pageDimensions[pageNumber - 1].Width;
				double height = _pageDimensions[pageNumber - 1].Height;
				int num = (int)(width * 1.33 * CurrentZoom);
				int num2 = (int)(height * 1.33 * CurrentZoom);
				Image image = new Image
				{
					Width = num,
					Height = num2,
					HorizontalAlignment = HorizontalAlignment.Center
				};
				RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
				Canvas canvas = new Canvas
				{
					Background = Brushes.Transparent,
					Width = num,
					Height = num2,
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center,
					Tag = pageNumber
				};
				canvas.MouseDown += OverlayCanvas_MouseDown;
				canvas.MouseMove += OverlayCanvas_MouseMove;
				canvas.MouseUp += OverlayCanvas_MouseUp;
				Grid grid = new Grid
				{
					Width = num,
					Height = num2,
					Margin = new Thickness(0.0, 0.0, 0.0, 15.0),
					HorizontalAlignment = HorizontalAlignment.Center
				};
				grid.Children.Add(image);
				grid.Children.Add(canvas);
				if (_pageRotations.TryGetValue(pageNumber, out var value) && value != 0)
				{
					grid.LayoutTransform = new RotateTransform(value);
				}
				Border pageBorder = new Border
				{
					Background = Brushes.White,
					BorderBrush = Brushes.DarkGray,
					BorderThickness = new Thickness(1.0),
					Margin = new Thickness(0.0, 0.0, 0.0, 20.0),
					Tag = pageNumber,
					Child = grid
				};
				stackPanel.Children.Add(pageBorder);
				if (!rebuildThumbnails)
				{
					continue;
				}
				Image image2 = new Image
				{
					Width = 150.0,
					Height = (int)(height / width * 150.0),
					Cursor = Cursors.Hand
				};
				Border thumbBorder = new Border
				{
					Background = Brushes.White,
					BorderBrush = Brushes.Gray,
					BorderThickness = new Thickness(1.0),
					Margin = new Thickness(5.0, 5.0, 5.0, 10.0),
					Tag = pageNumber,
					Child = image2
				};
				if (_pageRotations.TryGetValue(pageNumber, out var value2) && value2 != 0)
				{
					image2.LayoutTransform = new RotateTransform(value2);
				}
				thumbBorder.ContextMenu = CreateThumbnailContextMenu(pageNumber);
				thumbBorder.AllowDrop = true;
				Point? dragStartPoint = null;
				thumbBorder.PreviewMouseLeftButtonDown += delegate(object s, MouseButtonEventArgs ev)
				{
					dragStartPoint = ev.GetPosition(null);
					SelectThumbnailPage(pageNumber, Keyboard.Modifiers);
					pageBorder.BringIntoView();
				};
				thumbBorder.PreviewMouseRightButtonDown += delegate
				{
					EnsureContextMenuSelection(pageNumber);
					pageBorder.BringIntoView();
				};
				thumbBorder.MouseMove += delegate(object s, MouseEventArgs ev)
				{
					if (ev.LeftButton == MouseButtonState.Pressed && dragStartPoint.HasValue)
					{
						Point position = ev.GetPosition(null);
						Vector vector = dragStartPoint.Value - position;
						if (Math.Abs(vector.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(vector.Y) > SystemParameters.MinimumVerticalDragDistance)
						{
							DragDrop.DoDragDrop(thumbBorder, pageNumber, DragDropEffects.Move);
							dragStartPoint = null;
						}
					}
				};
				thumbBorder.DragEnter += delegate(object s, DragEventArgs ev)
				{
					if (ev.Data.GetDataPresent(typeof(int)))
					{
						thumbBorder.BorderBrush = Brushes.DeepSkyBlue;
						thumbBorder.BorderThickness = new Thickness(2.0);
					}
				};
				thumbBorder.DragLeave += delegate
				{
					thumbBorder.BorderBrush = Brushes.Gray;
					thumbBorder.BorderThickness = new Thickness(1.0);
				};
				thumbBorder.DragOver += delegate(object s, DragEventArgs ev)
				{
					if (ev.Data.GetDataPresent(typeof(int)))
					{
						ev.Effects = DragDropEffects.Move;
						ev.Handled = true;
					}
				};
				thumbBorder.Drop += delegate(object s, DragEventArgs ev)
				{
					if (ev.Data.GetDataPresent(typeof(int)))
					{
						int num3 = (int)ev.Data.GetData(typeof(int));
						int num4 = pageNumber;
						thumbBorder.BorderBrush = Brushes.Gray;
						thumbBorder.BorderThickness = new Thickness(1.0);
						if (num3 != num4)
						{
							int num5 = _pageOrder.IndexOf(num3);
							int num6 = _pageOrder.IndexOf(num4);
							if (num5 >= 0 && num6 >= 0)
							{
								_pageOrder.RemoveAt(num5);
								_pageOrder.Insert(num6, num3);
								RenderPdfPages();
							}
						}
						ev.Handled = true;
					}
				};
				list.Add(thumbBorder);
			}
			SetSelectedPage(Math.Clamp(SelectedPageNumber, 1, Math.Max(1, pageCount)));
			PagesHost.Children.Clear();
			PagesHost.Children.Add(stackPanel);
			ResetZoomPreviewTransform();
			_baseZoomForLayout = CurrentZoom;
			if (rebuildThumbnails)
			{
				ThumbnailContainer.Children.Clear();
				foreach (UIElement item in list)
				{
					ThumbnailContainer.Children.Add(item);
				}
				UpdateThumbnailSelectionVisuals();
			}
			await base.Dispatcher.InvokeAsync(delegate
			{
			}, DispatcherPriority.Loaded);
			if (renderGeneration != _renderGeneration)
			{
				PdfPerfLogger.Log("RenderPdfPagesFromCacheAsync aborted after layout: generation changed");
				return;
			}
			ApplyPendingZoomScroll();
			if (_resetScrollAfterRender)
			{
				DocumentScrollViewer.ScrollToHome();
				DocumentScrollViewer.ScrollToLeftEnd();
				_resetScrollAfterRender = false;
			}
			LogStatus($"Loaded {pageCount} pages (Lazy Load)");
			UpdateSelectedPageFromViewport();
			QueuePageRender(SelectedPageNumber, 0, renderGeneration);
			StartProgressivePagePrefetchAsync(renderGeneration, SelectedPageNumber);
			if (rebuildThumbnails)
			{
				StartDeferredThumbnailLoadAsync(renderGeneration);
			}
			renderSw.Stop();
			PdfPerfLogger.Log($"RenderPdfPagesFromCacheAsync: {renderSw.ElapsedMilliseconds} ms (pages={pageCount}, thumbnails={rebuildThumbnails})");
		}
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

	private async Task RenderPdfPagesCoreAsync(int renderGeneration)
	{
		Stopwatch totalSw = Stopwatch.StartNew();
		bool rebuildThumbnails = ThumbnailContainer.Children.Count == 0 || _resetScrollAfterRender;
		ClearRenderQueue();
		if (string.IsNullOrEmpty(CurrentPdfPath))
		{
			PagesHost.Children.Clear();
			ThumbnailContainer.Children.Clear();
			EmptyStateText.Visibility = Visibility.Visible;
			ReportPageChanged();
			return;
		}
		if (_pageDimensions.Count == PageCount && PageCount > 0)
		{
			PdfPerfLogger.Log("RenderPdfPagesCoreAsync using cached dimensions");
			await RenderPdfPagesFromCacheAsync(renderGeneration, rebuildThumbnails);
			return;
		}
		if (!TryApplyInitialZoom())
		{
			PdfPerfLogger.Log("RenderPdfPagesCoreAsync waiting for initial zoom");
			return;
		}
		try
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			nint tempDoc = PdfiumEngine.FPDF_LoadDocument(CurrentPdfPath, null);
			stopwatch.Stop();
			PdfPerfLogger.Log($"Render open document: {stopwatch.ElapsedMilliseconds} ms");
			if (tempDoc == IntPtr.Zero)
			{
				LogStatus("Failed to open document with PDFium");
				EmptyStateText.Visibility = Visibility.Visible;
				return;
			}
			int pageCount = 0;
			List<Size> pageDimensions = new List<Size>();
			try
			{
				Stopwatch stopwatch2 = Stopwatch.StartNew();
				pageCount = PdfiumEngine.FPDF_GetPageCount(tempDoc);
				stopwatch2.Stop();
				PdfPerfLogger.Log($"Render page count: {stopwatch2.ElapsedMilliseconds} ms (pages={pageCount})");
				PageCount = pageCount;
				Stopwatch dimensionSw = Stopwatch.StartNew();
				await Task.Run(delegate
				{
					for (int i = 0; i < pageCount; i++)
					{
						nint num = PdfiumEngine.FPDF_LoadPage(tempDoc, i);
						if (num != IntPtr.Zero)
						{
							pageDimensions.Add(new Size(PdfiumEngine.FPDF_GetPageWidth(num), PdfiumEngine.FPDF_GetPageHeight(num)));
							PdfiumEngine.FPDF_ClosePage(num);
						}
						else
						{
							pageDimensions.Add(new Size(800.0, 1100.0));
						}
					}
				});
				dimensionSw.Stop();
				PdfPerfLogger.Log($"Render collect dimensions: {dimensionSw.ElapsedMilliseconds} ms");
			}
			finally
			{
				PdfiumEngine.FPDF_CloseDocument(tempDoc);
			}
			if (renderGeneration != _renderGeneration)
			{
				PdfPerfLogger.Log("RenderPdfPagesCoreAsync aborted: generation changed after dimensions");
				return;
			}
			_pageDimensions.Clear();
			_pageDimensions.AddRange(pageDimensions);
			await RenderPdfPagesFromCacheAsync(renderGeneration, rebuildThumbnails);
			totalSw.Stop();
			PdfPerfLogger.Log($"RenderPdfPagesCoreAsync total: {totalSw.ElapsedMilliseconds} ms");
		}
		catch (Exception ex)
		{
			LogStatus("Load failed: " + ex.Message);
			EmptyStateText.Visibility = Visibility.Visible;
			PdfPerfLogger.Log("RenderPdfPagesCoreAsync exception: " + ex.Message);
		}
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
		if (activeRotations.Count == 0 && !isOrderChanged && !hasPendingTextEdits)
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
						bool flag = overlay_pdf_image(workingFile, pendingTextEdit.Edit.PageNumber, pendingTextEdit.ImagePath, pendingTextEdit.Edit.Left, pendingTextEdit.Edit.Bottom, pendingTextEdit.Edit.Width, pendingTextEdit.Edit.Height, text);
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
				foreach (KeyValuePair<int, int> item in activeRotations)
				{
					int key = item.Key;
					int value = item.Value;
					string text = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
					bool flag = rotate_pdf_page(tempFile, key, value, text);
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
					bool flag2 = reorder_pdf_pages(tempFile, orderSemicolon, text2);
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
			success = await Task.Run(() => delete_pdf_page(CurrentPdfPath, pageNumber, outputPath));
		}
		else
		{
			string pagesToKeep = string.Join(";", pagesToKeepInOrder);
			success = await Task.Run(() => reorder_pdf_pages(CurrentPdfPath, pagesToKeep, outputPath));
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
		if (await Task.Run(() => insert_blank_page(CurrentPdfPath, SelectedPageNumber, optDialog.InsertBefore, outputPath)))
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
		if (await Task.Run(() => reorder_pdf_pages(CurrentPdfPath, orderSemicolon, outputPath)))
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
		if (await Task.Run(() => extract_pdf_pages(CurrentPdfPath, pageNumber.ToString(), outputPath)))
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
		if (await Task.Run(() => extract_pdf_pages(CurrentPdfPath, pagesSemicolon, outputPath)))
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

	private void UpdateSelectedPageFromViewport()
	{
		if (PagesHost.Children.Count == 0 || !(PagesHost.Children[0] is StackPanel stackPanel) || stackPanel.Children.Count == 0)
		{
			return;
		}
		double num = DocumentScrollViewer.ViewportHeight / 2.0;
		int num2 = SelectedPageNumber;
		double num3 = double.MaxValue;
		List<int> list = new List<int>();
		foreach (UIElement child in stackPanel.Children)
		{
			if (!(child is Border { Tag: var tag } border) || !(tag is int num4))
			{
				continue;
			}
			try
			{
				double y = border.TransformToAncestor(DocumentScrollViewer).Transform(new Point(0.0, 0.0)).Y;
				if (y + border.ActualHeight > -1500.0 && y < DocumentScrollViewer.ViewportHeight + 1500.0)
				{
					list.Add(num4);
				}
				else if (border.Child is Grid grid)
				{
					Image image = grid.Children.OfType<Image>().FirstOrDefault();
					if (image != null && image.Source != null)
					{
						image.Source = null;
					}
				}
				double num5 = Math.Abs(y + border.ActualHeight / 2.0 - num);
				if (num5 < num3)
				{
					num3 = num5;
					num2 = num4;
				}
			}
			catch (InvalidOperationException)
			{
				return;
			}
		}
		if (num2 != SelectedPageNumber)
		{
			SelectedPageNumber = num2;
			ReportPageChanged();
			UpdateThumbnailSelectionVisuals();
			RecordRecentPage(SelectedPageNumber);
			RefreshBookmarksPanel();
		}
		QueuePageRender(SelectedPageNumber, 0, _renderGeneration);
		bool highCostRender = IsHighCostRenderZoom();
		foreach (int item2 in from pageNumber in list
			where pageNumber != SelectedPageNumber
			where !highCostRender || Math.Abs(pageNumber - SelectedPageNumber) <= 1
			orderby Math.Abs(pageNumber - SelectedPageNumber)
			select pageNumber)
		{
			QueuePageRender(item2, Math.Abs(item2 - SelectedPageNumber), _renderGeneration);
		}
		if (highCostRender || _zoomPreviewActive || !(ThumbnailContainer.Parent is ScrollViewer scrollViewer))
		{
			return;
		}
		List<int> list2 = new List<int>();
		foreach (UIElement child2 in ThumbnailContainer.Children)
		{
			if (!(child2 is Border { Tag: var tag2 } border2) || !(tag2 is int item))
			{
				continue;
			}
			try
			{
				double y2 = border2.TransformToAncestor(scrollViewer).Transform(new Point(0.0, 0.0)).Y;
				if (y2 + border2.ActualHeight > -500.0 && y2 < scrollViewer.ViewportHeight + 500.0 && !_thumbnailLoadDeferred)
				{
					list2.Add(item);
				}
			}
			catch (InvalidOperationException)
			{
			}
		}
		QueueThumbnailRender(SelectedPageNumber, 100, _renderGeneration);
		foreach (int item3 in from pageNumber in list2
			where pageNumber != SelectedPageNumber
			orderby Math.Abs(pageNumber - SelectedPageNumber)
			select pageNumber)
		{
			QueueThumbnailRender(item3, 100 + Math.Abs(item3 - SelectedPageNumber), _renderGeneration);
		}
	}

	private async Task LoadPageContent(int pageNumber, Border pageBorder, int renderGeneration)
	{
		UIElement child = pageBorder.Child;
		if (!(child is Grid grid))
		{
			return;
		}
		Image image = grid.Children.OfType<Image>().FirstOrDefault();
		if (image == null || image.Source != null || !_loadingPages.Add(pageNumber))
		{
			return;
		}
		try
		{
			string pdfPath = CurrentPdfPath;
			if (string.IsNullOrWhiteSpace(pdfPath))
			{
				return;
			}
			double currentZoom = CurrentZoom;
			if (pageNumber - 1 >= _pageDimensions.Count)
			{
				return;
			}
			double width = _pageDimensions[pageNumber - 1].Width;
			double height = _pageDimensions[pageNumber - 1].Height;
			int renderWidth = Math.Max(1, (int)(width * 1.33 * currentZoom));
			int renderHeight = Math.Max(1, (int)(height * 1.33 * currentZoom));
			string cacheKey = BuildPageCacheKey(pageNumber, renderWidth, renderHeight, isThumbnail: false);
			if (TryGetCachedBitmap(cacheKey, out BitmapSource bitmap) && bitmap != null)
			{
				PdfPerfLogger.Log($"Page {pageNumber} cache hit ({renderWidth}x{renderHeight})");
				if (renderGeneration == _renderGeneration)
				{
					image.Source = bitmap;
					image.Width = bitmap.Width;
					image.Height = bitmap.Height;
					Canvas canvas = grid.Children.OfType<Canvas>().FirstOrDefault();
					if (canvas != null)
					{
						canvas.Width = bitmap.Width;
						canvas.Height = bitmap.Height;
						RedrawPageAnnotations(canvas, pageNumber);
					}
				}
				return;
			}
			Stopwatch renderSw = Stopwatch.StartNew();
			BitmapSource bitmapSource = await Task.Run(delegate
			{
				try
				{
					if (_documentHandle != IntPtr.Zero)
					{
						return PdfiumEngine.RenderPageToBitmap(_documentHandle, pageNumber - 1, renderWidth, renderHeight);
					}
					return PdfiumEngine.RenderPageToBitmap(pdfPath, pageNumber - 1, renderWidth, renderHeight);
				}
				catch (Exception ex2)
				{
					Exception ex3 = ex2;
					Exception ex4 = ex3;
					base.Dispatcher.BeginInvoke((Action)delegate
					{
						LogStatus("Page render failed: " + ex4.Message);
					});
					return (BitmapSource)null;
				}
			});
			renderSw.Stop();
			if (renderGeneration == _renderGeneration && bitmapSource != null)
			{
				PdfPerfLogger.Log($"Page {pageNumber} render miss ({renderWidth}x{renderHeight}) => {renderSw.ElapsedMilliseconds} ms");
				StoreBitmap(cacheKey, bitmapSource);
				image.Source = bitmapSource;
				image.Width = bitmapSource.Width;
				image.Height = bitmapSource.Height;
				Canvas canvas2 = grid.Children.OfType<Canvas>().FirstOrDefault();
				if (canvas2 != null)
				{
					canvas2.Width = bitmapSource.Width;
					canvas2.Height = bitmapSource.Height;
					RedrawPageAnnotations(canvas2, pageNumber);
				}
			}
		}
		catch (Exception ex)
		{
			LogStatus("Page load failed: " + ex.Message);
		}
		finally
		{
			_loadingPages.Remove(pageNumber);
		}
	}

	private async Task LoadThumbnailContent(int pageNumber, Border thumbBorder, int renderGeneration)
	{
		UIElement child = thumbBorder.Child;
		if (!(child is Image { Source: null } image) || !_loadingThumbs.Add(pageNumber))
		{
			return;
		}
		try
		{
			string pdfPath = CurrentPdfPath;
			if (string.IsNullOrWhiteSpace(pdfPath) || pageNumber - 1 >= _pageDimensions.Count)
			{
				return;
			}
			double width = _pageDimensions[pageNumber - 1].Width;
			double height = _pageDimensions[pageNumber - 1].Height;
			int thumbWidth = 150;
			int thumbHeight = Math.Max(1, (int)(height / Math.Max(1.0, width) * (double)thumbWidth));
			string cacheKey = BuildPageCacheKey(pageNumber, thumbWidth, thumbHeight, isThumbnail: true);
			if (TryGetCachedBitmap(cacheKey, out BitmapSource bitmap) && bitmap != null)
			{
				PdfPerfLogger.Log($"Thumb {pageNumber} cache hit ({thumbWidth}x{thumbHeight})");
				if (renderGeneration == _renderGeneration)
				{
					image.Source = bitmap;
				}
				return;
			}
			Stopwatch renderSw = Stopwatch.StartNew();
			BitmapSource bitmapSource = await Task.Run(delegate
			{
				try
				{
					if (_documentHandle != IntPtr.Zero)
					{
						return PdfiumEngine.RenderPageToBitmap(_documentHandle, pageNumber - 1, thumbWidth, thumbHeight);
					}
					return PdfiumEngine.RenderPageToBitmap(pdfPath, pageNumber - 1, thumbWidth, thumbHeight);
				}
				catch (Exception ex2)
				{
					Exception ex3 = ex2;
					Exception ex4 = ex3;
					base.Dispatcher.BeginInvoke((Action)delegate
					{
						LogStatus("Thumbnail render failed: " + ex4.Message);
					});
					return (BitmapSource)null;
				}
			});
			renderSw.Stop();
			if (renderGeneration == _renderGeneration && bitmapSource != null)
			{
				PdfPerfLogger.Log($"Thumb {pageNumber} render miss ({thumbWidth}x{thumbHeight}) => {renderSw.ElapsedMilliseconds} ms");
				StoreBitmap(cacheKey, bitmapSource);
				image.Source = bitmapSource;
			}
		}
		catch (Exception ex)
		{
			LogStatus("Thumbnail load failed: " + ex.Message);
		}
		finally
		{
			_loadingThumbs.Remove(pageNumber);
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

	private void SetSelectedPage(int pageNumber)
	{
		if (PageCount <= 0)
		{
			SelectedPageNumber = 1;
			ReportPageChanged();
			UpdateBookmarkControlsState();
			return;
		}
		SelectedPageNumber = Math.Clamp(pageNumber, 1, PageCount);
		if (_selectedPages.Count == 0)
		{
			_selectedPages.Add(SelectedPageNumber);
			_selectionAnchorPage = SelectedPageNumber;
		}
		ReportPageChanged();
		UpdateThumbnailSelectionVisuals();
		RecordRecentPage(SelectedPageNumber);
		UpdateBookmarkControlsState();
		UnloadDistantPageContent();
	}

	private void SelectThumbnailPage(int pageNumber, ModifierKeys modifiers)
	{
		if (PageCount <= 0)
		{
			return;
		}

		int num = Math.Clamp(pageNumber, 1, PageCount);
		if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
		{
			SelectPageRange(_selectionAnchorPage, num);
		}
		else if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
		{
			if (!_selectedPages.Add(num))
			{
				_selectedPages.Remove(num);
			}
			if (_selectedPages.Count == 0)
			{
				_selectedPages.Add(num);
			}
			_selectionAnchorPage = num;
		}
		else
		{
			_selectedPages.Clear();
			_selectedPages.Add(num);
			_selectionAnchorPage = num;
		}

		SetSelectedPage(num);
	}

	private void SelectPageRange(int anchorPage, int targetPage)
	{
		if (_pageOrder.Count == 0)
		{
			_selectedPages.Clear();
			_selectedPages.Add(targetPage);
			return;
		}

		int anchorIndex = _pageOrder.IndexOf(anchorPage);
		int targetIndex = _pageOrder.IndexOf(targetPage);
		if (anchorIndex < 0 || targetIndex < 0)
		{
			_selectedPages.Clear();
			_selectedPages.Add(targetPage);
			return;
		}

		int start = Math.Min(anchorIndex, targetIndex);
		int end = Math.Max(anchorIndex, targetIndex);
		_selectedPages.Clear();
		for (int i = start; i <= end; i++)
		{
			_selectedPages.Add(_pageOrder[i]);
		}
	}

	private List<int> GetSelectedPagesInOrder()
	{
		if (_selectedPages.Count == 0)
		{
			return new List<int>();
		}

		List<int> ordered = new List<int>();
		foreach (int page in _pageOrder)
		{
			if (_selectedPages.Contains(page))
			{
				ordered.Add(page);
			}
		}
		return ordered;
	}

	private void EnsurePageOrderInitialized()
	{
		if (_pageOrder.Count == PageCount)
		{
			return;
		}

		_pageOrder.Clear();
		_pageOrder.AddRange(Enumerable.Range(1, PageCount));
	}

	private bool MoveSelectedPagesUp(HashSet<int> selectedSet)
	{
		bool moved = false;
		for (int i = 1; i < _pageOrder.Count; i++)
		{
			if (selectedSet.Contains(_pageOrder[i]) && !selectedSet.Contains(_pageOrder[i - 1]))
			{
				(_pageOrder[i - 1], _pageOrder[i]) = (_pageOrder[i], _pageOrder[i - 1]);
				moved = true;
			}
		}

		return moved;
	}

	private bool MoveSelectedPagesDown(HashSet<int> selectedSet)
	{
		bool moved = false;
		for (int i = _pageOrder.Count - 2; i >= 0; i--)
		{
			if (selectedSet.Contains(_pageOrder[i]) && !selectedSet.Contains(_pageOrder[i + 1]))
			{
				(_pageOrder[i + 1], _pageOrder[i]) = (_pageOrder[i], _pageOrder[i + 1]);
				moved = true;
			}
		}

		return moved;
	}

	private System.Windows.Controls.ContextMenu CreateThumbnailContextMenu(int pageNumber)
	{
		System.Windows.Controls.ContextMenu contextMenu = new System.Windows.Controls.ContextMenu();
		contextMenu.Opened += delegate
		{
			EnsureContextMenuSelection(pageNumber);
		};

		contextMenu.Items.Add(CreateThumbnailMenuItem("Rotate left", async delegate
		{
			await RotateSelectedPageAsync(-90);
		}));
		contextMenu.Items.Add(CreateThumbnailMenuItem("Rotate right", async delegate
		{
			await RotateSelectedPageAsync(90);
		}));
		contextMenu.Items.Add(CreateThumbnailMenuItem("Extract selected", async delegate
		{
			await ExtractSelectedPagesAsync();
		}));
		contextMenu.Items.Add(CreateThumbnailMenuItem("Duplicate selected", async delegate
		{
			await DuplicateSelectedPageAsync();
		}));
		contextMenu.Items.Add(CreateThumbnailMenuItem("Delete selected", async delegate
		{
			await DeleteSelectedPageAsync();
		}));
		contextMenu.Items.Add(new System.Windows.Controls.Separator());
		contextMenu.Items.Add(CreateThumbnailMenuItem("Move selected up", delegate
		{
			MoveSelectedPage(-1);
		}));
		contextMenu.Items.Add(CreateThumbnailMenuItem("Move selected down", delegate
		{
			MoveSelectedPage(1);
		}));
		contextMenu.Items.Add(new System.Windows.Controls.Separator());
		contextMenu.Items.Add(CreateThumbnailMenuItem("Select all pages", delegate
		{
			SelectAllThumbnailPages();
		}));
		contextMenu.Items.Add(CreateThumbnailMenuItem("Invert selection", delegate
		{
			InvertThumbnailSelection();
		}));
		contextMenu.Items.Add(CreateThumbnailMenuItem("Select odd pages", delegate
		{
			SelectThumbnailPagesByParity(selectOddPages: true);
		}));
		contextMenu.Items.Add(CreateThumbnailMenuItem("Select even pages", delegate
		{
			SelectThumbnailPagesByParity(selectOddPages: false);
		}));
		contextMenu.Items.Add(new System.Windows.Controls.Separator());
		contextMenu.Items.Add(CreateThumbnailMenuItem("Bookmark selected", delegate
		{
			BookmarkSelectedPages();
		}));
		contextMenu.Items.Add(CreateThumbnailMenuItem("Clear selection", delegate
		{
			ClearThumbnailSelection();
		}));

		return contextMenu;
	}

	private System.Windows.Controls.MenuItem CreateThumbnailMenuItem(string header, RoutedEventHandler clickHandler)
	{
		System.Windows.Controls.MenuItem item = new System.Windows.Controls.MenuItem
		{
			Header = header
		};
		item.Click += clickHandler;
		return item;
	}

	private void EnsureContextMenuSelection(int pageNumber)
	{
		if (PageCount <= 0)
		{
			return;
		}

		int num = Math.Clamp(pageNumber, 1, PageCount);
		if (!_selectedPages.Contains(num))
		{
			_selectedPages.Clear();
			_selectedPages.Add(num);
			_selectionAnchorPage = num;
		}

		SetSelectedPage(num);
	}

	private void ClearThumbnailSelection()
	{
		if (PageCount <= 0)
		{
			return;
		}

		int num = Math.Clamp(SelectedPageNumber, 1, PageCount);
		_selectedPages.Clear();
		_selectedPages.Add(num);
		_selectionAnchorPage = num;
		UpdateThumbnailSelectionVisuals();
		LogStatus($"Selection cleared to page {num}.");
	}

	private void SelectAllThumbnailPages()
	{
		if (PageCount <= 0)
		{
			return;
		}

		EnsurePageOrderInitialized();
		ApplyThumbnailSelection(_pageOrder, $"Selected all {PageCount} pages.");
	}

	private void InvertThumbnailSelection()
	{
		if (PageCount <= 0)
		{
			return;
		}

		EnsurePageOrderInitialized();
		HashSet<int> currentSelection = new HashSet<int>(_selectedPages);
		List<int> inverted = _pageOrder.Where(page => !currentSelection.Contains(page)).ToList();
		if (inverted.Count == 0)
		{
			inverted.Add(Math.Clamp(SelectedPageNumber, 1, PageCount));
		}

		ApplyThumbnailSelection(inverted, $"Inverted selection: {inverted.Count} pages selected.");
	}

	private void SelectThumbnailPagesByParity(bool selectOddPages)
	{
		if (PageCount <= 0)
		{
			return;
		}

		EnsurePageOrderInitialized();
		List<int> pages = _pageOrder.Where(page => selectOddPages ? page % 2 == 1 : page % 2 == 0).ToList();
		if (pages.Count == 0)
		{
			LogStatus(selectOddPages ? "No odd pages to select." : "No even pages to select.");
			return;
		}

		ApplyThumbnailSelection(pages, selectOddPages ? $"Selected {pages.Count} odd pages." : $"Selected {pages.Count} even pages.");
	}

	private void ApplyThumbnailSelection(IEnumerable<int> pages, string statusMessage)
	{
		List<int> pageList = pages.Where(page => page >= 1 && page <= PageCount).Distinct().ToList();
		if (pageList.Count == 0)
		{
			return;
		}

		_selectedPages.Clear();
		foreach (int page in pageList)
		{
			_selectedPages.Add(page);
		}

		_selectionAnchorPage = pageList[0];
		SetSelectedPage(pageList[0]);
		LogStatus(statusMessage);
	}

	private void RecordRecentPage(int pageNumber)
	{
		if (PageCount <= 0)
		{
			return;
		}

		int num = Math.Clamp(pageNumber, 1, PageCount);
		_recentPages.Remove(num);
		_recentPages.Insert(0, num);
		while (_recentPages.Count > RecentPagesLimit)
		{
			_recentPages.RemoveAt(_recentPages.Count - 1);
		}

		RefreshRecentPagesPanel();
		SaveNavigationState();
	}

	private void LoadPersistedNavigationState(string path)
	{
		DocumentNavigationState state = DocumentNavigationStateService.Load(path, PageCount);
		_recentPages.Clear();
		_recentPages.AddRange(state.RecentPages);
		_bookmarkedPages.Clear();
		foreach (int page in state.BookmarkedPages)
		{
			_bookmarkedPages.Add(page);
		}

		RefreshRecentPagesPanel();
		RefreshBookmarksPanel();
		UpdateBookmarkControlsState();
	}

	private void SaveNavigationState()
	{
		if (string.IsNullOrWhiteSpace(CurrentPdfPath) || PageCount <= 0)
		{
			return;
		}

		DocumentNavigationStateService.Save(CurrentPdfPath, _recentPages, _bookmarkedPages, PageCount);
	}

	private void ToggleBookmarkCurrentPage()
	{
		if (PageCount <= 0)
		{
			return;
		}

		int num = Math.Clamp(SelectedPageNumber, 1, PageCount);
		if (!_bookmarkedPages.Add(num))
		{
			_bookmarkedPages.Remove(num);
		}

		RefreshBookmarksPanel();
		SaveNavigationState();
	}

	private void BookmarkSelectedPages()
	{
		if (PageCount <= 0)
		{
			return;
		}

		List<int> pages = GetSelectedPagesInOrder();
		if (pages.Count == 0)
		{
			pages.Add(Math.Clamp(SelectedPageNumber, 1, PageCount));
		}

		foreach (int page in pages)
		{
			_bookmarkedPages.Add(page);
		}

		RefreshBookmarksPanel();
		SaveNavigationState();
		LogStatus(pages.Count == 1 ? $"Bookmarked page {pages[0]}." : $"Bookmarked {pages.Count} selected pages.");
	}

	private void BookmarkCurrentPage_Click(object sender, RoutedEventArgs e)
	{
		ToggleBookmarkCurrentPage();
	}

	private void ClearBookmarks_Click(object sender, RoutedEventArgs e)
	{
		_bookmarkedPages.Clear();
		RefreshBookmarksPanel();
		SaveNavigationState();
	}

	private void RefreshRecentPagesPanel()
	{
		RecentPagesContainer.Children.Clear();
		if (PageCount <= 0 || _recentPages.Count == 0)
		{
			RecentPagesContainer.Children.Add(CreateEmptyPanelMessage("No recent pages"));
			return;
		}

		foreach (int recentPage in _recentPages)
		{
			RecentPagesContainer.Children.Add(CreatePageJumpButton($"Page {recentPage}", recentPage, recentPage == SelectedPageNumber));
		}
	}

	private void RefreshBookmarksPanel()
	{
		BookmarksContainer.Children.Clear();
		if (PageCount <= 0 || _bookmarkedPages.Count == 0)
		{
			BookmarksContainer.Children.Add(CreateEmptyPanelMessage("No bookmarks"));
			UpdateBookmarkControlsState();
			return;
		}

		foreach (int bookmarkedPage in _bookmarkedPages.OrderBy(page => page))
		{
			BookmarksContainer.Children.Add(CreateBookmarkRow(bookmarkedPage, bookmarkedPage == SelectedPageNumber));
		}

		UpdateBookmarkControlsState();
	}

	private void UpdateBookmarkControlsState()
	{
		if (BookmarkCurrentPageBtn != null)
		{
			BookmarkCurrentPageBtn.Content = (_bookmarkedPages.Contains(SelectedPageNumber) ? "Remove bookmark" : "Bookmark");
		}

		if (ClearBookmarksBtn != null)
		{
			ClearBookmarksBtn.IsEnabled = _bookmarkedPages.Count > 0;
		}
	}

	private UIElement CreateEmptyPanelMessage(string message)
	{
		return new TextBlock
		{
			Text = message,
			Foreground = Brushes.SlateGray,
			FontSize = 11.0,
			Margin = new Thickness(4.0, 0.0, 4.0, 8.0),
			TextWrapping = TextWrapping.Wrap
		};
	}

	private System.Windows.Controls.Button CreatePageJumpButton(string label, int pageNumber, bool selected)
	{
		System.Windows.Controls.Button button = new System.Windows.Controls.Button
		{
			Content = label,
			Margin = new Thickness(0.0, 0.0, 0.0, 4.0),
			Padding = new Thickness(8.0, 5.0, 8.0, 5.0),
			HorizontalAlignment = HorizontalAlignment.Stretch,
			HorizontalContentAlignment = HorizontalAlignment.Left,
			Background = (selected ? new SolidColorBrush(Color.FromRgb(15, 118, 110)) : new SolidColorBrush(Color.FromRgb(30, 41, 59))),
			Foreground = Brushes.White,
			BorderBrush = (selected ? new SolidColorBrush(Color.FromRgb(45, 212, 191)) : new SolidColorBrush(Color.FromRgb(51, 65, 85))),
			BorderThickness = new Thickness(1.0),
			ToolTip = $"Go to page {pageNumber}"
		};
		button.Click += delegate
		{
			GoToPage(pageNumber);
		};
		return button;
	}

	private UIElement CreateBookmarkRow(int pageNumber, bool selected)
	{
		DockPanel row = new DockPanel
		{
			LastChildFill = true,
			Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
		};

		System.Windows.Controls.Button removeButton = new System.Windows.Controls.Button
		{
			Content = "X",
			Width = 28.0,
			Padding = new Thickness(0.0),
			Margin = new Thickness(6.0, 0.0, 0.0, 0.0),
			Background = new SolidColorBrush(Color.FromRgb(69, 26, 26)),
			Foreground = Brushes.White,
			BorderBrush = new SolidColorBrush(Color.FromRgb(127, 29, 29)),
			BorderThickness = new Thickness(1.0),
			ToolTip = $"Remove bookmark on page {pageNumber}"
		};
		removeButton.Click += delegate
		{
			_bookmarkedPages.Remove(pageNumber);
			RefreshBookmarksPanel();
			SaveNavigationState();
		};
		DockPanel.SetDock(removeButton, Dock.Right);
		row.Children.Add(removeButton);
		row.Children.Add(CreatePageJumpButton($"Page {pageNumber}", pageNumber, selected));
		return row;
	}

	private void UpdateThumbnailSelectionVisuals()
	{
		foreach (object child in ThumbnailContainer.Children)
		{
			if (child is Border { Tag: var tag } border && tag is int num)
			{
				bool isActive = num == SelectedPageNumber;
				bool isSelected = _selectedPages.Contains(num);
				border.BorderBrush = (isActive ? Brushes.DeepSkyBlue : isSelected ? new SolidColorBrush(Color.FromRgb(20, 184, 166)) : Brushes.Gray);
				border.BorderThickness = (isActive ? new Thickness(3.0) : isSelected ? new Thickness(2.0) : new Thickness(1.0));
				border.Background = (isSelected ? new SolidColorBrush(Color.FromRgb(220, 252, 231)) : Brushes.White);
			}
		}
	}

	private void QueuePageRender(int pageNumber, int priority, int renderGeneration)
	{
		if (renderGeneration == _renderGeneration && pageNumber >= 1 && pageNumber <= PageCount && !_zoomPreviewActive)
		{
			EnqueueRenderRequest(pageNumber, isThumbnail: false, priority, renderGeneration);
		}
	}

	private bool IsHighCostRenderZoom()
	{
		if (_pageDimensions.Count == 0)
		{
			return false;
		}
		int index = Math.Clamp(SelectedPageNumber - 1, 0, _pageDimensions.Count - 1);
		Size size = _pageDimensions[index];
		return (long)Math.Max(1, (int)(size.Width * 1.33 * CurrentZoom)) * (long)Math.Max(1, (int)(size.Height * 1.33 * CurrentZoom)) >= 12000000;
	}

	private void QueueThumbnailRender(int pageNumber, int priority, int renderGeneration)
	{
		if (renderGeneration == _renderGeneration && pageNumber >= 1 && pageNumber <= PageCount && _isSidebarVisible && !IsHighCostRenderZoom() && !_zoomPreviewActive)
		{
			EnqueueRenderRequest(pageNumber, isThumbnail: true, priority, renderGeneration);
		}
	}

	private void EnqueueRenderRequest(int pageNumber, bool isThumbnail, int priority, int renderGeneration)
	{
		string text = (isThumbnail ? $"thumb:{pageNumber}" : $"page:{pageNumber}");
		lock (_renderQueue)
		{
			if (!_renderQueueKeys.Add(text))
			{
				return;
			}
			_renderQueue.Enqueue(new RenderQueueItem(pageNumber, isThumbnail, renderGeneration, text, priority), (priority, _renderQueueSequence++));
			if (_renderQueueWorkerRunning)
			{
				return;
			}
			_renderQueueWorkerRunning = true;
		}
		ProcessRenderQueueAsync();
	}

	private async Task ProcessRenderQueueAsync()
	{
		_ = 2;
		try
		{
			while (true)
			{
				RenderQueueItem renderQueueItem;
				lock (_renderQueue)
				{
					if (_renderQueue.Count == 0)
					{
						_renderQueueWorkerRunning = false;
						break;
					}
					renderQueueItem = _renderQueue.Dequeue();
					_renderQueueKeys.Remove(renderQueueItem.Key);
				}
				if (renderQueueItem.Generation != _renderGeneration)
				{
					continue;
				}
				if (renderQueueItem.IsThumbnail)
				{
					PdfPerfLogger.Log($"Render queue thumb: page={renderQueueItem.PageNumber} priority={renderQueueItem.Priority}");
					Border thumbnailBorder = GetThumbnailBorder(renderQueueItem.PageNumber);
					if (thumbnailBorder != null)
					{
						await LoadThumbnailContent(renderQueueItem.PageNumber, thumbnailBorder, renderQueueItem.Generation);
					}
				}
				else
				{
					PdfPerfLogger.Log($"Render queue page: page={renderQueueItem.PageNumber} priority={renderQueueItem.Priority}");
					Border pageBorder = GetPageBorder(renderQueueItem.PageNumber);
					if (pageBorder != null)
					{
						await LoadPageContent(renderQueueItem.PageNumber, pageBorder, renderQueueItem.Generation);
					}
				}
				await Task.Yield();
			}
		}
		finally
		{
			lock (_renderQueue)
			{
				_renderQueueWorkerRunning = false;
			}
		}
	}

	private void ClearRenderQueue()
	{
		lock (_renderQueue)
		{
			_renderQueue.Clear();
			_renderQueueKeys.Clear();
			_renderQueueSequence = 0L;
		}
	}

	private void UnloadDistantPageContent()
	{
		if (PagesHost.Children.Count == 0 || !(PagesHost.Children[0] is StackPanel stackPanel))
		{
			return;
		}
		foreach (UIElement child in stackPanel.Children)
		{
			if (child is Border { Tag: var tag } border && tag is int num && Math.Abs(num - SelectedPageNumber) > 1 && border.Child is Grid grid)
			{
				Image image = grid.Children.OfType<Image>().FirstOrDefault();
				if (image != null)
				{
					image.Source = null;
				}
			}
		}
	}

	private Border? GetPageBorder(int pageNumber)
	{
		if (PagesHost.Children.Count == 0 || !(PagesHost.Children[0] is StackPanel stackPanel))
		{
			return null;
		}
		foreach (UIElement child in stackPanel.Children)
		{
			if (child is Border { Tag: var tag } border && tag is int num && num == pageNumber)
			{
				return border;
			}
		}
		return null;
	}

	private Border? GetThumbnailBorder(int pageNumber)
	{
		foreach (UIElement child in ThumbnailContainer.Children)
		{
			if (child is Border { Tag: var tag } border && tag is int num && num == pageNumber)
			{
				return border;
			}
		}
		return null;
	}

	private static IEnumerable<int> GetProgressivePageOrder(int selectedPage, int pageCount)
	{
		selectedPage = Math.Clamp(selectedPage, 1, Math.Max(1, pageCount));
		yield return selectedPage;
		for (int distance = 1; distance < pageCount; distance++)
		{
			int num = selectedPage - distance;
			int next = selectedPage + distance;
			if (num >= 1)
			{
				yield return num;
			}
			if (next <= pageCount)
			{
				yield return next;
			}
		}
	}

	private async Task StartProgressivePagePrefetchAsync(int renderGeneration, int selectedPage)
	{
		await Task.Delay(IsHighCostRenderZoom() ? 450 : 150);
		int pageCount = PageCount;
		int priority = 10;
		int maxDistance = (IsHighCostRenderZoom() ? 1 : 2);
		foreach (int item in GetProgressivePageOrder(selectedPage, pageCount))
		{
			if (renderGeneration != _renderGeneration)
			{
				return;
			}
			if (Math.Abs(item - selectedPage) <= maxDistance)
			{
				QueuePageRender(item, priority++, renderGeneration);
				await Task.Yield();
			}
		}
	}

	private async Task StartDeferredThumbnailLoadAsync(int renderGeneration)
	{
		await Task.Delay(900);
		if (renderGeneration != _renderGeneration)
		{
			return;
		}
		if (!_isSidebarVisible)
		{
			PdfPerfLogger.Log("Deferred thumbnail load skipped: sidebar hidden.");
			return;
		}
		if (IsHighCostRenderZoom() || _zoomPreviewActive)
		{
			PdfPerfLogger.Log("Deferred thumbnail load skipped during high-cost zoom render.");
			return;
		}
		_thumbnailLoadDeferred = false;
		UpdateSelectedPageFromViewport();
		int pageCount = PageCount;
		int priority = 100;
		foreach (int item in GetProgressivePageOrder(SelectedPageNumber, pageCount))
		{
			if (renderGeneration != _renderGeneration)
			{
				return;
			}
			QueueThumbnailRender(item, priority++, renderGeneration);
			await Task.Yield();
		}
	}

	public void ApplyStylesToActiveAnnotation(string fontFamily, double fontSize, bool bold, bool italic, bool underline, Color stroke, Color bg, double opacity)
	{
		ActiveFontFamily = fontFamily;
		ActiveFontSize = fontSize;
		ActiveIsBold = bold;
		ActiveIsItalic = italic;
		ActiveIsUnderline = underline;
		ActiveStrokeColor = stroke;
		ActiveBgColor = bg;
		ActiveOpacity = opacity;
		if (SelectedAnnotation != null)
		{
			SelectedAnnotation.FontFamily = fontFamily;
			SelectedAnnotation.FontSize = fontSize;
			SelectedAnnotation.IsBold = bold;
			SelectedAnnotation.IsItalic = italic;
			SelectedAnnotation.IsUnderline = underline;
			SelectedAnnotation.StrokeColor = stroke;
			SelectedAnnotation.BgColor = bg;
			SelectedAnnotation.Opacity = opacity;
			RedrawAllPageAnnotations();
		}
	}

	public void HandleDeleteKey()
	{
		if (SelectedAnnotation != null)
		{
			Annotations.Remove(SelectedAnnotation);
			SelectedAnnotation = null;
			RedrawAllPageAnnotations();
		}
	}

	private void RedrawAllPageAnnotations()
	{
		if (PagesHost.Children.Count == 0 || !(PagesHost.Children[0] is StackPanel stackPanel))
		{
			return;
		}
		foreach (UIElement child in stackPanel.Children)
		{
			if (child is Border { Tag: var tag } border && tag is int pageNumber && border.Child is Grid grid)
			{
				Canvas canvas = grid.Children.OfType<Canvas>().FirstOrDefault();
				if (canvas != null)
				{
					RedrawPageAnnotations(canvas, pageNumber);
				}
			}
		}
	}

	private void RedrawPageAnnotations(Canvas canvas, int pageNumber)
	{
		canvas.Children.Clear();
		int actualPageIndex = pageNumber - 1;
		foreach (PdfAnnotation item in Annotations.Where((PdfAnnotation a) => a.PageIndex == actualPageIndex).ToList())
		{
			if (item is PdfTextBoxAnnotation pdfTextBoxAnnotation)
			{
				double num = pdfTextBoxAnnotation.Width * canvas.Width;
				double num2 = pdfTextBoxAnnotation.Height * canvas.Height;
				double num3 = pdfTextBoxAnnotation.X * canvas.Width;
				double num4 = pdfTextBoxAnnotation.Y * canvas.Height;
				Border border = new Border
				{
					Width = num,
					Height = num2,
					BorderBrush = new SolidColorBrush(pdfTextBoxAnnotation.StrokeColor),
					BorderThickness = new Thickness(1.5),
					Background = ((pdfTextBoxAnnotation.BgColor == Colors.Transparent) ? Brushes.Transparent : new SolidColorBrush(pdfTextBoxAnnotation.BgColor)),
					Opacity = pdfTextBoxAnnotation.Opacity,
					Tag = pdfTextBoxAnnotation
				};
				TextBlock child = new TextBlock
				{
					Text = pdfTextBoxAnnotation.Text,
					FontFamily = new FontFamily(pdfTextBoxAnnotation.FontFamily),
					FontSize = pdfTextBoxAnnotation.FontSize,
					FontWeight = (pdfTextBoxAnnotation.IsBold ? FontWeights.Bold : FontWeights.Normal),
					FontStyle = (pdfTextBoxAnnotation.IsItalic ? FontStyles.Italic : FontStyles.Normal),
					TextDecorations = (pdfTextBoxAnnotation.IsUnderline ? TextDecorations.Underline : null),
					Foreground = new SolidColorBrush(pdfTextBoxAnnotation.StrokeColor),
					TextWrapping = TextWrapping.Wrap,
					Padding = new Thickness(4.0)
				};
				border.Child = child;
				Canvas.SetLeft(border, num3);
				Canvas.SetTop(border, num4);
				canvas.Children.Add(border);
				if (item is PdfCalloutAnnotation pdfCalloutAnnotation)
				{
					double num5 = pdfCalloutAnnotation.ArrowX * canvas.Width;
					double num6 = pdfCalloutAnnotation.ArrowY * canvas.Height;
					Point point = new Point(num5, num6);
					new Point(num3 + num / 2.0, num4 + num2 / 2.0);
					Point target = FindBoxIntersection(point, new Rect(num3, num4, num, num2));
					Line element = new Line
					{
						X1 = point.X,
						Y1 = point.Y,
						X2 = target.X,
						Y2 = target.Y,
						Stroke = new SolidColorBrush(pdfCalloutAnnotation.StrokeColor),
						StrokeThickness = 2.0,
						Tag = pdfCalloutAnnotation
					};
					canvas.Children.Add(element);
					DrawArrowHeadOnCanvas(canvas, point, target, new SolidColorBrush(pdfCalloutAnnotation.StrokeColor));
					if (item == SelectedAnnotation)
					{
						Ellipse element2 = new Ellipse
						{
							Width = 8.0,
							Height = 8.0,
							Fill = Brushes.White,
							Stroke = Brushes.DodgerBlue,
							StrokeThickness = 2.0,
							Cursor = Cursors.Hand,
							Tag = "ArrowHandle"
						};
						Canvas.SetLeft(element2, num5 - 4.0);
						Canvas.SetTop(element2, num6 - 4.0);
						canvas.Children.Add(element2);
					}
				}
				if (item == SelectedAnnotation)
				{
					Border element3 = new Border
					{
						Width = num + 4.0,
						Height = num2 + 4.0,
						BorderBrush = Brushes.DodgerBlue,
						BorderThickness = new Thickness(1.5),
						Background = Brushes.Transparent,
						IsHitTestVisible = false
					};
					Canvas.SetLeft(element3, num3 - 2.0);
					Canvas.SetTop(element3, num4 - 2.0);
					canvas.Children.Add(element3);
					Border element4 = new Border
					{
						Width = 10.0,
						Height = 10.0,
						Background = Brushes.DodgerBlue,
						BorderBrush = Brushes.White,
						BorderThickness = new Thickness(1.0),
						Cursor = Cursors.SizeNWSE,
						Tag = "ResizeHandle"
					};
					Canvas.SetLeft(element4, num3 + num - 5.0);
					Canvas.SetTop(element4, num4 + num2 - 5.0);
					canvas.Children.Add(element4);
				}
			}
			else if (item is PdfInkAnnotation pdfInkAnnotation)
			{
				if (string.IsNullOrEmpty(pdfInkAnnotation.Points))
				{
					continue;
				}
				Polyline polyline = new Polyline
				{
					Stroke = new SolidColorBrush(pdfInkAnnotation.StrokeColor),
					StrokeThickness = pdfInkAnnotation.Thickness,
					Opacity = pdfInkAnnotation.Opacity,
					Tag = pdfInkAnnotation
				};
				string[] array = pdfInkAnnotation.Points.Split(';', StringSplitOptions.RemoveEmptyEntries);
				for (int num7 = 0; num7 < array.Length; num7++)
				{
					string[] array2 = array[num7].Split(',');
					if (array2.Length == 2 && double.TryParse(array2[0], out var result) && double.TryParse(array2[1], out var result2))
					{
						polyline.Points.Add(new Point(result * canvas.Width, result2 * canvas.Height));
					}
				}
				canvas.Children.Add(polyline);
				if (item == SelectedAnnotation && polyline.Points.Count > 0)
				{
					double num8 = polyline.Points.Min((Point p) => p.X);
					double num9 = polyline.Points.Max((Point p) => p.X);
					double num10 = polyline.Points.Min((Point p) => p.Y);
					double num11 = polyline.Points.Max((Point p) => p.Y);
					Border element5 = new Border
					{
						Width = Math.Max(10.0, num9 - num8 + 4.0),
						Height = Math.Max(10.0, num11 - num10 + 4.0),
						BorderBrush = Brushes.DodgerBlue,
						BorderThickness = new Thickness(1.5),
						Background = Brushes.Transparent,
						IsHitTestVisible = false
					};
					Canvas.SetLeft(element5, num8 - 2.0);
					Canvas.SetTop(element5, num10 - 2.0);
					canvas.Children.Add(element5);
				}
			}
			else if (item is PdfShapeAnnotation pdfShapeAnnotation)
			{
				double num12 = pdfShapeAnnotation.Width * canvas.Width;
				double num13 = pdfShapeAnnotation.Height * canvas.Height;
				double num14 = pdfShapeAnnotation.X * canvas.Width;
				double num15 = pdfShapeAnnotation.Y * canvas.Height;
				Shape shape = null;
				if (pdfShapeAnnotation.Type == ShapeType.Rectangle)
				{
					shape = new Rectangle
					{
						Width = num12,
						Height = num13,
						Stroke = new SolidColorBrush(pdfShapeAnnotation.StrokeColor),
						StrokeThickness = pdfShapeAnnotation.Thickness,
						Fill = ((pdfShapeAnnotation.BgColor == Colors.Transparent) ? Brushes.Transparent : new SolidColorBrush(pdfShapeAnnotation.BgColor)),
						Opacity = pdfShapeAnnotation.Opacity,
						Tag = pdfShapeAnnotation
					};
					Canvas.SetLeft(shape, num14);
					Canvas.SetTop(shape, num15);
				}
				else if (pdfShapeAnnotation.Type == ShapeType.Oval)
				{
					shape = new Ellipse
					{
						Width = num12,
						Height = num13,
						Stroke = new SolidColorBrush(pdfShapeAnnotation.StrokeColor),
						StrokeThickness = pdfShapeAnnotation.Thickness,
						Fill = ((pdfShapeAnnotation.BgColor == Colors.Transparent) ? Brushes.Transparent : new SolidColorBrush(pdfShapeAnnotation.BgColor)),
						Opacity = pdfShapeAnnotation.Opacity,
						Tag = pdfShapeAnnotation
					};
					Canvas.SetLeft(shape, num14);
					Canvas.SetTop(shape, num15);
				}
				else if (pdfShapeAnnotation.Type == ShapeType.Line)
				{
					double x = pdfShapeAnnotation.EndX * canvas.Width;
					double y = pdfShapeAnnotation.EndY * canvas.Height;
					shape = new Line
					{
						X1 = num14,
						Y1 = num15,
						X2 = x,
						Y2 = y,
						Stroke = new SolidColorBrush(pdfShapeAnnotation.StrokeColor),
						StrokeThickness = pdfShapeAnnotation.Thickness,
						Opacity = pdfShapeAnnotation.Opacity,
						Tag = pdfShapeAnnotation
					};
				}
				if (shape == null)
				{
					continue;
				}
				canvas.Children.Add(shape);
				if (item == SelectedAnnotation)
				{
					if (pdfShapeAnnotation.Type == ShapeType.Line)
					{
						double num16 = pdfShapeAnnotation.EndX * canvas.Width;
						double num17 = pdfShapeAnnotation.EndY * canvas.Height;
						Ellipse element6 = new Ellipse
						{
							Width = 8.0,
							Height = 8.0,
							Fill = Brushes.White,
							Stroke = Brushes.DodgerBlue,
							StrokeThickness = 2.0,
							Cursor = Cursors.Hand,
							Tag = "LineStartHandle"
						};
						Canvas.SetLeft(element6, num14 - 4.0);
						Canvas.SetTop(element6, num15 - 4.0);
						canvas.Children.Add(element6);
						Ellipse element7 = new Ellipse
						{
							Width = 8.0,
							Height = 8.0,
							Fill = Brushes.White,
							Stroke = Brushes.DodgerBlue,
							StrokeThickness = 2.0,
							Cursor = Cursors.Hand,
							Tag = "LineEndHandle"
						};
						Canvas.SetLeft(element7, num16 - 4.0);
						Canvas.SetTop(element7, num17 - 4.0);
						canvas.Children.Add(element7);
					}
					else
					{
						Border element8 = new Border
						{
							Width = num12 + 4.0,
							Height = num13 + 4.0,
							BorderBrush = Brushes.DodgerBlue,
							BorderThickness = new Thickness(1.5),
							Background = Brushes.Transparent,
							IsHitTestVisible = false
						};
						Canvas.SetLeft(element8, num14 - 2.0);
						Canvas.SetTop(element8, num15 - 2.0);
						canvas.Children.Add(element8);
						Border element9 = new Border
						{
							Width = 10.0,
							Height = 10.0,
							Background = Brushes.DodgerBlue,
							BorderBrush = Brushes.White,
							BorderThickness = new Thickness(1.0),
							Cursor = Cursors.SizeNWSE,
							Tag = "ResizeHandle"
						};
						Canvas.SetLeft(element9, num14 + num12 - 5.0);
						Canvas.SetTop(element9, num15 + num13 - 5.0);
						canvas.Children.Add(element9);
					}
				}
			}
			else if (item is PdfStickyNoteAnnotation pdfStickyNoteAnnotation)
			{
				double num18 = pdfStickyNoteAnnotation.X * canvas.Width;
				double num19 = pdfStickyNoteAnnotation.Y * canvas.Height;
				double num20 = 28.0;
				Border border2 = new Border
				{
					Width = num20,
					Height = num20,
					Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(pdfStickyNoteAnnotation.ColorHex)),
					BorderBrush = Brushes.Goldenrod,
					BorderThickness = new Thickness(1.5),
					CornerRadius = new CornerRadius(4.0),
					Cursor = Cursors.Hand,
					ToolTip = new ToolTip
					{
						Content = pdfStickyNoteAnnotation.NoteText
					},
					Opacity = pdfStickyNoteAnnotation.Opacity,
					Tag = pdfStickyNoteAnnotation
				};
				TextBlock child2 = new TextBlock
				{
					Text = "\ud83d\udcdd",
					FontSize = 14.0,
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center
				};
				border2.Child = child2;
				Canvas.SetLeft(border2, num18);
				Canvas.SetTop(border2, num19);
				canvas.Children.Add(border2);
				if (item == SelectedAnnotation)
				{
					Border element10 = new Border
					{
						Width = num20 + 4.0,
						Height = num20 + 4.0,
						BorderBrush = Brushes.DodgerBlue,
						BorderThickness = new Thickness(1.5),
						Background = Brushes.Transparent,
						IsHitTestVisible = false
					};
					Canvas.SetLeft(element10, num18 - 2.0);
					Canvas.SetTop(element10, num19 - 2.0);
					canvas.Children.Add(element10);
				}
			}
			else if (item is PdfMeasurementAnnotation pdfMeasurementAnnotation)
			{
				if (pdfMeasurementAnnotation.Points.Count >= 2)
				{
					if (pdfMeasurementAnnotation.MeasurementType == "Distance")
					{
						Point p1 = new Point(pdfMeasurementAnnotation.Points[0].X * canvas.Width, pdfMeasurementAnnotation.Points[0].Y * canvas.Height);
						Point p2 = new Point(pdfMeasurementAnnotation.Points[1].X * canvas.Width, pdfMeasurementAnnotation.Points[1].Y * canvas.Height);

						Line line = new Line
						{
							X1 = p1.X,
							Y1 = p1.Y,
							X2 = p2.X,
							Y2 = p2.Y,
							Stroke = new SolidColorBrush(pdfMeasurementAnnotation.StrokeColor),
							StrokeThickness = pdfMeasurementAnnotation.Thickness,
							Opacity = pdfMeasurementAnnotation.Opacity,
							Tag = pdfMeasurementAnnotation
						};
						canvas.Children.Add(line);

						DrawEndTick(canvas, p1, p2, pdfMeasurementAnnotation.StrokeColor);
						DrawEndTick(canvas, p2, p1, pdfMeasurementAnnotation.StrokeColor);

						Size pageSize = _pageDimensions[pageNumber - 1];
						double dxPoints = (pdfMeasurementAnnotation.Points[1].X - pdfMeasurementAnnotation.Points[0].X) * pageSize.Width;
						double dyPoints = (pdfMeasurementAnnotation.Points[1].Y - pdfMeasurementAnnotation.Points[0].Y) * pageSize.Height;
						double distPoints = Math.Sqrt(dxPoints * dxPoints + dyPoints * dyPoints);
						double distMm = distPoints * 0.352777;
						double realMeters = (distMm / 1000.0) * pdfMeasurementAnnotation.Scale;

						string labelText = $"{realMeters:F2} m";
						Border labelBorder = CreateMeasurementLabel(labelText, pdfMeasurementAnnotation.StrokeColor);
						Point midPoint = new Point((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0);
						Canvas.SetLeft(labelBorder, midPoint.X - 30.0);
						Canvas.SetTop(labelBorder, midPoint.Y - 12.0);
						canvas.Children.Add(labelBorder);
					}
					else if (pdfMeasurementAnnotation.MeasurementType == "Area" && pdfMeasurementAnnotation.Points.Count >= 3)
					{
						Polygon polygon = new Polygon
						{
							Stroke = new SolidColorBrush(pdfMeasurementAnnotation.StrokeColor),
							StrokeThickness = pdfMeasurementAnnotation.Thickness,
							Fill = new SolidColorBrush(Color.FromArgb(64, pdfMeasurementAnnotation.StrokeColor.R, pdfMeasurementAnnotation.StrokeColor.G, pdfMeasurementAnnotation.StrokeColor.B)),
							Opacity = pdfMeasurementAnnotation.Opacity,
							Tag = pdfMeasurementAnnotation
						};
						foreach (var pt in pdfMeasurementAnnotation.Points)
						{
							polygon.Points.Add(new Point(pt.X * canvas.Width, pt.Y * canvas.Height));
						}
						canvas.Children.Add(polygon);

						Size pageSize = _pageDimensions[pageNumber - 1];
						double areaSqm = CalculatePolygonArea(pdfMeasurementAnnotation.Points, pageSize, pdfMeasurementAnnotation.Scale);

						double cx = 0, cy = 0;
						foreach (var pt in polygon.Points)
						{
							cx += pt.X;
							cy += pt.Y;
						}
						cx /= polygon.Points.Count;
						cy /= polygon.Points.Count;

						string labelText = $"{areaSqm:F2} m²";
						Border labelBorder = CreateMeasurementLabel(labelText, pdfMeasurementAnnotation.StrokeColor);
						Canvas.SetLeft(labelBorder, cx - 40.0);
						Canvas.SetTop(labelBorder, cy - 12.0);
						canvas.Children.Add(labelBorder);
					}
				}

				if (item == SelectedAnnotation && pdfMeasurementAnnotation.Points.Count > 0)
				{
					for (int i = 0; i < pdfMeasurementAnnotation.Points.Count; i++)
					{
						double px = pdfMeasurementAnnotation.Points[i].X * canvas.Width;
						double py = pdfMeasurementAnnotation.Points[i].Y * canvas.Height;

						Ellipse handle = new Ellipse
						{
							Width = 8.0,
							Height = 8.0,
							Fill = Brushes.White,
							Stroke = Brushes.DodgerBlue,
							StrokeThickness = 2.0,
							Cursor = Cursors.Hand,
							Tag = $"MeasureHandle_{i}"
						};
						Canvas.SetLeft(handle, px - 4.0);
						Canvas.SetTop(handle, py - 4.0);
						canvas.Children.Add(handle);
					}
				}
			}
			else if (item is PdfSignatureAnnotation pdfSignatureAnnotation)
			{
				double canvasX = pdfSignatureAnnotation.X * canvas.Width;
				double canvasY = pdfSignatureAnnotation.Y * canvas.Height;
				double canvasW = pdfSignatureAnnotation.Width * canvas.Width;
				double canvasH = pdfSignatureAnnotation.Height * canvas.Height;

				if (pdfSignatureAnnotation.SignatureType == "Handwrite" && pdfSignatureAnnotation.Strokes.Count > 0)
				{
					double scaleX = pdfSignatureAnnotation.OriginalWidth > 0 ? canvasW / pdfSignatureAnnotation.OriginalWidth : 1.0;
					double scaleY = pdfSignatureAnnotation.OriginalHeight > 0 ? canvasH / pdfSignatureAnnotation.OriginalHeight : 1.0;

					Canvas sigCanvas = new Canvas
					{
						Width = canvasW,
						Height = canvasH,
						Background = Brushes.Transparent,
						Opacity = pdfSignatureAnnotation.Opacity,
						Tag = pdfSignatureAnnotation
					};

					foreach (var stroke in pdfSignatureAnnotation.Strokes)
					{
						Polyline polyline = new Polyline
						{
							Stroke = new SolidColorBrush(pdfSignatureAnnotation.StrokeColor),
							StrokeThickness = pdfSignatureAnnotation.Thickness
						};
						foreach (var pt in stroke)
						{
							polyline.Points.Add(new Point(pt.X * scaleX, pt.Y * scaleY));
						}
						sigCanvas.Children.Add(polyline);
					}

					Canvas.SetLeft(sigCanvas, canvasX);
					Canvas.SetTop(sigCanvas, canvasY);
					canvas.Children.Add(sigCanvas);
				}
				else if (pdfSignatureAnnotation.SignatureType == "Stamp")
				{
					Border stampBorder = new Border
					{
						Width = canvasW,
						Height = canvasH,
						BorderBrush = new SolidColorBrush(pdfSignatureAnnotation.StrokeColor),
						BorderThickness = new Thickness(3.0),
						CornerRadius = new CornerRadius(4.0),
						Background = new SolidColorBrush(Color.FromArgb(16, pdfSignatureAnnotation.StrokeColor.R, pdfSignatureAnnotation.StrokeColor.G, pdfSignatureAnnotation.StrokeColor.B)),
						Opacity = pdfSignatureAnnotation.Opacity,
						Tag = pdfSignatureAnnotation,
						RenderTransform = new RotateTransform(-8.0, canvasW / 2.0, canvasH / 2.0)
					};

					Grid stampGrid = new Grid();
					stampGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
					stampGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

					TextBlock textBlock = new TextBlock
					{
						Text = pdfSignatureAnnotation.StampText,
						FontFamily = new FontFamily("Segoe UI"),
						FontSize = 18.0,
						FontWeight = FontWeights.Bold,
						Foreground = new SolidColorBrush(pdfSignatureAnnotation.StrokeColor),
						HorizontalAlignment = HorizontalAlignment.Center,
						VerticalAlignment = VerticalAlignment.Center,
						Margin = new Thickness(5.0)
					};
					stampGrid.Children.Add(textBlock);

					TextBlock dateBlock = new TextBlock
					{
						Text = DateTime.Now.ToString("dd-MM-yyyy"),
						FontFamily = new FontFamily("Segoe UI"),
						FontSize = 9.0,
						Foreground = new SolidColorBrush(pdfSignatureAnnotation.StrokeColor),
						HorizontalAlignment = HorizontalAlignment.Center,
						Margin = new Thickness(0, 0, 0, 4)
					};
					stampGrid.Children.Add(dateBlock);
					Grid.SetRow(dateBlock, 1);

					stampBorder.Child = stampGrid;
					Canvas.SetLeft(stampBorder, canvasX);
					Canvas.SetTop(stampBorder, canvasY);
					canvas.Children.Add(stampBorder);
				}

				if (item == SelectedAnnotation)
				{
					Border selectionBorder = new Border
					{
						Width = canvasW + 4.0,
						Height = canvasH + 4.0,
						BorderBrush = Brushes.DodgerBlue,
						BorderThickness = new Thickness(1.5),
						Background = Brushes.Transparent,
						IsHitTestVisible = false
					};
					Canvas.SetLeft(selectionBorder, canvasX - 2.0);
					Canvas.SetTop(selectionBorder, canvasY - 2.0);
					canvas.Children.Add(selectionBorder);

					Border resizeHandle = new Border
					{
						Width = 10.0,
						Height = 10.0,
						Background = Brushes.DodgerBlue,
						BorderBrush = Brushes.White,
						BorderThickness = new Thickness(1.0),
						Cursor = Cursors.SizeNWSE,
						Tag = "ResizeHandle"
					};
					Canvas.SetLeft(resizeHandle, canvasX + canvasW - 5.0);
					Canvas.SetTop(resizeHandle, canvasY + canvasH - 5.0);
					canvas.Children.Add(resizeHandle);
				}
			}
		}
		DrawTextSelectionHighlights(canvas, pageNumber);
	}

	private void DrawEndTick(Canvas canvas, Point p1, Point p2, Color color)
	{
		Vector v = p2 - p1;
		if (v.Length == 0) return;
		v.Normalize();
		Vector perp = new Vector(-v.Y, v.X) * 6.0;

		Line tick = new Line
		{
			X1 = p1.X - perp.X,
			Y1 = p1.Y - perp.Y,
			X2 = p1.X + perp.X,
			Y2 = p1.Y + perp.Y,
			Stroke = new SolidColorBrush(color),
			StrokeThickness = 1.5
		};
		canvas.Children.Add(tick);
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
		base.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isDark ? "#0B0F19" : "#F8FAFC"));
		if (SidebarBorder != null)
		{
			SidebarBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isDark ? "#0F172A" : "#FFFFFF"));
		}
		if (SidebarHeaderBorder != null)
		{
			SidebarHeaderBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isDark ? "#1E1B4B" : "#F1F5F9"));
		}
		if (SidebarHeaderText != null)
		{
			SidebarHeaderText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isDark ? "#FFFFFF" : "#0F172A"));
		}
		if (CanvasBorder != null)
		{
			CanvasBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isDark ? "#111827" : "#E2E8F0"));
		}
		if (EmptyStateText != null)
		{
			EmptyStateText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isDark ? "#94A3B8" : "#475569"));
		}
	}

	private void LogStatus(string message)
	{
		LastStatusMessage = message;
		this.StatusChanged?.Invoke(this, EventArgs.Empty);
	}

	private void ReportZoomChanged()
	{
		this.ZoomChanged?.Invoke(this, EventArgs.Empty);
	}

	private void ReportPageChanged()
	{
		this.PageChanged?.Invoke(this, EventArgs.Empty);
	}

	public void StartPlaceSignature(List<List<Point>> strokes, double width, double height, Color color)
	{
		_tempSignatureStrokes = strokes;
		_tempSignatureWidth = width;
		_tempSignatureHeight = height;
		_tempSignatureColor = color;
		ActiveTool = "PlaceSignature";
		LogStatus("Nhấp chuột vào trang bất kỳ để dán chữ ký tay của bạn.");
	}

	public void StartPlaceStamp(string stampText)
	{
		_tempStampText = stampText;
		ActiveTool = "PlaceStamp";
		LogStatus($"Nhấp chuột vào trang để đóng dấu '{stampText}'.");
	}
}
