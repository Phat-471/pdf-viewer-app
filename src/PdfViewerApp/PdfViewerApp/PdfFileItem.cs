using System.IO;

namespace PdfViewerApp;

public class PdfFileItem
{
	public string FullPath { get; set; } = string.Empty;

	public long SizeBytes { get; set; }

	public string FileName => Path.GetFileName(FullPath);

	public string DisplayName => FileName + " (" + FormatBytes(SizeBytes) + ")";

	public static PdfFileItem FromPath(string path)
	{
		long sizeBytes = 0L;
		try
		{
			sizeBytes = new FileInfo(path).Length;
		}
		catch
		{
		}
		return new PdfFileItem
		{
			FullPath = path,
			SizeBytes = sizeBytes
		};
	}

	private static string FormatBytes(long bytes)
	{
		string[] array = new string[4] { "B", "KB", "MB", "GB" };
		double num = bytes;
		int num2 = 0;
		while (num >= 1024.0 && num2 < array.Length - 1)
		{
			num /= 1024.0;
			num2++;
		}
		return $"{num:0.##} {array[num2]}";
	}
}
