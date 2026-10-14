using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PdfViewerApp;

public partial class PdfDocumentTab
{
	// Page coordinates survive bitmap replacement, zoom layout and annotation redraw.
	private Rect? _snapshotBounds;
	private int _snapshotPageNumber;
	private Rectangle? _snapshotContrastBorder;
	private Border? _snapshotDragLabel;
	private Canvas? _snapshotDragCanvas;
	private Point _snapshotDragStart;
	private Rect _snapshotDragBounds;
	private readonly List<FrameworkElement> _snapshotHandles = new();
	private bool _editingSnapshot;
	private SnapshotPaperLayout? _lastSnapshotPaperLayout;
	private bool IsDrawingSnapshot => _isDrawing && (ActiveTool == "Snapshot" || ActiveTool == "AiSnapshot");

	private static Point ClampSnapshotPoint(Canvas canvas, Point point) => new Point(
		Math.Clamp(point.X, 0, canvas.Width), Math.Clamp(point.Y, 0, canvas.Height));

	private void BeginSnapshotSelection(Canvas canvas)
	{
		ClearLastSnapshotOverlay();
		_snapshotDragStart = new Point(_drawStartPoint.X / canvas.Width, _drawStartPoint.Y / canvas.Height);
		_snapshotDragBounds = new Rect(_snapshotDragStart, _snapshotDragStart);
		_tempRect = new Rectangle
		{
			Stroke = new SolidColorBrush(Color.FromRgb(47, 47, 239)),
			StrokeThickness = 2.0,
			Fill = new SolidColorBrush(Color.FromArgb(76, 70, 70, 255)),
			IsHitTestVisible = false
		};
		_snapshotDragLabel = new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
			CornerRadius = new CornerRadius(3),
			Padding = new Thickness(8, 4, 8, 4),
			Child = new TextBlock { Foreground = Brushes.White, FontSize = 12 },
			IsHitTestVisible = false,
			Visibility = Visibility.Collapsed
		};
		_snapshotDragCanvas = canvas;
		canvas.Children.Add(_tempRect);
		canvas.Children.Add(_snapshotDragLabel);
		Panel.SetZIndex(_tempRect, 1000);
		Panel.SetZIndex(_snapshotDragLabel, 1001);
	}

	private void UpdateSnapshotSelectionLabel(Canvas canvas, double x, double y, double width, double height)
	{
		_snapshotDragBounds = new Rect(x / canvas.Width, y / canvas.Height, width / canvas.Width, height / canvas.Height);
		if (_snapshotDragLabel?.Child is not TextBlock label) return;
		if (canvas.Tag is int page && TryGetPageSize(page, out Size size))
			label.Text = $"Vùng chọn · {width / canvas.Width * size.Width * 25.4 / 72:0.#} × {height / canvas.Height * size.Height * 25.4 / 72:0.#} mm";
		else label.Text = "Vùng chọn Snapshot";
		_snapshotDragLabel.Visibility = Visibility.Visible;
		_snapshotDragLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
		Canvas.SetLeft(_snapshotDragLabel, Math.Max(0, Math.Min(x, canvas.Width - _snapshotDragLabel.DesiredSize.Width)));
		Canvas.SetTop(_snapshotDragLabel, y >= _snapshotDragLabel.DesiredSize.Height + 4 ? y - _snapshotDragLabel.DesiredSize.Height - 4 : y + 4);
	}

	private void ClearSnapshotDragLabel()
	{
		if (_snapshotDragLabel != null) _snapshotDragCanvas?.Children.Remove(_snapshotDragLabel);
		_snapshotDragLabel = null;
		_snapshotDragCanvas = null;
	}

	private void ClearLastSnapshotOverlay()
	{
		foreach (var handle in _snapshotHandles) _lastSnapshotCanvas?.Children.Remove(handle);
		_snapshotHandles.Clear();
		_editingSnapshot = false;
		if (_lastSnapshotOverlay != null) _lastSnapshotCanvas?.Children.Remove(_lastSnapshotOverlay);
		if (_snapshotContrastBorder != null) _lastSnapshotCanvas?.Children.Remove(_snapshotContrastBorder);
		_lastSnapshotOverlay = null;
		_snapshotContrastBorder = null;
		_lastSnapshotCanvas = null;
		_snapshotBounds = null;
		_snapshotPageNumber = 0;
		ClearSnapshotDragLabel();
	}

	private void KeepSnapshotOverlay(Canvas canvas, Rectangle drawnRect, double x, double y, double width, double height)
	{
		canvas.Children.Remove(drawnRect);
		ClearSnapshotDragLabel();
		if (canvas.Tag is not int page || canvas.Width <= 0 || canvas.Height <= 0 || width < 10 || height < 10) return;
		_snapshotPageNumber = page;
		_snapshotBounds = new Rect(x / canvas.Width, y / canvas.Height, width / canvas.Width, height / canvas.Height);
		DrawSnapshotOverlay(canvas, page);
	}

	private void DrawSnapshotOverlay(Canvas canvas, int page)
	{
		if (IsDrawingSnapshot && ReferenceEquals(_activeCanvas, canvas) && _tempRect != null)
		{
			_tempRect.Width = _snapshotDragBounds.Width * canvas.Width;
			_tempRect.Height = _snapshotDragBounds.Height * canvas.Height;
			Canvas.SetLeft(_tempRect, _snapshotDragBounds.X * canvas.Width);
			Canvas.SetTop(_tempRect, _snapshotDragBounds.Y * canvas.Height);
			if (!canvas.Children.Contains(_tempRect)) canvas.Children.Add(_tempRect);
			if (_snapshotDragLabel != null && !canvas.Children.Contains(_snapshotDragLabel)) canvas.Children.Add(_snapshotDragLabel);
		}
		if (_snapshotBounds is not Rect bounds || page != _snapshotPageNumber) return;
		foreach (var handle in _snapshotHandles) _lastSnapshotCanvas?.Children.Remove(handle);
		_snapshotHandles.Clear();
		if (_lastSnapshotOverlay != null) _lastSnapshotCanvas?.Children.Remove(_lastSnapshotOverlay);
		if (_snapshotContrastBorder != null) _lastSnapshotCanvas?.Children.Remove(_snapshotContrastBorder);
		// White underlay keeps the thin black dashed boundary visible on dense linework.
		_snapshotContrastBorder = CreateSnapshotBoundary(canvas, bounds, Brushes.White, 2.5, null);
		_lastSnapshotOverlay = CreateSnapshotBoundary(canvas, bounds, Brushes.Black, 1, new DoubleCollection { 5, 3 });
		_lastSnapshotCanvas = canvas;
		if (_editingSnapshot) AddSnapshotHandles(canvas);
	}

	private void AddSnapshotHandles(Canvas canvas)
	{
		foreach (string kind in new[] { "Move", "NW", "NE", "SW", "SE" })
		{
			var visual = new FrameworkElementFactory(typeof(Border));
			visual.SetValue(Border.BackgroundProperty, kind == "Move" ? Brushes.Transparent : Brushes.White);
			if (kind != "Move") { visual.SetValue(Border.BorderBrushProperty, Brushes.DodgerBlue); visual.SetValue(Border.BorderThicknessProperty, new Thickness(2)); }
			var thumb = new Thumb { Tag = kind, Width = 12, Height = 12,
				Cursor = kind == "Move" ? Cursors.SizeAll : kind is "NW" or "SE" ? Cursors.SizeNWSE : Cursors.SizeNESW,
				Template = new ControlTemplate(typeof(Thumb)) { VisualTree = visual },
				ToolTip = kind == "Move" ? "Kéo để di chuyển vùng chọn" : "Kéo để đổi kích thước vùng chọn" };
			thumb.DragDelta += (_, e) =>
			{
				if (_snapshotBounds is not Rect b) return;
				double dx = e.HorizontalChange / canvas.Width, dy = e.VerticalChange / canvas.Height;
				if (kind == "Move") b = new Rect(Math.Clamp(b.X + dx, 0, 1 - b.Width), Math.Clamp(b.Y + dy, 0, 1 - b.Height), b.Width, b.Height);
				else
				{
					double l = b.Left, t = b.Top, r = b.Right, bottom = b.Bottom;
					double minW = Math.Min(b.Width, Math.Min(0.01, 10 / canvas.Width)), minH = Math.Min(b.Height, Math.Min(0.01, 10 / canvas.Height));
					if (kind.Contains('W')) l = Math.Clamp(l + dx, 0, r - minW); else r = Math.Clamp(r + dx, l + minW, 1);
					if (kind.Contains('N')) t = Math.Clamp(t + dy, 0, bottom - minH); else bottom = Math.Clamp(bottom + dy, t + minH, 1);
					b = new Rect(l, t, r - l, bottom - t);
				}
				_snapshotBounds = b; PositionSnapshotHandles(canvas); e.Handled = true;
			};
			thumb.DragCompleted += (_, _) => { if (_snapshotBounds is Rect b && TryGetPageSize(_snapshotPageNumber, out Size size)) LogStatus($"Vùng Snapshot: {b.Width * size.Width * 25.4 / 72:0.#} × {b.Height * size.Height * 25.4 / 72:0.#} mm. Chọn Xem trước để tiếp tục."); };
			Panel.SetZIndex(thumb, kind == "Move" ? 1001 : 1002); canvas.Children.Add(thumb); _snapshotHandles.Add(thumb);
		}
		var preview = new Button { Tag = "Preview", Content = "Xem trước / OCR…", Padding = new Thickness(8, 4, 8, 4), Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)), Foreground = Brushes.White };
		preview.Click += (_, e) =>
		{
			if (_snapshotBounds is Rect b) PrintSnapshotSelection(canvas, null, b.X * canvas.Width, b.Y * canvas.Height, b.Width * canvas.Width, b.Height * canvas.Height, _snapshotPageNumber);
			e.Handled = true;
		};
		Panel.SetZIndex(preview, 1003); canvas.Children.Add(preview); _snapshotHandles.Add(preview);
		var sizeLabel = new TextBlock { Tag = "Size", Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)), Padding = new Thickness(6, 3, 6, 3), IsHitTestVisible = false };
		Panel.SetZIndex(sizeLabel, 1003); canvas.Children.Add(sizeLabel); _snapshotHandles.Add(sizeLabel);
		PositionSnapshotHandles(canvas);
	}

	private void PositionSnapshotHandles(Canvas canvas)
	{
		if (_snapshotBounds is not Rect b) return;
		foreach (var edge in new[] { _lastSnapshotOverlay, _snapshotContrastBorder })
			if (edge != null) { edge.Width = b.Width * canvas.Width; edge.Height = b.Height * canvas.Height; Canvas.SetLeft(edge, b.X * canvas.Width); Canvas.SetTop(edge, b.Y * canvas.Height); }
		foreach (var handle in _snapshotHandles)
		{
			string kind = handle.Tag?.ToString() ?? "";
			if (kind == "Move") { handle.Width = b.Width * canvas.Width; handle.Height = b.Height * canvas.Height; Canvas.SetLeft(handle, b.X * canvas.Width); Canvas.SetTop(handle, b.Y * canvas.Height); }
			else if (kind == "Preview") { Canvas.SetLeft(handle, Math.Max(0, Math.Min(b.X * canvas.Width, canvas.Width - 160))); Canvas.SetTop(handle, Math.Max(0, b.Y * canvas.Height - 34)); }
			else if (kind == "Size" && handle is TextBlock label)
			{
				if (TryGetPageSize(_snapshotPageNumber, out Size size)) label.Text = $"{b.Width * size.Width * 25.4 / 72:0.#} × {b.Height * size.Height * 25.4 / 72:0.#} mm";
				Canvas.SetLeft(label, Math.Max(0, Math.Min(b.X * canvas.Width, canvas.Width - 180)));
				Canvas.SetTop(label, Math.Max(0, Math.Min(canvas.Height - 26, b.Bottom * canvas.Height + 6)));
			}
			else { Canvas.SetLeft(handle, (kind.Contains('W') ? b.Left : b.Right) * canvas.Width - 6); Canvas.SetTop(handle, (kind.Contains('N') ? b.Top : b.Bottom) * canvas.Height - 6); }
		}
	}

	private static Rectangle CreateSnapshotBoundary(Canvas canvas, Rect bounds, Brush stroke, double thickness, DoubleCollection? dash)
	{
		var rectangle = new Rectangle
		{
			Width = bounds.Width * canvas.Width,
			Height = bounds.Height * canvas.Height,
			Stroke = stroke,
			StrokeThickness = thickness,
			StrokeDashArray = dash,
			Fill = null,
			IsHitTestVisible = false,
			SnapsToDevicePixels = true
		};
		Canvas.SetLeft(rectangle, bounds.X * canvas.Width);
		Canvas.SetTop(rectangle, bounds.Y * canvas.Height);
		Panel.SetZIndex(rectangle, 1000);
		canvas.Children.Add(rectangle);
		return rectangle;
	}

	public void CancelSnapshotSelection()
	{
		if (_activeCanvas != null && _isDrawing && (ActiveTool == "Snapshot" || ActiveTool == "AiSnapshot"))
			EndMouseInteraction(_activeCanvas);
		ClearLastSnapshotOverlay();
	}

	public string CurrentPageSizeLabel => TryGetPageSize(SelectedPageNumber, out Size size)
		? $"{size.Width * 25.4 / 72:0.#} × {size.Height * 25.4 / 72:0.#} mm" : string.Empty;
}
