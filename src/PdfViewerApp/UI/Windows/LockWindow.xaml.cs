using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace PdfViewerApp;

public partial class LockWindow : Window
{
	public LockWindow()
	{
		InitializeComponent();
	}

	private void Unlock_Click(object sender, RoutedEventArgs e)
	{
		string enteredKey = RecoveryKeyBox.Password.Trim().Replace("-", "").ToUpperInvariant();
		if (string.IsNullOrEmpty(enteredKey))
		{
			MessageBox.Show("Vui lòng nhập mã khôi phục.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
			return;
		}

		string machineId = ActivationLicense.MachineId; // e.g. "DCA6-8A6C-9F..."
		string salt = "HPhat.PdfPro.LockBypass.2026";
		string input = machineId + salt;
		
		string expectedHashPart = string.Empty;
		using (SHA256 sha256 = SHA256.Create())
		{
			byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
			StringBuilder sb = new StringBuilder();
			foreach (byte b in bytes)
			{
				sb.Append(b.ToString("X2"));
			}
			expectedHashPart = sb.ToString().Substring(0, 16);
		}

		string expectedNormalized = "PDFPROUNLOCK" + expectedHashPart;
		
		if (enteredKey == expectedNormalized || enteredKey == expectedHashPart)
		{
			MessageBox.Show("Mở khóa ứng dụng thành công!", "Bảo mật", MessageBoxButton.OK, MessageBoxImage.Information);
			this.DialogResult = true;
			this.Close();
		}
		else
		{
			MessageBox.Show("Mã khôi phục không chính xác dành cho thiết bị này. Vui lòng kiểm tra lại.", "Lỗi bảo mật", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private void Exit_Click(object sender, RoutedEventArgs e)
	{
		Environment.Exit(0);
	}
}
