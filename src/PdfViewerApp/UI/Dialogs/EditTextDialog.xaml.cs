using System;
using System.IO;
using System.Windows;
using PdfViewerApp.Services;

namespace PdfViewerApp
{
    public partial class EditTextDialog : Window
    {
        public string? PdfPath { get; set; }
        public string? ResultPath { get; private set; }

        public EditTextDialog()
        {
            InitializeComponent();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PdfPath) || !File.Exists(PdfPath))
            {
                StatusText.Text = "Không tìm thấy file PDF đang mở.";
                return;
            }

            string oldText = OldTextbox.Text;
            string newText = NewTextbox.Text;

            if (string.IsNullOrEmpty(oldText))
            {
                StatusText.Text = "Vui lòng nhập chữ cần tìm.";
                return;
            }

            try
            {
                ApplyButton.IsEnabled = false;
                StatusText.Text = "Đang xử lý...";

                // Ghi ra file tạm, sau đó copy đè lên file gốc để giữ nguyên đường dẫn.
                string tempPath = Path.Combine(
                    Path.GetTempPath(),
                    "pdfpro_edit_" + Guid.NewGuid().ToString("N") + ".pdf");

                bool ok = PdfCoreInterop.replace_text_full(PdfPath, oldText, newText, tempPath);

                if (!ok || !File.Exists(tempPath))
                {
                    StatusText.Text = "Không tìm thấy chữ cần thay (hoặc xử lý thất bại).";
                    ApplyButton.IsEnabled = true;
                    return;
                }

                File.Copy(tempPath, PdfPath!, true);
                File.Delete(tempPath);

                ResultPath = PdfPath;
                StatusText.Text = "Đã thay thế thành công. Font, cỡ chữ và màu được giữ nguyên.";
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                StatusText.Text = "Lỗi: " + ex.Message;
                ApplyButton.IsEnabled = true;
            }
        }
    }
}
