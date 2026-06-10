using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

	[DllImport("pdfium.dll")]
	public static extern void FPDF_InitLibrary();

	[DllImport("pdfium.dll")]
	public static extern void FPDF_DestroyLibrary();

	[DllImport("pdfium.dll")]
	public static extern nint FPDF_LoadDocument([MarshalAs(UnmanagedType.LPUTF8Str)] string file_path, [MarshalAs(UnmanagedType.LPUTF8Str)] string? password);

	[DllImport("pdfium.dll")]
	public static extern void FPDF_CloseDocument(nint document);

	[DllImport("pdfium.dll")]
	public static extern int FPDF_GetPageCount(nint document);

	[DllImport("pdfium.dll")]
	public static extern nint FPDF_LoadPage(nint document, int page_index);

	[DllImport("pdfium.dll")]
	public static extern void FPDF_ClosePage(nint page);

	[DllImport("pdfium.dll")]
	public static extern double FPDF_GetPageWidth(nint page);

	[DllImport("pdfium.dll")]
	public static extern double FPDF_GetPageHeight(nint page);

	[DllImport("pdfium.dll")]
	private static extern int FPDF_GetPageSizeByIndex(nint document, int page_index, out double width, out double height);

	[DllImport("pdfium.dll")]
	public static extern nint FPDFBitmap_CreateEx(int width, int height, int format, nint first_scan, int stride);

	[DllImport("pdfium.dll")]
	public static extern void FPDFBitmap_FillRect(nint bitmap, int left, int top, int width, int height, uint color);

	[DllImport("pdfium.dll")]
	public static extern void FPDF_RenderPageBitmap(nint bitmap, nint page, int start_x, int start_y, int size_x, int size_y, int rotate, int flags);

	[DllImport("pdfium.dll")]
	public static extern void FPDF_RenderPage(nint dc, nint page, int start_x, int start_y, int size_x, int size_y, int rotate, int flags);

	[DllImport("pdfium.dll")]
	public static extern void FPDFBitmap_Destroy(nint bitmap);

	[DllImport("pdfium.dll")]
	public static extern nint FPDFText_LoadPage(nint page);

	[DllImport("pdfium.dll")]
	public static extern void FPDFText_ClosePage(nint text_page);

	[DllImport("pdfium.dll")]
	public static extern int FPDFText_CountChars(nint text_page);

	[DllImport("pdfium.dll")]
	public static extern int FPDFText_GetText(nint text_page, int start_index, int count, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder result);

	[DllImport("pdfium.dll")]
	public static extern int FPDFText_GetCharIndexAtPos(nint text_page, double x, double y, double xTolerance, double yTolerance);

	[DllImport("pdfium.dll")]
	public static extern bool FPDFText_GetCharBox(nint text_page, int index, out double left, out double right, out double bottom, out double top);

	public static void Initialize()
	{
		lock (RenderLock)
		{
			if (!_initialized)
			{
				FPDF_InitLibrary();
				_initialized = true;
			}
		}
	}

	public static void Shutdown()
	{
		lock (RenderLock)
		{
			if (_initialized)
			{
				FPDF_DestroyLibrary();
				_initialized = false;
			}
		}
	}

	public static void CloseDocument(nint document)
	{
		if (document == IntPtr.Zero)
		{
			return;
		}
		lock (RenderLock)
		{
			FPDF_CloseDocument(document);
		}
	}

	public static BitmapSource? RenderPageToBitmap(string filePath, int pageIndex, int targetWidth, int targetHeight, bool invertColors = false)
	{
		return RenderPageToBitmap(filePath, pageIndex, targetWidth, targetHeight, 24000000L, invertColors);
	}

	public static BitmapSource? RenderPageToBitmap(string filePath, int pageIndex, int targetWidth, int targetHeight, long maxPixels, bool invertColors = false)
	{
		Initialize();
		lock (RenderLock)
		{
			nint num = FPDF_LoadDocument(filePath, null);
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
				FPDF_CloseDocument(num);
			}
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
		lock (RenderLock)
		{
			nint num3 = FPDF_LoadPage(document, pageIndex);
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
					nint num5 = FPDFBitmap_CreateEx(targetWidth, targetHeight, 4, first_scan, num4);
					if (num5 != IntPtr.Zero)
					{
						FPDFBitmap_FillRect(num5, 0, 0, targetWidth, targetHeight, uint.MaxValue);
						FPDF_RenderPageBitmap(num5, num3, 0, 0, targetWidth, targetHeight, 0, 3);
						FPDFBitmap_Destroy(num5);
					}
				}
				finally
				{
					gCHandle.Free();
				}

				if (invertColors)
				{
					for (int i = 0; i < array.Length; i += 4)
					{
						array[i] = (byte)(255 - array[i]);       // B
						array[i + 1] = (byte)(255 - array[i + 1]); // G
						array[i + 2] = (byte)(255 - array[i + 2]); // R
					}
				}

				BitmapSource bitmapSource = BitmapSource.Create(targetWidth, targetHeight, 96.0, 96.0, PixelFormats.Bgra32, null, array, num4);
				bitmapSource.Freeze();
				return bitmapSource;
			}
			finally
			{
				FPDF_ClosePage(num3);
			}
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
		lock (RenderLock)
		{
			nint num = FPDF_LoadPage(document, pageIndex);
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
					nint num3 = FPDFBitmap_CreateEx(tileWidth, tileHeight, 4, first_scan, num2);
					if (num3 != IntPtr.Zero)
					{
						FPDFBitmap_FillRect(num3, 0, 0, tileWidth, tileHeight, uint.MaxValue);
						FPDF_RenderPageBitmap(num3, num, -tileX, -tileY, fullWidth, fullHeight, 0, 3);
						FPDFBitmap_Destroy(num3);
					}
				}
				finally
				{
					gCHandle.Free();
				}

				if (invertColors)
				{
					for (int i = 0; i < array.Length; i += 4)
					{
						array[i] = (byte)(255 - array[i]);       // B
						array[i + 1] = (byte)(255 - array[i + 1]); // G
						array[i + 2] = (byte)(255 - array[i + 2]); // R
					}
				}

				BitmapSource bitmapSource = BitmapSource.Create(tileWidth, tileHeight, 96.0, 96.0, PixelFormats.Bgra32, null, array, num2);
				bitmapSource.Freeze();
				return bitmapSource;
			}
			finally
			{
				FPDF_ClosePage(num);
			}
		}
	}

	public static bool TryGetPageSizeByIndex(nint document, int pageIndex, out double width, out double height)
	{
		Initialize();
		try
		{
			return FPDF_GetPageSizeByIndex(document, pageIndex, out width, out height) != 0;
		}
		catch (EntryPointNotFoundException)
		{
			width = 0.0;
			height = 0.0;
			return false;
		}
	}
}
