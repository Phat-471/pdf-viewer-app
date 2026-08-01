using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using PdfViewerApp;

namespace PdfViewerApp.UI.Dialogs
{
    public partial class ExportDocumentDialog : Window
    {
        private readonly string _pdfPath;

        public ExportDocumentDialog(string pdfPath)
        {
            InitializeComponent();
            _pdfPath = pdfPath;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_pdfPath) || !File.Exists(_pdfPath))
            {
                MessageBox.Show("Tệp PDF đầu vào không tồn tại!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var saveFileDialog = new SaveFileDialog();
            if (RadioDocx.IsChecked == true)
            {
                saveFileDialog.Filter = "Word Document (*.docx)|*.docx|Text Document (*.txt)|*.txt";
                saveFileDialog.FileName = Path.GetFileNameWithoutExtension(_pdfPath) + "_exported.docx";
            }
            else
            {
                saveFileDialog.Filter = "Excel Workbook (*.xlsx)|*.xlsx|CSV File (*.csv)|*.csv";
                saveFileDialog.FileName = Path.GetFileNameWithoutExtension(_pdfPath) + "_exported.xlsx";
            }

            if (saveFileDialog.ShowDialog() == true)
            {
                string outputPath = saveFileDialog.FileName;
                bool success = PdfInterop.PdfCore.pdf_export_to_docx(_pdfPath, outputPath);

                if (success && File.Exists(outputPath))

                {
                    MessageBox.Show($"Xuất tài liệu thành công!\nĐường dẫn: {outputPath}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Có lỗi xảy ra trong quá trình xuất tài liệu!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
