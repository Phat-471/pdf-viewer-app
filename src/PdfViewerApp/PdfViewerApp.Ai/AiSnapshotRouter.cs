using System;
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

	public async Task<string> AskSnapshotAsync(AiSnapshotRequest request, CancellationToken cancellationToken)
	{
		var provider = ResolveProvider();
		bool isOnline = (provider == _geminiProvider || provider == _openAiProvider);
		if (isOnline)
		{
			if (!_settings.AllowOnlineSnapshot)
			{
				if (_localProvider.IsAvailable && _settings.ProviderMode == "Auto")
				{
					return await _localProvider.AskSnapshotAsync(request, cancellationToken);
				}
				return "Chế độ gửi ảnh trực tuyến (Online snapshot) chưa được bật. Vui lòng tích chọn 'Online snapshot' trong phần cấu hình trợ lý AI để phân tích ảnh chụp bằng mô hình đám mây (Gemini/OpenAI).";
			}
		}

		if (_settings.ProviderMode == "Auto" && isOnline)
		{
			try
			{
				string result = await provider.AskSnapshotAsync(request, cancellationToken);
				if (result.Contains("lỗi HTTP", StringComparison.OrdinalIgnoreCase) || 
				    result.Contains("model error", StringComparison.OrdinalIgnoreCase) || 
				    result.Contains("chua duoc cau hinh", StringComparison.OrdinalIgnoreCase))
				{
					if (_localProvider.IsAvailable)
					{
						return await _localProvider.AskSnapshotAsync(request, cancellationToken);
					}
				}
				return result;
			}
			catch
			{
				if (_localProvider.IsAvailable)
				{
					try
					{
						return await _localProvider.AskSnapshotAsync(request, cancellationToken);
					}
					catch
					{
						// Ignore and throw original or show error
					}
				}
				throw;
			}
		}
		return await provider.AskSnapshotAsync(request, cancellationToken);
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
