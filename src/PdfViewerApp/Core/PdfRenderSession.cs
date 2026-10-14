using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PdfViewerApp;

// Owns the native handles used by background rendering. Keeping parsed pages alive
// avoids reparsing large Revit content streams on every zoom, tile and thumbnail.
internal sealed class PdfRenderSession : IDisposable
{
	private readonly string _path;
	private readonly PdfNativeTextEdit[] _textEdits;
	private readonly HashSet<int> _editedPages = new();
	private readonly Dictionary<int, (nint Handle, long Used)> _pages = new();
	private readonly ConcurrentDictionary<int, int> _rotations = new();
	private nint _document;
	private long _sequence;
	private int _disposed;
	internal Task Closed { get; private set; } = Task.CompletedTask;
	private readonly Lazy<string> _fingerprint;

	public PdfRenderSession(string path, PdfNativeTextEdit[]? textEdits = null)
    {
        _path = path;
        _textEdits = textEdits?.ToArray() ?? Array.Empty<PdfNativeTextEdit>();
        _fingerprint = new Lazy<string>(() => PdfPersistentCache.Fingerprint(path) +
            (_textEdits.Length == 0 ? "" : ":text:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(_textEdits))))));
	}
	internal string Fingerprint => _fingerprint.Value;

    public System.Windows.Size GetPageSizePoints(int pageIndex)
    {
        lock (PdfiumEngine.SyncRoot)
        {
            if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(PdfRenderSession));
            PdfiumEngine.Initialize();
            if (_document == IntPtr.Zero) _document = PdfiumEngine.FPDF_LoadDocument(_path, null);
            if (_document == IntPtr.Zero || !PdfiumEngine.TryGetPageSizeByIndex(_document, pageIndex, out double w, out double h))
                throw new InvalidOperationException("Không đọc được kích thước trang PDF.");
            return new System.Windows.Size(w, h);
        }
    }

	public BitmapSource? RenderSnapshot(PdfSnapshotSelection snapshot, int longEdge = 900, int maxPixels = 8000000)
	{
		if (!double.IsFinite(snapshot.X) || !double.IsFinite(snapshot.Y) ||
			!double.IsFinite(snapshot.Width) || !double.IsFinite(snapshot.Height) ||
			snapshot.X < 0 || snapshot.Y < 0 || snapshot.Width <= 0 || snapshot.Height <= 0 ||
			snapshot.X + snapshot.Width > 1.000001 || snapshot.Y + snapshot.Height > 1.000001)
			throw new ArgumentOutOfRangeException(nameof(snapshot), "Vùng chọn Snapshot không hợp lệ.");
		longEdge = Math.Clamp(longEdge, 64, 12000);
		maxPixels = Math.Clamp(maxPixels, 4096, 32000000);
		var size = GetPageSizePoints(snapshot.PageIndex);
		double w = size.Width, h = size.Height;
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

	public BitmapSource? Render(int pageIndex, int fullWidth, int fullHeight,
		int x, int y, int width, int height, bool invertColors, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (Volatile.Read(ref _disposed) != 0) return null;
		string? cacheKey = null;
		try
		{
			cacheKey = $"render-v3:{Fingerprint}:{pageIndex}:{fullWidth},{fullHeight}:{x},{y},{width},{height}:{invertColors}:{PdfiumEngine.EnhanceThinLines}";
			var cached = PdfPersistentCache.ReadBitmap(cacheKey);
			cancellationToken.ThrowIfCancellationRequested();
			if (cached != null && int.TryParse(PdfPersistentCache.ReadText(cacheKey + ":rotation"), out int rotation))
			{ _rotations[pageIndex] = rotation; return cached; }
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) { PdfPerfLogger.Log("Cache lookup: " + ex.Message); }
		BitmapSource? result;
		lock (PdfiumEngine.SyncRoot)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (Volatile.Read(ref _disposed) != 0) return null;
			PdfiumEngine.Initialize();
			if (_document == IntPtr.Zero)
				_document = PdfiumEngine.FPDF_LoadDocument(_path, null);
			if (_document == IntPtr.Zero) return null;

            nint pageHandle = GetRetainedPage(pageIndex);
            if (pageHandle == IntPtr.Zero) return null;
			cancellationToken.ThrowIfCancellationRequested();
			result = PdfiumEngine.RenderLoadedPageToBitmap(pageHandle, fullWidth, fullHeight,
				x, y, width, height, invertColors);
		}
		cancellationToken.ThrowIfCancellationRequested();
		if (result != null && cacheKey != null)
		{
			if (_rotations.TryGetValue(pageIndex, out int rotation)) PdfPersistentCache.WriteText(cacheKey + ":rotation", rotation.ToString());
			PdfPersistentCache.QueueBitmap(cacheKey, result);
		}
		return result;
	}

    // Caller holds the PDFium lock; native pages are bounded by the same LRU for rendering and text.
    private nint GetRetainedPage(int pageIndex)
    {
        if (_pages.TryGetValue(pageIndex, out var saved)) { _pages[pageIndex] = (saved.Handle, ++_sequence); return saved.Handle; }
        if (_pages.Count >= 2)
        {
            var oldest = _pages.OrderBy(p => p.Value.Used).First();
            PdfiumEngine.FPDF_ClosePage(oldest.Value.Handle); _pages.Remove(oldest.Key);
        }
        nint page = PdfiumEngine.FPDF_LoadPage(_document, pageIndex);
        if (page != IntPtr.Zero)
        {
            try
            {
                if (!_editedPages.Contains(pageIndex))
                {
                    PdfNativeTextEditor.ApplyPage(_document, page, _textEdits.Where(e => e.PageNumber == pageIndex + 1).ToArray());
                    _editedPages.Add(pageIndex);
                }
            }
            catch { PdfiumEngine.FPDF_ClosePage(page); throw; }
        }
        if (page != IntPtr.Zero) { _pages[pageIndex] = (page, ++_sequence); _rotations[pageIndex] = PdfInterop.Pdfium.FPDFPage_GetRotation(page) * 90; }
        return page;
    }

    internal PdfOcrRegion MapOcrToPdf(int pageIndex, PdfOcrRegion word)
    {
        lock (PdfiumEngine.SyncRoot)
        {
            var size = GetPageSizePoints(pageIndex); nint page = GetRetainedPage(pageIndex);
            int rw = Math.Max(1, (int)Math.Round(size.Width * 4)), rh = Math.Max(1, (int)Math.Round(size.Height * 4));
            if (page == IntPtr.Zero ||
                !FPDF_DeviceToPage(page, 0, 0, rw, rh, 0, (int)Math.Round(word.Left / size.Width * rw), (int)Math.Round((size.Height - word.Bottom - word.Height) / size.Height * rh), out double x1, out double y1) ||
                !FPDF_DeviceToPage(page, 0, 0, rw, rh, 0, (int)Math.Round((word.Left + word.Width) / size.Width * rw), (int)Math.Round((size.Height - word.Bottom) / size.Height * rh), out double x2, out double y2))
                throw new InvalidOperationException("Không quy đổi được vị trí chữ trên PDF.");
            return word with { Left = Math.Min(x1, x2), Bottom = Math.Min(y1, y2), Width = Math.Abs(x2 - x1), Height = Math.Abs(y2 - y1) };
        }
    }

    [DllImport("pdfium.dll")]
    private static extern bool FPDF_DeviceToPage(nint page, int startX, int startY, int sizeX, int sizeY, int rotate,
        int deviceX, int deviceY, out double pageX, out double pageY);

	internal List<PdfOcrRegion> ReadNativeText(int pageIndex, System.Windows.Rect region, CancellationToken token)
	{
		lock (PdfiumEngine.SyncRoot)
		{
			token.ThrowIfCancellationRequested();
			if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(PdfRenderSession));
			PdfiumEngine.Initialize();
			if (_document == IntPtr.Zero) _document = PdfiumEngine.FPDF_LoadDocument(_path, null);
			if (_document == IntPtr.Zero) throw new InvalidOperationException("Không đọc được PDF để nhận diện chữ.");
			nint page = GetRetainedPage(pageIndex);
			if (page == IntPtr.Zero) throw new InvalidOperationException("Không đọc được trang PDF.");
			nint textPage = IntPtr.Zero;
			try
			{
				textPage = PdfiumEngine.FPDFText_LoadPage(page);
				var result = new List<PdfOcrRegion>();
				if (textPage == IntPtr.Zero) return result;
				var size = GetPageSizePoints(pageIndex);
				int rw = Math.Max(1, (int)Math.Round(size.Width * 4)), rh = Math.Max(1, (int)Math.Round(size.Height * 4));
				var word = new StringBuilder();
				System.Windows.Rect box = System.Windows.Rect.Empty;
				void Flush()
				{
					if (word.Length > 0 && !box.IsEmpty && box.Width > 0 && box.Height > 0)
						result.Add(new PdfOcrRegion(word.ToString(), box.X, size.Height - box.Bottom, box.Width, box.Height));
					word.Clear(); box = System.Windows.Rect.Empty;
				}
				int count = PdfiumEngine.FPDFText_CountChars(textPage);
				for (int i = 0; i < count; i++)
				{
					if (i % 128 == 0) token.ThrowIfCancellationRequested();
					uint code = FPDFText_GetUnicode(textPage, i);
					if (code == 0 || code > 0x10ffff || code is >= 0xd800 and <= 0xdfff || code == 0xfffd) { Flush(); continue; }
					string value = char.ConvertFromUtf32((int)code);
					if (string.IsNullOrWhiteSpace(value) || char.IsControl(value, 0)) { Flush(); continue; }
					if (!PdfiumEngine.FPDFText_GetCharBox(textPage, i, out double left, out double right, out double bottom, out double top)) { Flush(); continue; }
					if (!FPDF_PageToDevice(page, 0, 0, rw, rh, 0, left, top, out int x1, out int y1) ||
						!FPDF_PageToDevice(page, 0, 0, rw, rh, 0, right, bottom, out int x2, out int y2)) { Flush(); continue; }
					var character = new System.Windows.Rect(Math.Min(x1, x2) * size.Width / rw, Math.Min(y1, y2) * size.Height / rh,
						Math.Abs(x2 - x1) * size.Width / rw, Math.Abs(y2 - y1) * size.Height / rh);
					if (!region.Contains(new System.Windows.Point((character.X + character.Width / 2) / size.Width, (character.Y + character.Height / 2) / size.Height))) { Flush(); continue; }
					if (!box.IsEmpty && (Math.Abs(character.Y - box.Y) > Math.Max(character.Height, box.Height) * 0.7 || Math.Abs(character.X - box.Right) > Math.Max(character.Height, box.Height) * 1.5)) Flush();
					word.Append(value); if (box.IsEmpty) box = character; else box.Union(character);
				}
				Flush(); return result;
			}
			finally { if (textPage != IntPtr.Zero) PdfiumEngine.FPDFText_ClosePage(textPage); }
		}
	}

	[DllImport("pdfium.dll")]
	private static extern uint FPDFText_GetUnicode(nint textPage, int index);
	[DllImport("pdfium.dll")]
	private static extern bool FPDF_PageToDevice(nint page, int startX, int startY, int sizeX, int sizeY, int rotate,
		double pageX, double pageY, out int deviceX, out int deviceY);

	public bool TryGetRotation(int pageIndex, out int rotation) => _rotations.TryGetValue(pageIndex, out rotation);

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
		// Closing a tab must not wait on the UI thread for an ongoing native render.
		Closed = Task.Run(() =>
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
