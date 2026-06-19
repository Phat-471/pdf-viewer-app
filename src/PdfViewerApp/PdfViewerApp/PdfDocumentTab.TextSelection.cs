using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace PdfViewerApp;

public partial class PdfDocumentTab
{
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

	public void HighlightSelectedText(string colorHex)
	{
		if (_selectionStartPageIndex == -1 || _selectionEndPageIndex == -1)
		{
			return;
		}

		int startPgIndex = Math.Min(_selectionStartPageIndex, _selectionEndPageIndex);
		int endPgIndex = Math.Max(_selectionStartPageIndex, _selectionEndPageIndex);

		SaveUndoState();

		for (int pageIdx = startPgIndex; pageIdx <= endPgIndex; pageIdx++)
		{
			int pageNumber = pageIdx + 1;
			nint textPage = GetTextPage(pageNumber);
			if (textPage == IntPtr.Zero) continue;

			int charCount;
			lock (PdfiumEngine.SyncRoot)
			{
				charCount = PdfiumEngine.FPDFText_CountChars(textPage);
			}
			if (charCount <= 0) continue;

			int startChar = 0;
			int endChar = charCount - 1;

			if (pageIdx == _selectionStartPageIndex && pageIdx == _selectionEndPageIndex)
			{
				startChar = Math.Min(_selectionStartIndex, _selectionEndIndex);
				endChar = Math.Max(_selectionStartIndex, _selectionEndIndex);
			}
			else if (pageIdx == _selectionStartPageIndex)
			{
				if (_selectionStartPageIndex < _selectionEndPageIndex)
					startChar = _selectionStartIndex;
				else
					endChar = _selectionStartIndex;
			}
			else if (pageIdx == _selectionEndPageIndex)
			{
				if (_selectionStartPageIndex < _selectionEndPageIndex)
					endChar = _selectionEndIndex;
				else
					startChar = _selectionEndIndex;
			}

			if (startChar < 0) startChar = 0;
			if (endChar >= charCount) endChar = charCount - 1;

			var pageRects = new List<Rect>();
			lock (PdfiumEngine.SyncRoot)
			{
				Rect currentRect = Rect.Empty;
				for (int charIdx = startChar; charIdx <= endChar; charIdx++)
				{
					if (PdfiumEngine.FPDFText_GetCharBox(textPage, charIdx, out var left, out var right, out var bottom, out var top))
					{
						double pdfX = Math.Min(left, right);
						double pdfY = Math.Min(bottom, top);
						double pdfW = Math.Abs(right - left);
						double pdfH = Math.Abs(top - bottom);
						Rect charRect = new Rect(pdfX, pdfY, pdfW, pdfH);

						if (charRect.Width <= 0.0) charRect.Width = 6.0;
						if (charRect.Height <= 0.0) charRect.Height = 12.0;

						if (currentRect.IsEmpty)
						{
							currentRect = charRect;
						}
						else
						{
							double verticalDistance = Math.Abs(charRect.Y - currentRect.Y);
							double heightDifference = Math.Abs(charRect.Height - currentRect.Height);
							double horizontalGap = charRect.X - (currentRect.X + currentRect.Width);

							double heightThreshold = Math.Max(currentRect.Height, charRect.Height);
							if (verticalDistance < heightThreshold * 0.4 && 
							    heightDifference < heightThreshold * 0.4 && 
							    horizontalGap >= -2.0 && 
							    horizontalGap < heightThreshold * 3.0)
							{
								double minX = Math.Min(currentRect.X, charRect.X);
								double maxX = Math.Max(charRect.X + charRect.Width, currentRect.X + currentRect.Width);
								double minY = Math.Min(currentRect.Y, charRect.Y);
								double maxY = Math.Max(charRect.Y + charRect.Height, currentRect.Y + currentRect.Height);
								currentRect = new Rect(minX, minY, maxX - minX, maxY - minY);
							}
							else
							{
								pageRects.Add(currentRect);
								currentRect = charRect;
							}
						}
					}
				}
				if (!currentRect.IsEmpty)
				{
					pageRects.Add(currentRect);
				}
			}

			if (!TryGetPageSize(pageNumber, out Size pageSize))
			{
				continue;
			}

			foreach (var rect in pageRects)
			{
				PdfHighlightAnnotation hl = new PdfHighlightAnnotation
				{
					PageIndex = pageIdx,
					X = rect.X / pageSize.Width,
					Y = (pageSize.Height - (rect.Y + rect.Height)) / pageSize.Height,
					Width = rect.Width / pageSize.Width,
					Height = rect.Height / pageSize.Height,
					ColorHex = colorHex,
					StrokeColor = System.Windows.Media.Colors.Yellow,
					Opacity = 0.5
				};

				Annotations.Add(hl);
			}
		}

		ClearAllTextSelectionHighlights();
	}
}