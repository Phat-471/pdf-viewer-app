using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PdfViewerApp
{
	public partial class PdfDocumentTab
	{
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

			// Pre-calculate the anchor in page space based on current zoom before starting the animation
			double currentScale = CurrentZoom / Math.Max(0.0001, _baseZoomForLayout);
			_smoothZoomHostAnchor = GetZoomHostAnchor(_smoothZoomAnchor, currentScale);

			_pendingZoomBaseZoom = _baseZoomForLayout;
			_pendingZoomViewportPoint = _smoothZoomAnchor;
			_pendingZoomHostPoint = _smoothZoomHostAnchor; // Ensure post-render layout scrolling centers on the exact same host point
			_pendingZoomContentPoint = null;
			_resetScrollAfterRender = false;
			ClearRenderQueue();
			_renderGeneration++;
			_loadingPages.Clear();
			_zoomTimer.Stop();
			RenderOptions.SetBitmapScalingMode(PagesHost, System.Windows.Media.BitmapScalingMode.LowQuality);
			SetImagesScalingMode(System.Windows.Media.BitmapScalingMode.LowQuality);
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
			SetImagesScalingMode(BitmapScalingMode.HighQuality);
		}

		private void SetImagesScalingMode(BitmapScalingMode scalingMode)
		{
			if (PagesHost.Children.Count > 0 && PagesHost.Children[0] is StackPanel stackPanel)
			{
				foreach (UIElement child in stackPanel.Children)
				{
					if (child is Border border && border.Child is Grid grid)
					{
						foreach (UIElement gridChild in grid.Children)
						{
							if (gridChild is Image image)
							{
								RenderOptions.SetBitmapScalingMode(image, scalingMode);
							}
						}
					}
				}
			}
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
			else
			{
				e.Handled = true;
				bool scrollHorizontal = (Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.None;

				if (scrollHorizontal)
				{
					if (_targetHorizontalOffset < 0)
					{
						_targetHorizontalOffset = DocumentScrollViewer.HorizontalOffset;
					}
					double step = 150.0 * ((double)e.Delta / 120.0);
					_targetHorizontalOffset = Math.Clamp(_targetHorizontalOffset - step, 0.0, DocumentScrollViewer.ScrollableWidth);
				}
				else
				{
					if (_targetVerticalOffset < 0)
					{
						_targetVerticalOffset = DocumentScrollViewer.VerticalOffset;
					}
					double step = 150.0 * ((double)e.Delta / 120.0);
					_targetVerticalOffset = Math.Clamp(_targetVerticalOffset - step, 0.0, DocumentScrollViewer.ScrollableHeight);
				}

				_smoothScrollTimer?.Start();
			}
		}

		private void SmoothScrollTimer_Tick(object? sender, EventArgs e)
		{
			bool needsMoreVertical = false;
			bool needsMoreHorizontal = false;

			if (_targetVerticalOffset >= 0)
			{
				double current = DocumentScrollViewer.VerticalOffset;
				double delta = _targetVerticalOffset - current;
				if (Math.Abs(delta) < 0.5)
				{
					DocumentScrollViewer.ScrollToVerticalOffset(_targetVerticalOffset);
					_targetVerticalOffset = -1;
				}
				else
				{
					DocumentScrollViewer.ScrollToVerticalOffset(current + delta * 0.2);
					needsMoreVertical = true;
				}
			}

			if (_targetHorizontalOffset >= 0)
			{
				double current = DocumentScrollViewer.HorizontalOffset;
				double delta = _targetHorizontalOffset - current;
				if (Math.Abs(delta) < 0.5)
				{
					DocumentScrollViewer.ScrollToHorizontalOffset(_targetHorizontalOffset);
					_targetHorizontalOffset = -1;
				}
				else
				{
					DocumentScrollViewer.ScrollToHorizontalOffset(current + delta * 0.2);
					needsMoreHorizontal = true;
				}
			}

			if (!needsMoreVertical && !needsMoreHorizontal)
			{
				_smoothScrollTimer?.Stop();
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
			UpdateRulers();
			if (_smoothScrollTimer == null || !_smoothScrollTimer.IsEnabled)
			{
				_targetVerticalOffset = e.VerticalOffset;
				_targetHorizontalOffset = e.HorizontalOffset;
			}
		}

		private void ThumbScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			RequestViewportRefresh();
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
	}
}
