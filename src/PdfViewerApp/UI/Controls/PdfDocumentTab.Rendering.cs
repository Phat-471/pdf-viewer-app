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

public partial class PdfDocumentTab
{

	private string BuildPageCacheKey(int pageNumber, int width, int height, bool isThumbnail)
	{
		string suffix = _isReverseView ? ":dark" : "";
		if (!isThumbnail)
		{
			return $"page:{pageNumber}:{width}x{height}{suffix}";
		}
		return $"thumb:{pageNumber}:{width}x{height}{suffix}";
	}

	private bool TryGetCachedBitmap(string key, out BitmapSource? bitmap)
	{
		return _cacheManager.TryGetCachedBitmap(key, out bitmap);
	}

	private void ClearBitmapCache()
	{
		_cacheManager.Clear();
	}

	private void DrawHorizontalRuler(double pageOriginX)
	{
		double width = HorizontalRuler.ActualWidth;
		if (width <= 0 || PagesHost.ActualWidth <= 0) return;

		double visualScale = 1.33333333 * CurrentZoom;

		// Adaptive tick step
		double pointsPerTick = 100.0;
		double pixelStep = pointsPerTick * visualScale;
		while (pixelStep < 50.0)
		{
			pointsPerTick *= 2.0;
			pixelStep = pointsPerTick * visualScale;
		}
		while (pixelStep > 150.0)
		{
			pointsPerTick /= 2.0;
			pixelStep = pointsPerTick * visualScale;
		}

		double subTickStep = pointsPerTick / 10.0;
		double startPt = -pageOriginX / visualScale;
		double endPt = (width - pageOriginX) / visualScale;
		double startAligned = Math.Floor(startPt / subTickStep) * subTickStep;

		var existingLines = HorizontalRuler.Children.OfType<Line>().ToList();
		var existingTextBlocks = HorizontalRuler.Children.OfType<TextBlock>().ToList();
		int lineIdx = 0;
		int textIdx = 0;

		for (double pt = startAligned; pt <= endPt; pt += subTickStep)
		{
			double x = pageOriginX + pt * visualScale;
			if (x < 0 || x > width) continue;

			bool isMajor = Math.Abs(pt % pointsPerTick) < 0.001 || Math.Abs((pt % pointsPerTick) - pointsPerTick) < 0.001;
			bool isMedium = Math.Abs(pt % (pointsPerTick / 2.0)) < 0.001 || Math.Abs((pt % (pointsPerTick / 2.0)) - (pointsPerTick / 2.0)) < 0.001;

			Line tick;
			if (lineIdx < existingLines.Count)
			{
				tick = existingLines[lineIdx];
				tick.Visibility = Visibility.Visible;
			}
			else
			{
				tick = new Line
				{
					Stroke = RulerBrush,
					StrokeThickness = 1
				};
				HorizontalRuler.Children.Add(tick);
				existingLines.Add(tick);
			}
			lineIdx++;

			tick.X1 = x;
			tick.X2 = x;

			if (isMajor)
			{
				tick.Y1 = 8;
				tick.Y2 = 20;

				TextBlock label;
				if (textIdx < existingTextBlocks.Count)
				{
					label = existingTextBlocks[textIdx];
					label.Visibility = Visibility.Visible;
				}
				else
				{
					label = new TextBlock
					{
						Foreground = RulerBrush,
						FontSize = 8,
						FontWeight = FontWeights.Bold
					};
					HorizontalRuler.Children.Add(label);
					existingTextBlocks.Add(label);
				}
				textIdx++;

				label.Text = Math.Round(pt).ToString();
				Canvas.SetLeft(label, x + 2);
				Canvas.SetTop(label, 0);
			}
			else if (isMedium)
			{
				tick.Y1 = 12;
				tick.Y2 = 20;
			}
			else
			{
				tick.Y1 = 15;
				tick.Y2 = 20;
			}
		}

		for (int i = lineIdx; i < existingLines.Count; i++)
		{
			existingLines[i].Visibility = Visibility.Collapsed;
		}
		for (int i = textIdx; i < existingTextBlocks.Count; i++)
		{
			existingTextBlocks[i].Visibility = Visibility.Collapsed;
		}
	}

	private void DrawVerticalRuler(double pageOriginY)
	{
		double height = VerticalRuler.ActualHeight;
		if (height <= 0 || PagesHost.ActualHeight <= 0) return;

		double visualScale = 1.33333333 * CurrentZoom;

		// Adaptive tick step
		double pointsPerTick = 100.0;
		double pixelStep = pointsPerTick * visualScale;
		while (pixelStep < 50.0)
		{
			pointsPerTick *= 2.0;
			pixelStep = pointsPerTick * visualScale;
		}
		while (pixelStep > 150.0)
		{
			pointsPerTick /= 2.0;
			pixelStep = pointsPerTick * visualScale;
		}

		double subTickStep = pointsPerTick / 10.0;
		double startPt = -pageOriginY / visualScale;
		double endPt = (height - pageOriginY) / visualScale;
		double startAligned = Math.Floor(startPt / subTickStep) * subTickStep;

		var existingLines = VerticalRuler.Children.OfType<Line>().ToList();
		var existingTextBlocks = VerticalRuler.Children.OfType<TextBlock>().ToList();
		int lineIdx = 0;
		int textIdx = 0;

		for (double pt = startAligned; pt <= endPt; pt += subTickStep)
		{
			double y = pageOriginY + pt * visualScale;
			if (y < 0 || y > height) continue;

			bool isMajor = Math.Abs(pt % pointsPerTick) < 0.001 || Math.Abs((pt % pointsPerTick) - pointsPerTick) < 0.001;
			bool isMedium = Math.Abs(pt % (pointsPerTick / 2.0)) < 0.001 || Math.Abs((pt % (pointsPerTick / 2.0)) - (pointsPerTick / 2.0)) < 0.001;

			Line tick;
			if (lineIdx < existingLines.Count)
			{
				tick = existingLines[lineIdx];
				tick.Visibility = Visibility.Visible;
			}
			else
			{
				tick = new Line
				{
					Stroke = RulerBrush,
					StrokeThickness = 1
				};
				VerticalRuler.Children.Add(tick);
				existingLines.Add(tick);
			}
			lineIdx++;

			tick.Y1 = y;
			tick.Y2 = y;

			if (isMajor)
			{
				tick.X1 = 8;
				tick.X2 = 20;

				TextBlock label;
				if (textIdx < existingTextBlocks.Count)
				{
					label = existingTextBlocks[textIdx];
					label.Visibility = Visibility.Visible;
				}
				else
				{
					label = new TextBlock
					{
						Foreground = RulerBrush,
						FontSize = 8,
						FontWeight = FontWeights.Bold,
						LayoutTransform = new RotateTransform(-90)
					};
					VerticalRuler.Children.Add(label);
					existingTextBlocks.Add(label);
				}
				textIdx++;

				label.Text = Math.Round(pt).ToString();
				Canvas.SetLeft(label, 0);
				Canvas.SetTop(label, y + 2);
			}
			else if (isMedium)
			{
				tick.X1 = 12;
				tick.X2 = 20;
			}
			else
			{
				tick.X1 = 15;
				tick.X2 = 20;
			}
		}

		for (int i = lineIdx; i < existingLines.Count; i++)
		{
			existingLines[i].Visibility = Visibility.Collapsed;
		}
		for (int i = textIdx; i < existingTextBlocks.Count; i++)
		{
			existingTextBlocks[i].Visibility = Visibility.Collapsed;
		}
	}

	private void RequestViewportRefresh()
	{
		_viewportTimer.Stop();
		_viewportTimer.Start();
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
				if (_cacheManager.FindAnyCachedBitmapForPage(pageNumber, isThumbnail: false) is BitmapSource placeholder)
				{
					image.Source = placeholder;
				}
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
				
				MessageBoxResult result = MessageBox.Show(
					Application.Current.MainWindow,
					"Tài liệu PDF này dường như bị lỗi cấu trúc hoặc bị hỏng. Bạn có muốn ứng dụng cố gắng tự động sửa chữa và khôi phục tệp tin này không?",
					"Phát hiện tệp lỗi",
					MessageBoxButton.YesNo,
					MessageBoxImage.Warning);
				
				if (result == MessageBoxResult.Yes)
				{
					try
					{
						string dir = System.IO.Path.GetDirectoryName(CurrentPdfPath) ?? "";
						string fileName = System.IO.Path.GetFileNameWithoutExtension(CurrentPdfPath);
						string ext = System.IO.Path.GetExtension(CurrentPdfPath);
						string repairedPath = System.IO.Path.Combine(dir, $"{fileName}_repaired{ext}");
						
						bool success = await Task.Run(() => PdfInterop.PdfCore.repair_pdf(CurrentPdfPath, repairedPath));
						if (success && File.Exists(repairedPath))
						{
							MessageBox.Show(
								Application.Current.MainWindow,
								"Sửa chữa thành công! Đang tải lại tệp tin đã khôi phục.",
								"Thành công",
								MessageBoxButton.OK,
								MessageBoxImage.Information);
							
							LoadDocument(repairedPath);
							return;
						}
						else
						{
							MessageBox.Show(
								Application.Current.MainWindow,
								"Không thể sửa chữa tệp tin này. Cấu trúc tệp bị hỏng quá nặng.",
								"Thất bại",
								MessageBoxButton.OK,
								MessageBoxImage.Error);
						}
					}
					catch (Exception ex)
					{
						MessageBox.Show(
							Application.Current.MainWindow,
							$"Lỗi trong quá trình sửa chữa: {ex.Message}",
							"Lỗi",
							MessageBoxButton.OK,
							MessageBoxImage.Error);
					}
				}

				EmptyStateText.Visibility = Visibility.Visible;
				return;
			}

			int pageCount = 0;
			List<Size> pageDimensions = new List<Size>();
			List<int> inherentRotations = new List<int>();
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
							int rot = PdfInterop.Pdfium.FPDFPage_GetRotation(num) * 90;
							inherentRotations.Add(rot);
							PdfiumEngine.FPDF_ClosePage(num);
						}
						else
						{
							pageDimensions.Add(new Size(800.0, 1100.0));
							inherentRotations.Add(0);
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
			_inherentPageRotations.Clear();
			_inherentPageRotations.AddRange(inherentRotations);
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
			StartProgressivePagePrefetchAsync(_renderGeneration, SelectedPageNumber);
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

	private double GetStableRenderScale(double currentZoom)
	{
		if (currentZoom <= 1.5) return 1.5;
		if (currentZoom <= 3.0) return 3.0;
		return Math.Min(4.0, currentZoom);
	}

	private async Task LoadPageContent(int pageNumber, Border pageBorder, int renderGeneration)
	{
		Grid grid = pageBorder.Child as Grid;
		if (grid == null)
		{
			return;
		}
		Image image = grid.Children.OfType<Image>().FirstOrDefault();
		if (image == null || !_loadingPages.Add(pageNumber))
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
			double stableScale = GetStableRenderScale(currentZoom);
			int renderWidth = Math.Max(1, (int)(width * 1.33 * stableScale));
			int renderHeight = Math.Max(1, (int)(height * 1.33 * stableScale));
			string cacheKey = BuildPageCacheKey(pageNumber, renderWidth, renderHeight, isThumbnail: false);
			if (TryGetCachedBitmap(cacheKey, out BitmapSource bitmap) && bitmap != null)
			{
				PdfPerfLogger.Log($"Page {pageNumber} cache hit ({renderWidth}x{renderHeight})");
				if (renderGeneration == _renderGeneration)
				{
					image.Source = bitmap;
					image.Width = width * 1.33 * currentZoom;
					image.Height = height * 1.33 * currentZoom;
					Canvas canvas = grid.Children.OfType<Canvas>().FirstOrDefault();
					if (canvas != null)
					{
						canvas.Width = width * 1.33 * currentZoom;
						canvas.Height = height * 1.33 * currentZoom;
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
						return PdfiumEngine.RenderPageToBitmap(_documentHandle, pageNumber - 1, renderWidth, renderHeight, _isReverseView);
					}
					return PdfiumEngine.RenderPageToBitmap(pdfPath, pageNumber - 1, renderWidth, renderHeight, _isReverseView);
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
				image.Width = width * 1.33 * currentZoom;
				image.Height = height * 1.33 * currentZoom;
				Canvas canvas2 = grid.Children.OfType<Canvas>().FirstOrDefault();
				if (canvas2 != null)
				{
					canvas2.Width = width * 1.33 * currentZoom;
					canvas2.Height = height * 1.33 * currentZoom;
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
						return PdfiumEngine.RenderPageToBitmap(_documentHandle, pageNumber - 1, thumbWidth, thumbHeight, _isReverseView);
					}
					return PdfiumEngine.RenderPageToBitmap(pdfPath, pageNumber - 1, thumbWidth, thumbHeight, _isReverseView);
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

	private void QueuePageRender(int pageNumber, int priority, int renderGeneration)
	{
		if (renderGeneration == _renderGeneration && pageNumber >= 1 && pageNumber <= PageCount && !_zoomPreviewActive)
		{
			EnqueueRenderRequest(pageNumber, isThumbnail: false, priority, renderGeneration);
		}
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

	private async Task StartProgressivePagePrefetchAsync(int renderGeneration, int selectedPage)
	{
		await Task.Delay(IsHighCostRenderZoom() ? 450 : 150);
		int pageCount = PageCount;
		int priority = 10;
		int maxDistance = 2;
		if (IsHighCostRenderZoom())
		{
			maxDistance = 1;
		}
		else if (MaxBitmapCacheBytes > 536870912L) // > 512MB
		{
			maxDistance = 4;
		}
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
}