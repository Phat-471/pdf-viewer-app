using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using Microsoft.Win32;

namespace PdfViewerApp;

public partial class MergeDialog : Window, IComponentConnector
{
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate void MergeProgressCallback(uint current, uint total);

	private readonly HashSet<string> _fileSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private readonly bool _autoStartMerge;

	private bool _mergeInProgress;

	private MergeProgressCallback? _progressCallback;

	public ObservableCollection<PdfFileItem> Files { get; } = new ObservableCollection<PdfFileItem>();

	public string? MergedFilePath { get; private set; }

	[DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern bool merge_pdfs_with_progress([MarshalAs(UnmanagedType.LPUTF8Str)] string pathsSemicolon, [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath, MergeProgressCallback? progressCallback);

	public MergeDialog()
		: this(null, autoStartMerge: false, sortInitialFilesByName: false)
	{
	}

	public MergeDialog(IEnumerable<string>? initialFiles)
		: this(initialFiles, autoStartMerge: false, sortInitialFilesByName: false)
	{
	}

	public MergeDialog(IEnumerable<string>? initialFiles, bool autoStartMerge)
		: this(initialFiles, autoStartMerge, sortInitialFilesByName: false)
	{
	}

	public MergeDialog(IEnumerable<string>? initialFiles, bool autoStartMerge, bool sortInitialFilesByName)
	{
		InitializeComponent();
		_autoStartMerge = autoStartMerge;
		FileListBox.ItemsSource = Files;
		base.Loaded += MergeDialog_Loaded;
		AutoSortCheckBox.IsChecked = sortInitialFilesByName || autoStartMerge || AutoSortCheckBox.IsChecked == true;
		AddFiles(initialFiles, sortInitialFilesByName || autoStartMerge);
		UpdateStatus();
	}

	private void AddFile_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Filter = "PDF documents (*.pdf)|*.pdf",
			Multiselect = true,
			Title = "Chọn các file PDF cần gộp"
		};
		if (openFileDialog.ShowDialog() == true)
		{
			AddFiles(openFileDialog.FileNames, AutoSortCheckBox.IsChecked == true);
		}
	}

	private void Window_Drop(object sender, DragEventArgs e)
	{
		if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] filePaths)
		{
			AddFiles(filePaths, sortByName: true);
		}
	}

	private void MoveUp_Click(object sender, RoutedEventArgs e)
	{
		int selectedIndex = FileListBox.SelectedIndex;
		if (selectedIndex > 0)
		{
			AutoSortCheckBox.IsChecked = false;
			PdfFileItem item = Files[selectedIndex];
			Files.RemoveAt(selectedIndex);
			Files.Insert(selectedIndex - 1, item);
			FileListBox.SelectedIndex = selectedIndex - 1;
		}
	}

	private void MoveDown_Click(object sender, RoutedEventArgs e)
	{
		int selectedIndex = FileListBox.SelectedIndex;
		if (selectedIndex >= 0 && selectedIndex < Files.Count - 1)
		{
			AutoSortCheckBox.IsChecked = false;
			PdfFileItem item = Files[selectedIndex];
			Files.RemoveAt(selectedIndex);
			Files.Insert(selectedIndex + 1, item);
			FileListBox.SelectedIndex = selectedIndex + 1;
		}
	}

	private void Remove_Click(object sender, RoutedEventArgs e)
	{
		if (FileListBox.SelectedIndex >= 0)
		{
			_fileSet.Remove(Files[FileListBox.SelectedIndex].FullPath);
			Files.RemoveAt(FileListBox.SelectedIndex);
			UpdateStatus();
		}
	}

	private void Clear_Click(object sender, RoutedEventArgs e)
	{
		Files.Clear();
		_fileSet.Clear();
		UpdateStatus();
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		if (!_mergeInProgress)
		{
			base.DialogResult = false;
			Close();
		}
	}

	private async void Merge_Click(object sender, RoutedEventArgs e)
	{
		await StartMergeAsync();
	}

	private async void MergeDialog_Loaded(object sender, RoutedEventArgs e)
	{
		if (_autoStartMerge && Files.Count >= 2)
		{
			await Task.Yield();
			await StartMergeAsync();
		}
	}

	private async Task StartMergeAsync()
	{
		if (_mergeInProgress)
		{
			return;
		}
		if (Files.Count < 2)
		{
			MessageBox.Show("Vui lòng chọn ít nhất 2 file PDF để gộp.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (AutoSortCheckBox.IsChecked == true)
		{
			SortFilesByName();
		}
		List<PdfFileItem> mergeFiles = Files.Where((PdfFileItem file) => File.Exists(file.FullPath)).ToList();
		if (mergeFiles.Count != Files.Count)
		{
			MessageBox.Show("Một số file không còn tồn tại. Vui lòng kiểm tra lại danh sách.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		_mergeInProgress = true;
		SetMergeUiState(isMerging: true);
		try
		{
			string targetPath = CreateAutoOutputPath(mergeFiles.First().FullPath);
			string pathsJoined = string.Join(";", mergeFiles.Select((PdfFileItem file) => file.FullPath));
			long value = mergeFiles.Sum((PdfFileItem file) => file.SizeBytes);
			Stopwatch mergeSw = Stopwatch.StartNew();
			PdfPerfLogger.Log($"Merge start: files={mergeFiles.Count}, inputBytes={value:N0}, output={targetPath}");
			for (int num = 0; num < mergeFiles.Count; num++)
			{
				PdfPerfLogger.Log($"Merge input {num + 1}/{mergeFiles.Count}: {mergeFiles[num].FullPath} ({mergeFiles[num].SizeBytes:N0} bytes)");
			}
			_progressCallback = delegate(uint current, uint total)
			{
				uint safeCurrent = Math.Min(current, total);
				string fileName = ((safeCurrent == 0 || safeCurrent > mergeFiles.Count) ? "start" : mergeFiles[(int)(safeCurrent - 1)].FileName);
				PdfPerfLogger.Log($"Merge progress: {safeCurrent}/{total} {fileName}");
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					MergeProgress.Visibility = Visibility.Visible;
					MergeProgress.Maximum = Math.Max(1u, total);
					MergeProgress.Value = Math.Min(current, total);
					ProgressText.Text = $"Đã xử lý {safeCurrent}/{total} file - {fileName} - {mergeSw.Elapsed:mm\\:ss}";
					StatusText.Text = "Đang gộp PDF, không tắt ứng dụng trong lúc này.";
				});
			};
			bool num2 = await Task.Run(() => merge_pdfs_with_progress(pathsJoined, targetPath, _progressCallback));
			mergeSw.Stop();
			if (num2)
			{
				long value2 = (File.Exists(targetPath) ? new FileInfo(targetPath).Length : 0);
				PdfPerfLogger.Log($"Merge success: elapsed={mergeSw.ElapsedMilliseconds} ms, outputBytes={value2:N0}");
				MergedFilePath = targetPath;
				base.DialogResult = true;
				Close();
			}
			else
			{
				PdfPerfLogger.Log($"Merge failed: elapsed={mergeSw.ElapsedMilliseconds} ms");
				MessageBox.Show("Gộp file thất bại. Có thể một file bị lỗi hoặc đang bị ứng dụng khác khóa.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}
		catch (Exception ex)
		{
			PdfPerfLogger.Log($"Merge exception: {ex}");
			MessageBox.Show("Đã xảy ra lỗi: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
		finally
		{
			_progressCallback = null;
			_mergeInProgress = false;
			if (base.IsVisible)
			{
				SetMergeUiState(isMerging: false);
				UpdateStatus();
			}
		}
	}

	private void SetMergeUiState(bool isMerging)
	{
		FileListBox.IsEnabled = !isMerging;
		AddFileButton.IsEnabled = !isMerging;
		MoveUpButton.IsEnabled = !isMerging;
		MoveDownButton.IsEnabled = !isMerging;
		RemoveButton.IsEnabled = !isMerging;
		ClearButton.IsEnabled = !isMerging;
		MergeButton.IsEnabled = !isMerging;
		CancelButton.IsEnabled = !isMerging;
		AutoSortCheckBox.IsEnabled = !isMerging;
		MergeProgress.Visibility = ((!isMerging) ? Visibility.Collapsed : Visibility.Visible);
		if (!isMerging)
		{
			MergeProgress.Value = 0.0;
			ProgressText.Text = string.Empty;
		}
	}

	private void UpdateStatus()
	{
		long bytes = Files.Sum((PdfFileItem file) => file.SizeBytes);
		if (StatusText != null)
		{
			StatusText.Text = $"Đã chọn {Files.Count} file - tổng {FormatBytes(bytes)}";
		}
		if (DetailsText != null)
		{
			DetailsText.Text = ((Files.Count == 0) ? string.Empty : $"{Files.Count} file / {FormatBytes(bytes)}");
		}
	}

	private void AddFiles(IEnumerable<string>? filePaths, bool sortByName = false)
	{
		if (filePaths == null)
		{
			return;
		}
		IEnumerable<string> enumerable = filePaths;
		if (sortByName)
		{
			enumerable = enumerable.OrderBy((string path) => path, NaturalFilePathComparer.Instance).ToList();
		}
		bool flag = false;
		foreach (string item in enumerable)
		{
			if (TryNormalizePdfPath(item, out string normalized) && _fileSet.Add(normalized))
			{
				Files.Add(PdfFileItem.FromPath(normalized));
				flag = true;
			}
		}
		if (flag)
		{
			if (sortByName || AutoSortCheckBox.IsChecked == true)
			{
				SortFilesByName();
			}
			UpdateStatus();
		}
	}

	private void AutoSortCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (AutoSortCheckBox != null && FileListBox != null && AutoSortCheckBox.IsChecked == true && !_mergeInProgress)
		{
			SortFilesByName();
			UpdateStatus();
		}
	}

	private void SortFilesByName()
	{
		if (Files.Count < 2)
		{
			return;
		}
		List<PdfFileItem> list = Files.OrderBy((PdfFileItem file) => file.FullPath, NaturalFilePathComparer.Instance).ToList();
		Files.Clear();
		foreach (PdfFileItem item in list)
		{
			Files.Add(item);
		}
	}

	public static string CreateAutoOutputPath(string firstSourcePath)
	{
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PdfPro", "Merged");
		Directory.CreateDirectory(text);
		string text2 = "MergedDocument";
		if (!string.IsNullOrWhiteSpace(firstSourcePath))
		{
			text2 = new string((from ch in Path.GetFileNameWithoutExtension(firstSourcePath)
				where !Path.GetInvalidFileNameChars().Contains(ch)
				select ch).ToArray()).Trim();
			if (string.IsNullOrWhiteSpace(text2))
			{
				text2 = "MergedDocument";
			}
		}
		string text3 = DateTime.Now.ToString("yyyyMMdd_HHmmss");
		string text4 = Path.Combine(text, text2 + "_merged_" + text3 + ".pdf");
		int num = 1;
		while (File.Exists(text4))
		{
			text4 = Path.Combine(text, $"{text2}_merged_{text3}_{num}.pdf");
			num++;
		}
		return text4;
	}

	private static bool TryNormalizePdfPath(string path, out string normalized)
	{
		normalized = string.Empty;
		if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
		{
			return false;
		}
		if (!Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		try
		{
			normalized = Path.GetFullPath(path);
			return true;
		}
		catch
		{
			return false;
		}
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
