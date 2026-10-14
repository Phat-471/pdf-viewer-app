using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PdfViewerApp;

internal sealed class PdfTextEditDialog : Window
{
    private readonly TextBox _input;
    private readonly Image _image;
    private readonly TextBlock _status;
    private readonly Button _previewButton;
    private readonly Button _applyButton;
    private readonly Func<string, Task<PdfTextEditPreview>> _preview;
    private string? _validated;
    private int _generation;
    private bool _closed;
    internal string ReplacementText => _input.Text;

    internal PdfTextEditDialog(PdfNativeTextRegion region, Func<string, Task<PdfTextEditPreview>> preview)
    {
        _preview = preview;
        Title = "Sửa chữ gốc trong PDF";
        Width = 820; Height = 580; MinWidth = 620; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#0F172A"); Foreground = Brush("#F8FAFC"); FontFamily = new FontFamily("Segoe UI");
        var root = new Grid { Margin = new Thickness(24) };
        for (int i = 0; i < 5; i++) root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Content = root;
        Add(root, new TextBlock { Text = "Sửa chữ · Giữ định dạng gốc", FontSize = 22, FontWeight = FontWeights.SemiBold }, 0);
        Add(root, new TextBlock { Text = $"Trang {region.PageNumber}   •   {region.FontName}   •   {region.FontSize:0.###} pt   •   " +
            (region.Embedded ? "Font nhúng trong PDF" : "Font không nhúng — hiển thị phụ thuộc font trên máy"),
            Foreground = Brush("#94A3B8"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 14) }, 1);
        Add(root, new TextBlock { Text = "Nội dung mới", Margin = new Thickness(0, 0, 0, 6) }, 2);
        _input = new TextBox { Text = region.Text, FontSize = 16, Padding = new Thickness(12),
            Background = Brush("#1E293B"), Foreground = Foreground, BorderBrush = Brush("#475569"),
            AcceptsReturn = false, MaxLength = 20000, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        Add(root, _input, 3);
        Add(root, new TextBlock { Text = "Font, cỡ chữ, màu và vị trí được giữ nguyên. Để trống nội dung nếu muốn xóa đối tượng chữ.",
            Foreground = Brush("#94A3B8"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 14) }, 4);
        _image = new Image { Stretch = Stretch.Uniform, Margin = new Thickness(10) };
        Add(root, new Border { Background = Brushes.White, CornerRadius = new CornerRadius(6), Child = _image }, 5);
        var footer = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        _status = new TextBlock { Text = "Đang tạo bản xem trước…", Foreground = Brush("#94A3B8"), TextWrapping = TextWrapping.Wrap,
            MinHeight = 42, Margin = new Thickness(0, 0, 0, 8) };
        footer.Children.Add(_status);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _previewButton = Button("Xem trước", false);
        _applyButton = Button("Áp dụng", true); _applyButton.IsEnabled = false;
        var cancel = Button("Hủy", false); cancel.IsCancel = true;
        actions.Children.Add(_previewButton); actions.Children.Add(_applyButton); actions.Children.Add(cancel);
        footer.Children.Add(actions); Add(root, footer, 6);
        _input.TextChanged += (_, _) => { _generation++; _validated = null; _applyButton.IsEnabled = false; _image.Source = null;
            _status.Text = "Nội dung đã đổi. Nhấn Xem trước để kiểm tra bằng font gốc."; };
        _previewButton.Click += async (_, _) => await UpdatePreviewAsync();
        _applyButton.Click += (_, _) => { if (_validated == _input.Text) DialogResult = true; };
        Loaded += async (_, _) => { _input.Focus(); _input.SelectAll(); await UpdatePreviewAsync(); };
        Closed += (_, _) => { _closed = true; _generation++; };
    }

    private async Task UpdatePreviewAsync()
    {
        int generation = ++_generation;
        string text = _input.Text;
        _validated = null; _applyButton.IsEnabled = false; _previewButton.IsEnabled = false;
        _status.Text = "Đang kiểm tra ký tự và tạo bản xem trước…";
        try
        {
            var result = await _preview(text);
            if (_closed || generation != _generation) return;
            _image.Source = result.Image;
            _status.Foreground = Brush("#94A3B8"); _status.Text = result.Message;
            _validated = text; _applyButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            if (_closed || generation != _generation) return;
            _image.Source = null; _status.Foreground = Brush("#FBBF24"); _status.Text = ex.Message;
        }
        finally { if (!_closed) _previewButton.IsEnabled = true; }
    }

    private static Brush Brush(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
    private static void Add(Grid grid, UIElement element, int row) { Grid.SetRow(element, row); grid.Children.Add(element); }
    private static Button Button(string text, bool primary)
    {
        var button = new Button { Content = text, MinWidth = 104, Height = 38,
            Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(14, 6, 14, 6),
            Foreground = Brushes.White, BorderBrush = Brush("#334155"), BorderThickness = new Thickness(1),
            Background = primary ? new LinearGradientBrush(Color.FromRgb(15, 118, 110), Color.FromRgb(20, 184, 166), 0) : Brush("#1E293B") };
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(OpacityProperty, 0.85)); template.Triggers.Add(hover);
        var disabled = new Trigger { Property = IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(OpacityProperty, 0.45)); template.Triggers.Add(disabled);
        var focus = new Trigger { Property = IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("#38BDF8"))); template.Triggers.Add(focus);
        button.Template = template;
        return button;
    }
}
