using System;

namespace PdfViewerApp;

internal sealed class ActivationRecord
{
	public string ActivationKey { get; set; } = string.Empty;

	public string MachineId { get; set; } = string.Empty;

	public string Payload { get; set; } = string.Empty;

	public string Signature { get; set; } = string.Empty;

	public string PublicKey { get; set; } = string.Empty;

	public string Edition { get; set; } = string.Empty;

	public DateTimeOffset ActivatedAt { get; set; }
}
