using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PdfViewerApp;

public partial class PdfDocumentTab
{
	private const int DetailTileSize = 1024;
	private int _detailGeneration;
	private bool _detailWorkerRunning;
	private bool _detailAgainRequested;
	private string _detailViewportKey = string.Empty;
	private sealed record DetailTilePlacement(string Key, Rect Bounds);

	private static Canvas GetDetailCanvas(Grid page)
	{
		var layer = page.Children.OfType<Grid>().FirstOrDefault(g => Equals(g.Tag, "page-detail"));
		if (layer == null)
		{
			layer = new Grid { Tag = "page-detail", IsHitTestVisible = false, ClipToBounds = true };
			layer.Children.Add(new Canvas { IsHitTestVisible = false });
			page.Children.Insert(1, layer);
		}
		return (Canvas)layer.Children[0];
	}

	private static void ResizeDetailImages(Grid grid, double width, double height)
	{
		var layer = grid.Children.OfType<Grid>().FirstOrDefault(g => Equals(g.Tag, "page-detail"));
		if (layer?.Children[0] is not Canvas canvas) return;
		foreach (Image tile in canvas.Children.OfType<Image>())
		{
			if (tile.Tag is not DetailTilePlacement placement) continue;
			Canvas.SetLeft(tile, placement.Bounds.X * width);
			Canvas.SetTop(tile, placement.Bounds.Y * height);
			tile.Width = placement.Bounds.Width * width;
			tile.Height = placement.Bounds.Height * height;
		}
	}

	private static void ClearDetailImages(Grid grid)
	{
		var layer = grid.Children.OfType<Grid>().FirstOrDefault(g => Equals(g.Tag, "page-detail"));
		if (layer?.Children[0] is Canvas canvas) { canvas.Children.Clear(); canvas.Tag = null; }
	}

	private void RequestDetailRefresh()
	{
		if (_zoomPreviewActive || _renderSession == null ||
			GetPageBorder(SelectedPageNumber)?.Child is not Grid grid ||
			grid.Children.OfType<Image>().FirstOrDefault()?.Source is not BitmapSource bitmap) return;
		Image image = grid.Children.OfType<Image>().First();
		Rect visible;
		try
		{
			var inverse = image.TransformToAncestor(DocumentScrollViewer).Inverse;
			if (inverse == null) return;
			visible = inverse.TransformBounds(new Rect(0, 0, DocumentScrollViewer.ViewportWidth, DocumentScrollViewer.ViewportHeight));
			visible.Intersect(new Rect(0, 0, image.Width, image.Height));
		}
		catch (InvalidOperationException) { return; }
		if (visible.IsEmpty || visible.Width < 1 || visible.Height < 1) return;

		DpiScale dpi = VisualTreeHelper.GetDpi(this);
		double density = Math.Max(dpi.DpiScaleX, dpi.DpiScaleY) * (PdfiumEngine.EnhanceThinLines ? 1.25 : 1.0);
		int fullWidth = Math.Max(1, (int)Math.Ceiling(image.Width * density));
		int fullHeight = Math.Max(1, (int)Math.Ceiling(image.Height * density));
		PdfiumEngine.ClampRenderDimensions(ref fullWidth, ref fullHeight, long.MaxValue);
		if (fullWidth <= bitmap.PixelWidth && fullHeight <= bitmap.PixelHeight)
		{
			if (_detailViewportKey.Length > 0) { _detailGeneration++; _detailViewportKey = string.Empty; }
			ClearDetailImages(grid);
			return;
		}

		int left = (int)(visible.Left / image.Width * fullWidth) / DetailTileSize * DetailTileSize;
		int top = (int)(visible.Top / image.Height * fullHeight) / DetailTileSize * DetailTileSize;
		int right = Math.Min(fullWidth, (int)Math.Ceiling(visible.Right / image.Width * fullWidth));
		int bottom = Math.Min(fullHeight, (int)Math.Ceiling(visible.Bottom / image.Height * fullHeight));
		string viewportKey = $"{_renderGeneration}:{SelectedPageNumber}:{fullWidth}x{fullHeight}:{left},{top},{right / DetailTileSize},{bottom / DetailTileSize}:{_isReverseView}";
		if (_detailViewportKey == viewportKey) return;
		_detailViewportKey = viewportKey;
		_detailGeneration++;
		_detailAgainRequested = true;
		if (!_detailWorkerRunning) _ = RefreshDetailLoopAsync();
	}

	private async Task RefreshDetailLoopAsync()
	{
		_detailWorkerRunning = true;
		try
		{
			do
			{
				_detailAgainRequested = false;
				await RenderVisibleDetailsAsync(_detailGeneration);
			} while (_detailAgainRequested && !_zoomPreviewActive);
		}
		catch (Exception ex) { PdfPerfLogger.Log("Detail render failed: " + ex.Message); }
		finally { _detailWorkerRunning = false; }
	}

	private async Task RenderVisibleDetailsAsync(int generation)
	{
		PdfRenderSession? session = _renderSession;
		int pageNumber = SelectedPageNumber;
		bool inverted = _isReverseView;
		if (session == null || GetPageBorder(pageNumber)?.Child is not Grid grid) return;
		Image? image = grid.Children.OfType<Image>().FirstOrDefault();
		if (image?.Source is not BitmapSource || image.Width <= 0 || image.Height <= 0) return;
		var inverse = image.TransformToAncestor(DocumentScrollViewer).Inverse;
		if (inverse == null) return;
		Rect visible = inverse.TransformBounds(new Rect(0, 0, DocumentScrollViewer.ViewportWidth, DocumentScrollViewer.ViewportHeight));
		visible.Intersect(new Rect(0, 0, image.Width, image.Height));
		if (visible.IsEmpty) return;
		DpiScale dpi = VisualTreeHelper.GetDpi(this);
		double density = Math.Max(dpi.DpiScaleX, dpi.DpiScaleY) * (PdfiumEngine.EnhanceThinLines ? 1.25 : 1.0);
		int fullWidth = Math.Max(1, (int)Math.Ceiling(image.Width * density));
		int fullHeight = Math.Max(1, (int)Math.Ceiling(image.Height * density));
		PdfiumEngine.ClampRenderDimensions(ref fullWidth, ref fullHeight, long.MaxValue);
		int left = (int)(visible.Left / image.Width * fullWidth) / DetailTileSize * DetailTileSize;
		int top = (int)(visible.Top / image.Height * fullHeight) / DetailTileSize * DetailTileSize;
		int right = Math.Min(fullWidth, (int)Math.Ceiling(visible.Right / image.Width * fullWidth));
		int bottom = Math.Min(fullHeight, (int)Math.Ceiling(visible.Bottom / image.Height * fullHeight));
		Canvas canvas = GetDetailCanvas(grid);
		string sizeKey = $"{fullWidth}x{fullHeight}:{inverted}";
		if (!Equals(canvas.Tag, sizeKey)) { canvas.Children.Clear(); canvas.Tag = sizeKey; }
		var tiles = new List<(string Key, int X, int Y, int Width, int Height)>();
		for (int y = top; y < bottom; y += DetailTileSize)
			for (int x = left; x < right; x += DetailTileSize)
			{
				int width = Math.Min(DetailTileSize + 2, fullWidth - x);
				int height = Math.Min(DetailTileSize + 2, fullHeight - y);
				tiles.Add(($"tile:{pageNumber}:{fullWidth}x{fullHeight}:{x},{y}:{width}x{height}:{inverted}", x, y, width, height));
			}
		var wanted = tiles.Select(t => t.Key).ToHashSet();
		foreach (Image tile in canvas.Children.OfType<Image>().ToArray())
			if (tile.Tag is DetailTilePlacement placement && !wanted.Contains(placement.Key)) canvas.Children.Remove(tile);
		var existing = canvas.Children.OfType<Image>().Select(t => ((DetailTilePlacement)t.Tag).Key).ToHashSet();
		// Refine the center of the viewport first.
		foreach (var tile in tiles.OrderBy(t => Math.Abs(t.X + t.Width / 2.0 - (left + right) / 2.0) + Math.Abs(t.Y + t.Height / 2.0 - (top + bottom) / 2.0)))
		{
			if (generation != _detailGeneration || _zoomPreviewActive || session != _renderSession) return;
			if (existing.Contains(tile.Key)) continue;
			if (!TryGetCachedBitmap(tile.Key, out BitmapSource? source))
			{
				Stopwatch sw = Stopwatch.StartNew();
				source = await Task.Run(() => session.Render(pageNumber - 1, fullWidth, fullHeight, tile.X, tile.Y, tile.Width, tile.Height, inverted));
				PdfPerfLogger.Log($"Detail tile page={pageNumber} {tile.X},{tile.Y}: {sw.ElapsedMilliseconds} ms");
				if (source != null && session == _renderSession) StoreBitmap(tile.Key, source);
			}
			if (source == null || generation != _detailGeneration || _zoomPreviewActive || session != _renderSession) return;
			var bounds = new Rect((double)tile.X / fullWidth, (double)tile.Y / fullHeight, (double)tile.Width / fullWidth, (double)tile.Height / fullHeight);
			var detailImage = new Image { Source = source, Width = bounds.Width * image.Width, Height = bounds.Height * image.Height,
				Tag = new DetailTilePlacement(tile.Key, bounds), IsHitTestVisible = false, Stretch = Stretch.Fill };
			Canvas.SetLeft(detailImage, bounds.X * image.Width);
			Canvas.SetTop(detailImage, bounds.Y * image.Height);
			RenderOptions.SetBitmapScalingMode(detailImage, BitmapScalingMode.HighQuality);
			canvas.Children.Add(detailImage);
		}
	}
}
