using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PdfViewerApp.Ai;

internal static class LocalAiInstaller
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
		Timeout = TimeSpan.FromSeconds(5.0)
	};

	private static readonly HttpClient LongTimeoutHttpClient = new HttpClient
	{
		Timeout = TimeSpan.FromMinutes(15.0)
	};

	public static void StartInitializeBackground()
	{
		Task.Run(async delegate
		{
			try
			{
				await InitializeAsync();
			}
			catch (Exception)
			{
			}
		});
	}

	public static async Task InitializeAsync()
	{
		var settings = AiSettings.Load();
		if (settings.ProviderMode != "Local")
		{
			return;
		}

		if (GetTotalRamGb() < 7.5)
		{
			return;
		}
		if (await IsOllamaRunningAsync())
		{
			EnsureLocalProviderMode();
			return;
		}
		string ollamaPath = GetOllamaExePath();
		if (File.Exists(ollamaPath))
		{
			StartOllamaServer(ollamaPath);
			for (int i = 0; i < 6; i++)
			{
				if (await IsOllamaRunningAsync())
				{
					break;
				}
				await Task.Delay(1000);
			}
			EnsureLocalProviderMode();
			return;
		}
		string tempInstaller = Path.Combine(Path.GetTempPath(), "OllamaSetup.exe");
		try
		{
			using (HttpResponseMessage response = await LongTimeoutHttpClient.GetAsync("https://ollama.com/download/OllamaSetup.exe"))
			{
				response.EnsureSuccessStatusCode();
				using FileStream fs = new FileStream(tempInstaller, FileMode.Create, FileAccess.Write, FileShare.None);
				await response.Content.CopyToAsync(fs);
			}
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = tempInstaller,
				Arguments = "/silent",
				UseShellExecute = true,
				CreateNoWindow = true
			};
			using (Process process = Process.Start(startInfo))
			{
				if (process != null)
				{
					await process.WaitForExitAsync();
				}
			}
			await Task.Delay(3000);
			bool flag = File.Exists(ollamaPath);
			if (flag)
			{
				flag = !(await IsOllamaRunningAsync());
			}
			if (flag)
			{
				StartOllamaServer(ollamaPath);
			}
			for (int i = 0; i < 10; i++)
			{
				if (await IsOllamaRunningAsync())
				{
					break;
				}
				await Task.Delay(1000);
			}
			if (await IsOllamaRunningAsync())
			{
				await PullModelAsync("qwen2.5:1.5b");
				EnsureLocalProviderMode();
			}
		}
		finally
		{
			try
			{
				if (File.Exists(tempInstaller))
				{
					File.Delete(tempInstaller);
				}
			}
			catch
			{
			}
		}
	}

	private static async Task<bool> IsOllamaRunningAsync()
	{
		try
		{
			using HttpResponseMessage httpResponseMessage = await HttpClient.GetAsync("http://localhost:11434/api/tags");
			return httpResponseMessage.IsSuccessStatusCode;
		}
		catch
		{
			return false;
		}
	}

	private static string GetOllamaExePath()
	{
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs\\Ollama\\ollama.exe");
		if (File.Exists(text))
		{
			return text;
		}
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Ollama\\ollama.exe");
	}

	private static void StartOllamaServer(string exePath)
	{
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = exePath,
				UseShellExecute = true,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden
			});
		}
		catch
		{
		}
	}

	private static async Task PullModelAsync(string modelName)
	{
		try
		{
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = "ollama",
				Arguments = "pull " + modelName,
				CreateNoWindow = true,
				UseShellExecute = false
			};
			using Process process = Process.Start(startInfo);
			if (process != null)
			{
				await process.WaitForExitAsync();
			}
		}
		catch
		{
			try
			{
				StringContent content = new StringContent("{\"name\":\"" + modelName + "\"}", Encoding.UTF8, "application/json");
				await LongTimeoutHttpClient.PostAsync("http://localhost:11434/api/pull", content);
			}
			catch
			{
			}
		}
	}

	private static void EnsureLocalProviderMode()
	{
		try
		{
			AiSettings aiSettings = AiSettings.Load();
			if (aiSettings.ProviderMode != "Local")
			{
				aiSettings.ProviderMode = "Local";
				aiSettings.Save();
			}
		}
		catch
		{
		}
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
