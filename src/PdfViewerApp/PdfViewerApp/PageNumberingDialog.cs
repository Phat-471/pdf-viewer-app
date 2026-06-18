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
    public partial class PageNumberingDialog : Window
    {
        private readonly string _sourcePdfPath;
        private bool _isWorking;

        public string? NumberedPdfPath { get; private set; }



        public PageNumberingDialog(string sourcePdfPath)
        {
            InitializeComponent();
            _sourcePdfPath = sourcePdfPath;
        }

        private async void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (_isWorking) return;

            string format = FormatTextBox.Text;
            if (string.IsNullOrEmpty(format))
            {
                MessageBox.Show("Vui lòng nhập định dạng số trang.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _isWorking = true;
            ApplyBtn.IsEnabled = false;
            CancelBtn.IsEnabled = false;
            StatusTextBlock.Text = "Đang đánh số trang...";

            int position = 0;
            if (PositionComboBox.SelectedItem is ComboBoxItem posItem && int.TryParse(posItem.Tag?.ToString(), out var posVal))
            {
                position = posVal;
            }

            double fontSize = FontSizeSlider.Value;

            // Extract color values
            double r = 0.0, g = 0.0, b = 0.0;
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

            string tempDir = Path.Combine(Path.GetTempPath(), "PdfProNumbering");
            Directory.CreateDirectory(tempDir);
            string tempOutFile = Path.Combine(tempDir, $"{Guid.NewGuid():N}.pdf");

            bool success = await Task.Run(() =>
            {
                try
                {
                    return PdfInterop.PdfCore.add_pdf_page_numbers(_sourcePdfPath, format, position, fontSize, r, g, b, tempOutFile);
                }
                catch
                {
                    return false;
                }
            });

            if (success && File.Exists(tempOutFile))
            {
                string msg = "Đã đánh số trang thành công!\n\nBạn có muốn ghi đè lên file gốc không?";
                var result = MessageBox.Show(msg, "Đánh Số Trang Thành Công", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        File.Copy(tempOutFile, _sourcePdfPath, true);
                        NumberedPdfPath = _sourcePdfPath;
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
                StatusTextBlock.Text = "Lỗi khi đánh số trang tệp PDF.";
                ApplyBtn.IsEnabled = true;
                CancelBtn.IsEnabled = true;
                _isWorking = false;
                MessageBox.Show("Có lỗi xảy ra trong quá trình đánh số trang PDF.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAsNewFile(string tempFile)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF Documents (*.pdf)|*.pdf",
                Title = "Lưu File PDF Đã Đánh Số Trang",
                FileName = Path.GetFileNameWithoutExtension(_sourcePdfPath) + "_numbered.pdf",
                InitialDirectory = Path.GetDirectoryName(_sourcePdfPath)
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    File.Copy(tempFile, saveDialog.FileName, true);
                    NumberedPdfPath = saveDialog.FileName;
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
