using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PdfViewerApp
{
    public class PageOrganizerItem : INotifyPropertyChanged
    {
        private int _pageNumber;
        private string _pageLabel = "";
        private BitmapSource? _thumbnail;
        private int _rotationAngle;

        public int PageNumber
        {
            get => _pageNumber;
            set { _pageNumber = value; OnPropertyChanged(nameof(PageNumber)); }
        }

        public string PageLabel
        {
            get => _pageLabel;
            set { _pageLabel = value; OnPropertyChanged(nameof(PageLabel)); }
        }

        public BitmapSource? Thumbnail
        {
            get => _thumbnail;
            set { _thumbnail = value; OnPropertyChanged(nameof(Thumbnail)); }
        }

        public int RotationAngle
        {
            get => _rotationAngle;
            set { _rotationAngle = value; OnPropertyChanged(nameof(RotationAngle)); }
        }

        public bool IsBlank { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class PageOrganizerWindow : Window
    {
        private readonly string _pdfPath;
        private readonly nint _documentHandle;
        private readonly ObservableCollection<PageOrganizerItem> _items = new ObservableCollection<PageOrganizerItem>();
        private Point _dragStartPoint;
        private bool _isSaving;

        public string? SavedPdfPath { get; private set; }

        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool reorder_pdf_pages([MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath, [MarshalAs(UnmanagedType.LPUTF8Str)] string orderSemicolon, [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool rotate_pdf_page([MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath, int pageNumber, int rotationDelta, [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool delete_pdf_page([MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath, int pageNumber, [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool insert_blank_page([MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath, int targetPage, bool insertBefore, [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

        public PageOrganizerWindow(string pdfPath, nint documentHandle)
        {
            InitializeComponent();
            _pdfPath = pdfPath;
            _documentHandle = documentHandle;
            PagesListBox.ItemsSource = _items;

            // Setup Drag & Drop Handlers
            PagesListBox.PreviewMouseLeftButtonDown += ListBox_PreviewMouseLeftButtonDown;
            PagesListBox.MouseMove += ListBox_MouseMove;
            PagesListBox.Drop += ListBox_Drop;
            PagesListBox.AllowDrop = true;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_pdfPath) || _documentHandle == IntPtr.Zero)
            {
                Close();
                return;
            }

            StatusTextBlock.Text = "Đang tải ảnh thu nhỏ...";
            int pageCount = PdfiumEngine.FPDF_GetPageCount(_documentHandle);

            await Task.Run(() =>
            {
                for (int i = 1; i <= pageCount; i++)
                {
                    int pageNum = i;
                    double width, height;
                    PdfiumEngine.TryGetPageSizeByIndex(_documentHandle, pageNum - 1, out width, out height);
                    int thumbWidth = 120;
                    int thumbHeight = Math.Max(1, (int)(height / Math.Max(1.0, width) * thumbWidth));

                    BitmapSource? bitmap = PdfiumEngine.RenderPageToBitmap(_documentHandle, pageNum - 1, thumbWidth, thumbHeight);
                    
                    Dispatcher.Invoke(() =>
                    {
                        _items.Add(new PageOrganizerItem
                        {
                            PageNumber = pageNum,
                            PageLabel = $"Trang {pageNum}",
                            Thumbnail = bitmap,
                            RotationAngle = 0,
                            IsBlank = false
                        });
                        StatusTextBlock.Text = $"Đã tải trang {pageNum}/{pageCount}";
                    });
                }
            });

            StatusTextBlock.Text = $"Đã tải hoàn tất {pageCount} trang. Kéo thả để sắp xếp lại.";
        }

        // --- Drag and Drop ---
        private void ListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void ListBox_MouseMove(object sender, MouseEventArgs e)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = _dragStartPoint - mousePos;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                ListBox listBox = (ListBox)sender;
                DependencyObject dragSource = e.OriginalSource as DependencyObject;
                ListBoxItem? listBoxItem = FindVisualParent<ListBoxItem>(dragSource);

                if (listBoxItem != null && listBoxItem.DataContext is PageOrganizerItem draggedItem)
                {
                    DragDrop.DoDragDrop(listBoxItem, draggedItem, DragDropEffects.Move);
                }
            }
        }

        private void ListBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(PageOrganizerItem)))
            {
                PageOrganizerItem droppedData = (PageOrganizerItem)e.Data.GetData(typeof(PageOrganizerItem));
                ListBox listBox = (ListBox)sender;
                DependencyObject dropTarget = e.OriginalSource as DependencyObject;
                ListBoxItem? targetItem = FindVisualParent<ListBoxItem>(dropTarget);

                int targetIdx = -1;
                if (targetItem != null)
                {
                    targetIdx = listBox.Items.IndexOf(targetItem.DataContext);
                }
                else
                {
                    targetIdx = listBox.Items.Count;
                }

                int sourceIdx = _items.IndexOf(droppedData);

                if (sourceIdx != -1 && targetIdx != -1 && sourceIdx != targetIdx)
                {
                    _items.RemoveAt(sourceIdx);
                    if (targetIdx > sourceIdx && targetIdx < _items.Count + 1)
                    {
                        targetIdx--; // Adjust for item removal
                    }
                    _items.Insert(targetIdx, droppedData);
                    UpdateLabels();
                }
            }
        }

        private void UpdateLabels()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                string label = _items[i].IsBlank ? "Trang Trắng" : $"Trang {_items[i].PageNumber}";
                if (_items[i].RotationAngle != 0)
                {
                    label += $" ({_items[i].RotationAngle}°)";
                }
                _items[i].PageLabel = label;
            }
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent) return parent;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        // --- Toolbar Operations ---
        private void RotateLeft_Click(object sender, RoutedEventArgs e)
        {
            var selected = PagesListBox.SelectedItems.Cast<PageOrganizerItem>().ToList();
            if (selected.Count == 0) return;

            foreach (var item in selected)
            {
                item.RotationAngle = (item.RotationAngle - 90) % 360;
                if (item.RotationAngle < 0) item.RotationAngle += 360;
            }
            UpdateLabels();
        }

        private void RotateRight_Click(object sender, RoutedEventArgs e)
        {
            var selected = PagesListBox.SelectedItems.Cast<PageOrganizerItem>().ToList();
            if (selected.Count == 0) return;

            foreach (var item in selected)
            {
                item.RotationAngle = (item.RotationAngle + 90) % 360;
            }
            UpdateLabels();
        }

        private void DeletePage_Click(object sender, RoutedEventArgs e)
        {
            var selected = PagesListBox.SelectedItems.Cast<PageOrganizerItem>().ToList();
            if (selected.Count == 0) return;

            if (MessageBox.Show($"Bạn có chắc chắn muốn xóa {selected.Count} trang đã chọn?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                foreach (var item in selected)
                {
                    _items.Remove(item);
                }
                UpdateLabels();
            }
        }

        private void InsertBlank_Click(object sender, RoutedEventArgs e)
        {
            int insertIdx = _items.Count;
            if (PagesListBox.SelectedIndex != -1)
            {
                insertIdx = PagesListBox.SelectedIndex + 1;
            }

            _items.Insert(insertIdx, new PageOrganizerItem
            {
                PageNumber = -1,
                PageLabel = "Trang Trắng",
                Thumbnail = CreateBlankThumbnail(),
                RotationAngle = 0,
                IsBlank = true
            });
            UpdateLabels();
        }

        private BitmapSource CreateBlankThumbnail()
        {
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRectangle(Brushes.White, null, new Rect(0, 0, 120, 160));
                FormattedText formattedText = new FormattedText(
                    "TRANG TRẮNG",
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    12,
                    Brushes.DarkGray,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                
                drawingContext.DrawText(formattedText, new Point(20, 70));
            }

            RenderTargetBitmap rtb = new RenderTargetBitmap(120, 160, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(drawingVisual);
            rtb.Freeze();
            return rtb;
        }

        // --- Save Changes ---
        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_isSaving) return;
            if (_items.Count == 0)
            {
                MessageBox.Show("Không thể lưu tài liệu PDF không có trang nào.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _isSaving = true;
            StatusTextBlock.Text = "Đang xử lý và lưu tài liệu PDF...";
            PagesListBox.IsEnabled = false;

            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "PdfProOrganizer");
                Directory.CreateDirectory(tempDir);
                string currentTempFile = Path.Combine(tempDir, $"{Guid.NewGuid():N}.pdf");
                File.Copy(_pdfPath, currentTempFile, true);

                await Task.Run(() =>
                {
                    // Step 1: Reorder / Delete
                    // We generate the ordered list of existing pages
                    // Items that are blank will be handled separately
                    var existingPageOrder = _items.Where(i => !i.IsBlank).Select(i => i.PageNumber).ToList();
                    string orderSemicolon = string.Join(";", existingPageOrder);

                    string step1File = Path.Combine(tempDir, $"{Guid.NewGuid():N}.pdf");
                    if (existingPageOrder.Count > 0)
                    {
                        reorder_pdf_pages(currentTempFile, orderSemicolon, step1File);
                        currentTempFile = step1File;
                    }

                    // Step 2: Insert Blank Pages
                    // Since the items list might contain blank pages in between,
                    // we scan the collection and insert blank pages at their index.
                    // Loop backwards to keep index calculations simple
                    for (int i = 0; i < _items.Count; i++)
                    {
                        if (_items[i].IsBlank)
                        {
                            string step2File = Path.Combine(tempDir, $"{Guid.NewGuid():N}.pdf");
                            // insert_blank_page takes 1-based index targetPage
                            // targetPage is the page index where we insert
                            // insertBefore: if true, before i+1 (which is at index i).
                            insert_blank_page(currentTempFile, i + 1, true, step2File);
                            currentTempFile = step2File;
                        }
                    }

                    // Step 3: Apply Rotations
                    // Scan the collection and rotate pages
                    for (int i = 0; i < _items.Count; i++)
                    {
                        if (_items[i].RotationAngle != 0)
                        {
                            string step3File = Path.Combine(tempDir, $"{Guid.NewGuid():N}.pdf");
                            rotate_pdf_page(currentTempFile, i + 1, _items[i].RotationAngle, step3File);
                            currentTempFile = step3File;
                        }
                    }
                });

                SavedPdfPath = currentTempFile;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Đã xảy ra lỗi khi tổ chức các trang: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Lỗi khi lưu thay đổi.";
                PagesListBox.IsEnabled = true;
                _isSaving = false;
            }
        }
    }
}
