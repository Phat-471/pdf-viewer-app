using System.Threading;
using System.Threading.Tasks;

namespace PdfViewerApp.Ai;

internal interface IAiSnapshotProvider
{
	string Name { get; }

	bool IsAvailable { get; }

	Task<string> AskSnapshotAsync(AiSnapshotRequest request, CancellationToken cancellationToken);
}
