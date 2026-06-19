using System.Net.Http;

namespace PdfViewerApp;

/// <summary>
/// Provides a shared, thread-safe HttpClient instance to avoid socket exhaustion.
/// </summary>
public static class HttpHelper
{
    /// <summary>
    /// Shared HttpClient instance for the entire application.
    /// </summary>
    public static readonly HttpClient Client = new HttpClient();
}
