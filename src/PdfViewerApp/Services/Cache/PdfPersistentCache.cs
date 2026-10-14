using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PdfViewerApp;

// Optional, bounded local cache. A cache failure never prevents reading the PDF.
internal static class PdfPersistentCache
{
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PdfPro", "document-cache-v2");
    private static int _pendingWrites;
    private static readonly object WriteLock = new();

    internal static string Fingerprint(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string CachePath(string key, string extension) => Path.Combine(Folder,
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + extension);

    internal static BitmapSource? ReadBitmap(string key)
    {
        try
        {
            string path = CachePath(key, ".png");
            if (!File.Exists(path)) return null;
            using var stream = File.OpenRead(path);
            var image = new BitmapImage();
            image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze();
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            return image;
        }
        catch { return null; }
    }

    internal static string? ReadText(string key)
    {
        try { string path = CachePath(key, ".json"); if (!File.Exists(path)) return null; File.SetLastWriteTimeUtc(path, DateTime.UtcNow); return File.ReadAllText(path); }
        catch { return null; }
    }

    internal static void WriteText(string key, string value) => Write(key, ".json", stream =>
    { using var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true); writer.Write(value); });

    internal static void QueueBitmap(string key, BitmapSource bitmap)
    {
        if (!bitmap.IsFrozen || Interlocked.Increment(ref _pendingWrites) > 3)
        { if (bitmap.IsFrozen) Interlocked.Decrement(ref _pendingWrites); return; }
        _ = Task.Run(() =>
        {
            try { Write(key, ".png", stream => { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(stream); }); }
            finally { Interlocked.Decrement(ref _pendingWrites); }
        });
    }

    private static void Write(string key, string extension, Action<Stream> save)
    {
        string? temporary = null;
        try
        {
            lock (WriteLock)
            {
                Directory.CreateDirectory(Folder);
                string path = CachePath(key, extension);
                temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (var stream = File.Create(temporary)) save(stream);
                File.Move(temporary, path, overwrite: true); temporary = null;
                long total = 0;
                foreach (var file in new DirectoryInfo(Folder).EnumerateFiles().Where(f => f.Extension is ".png" or ".json").OrderByDescending(f => f.LastWriteTimeUtc))
                { total += file.Length; if (total > 256L * 1024 * 1024) { try { file.Delete(); } catch { } } }
            }
        }
        catch (Exception ex) { PdfPerfLogger.Log("Disk cache: " + ex.Message); }
        finally { if (temporary != null) { try { File.Delete(temporary); } catch { } } }
    }
}
