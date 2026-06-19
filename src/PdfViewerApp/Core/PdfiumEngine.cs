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
	private static readonly ReaderWriterLockSlim LockSlim = new ReaderWriterLockSlim();

	public static object SyncRoot => RenderLock;

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
	}

	public static void FPDF_InitLibrary()
	{
		LockSlim.EnterWriteLock();
		try
		{
			Native.FPDF_InitLibrary();
		}
		finally
		{
			LockSlim.ExitWriteLock();
		}
	}

	public static void FPDF_DestroyLibrary()
	{
		LockSlim.EnterWriteLock();
		try
		{
			Native.FPDF_DestroyLibrary();
		}
		finally
		{
			LockSlim.ExitWriteLock();
		}
	}

	public static nint FPDF_LoadDocument(string file_path, string? password)
	{
		LockSlim.EnterWriteLock();
		try
		{
			return Native.FPDF_LoadDocument(file_path, password);
		}
		finally
		{
			LockSlim.ExitWriteLock();
		}
	}

	public static void FPDF_CloseDocument(nint document)
	{
		LockSlim.EnterWriteLock();
		try
		{
			Native.FPDF_CloseDocument(document);
		}
		finally
		{
			LockSlim.ExitWriteLock();
		}
	}

	public static int FPDF_GetPageCount(nint document)
	{
		LockSlim.EnterReadLock();
		try
		{
			return Native.FPDF_GetPageCount(document);
		}
		finally
		{
			LockSlim.ExitReadLock();
		}
	}

	public static nint FPDF_LoadPage(nint document, int page_index)
	{
		LockSlim.EnterWriteLock();
		try
		{
			return Native.FPDF_LoadPage(document, page_index);
		}
		finally
		{
			LockSlim.ExitWriteLock();
		}
	}

	public static void FPDF_ClosePage(nint page)
	{
		LockSlim.EnterWriteLock();
		try
		{
			Native.FPDF_ClosePage(page);
		}
		finally
		{
			LockSlim.ExitWriteLock();
		}
	}

	public static double FPDF_GetPageWidth(nint page)
	{
		LockSlim.EnterReadLock();
		try
		{
			return Native.FPDF_GetPageWidth(page);
		}
		finally
		{
			LockSlim.ExitReadLock();
		}
	}

	public static double FPDF_GetPageHeight(nint page)
	{
		LockSlim.EnterReadLock();
		try
		{
			return Native.FPDF_GetPageHeight(page);
		}
		finally
		{
			LockSlim.ExitReadLock();
		}
	}

	public static nint FPDFBitmap_CreateEx(int width, int height, int format, nint first_scan, int stride)
	{
		LockSlim.EnterWriteLock();
		try
		{
			return Native.FPDFBitmap_CreateEx(width, height, format, first_scan, stride);
		}
		finally
		{
			LockSlim.ExitWriteLock();
		}
	}

	public static void FPDFBitmap_FillRect(nint bitmap, int left, int top, int width, int height, uint color)
	{
		LockSlim.EnterWriteLock();
		try
		{
			Native.FPDFBitmap_FillRect(bitmap, left, top, width, height, color);
		}
		finally
		{
			LockSlim.ExitWriteLock();
		}
	}

	public static void FPDF_RenderPageBitmap(nint bitmap, nint page, int start_x, int start_y, int size_x, int size_y, int rotate, int flags)
	{
		LockSlim.EnterWriteLock();
		try
		{
			Native.FPDF_RenderPageBitmap(bitmap, page, start_x, start_y, size_x, size_y, rotate, flags);
		}
		finally
		{
			LockSlim.ExitWriteLock();
		}
	}

	public static void FPDF_RenderPage(nint dc, nint page, int start_x, int start_y, int size_x, int size_y, int rotate, int flags)
	{
		LockSlim.EnterWriteLock();
		try
		{
			Native.FPDF_RenderPage(dc, page, start_x, start_y, size_x, size_y, rotate, flags);
		}
		finally
		{
			LockSlim.ExitWriteLock();
		}
	}

	public static void FPDFBitmap_Destroy(nint bitmap)
	{
		LockSlim.EnterWriteLock();
		try
		{
			Native.FPDFBitmap_Destroy(bitmap);
		}
		finally
		{
			LockSlim.ExitWriteLock();
		}
	}

	public static nint FPDFText_LoadPage(nint page)
	{
		LockSlim.EnterWriteLock();
		try
		{
			return Native.FPDFText_LoadPage(page);
		}
		finally
		{
			LockSlim.ExitWriteLock();
		}
	}

	public static void FPDFText_ClosePage(nint text_page)
	{
		LockSlim.EnterWriteLock();
		try
		{
			Native.FPDFText_ClosePage(text_page);
		}
		finally
		{
			LockSlim.ExitWriteLock();
		}
	}

	public static int FPDFText_CountChars(nint text_page)
	{
		LockSlim.EnterReadLock();
		try
		{
			return Native.FPDFText_CountChars(text_page);
		}
		finally
		{
			LockSlim.ExitReadLock();
		}
	}

	public static int FPDFText_GetText(nint text_page, int start_index, int count, StringBuilder result)
	{
		LockSlim.EnterReadLock();
		try
		{
			return Native.FPDFText_GetText(text_page, start_index, count, result);
		}
		finally
		{
			LockSlim.ExitReadLock();
		}
	}

	public static int FPDFText_GetCharIndexAtPos(nint text_page, double x, double y, double xTolerance, double yTolerance)
	{
		LockSlim.EnterReadLock();
		try
		{
			return Native.FPDFText_GetCharIndexAtPos(text_page, x, y, xTolerance, yTolerance);
		}
		finally
		{
			LockSlim.ExitReadLock();
		}
	}

	public static bool FPDFText_GetCharBox(nint text_page, int index, out double left, out double right, out double bottom, out double top)
	{
		LockSlim.EnterReadLock();
		try
		{
			return Native.FPDFText_GetCharBox(text_page, index, out left, out right, out bottom, out top);
		}
		finally
		{
			LockSlim.ExitReadLock();
		}
	}

	public static void Initialize()
	{
		LockSlim.EnterWriteLock();
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
			LockSlim.ExitWriteLock();
		}
	}

	public static void Shutdown()
	{
		LockSlim.EnterWriteLock();
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
			LockSlim.ExitWriteLock();
		}
	}

	public static void CloseDocument(nint document)
	{
		if (document == IntPtr.Zero)
		{
			return;
		}
		LockSlim.EnterWriteLock();
		try
		{
			Native.FPDF_CloseDocument(document);
		}
		finally
		{
			LockSlim.ExitWriteLock();
		}
	}

	public static BitmapSource? RenderPageToBitmap(string filePath, int pageIndex, int targetWidth, int targetHeight, bool invertColors = false)
	{
		return RenderPageToBitmap(filePath, pageIndex, targetWidth, targetHeight, 24000000L, invertColors);
	}

	public static BitmapSource? RenderPageToBitmap(string filePath, int pageIndex, int targetWidth, int targetHeight, long maxPixels, bool invertColors = false)
	{
		Initialize();
		LockSlim.EnterWriteLock();
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
			LockSlim.ExitWriteLock();
		}
	}

	public static BitmapSource? RenderPageToBitmap(nint document, int pageIndex, int targetWidth, int targetHeight, bool invertColors = false)
	{
		return RenderPageToBitmap(document, pageIndex, targetWidth, targetHeight, 24000000L, invertColors);
	}

	public static BitmapSource? RenderPageToBitmap(nint document, int pageIndex, int targetWidth, int targetHeight, long maxPixels, bool invertColors = false)
	{
		Initialize();
		targetWidth = Math.Max(1, Math.Min(targetWidth, 40000));
		targetHeight = Math.Max(1, Math.Min(targetHeight, 40000));
		long num = (long)targetWidth * (long)targetHeight;
		if (num > maxPixels)
		{
			double num2 = Math.Sqrt((double)maxPixels / (double)num);
			targetWidth = Math.Max(1, (int)((double)targetWidth * num2));
			targetHeight = Math.Max(1, (int)((double)targetHeight * num2));
		}
		LockSlim.EnterWriteLock();
		try
		{
			nint num3 = Native.FPDF_LoadPage(document, pageIndex);
			if (num3 == IntPtr.Zero)
			{
				return null;
			}
			try
			{
				int num4 = targetWidth * 4;
				byte[] array;
				try
				{
					array = new byte[num4 * targetHeight];
				}
				catch (OutOfMemoryException)
				{
					return null;
				}
				GCHandle gCHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
				try
				{
					nint first_scan = gCHandle.AddrOfPinnedObject();
					nint num5 = Native.FPDFBitmap_CreateEx(targetWidth, targetHeight, 4, first_scan, num4);
					if (num5 != IntPtr.Zero)
					{
						Native.FPDFBitmap_FillRect(num5, 0, 0, targetWidth, targetHeight, uint.MaxValue);
						Native.FPDF_RenderPageBitmap(num5, num3, 0, 0, targetWidth, targetHeight, 0, 3);
						Native.FPDFBitmap_Destroy(num5);
					}
				}
				finally
				{
					gCHandle.Free();
				}

				if (invertColors)
				{
					unsafe
					{
						fixed (byte* p = array)
						{
							uint* pCol = (uint*)p;
							int len = array.Length / 4;
							int j = 0;
							for (; j <= len - 4; j += 4)
							{
								pCol[j] ^= 0x00FFFFFF;
								pCol[j + 1] ^= 0x00FFFFFF;
								pCol[j + 2] ^= 0x00FFFFFF;
								pCol[j + 3] ^= 0x00FFFFFF;
							}
							for (; j < len; j++)
							{
								pCol[j] ^= 0x00FFFFFF;
							}
						}
					}
				}

				BitmapSource bitmapSource = BitmapSource.Create(targetWidth, targetHeight, 96.0, 96.0, PixelFormats.Bgra32, null, array, num4);
				bitmapSource.Freeze();
				return bitmapSource;
			}
			finally
			{
				Native.FPDF_ClosePage(num3);
			}
		}
		finally
		{
			LockSlim.ExitWriteLock();
		}
	}

	public static BitmapSource? RenderPageTileToBitmap(nint document, int pageIndex, int fullWidth, int fullHeight, int tileX, int tileY, int tileWidth, int tileHeight, bool invertColors = false)
	{
		Initialize();
		fullWidth = Math.Max(1, Math.Min(fullWidth, 40000));
		fullHeight = Math.Max(1, Math.Min(fullHeight, 40000));
		tileX = Math.Max(0, tileX);
		tileY = Math.Max(0, tileY);
		tileWidth = Math.Max(1, Math.Min(tileWidth, fullWidth - tileX));
		tileHeight = Math.Max(1, Math.Min(tileHeight, fullHeight - tileY));
		LockSlim.EnterWriteLock();
		try
		{
			nint num = Native.FPDF_LoadPage(document, pageIndex);
			if (num == IntPtr.Zero)
			{
				return null;
			}
			try
			{
				int num2 = tileWidth * 4;
				byte[] array;
				try
				{
					array = new byte[num2 * tileHeight];
				}
				catch (OutOfMemoryException)
				{
					return null;
				}
				GCHandle gCHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
				try
				{
					nint first_scan = gCHandle.AddrOfPinnedObject();
					nint num3 = Native.FPDFBitmap_CreateEx(tileWidth, tileHeight, 4, first_scan, num2);
					if (num3 != IntPtr.Zero)
					{
						Native.FPDFBitmap_FillRect(num3, 0, 0, tileWidth, tileHeight, uint.MaxValue);
						Native.FPDF_RenderPageBitmap(num3, num, -tileX, -tileY, fullWidth, fullHeight, 0, 3);
						Native.FPDFBitmap_Destroy(num3);
					}
				}
				finally
				{
					gCHandle.Free();
				}

				if (invertColors)
				{
					unsafe
					{
						fixed (byte* p = array)
						{
							uint* pCol = (uint*)p;
							int len = array.Length / 4;
							int j = 0;
							for (; j <= len - 4; j += 4)
							{
								pCol[j] ^= 0x00FFFFFF;
								pCol[j + 1] ^= 0x00FFFFFF;
								pCol[j + 2] ^= 0x00FFFFFF;
								pCol[j + 3] ^= 0x00FFFFFF;
							}
							for (; j < len; j++)
							{
								pCol[j] ^= 0x00FFFFFF;
							}
						}
					}
				}

				BitmapSource bitmapSource = BitmapSource.Create(tileWidth, tileHeight, 96.0, 96.0, PixelFormats.Bgra32, null, array, num2);
				bitmapSource.Freeze();
				return bitmapSource;
			}
			finally
			{
				Native.FPDF_ClosePage(num);
			}
		}
		finally
		{
			LockSlim.ExitWriteLock();
		}
	}

	public static bool TryGetPageSizeByIndex(nint document, int pageIndex, out double width, out double height)
	{
		Initialize();
		LockSlim.EnterReadLock();
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
			LockSlim.ExitReadLock();
		}
	}
}
