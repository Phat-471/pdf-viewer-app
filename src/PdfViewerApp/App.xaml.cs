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

	public static bool IsPrinting { get; set; } = false;
	public static bool HasShownPrintBusyNotification { get; set; } = false;
	public static readonly System.Collections.Concurrent.ConcurrentQueue<string> PendingFilesToOpen = new();

	public static void ResetPrintBusyNotification()
	{
		HasShownPrintBusyNotification = false;
	}

	public static void OpenPendingFiles()
	{
		Application.Current.Dispatcher.BeginInvoke(new Action(delegate
		{
			MainWindow mainWindow = Application.Current.MainWindow as MainWindow;
			if (mainWindow != null)
			{
				while (PendingFilesToOpen.TryDequeue(out string? result))
				{
					if (!string.IsNullOrEmpty(result))
					{
						mainWindow.OpenPdfTab(result);
					}
				}
				if (mainWindow.WindowState == WindowState.Minimized)
				{
					mainWindow.WindowState = WindowState.Normal;
				}
				mainWindow.Activate();
				mainWindow.Focus();
			}
		}));
	}

	public App()
	{
		base.Startup += async delegate(object sender, StartupEventArgs e)
		{
			SecurityHelper.CheckIntegrity();
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
				bool forceNewWindow = args.Any((string arg) => arg.Equals("--new-window", StringComparison.OrdinalIgnoreCase));

				if (appPreferences.AllowMultipleInstances || forceNewWindow)
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
					Application.Current.MainWindow = mainWindow;
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
			PdfViewerApp.Services.Diagnostics.ErrorReportingService.SendCrashTelemetry(e.Exception);
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
				PdfViewerApp.Services.Diagnostics.ErrorReportingService.SendCrashTelemetry(ex);
			}
			MessageBox.Show(e.ExceptionObject?.ToString() ?? "Unknown fatal error", "Fatal error", MessageBoxButton.OK, MessageBoxImage.Hand);
		};
	}

	public static void SendCrashTelemetry(Exception ex)
	{
		PdfViewerApp.Services.Diagnostics.ErrorReportingService.SendCrashTelemetry(ex);
	}

	public static void HandlePostMergeOpen(string outputPath)
	{
		try
		{
			string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
			string exeFile = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? exePath;
			if (exeFile.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
			{
				exeFile = Path.ChangeExtension(exeFile, ".exe");
			}
			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exeFile, $"\"{outputPath}\" --new-window") { UseShellExecute = true });
		}
		catch
		{
		}
		Environment.Exit(0);
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
					mainWindow.Dispatcher.BeginInvoke(new Action(delegate
					{
						if (paths.Count > 0)
						{
							if (App.IsPrinting)
							{
								foreach (string item in paths)
								{
									App.PendingFilesToOpen.Enqueue(item);
								}
								if (!App.HasShownPrintBusyNotification)
								{
									App.HasShownPrintBusyNotification = true;
									MessageBox.Show(mainWindow, "Ứng dụng đang bận gửi lệnh in. File của bạn sẽ tự động mở sau khi hoàn thành gửi lệnh in.", "Đang in ấn", MessageBoxButton.OK, MessageBoxImage.Information);
								}
							}
							else
							{
								foreach (string item in paths)
								{
									mainWindow.OpenPdfTab(item);
								}
								if (mainWindow.WindowState == WindowState.Minimized)
								{
									mainWindow.WindowState = WindowState.Normal;
								}
								mainWindow.Activate();
								mainWindow.Focus();
							}
						}
					}));
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
