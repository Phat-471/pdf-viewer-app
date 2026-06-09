# PdfViewerApp Update Client

This folder contains the app-side update client that matches the WordPress endpoint:

`/wp-json/pdfpro/v1/update-check`

## What It Adds

- Posts the current app version to the WordPress update endpoint.
- Compares `latest_version` against the running app version.
- Supports `mandatory` updates from the server.
- Downloads the update ZIP into `%LOCALAPPDATA%\PdfPro\Updates`.
- Verifies `file_size` and `sha256` before trusting the downloaded ZIP.
- Opens the verified ZIP with Windows shell so the existing installer/update flow can continue.

## Files

- `AppUpdateModels.cs`
- `AppUpdateService.cs`

## WPF Integration Example

Copy the files into:

`src\PdfViewerApp\Services\Update`

Then call from a button or startup check:

```csharp
using PdfViewerApp.UpdateClient;

private static readonly HttpClient UpdateHttpClient = new()
{
    Timeout = TimeSpan.FromSeconds(60)
};

private async Task CheckForUpdatesAsync()
{
    var updater = new AppUpdateService(UpdateHttpClient, "https://your-site.com");
    var result = await updater.CheckAsync();

    if (!result.HasUpdate)
    {
        MessageBox.Show("You are using the latest version.");
        return;
    }

    var message = $"Version {result.Response.LatestVersion} is available.\n\n{result.Response.Changelog}";
    if (result.Response.Mandatory)
    {
        message = "This update is required.\n\n" + message;
    }

    if (MessageBox.Show(message, "Update Available", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
    {
        return;
    }

    var downloaded = await updater.DownloadAndVerifyAsync(result.Response);
    AppUpdateService.OpenDownloadedPackage(downloaded.FilePath);
}
```

## Required Server Fields

The app expects these fields from WordPress:

- `success`
- `latest_version`
- `download_url`
- `sha256`
- `file_size`
- `release_date`
- `mandatory`
- `changelog`
