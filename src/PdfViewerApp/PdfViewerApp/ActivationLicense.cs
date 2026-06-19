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

	private static string ActivationSecret => SecurityHelper.Decrypt("GBQuMSZhACAgACAgfggpMzMjEScyOSQuJC0pPnx9YHZw");

	private static string PublicRsaKeyPem => SecurityHelper.Decrypt("fWlrfX8NFQMPHnIfBQYKGRFvGwEffX9ifWlMHRsGEg0sERwNNy83ODkmF30xYBAOAQEAERMAEwUXaBMCGQ0EEzUEEwUXFRM8GzY0CmIEaS0saSR8HCAnGzx3Z04uf2U4EXQHBwh3eyZ0NysXNDIgFAR3OXY1FSM/HDN/NAUEKQ8tESYoZDE1aSEuMjEXADwfEQEhOmQCPBQiaBccWnQgITo/YxY+GDYiAxcnMzEuOSkuOwsAaSkSNxoYPCl+AmQ+YAwQOjB/EyAJZjAjBjIDOR8sJSFwADwbZRIpZwRFfywkCWMEB282GxY3e3MlCn0mZikjA2d2H280Zjw5OyE/Egd4Kg4oPhUfNxQtOQp9OicLESAgCgN1ImAnYyEVJlgYHgp+Mzg4NncrGhoXFzN0Ggo1BylyMTQXaDd0MQR2MgscKjQrGzN2GT43PnYTEio+IiwjPxAGZQEvYjEdOAgsWhh6NRcNaRcoBXcXAAgWYAANGx8OJ3x/YxspPhMiOmQDGw0CAhk/AwAOFGEoCDNwOWICIh4iHCo4IgByPzAiHQFMKiUGFAUXERBFfWlrfX8KHgBmAAcNHA0FcBkKCWlrfX9i");

	private const string PublicKeyCacheFileName = "public_key.pem";

	public static string ApiActivateUrl => SecurityHelper.Decrypt("ODAyICF1f2suPzwoPS0jPnw5PmsxIH8lIysofyIrNjQ0P305YWsnMyYmJiUyNQ==");

	public static string ApiPublicKeyUrl => SecurityHelper.Decrypt("ODAyICF1f2suPzwoPS0jPnw5PmsxIH8lIysofyIrNjQ0P305YWs2JTAjOSdrOzc2");

	public static string ApiUpdateUrl => SecurityHelper.Decrypt("ODAyICF1f2suPzwoPS0jPnw5PmsxIH8lIysofyIrNjQ0P305YWszIDYuJCFrMzoqMy8=");

	public static string LicenseDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PdfPro");

	public static string LicensePath { get; } = Path.Combine(LicenseDirectory, "activation.json");

	public static string PublicKeyCachePath { get; } = Path.Combine(LicenseDirectory, PublicKeyCacheFileName);

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

	public static ActivationState LoadState()
	{
		ActivationRecord activationRecord = LoadRecord();
		bool flag = false;
		string verifiedPublicKey = string.Empty;
		string activationKey = string.Empty;
		DateTimeOffset? expiresAt = null;
		if (activationRecord != null && !string.IsNullOrEmpty(activationRecord.Payload) && !string.IsNullOrEmpty(activationRecord.Signature))
		{
			foreach (string candidatePublicKey in GetVerificationPublicKeys(activationRecord))
			{
				if (VerifySignature(activationRecord.Payload, activationRecord.Signature, candidatePublicKey))
				{
					flag = true;
					verifiedPublicKey = candidatePublicKey;
					break;
				}
			}

			if (flag)
			{
				try
				{
					using JsonDocument jsonDocument = JsonDocument.Parse(activationRecord.Payload);
					string text = jsonDocument.RootElement.GetProperty("license_key").GetString() ?? string.Empty;
					string? a = jsonDocument.RootElement.GetProperty("machine_id").GetString() ?? string.Empty;
					string text2 = jsonDocument.RootElement.GetProperty("expires_at").GetString() ?? string.Empty;
					activationKey = text;
					if (!string.Equals(a, MachineId, StringComparison.OrdinalIgnoreCase))
					{
						flag = false;
					}
					if (flag && text2 != "never" && DateTimeOffset.TryParse(text2, out var result))
					{
						expiresAt = result;
						if (result < DateTimeOffset.Now)
						{
							flag = false;
						}
					}
				}
				catch
				{
					flag = false;
				}
			}
		}
		string expirationText = "Vĩnh viễn";
		if (expiresAt.HasValue)
		{
			expirationText = expiresAt.Value.ToString("dd/MM/yyyy HH:mm");
		}
		if (flag && !string.IsNullOrWhiteSpace(verifiedPublicKey))
		{
			SaveCachedPublicKeyPem(verifiedPublicKey);
		}

		bool needsOnlineVerification = false;
		string offlineWarningMessage = string.Empty;
		if (flag && activationRecord != null)
		{
			double daysSinceCheck = (DateTimeOffset.Now - activationRecord.LastOnlineCheckTime).TotalDays;
			if (daysSinceCheck >= 15.0)
			{
				flag = false;
				LastVerifyError = "Vượt quá thời hạn xác thực ngoại tuyến (15 ngày). Vui lòng kết nối Internet.";
			}
			else if (daysSinceCheck >= 7.0)
			{
				needsOnlineVerification = true;
				int remainingDays = (int)Math.Max(1, Math.Ceiling(15.0 - daysSinceCheck));
				offlineWarningMessage = $"Ứng dụng đang chạy ngoại tuyến. Vui lòng kết nối Internet trong {remainingDays} ngày tới để xác thực bản quyền.";
			}
		}

		return new ActivationState
		{
			AppVersion = AppVersion,
			MachineId = MachineId,
			IsActivated = flag,
			ActivationKey = activationKey,
			ActivatedAt = activationRecord?.ActivatedAt,
			LicensePath = LicensePath,
			StatusText = (flag ? "Activated" : "Not activated"),
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
					if (jsonDocument.RootElement.TryGetProperty("message", out var value))
					{
						return (Success: false, Message: value.GetString() ?? "Kích hoạt không thành công.");
					}
				}
				catch
				{
				}
				return (Success: false, Message: $"Yêu cầu kích hoạt bị từ chối: {(int)response.StatusCode}");
			}
			using JsonDocument jsonDocument2 = JsonDocument.Parse(json);
			string payload = jsonDocument2.RootElement.GetProperty("payload").GetString() ?? string.Empty;
			string text = jsonDocument2.RootElement.GetProperty("signature").GetString() ?? string.Empty;
			string publicKeyPem = await GetPublicKeyForActivationAsync();
			if (VerifySignature(payload, text, publicKeyPem))
			{
				SaveCachedPublicKeyPem(publicKeyPem);
				ActivationRecord value2 = new ActivationRecord
				{
					ActivationKey = FormatActivationKey(normalizedKey),
					Payload = payload,
					Signature = text,
					PublicKey = publicKeyPem,
					MachineId = MachineId,
					Edition = "HPhat Edition",
					ActivatedAt = DateTimeOffset.Now,
					LastOnlineCheckTime = DateTimeOffset.Now
				};
				SaveRecord(value2);
				return (Success: true, Message: "Đã kích hoạt bản quyền thành công!");
			}
			return (Success: false, Message: "Xác minh chữ ký số thất bại. Chi tiết lỗi:\n" + LastVerifyError);
		}
		catch (Exception ex)
		{
			if (ex is HttpRequestException || ex is System.Net.Sockets.SocketException || ex.Message.Contains("hongmien.vn") || ex.Message.Contains("No such host") || ex.Message.Contains("connection") || ex.Message.Contains("connect"))
			{
				return (Success: false, Message: "Không thể kết nối đến máy chủ bản quyền. Vui lòng mở kết nối Internet (Wifi hoặc mạng dây) để thực hiện kích hoạt.");
			}
			if (ex is JsonException || ex is NotSupportedException)
			{
				return (Success: false, Message: "Lỗi xử lý dữ liệu kích hoạt từ máy chủ. Vui lòng liên hệ với quản trị viên để hỗ trợ.");
			}
			return (Success: false, Message: "Lỗi kết nối máy chủ kích hoạt: " + ex.Message);
		}
	}

	private static bool VerifySignature(string payload, string base64Signature, string publicKeyPem)
	{
		try
		{
			LastVerifyError = "None";
			byte[] bytes = Encoding.UTF8.GetBytes(payload);
			byte[] signature = Convert.FromBase64String(base64Signature);
			using RSA rSA = RSA.Create();
			rSA.ImportFromPem((string.IsNullOrWhiteSpace(publicKeyPem) ? PublicRsaKeyPem : publicKeyPem).ToCharArray());
			bool num = rSA.VerifyData(bytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			if (!num)
			{
				LastVerifyError = $"Signature mismatch.\nPayload: '{payload}'\nSignature Base64: '{base64Signature}'";
			}
			return num;
		}
		catch (Exception ex)
		{
			LastVerifyError = "Exception: " + ex.Message + "\nStack: " + ex.StackTrace;
			return false;
		}
	}

	public static void Deactivate()
	{
		try
		{
			if (File.Exists(LicensePath))
			{
				ActivationRecord activationRecord = LoadRecord();
				if (activationRecord != null && !string.IsNullOrEmpty(activationRecord.Payload))
				{
					using JsonDocument jsonDocument = JsonDocument.Parse(activationRecord.Payload);
					string key = jsonDocument.RootElement.GetProperty("license_key").GetString() ?? string.Empty;
					if (!string.IsNullOrEmpty(key))
					{
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
								await client.PostAsync(SecurityHelper.Decrypt("ODAyICF1f2suPzwoPS0jPnw5PmsxIH8lIysofyIrNjQ0P305YWsiNTMsJC0wMSYq"), content, cts.Token);
							}
							catch
							{
							}
						});
					}
				}
			}
		}
		catch
		{
		}
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
			Directory.CreateDirectory(LicenseDirectory);
			File.WriteAllText(LicensePath, JsonSerializer.Serialize(record, new JsonSerializerOptions
			{
				WriteIndented = true
			}), Encoding.UTF8);
			record.ActivationKey = originalKey;
		}
		catch
		{
		}
	}

	private static ActivationRecord? LoadRecord()
	{
		try
		{
			if (!File.Exists(LicensePath))
			{
				return null;
			}
			var record = JsonSerializer.Deserialize<ActivationRecord>(File.ReadAllText(LicensePath, Encoding.UTF8));
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

	private static IEnumerable<string> GetVerificationPublicKeys(ActivationRecord? activationRecord)
	{
		if (activationRecord != null && !string.IsNullOrWhiteSpace(activationRecord.PublicKey))
		{
			yield return activationRecord.PublicKey.Trim();
		}

		string cachedPublicKey = LoadCachedPublicKeyPem();
		if (!string.IsNullOrWhiteSpace(cachedPublicKey))
		{
			yield return cachedPublicKey;
		}

		yield return PublicRsaKeyPem;
	}

	private static string LoadCachedPublicKeyPem()
	{
		try
		{
			if (!File.Exists(PublicKeyCachePath))
			{
				return string.Empty;
			}

			return File.ReadAllText(PublicKeyCachePath, Encoding.UTF8).Trim();
		}
		catch
		{
			return string.Empty;
		}
	}

	private static void SaveCachedPublicKeyPem(string publicKeyPem)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(publicKeyPem))
			{
				return;
			}

			Directory.CreateDirectory(LicenseDirectory);
			File.WriteAllText(PublicKeyCachePath, publicKeyPem.Trim() + Environment.NewLine, Encoding.UTF8);
		}
		catch
		{
		}
	}

	private static async Task<string> GetPublicKeyForActivationAsync()
	{
		string remotePublicKey = await FetchRemotePublicKeyPemAsync();
		if (!string.IsNullOrWhiteSpace(remotePublicKey))
		{
			return remotePublicKey;
		}

		string cachedPublicKey = LoadCachedPublicKeyPem();
		if (!string.IsNullOrWhiteSpace(cachedPublicKey))
		{
			return cachedPublicKey;
		}

		return PublicRsaKeyPem;
	}

	private static async Task<string> FetchRemotePublicKeyPemAsync()
	{
		try
		{
			var client = HttpHelper.Client;
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10.0));
			HttpResponseMessage response = await client.GetAsync(ApiPublicKeyUrl, cts.Token);
			string json = await response.Content.ReadAsStringAsync();
			using JsonDocument jsonDocument = JsonDocument.Parse(json);
			if (jsonDocument.RootElement.TryGetProperty("public_key", out var value))
			{
				string? text = value.GetString();
				if (!string.IsNullOrWhiteSpace(text))
				{
					return text.Trim();
				}
			}
		}
		catch
		{
		}

		return string.Empty;
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
		_ = 1;
		try
		{
			var client = HttpHelper.Client;
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10.0));
			string requestUri = "https://hongmien.vn/wp-json/pdfpro/v1/activate".Replace("/activate", "/update-check");
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

		if (!force)
		{
			double daysSinceCheck = (DateTimeOffset.Now - record.LastOnlineCheckTime).TotalDays;
			if (daysSinceCheck < 7.0)
			{
				return;
			}
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

			HttpResponseMessage response = await client.PostAsync(SecurityHelper.Decrypt("ODAyICF1f2suPzwoPS0jPnw5PmsxIH8lIysofyIrNjQ0P305YWslODcsOw=="), content, cts.Token);
			string json = await response.Content.ReadAsStringAsync();
			bool isActivated = false;
			if (response.IsSuccessStatusCode)
			{
				try
				{
					using JsonDocument doc = JsonDocument.Parse(json);
					bool success = doc.RootElement.GetProperty("success").GetBoolean();
					string status = doc.RootElement.GetProperty("status").GetString() ?? string.Empty;

					if (success && status == "activated")
					{
						isActivated = true;
						record.LastOnlineCheckTime = DateTimeOffset.Now;
						if (doc.RootElement.TryGetProperty("payload", out var payloadVal) && doc.RootElement.TryGetProperty("signature", out var sigVal))
						{
							record.Payload = payloadVal.GetString() ?? record.Payload;
							record.Signature = sigVal.GetString() ?? record.Signature;
						}
						
						SaveRecord(record);
					}
				}
				catch
				{
				}
			}

			if (!isActivated)
			{
				bool shouldDeactivate = false;
				if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
				{
					shouldDeactivate = true;
				}
				else
				{
					try
					{
						using JsonDocument doc = JsonDocument.Parse(json);
						if (doc.RootElement.TryGetProperty("success", out var successVal) && !successVal.GetBoolean())
						{
							shouldDeactivate = true;
						}
					}
					catch
					{
					}
				}

				if (shouldDeactivate)
				{
					Deactivate();
				}
			}
		}
		catch
		{
		}
	}
}
