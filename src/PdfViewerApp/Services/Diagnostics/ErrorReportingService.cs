using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PdfViewerApp.Ai;

namespace PdfViewerApp.Services.Diagnostics
{
	public static class ErrorReportingService
	{
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
						var client = HttpHelper.Client;
						using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5.0));
						StringContent content = new StringContent(JsonSerializer.Serialize(new
						{
							app_version = ActivationLicense.AppVersion,
							machine_id = ActivationLicense.MachineId,
							error_message = ex.Message,
							stack_trace = ex.ToString(),
							os_version = Environment.OSVersion.VersionString,
							timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
						}), Encoding.UTF8, "application/json");
						string requestUri = $"{ActivationLicense.ApiDomain}/wp-json/pdfpro/v1/report-error";
						await client.PostAsync(requestUri, content, cts.Token);
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

		public static void ReportSecurityViolation(string violationType)
		{
			try
			{
				Task.Run(async () =>
				{
					try
					{
						using HttpClient client = new HttpClient();
						client.Timeout = TimeSpan.FromSeconds(3);
						var payload = new
						{
							app_version = ActivationLicense.AppVersion,
							machine_id = ActivationLicense.MachineId,
							error_message = $"SECURITY_VIOLATION: {violationType}",
							stack_trace = $"User: {Environment.UserName} | Machine: {Environment.MachineName}",
							os_version = Environment.OSVersion.VersionString,
							timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
						};
						var content = new StringContent(
							JsonSerializer.Serialize(payload),
							Encoding.UTF8,
							"application/json"
						);
						string requestUri = $"{ActivationLicense.ApiDomain}/wp-json/pdfpro/v1/report-error";
						await client.PostAsync(requestUri, content);
					}
					catch
					{
					}
				}).Wait(3000);
			}
			catch
			{
			}
		}
	}
}
