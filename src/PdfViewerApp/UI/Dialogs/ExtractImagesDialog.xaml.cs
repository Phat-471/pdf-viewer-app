using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace PdfViewerApp
{
    public partial class ExtractImagesDialog : Window
    {
        private readonly string _sourcePdfPath;
        private bool _isWorking;



        public ExtractImagesDialog(string sourcePdfPath)
        {
            InitializeComponent();
            _sourcePdfPath = sourcePdfPath;
            OutputFolderTextBox.Text = Path.GetDirectoryName(sourcePdfPath) ?? "";
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Chọn thư mục lưu hình ảnh trích xuất",
                Multiselect = false
            };
            if (dialog.ShowDialog() == true)
            {
                OutputFolderTextBox.Text = dialog.FolderName;
            }
        }

        private async void Extract_Click(object sender, RoutedEventArgs e)
        {
            if (_isWorking) return;

            string outDir = OutputFolderTextBox.Text.Trim();
            if (string.IsNullOrEmpty(outDir) || !Directory.Exists(outDir))
            {
                MessageBox.Show("Vui lòng chọn thư mục lưu hình ảnh hợp lệ.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _isWorking = true;
            ExtractBtn.IsEnabled = false;
            CancelBtn.IsEnabled = false;
            BrowseBtn.IsEnabled = false;
            ExtractProgressBar.Visibility = Visibility.Visible;
            ExtractProgressBar.IsIndeterminate = true;
            StatusTextBlock.Text = "Đang quét và trích xuất hình ảnh...";

            int imageCount = await Task.Run(() =>
            {
                try
                {
                    return PdfInterop.PdfCore.extract_pdf_images(_sourcePdfPath, outDir);
                }
                catch
                {
                    return -3;
                }
            });

            ExtractProgressBar.Visibility = Visibility.Collapsed;
            ExtractBtn.IsEnabled = true;
            CancelBtn.IsEnabled = true;
            BrowseBtn.IsEnabled = true;
            _isWorking = false;

            if (imageCount >= 0)
            {
                StatusTextBlock.Text = $"Đã trích xuất {imageCount} hình ảnh.";
                MessageBox.Show($"Trích xuất thành công {imageCount} hình ảnh từ PDF!", "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            else
            {
                StatusTextBlock.Text = "Không tìm thấy hoặc lỗi trích xuất ảnh.";
                string errorMsg = imageCount switch
                {
                    -1 => "Lỗi đối số đường dẫn.",
                    -2 => "Không thể load file PDF hoặc file bị lỗi.",
                    _ => "Có lỗi xảy ra khi quét XObject Images."
                };
                MessageBox.Show($"Lỗi trích xuất hình ảnh: {errorMsg}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
