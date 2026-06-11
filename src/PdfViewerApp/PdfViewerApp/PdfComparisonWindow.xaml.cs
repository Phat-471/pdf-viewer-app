using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace PdfViewerApp
{
    public partial class PdfComparisonWindow : Window
    {
        private string? _pathA;
        private string? _pathB;
        private nint _docHandleA = IntPtr.Zero;
        private nint _docHandleB = IntPtr.Zero;
        
        private int _currentPageA = 1;
        private int _currentPageB = 1;
        private int _pageOffset = 0; // Lưu độ lệch trang giữa hai file (Trang B - Trang A)
        
        private bool _isSyncingScroll;
        private bool _isLoaded;

        public PdfComparisonWindow()
        {
            InitializeComponent();
        }

        public void SetInitialFileA(string path)
        {
            _pathA = path;
            if (_isLoaded)
            {
                LoadFileA();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            if (!string.IsNullOrEmpty(_pathA))
            {
                LoadFileA();
            }
        }

        private void SelectFileA_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "PDF Documents (*.pdf)|*.pdf",
                Title = "Chọn Tài Liệu PDF Thứ Nhất (Bản Cũ)"
            };

            if (dialog.ShowDialog() == true)
            {
                _pathA = dialog.FileName;
                LoadFileA();
            }
        }

        private void SelectFileB_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "PDF Documents (*.pdf)|*.pdf",
                Title = "Chọn Tài Liệu PDF Thứ Hai (Bản Mới)"
            };

            if (dialog.ShowDialog() == true)
            {
                _pathB = dialog.FileName;
                LoadFileB();
            }
        }

        private void LoadFileA()
        {
            if (string.IsNullOrEmpty(_pathA)) return;

            try
            {
                CloseDocHandle(ref _docHandleA);
                _docHandleA = PdfiumEngine.FPDF_LoadDocument(_pathA, null);
                if (_docHandleA == IntPtr.Zero)
                {
                    MessageBox.Show("Không thể tải File A.", "Lỗi nạp tài liệu", MessageBoxButton.OK, MessageBoxImage.Error);
                    FileATextBlock.Text = "Lỗi nạp file";
                    return;
                }

                FileATextBlock.Text = Path.GetFileName(_pathA);
                PlaceholderTextA.Visibility = Visibility.Collapsed;
                _currentPageA = 1;
                UpdatePageLimits();
                RenderCurrentPage();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi tải File A: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadFileB()
        {
            if (string.IsNullOrEmpty(_pathB)) return;

            try
            {
                CloseDocHandle(ref _docHandleB);
                _docHandleB = PdfiumEngine.FPDF_LoadDocument(_pathB, null);
                if (_docHandleB == IntPtr.Zero)
                {
                    MessageBox.Show("Không thể tải File B.", "Lỗi nạp tài liệu", MessageBoxButton.OK, MessageBoxImage.Error);
                    FileBTextBlock.Text = "Lỗi nạp file";
                    return;
                }

                FileBTextBlock.Text = Path.GetFileName(_pathB);
                PlaceholderTextB.Visibility = Visibility.Collapsed;
                _currentPageB = 1;
                UpdatePageLimits();
                RenderCurrentPage();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi tải File B: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdatePageLimits()
        {
            int countA = _docHandleA != IntPtr.Zero ? PdfiumEngine.FPDF_GetPageCount(_docHandleA) : 1;
            int countB = _docHandleB != IntPtr.Zero ? PdfiumEngine.FPDF_GetPageCount(_docHandleB) : 1;
            
            PageNumberTextBlockA.Text = $"{_currentPageA} / {countA}";
            PageNumberTextBlockB.Text = $"{_currentPageB} / {countB}";
        }

        private async void RenderCurrentPage()
        {
            StatusTextBlock.Text = "Đang xử lý kết xuất trang...";
            
            BitmapSource? bmpA = null;
            BitmapSource? bmpB = null;

            int pageIdxA = _currentPageA - 1;
            int pageIdxB = _currentPageB - 1;

            // Render File A
            if (_docHandleA != IntPtr.Zero)
            {
                int countA = PdfiumEngine.FPDF_GetPageCount(_docHandleA);
                if (pageIdxA >= 0 && pageIdxA < countA)
                {
                    bmpA = await Task.Run(() => RenderPage(_docHandleA, pageIdxA));
                    ImageA.Source = bmpA;
                }
                else
                {
                    ImageA.Source = null;
                }
            }

            // Render File B
            if (_docHandleB != IntPtr.Zero)
            {
                int countB = PdfiumEngine.FPDF_GetPageCount(_docHandleB);
                if (pageIdxB >= 0 && pageIdxB < countB)
                {
                    bmpB = await Task.Run(() => RenderPage(_docHandleB, pageIdxB));
                    ImageB.Source = bmpB;
                }
                else
                {
                    ImageB.Source = null;
                }
            }

            // Nếu đang chọn chế độ Overlay Diff và có cả 2 trang, tiến hành tạo ảnh so khớp pixel
            if (CompareModeCombo.SelectedIndex == 1) // Overlay Diff
            {
                if (bmpA != null && bmpB != null)
                {
                    StatusTextBlock.Text = "Đang tính toán sai khác (Visual Diff)...";
                    BitmapSource? diffBmp = await Task.Run(() => PdfImageComparer.Compare(bmpA, bmpB));
                    ImageOverlay.Source = diffBmp;
                    StatusTextBlock.Text = "So sánh hoàn tất.";
                }
                else
                {
                    ImageOverlay.Source = null;
                    StatusTextBlock.Text = "Cần chọn cả hai File A và File B để so sánh.";
                }
            }
            else
            {
                StatusTextBlock.Text = "Hiển thị trang hoàn tất.";
            }

            UpdatePageLimits();
        }

        private BitmapSource? RenderPage(nint docHandle, int pageIndex)
        {
            double width, height;
            if (!PdfiumEngine.TryGetPageSizeByIndex(docHandle, pageIndex, out width, out height))
            {
                width = 595;
                height = 842;
            }

            // Render trang ở DPI cao (150 DPI) để hình ảnh so sánh chi tiết và rõ nét
            double dpiFactor = 150.0 / 96.0;
            int targetWidth = (int)(width * dpiFactor);
            int targetHeight = (int)(height * dpiFactor);

            return PdfiumEngine.RenderPageToBitmap(docHandle, pageIndex, targetWidth, targetHeight, false);
        }

        // --- Scroll Syncing ---
        private void ScrollA_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (SyncScrollToggle.IsChecked == true && !_isSyncingScroll && CompareModeCombo.SelectedIndex == 0)
            {
                _isSyncingScroll = true;
                ScrollB.ScrollToVerticalOffset(e.VerticalOffset);
                ScrollB.ScrollToHorizontalOffset(e.HorizontalOffset);
                _isSyncingScroll = false;
            }
        }

        private void ScrollB_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (SyncScrollToggle.IsChecked == true && !_isSyncingScroll && CompareModeCombo.SelectedIndex == 0)
            {
                _isSyncingScroll = true;
                ScrollA.ScrollToVerticalOffset(e.VerticalOffset);
                ScrollA.ScrollToHorizontalOffset(e.HorizontalOffset);
                _isSyncingScroll = false;
            }
        }

        private void SyncScroll_Toggled(object sender, RoutedEventArgs e)
        {
            if (SyncScrollToggle.IsChecked == true && CompareModeCombo.SelectedIndex == 0)
            {
                _isSyncingScroll = true;
                ScrollB.ScrollToVerticalOffset(ScrollA.VerticalOffset);
                ScrollB.ScrollToHorizontalOffset(ScrollA.HorizontalOffset);
                _isSyncingScroll = false;
            }
        }

        // --- Navigation ---
        private void PrevPageA_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPageA > 1)
            {
                _currentPageA--;
                if (LockPageShiftToggle.IsChecked == true)
                {
                    int countB = _docHandleB != IntPtr.Zero ? PdfiumEngine.FPDF_GetPageCount(_docHandleB) : 1;
                    _currentPageB = Math.Clamp(_currentPageA + _pageOffset, 1, countB);
                }
                RenderCurrentPage();
            }
        }

        private void NextPageA_Click(object sender, RoutedEventArgs e)
        {
            int countA = _docHandleA != IntPtr.Zero ? PdfiumEngine.FPDF_GetPageCount(_docHandleA) : 1;
            if (_currentPageA < countA)
            {
                _currentPageA++;
                if (LockPageShiftToggle.IsChecked == true)
                {
                    int countB = _docHandleB != IntPtr.Zero ? PdfiumEngine.FPDF_GetPageCount(_docHandleB) : 1;
                    _currentPageB = Math.Clamp(_currentPageA + _pageOffset, 1, countB);
                }
                RenderCurrentPage();
            }
        }

        private void PrevPageB_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPageB > 1)
            {
                _currentPageB--;
                if (LockPageShiftToggle.IsChecked == true)
                {
                    int countA = _docHandleA != IntPtr.Zero ? PdfiumEngine.FPDF_GetPageCount(_docHandleA) : 1;
                    _currentPageA = Math.Clamp(_currentPageB - _pageOffset, 1, countA);
                }
                RenderCurrentPage();
            }
        }

        private void NextPageB_Click(object sender, RoutedEventArgs e)
        {
            int countB = _docHandleB != IntPtr.Zero ? PdfiumEngine.FPDF_GetPageCount(_docHandleB) : 1;
            if (_currentPageB < countB)
            {
                _currentPageB++;
                if (LockPageShiftToggle.IsChecked == true)
                {
                    int countA = _docHandleA != IntPtr.Zero ? PdfiumEngine.FPDF_GetPageCount(_docHandleA) : 1;
                    _currentPageA = Math.Clamp(_currentPageB - _pageOffset, 1, countA);
                }
                RenderCurrentPage();
            }
        }

        private void LockPageShift_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            if (LockPageShiftToggle.IsChecked == true)
            {
                _pageOffset = _currentPageB - _currentPageA;
            }
        }

        private void CompareMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;

            if (CompareModeCombo.SelectedIndex == 1) // Overlay mode
            {
                // Ẩn song song, hiện Overlay panel
                ScrollOverlay.Visibility = Visibility.Visible;
                ScrollA.Visibility = Visibility.Collapsed;
                ScrollB.Visibility = Visibility.Collapsed;
                ColumnB.Width = new GridLength(0);
                ColumnSplitter.Width = new GridLength(0);
                SyncScrollToggle.IsEnabled = false;
            }
            else // Side-by-side mode
            {
                ScrollOverlay.Visibility = Visibility.Collapsed;
                ScrollA.Visibility = Visibility.Visible;
                ScrollB.Visibility = Visibility.Visible;
                ColumnB.Width = new GridLength(1, GridUnitType.Star);
                ColumnSplitter.Width = GridLength.Auto;
                SyncScrollToggle.IsEnabled = true;
            }

            RenderCurrentPage();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            CloseDocHandle(ref _docHandleA);
            CloseDocHandle(ref _docHandleB);
        }

        private void CloseDocHandle(ref nint handle)
        {
            if (handle != IntPtr.Zero)
            {
                PdfiumEngine.CloseDocument(handle);
                handle = IntPtr.Zero;
            }
        }
    }
}
