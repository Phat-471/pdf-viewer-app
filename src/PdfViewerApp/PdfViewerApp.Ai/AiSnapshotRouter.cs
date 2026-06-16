using System.Threading;
using System.Threading.Tasks;

namespace PdfViewerApp.Ai;

internal sealed class AiSnapshotRouter
{
	private readonly AiSettings _settings;

	private readonly IAiSnapshotProvider _geminiProvider;

	private readonly IAiSnapshotProvider _openAiProvider;

	private readonly IAiSnapshotProvider _localProvider = new LocalAiSnapshotProvider();

	public string ActiveProviderName
	{
		get
		{
			IAiSnapshotProvider aiSnapshotProvider = ResolveProvider();
			if (aiSnapshotProvider != _localProvider)
			{
				return aiSnapshotProvider.Name;
			}
			return _localProvider.Name;
		}
	}

	public AiSnapshotRouter(AiSettings? settings = null)
	{
		_settings = settings ?? AiSettings.Load();
		_geminiProvider = new GeminiSnapshotProvider(_settings);
		_openAiProvider = new OpenAiSnapshotProvider(_settings);
	}

	public Task<string> AskSnapshotAsync(AiSnapshotRequest request, CancellationToken cancellationToken)
	{
		var provider = ResolveProvider();
		if (provider == _geminiProvider || provider == _openAiProvider)
		{
			if (!_settings.AllowOnlineSnapshot)
			{
				return Task.FromResult("Chế độ gửi ảnh trực tuyến (Online snapshot) chưa được bật. Vui lòng tích chọn 'Online snapshot' trong phần cấu hình trợ lý AI để phân tích ảnh chụp bằng mô hình đám mây (Gemini/OpenAI).");
			}
		}
		return provider.AskSnapshotAsync(request, cancellationToken);
	}

	private IAiSnapshotProvider ResolveProvider()
	{
		return _settings.ProviderMode switch
		{
			"Gemini" => _geminiProvider, 
			"OpenAI" => _openAiProvider, 
			"Local" => _localProvider, 
			"Off" => _localProvider, 
			_ => _geminiProvider.IsAvailable ? _geminiProvider : (_openAiProvider.IsAvailable ? _openAiProvider : _localProvider), 
		};
	}
}
