using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PdfViewerApp.Ai;

internal static class AiSystemCheckService
{
	private struct MEMORYSTATUSEX
	{
		public uint dwLength;

		public uint dwMemoryLoad;

		public ulong ullTotalPhys;

		public ulong ullAvailPhys;

		public ulong ullTotalPageFile;

		public ulong ullAvailPageFile;

		public ulong ullTotalVirtual;

		public ulong ullAvailVirtual;

		public ulong ullAvailExtendedVirtual;
	}

	private static readonly HttpClient HttpClient = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(2.0)
	};

	public static Task<string> BuildReportAsync(CancellationToken cancellationToken)
	{
		return BuildReportAsync(AiSettings.Load(), cancellationToken);
	}

	public static async Task<string> BuildReportAsync(AiSettings settings, CancellationToken cancellationToken)
	{
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("PDF Pro - AI System Check");
		sb.AppendLine();
		StringBuilder stringBuilder = sb;
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder);
		handler.AppendLiteral("CPU cores: ");
		handler.AppendFormatted(Environment.ProcessorCount);
		stringBuilder2.AppendLine(ref handler);
		stringBuilder = sb;
		StringBuilder stringBuilder3 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(8, 1, stringBuilder);
		handler.AppendLiteral("RAM: ");
		handler.AppendFormatted(GetTotalRamGb(), "0.0");
		handler.AppendLiteral(" GB");
		stringBuilder3.AppendLine(ref handler);
		stringBuilder = sb;
		StringBuilder stringBuilder4 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(12, 1, stringBuilder);
		handler.AppendLiteral("Gemini key: ");
		handler.AppendFormatted(HasKey(settings.GeminiApiKey, "GEMINI_API_KEY") ? "configured" : "missing");
		stringBuilder4.AppendLine(ref handler);
		stringBuilder = sb;
		StringBuilder stringBuilder5 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(12, 1, stringBuilder);
		handler.AppendLiteral("OpenAI key: ");
		handler.AppendFormatted(HasKey(settings.OpenAiApiKey, "OPENAI_API_KEY") ? "configured" : "missing");
		stringBuilder5.AppendLine(ref handler);
		stringBuilder = sb;
		StringBuilder stringBuilder6 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder);
		handler.AppendLiteral("Gemini model: ");
		handler.AppendFormatted(settings.GeminiModel);
		stringBuilder6.AppendLine(ref handler);
		stringBuilder = sb;
		StringBuilder stringBuilder7 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder);
		handler.AppendLiteral("OpenAI model: ");
		handler.AppendFormatted(settings.OpenAiModel);
		stringBuilder7.AppendLine(ref handler);
		sb.AppendLine();
		sb.AppendLine("Gemini supported models:");
		StringBuilder stringBuilder8 = sb;
		stringBuilder8.AppendLine(await BuildGeminiModelReportAsync(settings, cancellationToken));
		sb.AppendLine();
		sb.AppendLine("Local AI:");
		stringBuilder8 = sb;
		stringBuilder8.AppendLine(await CheckOllamaAsync(cancellationToken));
		sb.AppendLine();
		sb.AppendLine("Recommended profile:");
		sb.AppendLine(GetRecommendedProfile());
		return sb.ToString();
	}

	private static bool HasKey(string settingsValue, string environmentName)
	{
		if (string.IsNullOrWhiteSpace(settingsValue))
		{
			return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(environmentName));
		}
		return true;
	}

	private static async Task<string> CheckOllamaAsync(CancellationToken cancellationToken)
	{
		_ = 1;
		try
		{
			using HttpResponseMessage response = await HttpClient.GetAsync("http://localhost:11434/api/tags", cancellationToken);
			string text = await response.Content.ReadAsStringAsync(cancellationToken);
			return response.IsSuccessStatusCode ? ("Ollama: running at http://localhost:11434\n" + text) : $"Ollama: HTTP {(int)response.StatusCode}";
		}
		catch
		{
			return "Ollama: not detected. Install from https://ollama.com/download if local AI is needed.";
		}
	}

	private static async Task<string> BuildGeminiModelReportAsync(AiSettings settings, CancellationToken cancellationToken)
	{
		string text = ((!string.IsNullOrWhiteSpace(settings.GeminiApiKey)) ? settings.GeminiApiKey.Trim() : Environment.GetEnvironmentVariable("GEMINI_API_KEY"));
		if (string.IsNullOrWhiteSpace(text))
		{
			return "Gemini: missing API key. Cannot auto-load supported models.";
		}
		try
		{
			GeminiModelDiscoveryService discovery = new GeminiModelDiscoveryService(text, settings.GeminiModel);
			string selected = await discovery.GetBestModelAsync(forceRefresh: true, cancellationToken);
			IReadOnlyList<GeminiModelInfo> readOnlyList = await discovery.ListSupportedModelsAsync(cancellationToken);
			StringBuilder stringBuilder = new StringBuilder();
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(27, 1, stringBuilder2);
			handler.AppendLiteral("Selected economical model: ");
			handler.AppendFormatted(selected);
			stringBuilder3.AppendLine(ref handler);
			foreach (GeminiModelInfo item in readOnlyList.Take(8))
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder4 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(14, 3, stringBuilder2);
				handler.AppendLiteral("- ");
				handler.AppendFormatted(item.Name);
				handler.AppendLiteral(" | score=");
				handler.AppendFormatted(item.Score);
				handler.AppendLiteral(" | ");
				handler.AppendFormatted(item.DisplayName);
				stringBuilder4.AppendLine(ref handler);
			}
			if (readOnlyList.Count == 0)
			{
				stringBuilder.AppendLine("- No generateContent Gemini models found.");
			}
			return stringBuilder.ToString();
		}
		catch (Exception ex)
		{
			return "Gemini model discovery failed: " + ex.Message;
		}
	}

	private static string GetRecommendedProfile()
	{
		double totalRamGb = GetTotalRamGb();
		if (totalRamGb < 8.0)
		{
			return "- Weak machine: use Gemini/OpenAI online. Local OCR only.";
		}
		if (totalRamGb < 16.0)
		{
			return "- Medium machine: local OCR + small text model. Use online for vision.";
		}
		return "- Strong machine: local OCR + small/medium local model possible. Online still recommended for vision snapshots.";
	}

	private static double GetTotalRamGb()
	{
		try
		{
			MEMORYSTATUSEX lpBuffer = new MEMORYSTATUSEX
			{
				dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>()
			};
			if (GlobalMemoryStatusEx(ref lpBuffer))
			{
				return (double)lpBuffer.ullTotalPhys / 1024.0 / 1024.0 / 1024.0;
			}
		}
		catch
		{
		}
		return 0.0;
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
