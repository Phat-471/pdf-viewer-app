using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading;

namespace PdfViewerApp;

public static class PdfiumEngine
{
	private const int FPDF_BGRA = 4;

	public const int FPDF_ANNOT = 1;

	private const int FPDF_LCD_TEXT = 2;

	public const int FPDF_PRINTING = 2048;

	private static bool _initialized;

	private static readonly object RenderLock = new object();

	public static object SyncRoot => RenderLock;

	private static bool? _enhanceThinLinesCached = null;
	public static bool EnhanceThinLines
	{
		get
		{
			if (!_enhanceThinLinesCached.HasValue)
			{
				try
				{
					_enhanceThinLinesCached = AppPreferences.Load().EnhanceThinLines;
				}
				catch
				{
					_enhanceThinLinesCached = true;
				}
			}
			return _enhanceThinLinesCached.Value;
		}
		set
		{
			_enhanceThinLinesCached = value;
		}
	}

	private static class Native
	{
		public static void FPDF_InitLibrary() => PdfInterop.Pdfium.FPDF_InitLibrary();
		public static void FPDF_DestroyLibrary() => PdfInterop.Pdfium.FPDF_DestroyLibrary();
		public static nint FPDF_LoadDocument(string file_path, string? password) => PdfInterop.Pdfium.FPDF_LoadDocument(file_path, password);
		public static void FPDF_CloseDocument(nint document) => PdfInterop.Pdfium.FPDF_CloseDocument(document);
		public static int FPDF_GetPageCount(nint document) => PdfInterop.Pdfium.FPDF_GetPageCount(document);
		public static nint FPDF_LoadPage(nint document, int page_index) => PdfInterop.Pdfium.FPDF_LoadPage(document, page_index);
		public static void FPDF_ClosePage(nint page) => PdfInterop.Pdfium.FPDF_ClosePage(page);
		public static double FPDF_GetPageWidth(nint page) => PdfInterop.Pdfium.FPDF_GetPageWidth(page);
		public static double FPDF_GetPageHeight(nint page) => PdfInterop.Pdfium.FPDF_GetPageHeight(page);
		public static int FPDF_GetPageSizeByIndex(nint document, int page_index, out double width, out double height) => PdfInterop.Pdfium.FPDF_GetPageSizeByIndex(document, page_index, out width, out height);
		public static nint FPDFBitmap_CreateEx(int width, int height, int format, nint first_scan, int stride) => PdfInterop.Pdfium.FPDFBitmap_CreateEx(width, height, format, first_scan, stride);
		public static void FPDFBitmap_FillRect(nint bitmap, int left, int top, int width, int height, uint color) => PdfInterop.Pdfium.FPDFBitmap_FillRect(bitmap, left, top, width, height, color);
		public static void FPDF_RenderPageBitmap(nint bitmap, nint page, int start_x, int start_y, int size_x, int size_y, int rotate, int flags) => PdfInterop.Pdfium.FPDF_RenderPageBitmap(bitmap, page, start_x, start_y, size_x, size_y, rotate, flags);
		public static void FPDF_RenderPage(nint dc, nint page, int start_x, int start_y, int size_x, int size_y, int rotate, int flags) => PdfInterop.Pdfium.FPDF_RenderPage(dc, page, start_x, start_y, size_x, size_y, rotate, flags);
		public static void FPDFBitmap_Destroy(nint bitmap) => PdfInterop.Pdfium.FPDFBitmap_Destroy(bitmap);
		public static nint FPDFText_LoadPage(nint page) => PdfInterop.Pdfium.FPDFText_LoadPage(page);
		public static void FPDFText_ClosePage(nint text_page) => PdfInterop.Pdfium.FPDFText_ClosePage(text_page);
		public static int FPDFText_CountChars(nint text_page) => PdfInterop.Pdfium.FPDFText_CountChars(text_page);
		public static int FPDFText_GetText(nint text_page, int start_index, int count, StringBuilder result) => PdfInterop.Pdfium.FPDFText_GetText(text_page, start_index, count, result);
		public static int FPDFText_GetCharIndexAtPos(nint text_page, double x, double y, double xTolerance, double yTolerance) => PdfInterop.Pdfium.FPDFText_GetCharIndexAtPos(text_page, x, y, xTolerance, yTolerance);
		public static bool FPDFText_GetCharBox(nint text_page, int index, out double left, out double right, out double bottom, out double top) => PdfInterop.Pdfium.FPDFText_GetCharBox(text_page, index, out left, out right, out bottom, out top);
		public static double FPDFText_GetFontSize(nint text_page, int index) => PdfInterop.Pdfium.FPDFText_GetFontSize(text_page, index);
		public static uint FPDFText_GetFontInfo(nint text_page, int index, nint buffer, uint buflen, out int flags) => PdfInterop.Pdfium.FPDFText_GetFontInfo(text_page, index, buffer, buflen, out flags);
		public static int FPDFText_GetFontWeight(nint text_page, int index) => PdfInterop.Pdfium.FPDFText_GetFontWeight(text_page, index);
	}

	public static void FPDF_InitLibrary()
	{
		Monitor.Enter(RenderLock);
		try
		{
			Native.FPDF_InitLibrary();
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static void FPDF_DestroyLibrary()
	{
		Monitor.Enter(RenderLock);
		try
		{
			Native.FPDF_DestroyLibrary();
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static nint FPDF_LoadDocument(string file_path, string? password)
	{
		Monitor.Enter(RenderLock);
		try
		{
			return Native.FPDF_LoadDocument(file_path, password);
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static void FPDF_CloseDocument(nint document)
	{
		Monitor.Enter(RenderLock);
		try
		{
			Native.FPDF_CloseDocument(document);
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static int FPDF_GetPageCount(nint document)
	{
		Monitor.Enter(RenderLock);
		try
		{
			return Native.FPDF_GetPageCount(document);
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static nint FPDF_LoadPage(nint document, int page_index)
	{
		Monitor.Enter(RenderLock);
		try
		{
			return Native.FPDF_LoadPage(document, page_index);
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static void FPDF_ClosePage(nint page)
	{
		Monitor.Enter(RenderLock);
		try
		{
			Native.FPDF_ClosePage(page);
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static double FPDF_GetPageWidth(nint page)
	{
		Monitor.Enter(RenderLock);
		try
		{
			return Native.FPDF_GetPageWidth(page);
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static double FPDF_GetPageHeight(nint page)
	{
		Monitor.Enter(RenderLock);
		try
		{
			return Native.FPDF_GetPageHeight(page);
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static nint FPDFBitmap_CreateEx(int width, int height, int format, nint first_scan, int stride)
	{
		Monitor.Enter(RenderLock);
		try
		{
			return Native.FPDFBitmap_CreateEx(width, height, format, first_scan, stride);
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static void FPDFBitmap_FillRect(nint bitmap, int left, int top, int width, int height, uint color)
	{
		Monitor.Enter(RenderLock);
		try
		{
			Native.FPDFBitmap_FillRect(bitmap, left, top, width, height, color);
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static void FPDF_RenderPageBitmap(nint bitmap, nint page, int start_x, int start_y, int size_x, int size_y, int rotate, int flags)
	{
		Monitor.Enter(RenderLock);
		try
		{
			Native.FPDF_RenderPageBitmap(bitmap, page, start_x, start_y, size_x, size_y, rotate, flags);
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static void FPDF_RenderPage(nint dc, nint page, int start_x, int start_y, int size_x, int size_y, int rotate, int flags)
	{
		Monitor.Enter(RenderLock);
		try
		{
			Native.FPDF_RenderPage(dc, page, start_x, start_y, size_x, size_y, rotate, flags);
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static void FPDFBitmap_Destroy(nint bitmap)
	{
		Monitor.Enter(RenderLock);
		try
		{
			Native.FPDFBitmap_Destroy(bitmap);
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static nint FPDFText_LoadPage(nint page)
	{
		Monitor.Enter(RenderLock);
		try
		{
			return Native.FPDFText_LoadPage(page);
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static void FPDFText_ClosePage(nint text_page)
	{
		Monitor.Enter(RenderLock);
		try
		{
			Native.FPDFText_ClosePage(text_page);
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static int FPDFText_CountChars(nint text_page)
	{
		Monitor.Enter(RenderLock);
		try
		{
			return Native.FPDFText_CountChars(text_page);
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static int FPDFText_GetText(nint text_page, int start_index, int count, StringBuilder result)
	{
		Monitor.Enter(RenderLock);
		try
		{
			return Native.FPDFText_GetText(text_page, start_index, count, result);
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static int FPDFText_GetCharIndexAtPos(nint text_page, double x, double y, double xTolerance, double yTolerance)
	{
		Monitor.Enter(RenderLock);
		try
		{
			return Native.FPDFText_GetCharIndexAtPos(text_page, x, y, xTolerance, yTolerance);
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static bool FPDFText_GetCharBox(nint text_page, int index, out double left, out double right, out double bottom, out double top)
	{
		Monitor.Enter(RenderLock);
		try
		{
			return Native.FPDFText_GetCharBox(text_page, index, out left, out right, out bottom, out top);
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static double FPDFText_GetFontSize(nint text_page, int index)
	{
		Monitor.Enter(RenderLock);
		try
		{
			return Native.FPDFText_GetFontSize(text_page, index);
		}
		catch (EntryPointNotFoundException)
		{
			return 0.0;
		}
		catch
		{
			return 0.0;
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static int FPDFText_GetFontWeight(nint text_page, int index)
	{
		Monitor.Enter(RenderLock);
		try
		{
			return Native.FPDFText_GetFontWeight(text_page, index);
		}
		catch (EntryPointNotFoundException)
		{
			return -1;
		}
		catch
		{
			return -1;
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static string FPDFText_GetFontName(nint text_page, int index, out int flags)
	{
		flags = 0;
		Monitor.Enter(RenderLock);
		try
		{
			uint length = Native.FPDFText_GetFontInfo(text_page, index, IntPtr.Zero, 0, out flags);
			if (length <= 0)
			{
				return "";
			}
			byte[] buffer = new byte[length];
			GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
			try
			{
				Native.FPDFText_GetFontInfo(text_page, index, handle.AddrOfPinnedObject(), length, out flags);
			}
			finally
			{
				handle.Free();
			}
			return Encoding.UTF8.GetString(buffer).TrimEnd('\0');
		}
		catch (EntryPointNotFoundException)
		{
			return "";
		}
		catch (Exception)
		{
			return "";
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static string FPDFText_GetFontName(nint text_page, int index)
	{
		return FPDFText_GetFontName(text_page, index, out _);
	}

	public static void Initialize()
	{
		Monitor.Enter(RenderLock);
		try
		{
			if (!_initialized)
			{
				Native.FPDF_InitLibrary();
				_initialized = true;
			}
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static void Shutdown()
	{
		Monitor.Enter(RenderLock);
		try
		{
			if (_initialized)
			{
				Native.FPDF_DestroyLibrary();
				_initialized = false;
			}
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static void CloseDocument(nint document)
	{
		if (document == IntPtr.Zero)
		{
			return;
		}
		Monitor.Enter(RenderLock);
		try
		{
			Native.FPDF_CloseDocument(document);
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static BitmapSource? RenderPageToBitmap(string filePath, int pageIndex, int targetWidth, int targetHeight, bool invertColors = false)
	{
		return RenderPageToBitmap(filePath, pageIndex, targetWidth, targetHeight, 24000000L, invertColors);
	}

	public static BitmapSource? RenderPageToBitmap(string filePath, int pageIndex, int targetWidth, int targetHeight, long maxPixels, bool invertColors = false)
	{
		Initialize();
		Monitor.Enter(RenderLock);
		try
		{
			nint num = Native.FPDF_LoadDocument(filePath, null);
			if (num == IntPtr.Zero)
			{
				return null;
			}
			try
			{
				return RenderPageToBitmap(num, pageIndex, targetWidth, targetHeight, maxPixels, invertColors);
			}
			finally
			{
				Native.FPDF_CloseDocument(num);
			}
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}

	public static BitmapSource? RenderPageToBitmap(nint document, int pageIndex, int targetWidth, int targetHeight, bool invertColors = false)
	{
		return RenderPageToBitmap(document, pageIndex, targetWidth, targetHeight, 24000000L, invertColors);
	}

	public static BitmapSource? RenderPageToBitmap(nint document, int pageIndex, int targetWidth, int targetHeight, long maxPixels, bool invertColors = false)
	{
		Initialize();
		ClampRenderDimensions(ref targetWidth, ref targetHeight, maxPixels);
		lock (RenderLock)
		{
			nint page = Native.FPDF_LoadPage(document, pageIndex);
			if (page == IntPtr.Zero) return null;
			try { return RenderLoadedPageToBitmap(page, targetWidth, targetHeight, 0, 0, targetWidth, targetHeight, invertColors); }
			finally { Native.FPDF_ClosePage(page); }
		}
	}

	internal static void ClampRenderDimensions(ref int width, ref int height, long maxPixels)
	{
		width = Math.Max(1, width);
		height = Math.Max(1, height);
		double scale = Math.Min(1.0, 40000.0 / Math.Max(width, height));
		scale = Math.Min(scale, Math.Sqrt(Math.Max(1L, maxPixels) / ((double)width * height)));
		width = Math.Max(1, (int)(width * scale));
		height = Math.Max(1, (int)(height * scale));
	}

	public static BitmapSource? RenderPageTileToBitmap(nint document, int pageIndex, int fullWidth, int fullHeight, int tileX, int tileY, int tileWidth, int tileHeight, bool invertColors = false)
	{
		Initialize();
		lock (RenderLock)
		{
			nint page = Native.FPDF_LoadPage(document, pageIndex);
			if (page == IntPtr.Zero) return null;
			try { return RenderLoadedPageToBitmap(page, fullWidth, fullHeight, tileX, tileY, tileWidth, tileHeight, invertColors); }
			finally { Native.FPDF_ClosePage(page); }
		}
	}

	internal static BitmapSource? RenderLoadedPageToBitmap(nint page, int fullWidth, int fullHeight,
		int tileX, int tileY, int tileWidth, int tileHeight, bool invertColors)
	{
		lock (RenderLock)
		{
			if (page == IntPtr.Zero || fullWidth < 1 || fullHeight < 1 ||
				fullWidth > 40000 || fullHeight > 40000 || tileX < 0 || tileY < 0 ||
				tileX >= fullWidth || tileY >= fullHeight) return null;
			tileWidth = Math.Min(tileWidth, fullWidth - tileX);
			tileHeight = Math.Min(tileHeight, fullHeight - tileY);
			if (tileWidth < 1 || tileHeight < 1) return null;
			int stride = checked(tileWidth * 4);
			byte[] pixels;
			try { pixels = new byte[checked(stride * tileHeight)]; }
			catch (OutOfMemoryException) { return null; }
			GCHandle pin = GCHandle.Alloc(pixels, GCHandleType.Pinned);
			nint bitmap = IntPtr.Zero;
			try
			{
				bitmap = Native.FPDFBitmap_CreateEx(tileWidth, tileHeight, FPDF_BGRA, pin.AddrOfPinnedObject(), stride);
				if (bitmap == IntPtr.Zero) return null;
				Native.FPDFBitmap_FillRect(bitmap, 0, 0, tileWidth, tileHeight, uint.MaxValue);
				// Retain antialiasing for small text and thin drawing paths.
				Native.FPDF_RenderPageBitmap(bitmap, page, -tileX, -tileY, fullWidth, fullHeight, 0, FPDF_ANNOT | FPDF_LCD_TEXT);
			}
			finally
			{
				if (bitmap != IntPtr.Zero) Native.FPDFBitmap_Destroy(bitmap);
				pin.Free();
			}
			if (invertColors)
			{
				for (int i = 0; i < pixels.Length; i += 4)
				{
					pixels[i] ^= 255;
					pixels[i + 1] ^= 255;
					pixels[i + 2] ^= 255;
				}
			}
			BitmapSource source = BitmapSource.Create(tileWidth, tileHeight, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
			source.Freeze();
			return source;
		}
	}

	public static bool TryGetPageSizeByIndex(nint document, int pageIndex, out double width, out double height)
	{
		Initialize();
		Monitor.Enter(RenderLock);
		try
		{
			try
			{
				return Native.FPDF_GetPageSizeByIndex(document, pageIndex, out width, out height) != 0;
			}
			catch (EntryPointNotFoundException)
			{
				width = 0.0;
				height = 0.0;
				return false;
			}
		}
		finally
		{
			Monitor.Exit(RenderLock);
		}
	}
}
