using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Navigation;

namespace PdfViewerApp;

public partial class ActivationDialog : Window, IComponentConnector
{
	public ActivationDialog()
	{
		InitializeComponent();
		RefreshState();
		ForceCheckLicenseAsync();
	}

	private async void ForceCheckLicenseAsync()
	{
		try
		{
			await ActivationLicense.CheckHeartbeatOnlineAsync(force: true);
			RefreshState();
		}
		catch {}
	}

	private void RefreshState()
	{
		ActivationState activationState = ActivationLicense.LoadState();
		VersionTextBlock.Text = activationState.AppVersion;
		MachineIdTextBox.Text = activationState.MachineId;
		ActivationKeyTextBox.Text = SecurityHelper.MaskKey(activationState.ActivationKey);
		StatusTextBlock.Text = (activationState.IsActivated ? ("Đã kích hoạt (Hạn: " + activationState.ExpirationText + ")") : "Chưa kích hoạt");
		StatusTextBlock.Foreground = (activationState.IsActivated ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(239, 68, 68)));
	}

	private void CopyMachineId_Click(object sender, RoutedEventArgs e)
	{
		Clipboard.SetText(MachineIdTextBox.Text);
	}

	private async void Activate_Click(object sender, RoutedEventArgs e)
	{
		StatusTextBlock.Text = "Connecting to activation server...";
		(bool, string) tuple = await ActivationLicense.TryActivateOnlineAsync(ActivationKeyTextBox.Text);
		RefreshState();
		if (tuple.Item1)
		{
			MessageBox.Show(this, tuple.Item2, "Activation", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			base.DialogResult = true;
		}
		else
		{
			MessageBox.Show(this, tuple.Item2, "Activation", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private void Deactivate_Click(object sender, RoutedEventArgs e)
	{
		if (MessageBox.Show(this, "Go kich hoat tren may nay?", "Activation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
		{
			ActivationLicense.Deactivate();
			RefreshState();
			base.DialogResult = true;
		}
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
			MessageBox.Show("Không thể mở liên kết: " + ex.Message);
		}
		e.Handled = true;
	}
}
