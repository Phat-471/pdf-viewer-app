using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace PdfViewerApp;

internal static class SecurityHelper
{
	// XOR obfuscation key
	private static readonly byte[] ObfuscationKey = new byte[]
	{
		(byte)('P' ^ 0x12),
		(byte)('D' ^ 0x34),
		(byte)('F' ^ 0x56),
		(byte)('P' ^ 0x78),
		(byte)('R' ^ 0x9A),
		(byte)('O' ^ 0xBC)
	};

	public static string Decrypt(string input)
	{
		if (string.IsNullOrEmpty(input))
		{
			return string.Empty;
		}

		try
		{
			byte[] data = Convert.FromBase64String(input);
			byte[] key = new byte[]
			{
				(byte)(ObfuscationKey[0] ^ 0x12),
				(byte)(ObfuscationKey[1] ^ 0x34),
				(byte)(ObfuscationKey[2] ^ 0x56),
				(byte)(ObfuscationKey[3] ^ 0x78),
				(byte)(ObfuscationKey[4] ^ 0x9A),
				(byte)(ObfuscationKey[5] ^ 0xBC)
			};

			for (int i = 0; i < data.Length; i++)
			{
				data[i] = (byte)(data[i] ^ key[i % key.Length]);
			}
			return Encoding.UTF8.GetString(data);
		}
		catch
		{
			return string.Empty;
		}
	}

	public static string Encrypt(string input)
	{
		if (string.IsNullOrEmpty(input))
		{
			return string.Empty;
		}

		try
		{
			byte[] data = Encoding.UTF8.GetBytes(input);
			byte[] key = new byte[]
			{
				(byte)(ObfuscationKey[0] ^ 0x12),
				(byte)(ObfuscationKey[1] ^ 0x34),
				(byte)(ObfuscationKey[2] ^ 0x56),
				(byte)(ObfuscationKey[3] ^ 0x78),
				(byte)(ObfuscationKey[4] ^ 0x9A),
				(byte)(ObfuscationKey[5] ^ 0xBC)
			};

			for (int i = 0; i < data.Length; i++)
			{
				data[i] = (byte)(data[i] ^ key[i % key.Length]);
			}
			return Convert.ToBase64String(data);
		}
		catch
		{
			return string.Empty;
		}
	}

	public static string MaskKey(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return string.Empty;
		}

		int prefixIndex = key.IndexOf('-');
		if (prefixIndex >= 0)
		{
			string prefix = key.Substring(0, prefixIndex + 1);
			string rest = key.Substring(prefixIndex + 1);
			StringBuilder sb = new StringBuilder(prefix);
			foreach (char c in rest)
			{
				if (char.IsLetterOrDigit(c))
				{
					sb.Append('*');
				}
				else
				{
					sb.Append(c);
				}
			}
			return sb.ToString();
		}
		else
		{
			if (key.Length <= 4)
			{
				return new string('*', key.Length);
			}
			return key.Substring(0, 2) + new string('*', key.Length - 2);
		}
	}

	public static bool IsDebuggerAttached()
	{
		try
		{
			if (Debugger.IsAttached || Debugger.IsLogging())
			{
				return true;
			}
			if (IsDebuggerPresentNative())
			{
				return true;
			}
			if (IsRemoteDebuggerPresent())
			{
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	[System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "IsDebuggerPresent")]
	private static extern bool IsDebuggerPresentNative();

	[System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isPresent);

	private static bool IsRemoteDebuggerPresent()
	{
		try
		{
			bool isPresent = false;
			CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref isPresent);
			return isPresent;
		}
		catch
		{
			return false;
		}
	}

	public static bool IsAnalysisToolRunning()
	{
		try
		{
			string[] badProcs = { "dnspy", "ilspy", "cheatengine", "ollydbg", "x64dbg", "windbg", "fiddler", "wireshark", "processhacker", "ida" };
			foreach (var proc in Process.GetProcesses())
			{
				try
				{
					string name = proc.ProcessName.ToLowerInvariant();
					foreach (var bad in badProcs)
					{
						if (name.Contains(bad))
						{
							return true;
						}
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
		return false;
	}

	public static bool IsVirtualMachine()
	{
		try
		{
			string[] vmFiles = { @"C:\windows\System32\Drivers\VBoxMouse.sys", @"C:\windows\System32\Drivers\vmmouse.sys", @"C:\windows\System32\Drivers\vmhgfs.sys" };
			foreach (var file in vmFiles)
			{
				if (System.IO.File.Exists(file))
				{
					return true;
				}
			}
		}
		catch
		{
		}
		return false;
	}

	public static void CheckIntegrity()
	{
		string violationReason = null;

		if (IsDebuggerAttached())
		{
			violationReason = "Debugger Detected";
		}
		else if (IsAnalysisToolRunning())
		{
			violationReason = "Reverse Engineering Tools Detected";
		}
		else if (IsVirtualMachine())
		{
			violationReason = "Sandbox/VM Environment Detected";
		}

		if (violationReason != null)
		{
			ReportSecurityViolation(violationReason);

			if (Application.Current != null && Application.Current.Dispatcher != null)
			{
				Application.Current.Dispatcher.Invoke(() =>
				{
					ShowLockDialog();
				});
			}
			else
			{
				System.Threading.Thread thread = new System.Threading.Thread(() =>
				{
					ShowLockDialog();
				});
				thread.SetApartmentState(System.Threading.ApartmentState.STA);
				thread.Start();
				thread.Join();
			}
		}
	}

	private static void ShowLockDialog()
	{
		LockWindow lockWin = new LockWindow();
		if (lockWin.ShowDialog() != true)
		{
			Environment.Exit(0);
		}
	}

	private static void ReportSecurityViolation(string violationType)
	{
		PdfViewerApp.Services.Diagnostics.ErrorReportingService.ReportSecurityViolation(violationType);
	}
}
