using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace PdfViewerApp.UpdateClient;

public sealed class AppUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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

    public async Task<UpdateCheckResult> CheckAsync(string? currentVersion = null, CancellationToken cancellationToken = default)
    {
        var appVersion = ParseVersion(currentVersion ?? GetAppVersion());
        var request = new UpdateCheckRequest
        {
            CurrentVersion = appVersion.ToString()
        };

        using var response = await _httpClient.PostAsJsonAsync(_updateEndpoint, request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
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
