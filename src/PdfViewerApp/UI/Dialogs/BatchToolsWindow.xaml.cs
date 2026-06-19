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

		public BatchToolsWindow()
		{
			InitializeComponent();
			FileListView.ItemsSource = Files;
			LoadPrinters();
			UpdatePlaceholderVisibility();

			// Default output folder: Desktop
			_selectedFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
			RotateOutputDirTextBox.Text = _selectedFolder;
			CompressOutputDirTextBox.Text = _selectedFolder;
			ExtractOutputDirTextBox.Text = _selectedFolder;
			MergeOutputDirTextBox.Text = _selectedFolder;
		}

		private void UpdatePlaceholderVisibility()
		{
			DropPlaceholder.Visibility = Files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
		}

		private void LoadPrinters()
		{
			List<PrintQueue> list = new List<PrintQueue>();
			try
			{
				list = new LocalPrintServer().GetPrintQueues(new[]
				{
					EnumeratedPrintQueueTypes.Local,
					EnumeratedPrintQueueTypes.Connections
				}).ToList();
			}
			catch
			{
				try
				{
					list = new LocalPrintServer().GetPrintQueues(new[]
					{
						EnumeratedPrintQueueTypes.Local
					}).ToList();
				}
				catch
				{
					try
					{
						PrintQueue defaultQueue = LocalPrintServer.GetDefaultPrintQueue();
						if (defaultQueue != null)
						{
							list.Add(defaultQueue);
						}
					}
					catch
					{
					}
				}
			}

			PrinterComboBox.ItemsSource = list;
			if (list.Count > 0)
			{
				if (!string.IsNullOrEmpty(PrintOptionsDialog.LastSelectedPrinterName))
				{
					var lastQueue = list.FirstOrDefault(q => q.FullName == PrintOptionsDialog.LastSelectedPrinterName);
					if (lastQueue != null)
					{
						PrinterComboBox.SelectedItem = lastQueue;
						return;
					}
				}
				try
				{
					PrintQueue defaultQueue = LocalPrintServer.GetDefaultPrintQueue();
					PrinterComboBox.SelectedItem = list.FirstOrDefault(q => q.FullName == defaultQueue.FullName) ?? list.FirstOrDefault();
				}
				catch
				{
					PrinterComboBox.SelectedItem = list.FirstOrDefault();
				}
			}
		}

		private void PrinterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (PrinterComboBox.SelectedItem is PrintQueue printQueue)
			{
				PrintOptionsDialog.LastSelectedPrinterName = printQueue.FullName;
			}
		}

		private void PageRangeRadio_Checked(object sender, RoutedEventArgs e)
		{
			if (PageRangeTextBox != null)
			{
				PageRangeTextBox.IsEnabled = CustomPagesRadio.IsChecked == true;
			}
		}

		private void RotateRadio_Checked(object sender, RoutedEventArgs e)
		{
			if (RotatePageRangeTextBox != null)
			{
				RotatePageRangeTextBox.IsEnabled = RotateCustomRadio.IsChecked == true;
			}
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
				if (files.Any(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)))
				{
					e.Effects = DragDropEffects.Copy;
					e.Handled = true;
					return;
				}
			}
			e.Effects = DragDropEffects.None;
			e.Handled = true;
		}

		private async void Window_Drop(object sender, DragEventArgs e)
		{
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
				Filter = "PDF documents (*.pdf)|*.pdf",
				Title = "Chọn các file PDF",
				Multiselect = true
			};
			if (openFileDialog.ShowDialog() == true)
			{
				await AddPdfFilesAsync(openFileDialog.FileNames);
			}
		}

		private async Task AddPdfFilesAsync(string[] paths)
		{
			var pdfFiles = paths.Where(p => p.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && File.Exists(p)).ToList();
			if (pdfFiles.Count == 0) return;

			OverallStatusText.Text = $"Đang đọc thông tin tệp...";
			foreach (var path in pdfFiles)
			{
				if (Files.Any(f => string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase)))
					continue;

				long sizeBytes = 0;
				try
				{
					sizeBytes = new FileInfo(path).Length;
				}
				catch { }

				var newItem = new BatchToolFileItem
				{
					FilePath = path,
					SizeBytes = sizeBytes,
					Status = "Đang tải số trang..."
				};
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

			OverallStatusText.Text = $"Đã nạp thêm {pdfFiles.Count} tệp.";
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
				RotateOutputDirTextBox.Text = _selectedFolder;
				CompressOutputDirTextBox.Text = _selectedFolder;
				ExtractOutputDirTextBox.Text = _selectedFolder;
				MergeOutputDirTextBox.Text = _selectedFolder;
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
				if (selectedTab.Header.ToString() == "In Hàng Loạt")
				{
					await StartPrintFlowAsync(token);
				}
				else if (selectedTab.Header.ToString() == "Xoay Trang")
				{
					await StartRotateFlowAsync(token);
				}
				else if (selectedTab.Header.ToString() == "Nén PDF")
				{
					await StartCompressFlowAsync(token);
				}
				else if (selectedTab.Header.ToString() == "Trích Xuất Trang")
				{
					await StartExtractFlowAsync(token);
				}
				else if (selectedTab.Header.ToString() == "Gộp File")
				{
					await StartMergeFlowAsync(token);
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

		private async Task StartPrintFlowAsync(CancellationToken token)
		{
			if (PrinterComboBox.SelectedItem is not PrintQueue selectedQueue)
			{
				MessageBox.Show(this, "Vui lòng chọn máy in hợp lệ.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			if (!int.TryParse(CopiesTextBox.Text, out int copies) || copies < 1)
			{
				MessageBox.Show(this, "Số bản sao (Copies) phải là một số nguyên dương từ 1.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			string printEngine = (PrintEngineComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "NativePdfium";
			bool fitToPrintableArea = FitMarginsRadio.IsChecked == true;
			bool autoCenter = AutoCenterCheckBox.IsChecked == true;
			bool separatePageJobs = SeparateJobsCheckBox.IsChecked == true;
			bool forceRasterize = OptimizeCadCheckBox.IsChecked == true;

			string printerQueueName = selectedQueue.FullName;
			PrinterPrintProfile profile = PrinterPrintProfile.Resolve(selectedQueue);

			byte[]? devModeBytes = null;
			try
			{
				using var converter = new PrintTicketConverter(selectedQueue.FullName, selectedQueue.ClientPrintSchemaVersion);
				var ticket = selectedQueue.UserPrintTicket ?? selectedQueue.DefaultPrintTicket;
				if (ticket != null)
				{
					var cloned = ticket.Clone();
					cloned.CopyCount = 1;
					devModeBytes = converter.ConvertPrintTicketToDevMode(cloned, BaseDevModeType.UserDefault);
				}
			}
			catch { }

			for (int i = 0; i < Files.Count; i++)
			{
				token.ThrowIfCancellationRequested();
				var fileItem = Files[i];
				fileItem.Status = "Đang chuẩn bị in...";
				OverallStatusText.Text = $"Đang in {i + 1}/{Files.Count}: {fileItem.FileName}...";

				if (fileItem.PageCount <= 0)
				{
					fileItem.Status = "Lỗi đọc tệp";
					continue;
				}

				int startPageIndex = 0;
				int endPageIndex = fileItem.PageCount - 1;

				if (CustomPagesRadio.IsChecked == true)
				{
					if (!TryParsePageRange(PageRangeTextBox.Text, fileItem.PageCount, out int s, out int eVal))
					{
						fileItem.Status = "Lỗi dải trang";
						continue;
					}
					startPageIndex = s - 1;
					endPageIndex = eVal - 1;
				}

				fileItem.Status = "Đang in...";

				IProgress<PrintProgressInfo> itemProgress = new Progress<PrintProgressInfo>(info =>
				{
					Dispatcher.Invoke(() =>
					{
						if (info.TotalPages > 0)
						{
							fileItem.Status = $"Đang in: trang {info.CurrentPage}/{info.TotalPages}";
						}
						else
						{
							fileItem.Status = info.Message;
						}
					});
				});

				bool success = false;
				string errorMessage = string.Empty;

				await Task.Run(() =>
				{
					try
					{
						if (printEngine == "NativePdfium_Optimized")
						{
							NativePdfPrinter.PrintOptimized(
								fileItem.FilePath,
								printerQueueName,
								devModeBytes,
								startPageIndex,
								endPageIndex,
								copies,
								fitToPrintableArea,
								autoCenter,
								profile.DriverAlreadyOffsetsPrintableArea,
								profile.RightSafetyPadding,
								profile.BottomSafetyPadding,
								separatePageJobs,
								false,
								forceRasterize,
								itemProgress,
								token);
							success = true;
						}
						else
						{
							// WPF Bitmap Printing Fallback
							Dispatcher.Invoke(() =>
							{
								var printDialog = new PrintDialog();
								printDialog.PrintQueue = selectedQueue;
								var ticket = selectedQueue.UserPrintTicket ?? selectedQueue.DefaultPrintTicket;
								if (ticket != null)
								{
									var cloned = ticket.Clone();
									cloned.CopyCount = copies;
									printDialog.PrintTicket = cloned;
								}

								var paginator = new PdfDocumentPaginator(fileItem.FilePath)
								{
									StartPage = startPageIndex,
									EndPage = endPageIndex,
									PrintProgress = itemProgress
								};
								
								printDialog.PrintDocument(paginator, Path.GetFileName(fileItem.FilePath));
								success = true;
							});
						}
					}
					catch (OperationCanceledException)
					{
						throw;
					}
					catch (Exception ex)
					{
						errorMessage = ex.Message;
					}
				});

				fileItem.Status = success ? "Thành công" : $"Lỗi: {errorMessage}";
				OverallProgressBar.Value = i + 1;
			}

			OverallStatusText.Text = "Đã hoàn thành in tất cả tệp!";
			MessageBox.Show(this, "Hoàn tất tiến trình in hàng loạt!", "In ấn hàng loạt", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		private async Task StartRotateFlowAsync(CancellationToken token)
		{
			int rotateDelta = int.Parse((RotateAngleComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "90");

			for (int i = 0; i < Files.Count; i++)
			{
				token.ThrowIfCancellationRequested();
				var fileItem = Files[i];
				fileItem.Status = "Đang xoay...";
				OverallStatusText.Text = $"Đang xoay tệp {i + 1}/{Files.Count}: {fileItem.FileName}...";

				string outPath = Path.Combine(_selectedFolder, $"rotated_{fileItem.FileName}");
				bool success = false;

				await Task.Run(() =>
				{
					try
					{
						if (RotateAllRadio.IsChecked == true)
						{
							// Rotate all pages by calling Rust core for each page
							string tempIn = fileItem.FilePath;
							string tempOut = outPath;
							for (int page = 1; page <= fileItem.PageCount; page++)
							{
								token.ThrowIfCancellationRequested();
								tempOut = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
								bool ok = PdfInterop.PdfCore.rotate_pdf_page(tempIn, page, rotateDelta, tempOut);
								if (!ok) return;

								if (tempIn != fileItem.FilePath)
								{
									try { File.Delete(tempIn); } catch { }
								}
								tempIn = tempOut;
							}
							if (File.Exists(outPath)) File.Delete(outPath);
							File.Move(tempOut, outPath);
							success = true;
						}
						else if (RotateOddRadio.IsChecked == true || RotateEvenRadio.IsChecked == true)
						{
							bool isOdd = RotateOddRadio.IsChecked == true;
							string tempIn = fileItem.FilePath;
							string tempOut = outPath;
							for (int page = 1; page <= fileItem.PageCount; page++)
							{
								token.ThrowIfCancellationRequested();
								if ((page % 2 == 1 && isOdd) || (page % 2 == 0 && !isOdd))
								{
									tempOut = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
									bool ok = PdfInterop.PdfCore.rotate_pdf_page(tempIn, page, rotateDelta, tempOut);
									if (!ok) return;

									if (tempIn != fileItem.FilePath)
									{
										try { File.Delete(tempIn); } catch { }
									}
									tempIn = tempOut;
								}
							}
							if (tempIn != fileItem.FilePath)
							{
								if (File.Exists(outPath)) File.Delete(outPath);
								File.Move(tempIn, outPath);
								success = true;
							}
						}
						else if (RotateCustomRadio.IsChecked == true)
						{
							if (TryParsePageRange(RotatePageRangeTextBox.Text, fileItem.PageCount, out int start, out int end))
							{
								string tempIn = fileItem.FilePath;
								for (int page = start; page <= end; page++)
								{
									token.ThrowIfCancellationRequested();
									string tempOut = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
									bool ok = PdfInterop.PdfCore.rotate_pdf_page(tempIn, page, rotateDelta, tempOut);
									if (!ok) return;

									if (tempIn != fileItem.FilePath)
									{
										try { File.Delete(tempIn); } catch { }
									}
									tempIn = tempOut;
								}
								if (tempIn != fileItem.FilePath)
								{
									if (File.Exists(outPath)) File.Delete(outPath);
									File.Move(tempIn, outPath);
									success = true;
								}
							}
						}
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

			OverallStatusText.Text = "Đã hoàn thành xoay tất cả tệp!";
			MessageBox.Show(this, "Hoàn tất tiến trình xoay trang hàng loạt!", "Xoay trang", MessageBoxButton.OK, MessageBoxImage.Information);
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

		private async Task StartMergeFlowAsync(CancellationToken token)
		{
			OverallStatusText.Text = "Đang chuẩn bị gộp các tệp PDF...";
			string mergeName = MergeFileNameTextBox.Text.Trim();
			if (string.IsNullOrEmpty(mergeName)) mergeName = "MergedDocument.pdf";
			if (!mergeName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) mergeName += ".pdf";

			string outPath = Path.Combine(_selectedFolder, mergeName);
			bool success = false;

			string semicolonPaths = string.Join(";", Files.Select(f => f.FilePath));

			await Task.Run(() =>
			{
				try
				{
					success = PdfInterop.PdfCore.merge_pdfs(semicolonPaths, outPath);
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch { }
			});

			if (success)
			{
				foreach (var file in Files)
				{
					file.Status = "Thành công";
				}
				OverallProgressBar.Value = Files.Count;
				OverallStatusText.Text = "Đã gộp file thành công!";
				MessageBox.Show(this, $"Gộp file thành công! Lưu tại: {outPath}", "Gộp file PDF", MessageBoxButton.OK, MessageBoxImage.Information);
			}
			else
			{
				foreach (var file in Files)
				{
					file.Status = "Lỗi xử lý";
				}
				MessageBox.Show(this, "Không thể gộp các file PDF đã chọn.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
			}
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

			PrinterComboBox.IsEnabled = enabled;
			CopiesTextBox.IsEnabled = enabled;
			AllPagesRadio.IsEnabled = enabled;
			CustomPagesRadio.IsEnabled = enabled;
			if (enabled && CustomPagesRadio.IsChecked == true) PageRangeTextBox.IsEnabled = true;
			else PageRangeTextBox.IsEnabled = false;
			FitMarginsRadio.IsEnabled = enabled;
			ActualSizeRadio.IsEnabled = enabled;
			AutoCenterCheckBox.IsEnabled = enabled;
			PrintEngineComboBox.IsEnabled = enabled;
			OptimizeCadCheckBox.IsEnabled = enabled;
			SeparateJobsCheckBox.IsEnabled = enabled;

			RotateAngleComboBox.IsEnabled = enabled;
			RotateAllRadio.IsEnabled = enabled;
			RotateOddRadio.IsEnabled = enabled;
			RotateEvenRadio.IsEnabled = enabled;
			RotateCustomRadio.IsEnabled = enabled;
			if (enabled && RotateCustomRadio.IsChecked == true) RotatePageRangeTextBox.IsEnabled = true;
			else RotatePageRangeTextBox.IsEnabled = false;
			RotateBrowseBtn.IsEnabled = enabled;

			CompressQualitySlider.IsEnabled = enabled;
			CompressBrowseBtn.IsEnabled = enabled;

			ExtractPageRangeTextBox.IsEnabled = enabled;
			ExtractBrowseBtn.IsEnabled = enabled;

			MergeFileNameTextBox.IsEnabled = enabled;
			MergeBrowseBtn.IsEnabled = enabled;
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
