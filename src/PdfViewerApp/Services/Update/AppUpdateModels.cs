using System;
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

/// <summary>
/// Lưu trạng thái bản cập nhật đã tải ngầm, sẵn sàng cài đặt khi người dùng đóng ứng dụng.
/// </summary>
public sealed class SilentUpdateReadyState
{
    [JsonPropertyName("target_version")]
    public string TargetVersion { get; init; } = string.Empty;

    [JsonPropertyName("download_zip_path")]
    public string DownloadZipPath { get; init; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; init; } = string.Empty;

    [JsonPropertyName("download_url")]
    public string DownloadUrl { get; init; } = string.Empty;

    [JsonPropertyName("changelog")]
    public string Changelog { get; init; } = string.Empty;

    [JsonPropertyName("downloaded_utc")]
    public string DownloadedUtc { get; init; } = string.Empty;
}

public sealed class UpdateRollbackState
{
    [JsonPropertyName("attempt_id")]
    public string AttemptId { get; init; } = string.Empty;

    [JsonPropertyName("current_version")]
    public string CurrentVersion { get; init; } = string.Empty;

    [JsonPropertyName("target_version")]
    public string TargetVersion { get; init; } = string.Empty;

    [JsonPropertyName("install_directory")]
    public string InstallDirectory { get; init; } = string.Empty;

    [JsonPropertyName("backup_zip_path")]
    public string BackupZipPath { get; init; } = string.Empty;

    [JsonPropertyName("download_zip_path")]
    public string DownloadZipPath { get; init; } = string.Empty;

    [JsonPropertyName("confirmation_marker_path")]
    public string ConfirmationMarkerPath { get; init; } = string.Empty;

    [JsonPropertyName("state_file_path")]
    public string StateFilePath { get; init; } = string.Empty;

    [JsonPropertyName("created_utc")]
    public string CreatedUtc { get; init; } = string.Empty;

    [JsonPropertyName("app_executable_path")]
    public string AppExecutablePath { get; init; } = string.Empty;
}
