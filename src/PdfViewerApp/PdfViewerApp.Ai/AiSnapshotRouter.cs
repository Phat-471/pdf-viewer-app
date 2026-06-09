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
		if (!_settings.AllowOnlineSnapshot)
		{
			return _localProvider.AskSnapshotAsync(request, cancellationToken);
		}
		return ResolveProvider().AskSnapshotAsync(request, cancellationToken);
	}

	private IAiSnapshotProvider ResolveProvider()
	{
		return _settings.ProviderMode switch
		{
			"Gemini" => _geminiProvider.IsAvailable ? _geminiProvider : _localProvider, 
			"OpenAI" => _openAiProvider.IsAvailable ? _openAiProvider : _localProvider, 
			"Local" => _localProvider, 
			"Off" => _localProvider, 
			_ => _geminiProvider.IsAvailable ? _geminiProvider : (_openAiProvider.IsAvailable ? _openAiProvider : _localProvider), 
		};
	}
}
