using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ControlzEx.Theming;
using PdfViewerApp.Ai;

namespace PdfViewerApp;

public partial class App : Application
{
	private static Mutex? _singleInstanceMutex;

	private static CancellationTokenSource? _pipeServerCts;

	public App()
	{
		base.Startup += async delegate(object sender, StartupEventArgs e)
		{
			string[] args = e.Args;
			bool flag = args.Any((string arg) => arg.Equals("--merge", StringComparison.OrdinalIgnoreCase));
			bool flag2 = args.Any((string arg) => arg.Equals("--exit-after-merge", StringComparison.OrdinalIgnoreCase));
			if (flag || flag2)
			{
				string[] array = (from file in args.Where((string arg) => !arg.Equals("--merge", StringComparison.OrdinalIgnoreCase) && !arg.Equals("--exit-after-merge", StringComparison.OrdinalIgnoreCase)).ToArray()
					where !string.IsNullOrWhiteSpace(file) && File.Exists(file) && Path.GetExtension(file).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
					select file).ToArray();
				if (array.Length == 0)
				{
					Environment.Exit(0);
				}
				else
				{
					PdfViewerApp.MainWindow.QueueExplorerMergeFiles(array);
					if (!PdfViewerApp.MainWindow.TryBecomeExplorerMergeOwner())
					{
						Environment.Exit(0);
					}
					else
					{
						Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
						PdfViewerApp.MainWindow.RunExplorerMergeFlowAsync(array);
					}
				}
			}
			else
			{
				AppPreferences appPreferences = AppPreferences.Load();
				bool launchNewInstance = false;

				if (appPreferences.AllowMultipleInstances)
				{
					launchNewInstance = true;
				}
				else
				{
					_singleInstanceMutex = new Mutex(initiallyOwned: true, "Local\\PdfPro.SingleInstanceMutex", out var createdNew);
					if (!createdNew)
					{
						string[] pdfArgs = args.Where((string file) => !string.IsNullOrWhiteSpace(file) && File.Exists(file) && Path.GetExtension(file).Equals(".pdf", StringComparison.OrdinalIgnoreCase)).ToArray();
						bool sent = SendArgsToExistingInstance(pdfArgs);
						if (sent)
						{
							Environment.Exit(0);
						}
						else
						{
							// Main instance is unresponsive or busy printing. Bypass single instance check.
							_singleInstanceMutex.Dispose();
							_singleInstanceMutex = null;
							launchNewInstance = true;
						}
					}
				}

				if (launchNewInstance || _singleInstanceMutex != null)
				{
					try
					{
						ThemeManager.Current.ChangeTheme(this, AppThemeRegistry.Get(appPreferences.ThemeName).FluentTheme);
					}
					catch
					{
					}
					SplashWindow splashWindow = new SplashWindow();
					splashWindow.Show();
					await Dispatcher.Yield(DispatcherPriority.Background);
					PdfiumEngine.Initialize();
					MainWindow mainWindow = new MainWindow();
					mainWindow.Show();
					splashWindow.Close();
					if (launchNewInstance && args.Length > 0)
					{
						foreach (string text in args)
						{
							if (!string.IsNullOrWhiteSpace(text) && File.Exists(text) && Path.GetExtension(text).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
							{
								mainWindow.OpenPdfTab(text);
							}
						}
					}
					if (_singleInstanceMutex != null)
					{
						StartSingleInstanceServer(mainWindow);
					}
				}
			}
		};
		base.Exit += delegate
		{
			_pipeServerCts?.Cancel();
			try
			{
				_singleInstanceMutex?.ReleaseMutex();
			}
			catch
			{
			}
			_singleInstanceMutex?.Dispose();
			PdfiumEngine.Shutdown();
		};
		base.DispatcherUnhandledException += delegate(object _, DispatcherUnhandledExceptionEventArgs e)
		{
			File.WriteAllText("crash.log", e.Exception.ToString());
			SendCrashTelemetry(e.Exception);
			MessageBox.Show(e.Exception.ToString(), "Application error", MessageBoxButton.OK, MessageBoxImage.Hand);
			e.Handled = true;
			Application.Current.Shutdown();
		};
		AppDomain.CurrentDomain.UnhandledException += delegate(object _, UnhandledExceptionEventArgs e)
		{
			string contents = $"[{DateTime.Now}] Fatal Error: {e.ExceptionObject?.ToString()}\n";
			File.AppendAllText("debug_pdfpro.log", contents);
			if (e.ExceptionObject is Exception ex)
			{
				SendCrashTelemetry(ex);
			}
			MessageBox.Show(e.ExceptionObject?.ToString() ?? "Unknown fatal error", "Fatal error", MessageBoxButton.OK, MessageBoxImage.Hand);
		};
	}

	public static void SendCrashTelemetry(Exception ex)
	{
		try
		{
			if (!AiSettings.Load().EnableTelemetry)
			{
				return;
			}
			Task.Run(async delegate
			{
				try
				{
					using HttpClient client = new HttpClient();
					client.Timeout = TimeSpan.FromSeconds(5.0);
					StringContent content = new StringContent(JsonSerializer.Serialize(new
					{
						app_version = ActivationLicense.AppVersion,
						machine_id = ActivationLicense.MachineId,
						error_message = ex.Message,
						stack_trace = ex.ToString(),
						os_version = Environment.OSVersion.VersionString,
						timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
					}), Encoding.UTF8, "application/json");
					string requestUri = "https://hongmien.vn/wp-json/pdfpro/v1/report-error";
					await client.PostAsync(requestUri, content);
				}
				catch
				{
				}
			});
		}
		catch
		{
		}
	}

	public static void HandlePostMergeOpen(string outputPath)
	{
		AppPreferences appPreferences = AppPreferences.Load();
		bool launchNewInstance = false;
		if (appPreferences.AllowMultipleInstances)
		{
			launchNewInstance = true;
		}
		else
		{
			_singleInstanceMutex = new Mutex(initiallyOwned: true, "Local\\PdfPro.SingleInstanceMutex", out var createdNew);
			if (!createdNew)
			{
				bool sent = SendArgsToExistingInstance(new string[] { outputPath });
				if (sent)
				{
					Environment.Exit(0);
				}
				else
				{
					_singleInstanceMutex.Dispose();
					_singleInstanceMutex = null;
					launchNewInstance = true;
				}
			}
		}

		if (launchNewInstance || _singleInstanceMutex != null)
		{
			Application.Current.Dispatcher.Invoke(delegate
			{
				Application.Current.ShutdownMode = ShutdownMode.OnLastWindowClose;
				SplashWindow splashWindow = new SplashWindow();
				splashWindow.Show();
				PdfiumEngine.Initialize();
				PdfViewerApp.MainWindow.SkipStartupMergeArgs = true;
				MainWindow mainWindow = new MainWindow();
				mainWindow.Show();
				splashWindow.Close();
				mainWindow.OpenPdfTab(outputPath);
				if (_singleInstanceMutex != null)
				{
					StartSingleInstanceServer(mainWindow);
				}
				mainWindow.Activate();
				mainWindow.Focus();
			});
		}
	}

	private static bool SendArgsToExistingInstance(string[] args)
	{
		try
		{
			using (NamedPipeClientStream namedPipeClientStream = new NamedPipeClientStream(".", "PdfProSingleInstancePipe", PipeDirection.Out))
			{
				namedPipeClientStream.Connect(2000);
				using StreamWriter streamWriter = new StreamWriter(namedPipeClientStream, Encoding.UTF8);
				foreach (string value in args)
				{
					streamWriter.WriteLine(value);
				}
				streamWriter.Flush();
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static void StartSingleInstanceServer(MainWindow mainWindow)
	{
		_pipeServerCts = new CancellationTokenSource();
		Task.Run(async delegate
		{
			while (!_pipeServerCts.Token.IsCancellationRequested)
			{
				try
				{
					using NamedPipeServerStream server = new NamedPipeServerStream("PdfProSingleInstancePipe", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
					await server.WaitForConnectionAsync(_pipeServerCts.Token);
					using StreamReader reader = new StreamReader(server, Encoding.UTF8);
					List<string> paths = new List<string>();
					string text;
					while ((text = await reader.ReadLineAsync()) != null)
					{
						if (!string.IsNullOrEmpty(text))
						{
							paths.Add(text);
						}
					}
					mainWindow.Dispatcher.Invoke(delegate
					{
						if (paths.Count > 0)
						{
							foreach (string item in paths)
							{
								mainWindow.OpenPdfTab(item);
							}
						}
						if (mainWindow.WindowState == WindowState.Minimized)
						{
							mainWindow.WindowState = WindowState.Normal;
						}
						mainWindow.Activate();
						mainWindow.Focus();
					});
				}
				catch (OperationCanceledException)
				{
					break;
				}
				catch
				{
					await Task.Delay(1000);
				}
			}
		});
	}
}
