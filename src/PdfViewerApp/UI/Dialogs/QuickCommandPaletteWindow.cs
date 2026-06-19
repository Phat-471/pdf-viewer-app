using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PdfViewerApp;

internal sealed class QuickCommandItem
{
	public QuickCommandItem(string title, string description, string keywords, Action execute, Func<bool>? canExecute = null)
	{
		Title = title;
		Description = description;
		Keywords = keywords;
		Execute = execute;
		CanExecute = canExecute;
	}

	public string Title { get; }

	public string Description { get; }

	public string Keywords { get; }

	public Action Execute { get; }

	public Func<bool>? CanExecute { get; }

	public bool IsEnabled => CanExecute?.Invoke() ?? true;

	public bool Matches(string query)
	{
		if (string.IsNullOrWhiteSpace(query))
		{
			return true;
		}

		string haystack = (Title + " " + Description + " " + Keywords).ToLowerInvariant();
		string[] terms = query.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
		return terms.All(term => haystack.Contains(term.ToLowerInvariant()));
	}
}

internal sealed class QuickCommandPaletteWindow : Window
{
	private readonly List<QuickCommandItem> _commands;
	private readonly TextBox _searchBox;
	private readonly ListBox _resultsList;
	private readonly TextBlock _emptyText;

	public QuickCommandPaletteWindow(IEnumerable<QuickCommandItem> commands)
	{
		_commands = commands.ToList();
		Title = "Command Palette";
		Width = 720.0;
		Height = 520.0;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowStyle = WindowStyle.None;
		AllowsTransparency = true;
		ResizeMode = ResizeMode.NoResize;
		Background = Brushes.Transparent;
		ShowInTaskbar = false;
		Topmost = true;

		Border shell = new Border
		{
			CornerRadius = new CornerRadius(22.0),
			Background = CreateShellBackground(),
			BorderBrush = new SolidColorBrush(Color.FromRgb(20, 184, 166)),
			BorderThickness = new Thickness(1.0),
			Padding = new Thickness(22.0),
			Effect = new System.Windows.Media.Effects.DropShadowEffect
			{
				Color = Colors.Black,
				BlurRadius = 36.0,
				ShadowDepth = 10.0,
				Opacity = 0.38
			}
		};

		Grid layout = new Grid();
		layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
		shell.Child = layout;

		TextBlock title = new TextBlock
		{
			Text = "Command Palette",
			Foreground = Brushes.White,
			FontSize = 24.0,
			FontWeight = FontWeights.SemiBold,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		layout.Children.Add(title);

		TextBlock hint = new TextBlock
		{
			Text = "Type to search actions. Enter runs, Esc closes, Ctrl+K opens.",
			Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
			FontSize = 13.0,
			Margin = new Thickness(0.0, 34.0, 0.0, 16.0)
		};
		layout.Children.Add(hint);

		_searchBox = new TextBox
		{
			FontSize = 18.0,
			Padding = new Thickness(16.0, 12.0, 16.0, 12.0),
			Foreground = Brushes.White,
			Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(45, 212, 191)),
			BorderThickness = new Thickness(1.0),
			CaretBrush = Brushes.White
		};
		_searchBox.TextChanged += delegate { RefreshResults(); };
		_searchBox.PreviewKeyDown += SearchBox_PreviewKeyDown;
		Grid.SetRow(_searchBox, 1);
		layout.Children.Add(_searchBox);

		Grid resultsHost = new Grid
		{
			Margin = new Thickness(0.0, 16.0, 0.0, 0.0)
		};
		Grid.SetRow(resultsHost, 2);
		layout.Children.Add(resultsHost);

		_resultsList = new ListBox
		{
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			Foreground = Brushes.White
		};
		ScrollViewer.SetVerticalScrollBarVisibility(_resultsList, ScrollBarVisibility.Auto);
		_resultsList.MouseDoubleClick += delegate { ExecuteSelected(); };
		_resultsList.PreviewKeyDown += ResultsList_PreviewKeyDown;
		resultsHost.Children.Add(_resultsList);

		_emptyText = new TextBlock
		{
			Text = "No matching command",
			Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Visibility = Visibility.Collapsed
		};
		resultsHost.Children.Add(_emptyText);

		Content = shell;
		Loaded += delegate
		{
			RefreshResults();
			_searchBox.Focus();
		};
	}

	private static Brush CreateShellBackground()
	{
		LinearGradientBrush brush = new LinearGradientBrush
		{
			StartPoint = new Point(0.0, 0.0),
			EndPoint = new Point(1.0, 1.0)
		};
		brush.GradientStops.Add(new GradientStop(Color.FromRgb(2, 6, 23), 0.0));
		brush.GradientStops.Add(new GradientStop(Color.FromRgb(15, 23, 42), 0.58));
		brush.GradientStops.Add(new GradientStop(Color.FromRgb(12, 74, 110), 1.0));
		return brush;
	}

	private void RefreshResults()
	{
		string query = _searchBox.Text;
		List<QuickCommandItem> matches = _commands
			.Where(command => command.Matches(query))
			.Take(30)
			.ToList();

		_resultsList.Items.Clear();
		foreach (QuickCommandItem command in matches)
		{
			_resultsList.Items.Add(CreateResultItem(command));
		}

		_emptyText.Visibility = matches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
		if (_resultsList.Items.Count > 0)
		{
			_resultsList.SelectedIndex = 0;
		}
	}

	private static ListBoxItem CreateResultItem(QuickCommandItem command)
	{
		StackPanel panel = new StackPanel
		{
			Margin = new Thickness(4.0, 2.0, 4.0, 2.0)
		};

		TextBlock title = new TextBlock
		{
			Text = command.Title,
			Foreground = command.IsEnabled ? Brushes.White : new SolidColorBrush(Color.FromRgb(100, 116, 139)),
			FontSize = 15.0,
			FontWeight = FontWeights.SemiBold
		};
		panel.Children.Add(title);

		TextBlock description = new TextBlock
		{
			Text = command.Description,
			Foreground = command.IsEnabled ? new SolidColorBrush(Color.FromRgb(148, 163, 184)) : new SolidColorBrush(Color.FromRgb(71, 85, 105)),
			FontSize = 12.0,
			Margin = new Thickness(0.0, 3.0, 0.0, 0.0)
		};
		panel.Children.Add(description);

		return new ListBoxItem
		{
			Tag = command,
			Content = panel,
			IsEnabled = command.IsEnabled,
			Padding = new Thickness(12.0, 10.0, 12.0, 10.0),
			Margin = new Thickness(0.0, 0.0, 0.0, 7.0),
			Background = new SolidColorBrush(Color.FromArgb(112, 15, 23, 42)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
			BorderThickness = new Thickness(1.0)
		};
	}

	private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape)
		{
			Close();
			e.Handled = true;
			return;
		}

		if (e.Key == Key.Enter)
		{
			ExecuteSelected();
			e.Handled = true;
			return;
		}

		if (e.Key == Key.Down && _resultsList.Items.Count > 0)
		{
			_resultsList.Focus();
			_resultsList.SelectedIndex = Math.Min(_resultsList.SelectedIndex + 1, _resultsList.Items.Count - 1);
			e.Handled = true;
		}
	}

	private void ResultsList_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape)
		{
			Close();
			e.Handled = true;
		}
		else if (e.Key == Key.Enter)
		{
			ExecuteSelected();
			e.Handled = true;
		}
		else if (e.Key == Key.Up && _resultsList.SelectedIndex <= 0)
		{
			_searchBox.Focus();
			e.Handled = true;
		}
	}

	private void ExecuteSelected()
	{
		if (_resultsList.SelectedItem is not ListBoxItem { Tag: QuickCommandItem command } || !command.IsEnabled)
		{
			return;
		}

		Close();
		command.Execute();
	}
}
