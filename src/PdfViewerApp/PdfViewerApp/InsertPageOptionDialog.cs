using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PdfViewerApp;

public class InsertPageOptionDialog : Window
{
	public bool InsertBefore { get; private set; }

	public bool IsConfirmed { get; private set; }

	public InsertPageOptionDialog(int currentPage)
	{
		base.Title = "Chèn Trang Trống - PDF HPhat";
		base.Width = 360.0;
		base.Height = 180.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		base.ResizeMode = ResizeMode.NoResize;
		base.ShowInTaskbar = false;
		base.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
		Grid grid = new Grid
		{
			RowDefinitions = 
			{
				new RowDefinition
				{
					Height = new GridLength(1.0, GridUnitType.Star)
				},
				new RowDefinition
				{
					Height = GridLength.Auto
				}
			}
		};
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(20.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock element = new TextBlock
		{
			Text = $"Bạn muốn chèn trang trống ở đâu so với trang {currentPage}?",
			FontWeight = FontWeights.SemiBold,
			FontSize = 14.0,
			Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A")),
			Margin = new Thickness(0.0, 0.0, 0.0, 15.0),
			TextWrapping = TextWrapping.Wrap
		};
		stackPanel.Children.Add(element);
		RadioButton rbBefore = new RadioButton
		{
			Content = $"Trước trang hiện tại (Trang {currentPage})",
			IsChecked = true,
			FontSize = 13.0,
			Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155")),
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		RadioButton element2 = new RadioButton
		{
			Content = $"Sau trang hiện tại (Trang {currentPage})",
			FontSize = 13.0,
			Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"))
		};
		stackPanel.Children.Add(rbBefore);
		stackPanel.Children.Add(element2);
		Grid.SetRow(stackPanel, 0);
		grid.Children.Add(stackPanel);
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 0.0, 20.0, 15.0)
		};
		Button button = new Button
		{
			Content = "Chèn",
			Width = 80.0,
			Height = 28.0,
			IsDefault = true,
			Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F766E")),
			Foreground = Brushes.White,
			BorderThickness = new Thickness(0.0),
			Margin = new Thickness(0.0, 0.0, 10.0, 0.0)
		};
		button.Style = new Style(typeof(Button))
		{
			Setters = { (SetterBase)new Setter(Control.TemplateProperty, CreateButtonTemplate("#0F766E", "#115E59")) }
		};
		Button button2 = new Button
		{
			Content = "Hủy",
			Width = 80.0,
			Height = 28.0,
			IsCancel = true,
			Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
			Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155")),
			BorderThickness = new Thickness(0.0)
		};
		button2.Style = new Style(typeof(Button))
		{
			Setters = { (SetterBase)new Setter(Control.TemplateProperty, CreateButtonTemplate("#E2E8F0", "#CBD5E1")) }
		};
		button.Click += delegate
		{
			InsertBefore = rbBefore.IsChecked == true;
			IsConfirmed = true;
			Close();
		};
		button2.Click += delegate
		{
			Close();
		};
		stackPanel2.Children.Add(button);
		stackPanel2.Children.Add(button2);
		Grid.SetRow(stackPanel2, 1);
		grid.Children.Add(stackPanel2);
		base.Content = grid;
	}

	private ControlTemplate CreateButtonTemplate(string normalHex, string hoverHex)
	{
		ControlTemplate controlTemplate = new ControlTemplate(typeof(Button));
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString(normalHex)));
		frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4.0));
		frameworkElementFactory.SetValue(Border.BorderThicknessProperty, new Thickness(0.0));
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory2.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		frameworkElementFactory2.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		controlTemplate.VisualTree = frameworkElementFactory;
		Trigger trigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		trigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString(hoverHex)), ""));
		controlTemplate.Triggers.Add(trigger);
		return controlTemplate;
	}
}
