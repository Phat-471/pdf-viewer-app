using System;
using System.Net.Http;

namespace PdfViewerApp;

/// <summary>
/// Provides a shared, thread-safe HttpClient instance to avoid socket exhaustion.
/// Configured with standard User-Agent and headers to prevent WAF / firewall blocks.
/// </summary>
public static class HttpHelper
{
    public static readonly string DefaultUserAgent = $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 PDFProDesktop/{ActivationLicense.AppVersion}";

    /// <summary>
    /// Shared HttpClient instance for the entire application.
    /// </summary>
    public static readonly HttpClient Client = CreateConfiguredClient();

    public static HttpClient CreateConfiguredClient(TimeSpan? timeout = null)
    {
        var client = new HttpClient();
        ConfigureHeaders(client);
        if (timeout.HasValue)
        {
            client.Timeout = timeout.Value;
        }
        return client;
    }

    public static void ConfigureHeaders(HttpClient client)
    {
        if (client == null) return;

        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
    }
}
