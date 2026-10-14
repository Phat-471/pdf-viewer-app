#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace PdfViewerApp.UpdateClient;

internal static class UpdateInstallerScript
{
    internal const string LaunchArgument = "--pdfpro-update-launch";

    internal static string Read()
    {
        using var stream = typeof(UpdateInstallerScript).Assembly.GetManifestResourceStream("PdfViewerApp.UpdateInstaller.ps1")
            ?? throw new InvalidOperationException("Không tìm được thành phần cài đặt cập nhật.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    internal static bool IsInstallationBusy(string directory)
    {
        string path = Path.GetFullPath(directory).TrimEnd('\\', '/').ToLowerInvariant();
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..16].ToLowerInvariant();
        using var mutex = new Mutex(false, "Local\\PdfPro.UpdateInstaller." + key);
        bool acquired;
        try { acquired = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { acquired = true; }
        if (acquired) mutex.ReleaseMutex();
        return !acquired;
    }
}
