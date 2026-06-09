using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PdfViewerApp.UpdateClient;

public sealed class AppUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly string UpdateRootDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PdfPro",
        "Updates");

    private readonly HttpClient _httpClient;
    private readonly Uri _updateEndpoint;
    private readonly string _downloadDirectory;

    public AppUpdateService(HttpClient httpClient, string siteOrEndpointUrl, string? downloadDirectory = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _updateEndpoint = BuildUpdateEndpoint(siteOrEndpointUrl);
        _downloadDirectory = downloadDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PdfPro",
            "Updates");
    }

    public static string GetUpdateRootDirectory()
    {
        return UpdateRootDirectory;
    }

    public static string GetAttemptDirectory(string currentVersion, string targetVersion)
    {
        var attemptId = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N");
        return Path.Combine(
            UpdateRootDirectory,
            $"rollback-{SanitizeFileName(currentVersion)}-to-{SanitizeFileName(targetVersion)}-{attemptId}");
    }

    public static string GetStateFilePath(string attemptDirectory)
    {
        return Path.Combine(attemptDirectory, "rollback-state.json");
    }

    public static string GetConfirmationMarkerPath(string attemptDirectory)
    {
        return Path.Combine(attemptDirectory, "update-confirmed.flag");
    }

    public static string GetBackupZipPath(string attemptDirectory)
    {
        return Path.Combine(attemptDirectory, "previous-install.zip");
    }

    public static async Task<bool> TryConfirmPendingLaunchAsync()
    {
        try
        {
            var state = LoadRollbackState();
            if (state is null || string.IsNullOrWhiteSpace(state.ConfirmationMarkerPath))
            {
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(state.ConfirmationMarkerPath)!);
            await File.WriteAllTextAsync(state.ConfirmationMarkerPath, DateTimeOffset.UtcNow.ToString("O"), Encoding.UTF8)
                .ConfigureAwait(false);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static UpdateRollbackState CreateRollbackState(
        string currentVersion,
        string targetVersion,
        string installDirectory,
        string downloadZipPath)
    {
        var attemptDirectory = GetAttemptDirectory(currentVersion, targetVersion);
        Directory.CreateDirectory(attemptDirectory);

        var state = new UpdateRollbackState
        {
            AttemptId = Path.GetFileName(attemptDirectory),
            CurrentVersion = currentVersion,
            TargetVersion = targetVersion,
            InstallDirectory = installDirectory,
            BackupZipPath = GetBackupZipPath(attemptDirectory),
            DownloadZipPath = downloadZipPath,
            ConfirmationMarkerPath = GetConfirmationMarkerPath(attemptDirectory),
            StateFilePath = GetStateFilePath(attemptDirectory),
            CreatedUtc = DateTimeOffset.UtcNow.ToString("O"),
            AppExecutablePath = Path.Combine(installDirectory, "PdfViewerApp.exe")
        };

        return state;
    }

    public static void SaveRollbackState(UpdateRollbackState state)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(state.StateFilePath))
        {
            throw new InvalidOperationException("Rollback state path is missing.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(state.StateFilePath)!);
        File.WriteAllText(
            state.StateFilePath,
            JsonSerializer.Serialize(state, JsonOptions),
            Encoding.UTF8);
    }

    public static UpdateRollbackState? LoadRollbackState()
    {
        var stateFile = Path.Combine(UpdateRootDirectory, "rollback-state.json");
        if (!File.Exists(stateFile))
        {
            var candidates = Directory.Exists(UpdateRootDirectory)
                ? Directory.GetFiles(UpdateRootDirectory, "rollback-state.json", SearchOption.AllDirectories)
                : Array.Empty<string>();
            stateFile = candidates.Length > 0 ? candidates.OrderByDescending(File.GetLastWriteTimeUtc).First() : stateFile;
        }

        if (!File.Exists(stateFile))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<UpdateRollbackState>(File.ReadAllText(stateFile, Encoding.UTF8), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task<UpdateCheckResult> CheckAsync(string? currentVersion = null, CancellationToken cancellationToken = default)
    {
        var appVersion = ParseVersion(currentVersion ?? GetAppVersion());
        var requestUrl = _updateEndpoint.AbsoluteUri + "?current_version=" + Uri.EscapeDataString(appVersion.ToString());

        using var response = await _httpClient.GetAsync(requestUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var update = await response.Content.ReadFromJsonAsync<UpdateCheckResponse>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (update is null || !update.Success)
        {
            throw new InvalidOperationException("Update server returned an invalid response.");
        }

        var latestVersion = ParseVersion(update.LatestVersion);
        return new UpdateCheckResult
        {
            CurrentVersion = appVersion,
            LatestVersion = latestVersion,
            HasUpdate = latestVersion > appVersion,
            Response = update
        };
    }

    public async Task<UpdateDownloadResult> DownloadAndVerifyAsync(
        UpdateCheckResponse update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(update.DownloadUrl))
        {
            throw new InvalidOperationException("Update download URL is empty.");
        }

        if (!Uri.TryCreate(update.DownloadUrl, UriKind.Absolute, out var downloadUri) ||
            (downloadUri.Scheme != Uri.UriSchemeHttp && downloadUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Update download URL must be http or https.");
        }

        Directory.CreateDirectory(_downloadDirectory);

        var fileName = Path.GetFileName(downloadUri.LocalPath);
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            fileName = $"PdfViewerApp_v{SanitizeFileName(update.LatestVersion)}.zip";
        }

        var finalPath = Path.Combine(_downloadDirectory, fileName);
        var tempPath = finalPath + ".download";
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        using (var response = await _httpClient.GetAsync(downloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                   .ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 128, true);

            var buffer = new byte[1024 * 128];
            long totalRead = 0;
            while (true)
            {
                var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                totalRead += read;

                if (totalBytes is > 0)
                {
                    progress?.Report((double)totalRead / totalBytes.Value);
                }
            }
        }

        var actualSize = new FileInfo(tempPath).Length;
        if (update.FileSize > 0 && actualSize != update.FileSize)
        {
            File.Delete(tempPath);
            throw new InvalidOperationException($"Update size mismatch. Expected {update.FileSize}, got {actualSize}.");
        }

        var actualSha256 = await ComputeSha256Async(tempPath, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(update.Sha256) &&
            !string.Equals(actualSha256, update.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(tempPath);
            throw new InvalidOperationException("Update SHA256 mismatch. The downloaded file is not trusted.");
        }

        if (File.Exists(finalPath))
        {
            File.Delete(finalPath);
        }

        File.Move(tempPath, finalPath);
        progress?.Report(1d);

        return new UpdateDownloadResult
        {
            FilePath = finalPath,
            Size = actualSize,
            Sha256 = actualSha256
        };
    }

    public Task CreateInstallationBackupAsync(
        string installDirectory,
        string backupZipPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                throw new ArgumentException("Install directory is required.", nameof(installDirectory));
            }

            if (!Directory.Exists(installDirectory))
            {
                throw new DirectoryNotFoundException($"Install directory not found: {installDirectory}");
            }

            var backupDirectory = Path.GetDirectoryName(backupZipPath);
            if (!string.IsNullOrWhiteSpace(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
            }

            if (File.Exists(backupZipPath))
            {
                File.Delete(backupZipPath);
            }

            var stagingDirectory = Path.Combine(Path.GetDirectoryName(backupZipPath) ?? UpdateRootDirectory, "_staging");
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
            Directory.CreateDirectory(stagingDirectory);

            try
            {
                var files = Directory.EnumerateFiles(installDirectory, "*", SearchOption.AllDirectories)
                    .Where(path => !ShouldSkipBackupFile(path, installDirectory))
                    .ToList();

                var totalFiles = Math.Max(1, files.Count);
                var copied = 0;
                foreach (var filePath in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var relativePath = Path.GetRelativePath(installDirectory, filePath);
                    var destinationPath = Path.Combine(stagingDirectory, relativePath);
                    var destinationDirectory = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrWhiteSpace(destinationDirectory))
                    {
                        Directory.CreateDirectory(destinationDirectory);
                    }

                    File.Copy(filePath, destinationPath, overwrite: true);
                    copied++;
                    progress?.Report((double)copied / totalFiles);
                }

                ZipFile.CreateFromDirectory(
                    stagingDirectory,
                    backupZipPath,
                    CompressionLevel.Optimal,
                    includeBaseDirectory: false);
            }
            finally
            {
                if (Directory.Exists(stagingDirectory))
                {
                    Directory.Delete(stagingDirectory, recursive: true);
                }
            }
        }, cancellationToken);
    }

    public static async Task RestoreInstallationBackupAsync(
        string backupZipPath,
        string installDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(backupZipPath))
        {
            throw new FileNotFoundException("Rollback backup was not found.", backupZipPath);
        }

        if (Directory.Exists(installDirectory))
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(installDirectory).ToList())
            {
                cancellationToken.ThrowIfCancellationRequested();
                DeletePath(path);
            }
        }
        else
        {
            Directory.CreateDirectory(installDirectory);
        }

        await Task.Run(() =>
        {
            ZipFile.ExtractToDirectory(backupZipPath, installDirectory, overwriteFiles: true);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static void DeletePath(string path)
    {
        if (File.Exists(path))
        {
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
            return;
        }

        if (Directory.Exists(path))
        {
            foreach (var child in Directory.EnumerateFileSystemEntries(path).ToList())
            {
                DeletePath(child);
            }

            Directory.Delete(path, recursive: false);
        }
    }

    private static bool ShouldSkipBackupFile(string filePath, string installDirectory)
    {
        var relativePath = Path.GetRelativePath(installDirectory, filePath);
        var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var segment in segments)
        {
            if (string.Equals(segment, "backups", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "releases", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "_smoke", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "target", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var extension = Path.GetExtension(filePath);
        return string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".download", StringComparison.OrdinalIgnoreCase);
    }

    public static void OpenDownloadedPackage(string zipPath)
    {
        if (!File.Exists(zipPath))
        {
            throw new FileNotFoundException("Downloaded update package was not found.", zipPath);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = zipPath,
            UseShellExecute = true
        });
    }

    private static Uri BuildUpdateEndpoint(string siteOrEndpointUrl)
    {
        if (string.IsNullOrWhiteSpace(siteOrEndpointUrl))
        {
            throw new ArgumentException("Update site URL is required.", nameof(siteOrEndpointUrl));
        }

        var value = siteOrEndpointUrl.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Update site URL must be http or https.", nameof(siteOrEndpointUrl));
        }

        if (value.EndsWith("/wp-json/pdfpro/v1/update-check", StringComparison.OrdinalIgnoreCase))
        {
            return uri;
        }

        return new Uri(value.TrimEnd('/') + "/wp-json/pdfpro/v1/update-check");
    }

    private static string GetAppVersion()
    {
        return Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
            ?? "0.0.0";
    }

    private static Version ParseVersion(string? version)
    {
        var clean = (version ?? string.Empty).Trim().TrimStart('v', 'V');
        if (Version.TryParse(clean, out var parsed))
        {
            return parsed;
        }

        return new Version(0, 0, 0);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(value) ? "update" : value;
    }
}
