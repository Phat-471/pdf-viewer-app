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
