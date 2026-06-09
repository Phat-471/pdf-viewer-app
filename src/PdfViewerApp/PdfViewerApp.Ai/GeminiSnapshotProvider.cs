using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PdfViewerApp.Ai;

internal sealed class GeminiSnapshotProvider : IAiSnapshotProvider
{
	private static readonly HttpClient HttpClient = new HttpClient();

	private readonly string? _apiKey;

	private readonly GeminiModelDiscoveryService? _modelDiscovery;

	public string Name => "Gemini";

	public bool IsAvailable => !string.IsNullOrWhiteSpace(_apiKey);

	public GeminiSnapshotProvider(AiSettings? settings = null)
	{
		_apiKey = ((!string.IsNullOrWhiteSpace(settings?.GeminiApiKey)) ? settings.GeminiApiKey.Trim() : Environment.GetEnvironmentVariable("GEMINI_API_KEY"));
		if (!string.IsNullOrWhiteSpace(_apiKey))
		{
			_modelDiscovery = new GeminiModelDiscoveryService(_apiKey, settings?.GeminiModel ?? "auto");
		}
	}

	public async Task<string> AskSnapshotAsync(AiSnapshotRequest request, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(_apiKey) || _modelDiscovery == null)
		{
			return "Gemini API key chua duoc cau hinh. Hay nhap key trong AI Snapshot hoac cau hinh GEMINI_API_KEY.";
		}
		string text = await GenerateContentAsync(await _modelDiscovery.GetBestModelAsync(forceRefresh: false, cancellationToken), request, cancellationToken);
		if (text.StartsWith("Gemini model error:", StringComparison.OrdinalIgnoreCase))
		{
			_modelDiscovery.ClearCache();
			text = await GenerateContentAsync(await _modelDiscovery.GetBestModelAsync(forceRefresh: true, cancellationToken), request, cancellationToken);
		}
		return text;
	}

	private async Task<string> GenerateContentAsync(string model, AiSnapshotRequest request, CancellationToken cancellationToken)
	{
		string text = $"Bạn là trợ lý đọc bản vẽ kỹ thuật/PDF kiến trúc.\nChỉ phân tích vùng snapshot được gửi, không suy đoán ngoài ảnh.\nTrang: {request.PageNumber}\nVùng chọn normalized: x={request.X:0.####}, y={request.Y:0.####}, width={request.Width:0.####}, height={request.Height:0.####}\nCâu hỏi người dùng:\n{request.Prompt}";
		var value = new
		{
			contents = new object[1]
			{
				new
				{
					parts = new object[2]
					{
						new { text },
						new
						{
							inline_data = new
							{
								mime_type = "image/png",
								data = request.PngBase64
							}
						}
					}
				}
			}
		};
		string requestUri = "https://generativelanguage.googleapis.com/v1beta/" + model + ":generateContent?key=" + Uri.EscapeDataString(_apiKey);
		using HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri);
		httpRequest.Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
		using HttpResponseMessage response = await HttpClient.SendAsync(httpRequest, cancellationToken);
		string text2 = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			if (response.StatusCode == HttpStatusCode.NotFound || text2.Contains("not found", StringComparison.OrdinalIgnoreCase))
			{
				return "Gemini model error: " + text2;
			}
			return $"Gemini lỗi HTTP {(int)response.StatusCode}: {text2}";
		}
		return ExtractGeminiText(text2);
	}

	private static string ExtractGeminiText(string responseText)
	{
		using JsonDocument jsonDocument = JsonDocument.Parse(responseText);
		if (!jsonDocument.RootElement.TryGetProperty("candidates", out var value) || value.ValueKind != JsonValueKind.Array)
		{
			return responseText;
		}
		string[] array = (from part in value.EnumerateArray().SelectMany((JsonElement candidate) => (IEnumerable<JsonElement>)((!candidate.TryGetProperty("content", out var value2) || !value2.TryGetProperty("parts", out var value3) || value3.ValueKind != JsonValueKind.Array) ? Enumerable.Empty<JsonElement>() : ((object)value3.EnumerateArray())))
			where part.TryGetProperty("text", out var _)
			select part.GetProperty("text").GetString() into text
			where !string.IsNullOrWhiteSpace(text)
			select text).ToArray();
		return (array.Length != 0) ? string.Join(Environment.NewLine, array) : responseText;
	}
}
