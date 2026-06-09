using System.Text.Json.Serialization;

namespace PdfViewerApp.UpdateClient;

public sealed class UpdateCheckRequest
{
    [JsonPropertyName("current_version")]
    public string CurrentVersion { get; init; } = string.Empty;
}

public sealed class UpdateCheckResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("latest_version")]
    public string LatestVersion { get; init; } = string.Empty;

    [JsonPropertyName("download_url")]
    public string DownloadUrl { get; init; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; init; } = string.Empty;

    [JsonPropertyName("file_size")]
    public long FileSize { get; init; }

    [JsonPropertyName("release_date")]
    public string ReleaseDate { get; init; } = string.Empty;

    [JsonPropertyName("mandatory")]
    public bool Mandatory { get; init; }

    [JsonPropertyName("changelog")]
    public string Changelog { get; init; } = string.Empty;
}

public sealed class UpdateCheckResult
{
    public bool HasUpdate { get; init; }

    public Version CurrentVersion { get; init; } = new(0, 0, 0);

    public Version LatestVersion { get; init; } = new(0, 0, 0);

    public UpdateCheckResponse Response { get; init; } = new();
}

public sealed class UpdateDownloadResult
{
    public string FilePath { get; init; } = string.Empty;

    public long Size { get; init; }

    public string Sha256 { get; init; } = string.Empty;
}
