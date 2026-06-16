using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace PdfViewerApp
{
    public partial class WatermarkDialog : Window
    {
        private readonly string _sourcePdfPath;
        private bool _isWorking;

        public string? WatermarkedPdfPath { get; private set; }

        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool add_pdf_watermark(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
            double angle,
            double opacity,
            double fontSize,
            double r,
            double g,
            double b,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath
        );

        public WatermarkDialog(string sourcePdfPath)
        {
            InitializeComponent();
            _sourcePdfPath = sourcePdfPath;
        }

        private async void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (_isWorking) return;

            string text = WatermarkTextBox.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show("Vui lòng nhập nội dung chữ Watermark.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _isWorking = true;
            ApplyBtn.IsEnabled = false;
            CancelBtn.IsEnabled = false;
            StatusTextBlock.Text = "Đang áp dụng đóng dấu...";

            double opacity = OpacitySlider.Value;
            double fontSize = FontSizeSlider.Value;
            double angle = AngleSlider.Value;

            // Extract color values
            double r = 0.5, g = 0.5, b = 0.5;
            if (ColorComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string hexColor)
            {
                try
                {
                    Color color = (Color)ColorConverter.ConvertFromString(hexColor);
                    r = color.R / 255.0;
                    g = color.G / 255.0;
                    b = color.B / 255.0;
                }
                catch { }
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "PdfProWatermark");
            Directory.CreateDirectory(tempDir);
            string tempOutFile = Path.Combine(tempDir, $"{Guid.NewGuid():N}.pdf");

            bool success = await Task.Run(() =>
            {
                try
                {
                    return add_pdf_watermark(_sourcePdfPath, text, angle, opacity, fontSize, r, g, b, tempOutFile);
                }
                catch
                {
                    return false;
                }
            });

            if (success && File.Exists(tempOutFile))
            {
                string msg = "Đã tạo file đóng dấu thành công!\n\nBạn có muốn ghi đè lên file gốc không?";
                var result = MessageBox.Show(msg, "Đóng Dấu Thành Công", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        File.Copy(tempOutFile, _sourcePdfPath, true);
                        WatermarkedPdfPath = _sourcePdfPath;
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
                StatusTextBlock.Text = "Lỗi khi đóng dấu tệp PDF.";
                ApplyBtn.IsEnabled = true;
                CancelBtn.IsEnabled = true;
                _isWorking = false;
                MessageBox.Show("Có lỗi xảy ra trong quá trình đóng dấu PDF.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAsNewFile(string tempFile)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF Documents (*.pdf)|*.pdf",
                Title = "Lưu File PDF Đã Đóng Dấu",
                FileName = Path.GetFileNameWithoutExtension(_sourcePdfPath) + "_watermarked.pdf",
                InitialDirectory = Path.GetDirectoryName(_sourcePdfPath)
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    File.Copy(tempFile, saveDialog.FileName, true);
                    WatermarkedPdfPath = saveDialog.FileName;
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
