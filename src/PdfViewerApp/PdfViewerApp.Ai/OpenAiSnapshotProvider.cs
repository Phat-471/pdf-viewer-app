using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PdfViewerApp.Ai;

internal sealed class OpenAiSnapshotProvider : IAiSnapshotProvider
{
	private static readonly HttpClient HttpClient = new HttpClient();

	private readonly string? _apiKey;

	private readonly string _model;

	public string Name => "OpenAI";

	public bool IsAvailable => !string.IsNullOrWhiteSpace(_apiKey);

	public OpenAiSnapshotProvider(AiSettings? settings = null)
	{
		_apiKey = ((!string.IsNullOrWhiteSpace(settings?.OpenAiApiKey)) ? settings.OpenAiApiKey.Trim() : Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
		_model = Environment.GetEnvironmentVariable("PDFPRO_AI_MODEL") ?? settings?.OpenAiModel ?? "gpt-4.1";
	}

	public async Task<string> AskSnapshotAsync(AiSnapshotRequest request, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(_apiKey))
		{
			return "OpenAI API key chua duoc cau hinh. Hay nhap key trong AI Snapshot hoac cau hinh OPENAI_API_KEY.";
		}
		using HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
		httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
		string text = $"Bạn là trợ lý đọc bản vẽ kỹ thuật/PDF kiến trúc.\nChỉ phân tích vùng snapshot được gửi, không suy đoán ngoài ảnh.\nTrang: {request.PageNumber}\nVùng chọn normalized: x={request.X:0.####}, y={request.Y:0.####}, width={request.Width:0.####}, height={request.Height:0.####}\nCâu hỏi người dùng:\n{request.Prompt}";
		string content = JsonSerializer.Serialize(new
		{
			model = _model,
			input = new object[1]
			{
				new
				{
					role = "user",
					content = new object[2]
					{
						new
						{
							type = "input_text",
							text = text
						},
						new
						{
							type = "input_image",
							image_url = "data:image/png;base64," + request.PngBase64,
							detail = "high"
						}
					}
				}
			}
		});
		httpRequest.Content = new StringContent(content, Encoding.UTF8, "application/json");
		using HttpResponseMessage response = await HttpClient.SendAsync(httpRequest, cancellationToken);
		string text2 = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			return $"Online AI lỗi HTTP {(int)response.StatusCode}: {text2}";
		}
		return ExtractOutputText(text2);
	}

	private static string ExtractOutputText(string responseText)
	{
		using JsonDocument jsonDocument = JsonDocument.Parse(responseText);
		if (jsonDocument.RootElement.TryGetProperty("output_text", out var value) && value.ValueKind == JsonValueKind.String)
		{
			return value.GetString() ?? string.Empty;
		}
		if (jsonDocument.RootElement.TryGetProperty("output", out var value2) && value2.ValueKind == JsonValueKind.Array)
		{
			string[] array = (from content in value2.EnumerateArray().SelectMany((JsonElement item) => (IEnumerable<JsonElement>)((!item.TryGetProperty("content", out var value3) || value3.ValueKind != JsonValueKind.Array) ? Enumerable.Empty<JsonElement>() : ((object)value3.EnumerateArray())))
				where content.TryGetProperty("type", out var value3) && value3.GetString() == "output_text" && content.TryGetProperty("text", out var _)
				select content.GetProperty("text").GetString() into text
				where !string.IsNullOrWhiteSpace(text)
				select text).ToArray();
			if (array.Length != 0)
			{
				return string.Join(Environment.NewLine, array);
			}
		}
		return responseText;
	}
}
