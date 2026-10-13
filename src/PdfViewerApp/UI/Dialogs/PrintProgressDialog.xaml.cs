using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Markup;

namespace PdfViewerApp;

public partial class PrintProgressDialog : Window, IComponentConnector
{
	private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

	private bool _allowClose;

	public CancellationToken CancellationToken => _cancellationTokenSource.Token;

	public PrintProgressDialog()
	{
		InitializeComponent();
	}

	public void UpdateProgress(PrintProgressInfo info)
	{
		if (!base.Dispatcher.CheckAccess())
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				UpdateProgress(info);
			});
			return;
		}
		if (_allowClose || _cancellationTokenSource.IsCancellationRequested) return;
		PrintProgressBar.IsIndeterminate = info.IsIndeterminate;
		if (!info.IsIndeterminate)
		{
			PrintProgressBar.Maximum = Math.Max(1, info.TotalPages);
			PrintProgressBar.Value = Math.Clamp(info.CurrentPage, 0, Math.Max(1, info.TotalPages));
		}
		StatusText.Text = info.Message;
		DetailText.Text = ((info.TotalPages > 0) ? $"Tiến trình: {Math.Clamp(info.CurrentPage, 0, info.TotalPages)} / {info.TotalPages}" : string.Empty);
	}

	public void MarkCompleted(string message)
	{
		if (!base.Dispatcher.CheckAccess())
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				MarkCompleted(message);
			});
			return;
		}
		PrintProgressBar.IsIndeterminate = false;
		PrintProgressBar.Value = PrintProgressBar.Maximum;
		StatusText.Text = message;
		CancelButton.Visibility = Visibility.Collapsed;
		CloseButton.Visibility = Visibility.Visible;
		_allowClose = true;

		// Auto close after 1 second
		System.Threading.Tasks.Task.Delay(1000).ContinueWith(_ =>
		{
			base.Dispatcher.Invoke(() =>
			{
				try
				{
					if (this.IsLoaded)
					{
						this.Close();
					}
				}
				catch {}
			});
		});
	}

	public void MarkFailed(string message)
	{
		if (!base.Dispatcher.CheckAccess())
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				MarkFailed(message);
			});
			return;
		}
		PrintProgressBar.IsIndeterminate = false;
		StatusText.Text = message;
		CancelButton.Visibility = Visibility.Collapsed;
		CloseButton.Visibility = Visibility.Visible;
		_allowClose = true;
	}

	public void CloseAfterSuccess()
	{
		if (!base.Dispatcher.CheckAccess())
		{
			base.Dispatcher.BeginInvoke(new Action(CloseAfterSuccess));
			return;
		}
		_allowClose = true;
		Close();
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		if (!_cancellationTokenSource.IsCancellationRequested)
		{
			_cancellationTokenSource.Cancel();
			CancelButton.IsEnabled = false;
			StatusText.Text = "Đang huỷ lệnh in…";
			DetailText.Text = "Đang chờ driver kết thúc bước hiện tại. Trang đã gửi có thể vẫn được in.";
		}
	}

	private void Close_Click(object sender, RoutedEventArgs e)
	{
		_allowClose = true;
		Close();
	}

	private void Window_Closing(object? sender, CancelEventArgs e)
	{
		if (!_allowClose && !_cancellationTokenSource.IsCancellationRequested)
		{
			e.Cancel = true;
			Cancel_Click(this, new RoutedEventArgs());
		}
	}
}
