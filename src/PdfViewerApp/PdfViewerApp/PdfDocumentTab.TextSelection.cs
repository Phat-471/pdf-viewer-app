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
}