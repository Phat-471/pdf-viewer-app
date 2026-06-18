using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Microsoft.Win32;

namespace PdfViewerApp;

public partial class SplitDialog : Window, IComponentConnector
{
    private string? _sourceFile;
    private int _pageCount = 0;
    private bool _splitInProgress = false;



    public SplitDialog() : this(null)
    {
    }

    public SplitDialog(string? initialFile)
    {
        InitializeComponent();
        _sourceFile = initialFile;
        Loaded += SplitDialog_Loaded;
    }

    private void SplitDialog_Loaded(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_sourceFile) && File.Exists(_sourceFile))
        {
            SetSourceFile(_sourceFile);
        }
        else
        {
            UpdateStatus("Vui lòng chọn file PDF nguồn.");
        }
    }

    private void SetSourceFile(string filePath)
    {
        _sourceFile = filePath;
        SourceFileTextBox.Text = filePath;
        
        try
        {
            PdfiumEngine.Initialize();
            IntPtr doc = PdfiumEngine.FPDF_LoadDocument(filePath, null);
            if (doc != IntPtr.Zero)
            {
                _pageCount = PdfiumEngine.FPDF_GetPageCount(doc);
                PdfiumEngine.CloseDocument(doc);
            }
        }
        catch (Exception ex)
        {
            PdfPerfLogger.Log($"Error counting pages: {ex}");
            _pageCount = 0;
        }

        if (_pageCount > 0)
        {
            UpdateStatus($"File đã chọn có {_pageCount} trang.");
            RangeTextBox.Text = $"1-{_pageCount}";
            GroupsTextBox.Text = $"1-{Math.Min(2, _pageCount)}; {Math.Min(3, _pageCount)}-{_pageCount}";
            
            // Set default output folder to the same folder as the source file
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                OutputFolderTextBox.Text = dir;
            }
        }
        else
        {
            UpdateStatus("Không thể đọc số trang của file PDF này.");
        }
    }

    private void SelectSourceFile_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog openFileDialog = new OpenFileDialog
        {
            Filter = "PDF documents (*.pdf)|*.pdf",
            Title = "Chọn file PDF nguồn"
        };
        if (openFileDialog.ShowDialog() == true)
        {
            SetSourceFile(openFileDialog.FileName);
        }
    }

    private void SelectOutputFolder_Click(object sender, RoutedEventArgs e)
    {
        // Use Microsoft.Win32.OpenFolderDialog which is natively supported in WPF (.NET 8.0+)
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Chọn thư mục lưu kết quả tách file",
            Multiselect = false
        };
        if (dialog.ShowDialog() == true)
        {
            OutputFolderTextBox.Text = dialog.FolderName;
        }
    }

    private void ModeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (RangeTextBox == null || EveryNTextBox == null || GroupsTextBox == null) return;

        RangeTextBox.IsEnabled = ModeRangeRadio.IsChecked == true;
        EveryNTextBox.IsEnabled = ModeEveryNRadio.IsChecked == true;
        GroupsTextBox.IsEnabled = ModeGroupsRadio.IsChecked == true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (!_splitInProgress)
        {
            DialogResult = false;
            Close();
        }
    }

    private void UpdateStatus(string message)
    {
        StatusText.Text = message;
    }

    private void SetUiState(bool isSplitting)
    {
        _splitInProgress = isSplitting;
        SelectSourceFileBtn.IsEnabled = !isSplitting;
        SelectOutputFolderBtn.IsEnabled = !isSplitting;
        ModeRangeRadio.IsEnabled = !isSplitting;
        ModeEveryNRadio.IsEnabled = !isSplitting;
        ModeSingleRadio.IsEnabled = !isSplitting;
        ModeGroupsRadio.IsEnabled = !isSplitting;
        
        RangeTextBox.IsEnabled = !isSplitting && ModeRangeRadio.IsChecked == true;
        EveryNTextBox.IsEnabled = !isSplitting && ModeEveryNRadio.IsChecked == true;
        GroupsTextBox.IsEnabled = !isSplitting && ModeGroupsRadio.IsChecked == true;

        SplitButton.IsEnabled = !isSplitting;
        CancelButton.IsEnabled = !isSplitting;

        SplitProgress.Visibility = isSplitting ? Visibility.Visible : Visibility.Collapsed;
        if (!isSplitting)
        {
            SplitProgress.Value = 0;
            ProgressText.Text = string.Empty;
        }
    }

    private async void Split_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_sourceFile) || !File.Exists(_sourceFile))
        {
            MessageBox.Show("Vui lòng chọn file PDF nguồn hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (_pageCount <= 0)
        {
            MessageBox.Show("Không có trang nào để tách hoặc file bị lỗi.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        string outputFolder = OutputFolderTextBox.Text.Trim();
        if (string.IsNullOrEmpty(outputFolder) || !Directory.Exists(outputFolder))
        {
            MessageBox.Show("Vui lòng chọn thư mục đầu ra hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        SetUiState(true);
        try
        {
            string baseName = Path.GetFileNameWithoutExtension(_sourceFile);

            if (ModeRangeRadio.IsChecked == true)
            {
                // Mode 1: Range extract
                string rangeStr = RangeTextBox.Text.Trim();
                List<int> pages = MainWindow.ParsePageRange(rangeStr, _pageCount);
                if (pages.Count == 0)
                {
                    MessageBox.Show("Dải trang trích xuất không hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string pagesJoined = string.Join(";", pages);
                string outPath = Path.Combine(outputFolder, $"{baseName}_tách_{rangeStr.Replace(" ", "").Replace(",", "_").Replace(";", "_")}.pdf");
                
                UpdateStatus("Đang trích xuất...");
                SplitProgress.Maximum = 1;
                SplitProgress.Value = 0;
                ProgressText.Text = $"Đang trích xuất các trang: {rangeStr}...";

                bool success = await Task.Run(() => PdfInterop.PdfCore.extract_pdf_pages(_sourceFile, pagesJoined, outPath));
                if (success)
                {
                    SplitProgress.Value = 1;
                    UpdateStatus("Tách file thành công!");
                    MessageBox.Show($"Đã trích xuất dải trang thành công!\nFile lưu tại: {outPath}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Tách file thất bại. Vui lòng kiểm tra lại file nguồn hoặc quyền truy cập.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else if (ModeEveryNRadio.IsChecked == true)
            {
                // Mode 2: Split every N pages
                if (!int.TryParse(EveryNTextBox.Text, out int n) || n <= 0)
                {
                    MessageBox.Show("Số trang mỗi file phải là số nguyên dương.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                UpdateStatus("Đang tách file...");
                int totalSteps = (int)Math.Ceiling((double)_pageCount / n);
                SplitProgress.Maximum = totalSteps;
                SplitProgress.Value = 0;

                int filesCreated = 0;
                for (int start = 1; start <= _pageCount; start += n)
                {
                    int end = Math.Min(start + n - 1, _pageCount);
                    string pagesJoined = string.Join(";", Enumerable.Range(start, end - start + 1));
                    string rangeLabel = start == end ? $"{start}" : $"{start}-{end}";
                    string outPath = Path.Combine(outputFolder, $"{baseName}_tách_{rangeLabel}.pdf");

                    ProgressText.Text = $"Đang xử lý nhóm trang {rangeLabel} ({filesCreated + 1}/{totalSteps})...";
                    bool success = await Task.Run(() => PdfInterop.PdfCore.extract_pdf_pages(_sourceFile, pagesJoined, outPath));
                    if (!success)
                    {
                        MessageBox.Show($"Thất bại ở nhóm trang {rangeLabel}.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    filesCreated++;
                    SplitProgress.Value = filesCreated;
                }

                UpdateStatus("Tách file thành công!");
                MessageBox.Show($"Đã tách thành công thành {filesCreated} file PDF!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            else if (ModeSingleRadio.IsChecked == true)
            {
                // Mode 3: Split into single-page files
                UpdateStatus("Đang tách các trang đơn lẻ...");
                SplitProgress.Maximum = _pageCount;
                SplitProgress.Value = 0;

                for (int page = 1; page <= _pageCount; page++)
                {
                    string outPath = Path.Combine(outputFolder, $"{baseName}_trang_{page}.pdf");
                    ProgressText.Text = $"Đang trích xuất trang {page}/{_pageCount}...";
                    bool success = await Task.Run(() => PdfInterop.PdfCore.extract_pdf_pages(_sourceFile, page.ToString(), outPath));
                    if (!success)
                    {
                        MessageBox.Show($"Thất bại ở trang {page}.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    SplitProgress.Value = page;
                }

                UpdateStatus("Tách file thành công!");
                MessageBox.Show($"Đã tách thành công thành {_pageCount} file PDF trang đơn!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            else if (ModeGroupsRadio.IsChecked == true)
            {
                // Mode 4: Custom groups
                string groupsStr = GroupsTextBox.Text.Trim();
                string[] groupParts = groupsStr.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (groupParts.Length == 0)
                {
                    MessageBox.Show("Vui lòng nhập các nhóm trang ngăn cách bởi dấu chấm phẩy.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                UpdateStatus("Đang tách theo nhóm...");
                SplitProgress.Maximum = groupParts.Length;
                SplitProgress.Value = 0;

                int groupIndex = 0;
                foreach (string part in groupParts)
                {
                    string cleanPart = part.Trim();
                    if (string.IsNullOrEmpty(cleanPart)) continue;

                    List<int> pages = MainWindow.ParsePageRange(cleanPart, _pageCount);
                    if (pages.Count == 0)
                    {
                        MessageBox.Show($"Nhóm trang '{cleanPart}' không hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    string pagesJoined = string.Join(";", pages);
                    string safeLabel = cleanPart.Replace(" ", "").Replace(",", "_").Replace("-", "_");
                    string outPath = Path.Combine(outputFolder, $"{baseName}_tách_{safeLabel}.pdf");

                    ProgressText.Text = $"Đang xử lý nhóm trang {cleanPart} ({groupIndex + 1}/{groupParts.Length})...";
                    bool success = await Task.Run(() => PdfInterop.PdfCore.extract_pdf_pages(_sourceFile, pagesJoined, outPath));
                    if (!success)
                    {
                        MessageBox.Show($"Thất bại ở nhóm trang '{cleanPart}'.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    groupIndex++;
                    SplitProgress.Value = groupIndex;
                }

                UpdateStatus("Tách file thành công!");
                MessageBox.Show($"Đã tách thành công thành {groupIndex} nhóm file PDF tùy chỉnh!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Đã xảy ra lỗi trong quá trình tách: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetUiState(false);
        }
    }
}
