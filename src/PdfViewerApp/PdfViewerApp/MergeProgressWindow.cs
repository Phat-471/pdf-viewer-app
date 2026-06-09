using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;

namespace PdfViewerApp;

public partial class MergeProgressWindow : Window, IComponentConnector
{
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void MergeProgressDelegate(uint current, uint total);

	private readonly string _pathsSemicolon;

	private readonly string _outputPath;

	private bool _isCancelled;

	public bool MergeResult { get; private set; }

	[DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern bool merge_pdfs_with_progress([MarshalAs(UnmanagedType.LPUTF8Str)] string pathsSemicolon, [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath, MergeProgressDelegate progressCb);

	public MergeProgressWindow(string pathsSemicolon, string outputPath)
	{
		InitializeComponent();
		_pathsSemicolon = pathsSemicolon;
		_outputPath = outputPath;
		base.Loaded += MergeProgressWindow_Loaded;
	}

	private async void MergeProgressWindow_Loaded(object sender, RoutedEventArgs e)
	{
		MergeResult = await Task.Run(delegate
		{
			try
			{
				return merge_pdfs_with_progress(_pathsSemicolon, _outputPath, delegate(uint current, uint total)
				{
					base.Dispatcher.BeginInvoke((Action)delegate
					{
						if (total != 0)
						{
							double value = (double)current / (double)total * 100.0;
							MergeProgressBar.Value = value;
							ProgressStatusText.Text = $"Đang ghép tệp: {current} / {total}...";
						}
					});
				});
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					MessageBox.Show("Lỗi ghép tệp: " + ex3.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
				});
				return false;
			}
		});
		if (MergeResult && !_isCancelled)
		{
			ProgressStatusText.Text = "Ghép tệp hoàn tất!";
			MergeProgressBar.Value = 100.0;
			await Task.Delay(500);
			base.DialogResult = true;
		}
		else
		{
			base.DialogResult = false;
		}
		Close();
	}

	private void CancelButton_Click(object sender, RoutedEventArgs e)
	{
		_isCancelled = true;
		base.DialogResult = false;
		Close();
	}
}
