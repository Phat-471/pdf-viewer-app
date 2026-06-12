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
		object payloadContents;

		if (request.History != null && request.History.Count > 0)
		{
			var list = new List<object>();
			for (int i = 0; i < request.History.Count; i++)
			{
				var msg = request.History[i];
				bool isUser = string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase);

				if (i == 0)
				{
					string systemContext = $"Bạn là trợ lý đọc bản vẽ kỹ thuật/PDF kiến trúc.\nChỉ phân tích vùng snapshot/trang được gửi, không suy đoán ngoài ảnh.\nTrang: {request.PageNumber}\n";
					if (request.Width < 0.99)
					{
						systemContext += $"Vùng chọn normalized: x={request.X:0.####}, y={request.Y:0.####}, width={request.Width:0.####}, height={request.Height:0.####}\n";
					}
					string firstPrompt = systemContext + "Câu hỏi:\n" + msg.Text;

					var partsList = new List<object> { new { text = firstPrompt } };
					string? imgData = msg.ImageBase64 ?? request.PngBase64;
					if (!string.IsNullOrWhiteSpace(imgData))
					{
						partsList.Add(new
						{
							inline_data = new
							{
								mime_type = "image/png",
								data = imgData
							}
						});
					}

					list.Add(new
					{
						role = "user",
						parts = partsList.ToArray()
					});
				}
				else
				{
					if (!string.IsNullOrEmpty(msg.ImageBase64))
					{
						list.Add(new
						{
							role = isUser ? "user" : "model",
							parts = new object[]
							{
								new { text = msg.Text },
								new
								{
									inline_data = new
									{
										mime_type = "image/png",
										data = msg.ImageBase64
									}
								}
							}
						});
					}
					else
					{
						list.Add(new
						{
							role = isUser ? "user" : "model",
							parts = new object[]
							{
								new { text = msg.Text }
							}
						});
					}
				}
			}
			payloadContents = list.ToArray();
		}
		else
		{
			string systemContext = $"Bạn là trợ lý đọc bản vẽ kỹ thuật/PDF kiến trúc.\nChỉ phân tích vùng snapshot/trang được gửi, không suy đoán ngoài ảnh.\nTrang: {request.PageNumber}\n";
			if (request.Width < 0.99)
			{
				systemContext += $"Vùng chọn normalized: x={request.X:0.####}, y={request.Y:0.####}, width={request.Width:0.####}, height={request.Height:0.####}\n";
			}
			string text = systemContext + "Câu hỏi người dùng:\n" + request.Prompt;

			var partsList = new List<object> { new { text } };
			if (!string.IsNullOrWhiteSpace(request.PngBase64))
			{
				partsList.Add(new
				{
					inline_data = new
					{
						mime_type = "image/png",
						data = request.PngBase64
					}
				});
			}

			payloadContents = new object[]
			{
				new
				{
					role = "user",
					parts = partsList.ToArray()
				}
			};
		}

		var value = new
		{
			contents = payloadContents
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
