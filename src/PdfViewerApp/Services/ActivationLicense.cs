using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Win32;

namespace PdfViewerApp;

internal static class ActivationLicense
{
	private const string ProductName = "PDF Pro";

	private const string EditionName = "HPhat Edition";

	private const string KeyPrefix = "PDFPRO";

	// Tên miền máy chủ bản quyền (mặc định)
	private const string DefaultApiDomain = "https://hongmien.vn";

	private static bool _isOfflineDetected = false;

	public static string ApiDomain => GetApiDomain();

	public static string ApiActivateUrl => $"{ApiDomain}/wp-json/pdfpro/v1/activate";

	public static string ApiUpdateUrl => $"{ApiDomain}/wp-json/pdfpro/v1/update-check";

	public static string ApiCheckUrl => $"{ApiDomain}/wp-json/pdfpro/v1/check";

	public static string ApiDeactivateUrl => $"{ApiDomain}/wp-json/pdfpro/v1/deactivate";

	public static string LicenseDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PdfPro");

	public static string LicensePath { get; } = Path.Combine(LicenseDirectory, "activation.json");

	public static string AppVersion
	{
		get
		{
			string text = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text;
			}
			return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
		}
	}

	public static string AppTitle => $"{"PDF Pro"} - {"HPhat Edition"} v{AppVersion}";

	public static string MachineId => BuildMachineId();

	public static string LastVerifyError { get; set; } = string.Empty;

	private static string GetApiDomain()
	{
		try
		{
			string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_config.json");
			if (File.Exists(configPath))
			{
				using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(configPath, Encoding.UTF8));
				if (doc.RootElement.TryGetProperty("api_domain", out var prop))
				{
					string domain = prop.GetString() ?? string.Empty;
					if (!string.IsNullOrWhiteSpace(domain))
					{
						return domain.TrimEnd('/');
					}
				}
			}
		}
		catch {}
		return DefaultApiDomain;
	}

	private static string EncryptWithMachineKey(string plainText, string machineId)
	{
		if (string.IsNullOrEmpty(plainText)) return string.Empty;
		try
		{
			byte[] key = SHA256.HashData(Encoding.UTF8.GetBytes(machineId + "PDFPRO-OFFLINE-SALT-2026"));
			using Aes aes = Aes.Create();
			aes.Key = key;
			aes.GenerateIV();
			byte[] iv = aes.IV;
			
			using MemoryStream ms = new MemoryStream();
			ms.Write(iv, 0, iv.Length);
			
			using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
			using (StreamWriter sw = new StreamWriter(cs, Encoding.UTF8))
			{
				sw.Write(plainText);
			}
			return Convert.ToBase64String(ms.ToArray());
		}
		catch
		{
			return string.Empty;
		}
	}

	private static string DecryptWithMachineKey(string cipherText, string machineId)
	{
		if (string.IsNullOrEmpty(cipherText)) return string.Empty;
		try
		{
			byte[] cipherBytes = Convert.FromBase64String(cipherText);
			byte[] key = SHA256.HashData(Encoding.UTF8.GetBytes(machineId + "PDFPRO-OFFLINE-SALT-2026"));
			
			using Aes aes = Aes.Create();
			aes.Key = key;
			
			byte[] iv = new byte[16];
			Array.Copy(cipherBytes, 0, iv, 0, 16);
			aes.IV = iv;
			
			using MemoryStream ms = new MemoryStream(cipherBytes, 16, cipherBytes.Length - 16);
			using CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
			using StreamReader sr = new StreamReader(cs, Encoding.UTF8);
			return sr.ReadToEnd();
		}
		catch
		{
			return string.Empty;
		}
	}

	public static ActivationState LoadState()
	{
		ActivationRecord activationRecord = LoadRecord();
		bool isActivated = false;
		string activationKey = string.Empty;
		DateTimeOffset? expiresAt = null;
		bool needsOnlineVerification = false;
		string offlineWarningMessage = string.Empty;

		if (activationRecord != null && !string.IsNullOrEmpty(activationRecord.ActivationKey))
		{
			if (string.Equals(activationRecord.MachineId, MachineId, StringComparison.OrdinalIgnoreCase))
			{
				isActivated = true;
				activationKey = activationRecord.ActivationKey;

				if (activationRecord.ExpiresAt != "never" && DateTimeOffset.TryParse(activationRecord.ExpiresAt, out var expDate))
				{
					expiresAt = expDate;
					if (expDate < DateTimeOffset.Now)
					{
						isActivated = false;
						LastVerifyError = "Bản quyền đã hết hạn sử dụng.";
					}
				}

				if (isActivated)
				{
					// Kiểm tra trạng thái offline
					bool currentlyOffline = _isOfflineDetected || !System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
					double daysSinceCheck = (DateTimeOffset.Now - activationRecord.LastOnlineCheckTime).TotalDays;

					if (currentlyOffline)
					{
						if (daysSinceCheck >= 15.0)
						{
							isActivated = false;
							LastVerifyError = "Đã quá 15 ngày chưa xác thực trực tuyến. Vui lòng kết nối Internet để tiếp tục sử dụng.";
						}
						else
						{
							needsOnlineVerification = true;
							int remainingDays = (int)Math.Max(0, Math.Ceiling(15.0 - daysSinceCheck));
							offlineWarningMessage = $"Máy tính của bạn hiện không có kết nối Internet. Ứng dụng đang chạy ở chế độ Ngoại tuyến (Hạn dùng offline còn lại: {remainingDays} ngày).";
						}
					}
				}
			}
			else
			{
				LastVerifyError = "Mã thiết bị không khớp.";
			}
		}

		string expirationText = "Vĩnh viễn";
		if (expiresAt.HasValue)
		{
			expirationText = expiresAt.Value.ToString("dd/MM/yyyy HH:mm");
		}

		return new ActivationState
		{
			AppVersion = AppVersion,
			MachineId = MachineId,
			IsActivated = isActivated,
			ActivationKey = activationKey,
			ActivatedAt = activationRecord?.ActivatedAt,
			LicensePath = LicensePath,
			StatusText = (isActivated ? "Activated" : "Not activated"),
			ExpiresAt = expiresAt,
			ExpirationText = expirationText,
			NeedsOnlineVerification = needsOnlineVerification,
			OfflineWarningMessage = offlineWarningMessage
		};
	}

	public static async Task<(bool Success, string Message)> TryActivateOnlineAsync(string activationKey)
	{
		string normalizedKey = NormalizeKey(activationKey);
		if (string.IsNullOrEmpty(normalizedKey))
		{
			return (Success: false, Message: "Vui lòng nhập mã kích hoạt.");
		}
		try
		{
			var client = HttpHelper.Client;
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15.0));
			StringContent content = new StringContent(JsonSerializer.Serialize(new
			{
				license_key = normalizedKey,
				machine_id = MachineId,
				machine_name = Environment.MachineName
			}), Encoding.UTF8, "application/json");

			HttpResponseMessage response = await client.PostAsync(ApiActivateUrl, content, cts.Token);
			string json = await response.Content.ReadAsStringAsync();
			if (!response.IsSuccessStatusCode)
			{
				try
				{
					using JsonDocument jsonDocument = JsonDocument.Parse(json);
					if (jsonDocument.RootElement.TryGetProperty("message", out var val))
					{
						return (Success: false, Message: val.GetString() ?? "Kích hoạt không thành công.");
					}
				}
				catch {}
				return (Success: false, Message: $"Yêu cầu kích hoạt bị từ chối: {(int)response.StatusCode}");
			}

			using JsonDocument doc = JsonDocument.Parse(json);
			bool success = doc.RootElement.GetProperty("success").GetBoolean();
			string status = doc.RootElement.GetProperty("status").GetString() ?? string.Empty;

			if (success && status == "activated")
			{
				string key = doc.RootElement.GetProperty("license_key").GetString() ?? normalizedKey;
				string expStr = doc.RootElement.GetProperty("expires_at").GetString() ?? "never";

				ActivationRecord record = new ActivationRecord
				{
					ActivationKey = FormatActivationKey(key),
					MachineId = MachineId,
					ExpiresAt = expStr,
					Status = status,
					Edition = EditionName,
					ActivatedAt = DateTimeOffset.Now,
					LastOnlineCheckTime = DateTimeOffset.Now
				};

				SaveRecord(record);
				_isOfflineDetected = false;
				return (Success: true, Message: "Đã kích hoạt bản quyền thành công!");
			}
			return (Success: false, Message: "Kích hoạt không thành công. Trạng thái bản quyền không hợp lệ.");
		}
		catch (Exception ex)
		{
			if (ex is HttpRequestException || ex is System.Net.Sockets.SocketException || ex.Message.Contains(ApiDomain) || ex.Message.Contains("No such host") || ex.Message.Contains("connection") || ex.Message.Contains("connect"))
			{
				_isOfflineDetected = true;
				return (Success: false, Message: "Không thể kết nối đến máy chủ bản quyền. Vui lòng kiểm tra lại kết nối Internet.");
			}
			return (Success: false, Message: "Lỗi kết nối máy chủ kích hoạt: " + ex.Message);
		}
	}

	public static void Deactivate()
	{
		try
		{
			if (File.Exists(LicensePath))
			{
				ActivationRecord activationRecord = LoadRecord();
				if (activationRecord != null && !string.IsNullOrEmpty(activationRecord.ActivationKey))
				{
					string key = activationRecord.ActivationKey;
					Task.Run(async delegate
					{
						try
						{
							var client = HttpHelper.Client;
							using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5.0));
							StringContent content = new StringContent(JsonSerializer.Serialize(new
							{
								license_key = key,
								machine_id = MachineId
							}), Encoding.UTF8, "application/json");
							await client.PostAsync(ApiDeactivateUrl, content, cts.Token);
						}
						catch {}
					});
				}
			}
		}
		catch {}

		if (File.Exists(LicensePath))
		{
			File.Delete(LicensePath);
		}
	}

	public static string GenerateActivationKeyForMachine(string machineId)
	{
		string value = Sha256Hex("PDFPRO-OFFLINE-SALT-" + machineId + "-HPhat.PdfPro.LocalActivation.2026").Substring(0, 16);
		return "PDFPRO-" + Group(value, 4);
	}

	private static bool ValidateKey(string activationKey, string machineId)
	{
		string a = NormalizeKey(activationKey);
		string b = NormalizeKey(GenerateActivationKeyForMachine(machineId));
		return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
	}

	private static void SaveRecord(ActivationRecord record)
	{
		try
		{
			string originalKey = record.ActivationKey;
			record.ActivationKey = SecurityHelper.Encrypt(originalKey);
			
			string json = JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true });
			string encrypted = EncryptWithMachineKey(json, MachineId);

			Directory.CreateDirectory(LicenseDirectory);
			File.WriteAllText(LicensePath, encrypted, Encoding.UTF8);
			record.ActivationKey = originalKey;
		}
		catch {}
	}

	private static ActivationRecord? LoadRecord()
	{
		try
		{
			if (!File.Exists(LicensePath))
			{
				return null;
			}
			string encrypted = File.ReadAllText(LicensePath, Encoding.UTF8);
			string decryptedJson = DecryptWithMachineKey(encrypted, MachineId);
			if (string.IsNullOrEmpty(decryptedJson))
			{
				return null;
			}

			var record = JsonSerializer.Deserialize<ActivationRecord>(decryptedJson);
			if (record != null && !string.IsNullOrEmpty(record.ActivationKey))
			{
				try
				{
					string decrypted = SecurityHelper.Decrypt(record.ActivationKey);
					if (!string.IsNullOrEmpty(decrypted))
					{
						record.ActivationKey = decrypted;
					}
				}
				catch {}
			}
			return record;
		}
		catch
		{
			return null;
		}
	}

	private static string BuildMachineId()
	{
		string text = GetMachineGuid();
		if (string.IsNullOrWhiteSpace(text))
		{
			text = $"{Environment.MachineName}|{Environment.ProcessorCount}|{Environment.OSVersion.VersionString}";
		}
		return Group(Sha256Hex("PDFPRO-MACHINE|" + text).Substring(0, 16), 4);
	}

	private static string GetMachineGuid()
	{
		try
		{
			return Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Cryptography", "MachineGuid", null)?.ToString() ?? string.Empty;
		}
		catch
		{
			return string.Empty;
		}
	}

	private static string NormalizeMachineId(string value)
	{
		return KeepAlphaNumeric(value).ToUpperInvariant();
	}

	private static string NormalizeKey(string value)
	{
		return KeepAlphaNumeric(value).ToUpperInvariant();
	}

	private static string FormatActivationKey(string normalizedKey)
	{
		if (normalizedKey.StartsWith("PDFPRO", StringComparison.OrdinalIgnoreCase))
		{
			normalizedKey = normalizedKey.Substring("PDFPRO".Length);
		}
		return "PDFPRO-" + Group(normalizedKey, 4);
	}

	private static string KeepAlphaNumeric(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder(value.Length);
		foreach (char c in value)
		{
			if (char.IsLetterOrDigit(c))
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}

	private static string Sha256Hex(string value)
	{
		byte[] array = SHA256.HashData(Encoding.UTF8.GetBytes(value));
		StringBuilder stringBuilder = new StringBuilder(array.Length * 2);
		byte[] array2 = array;
		foreach (byte b in array2)
		{
			stringBuilder.Append(b.ToString("X2"));
		}
		return stringBuilder.ToString();
	}

	private static string Group(string value, int groupSize)
	{
		StringBuilder stringBuilder = new StringBuilder(value.Length + value.Length / groupSize);
		for (int i = 0; i < value.Length; i++)
		{
			if (i > 0 && i % groupSize == 0)
			{
				stringBuilder.Append('-');
			}
			stringBuilder.Append(value[i]);
		}
		return stringBuilder.ToString();
	}

	public static async Task<(bool UpdateAvailable, string LatestVersion, string DownloadUrl, string Changelog)> CheckForUpdatesAsync()
	{
		try
		{
			var client = HttpHelper.Client;
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10.0));
			string requestUri = ApiUpdateUrl;
			HttpResponseMessage httpResponseMessage = await client.GetAsync(requestUri, cts.Token);
			if (!httpResponseMessage.IsSuccessStatusCode)
			{
				return (UpdateAvailable: false, LatestVersion: string.Empty, DownloadUrl: string.Empty, Changelog: string.Empty);
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(await httpResponseMessage.Content.ReadAsStringAsync());
			JsonElement rootElement = jsonDocument.RootElement;
			string text = rootElement.GetProperty("latest_version").GetString() ?? string.Empty;
			string item = rootElement.GetProperty("download_url").GetString() ?? string.Empty;
			string item2 = rootElement.GetProperty("changelog").GetString() ?? string.Empty;
			if (string.IsNullOrEmpty(text))
			{
				return (UpdateAvailable: false, LatestVersion: string.Empty, DownloadUrl: string.Empty, Changelog: string.Empty);
			}
			if (IsNewerVersion(AppVersion, text))
			{
				return (UpdateAvailable: true, LatestVersion: text, DownloadUrl: item, Changelog: item2);
			}
		}
		catch
		{
		}
		return (UpdateAvailable: false, LatestVersion: string.Empty, DownloadUrl: string.Empty, Changelog: string.Empty);
	}

	private static bool IsNewerVersion(string currentVersionStr, string latestVersionStr)
	{
		try
		{
			string text = currentVersionStr.Split('-')[0];
			string text2 = latestVersionStr.Split('-')[0];
			if (!text.Contains('.'))
			{
				text += ".0";
			}
			if (!text2.Contains('.'))
			{
				text2 += ".0";
			}
			if (Version.TryParse(text, out Version result) && Version.TryParse(text2, out Version result2))
			{
				return result2 > result;
			}
		}
		catch
		{
		}
		return false;
	}

	public static async Task CheckHeartbeatOnlineAsync(bool force = false)
	{
		ActivationRecord record = LoadRecord();
		if (record == null || string.IsNullOrEmpty(record.ActivationKey))
		{
			return;
		}

		try
		{
			var client = HttpHelper.Client;
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10.0));
			StringContent content = new StringContent(JsonSerializer.Serialize(new
			{
				license_key = NormalizeKey(record.ActivationKey),
				machine_id = MachineId
			}), Encoding.UTF8, "application/json");

			HttpResponseMessage response = await client.PostAsync(ApiCheckUrl, content, cts.Token);
			string json = await response.Content.ReadAsStringAsync();
			bool isActivated = false;
			if (response.IsSuccessStatusCode)
			{
				using JsonDocument doc = JsonDocument.Parse(json);
				bool success = doc.RootElement.GetProperty("success").GetBoolean();
				string status = doc.RootElement.GetProperty("status").GetString() ?? string.Empty;

				if (success && status == "activated")
				{
					isActivated = true;
					record.LastOnlineCheckTime = DateTimeOffset.Now;
					record.Status = status;
					if (doc.RootElement.TryGetProperty("expires_at", out var expVal))
					{
						record.ExpiresAt = expVal.GetString() ?? record.ExpiresAt;
					}
					SaveRecord(record);
					_isOfflineDetected = false;
				}
				else if (!success)
				{
					if (status == "suspended" || status == "expired" || status == "unregistered_device")
					{
						Deactivate();
					}
				}
			}
			else
			{
				// Phản hồi lỗi HTTP từ server, xem như offline
				_isOfflineDetected = true;
			}
		}
		catch
		{
			_isOfflineDetected = true;
		}
	}
}
