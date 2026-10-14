#nullable enable
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PdfViewerApp;

internal sealed class PdfTextEditDialog : Window
{
    private readonly TextBox _input;
    private readonly Image _image;
    private readonly TextBlock _status;
    private readonly Button _previewButton;
    private readonly Button _applyButton;
    private readonly Func<string, PdfNativeTextFont?, Task<PdfTextEditPreview>> _preview;
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private readonly TextBlock _fontStatus;
    private readonly Button _fullFontButton;
    private readonly Button _originalFontButton, _vietnameseFontButton;
    private readonly PdfNativeTextRegion _region;
    private PdfNativeTextFont? _selectedFont, _matchingFont, _vietnameseFont, _validatedFont;
    private bool _previewRunning, _refreshPending, _fontLoading;
    private string? _validated;
    private int _generation;
    private bool _closed;
    internal string ReplacementText => _input.Text.Normalize(System.Text.NormalizationForm.FormC);
    internal PdfNativeTextFont? ReplacementFont => _selectedFont;

    internal PdfTextEditDialog(PdfNativeTextRegion region, PdfNativeTextFont? initialFont,
        Func<string, PdfNativeTextFont?, Task<PdfTextEditPreview>> preview)
    {
        _preview = preview; _selectedFont = initialFont; _region = region;
        Title = "Sửa chữ gốc trong PDF";
        Width = 950; Height = 720; MinWidth = 780; MinHeight = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#0F172A"); Foreground = Brush("#F8FAFC"); FontFamily = new FontFamily("Segoe UI");
        var root = new Grid { Margin = new Thickness(24) };
        for (int i = 0; i < 6; i++) root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Content = root;
        Add(root, new TextBlock { Text = "Sửa chữ trong PDF", FontSize = 22, FontWeight = FontWeights.SemiBold }, 0);
        Add(root, new TextBlock { Text = $"Trang {region.PageNumber}   •   {region.FontName}   •   {region.DisplayFontSize:0.###} pt   •   " +
            (region.Embedded ? "Font nhúng trong PDF" : "Font không nhúng — hiển thị phụ thuộc font trên máy"),
            ToolTip = $"Cỡ chữ lưu trong PDF: {region.FontSize:0.###} pt; hệ số phóng đại: {region.TextScale:0.###}.",
            Foreground = Brush("#94A3B8"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 14) }, 1);
        Add(root, new TextBlock { Text = "Nội dung mới", Margin = new Thickness(0, 0, 0, 6) }, 2);
        _input = new TextBox { Text = region.Text, FontSize = 16, Padding = new Thickness(12),
            Background = Brush("#1E293B"), Foreground = Foreground, BorderBrush = Brush("#475569"),
            AcceptsReturn = false, MaxLength = 20000, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        Add(root, _input, 3);
        var fontPanel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        var fontActions = new WrapPanel();
        _originalFontButton = Button("Font nhúng gốc", false);
        _fullFontButton = Button("Dùng font đầy đủ", false); _fullFontButton.IsEnabled = false;
        _vietnameseFontButton = Button("Đổi sang Noto Sans · tiếng Việt", false); _vietnameseFontButton.IsEnabled = false;
        var chooseFontButton = Button("Chọn font TTF…", false);
        fontActions.Children.Add(_originalFontButton); fontActions.Children.Add(_fullFontButton);
        fontActions.Children.Add(_vietnameseFontButton); fontActions.Children.Add(chooseFontButton);
        fontPanel.Children.Add(fontActions);
        _fontStatus = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brush("#38BDF8"), Margin = new Thickness(0, 8, 0, 0) };
        fontPanel.Children.Add(_fontStatus); Add(root, fontPanel, 4);
        Add(root, new TextBlock { Text = "Giữ cỡ chữ, màu và vị trí. Đổi sang font khác sẽ thay hình dáng và chiều rộng chữ; cần kiểm tra xem trước. Để trống để xóa chữ.",
            Foreground = Brush("#94A3B8"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 14) }, 5);
        _image = new Image { Stretch = Stretch.Uniform, Margin = new Thickness(10) };
        Add(root, new Border { Background = Brushes.White, CornerRadius = new CornerRadius(6), Child = _image }, 6);
        var footer = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        _status = new TextBlock { Text = "Đang tạo bản xem trước…", Foreground = Brush("#94A3B8"), TextWrapping = TextWrapping.Wrap,
            MinHeight = 42, Margin = new Thickness(0, 0, 0, 8) };
        footer.Children.Add(_status);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _previewButton = Button("Xem trước", false);
        _applyButton = Button("Áp dụng", true); _applyButton.IsEnabled = false;
        var cancel = Button("Hủy", false); cancel.IsCancel = true;
        actions.Children.Add(_previewButton); actions.Children.Add(_applyButton); actions.Children.Add(cancel);
        footer.Children.Add(actions); Add(root, footer, 7);
        _input.TextChanged += (_, _) => InvalidatePreview();
        _refreshTimer.Tick += async (_, _) => { _refreshTimer.Stop(); await UpdatePreviewAsync(); };
        _originalFontButton.Click += (_, _) => { _selectedFont = null; UpdateFontStatus(); InvalidatePreview(); };
        _fullFontButton.Click += (_, _) => { _selectedFont = _matchingFont; UpdateFontStatus(); InvalidatePreview(); };
        _vietnameseFontButton.Click += (_, _) => { _selectedFont = _vietnameseFont; UpdateFontStatus(); InvalidatePreview(); };
        chooseFontButton.Click += async (_, _) =>
        {
            var picker = new Microsoft.Win32.OpenFileDialog { Title = "Chọn font TrueType có quyền nhúng", Filter = "Font TrueType (*.ttf)|*.ttf" };
            if (picker.ShowDialog(this) != true) return;
            chooseFontButton.IsEnabled = false; _fontLoading = true; InvalidatePreview();
            try
            {
                var font = await Task.Run(() => PdfTextFontSource.Read(picker.FileName));
                if (_closed) return;
                _selectedFont = font; UpdateFontStatus();
            }
            catch (Exception ex) { if (!_closed) MessageBox.Show(this, ex.Message, "Không dùng được font", MessageBoxButton.OK, MessageBoxImage.Information); }
            finally { _fontLoading = false; if (!_closed) { chooseFontButton.IsEnabled = true; InvalidatePreview(); } }
        };
        _previewButton.Click += async (_, _) => await UpdatePreviewAsync();
        _applyButton.Click += (_, _) => { if (_validated == ReplacementText && Equals(_validatedFont, _selectedFont)) DialogResult = true; };
        Loaded += async (_, _) =>
        {
            _input.Focus(); _input.SelectAll();
            var previewTask = UpdatePreviewAsync();
            try
            {
                var fonts = await Task.Run(() => (Matching: PdfTextFontSource.FindMatching(region.FontName),
                    Vietnamese: PdfTextFontSource.ReadVietnameseFont(region.FontName)));
                _matchingFont = fonts.Matching; _vietnameseFont = fonts.Vietnamese;
                if (!_closed)
                {
                    _fullFontButton.IsEnabled = _matchingFont != null;
                    _fullFontButton.ToolTip = _matchingFont == null ? "Chưa tìm được bản đầy đủ cùng font. Có thể chọn file TTF." : _matchingFont.Name;
                    _vietnameseFontButton.IsEnabled = true;
                    _vietnameseFontButton.ToolTip = $"Dùng {_vietnameseFont.Name}. Font sẽ đổi; cỡ chữ, màu và vị trí được giữ nguyên.";
                    UpdateFontStatus();
                }
            }
            catch { if (!_closed) _fullFontButton.ToolTip = "Chưa tìm được font đầy đủ. Có thể chọn file TTF."; }
            await previewTask;
        };
        Closed += (_, _) => { _closed = true; _generation++; _refreshTimer.Stop(); };
        UpdateFontStatus();
    }

    private void UpdateFontStatus()
    {
        bool changedFamily = _selectedFont != null && !PdfTextFontSource.Matches(_region.FontName, _selectedFont.Name);
        _fontStatus.Foreground = Brush(changedFamily ? "#FBBF24" : "#38BDF8");
        _fontStatus.Text = _selectedFont == null
            ? "Đang dùng font nhúng gốc. Nếu thiếu ký tự, có thể dùng bản đầy đủ cùng tên hoặc chọn font khác."
            : $"Đang dùng: {_selectedFont.Name} · " + (changedFamily
                ? "Đã đổi font. Hình dáng và chiều rộng chữ có thể khác bản gốc."
                : "Bản đầy đủ cùng tên; có thể khác phiên bản font nhúng.");
        foreach (var button in new[] { _originalFontButton, _fullFontButton, _vietnameseFontButton })
        {
            bool selected = button == _originalFontButton ? _selectedFont == null
                : button == _fullFontButton ? _matchingFont != null && Equals(_selectedFont, _matchingFont)
                : _vietnameseFont != null && Equals(_selectedFont, _vietnameseFont);
            button.BorderBrush = Brush(selected ? "#38BDF8" : "#334155");
            button.Background = Brush(selected ? "#164E63" : "#1E293B");
        }
    }

    private void InvalidatePreview()
    {
        _generation++; _validated = null; _validatedFont = null; _applyButton.IsEnabled = false;
        _image.Opacity = 0.35;
        _status.Foreground = Brush("#94A3B8");
        _status.Text = "Nội dung hoặc font đã đổi. Đang chờ tạo bản xem trước mới; ảnh mờ là bản trước đó.";
        _refreshTimer.Stop(); _refreshTimer.Start();
    }

    private async Task UpdatePreviewAsync()
    {
        _refreshTimer.Stop();
        if (_fontLoading || _closed) return;
        if (_previewRunning) { _refreshPending = true; return; }
        _previewRunning = true; _refreshPending = false;
        int generation = ++_generation;
        string text = ReplacementText;
        var font = _selectedFont;
        _validated = null; _applyButton.IsEnabled = false; _previewButton.IsEnabled = false;
        _status.Text = "Đang kiểm tra ký tự và tạo bản xem trước…";
        try
        {
            if (font != null) PdfTextFontSource.ValidateText(font, text);
            var result = await _preview(text, font);
            // Keep the first original preview as a reference even if typing made
            // its request stale. Never authorize Apply from a stale result.
            if (!_closed && _image.Source == null && text == _region.Text)
            { _image.Source = result.Image; _image.Opacity = 0.35; }
            if (_closed || generation != _generation) return;
            _image.Source = result.Image; _image.Opacity = 1;
            _status.Foreground = Brush("#94A3B8"); _status.Text = result.Message;
            _validated = text; _validatedFont = font; _applyButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            if (_closed || generation != _generation) return;
            _image.Opacity = 0.35; _status.Foreground = Brush("#FBBF24"); _status.Text = ex.Message +
                (_image.Source == null ? "" : " Ảnh mờ vẫn là bản trước đó.");
            if (font == null && _matchingFont != null)
            {
                string missing = PdfTextFontSource.MissingCharacters(_matchingFont, text);
                if (missing.Length > 0) _status.Text += $" Bản đầy đủ {_matchingFont.Name} trên máy cũng thiếu: {missing}. Có thể chọn Đổi sang Noto Sans · tiếng Việt.";
            }
        }
        finally
        {
            _previewRunning = false;
            if (!_closed) { _previewButton.IsEnabled = true; if (_refreshPending) await UpdatePreviewAsync(); }
        }
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
