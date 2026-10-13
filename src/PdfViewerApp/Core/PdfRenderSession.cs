using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PdfViewerApp;

// Owns the native handles used by background rendering. Keeping parsed pages alive
// avoids reparsing large Revit content streams on every zoom, tile and thumbnail.
internal sealed class PdfRenderSession : IDisposable
{
	private readonly string _path;
	private readonly Dictionary<int, (nint Handle, long Used)> _pages = new();
	private readonly ConcurrentDictionary<int, int> _rotations = new();
	private nint _document;
	private long _sequence;
	private int _disposed;

	public PdfRenderSession(string path) => _path = path;

	public BitmapSource? RenderSnapshot(PdfSnapshotSelection snapshot, int longEdge = 900, int maxPixels = 8000000)
	{
		if (!double.IsFinite(snapshot.X) || !double.IsFinite(snapshot.Y) ||
			!double.IsFinite(snapshot.Width) || !double.IsFinite(snapshot.Height) ||
			snapshot.X < 0 || snapshot.Y < 0 || snapshot.Width <= 0 || snapshot.Height <= 0 ||
			snapshot.X + snapshot.Width > 1.000001 || snapshot.Y + snapshot.Height > 1.000001)
			throw new ArgumentOutOfRangeException(nameof(snapshot), "Vùng chọn Snapshot không hợp lệ.");
		longEdge = Math.Clamp(longEdge, 64, 12000);
		maxPixels = Math.Clamp(maxPixels, 4096, 32000000);
		lock (PdfiumEngine.SyncRoot)
		{
			if (Volatile.Read(ref _disposed) != 0) return null;
			PdfiumEngine.Initialize();
			if (_document == IntPtr.Zero) _document = PdfiumEngine.FPDF_LoadDocument(_path, null);
			if (_document == IntPtr.Zero || !PdfiumEngine.TryGetPageSizeByIndex(_document, snapshot.PageIndex, out double w, out double h)) return null;
			double ratio = Math.Max(0.001, w * snapshot.Width) / Math.Max(0.001, h * snapshot.Height);
			int width = ratio >= 1 ? longEdge : Math.Max(1, (int)Math.Round(longEdge * ratio));
			int height = ratio >= 1 ? Math.Max(1, (int)Math.Round(longEdge / ratio)) : longEdge;
			double pixelScale = Math.Min(1, Math.Sqrt((double)maxPixels / ((double)width * height)));
			width = Math.Max(1, (int)(width * pixelScale));
			height = Math.Max(1, (int)(height * pixelScale));
			int fullWidth = (int)Math.Min(int.MaxValue, width / Math.Max(0.0001, snapshot.Width));
			int fullHeight = (int)Math.Min(int.MaxValue, height / Math.Max(0.0001, snapshot.Height));
			PdfiumEngine.ClampRenderDimensions(ref fullWidth, ref fullHeight, long.MaxValue);
			return Render(snapshot.PageIndex, fullWidth, fullHeight,
				(int)(snapshot.X * fullWidth), (int)(snapshot.Y * fullHeight),
				Math.Max(1, (int)(snapshot.Width * fullWidth)), Math.Max(1, (int)(snapshot.Height * fullHeight)), false);
		}
	}

	public BitmapSource? Render(int pageIndex, int fullWidth, int fullHeight,
		int x, int y, int width, int height, bool invertColors)
	{
		lock (PdfiumEngine.SyncRoot)
		{
			if (Volatile.Read(ref _disposed) != 0) return null;
			PdfiumEngine.Initialize();
			if (_document == IntPtr.Zero)
				_document = PdfiumEngine.FPDF_LoadDocument(_path, null);
			if (_document == IntPtr.Zero) return null;

			if (!_pages.TryGetValue(pageIndex, out var page))
			{
				// Bound native page retention independently of the bitmap cache.
				if (_pages.Count >= 2)
				{
					var oldest = _pages.OrderBy(p => p.Value.Used).First();
					PdfiumEngine.FPDF_ClosePage(oldest.Value.Handle);
					_pages.Remove(oldest.Key);
				}
				nint handle = PdfiumEngine.FPDF_LoadPage(_document, pageIndex);
				if (handle == IntPtr.Zero) return null;
				page = (handle, ++_sequence);
				_rotations[pageIndex] = PdfInterop.Pdfium.FPDFPage_GetRotation(handle) * 90;
			}
			_pages[pageIndex] = (page.Handle, ++_sequence);
			return PdfiumEngine.RenderLoadedPageToBitmap(page.Handle, fullWidth, fullHeight,
				x, y, width, height, invertColors);
		}
	}

	public bool TryGetRotation(int pageIndex, out int rotation) => _rotations.TryGetValue(pageIndex, out rotation);

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
		// Closing a tab must not wait on the UI thread for an ongoing native render.
		_ = Task.Run(() =>
		{
			lock (PdfiumEngine.SyncRoot)
			{
				foreach (var page in _pages.Values) PdfiumEngine.FPDF_ClosePage(page.Handle);
				_pages.Clear();
				if (_document != IntPtr.Zero) PdfiumEngine.FPDF_CloseDocument(_document);
				_document = IntPtr.Zero;
			}
		});
	}
}
