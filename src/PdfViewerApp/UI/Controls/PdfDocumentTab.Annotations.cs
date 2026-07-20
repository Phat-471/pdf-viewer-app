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

	public void EnterCalibrateMode()
	{
		ActiveTool = "MeasureCalibrate";
	}

	public void ApplyStylesToActiveAnnotation(string fontFamily, double fontSize, bool bold, bool italic, bool underline, bool strikeout, bool subscript, bool superscript, TextAlignment alignment, Color stroke, Color bg, double opacity)
	{
		ActiveFontFamily = fontFamily;
		ActiveFontSize = fontSize;
		ActiveIsBold = bold;
		ActiveIsItalic = italic;
		ActiveIsUnderline = underline;
		ActiveIsStrikeout = strikeout;
		ActiveIsSubscript = subscript;
		ActiveIsSuperscript = superscript;
		ActiveTextAlignment = alignment;
		ActiveStrokeColor = stroke;
		ActiveBgColor = bg;
		ActiveOpacity = opacity;
		if (SelectedAnnotation != null)
		{
			SaveUndoState();
			SelectedAnnotation.FontFamily = fontFamily;
			SelectedAnnotation.FontSize = fontSize;
			SelectedAnnotation.IsBold = bold;
			SelectedAnnotation.IsItalic = italic;
			SelectedAnnotation.IsUnderline = underline;
			SelectedAnnotation.IsStrikeout = strikeout;
			SelectedAnnotation.IsSubscript = subscript;
			SelectedAnnotation.IsSuperscript = superscript;
			SelectedAnnotation.TextAlignment = alignment;
			SelectedAnnotation.StrokeColor = stroke;
			SelectedAnnotation.BgColor = bg;
			SelectedAnnotation.Opacity = opacity;
			RedrawAllPageAnnotations();
		}
	}

	public void ApplyBulletListToActiveTextBox()
	{
		if (SelectedAnnotation is PdfTextBoxAnnotation tb)
		{
			SaveUndoState();
			string[] lines = tb.Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
			for (int i = 0; i < lines.Length; i++)
			{
				string trimmed = lines[i].TrimStart();
				if (!trimmed.StartsWith("• "))
				{
					lines[i] = "• " + lines[i];
				}
			}
			tb.Text = string.Join(Environment.NewLine, lines);
			RedrawAllPageAnnotations();
		}
	}

	public void ApplyNumberListToActiveTextBox()
	{
		if (SelectedAnnotation is PdfTextBoxAnnotation tb)
		{
			SaveUndoState();
			string[] lines = tb.Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
			int counter = 1;
			for (int i = 0; i < lines.Length; i++)
			{
				string trimmed = lines[i].TrimStart();
				int dotIndex = trimmed.IndexOf(". ");
				bool alreadyNumbered = false;
				if (dotIndex > 0 && int.TryParse(trimmed.Substring(0, dotIndex), out _))
				{
					alreadyNumbered = true;
				}

				if (!alreadyNumbered)
				{
					lines[i] = $"{counter}. " + lines[i];
					counter++;
				}
				else
				{
					lines[i] = $"{counter}. " + trimmed.Substring(dotIndex + 2);
					counter++;
				}
			}
			tb.Text = string.Join(Environment.NewLine, lines);
			RedrawAllPageAnnotations();
		}
	}

	public void HandleDeleteKey()
	{
		if (SelectedAnnotation != null)
		{
			SaveUndoState();
			string groupId = SelectedAnnotation.AnnotationGroupId;
			if (!string.IsNullOrEmpty(groupId))
			{
				Annotations.RemoveAll(annotation => annotation.AnnotationGroupId == groupId);
				_pendingTextEdits.RemoveAll(edit =>
					edit.WhiteoutAnnotation.AnnotationGroupId == groupId ||
					edit.TextAnnotation.AnnotationGroupId == groupId);
			}
			else
			{
				_pendingTextEdits.RemoveAll(edit =>
					ReferenceEquals(edit.WhiteoutAnnotation, SelectedAnnotation) ||
					ReferenceEquals(edit.TextAnnotation, SelectedAnnotation));
				Annotations.Remove(SelectedAnnotation);
			}
			SelectedAnnotation = null;
			RedrawAllPageAnnotations();
		}
	}

	private void SaveUndoState()
	{
		try
		{
			List<PdfAnnotation> snapshot = new List<PdfAnnotation>();
			foreach (var ann in Annotations)
			{
				var cloned = CloneAnnotation(ann);
				if (cloned != null)
				{
					snapshot.Add(cloned);
				}
			}

			_undoStack.Push(snapshot);
			if (_undoStack.Count > 50)
			{
				var tempStack = new Stack<List<PdfAnnotation>>();
				while (_undoStack.Count > 1)
				{
					tempStack.Push(_undoStack.Pop());
				}
				_undoStack.Clear();
				while (tempStack.Count > 0)
				{
					_undoStack.Push(tempStack.Pop());
				}
			}

			_redoStack.Clear();
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Failed to save undo state: {ex}");
		}
	}

	public void Undo()
	{
		if (_undoStack.Count == 0)
		{
			LogStatus("Không có gì để hoàn tác");
			return;
		}

		try
		{
			List<PdfAnnotation> currentSnapshot = new List<PdfAnnotation>();
			foreach (var ann in Annotations)
			{
				var cloned = CloneAnnotation(ann);
				if (cloned != null)
				{
					currentSnapshot.Add(cloned);
				}
			}
			_redoStack.Push(currentSnapshot);

			var previousState = _undoStack.Pop();
			Annotations.Clear();
			Annotations.AddRange(previousState);

			SelectedAnnotation = null;
			RedrawAllPageAnnotations();
			LogStatus("Đã hoàn tác");
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Undo failed: {ex}");
		}
	}

	public void Redo()
	{
		if (_redoStack.Count == 0)
		{
			LogStatus("Không có gì để làm lại");
			return;
		}

		try
		{
			List<PdfAnnotation> currentSnapshot = new List<PdfAnnotation>();
			foreach (var ann in Annotations)
			{
				var cloned = CloneAnnotation(ann);
				if (cloned != null)
				{
					currentSnapshot.Add(cloned);
				}
			}
			_undoStack.Push(currentSnapshot);

			var nextState = _redoStack.Pop();
			Annotations.Clear();
			Annotations.AddRange(nextState);

			SelectedAnnotation = null;
			RedrawAllPageAnnotations();
			LogStatus("Đã làm lại");
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Redo failed: {ex}");
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
				Size pageSize = _pageDimensions[pageNumber - 1];
				Border border = new Border
				{
					Width = num,
					Height = num2,
					BorderBrush = new SolidColorBrush(pdfTextBoxAnnotation.StrokeColor),
					BorderThickness = string.IsNullOrEmpty(pdfTextBoxAnnotation.AnnotationGroupId) ? new Thickness(1.5) : new Thickness(0.0),
					Background = ((pdfTextBoxAnnotation.BgColor == Colors.Transparent) ? Brushes.Transparent : new SolidColorBrush(pdfTextBoxAnnotation.BgColor)),
					Opacity = pdfTextBoxAnnotation.Opacity,
					Tag = pdfTextBoxAnnotation
				};
				TextBlock child = new TextBlock
				{
					Text = pdfTextBoxAnnotation.Text,
					FontFamily = new FontFamily(pdfTextBoxAnnotation.FontFamily),
					FontSize = pdfTextBoxAnnotation.FontSize * (canvas.Height / pageSize.Height),
					FontWeight = (pdfTextBoxAnnotation.IsBold ? FontWeights.Bold : FontWeights.Normal),
					FontStyle = (pdfTextBoxAnnotation.IsItalic ? FontStyles.Italic : FontStyles.Normal),
					Foreground = new SolidColorBrush(pdfTextBoxAnnotation.StrokeColor),
					TextWrapping = string.IsNullOrEmpty(pdfTextBoxAnnotation.AnnotationGroupId) ? TextWrapping.Wrap : TextWrapping.NoWrap,
					Padding = string.IsNullOrEmpty(pdfTextBoxAnnotation.AnnotationGroupId) ? new Thickness(4.0) : new Thickness(0.0),
					TextAlignment = pdfTextBoxAnnotation.TextAlignment,
					VerticalAlignment = string.IsNullOrEmpty(pdfTextBoxAnnotation.AnnotationGroupId) ? VerticalAlignment.Top : VerticalAlignment.Center
				};
				TextDecorationCollection decors = new TextDecorationCollection();
				if (pdfTextBoxAnnotation.IsUnderline) decors.Add(TextDecorations.Underline[0]);
				if (pdfTextBoxAnnotation.IsStrikeout) decors.Add(TextDecorations.Strikethrough[0]);
				child.TextDecorations = decors;
				if (pdfTextBoxAnnotation.IsSubscript)
				{
					child.FontSize = child.FontSize * 0.7;
					child.Margin = new Thickness(0, child.FontSize * 0.3, 0, 0);
				}
				else if (pdfTextBoxAnnotation.IsSuperscript)
				{
					child.FontSize = child.FontSize * 0.7;
					child.Margin = new Thickness(0, 0, 0, child.FontSize * 0.3);
				}
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

					// Draw 8 resize handles (4 corners + 4 midpoints) matching professional PDF editors
					Func<double, double, Cursor, Border> createHandle = (xPos, yPos, cursor) =>
					{
						return new Border
						{
							Width = 8.0,
							Height = 8.0,
							Background = Brushes.White,
							BorderBrush = Brushes.DodgerBlue,
							BorderThickness = new Thickness(1.5),
							Cursor = cursor,
							Tag = "ResizeHandle"
						};
					};

					// Corners
					Border hTopLeft = createHandle(num3, num4, Cursors.SizeNWSE);
					Canvas.SetLeft(hTopLeft, num3 - 4.0);
					Canvas.SetTop(hTopLeft, num4 - 4.0);
					canvas.Children.Add(hTopLeft);

					Border hTopRight = createHandle(num3 + num, num4, Cursors.SizeNESW);
					Canvas.SetLeft(hTopRight, num3 + num - 4.0);
					Canvas.SetTop(hTopRight, num4 - 4.0);
					canvas.Children.Add(hTopRight);

					Border hBottomLeft = createHandle(num3, num4 + num2, Cursors.SizeNESW);
					Canvas.SetLeft(hBottomLeft, num3 - 4.0);
					Canvas.SetTop(hBottomLeft, num4 + num2 - 4.0);
					canvas.Children.Add(hBottomLeft);

					Border hBottomRight = createHandle(num3 + num, num4 + num2, Cursors.SizeNWSE);
					Canvas.SetLeft(hBottomRight, num3 + num - 4.0);
					Canvas.SetTop(hBottomRight, num4 + num2 - 4.0);
					canvas.Children.Add(hBottomRight);

					// Midpoints
					Border hTopMiddle = createHandle(num3 + num / 2.0, num4, Cursors.SizeNS);
					Canvas.SetLeft(hTopMiddle, num3 + num / 2.0 - 4.0);
					Canvas.SetTop(hTopMiddle, num4 - 4.0);
					canvas.Children.Add(hTopMiddle);

					Border hBottomMiddle = createHandle(num3 + num / 2.0, num4 + num2, Cursors.SizeNS);
					Canvas.SetLeft(hBottomMiddle, num3 + num / 2.0 - 4.0);
					Canvas.SetTop(hBottomMiddle, num4 + num2 - 4.0);
					canvas.Children.Add(hBottomMiddle);

					Border hMiddleLeft = createHandle(num3, num4 + num2 / 2.0, Cursors.SizeWE);
					Canvas.SetLeft(hMiddleLeft, num3 - 4.0);
					Canvas.SetTop(hMiddleLeft, num4 + num2 / 2.0 - 4.0);
					canvas.Children.Add(hMiddleLeft);

					Border hMiddleRight = createHandle(num3 + num, num4 + num2 / 2.0, Cursors.SizeWE);
					Canvas.SetLeft(hMiddleRight, num3 + num - 4.0);
					Canvas.SetTop(hMiddleRight, num4 + num2 / 2.0 - 4.0);
					canvas.Children.Add(hMiddleRight);
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
			else if (item is PdfHighlightAnnotation pdfHighlightAnnotation)
			{
				double numH = pdfHighlightAnnotation.Width * canvas.Width;
				double numV = pdfHighlightAnnotation.Height * canvas.Height;
				double numX = pdfHighlightAnnotation.X * canvas.Width;
				double numY = pdfHighlightAnnotation.Y * canvas.Height;
				Color color = Colors.Yellow;
				try
				{
					color = (Color)ColorConverter.ConvertFromString(pdfHighlightAnnotation.ColorHex);
				}
				catch {}
				
				Rectangle rect = new Rectangle
				{
					Width = numH,
					Height = numV,
					// SỬ DỤNG ALPHA THẤP (95) ĐỂ ĐẢM BẢO CHỮ ĐEN PDF KHÔNG BỊ XÁM, GIỮ ĐỘ ĐẬM GỐC CỰC TỐT
					Fill = new SolidColorBrush(Color.FromArgb(95, color.R, color.G, color.B)),
					IsHitTestVisible = true,
					Tag = pdfHighlightAnnotation
				};
				
				// Thiết lập BitmapScaling Mode để chống răng cưa biên khối Highlight
				RenderOptions.SetBitmapScalingMode(rect, BitmapScalingMode.LowQuality);
				
				Canvas.SetLeft(rect, numX);
				Canvas.SetTop(rect, numY);
				canvas.Children.Add(rect);

				if (item == SelectedAnnotation)
				{
					Border borderHighlight = new Border
					{
						Width = numH + 4.0,
						Height = numV + 4.0,
						BorderBrush = Brushes.DodgerBlue,
						BorderThickness = new Thickness(1.5),
						Background = Brushes.Transparent,
						IsHitTestVisible = false
					};
					Canvas.SetLeft(borderHighlight, numX - 2.0);
					Canvas.SetTop(borderHighlight, numY - 2.0);
					canvas.Children.Add(borderHighlight);
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
					else if (pdfMeasurementAnnotation.MeasurementType == "Perimeter" && pdfMeasurementAnnotation.Points.Count >= 2)
					{
						Polyline polyline = new Polyline
						{
							Stroke = new SolidColorBrush(pdfMeasurementAnnotation.StrokeColor),
							StrokeThickness = pdfMeasurementAnnotation.Thickness,
							Opacity = pdfMeasurementAnnotation.Opacity,
							Tag = pdfMeasurementAnnotation
						};
						foreach (var pt in pdfMeasurementAnnotation.Points)
						{
							polyline.Points.Add(new Point(pt.X * canvas.Width, pt.Y * canvas.Height));
						}
						canvas.Children.Add(polyline);

						Size pageSize = _pageDimensions[pageNumber - 1];
						double totalLengthMm = 0.0;
						for (int i = 0; i < pdfMeasurementAnnotation.Points.Count - 1; i++)
						{
							Point pt1 = pdfMeasurementAnnotation.Points[i];
							Point pt2 = pdfMeasurementAnnotation.Points[i + 1];
							double dxPoints = (pt2.X - pt1.X) * pageSize.Width;
							double dyPoints = (pt2.Y - pt1.Y) * pageSize.Height;
							double distPoints = Math.Sqrt(dxPoints * dxPoints + dyPoints * dyPoints);
							double distMm = (distPoints / 72.0) * 25.4;
							totalLengthMm += distMm;
						}
						double realMeters = (totalLengthMm / 1000.0) * pdfMeasurementAnnotation.Scale;

						double cx = 0, cy = 0;
						foreach (var pt in polyline.Points)
						{
							cx += pt.X;
							cy += pt.Y;
						}
						cx /= polyline.Points.Count;
						cy /= polyline.Points.Count;

						string labelText = $"{realMeters:F2} m";
						Border labelBorder = CreateMeasurementLabel(labelText, pdfMeasurementAnnotation.StrokeColor);
						Canvas.SetLeft(labelBorder, cx - 30.0);
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
				else if (pdfSignatureAnnotation.SignatureType == "Image" && !string.IsNullOrEmpty(pdfSignatureAnnotation.ImagePath))
				{
					try
					{
						System.Windows.Controls.Image img = new System.Windows.Controls.Image
						{
							Width = canvasW,
							Height = canvasH,
							Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(pdfSignatureAnnotation.ImagePath)),
							Opacity = pdfSignatureAnnotation.Opacity,
							Tag = pdfSignatureAnnotation
						};
						Canvas.SetLeft(img, canvasX);
						Canvas.SetTop(img, canvasY);
						canvas.Children.Add(img);
					}
					catch {}
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
		DrawEditTextSelectionBorder(canvas, pageNumber);
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

	public void StartPlaceSignature(List<List<Point>> strokes, double width, double height, Color color)
	{
		_tempSignatureStrokes = strokes;
		_tempSignatureWidth = width;
		_tempSignatureHeight = height;
		_tempSignatureColor = color;
		ActiveTool = "PlaceSignature";
		LogStatus("Nhấp chuột vào trang bất kỳ để dán chữ ký tay của bạn.");
	}

	public void StartPlaceImageSignature(string imagePath)
	{
		_tempSignatureImagePath = imagePath;
		ActiveTool = "PlaceImageSignature";
		LogStatus("Nhấp chuột vào trang bất kỳ để chèn chữ ký hình ảnh.");
	}

	public void StartPlaceStamp(string stampText)
	{
		_tempStampText = stampText;
		ActiveTool = "PlaceStamp";
		LogStatus($"Nhấp chuột vào trang để đóng dấu '{stampText}'.");
	}
}
