using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Printing;
using System.Printing.Interop;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace PdfViewerApp
{
	public partial class BatchToolsWindow : Window
	{
		public ObservableCollection<BatchToolFileItem> Files { get; } = new ObservableCollection<BatchToolFileItem>();
		private CancellationTokenSource? _cancellationTokenSource;
		private bool _isProcessing = false;
		private string _selectedFolder = string.Empty;

		public BatchToolsWindow(int initialTabIndex = 0)
		{
			InitializeComponent();
			FileListView.ItemsSource = Files;
			UpdatePlaceholderVisibility();

			// Default output folder: Desktop
			_selectedFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
			CompressOutputDirTextBox.Text = _selectedFolder;
			ExtractOutputDirTextBox.Text = _selectedFolder;
			ConvertOutputDirTextBox.Text = _selectedFolder;

			if (initialTabIndex >= 0 && initialTabIndex < BatchTabControl.Items.Count)
			{
				BatchTabControl.SelectedIndex = initialTabIndex;
			}
		}

		private void UpdatePlaceholderVisibility()
		{
			DropPlaceholder.Visibility = Files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
		}

		private void NumberValidation_KeyDown(object sender, KeyEventArgs e)
		{
			if ((e.Key < Key.D0 || e.Key > Key.D9) && (e.Key < Key.NumPad0 || e.Key > Key.NumPad9) && e.Key != Key.Back && e.Key != Key.Delete && e.Key != Key.Tab)
			{
				e.Handled = true;
			}
		}

		// Drag Drop Handlers
		private void Window_DragOver(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
				if (files.Any(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
				                   f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
				                   f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
				                   f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
				                   Directory.Exists(f)))
				{
					FileListBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(20, 184, 166)); // Emerald green highlight
					e.Effects = DragDropEffects.Copy;
					e.Handled = true;
					return;
				}
			}
			FileListBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red for invalid files
			e.Effects = DragDropEffects.None;
			e.Handled = true;
		}

		private void Window_DragLeave(object sender, DragEventArgs e)
		{
			FileListBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)); // Reset to slate
		}

		private async void Window_Drop(object sender, DragEventArgs e)
		{
			FileListBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)); // Reset
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
				await AddPdfFilesAsync(files);
			}
		}

		private async void AddFiles_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Filter = "All supported files (*.pdf;*.jpg;*.jpeg;*.png)|*.pdf;*.jpg;*.jpeg;*.png|PDF documents (*.pdf)|*.pdf|Image files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
				Title = "Chọn các file PDF hoặc Hình ảnh",
				Multiselect = true
			};
			if (openFileDialog.ShowDialog() == true)
			{
				await AddPdfFilesAsync(openFileDialog.FileNames);
			}
		}

		private async Task AddPdfFilesAsync(string[] paths)
		{
			var validFiles = new List<string>();
			await Task.Run(() =>
			{
				foreach (var path in paths)
				{
					if (File.Exists(path))
					{
						if (path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
						    path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
						    path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
						    path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
						{
							validFiles.Add(path);
						}
					}
					else if (Directory.Exists(path))
					{
						try
						{
							var filesInDir = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
								.Where(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
								            f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
								            f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
								            f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
							validFiles.AddRange(filesInDir);
						}
						catch { }
					}
				}
			});

			if (validFiles.Count == 0) return;

			OverallStatusText.Text = $"Đang đọc thông tin tệp...";
			foreach (var path in validFiles)
			{
				if (Files.Any(f => string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase)))
					continue;

				long sizeBytes = 0;
				try
				{
					sizeBytes = new FileInfo(path).Length;
				}
				catch { }

				bool isImage = !path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

				var newItem = new BatchToolFileItem
				{
					FilePath = path,
					SizeBytes = sizeBytes,
					Status = isImage ? "Sẵn sàng" : "Đang tải số trang..."
				};
				if (isImage)
				{
					newItem.PageCount = 1;
				}
				Files.Add(newItem);
			}

			ReindexItems();
			UpdatePlaceholderVisibility();

			// Load page count asynchronously using PDFiumEngine
			await Task.Run(() =>
			{
				PdfiumEngine.Initialize();
				foreach (var item in Files.ToList())
				{
					if (item.PageCount > 0) continue;
					if (!item.FilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) continue;

					int pages = 0;
					bool success = false;
					lock (PdfiumEngine.SyncRoot)
					{
						nint doc = PdfiumEngine.FPDF_LoadDocument(item.FilePath, null);
						if (doc != IntPtr.Zero)
						{
							pages = PdfiumEngine.FPDF_GetPageCount(doc);
							PdfiumEngine.FPDF_CloseDocument(doc);
							success = true;
						}
					}

					if (success)
					{
						Dispatcher.Invoke(() =>
						{
							item.PageCount = pages;
							item.Status = "Sẵn sàng";
						});
					}
					else
					{
						Dispatcher.Invoke(() =>
						{
							item.Status = "Lỗi đọc tệp";
						});
					}
				}
			});

			OverallStatusText.Text = $"Đã nạp thêm {validFiles.Count} tệp.";
		}

		private void RemoveFile_Click(object sender, RoutedEventArgs e)
		{
			var selectedItems = FileListView.SelectedItems.Cast<BatchToolFileItem>().ToList();
			foreach (var item in selectedItems)
			{
				Files.Remove(item);
			}
			ReindexItems();
			UpdatePlaceholderVisibility();
		}

		private void ClearAll_Click(object sender, RoutedEventArgs e)
		{
			Files.Clear();
			UpdatePlaceholderVisibility();
			OverallStatusText.Text = "Đã dọn dẹp danh sách tệp.";
			OverallProgressBar.Value = 0;
		}

		private void MoveUp_Click(object sender, RoutedEventArgs e)
		{
			int index = FileListView.SelectedIndex;
			if (index > 0)
			{
				Files.Move(index, index - 1);
				ReindexItems();
				FileListView.SelectedIndex = index - 1;
			}
		}

		private void MoveDown_Click(object sender, RoutedEventArgs e)
		{
			int index = FileListView.SelectedIndex;
			if (index >= 0 && index < Files.Count - 1)
			{
				Files.Move(index, index + 1);
				ReindexItems();
				FileListView.SelectedIndex = index + 1;
			}
		}

		private void ReindexItems()
		{
			for (int i = 0; i < Files.Count; i++)
			{
				Files[i].Index = i + 1;
			}
		}

		private void Close_Click(object sender, RoutedEventArgs e)
		{
			if (_isProcessing)
			{
				if (MessageBox.Show(this, "Tiến trình đang chạy. Bạn có chắc chắn muốn đóng hộp thoại?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
				{
					return;
				}
				_cancellationTokenSource?.Cancel();
			}
			Close();
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			_cancellationTokenSource?.Cancel();
			OverallStatusText.Text = "Đang gửi yêu cầu hủy...";
		}

		private void BrowseFolder_Click(object sender, RoutedEventArgs e)
		{
			var dialog = new OpenFolderDialog
			{
				Title = "Chọn thư mục lưu kết quả",
				InitialDirectory = _selectedFolder
			};

			if (dialog.ShowDialog() == true)
			{
				_selectedFolder = dialog.FolderName;
				CompressOutputDirTextBox.Text = _selectedFolder;
				ExtractOutputDirTextBox.Text = _selectedFolder;
				ConvertOutputDirTextBox.Text = _selectedFolder;
			}
		}

		private void CompressMode_Checked(object sender, RoutedEventArgs e)
		{
			if (LosslessOptionsPanel == null || StandardOptionsPanel == null) return;
			if (CompressModeLossless.IsChecked == true)
			{
				LosslessOptionsPanel.Visibility = Visibility.Visible;
				StandardOptionsPanel.Visibility = Visibility.Collapsed;
			}
			else
			{
				LosslessOptionsPanel.Visibility = Visibility.Collapsed;
				StandardOptionsPanel.Visibility = Visibility.Visible;
			}
		}

		private void BatchTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (e.Source is TabControl)
			{
				OverallProgressBar.Value = 0;
				OverallStatusText.Text = "Sẵn sàng";
			}
		}

		private async void Start_Click(object sender, RoutedEventArgs e)
		{
			if (Files.Count == 0)
			{
				MessageBox.Show(this, "Vui lòng chọn ít nhất một file PDF.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			TabItem? selectedTab = BatchTabControl.SelectedItem as TabItem;
			if (selectedTab == null) return;

			_isProcessing = true;
			SetControlsEnabled(false);
			_cancellationTokenSource = new CancellationTokenSource();
			CancellationToken token = _cancellationTokenSource.Token;

			OverallProgressBar.Maximum = Files.Count;
			OverallProgressBar.Value = 0;

			try
			{
				if (selectedTab.Header.ToString() == "Nén PDF")
				{
					if (CompressModeLossless.IsChecked == true)
					{
						await StartLosslessCompressFlowAsync(token);
					}
					else
					{
						await StartCompressFlowAsync(token);
					}
				}
				else if (selectedTab.Header.ToString() == "Trích Xuất Trang")
				{
					await StartExtractFlowAsync(token);
				}
				else if (selectedTab.Header.ToString() == "Chuyển Đổi")
				{
					await StartConvertFlowAsync(token);
				}
			}
			catch (OperationCanceledException)
			{
				OverallStatusText.Text = "Đã hủy tiến trình xử lý.";
				foreach (var file in Files)
				{
					if (file.Status.StartsWith("Đang"))
					{
						file.Status = "Đã hủy";
					}
				}
				MessageBox.Show(this, "Đã hủy tiến trình theo yêu cầu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
			}
			finally
			{
				_isProcessing = false;
				SetControlsEnabled(true);
				_cancellationTokenSource = null;
			}
		}

		private async Task StartCompressFlowAsync(CancellationToken token)
		{
			byte quality = (byte)CompressQualitySlider.Value;

			for (int i = 0; i < Files.Count; i++)
			{
				token.ThrowIfCancellationRequested();
				var fileItem = Files[i];
				fileItem.Status = "Đang nén...";
				OverallStatusText.Text = $"Đang nén tệp {i + 1}/{Files.Count}: {fileItem.FileName}...";

				string outPath = Path.Combine(_selectedFolder, $"compressed_{fileItem.FileName}");
				bool success = false;

				await Task.Run(() =>
				{
					try
					{
						success = PdfInterop.PdfCore.compress_pdf(fileItem.FilePath, quality, outPath);
					}
					catch (OperationCanceledException)
					{
						throw;
					}
					catch { }
				});

				fileItem.Status = success ? "Thành công" : "Lỗi xử lý";
				OverallProgressBar.Value = i + 1;
			}

			OverallStatusText.Text = "Đã hoàn thành nén tất cả tệp!";
			MessageBox.Show(this, "Hoàn tất tiến trình nén dung lượng hàng loạt!", "Nén PDF", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		private async Task StartLosslessCompressFlowAsync(CancellationToken token)
		{
			bool removeMetadata = OptRemoveMetadataCheckBox.IsChecked == true;

			for (int i = 0; i < Files.Count; i++)
			{
				token.ThrowIfCancellationRequested();
				var fileItem = Files[i];
				fileItem.Status = "Đang tối ưu...";
				OverallStatusText.Text = $"Đang tối ưu tệp {i + 1}/{Files.Count}: {fileItem.FileName}...";

				string outPath = Path.Combine(_selectedFolder, $"optimized_{fileItem.FileName}");
				bool success = false;

				await Task.Run(() =>
				{
					try
					{
						success = PdfInterop.PdfCore.optimize_pdf_lossless(fileItem.FilePath, removeMetadata, outPath);
					}
					catch (OperationCanceledException)
					{
						throw;
					}
					catch { }
				});

				fileItem.Status = success ? "Thành công" : "Lỗi xử lý";
				OverallProgressBar.Value = i + 1;
			}

			OverallStatusText.Text = "Đã hoàn thành tối ưu tất cả tệp!";
			MessageBox.Show(this, "Hoàn tất tiến trình tối ưu nén tệp hàng loạt!", "Nén Tối Ưu", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		private async Task StartExtractFlowAsync(CancellationToken token)
		{
			string pagesStr = ExtractPageRangeTextBox.Text.Trim().Replace(",", ";");

			for (int i = 0; i < Files.Count; i++)
			{
				token.ThrowIfCancellationRequested();
				var fileItem = Files[i];
				fileItem.Status = "Đang trích xuất...";
				OverallStatusText.Text = $"Đang trích xuất tệp {i + 1}/{Files.Count}: {fileItem.FileName}...";

				string outPath = Path.Combine(_selectedFolder, $"extracted_{fileItem.FileName}");
				bool success = false;

				await Task.Run(() =>
				{
					try
					{
						// Check range valid
						if (TryParsePageRange(pagesStr.Replace(";", "-"), fileItem.PageCount, out _, out _))
						{
							success = PdfInterop.PdfCore.extract_pdf_pages(fileItem.FilePath, pagesStr, outPath);
						}
					}
					catch (OperationCanceledException)
					{
						throw;
					}
					catch { }
				});

				fileItem.Status = success ? "Thành công" : "Dải trang không hợp lệ";
				OverallProgressBar.Value = i + 1;
			}

			OverallStatusText.Text = "Đã hoàn thành trích xuất tất cả tệp!";
			MessageBox.Show(this, "Hoàn tất tiến trình trích xuất hàng loạt!", "Trích xuất trang", MessageBoxButton.OK, MessageBoxImage.Information);
		}



		private void SetControlsEnabled(bool enabled)
		{
			StartBtn.IsEnabled = enabled;
			CloseBtn.IsEnabled = enabled;
			CancelBtn.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;

			AddFilesBtn.IsEnabled = enabled;
			RemoveFileBtn.IsEnabled = enabled;
			ClearAllBtn.IsEnabled = enabled;
			MoveUpBtn.IsEnabled = enabled;
			MoveDownBtn.IsEnabled = enabled;
			FileListView.IsEnabled = enabled;

			CompressQualitySlider.IsEnabled = enabled;
			CompressBrowseBtn.IsEnabled = enabled;

			OptCompressStreamsCheckBox.IsEnabled = enabled;
			OptPruneObjectsCheckBox.IsEnabled = enabled;
			OptRemoveMetadataCheckBox.IsEnabled = enabled;

			ExtractPageRangeTextBox.IsEnabled = enabled;
			ExtractBrowseBtn.IsEnabled = enabled;

			ConvertDirectionComboBox.IsEnabled = enabled;
			ConvertBrowseBtn.IsEnabled = enabled;
		}



		private async Task StartConvertFlowAsync(CancellationToken token)
		{
			string direction = (ConvertDirectionComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "PdfToImage";

			if (direction == "PdfToImage")
			{
				for (int i = 0; i < Files.Count; i++)
				{
					token.ThrowIfCancellationRequested();
					var fileItem = Files[i];
					fileItem.Status = "Đang trích xuất ảnh...";
					OverallStatusText.Text = $"Đang trích xuất tệp {i + 1}/{Files.Count}: {fileItem.FileName}...";

					string subFolder = Path.Combine(_selectedFolder, Path.GetFileNameWithoutExtension(fileItem.FileName) + "_images");
					try
					{
						if (!Directory.Exists(subFolder))
							Directory.CreateDirectory(subFolder);
					}
					catch { }

					int count = -1;
					await Task.Run(() =>
					{
						try
						{
							count = PdfInterop.PdfCore.extract_pdf_images(fileItem.FilePath, subFolder);
						}
						catch (OperationCanceledException)
						{
							throw;
						}
						catch { }
					});

					fileItem.Status = count >= 0 ? $"Thành công ({count} ảnh)" : "Lỗi trích xuất";
					OverallProgressBar.Value = i + 1;
				}
				OverallStatusText.Text = "Đã hoàn thành trích xuất ảnh!";
				MessageBox.Show(this, "Hoàn tất trích xuất ảnh hàng loạt!", "Chuyển đổi", MessageBoxButton.OK, MessageBoxImage.Information);
			}
			else
			{
				// Convert Image to PDF
				for (int i = 0; i < Files.Count; i++)
				{
					token.ThrowIfCancellationRequested();
					var fileItem = Files[i];
					if (fileItem.FilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					fileItem.Status = "Đang chuyển đổi...";
					OverallStatusText.Text = $"Đang chuyển đổi {i + 1}/{Files.Count}: {fileItem.FileName}...";

					string outPath = Path.Combine(_selectedFolder, Path.GetFileNameWithoutExtension(fileItem.FileName) + ".pdf");
					bool success = false;

					await Task.Run(() =>
					{
						try
						{
							using (var doc = new PdfSharp.Pdf.PdfDocument())
							{
								var page = doc.AddPage();
								using (var xImage = PdfSharp.Drawing.XImage.FromFile(fileItem.FilePath))
								{
									page.Width = PdfSharp.Drawing.XUnit.FromPoint(xImage.PointWidth);
									page.Height = PdfSharp.Drawing.XUnit.FromPoint(xImage.PointHeight);

									using (var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page))
									{
										gfx.DrawImage(xImage, 0, 0, page.Width.Point, page.Height.Point);
									}
								}
								doc.Save(outPath);
								success = true;
							}
						}
						catch (OperationCanceledException)
						{
							throw;
						}
						catch { }
					});

					fileItem.Status = success ? "Thành công" : "Lỗi chuyển đổi";
					OverallProgressBar.Value = i + 1;
				}
				OverallStatusText.Text = "Đã hoàn thành chuyển đổi ảnh sang PDF!";
				MessageBox.Show(this, "Hoàn tất chuyển đổi ảnh sang PDF!", "Chuyển đổi", MessageBoxButton.OK, MessageBoxImage.Information);
			}
		}

		private bool TryParsePageRange(string text, int pageCount, out int start, out int end)
		{
			start = 1;
			end = pageCount;
			text = text.Trim();
			if (string.IsNullOrEmpty(text)) return false;

			if (text.Contains("-"))
			{
				var parts = text.Split('-');
				if (parts.Length == 2 && int.TryParse(parts[0], out int s) && int.TryParse(parts[1], out int e))
				{
					if (s >= 1 && e >= s && e <= pageCount)
					{
						start = s;
						end = e;
						return true;
					}
				}
				return false;
			}

			if (int.TryParse(text, out int pageNum))
			{
				if (pageNum >= 1 && pageNum <= pageCount)
				{
					start = pageNum;
					end = pageNum;
					return true;
				}
			}

			return false;
		}


	}

	public class BatchToolFileItem : INotifyPropertyChanged
	{
		private int _index;
		private string _filePath = string.Empty;
		private int _pageCount = 0;
		private long _sizeBytes;
		private string _status = "Đang chờ";

		public int Index
		{
			get => _index;
			set { _index = value; OnPropertyChanged(); }
		}

		public string FilePath
		{
			get => _filePath;
			set { _filePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileName)); }
		}

		public string FileName => Path.GetFileName(FilePath);

		public int PageCount
		{
			get => _pageCount;
			set { _pageCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(PageCountText)); }
		}

		public string PageCountText => PageCount > 0 ? PageCount.ToString() : "...";

		public long SizeBytes
		{
			get => _sizeBytes;
			set { _sizeBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileSizeText)); }
		}

		public string FileSizeText => FormatBytes(SizeBytes);

		public string Status
		{
			get => _status;
			set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusColor)); }
		}

		public Brush StatusColor
		{
			get
			{
				if (Status == "Thành công") return new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Emerald green
				if (Status.StartsWith("Lỗi") || Status == "Đã hủy" || Status == "Lỗi xử lý" || Status == "Dải trang không hợp lệ") return new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
				if (Status.StartsWith("Đang")) return new SolidColorBrush(Color.FromRgb(56, 189, 248)); // Sky blue
				return new SolidColorBrush(Color.FromRgb(148, 163, 184)); // Gray
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;
		protected void OnPropertyChanged([CallerMemberName] string? name = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}

		private static string FormatBytes(long bytes)
		{
			string[] array = new string[4] { "B", "KB", "MB", "GB" };
			double num = bytes;
			int num2 = 0;
			while (num >= 1024.0 && num2 < array.Length - 1)
			{
				num /= 1024.0;
				num2++;
			}
			return $"{num:0.##} {array[num2]}";
		}
	}
}
