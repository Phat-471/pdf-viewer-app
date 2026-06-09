using System;

namespace PdfViewerApp;

internal sealed class ActivationState
{
	public string AppVersion { get; init; } = string.Empty;

	public string MachineId { get; init; } = string.Empty;

	public bool IsActivated { get; init; }

	public string ActivationKey { get; init; } = string.Empty;

	public DateTimeOffset? ActivatedAt { get; init; }

	public string LicensePath { get; init; } = string.Empty;

	public string StatusText { get; init; } = string.Empty;

	public DateTimeOffset? ExpiresAt { get; init; }

	public string ExpirationText { get; init; } = string.Empty;
}
