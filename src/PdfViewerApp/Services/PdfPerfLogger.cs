using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PdfViewerApp;

public static class PdfPerfLogger
{
	private static readonly object Sync = new object();

	private static readonly string LogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PdfPro", "PerfLogs");

	private static readonly string SessionLogPath = Path.Combine(LogDirectory, $"perf_{DateTime.Now:yyyyMMdd_HHmmss}_{Environment.ProcessId}.log");

	public static string CurrentLogPath => SessionLogPath;

	public static void Log(string message)
	{
		string text = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
		lock (Sync)
		{
			Directory.CreateDirectory(LogDirectory);
			File.AppendAllText(SessionLogPath, text + Environment.NewLine, Encoding.UTF8);
		}
	}

	public static void LogUiAction(string action, string context)
	{
		try
		{
			Log($"TEST_TRACE|ACTION|{action}|{context}");
		}
		catch
		{
			// Diagnostics must never interrupt the user's operation.
		}
	}

	public static void LogUiCommand(string command, string context)
	{
		try
		{
			Log($"TEST_TRACE|ACTION|{command}|{context}");
			Log($"TEST_TRACE|DISPATCHED|{command}|Click handler returned; async work may still be running.");
		}
		catch
		{
			// Diagnostics must never interrupt the user's operation.
		}
	}

	public static void LogTestStatus(string source, string message)
	{
		try
		{
			string normalized = message.ToLowerInvariant();
			string result = normalized.StartsWith("fail", StringComparison.Ordinal) || normalized.Contains("failed") || normalized.Contains("failure") || normalized.Contains("lỗi") ||
				normalized.Contains("không thể") || normalized.Contains("thất bại")
				? "FAIL"
				: normalized.StartsWith("pass", StringComparison.Ordinal) || normalized.Contains("thành công") || normalized.Contains("đã lưu") || normalized.Contains("đã mở") ||
				  normalized.Contains("đã tạo") || normalized.Contains("đã gộp") || normalized.Contains("đã xóa") ||
				  normalized.Contains("đã chuyển") || normalized.Contains("đã bật") || normalized.Contains("đã chọn") ||
				  normalized.Contains("captured") || normalized.Contains("completed") || normalized.Contains("success") ||
				  normalized.Contains("received") || normalized.Contains("saved")
					? "PASS"
					: "INFO";
			Log($"TEST_TRACE|{result}|{source}|{message}");
		}
		catch
		{
			// Diagnostics must never interrupt the user's operation.
		}
	}

	public static void LogDiagnosticError(string source, Exception exception)
	{
		try
		{
			Log($"TEST_TRACE|ERROR|{source}|{exception}");
		}
		catch
		{
			// Diagnostics must never interrupt the user's operation.
		}
	}

	public static void Measure(string label, Action action)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		try
		{
			action();
		}
		finally
		{
			stopwatch.Stop();
			Log($"{label}: {stopwatch.ElapsedMilliseconds} ms");
		}
	}

	public static T Measure<T>(string label, Func<T> action)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		try
		{
			return action();
		}
		finally
		{
			stopwatch.Stop();
			Log($"{label}: {stopwatch.ElapsedMilliseconds} ms");
		}
	}

	public static async Task MeasureAsync(string label, Func<Task> action)
	{
		Stopwatch sw = Stopwatch.StartNew();
		try
		{
			await action();
		}
		finally
		{
			sw.Stop();
			Log($"{label}: {sw.ElapsedMilliseconds} ms");
		}
	}

	public static async Task<T> MeasureAsync<T>(string label, Func<Task<T>> action)
	{
		Stopwatch sw = Stopwatch.StartNew();
		try
		{
			return await action();
		}
		finally
		{
			sw.Stop();
			Log($"{label}: {sw.ElapsedMilliseconds} ms");
		}
	}

	public static string ReadCurrentLog()
	{
		try
		{
			return File.Exists(SessionLogPath) ? File.ReadAllText(SessionLogPath) : "No performance log has been written yet.";
		}
		catch (Exception ex)
		{
			return "Unable to read performance log: " + ex.Message;
		}
	}
}
