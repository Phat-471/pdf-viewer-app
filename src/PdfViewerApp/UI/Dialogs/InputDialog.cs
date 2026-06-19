using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PdfViewerApp;

public static class InputDialog
{
	public static string? Show(string title, string instruction, string defaultValue = "")
	{
		Window? ownerWindow = null;
		try
		{
			foreach (Window window in Application.Current.Windows)
			{
				if (window.IsActive)
				{
					ownerWindow = window;
					break;
				}
			}
		}
		catch { }

		if (ownerWindow == null)
		{
			try
			{
				ownerWindow = Application.Current.MainWindow;
			}
			catch { }
		}

		Window dialog = new Window
		{
			Title = title,
			Width = 420.0,
			Height = 190.0,
			ResizeMode = ResizeMode.NoResize,
			Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
			WindowStyle = WindowStyle.ToolWindow
		};

		if (ownerWindow != null && ownerWindow != dialog)
		{
			try
			{
				dialog.Owner = ownerWindow;
				dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			}
			catch
			{
				dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
			}
		}
		else
		{
			dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
		}
		Grid grid = new Grid
		{
			Margin = new Thickness(16.0)
		};
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		TextBlock element = new TextBlock
		{
			Text = instruction,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0),
			Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
			TextWrapping = TextWrapping.Wrap
		};
		Grid.SetRow(element, 0);
		grid.Children.Add(element);
		TextBox textBox = new TextBox
		{
			Text = defaultValue,
			Padding = new Thickness(6.0),
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0),
			Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
			Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105))
		};
		Grid.SetRow(textBox, 1);
		grid.Children.Add(textBox);
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		Grid.SetRow(stackPanel, 2);
		grid.Children.Add(stackPanel);
		Button button = new Button
		{
			Content = "Xác nhận",
			Width = 88.0,
			Height = 28.0,
			IsDefault = true,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			Background = new SolidColorBrush(Color.FromRgb(15, 118, 110)),
			Foreground = Brushes.White,
			BorderThickness = new Thickness(0.0)
		};
		button.Click += delegate
		{
			dialog.DialogResult = true;
			dialog.Close();
		};
		stackPanel.Children.Add(button);
		Button button2 = new Button
		{
			Content = "Hủy",
			Width = 88.0,
			Height = 28.0,
			IsCancel = true,
			Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
			Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
			BorderThickness = new Thickness(1.5)
		};
		button2.Click += delegate
		{
			dialog.DialogResult = false;
			dialog.Close();
		};
		stackPanel.Children.Add(button2);
		dialog.Content = grid;
		textBox.Focus();
		textBox.SelectAll();
		if (dialog.ShowDialog() == true)
		{
			return textBox.Text.Trim();
		}
		return null;
	}
}
