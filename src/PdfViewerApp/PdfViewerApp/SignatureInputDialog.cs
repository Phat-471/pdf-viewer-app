using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace PdfViewerApp;

public class SignatureInputDialog : Window
{
	private InkCanvas _inkCanvas;
	private ComboBox _colorCombo;
	private Slider _thicknessSlider;

	public List<List<Point>> ResultStrokes { get; private set; } = new List<List<Point>>();
	public double ResultWidth { get; private set; }
	public double ResultHeight { get; private set; }
	public Color ResultColor { get; private set; } = Colors.Blue;

	public SignatureInputDialog()
	{
		Title = "Ký Tay Điện Tử - PDF Pro";
		Width = 600;
		Height = 450;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A")); // Slate 900
		ResizeMode = ResizeMode.NoResize;

		Grid rootGrid = new Grid();
		rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

		// 1. Toolbar (Top)
		StackPanel toolbar = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(15),
			VerticalAlignment = VerticalAlignment.Center
		};

		TextBlock titleLbl = new TextBlock
		{
			Text = "Ký tay của bạn vào khung phía dưới:",
			Foreground = Brushes.White,
			FontSize = 14,
			FontWeight = FontWeights.SemiBold,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0, 0, 20, 0)
		};
		toolbar.Children.Add(titleLbl);

		// Color selector
		_colorCombo = new ComboBox { Width = 100, Height = 28, Margin = new Thickness(0, 0, 15, 0) };
		_colorCombo.Items.Add(new ComboBoxItem { Content = "Mực Xanh", Tag = Colors.Blue, IsSelected = true });
		_colorCombo.Items.Add(new ComboBoxItem { Content = "Mực Đen", Tag = Colors.Black });
		_colorCombo.Items.Add(new ComboBoxItem { Content = "Mực Đỏ", Tag = Colors.Red });
		_colorCombo.SelectionChanged += ColorCombo_SelectionChanged;
		toolbar.Children.Add(_colorCombo);

		// Thickness slider
		TextBlock thicknessLbl = new TextBlock { Text = "Nét vẽ:", Foreground = Brushes.LightGray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) };
		toolbar.Children.Add(thicknessLbl);

		_thicknessSlider = new Slider { Minimum = 1, Maximum = 10, Value = 3, Width = 100, VerticalAlignment = VerticalAlignment.Center };
		_thicknessSlider.ValueChanged += ThicknessSlider_ValueChanged;
		toolbar.Children.Add(_thicknessSlider);

		rootGrid.Children.Add(toolbar);
		Grid.SetRow(toolbar, 0);

		// 2. Drawing Area (Center)
		Border canvasBorder = new Border
		{
			BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155")), // Slate 700
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(6),
			Background = Brushes.White,
			Margin = new Thickness(15, 0, 15, 0)
		};

		_inkCanvas = new InkCanvas
		{
			Background = Brushes.Transparent,
			DefaultDrawingAttributes = new DrawingAttributes
			{
				Color = Colors.Blue,
				Width = 3,
				Height = 3,
				FitToCurve = true
			}
		};
		canvasBorder.Child = _inkCanvas;
		rootGrid.Children.Add(canvasBorder);
		Grid.SetRow(canvasBorder, 1);

		// 3. Actions (Bottom)
		Grid actionsGrid = new Grid { Margin = new Thickness(15) };
		actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

		Button clearBtn = new Button
		{
			Content = "Xóa Chữ Ký",
			Width = 100,
			Height = 32,
			Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155")),
			Foreground = Brushes.White,
			BorderThickness = new Thickness(0)
		};
		clearBtn.Click += ClearBtn_Click;
		actionsGrid.Children.Add(clearBtn);
		Grid.SetColumn(clearBtn, 0);
		clearBtn.HorizontalAlignment = HorizontalAlignment.Left;

		Button cancelBtn = new Button
		{
			Content = "Hủy",
			Width = 80,
			Height = 32,
			Background = Brushes.Transparent,
			Foreground = Brushes.LightGray,
			BorderThickness = new Thickness(0),
			Margin = new Thickness(0, 0, 10, 0)
		};
		cancelBtn.Click += (s, e) => Close();
		actionsGrid.Children.Add(cancelBtn);
		Grid.SetColumn(cancelBtn, 1);

		Button doneBtn = new Button
		{
			Content = "Hoàn Tất",
			Width = 100,
			Height = 32,
			Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F766E")), // Teal 700
			Foreground = Brushes.White,
			BorderThickness = new Thickness(0)
		};
		doneBtn.Click += DoneBtn_Click;
		actionsGrid.Children.Add(doneBtn);
		Grid.SetColumn(doneBtn, 2);

		rootGrid.Children.Add(actionsGrid);
		Grid.SetRow(actionsGrid, 2);

		Content = rootGrid;
	}

	private void ColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_inkCanvas != null && _colorCombo?.SelectedItem is ComboBoxItem item && item.Tag is Color color)
		{
			_inkCanvas.DefaultDrawingAttributes.Color = color;
			ResultColor = color;
		}
	}

	private void ThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (_inkCanvas != null && _thicknessSlider != null)
		{
			_inkCanvas.DefaultDrawingAttributes.Width = _thicknessSlider.Value;
			_inkCanvas.DefaultDrawingAttributes.Height = _thicknessSlider.Value;
		}
	}

	private void ClearBtn_Click(object sender, RoutedEventArgs e)
	{
		_inkCanvas?.Strokes.Clear();
	}

	private void DoneBtn_Click(object sender, RoutedEventArgs e)
	{
		if (_inkCanvas == null || _inkCanvas.Strokes.Count == 0)
		{
			MessageBox.Show("Vui lòng vẽ chữ ký của bạn trước khi nhấn hoàn tất.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
			return;
		}

		// Calculate bounding box of the strokes to normalize them
		double minX = double.MaxValue, minY = double.MaxValue;
		double maxX = double.MinValue, maxY = double.MinValue;

		foreach (var stroke in _inkCanvas.Strokes)
		{
			foreach (var pt in stroke.StylusPoints)
			{
				if (pt.X < minX) minX = pt.X;
				if (pt.Y < minY) minY = pt.Y;
				if (pt.X > maxX) maxX = pt.X;
				if (pt.Y > maxY) maxY = pt.Y;
			}
		}

		double width = maxX - minX;
		double height = maxY - minY;

		if (width <= 0 || height <= 0)
		{
			MessageBox.Show("Chữ ký không hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
			return;
		}

		// Shift all points relative to the top-left (minX, minY) of the bounding box
		List<List<Point>> normalizedStrokes = new List<List<Point>>();
		foreach (var stroke in _inkCanvas.Strokes)
		{
			List<Point> pts = new List<Point>();
			foreach (var pt in stroke.StylusPoints)
			{
				pts.Add(new Point(pt.X - minX, pt.Y - minY));
			}
			normalizedStrokes.Add(pts);
		}

		ResultStrokes = normalizedStrokes;
		ResultWidth = width;
		ResultHeight = height;

		DialogResult = true;
		Close();
	}
}
