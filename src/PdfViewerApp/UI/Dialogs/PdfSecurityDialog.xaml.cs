using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PdfViewerApp
{
    public partial class PdfSecurityDialog : Window
    {
        private readonly string _sourcePdfPath;
        private bool _isWorking;

        public string? SecuredPdfPath { get; private set; }

        public PdfSecurityDialog(string sourcePdfPath)
        {
            InitializeComponent();
            _sourcePdfPath = sourcePdfPath;
        }

        private async void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (_isWorking) return;

            string userPassword = UserPasswordBox.Password;
            string ownerPassword = OwnerPasswordBox.Password;

            if (string.IsNullOrEmpty(userPassword) && string.IsNullOrEmpty(ownerPassword))
            {
                MessageBox.Show("Vui lòng nhập ít nhất một mật khẩu (User hoặc Owner Password) để bảo mật.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _isWorking = true;
            ApplyBtn.IsEnabled = false;
            CancelBtn.IsEnabled = false;
            StatusTextBlock.Text = "Đang mã hóa tài liệu...";

            bool permitPrint = PrintCheckBox.IsChecked == true;
            bool permitCopy = CopyCheckBox.IsChecked == true;

            string tempDir = Path.Combine(Path.GetTempPath(), "PdfProSecurity");
            Directory.CreateDirectory(tempDir);
            string tempOutFile = Path.Combine(tempDir, $"{Guid.NewGuid():N}.pdf");

            bool success = await Task.Run(() =>
            {
                try
                {
                    // Open document using PdfSharp
                    using (PdfDocument document = PdfReader.Open(_sourcePdfPath, PdfDocumentOpenMode.Modify))
                    {
                        // Set passwords
                        if (!string.IsNullOrEmpty(userPassword))
                        {
                            document.SecuritySettings.UserPassword = userPassword;
                        }
                        if (!string.IsNullOrEmpty(ownerPassword))
                        {
                            document.SecuritySettings.OwnerPassword = ownerPassword;
                        }

                        // Set permissions
                        document.SecuritySettings.PermitPrint = permitPrint;
                        document.SecuritySettings.PermitExtractContent = permitCopy;

                        // Save to temporary file
                        document.Save(tempOutFile);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Encryption error: " + ex);
                    return false;
                }
            });

            if (success && File.Exists(tempOutFile))
            {
                string msg = "Bảo mật PDF thành công!\n\nBạn có muốn ghi đè lên file gốc không?";
                var result = MessageBox.Show(msg, "Bảo Mật Thành Công", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        File.Copy(tempOutFile, _sourcePdfPath, true);
                        SecuredPdfPath = _sourcePdfPath;
                        DialogResult = true;
                        Close();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Không thể ghi đè lên file gốc: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        SaveAsNewFile(tempOutFile);
                    }
                }
                else if (result == MessageBoxResult.No)
                {
                    SaveAsNewFile(tempOutFile);
                }
                else
                {
                    try { File.Delete(tempOutFile); } catch { }
                    ApplyBtn.IsEnabled = true;
                    CancelBtn.IsEnabled = true;
                    _isWorking = false;
                    StatusTextBlock.Text = "Đã hủy lưu tệp.";
                }
            }
            else
            {
                StatusTextBlock.Text = "Lỗi khi bảo mật tệp PDF.";
                ApplyBtn.IsEnabled = true;
                CancelBtn.IsEnabled = true;
                _isWorking = false;
                MessageBox.Show("Có lỗi xảy ra trong quá trình bảo mật PDF. File có thể đang bị khóa hoặc đã được bảo mật từ trước.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAsNewFile(string tempFile)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF Documents (*.pdf)|*.pdf",
                Title = "Lưu File PDF Đã Bảo Mật",
                FileName = Path.GetFileNameWithoutExtension(_sourcePdfPath) + "_secured.pdf",
                InitialDirectory = Path.GetDirectoryName(_sourcePdfPath)
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    File.Copy(tempFile, saveDialog.FileName, true);
                    SecuredPdfPath = saveDialog.FileName;
                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi khi lưu file mới: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
