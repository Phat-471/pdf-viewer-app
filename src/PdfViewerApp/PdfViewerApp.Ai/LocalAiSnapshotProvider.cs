using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PdfViewerApp.Ai;

internal sealed class LocalAiSnapshotProvider : IAiSnapshotProvider
{
	private static readonly HttpClient HttpClient = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(30.0)
	};

	public string Name => "Local";

	public bool IsAvailable
	{
		get
		{
			try
			{
				using (HttpClient.GetAsync("http://localhost:11434").Result)
				{
					return true;
				}
			}
			catch
			{
				return false;
			}
		}
	}

	public async Task<string> AskSnapshotAsync(AiSnapshotRequest request, CancellationToken cancellationToken)
	{
		if (!IsAvailable)
		{
			return "Local AI (Ollama) chưa khởi chạy hoặc chưa được cài đặt. Vui lòng mở Ollama hoặc đảm bảo máy chủ chạy tại http://localhost:11434.";
		}
		string prompt = $"Bạn là trợ lý đọc bản vẽ kỹ thuật/PDF kiến trúc.\nChỉ phân tích vùng snapshot được gửi, không suy đoán ngoài ảnh.\nTrang: {request.PageNumber}\nVùng chọn normalized: x={request.X:0.####}, y={request.Y:0.####}, width={request.Width:0.####}, height={request.Height:0.####}\nCâu hỏi người dùng:\n{request.Prompt}";
		var value = new
		{
			model = "qwen2.5:1.5b",
			prompt = prompt,
			images = new string[1] { request.PngBase64 },
			stream = false
		};
		try
		{
			string requestUri = "http://localhost:11434/api/generate";
			using HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri);
			httpRequest.Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
			using HttpResponseMessage response = await HttpClient.SendAsync(httpRequest, cancellationToken);
			string text = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				return $"Ollama lỗi HTTP {(int)response.StatusCode}: {text}";
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(text);
			if (jsonDocument.RootElement.TryGetProperty("response", out var value2))
			{
				return value2.GetString() ?? text;
			}
			return text;
		}
		catch (Exception ex)
		{
			return "Lỗi kết nối Local AI: " + ex.Message;
		}
	}
}
