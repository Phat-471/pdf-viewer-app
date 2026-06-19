using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Navigation;

namespace PdfViewerApp;

public partial class AboutDialog : Window, IComponentConnector
{
	public AboutDialog()
	{
		InitializeComponent();
		RefreshState();
	}

	private void RefreshState()
	{
		ActivationState activationState = ActivationLicense.LoadState();
		VersionValueTextBlock.Text = "v" + activationState.AppVersion;
		BuildValueTextBlock.Text = BuildStampText();
		ActivationValueTextBlock.Text = activationState.IsActivated ? ("Đã kích hoạt (Hạn: " + activationState.ExpirationText + ")") : "Chưa kích hoạt";
		ActivationValueTextBlock.Foreground = activationState.IsActivated ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(239, 68, 68));
		MachineIdValueTextBlock.Text = activationState.MachineId;
		CoreValueTextBlock.Text = BuildCoreSummary();
		CoreValueTextBlock.Foreground = HasCoreLibraries() ? new SolidColorBrush(Color.FromRgb(56, 189, 248)) : new SolidColorBrush(Color.FromRgb(248, 113, 113));
	}

	private static string BuildStampText()
	{
		try
		{
			string location = Assembly.GetExecutingAssembly().Location;
			if (File.Exists(location))
			{
				return File.GetLastWriteTime(location).ToString("dd/MM/yyyy HH:mm");
			}
		}
		catch
		{
		}

		return "Unknown";
	}

	private static bool HasCoreLibraries()
	{
		string baseDirectory = AppContext.BaseDirectory;
		return File.Exists(Path.Combine(baseDirectory, "pdf_core.dll")) && File.Exists(Path.Combine(baseDirectory, "pdfium.dll"));
	}

	private static string BuildCoreSummary()
	{
		string baseDirectory = AppContext.BaseDirectory;
		string coreStatus = File.Exists(Path.Combine(baseDirectory, "pdf_core.dll")) ? "pdf_core.dll: OK" : "pdf_core.dll: missing";
		string pdfiumStatus = File.Exists(Path.Combine(baseDirectory, "pdfium.dll")) ? "pdfium.dll: OK" : "pdfium.dll: missing";
		return coreStatus + " | " + pdfiumStatus;
	}

	private void CopyInfo_Click(object sender, RoutedEventArgs e)
	{
		ActivationState activationState = ActivationLicense.LoadState();
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("PDF Pro Workspace");
		stringBuilder.AppendLine("Version: " + activationState.AppVersion);
		stringBuilder.AppendLine("Build: " + BuildStampText());
		stringBuilder.AppendLine("Activation: " + activationState.StatusText);
		stringBuilder.AppendLine("Machine ID: " + activationState.MachineId);
		stringBuilder.AppendLine("License file: " + activationState.LicensePath);
		stringBuilder.AppendLine("Core: " + BuildCoreSummary());
		stringBuilder.AppendLine("Title: " + ActivationLicense.AppTitle);
		Clipboard.SetText(stringBuilder.ToString());
		MessageBox.Show(this, "Đã sao chép thông tin ứng dụng vào clipboard.", "Giới Thiệu", MessageBoxButton.OK, MessageBoxImage.Information);
	}

	private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
	{
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = e.Uri.AbsoluteUri,
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "Không thể mở liên kết: " + ex.Message, "Giới Thiệu", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}

		e.Handled = true;
	}
}
