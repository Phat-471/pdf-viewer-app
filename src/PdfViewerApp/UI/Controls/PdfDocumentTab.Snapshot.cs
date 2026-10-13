using System;
using System.Windows;
using System.Windows.Controls;
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
		if (_lastSnapshotOverlay != null) _lastSnapshotCanvas?.Children.Remove(_lastSnapshotOverlay);
		if (_snapshotContrastBorder != null) _lastSnapshotCanvas?.Children.Remove(_snapshotContrastBorder);
		// White underlay keeps the thin black dashed boundary visible on dense linework.
		_snapshotContrastBorder = CreateSnapshotBoundary(canvas, bounds, Brushes.White, 2.5, null);
		_lastSnapshotOverlay = CreateSnapshotBoundary(canvas, bounds, Brushes.Black, 1, new DoubleCollection { 5, 3 });
		_lastSnapshotCanvas = canvas;
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
