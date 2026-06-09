using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PdfViewerApp.Ai;

internal sealed class GeminiModelDiscoveryService
{
	private static readonly HttpClient HttpClient = new HttpClient();

	private readonly string _apiKey;

	private readonly string _configuredModel;

	private string? _cachedModel;

	public GeminiModelDiscoveryService(string apiKey, string configuredModel = "auto")
	{
		_apiKey = apiKey;
		_configuredModel = configuredModel;
	}

	public async Task<string> GetBestModelAsync(bool forceRefresh, CancellationToken cancellationToken)
	{
		string text = Environment.GetEnvironmentVariable("PDFPRO_GEMINI_MODEL") ?? _configuredModel;
		if (!string.Equals(text, "auto", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(text))
		{
			return NormalizeModelName(text);
		}
		if (!forceRefresh && !string.IsNullOrWhiteSpace(_cachedModel))
		{
			return _cachedModel;
		}
		using HttpResponseMessage response = await HttpClient.GetAsync("https://generativelanguage.googleapis.com/v1beta/models?key=" + Uri.EscapeDataString(_apiKey), cancellationToken);
		string text2 = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"Gemini list models failed HTTP {(int)response.StatusCode}: {text2}");
		}
		using JsonDocument jsonDocument = JsonDocument.Parse(text2);
		if (!jsonDocument.RootElement.TryGetProperty("models", out var value) || value.ValueKind != JsonValueKind.Array)
		{
			throw new InvalidOperationException("Gemini list models returned no models.");
		}
		List<GeminiModelInfo> list = new List<GeminiModelInfo>();
		foreach (JsonElement item in value.EnumerateArray())
		{
			JsonElement value2;
			string text3 = (item.TryGetProperty("name", out value2) ? value2.GetString() : null);
			if (!string.IsNullOrWhiteSpace(text3) && text3.Contains("gemini", StringComparison.OrdinalIgnoreCase) && item.TryGetProperty("supportedGenerationMethods", out var value3) && value3.ValueKind == JsonValueKind.Array && value3.EnumerateArray().Any((JsonElement method) => method.GetString() == "generateContent"))
			{
				JsonElement value4;
				string displayName = (item.TryGetProperty("displayName", out value4) ? (value4.GetString() ?? string.Empty) : string.Empty);
				JsonElement value5;
				string description = (item.TryGetProperty("description", out value5) ? (value5.GetString() ?? string.Empty) : string.Empty);
				list.Add(new GeminiModelInfo(text3, displayName, description, ScoreModel(text3)));
			}
		}
		_cachedModel = (from model in list
			orderby model.Score descending, model.Name
			select model).FirstOrDefault()?.Name ?? "models/gemini-2.0-flash";
		return _cachedModel;
	}

	public async Task<IReadOnlyList<GeminiModelInfo>> ListSupportedModelsAsync(CancellationToken cancellationToken)
	{
		using HttpResponseMessage response = await HttpClient.GetAsync("https://generativelanguage.googleapis.com/v1beta/models?key=" + Uri.EscapeDataString(_apiKey), cancellationToken);
		string text = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"Gemini list models failed HTTP {(int)response.StatusCode}: {text}");
		}
		using JsonDocument jsonDocument = JsonDocument.Parse(text);
		if (!jsonDocument.RootElement.TryGetProperty("models", out var value) || value.ValueKind != JsonValueKind.Array)
		{
			return Array.Empty<GeminiModelInfo>();
		}
		List<GeminiModelInfo> list = new List<GeminiModelInfo>();
		foreach (JsonElement item in value.EnumerateArray())
		{
			JsonElement value2;
			string text2 = (item.TryGetProperty("name", out value2) ? value2.GetString() : null);
			if (!string.IsNullOrWhiteSpace(text2) && text2.Contains("gemini", StringComparison.OrdinalIgnoreCase) && item.TryGetProperty("supportedGenerationMethods", out var value3) && value3.ValueKind == JsonValueKind.Array && value3.EnumerateArray().Any((JsonElement method) => method.GetString() == "generateContent"))
			{
				JsonElement value4;
				string displayName = (item.TryGetProperty("displayName", out value4) ? (value4.GetString() ?? string.Empty) : string.Empty);
				JsonElement value5;
				string description = (item.TryGetProperty("description", out value5) ? (value5.GetString() ?? string.Empty) : string.Empty);
				list.Add(new GeminiModelInfo(text2, displayName, description, ScoreModel(text2)));
			}
		}
		return (from model in list
			orderby model.Score descending, model.Name
			select model).ToArray();
	}

	public void ClearCache()
	{
		_cachedModel = null;
	}

	private static string NormalizeModelName(string model)
	{
		if (!model.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
		{
			return "models/" + model;
		}
		return model;
	}

	private static int ScoreModel(string model)
	{
		string text = model.ToLowerInvariant();
		int num = 0;
		if (text.Contains("2.5"))
		{
			num += 500;
		}
		if (text.Contains("2.0"))
		{
			num += 400;
		}
		if (text.Contains("1.5"))
		{
			num += 250;
		}
		if (text.Contains("flash"))
		{
			num += 220;
		}
		if (text.Contains("flash-lite"))
		{
			num += 260;
		}
		if (text.Contains("lite"))
		{
			num += 180;
		}
		if (text.Contains("pro"))
		{
			num += 40;
		}
		if (text.Contains("latest"))
		{
			num += 80;
		}
		if (text.Contains("vision"))
		{
			num += 60;
		}
		if (text.Contains("preview"))
		{
			num -= 20;
		}
		if (text.Contains("experimental"))
		{
			num -= 80;
		}
		if (text.Contains("embedding"))
		{
			num -= 500;
		}
		if (text.Contains("image-generation"))
		{
			num -= 500;
		}
		return num;
	}
}
