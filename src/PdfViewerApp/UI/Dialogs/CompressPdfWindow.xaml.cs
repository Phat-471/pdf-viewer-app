using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace PdfViewerApp
{
    public partial class CompressPdfWindow : Window
    {
        private readonly string _sourcePdfPath;
        private bool _isWorking;

        public string? CompressedPdfPath { get; private set; }



        public CompressPdfWindow(string sourcePdfPath)
        {
            InitializeComponent();
            _sourcePdfPath = sourcePdfPath;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_sourcePdfPath) || !File.Exists(_sourcePdfPath))
            {
                MessageBox.Show("Tệp PDF gốc không tồn tại.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            long sizeBytes = new FileInfo(_sourcePdfPath).Length;
            OriginalSizeText.Text = FormatFileSize(sizeBytes);
        }

        private async void Compress_Click(object sender, RoutedEventArgs e)
        {
            if (_isWorking) return;

            _isWorking = true;
            CompressBtn.IsEnabled = false;
            CancelBtn.IsEnabled = false;
            StatusTextBlock.Text = "Đang nén hình ảnh...";

            byte quality = 80;
            if (RadioMedium.IsChecked == true) quality = 60;
            else if (RadioHigh.IsChecked == true) quality = 35;

            string tempDir = Path.Combine(Path.GetTempPath(), "PdfProCompressor");
            Directory.CreateDirectory(tempDir);
            string tempOutFile = Path.Combine(tempDir, $"{Guid.NewGuid():N}.pdf");

            bool success = await Task.Run(() =>
            {
                try
                {
                    return PdfInterop.PdfCore.compress_pdf(_sourcePdfPath, quality, tempOutFile);
                }
                catch
                {
                    return false;
                }
            });

            if (success && File.Exists(tempOutFile))
            {
                long oldSize = new FileInfo(_sourcePdfPath).Length;
                long newSize = new FileInfo(tempOutFile).Length;

                double percentReduced = (double)(oldSize - newSize) / oldSize * 100.0;

                if (newSize >= oldSize)
                {
                    StatusTextBlock.Text = "Tệp đã được tối ưu từ trước, không thể nén thêm.";
                    try { File.Delete(tempOutFile); } catch {}
                    CompressBtn.IsEnabled = true;
                    CancelBtn.IsEnabled = true;
                    _isWorking = false;
                    MessageBox.Show("Tệp này đã được tối ưu hóa ở mức tối đa, việc nén thêm không làm giảm dung lượng.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string msg = $"Nén thành công!\nDung lượng cũ: {FormatFileSize(oldSize)}\nDung lượng mới: {FormatFileSize(newSize)}\nGiảm được: {percentReduced:F1}%\n\nBạn có muốn ghi đè (Save) lên file gốc không?";
                var result = MessageBox.Show(msg, "Nén Thành Công", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Ghi đè file gốc (cần đóng file gốc trước, MainWindow sẽ reload)
                        File.Copy(tempOutFile, _sourcePdfPath, true);
                        CompressedPdfPath = _sourcePdfPath;
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
                    // Cancel
                    try { File.Delete(tempOutFile); } catch {}
                    CompressBtn.IsEnabled = true;
                    CancelBtn.IsEnabled = true;
                    _isWorking = false;
                    StatusTextBlock.Text = "Đã hủy lưu tệp nén.";
                }
            }
            else
            {
                StatusTextBlock.Text = "Lỗi khi nén tệp PDF.";
                CompressBtn.IsEnabled = true;
                CancelBtn.IsEnabled = true;
                _isWorking = false;
                MessageBox.Show("Có lỗi xảy ra trong quá trình nén ảnh PDF.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAsNewFile(string tempFile)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF Documents (*.pdf)|*.pdf",
                Title = "Lưu File PDF Đã Nén",
                FileName = Path.GetFileNameWithoutExtension(_sourcePdfPath) + "_compressed.pdf",
                InitialDirectory = Path.GetDirectoryName(_sourcePdfPath)
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    File.Copy(tempFile, saveDialog.FileName, true);
                    CompressedPdfPath = saveDialog.FileName;
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

        private static string FormatFileSize(long bytes)
        {
            double mb = (double)bytes / 1024.0 / 1024.0;
            if (mb >= 1.0) return $"{mb:F2} MB";
            
            double kb = (double)bytes / 1024.0;
            return $"{kb:F1} KB";
        }
    }
}
