using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PdfViewerApp;

public partial class PdfDocumentTab
{
	// Giữ Page Handle sống trong bộ nhớ để Text Handle không bị crash khi click chuột
	private readonly Dictionary<int, nint> _openedPdfPages = new Dictionary<int, nint>();

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
			nint pageHandle = PdfiumEngine.FPDF_LoadPage(_documentHandle, pageNumber - 1);
			if (pageHandle == IntPtr.Zero)
			{
				return IntPtr.Zero;
			}
			value = PdfiumEngine.FPDFText_LoadPage(pageHandle);
			
			// QUAN TRỌNG: Không ClosePage ở đây để giữ text bắt chuột nhạy bén
			_openedPdfPages[pageNumber] = pageHandle;
			_textPages[pageNumber] = value;
			return value;
		}
	}

	public void HighlightSelectedText(string colorHex)
	{
		if (_documentHandle == IntPtr.Zero || _selectionStartIndex == -1 || _selectionEndIndex == -1)
		{
			return;
		}

		lock (PdfiumEngine.SyncRoot)
		{
			bool flag = false;
			int currentPage = (_selectionStartPageIndex != -1) ? _selectionStartPageIndex : SelectedPageNumber;
			
			int num2 = Math.Min(_selectionStartIndex, _selectionEndIndex);
			int num3 = Math.Abs(_selectionStartIndex - _selectionEndIndex) + 1;
			
			nint textPage = GetTextPage(currentPage);
			if (textPage == IntPtr.Zero)
			{
				return;
			}

			List<Rect> list = new List<Rect>();
			for (int j = 0; j < num3; j++)
			{
				int index = num2 + j;
				if (PdfiumEngine.FPDFText_GetCharBox(textPage, index, out var left, out var right, out var bottom, out var top))
				{
					list.Add(new Rect(left, bottom, right - left, top - bottom));
				}
			}

			if (list.Count == 0) return;

			if (!TryGetPageSize(currentPage, out Size pageSize)) return;

			List<List<Rect>> list2 = new List<List<Rect>>();
			foreach (Rect item in list)
			{
				bool flag2 = false;
				foreach (List<Rect> item2 in list2)
				{
					double y = item2[0].Y;
					double height = item2[0].Height;
					if (Math.Abs(item.Y - y) < height * 0.5)
					{
						item2.Add(item);
						flag2 = true;
						break;
					}
				}
				if (!flag2)
				{
					list2.Add(new List<Rect> { item });
				}
			}

			foreach (List<Rect> item3 in list2)
			{
				if (item3.Count != 0)
				{
					double num4 = double.MaxValue;
					double num5 = double.MinValue;
					double num6 = double.MaxValue;
					double num7 = double.MinValue;
					foreach (Rect item4 in item3)
					{
						num4 = Math.Min(num4, item4.X);
						num5 = Math.Max(num5, item4.X + item4.Width);
						num6 = Math.Min(num6, item4.Y);
						num7 = Math.Max(num7, item4.Y + item4.Height);
					}

					PdfHighlightAnnotation pdfHighlightAnnotation = new PdfHighlightAnnotation
					{
						PageIndex = currentPage - 1,
						X = num4 / pageSize.Width,
						Y = (pageSize.Height - num7) / pageSize.Height,
						Width = (num5 - num4) / pageSize.Width,
						Height = (num7 - num6) / pageSize.Height,
						ColorHex = colorHex
					};

					try { SaveUndoState(); } catch {} 
					Annotations.Add(pdfHighlightAnnotation);
					flag = true;
				}
			}

			_selectionStartIndex = -1;
			_selectionEndIndex = -1;
			_selectionStartPageIndex = -1;
			_selectionEndPageIndex = -1;

			if (flag)
			{
				ClearCacheAndRender(); 
			}
		}
	}
}