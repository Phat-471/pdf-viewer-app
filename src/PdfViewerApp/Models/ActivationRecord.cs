using System;

namespace PdfViewerApp;

internal sealed class ActivationRecord
{
	public string ActivationKey { get; set; } = string.Empty;

	public string MachineId { get; set; } = string.Empty;

	public string ExpiresAt { get; set; } = "never";

	public string Status { get; set; } = "activated";

	public string Edition { get; set; } = string.Empty;

	public DateTimeOffset ActivatedAt { get; set; }

	public DateTimeOffset LastOnlineCheckTime { get; set; } = DateTimeOffset.Now;
}
