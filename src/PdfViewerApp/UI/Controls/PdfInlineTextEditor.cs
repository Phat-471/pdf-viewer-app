#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PdfViewerApp;

// The text box is an editing surface only. The native bitmap and saved PDF are
// produced from the actual PDF text object, never from a screenshot or a cover.
internal sealed class PdfInlineTextEditor : Canvas, IDisposable
{
    private readonly PdfNativeTextRegion _region;
    private readonly TextBox _input;
    private readonly Border _tools;
    private readonly ComboBox _fontChoice;
    private readonly Image _previewImage;
    private readonly TextBlock _status;
    private readonly Button _apply;
    private readonly Func<string, PdfNativeTextFont?, Task<PdfTextEditPreview>> _preview;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly Task<PdfNativeTextFont?> _matching;
    private PdfNativeTextFont? _requestedFont, _resolvedFont;
    private string? _validatedText;
    private bool _running, _pending, _closed, _fontLoading, _applying;
    private readonly Button _choose, _cancel;
    private int _generation, _fontRequest;
    private int _mode;
    private PdfNativeTextFont? _chosenFont;
    private string? _fontError;
    private Canvas? _page;
    private Rect _placementBounds;
    internal event Action? ApplyRequested;
    internal event Action? CancelRequested;
    internal string ReplacementText => _input.Text.Normalize(System.Text.NormalizationForm.FormC);
    internal PdfNativeTextFont? ReplacementFont => _resolvedFont;
    internal bool CanApply => !_closed && !_running && !_fontLoading && !_applying && _validatedText == ReplacementText && _apply.IsEnabled;
    internal bool IsFontPopupOpen => _fontChoice.IsDropDownOpen;
    internal bool IsInputFocused => _input.IsKeyboardFocusWithin;
    internal void FocusInput() => _input.Focus();
    internal bool HasDraft => ReplacementText != _region.Text || !Equals(_requestedFont, InitialFont);
    internal void UndoDraft(bool redo)
    {
        if (redo) { if (_input.CanRedo) _input.Redo(); }
        else if (_input.CanUndo) _input.Undo();
        _input.Focus();
    }
    private PdfNativeTextFont? InitialFont { get; }

    internal PdfInlineTextEditor(PdfNativeTextRegion region, PdfNativeTextFont? initialFont,
        Func<string, PdfNativeTextFont?, Task<PdfTextEditPreview>> preview)
    {
        _region = region; InitialFont = initialFont; _chosenFont = initialFont; _requestedFont = initialFont; _preview = preview;
        _matching = Task.Run(() => PdfTextFontSource.FindMatching(region.SourceFontName));
        _ = _matching.ContinueWith(t => { if (t.IsFaulted) _ = t.Exception; }, TaskScheduler.Default);
        _input = new TextBox { Text = region.Text, AcceptsReturn = false, MaxLength = 20000, Padding = new Thickness(2, 0, 2, 0),
            Background = Brushes.White, Foreground = new SolidColorBrush(Color.FromArgb((byte)(region.TextColor >> 24),
                (byte)(region.TextColor >> 16), (byte)(region.TextColor >> 8), (byte)region.TextColor)),
            BorderBrush = Brush("#0284C7"), BorderThickness = new Thickness(1.5), SelectionBrush = Brush("#7DD3FC"),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "Sửa trực tiếp · Ctrl+Enter: áp dụng · Esc: hủy · Ctrl+Z: hoàn tác nội dung đang nhập" };
        Children.Add(_input);
        var panel = new StackPanel();
        var row = new WrapPanel();
        _fontChoice = new ComboBox { Width = 235, Height = 30, Margin = new Thickness(0, 0, 8, 0),
            ToolTip = "Tự động chỉ dùng font đầy đủ cùng họ và kiểu chữ. Chọn Noto Sans sẽ đổi font.",
            ItemsSource = initialFont == null
                ? new[] { "Giữ font · tự tìm bản đầy đủ", "Chỉ dùng font nhúng gốc", "Bản đầy đủ cùng họ và kiểu", "Đổi sang Noto Sans · tiếng Việt" }
                : new[] { "Giữ font · tự tìm bản đầy đủ", "Chỉ dùng font nhúng gốc", "Bản đầy đủ cùng họ và kiểu", "Đổi sang Noto Sans · tiếng Việt", initialFont.Name },
            SelectedIndex = initialFont == null ? 0 : 4 };
        _mode = _fontChoice.SelectedIndex;
        row.Children.Add(_fontChoice);
        _choose = Button("TTF…", false); row.Children.Add(_choose);
        _apply = Button("✓ Áp dụng", true); _apply.IsEnabled = false; row.Children.Add(_apply);
        _cancel = Button("Hủy", false); row.Children.Add(_cancel);
        panel.Children.Add(row);
        panel.Children.Add(new TextBlock { Text = $"{region.SourceFontName} · {region.DisplayFontSize:0.###} pt · Giữ cỡ, màu, vị trí và góc chữ",
            Foreground = Brush("#94A3B8"), Margin = new Thickness(0, 7, 0, 5), TextWrapping = TextWrapping.Wrap });
        _status = new TextBlock { Foreground = Brush("#38BDF8"), TextWrapping = TextWrapping.Wrap, Text = "Đang chuẩn bị bản xem trước PDF…" };
        panel.Children.Add(_status);
        _previewImage = new Image { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly,
            MaxHeight = 100, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(8), Visibility = Visibility.Collapsed };
        panel.Children.Add(new Border { Background = Brushes.White, CornerRadius = new CornerRadius(3), Child = _previewImage, Margin = new Thickness(0, 7, 0, 0) });
        _tools = new Border { Width = 555, Background = Brush("#0F172A"), BorderBrush = Brush("#38BDF8"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10), Child = panel };
        Children.Add(_tools);
        _input.TextChanged += (_, _) => Invalidate();
        _timer.Tick += async (_, _) => await ValidateAsync();
        _apply.Click += (_, _) => { if (CanApply) ApplyRequested?.Invoke(); };
        _cancel.Click += (_, _) => { if (!_applying) CancelRequested?.Invoke(); };
        _fontChoice.SelectionChanged += async (_, _) => await ChangeFontAsync();
        _choose.Click += async (_, _) =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Chọn font TrueType có quyền nhúng", Filter = "Font TrueType (*.ttf)|*.ttf" };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            int request = ++_fontRequest; _generation++; _fontLoading = true; _apply.IsEnabled = false;
            try
            {
                var font = await Task.Run(() => PdfTextFontSource.Read(dialog.FileName));
                if (_closed || request != _fontRequest) return;
                _chosenFont = font;
                _fontChoice.ItemsSource = new[] { "Giữ font · tự tìm bản đầy đủ", "Chỉ dùng font nhúng gốc", "Bản đầy đủ cùng họ và kiểu", "Đổi sang Noto Sans · tiếng Việt", font.Name };
                _fontChoice.SelectedIndex = 4;
            }
            catch (Exception ex) { if (!_closed) ShowError(ex.Message); }
            finally { if (request == _fontRequest) { _fontLoading = false; if (!_closed) Invalidate(); } }
        };
        PreviewKeyDown += (_, e) =>
        {
            if (_fontChoice.IsDropDownOpen) return;
            if (e.Key == Key.Escape) { e.Handled = true; if (!_applying) CancelRequested?.Invoke(); }
            else if (e.Key == Key.Enter && (_input.IsKeyboardFocusWithin || Keyboard.Modifiers == ModifierKeys.Control))
                { e.Handled = true; if (CanApply) ApplyRequested?.Invoke(); }
        };
        // Prevent page tools from interpreting clicks in this editor as new selections.
        MouseDown += (_, e) => e.Handled = true;
        MouseUp += (_, e) => e.Handled = true;
        SetInputTypeface(initialFont?.Name ?? region.SourceFontName);
        Invalidate();
    }

    internal void Place(Canvas page, Rect bounds, Point? click = null)
    {
        _page = page; _placementBounds = bounds;
        _tools.Width = Math.Min(555, Math.Max(280, page.Width));
        Width = Math.Max(bounds.Width, _tools.Width); Height = Math.Max(30, bounds.Height) + 205;
        SetLeft(this, bounds.Left); SetTop(this, bounds.Top); Panel.SetZIndex(this, 10000);
        _input.Width = Math.Max(90, bounds.Width + 16);
        _input.FontSize = Math.Clamp(_region.DisplayFontSize * page.Height * _region.DisplayBounds.Height / Math.Max(.01, _region.Bounds.Height), 12, 200);
        // Glyph bounds omit ascenders/descenders. Reserve a full line box and
        // keep the horizontal scrollbar from consuming the editing line.
        _input.Height = Math.Max(32, _input.FontSize * Math.Max(1.25, _input.FontFamily.LineSpacing) + 10);
        Height = Math.Max(Height, _input.Height + 205);
        SetTop(_tools, bounds.Top + _input.Height + 205 > page.Height && bounds.Top > 205 ? -205 : _input.Height + 7);
        SetLeft(_tools, Math.Clamp(0, -bounds.Left, Math.Max(-bounds.Left, page.Width - bounds.Left - _tools.Width)));
        if (click != null) Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (_closed) return;
            _input.Focus();
            Point p = new(click.Value.X - bounds.Left, click.Value.Y - bounds.Top);
            int index = _input.GetCharacterIndexFromPoint(p, true);
            _input.CaretIndex = Math.Clamp(index, 0, _input.Text.Length);
        }));
    }

    private async Task ChangeFontAsync()
    {
        int mode = _fontChoice.SelectedIndex;
        if (mode < 0) return;
        _fontError = null;
        int request = ++_fontRequest; _generation++; _mode = mode; _fontLoading = true; _apply.IsEnabled = false;
        try
        {
            var font = mode == 2 ? await Task.Run(() => PdfTextFontSource.FindMatching(_region.SourceFontName)) : mode == 3 ? await Task.Run(() => PdfTextFontSource.ReadVietnameseFont(_region.SourceFontName))
                : mode == 4 ? _chosenFont : null;
            if (_closed || request != _fontRequest) return;
            if (mode == 2 && font == null) throw new InvalidOperationException("Không tìm được bản đầy đủ cùng họ và kiểu chữ. Hãy chọn file TTF.");
            _requestedFont = font;
            SetInputTypeface(font?.Name ?? _region.SourceFontName);
        }
        catch (Exception ex) { if (!_closed && request == _fontRequest) { _requestedFont = null; _fontError = ex.Message; ShowError(ex.Message); } }
        finally { if (request == _fontRequest) { _fontLoading = false; Invalidate(); } }
    }

    private void Invalidate()
    {
        if (_closed) return;
        _generation++; _validatedText = null; _resolvedFont = null; _apply.IsEnabled = false;
        _previewImage.Source = null; _previewImage.Visibility = Visibility.Collapsed;
        _status.Foreground = Brush("#94A3B8"); _status.Text = "Đang chờ kiểm tra chữ PDF…";
        _timer.Stop(); _timer.Start();
    }

    internal void SetApplying(bool applying)
    {
        _applying = applying;
        if (_closed) return;
        _input.IsReadOnly = applying;
        _fontChoice.IsEnabled = _choose.IsEnabled = _cancel.IsEnabled = !applying;
        _apply.IsEnabled = !applying && !_fontLoading && _validatedText == ReplacementText;
        if (applying) { _timer.Stop(); _status.Text = "Đang áp dụng chữ và làm mới trang PDF…"; }
    }
    internal void ReportApplyFailure(string message) { if (!_closed) ShowError(message); }

    private async Task ValidateAsync()
    {
        _timer.Stop();
        if (_closed || _fontLoading || _applying) return;
        if (_fontError != null) { ShowError(_fontError); return; }
        if (_running) { _pending = true; return; }
        _running = true; _pending = false;
        int generation = _generation;
        string text = ReplacementText;
        var font = _requestedFont; int mode = _mode;
        _status.Text = "Đang kiểm tra font và tạo bản xem trước PDF…";
        try
        {
            PdfTextEditPreview result;
            try { result = await _preview(text, font); }
            catch (PdfTextEncodingException) when (font == null && mode == 0)
            {
                font = await _matching;
                if (font == null) throw new InvalidOperationException("Font nhúng thiếu ký tự; chưa tìm được bản đầy đủ cùng họ và kiểu. Hãy chọn TTF hoặc Noto Sans.");
                if (PdfTextFontSource.MissingCharacters(font, text) is string missing && missing.Length > 0)
                    throw new InvalidOperationException($"{font.Name} đầy đủ cũng thiếu: {missing}. Có thể chọn Noto Sans để nhập tiếng Việt.");
                result = await _preview(text, font);
            }
            if (_closed || generation != _generation) return;
            _resolvedFont = font; _validatedText = text;
            SetInputTypeface(font?.Name ?? _region.SourceFontName);
            _previewImage.Source = result.Image; _previewImage.Visibility = result.Image == null ? Visibility.Collapsed : Visibility.Visible;
            _status.Foreground = Brush(font != null && !PdfTextFontSource.Matches(_region.SourceFontName, font.Name) ? "#FBBF24" : "#38BDF8");
            _status.Text = "Bản xem trước PDF: " + result.Message +
                (font != null && !PdfTextFontSource.Matches(_region.SourceFontName, font.Name) ? " Đã đổi font; hình dáng có thể khác." : "");
            _apply.IsEnabled = true;
        }
        catch (Exception ex) { if (!_closed && generation == _generation) ShowError(ex.Message); }
        finally { _running = false; if (!_closed && _pending) await ValidateAsync(); }
    }

    private void ShowError(string message) { _validatedText = null; _apply.IsEnabled = false; _status.Foreground = Brush("#FBBF24"); _status.Text = message; }
    private void SetInputTypeface(string name)
    {
        if (name.Length > 7 && name[6] == '+') name = name[7..];
        name = name.Replace("TimesNewRomanPS", "TimesNewRoman", StringComparison.OrdinalIgnoreCase)
            .Replace("PSMT", "", StringComparison.OrdinalIgnoreCase);
        _input.FontWeight = name.Contains("Bold", StringComparison.OrdinalIgnoreCase) ? FontWeights.Bold : FontWeights.Normal;
        _input.FontStyle = name.Contains("Italic", StringComparison.OrdinalIgnoreCase) || name.Contains("Oblique", StringComparison.OrdinalIgnoreCase) ? FontStyles.Italic : FontStyles.Normal;
        name = System.Text.RegularExpressions.Regex.Replace(name, "[- ]?(Bold[ -]?(Italic|Oblique)|Bold|Italic|Oblique|Regular)(MT)?$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (name.Equals("ArialNarrow", StringComparison.OrdinalIgnoreCase)) name = "Arial Narrow";
        if (name.Equals("TimesNewRoman", StringComparison.OrdinalIgnoreCase) || name.Equals("TimesNewRomanPSMT", StringComparison.OrdinalIgnoreCase)) name = "Times New Roman";
        if (name.Equals("CourierNew", StringComparison.OrdinalIgnoreCase)) name = "Courier New";
        if (name.Equals("NotoSans", StringComparison.OrdinalIgnoreCase)) name = "Noto Sans";
        if (name.Equals("ArialMT", StringComparison.OrdinalIgnoreCase)) name = "Arial";
        _input.FontFamily = new FontFamily(name);
        if (_page != null) Place(_page, _placementBounds);
    }
    private static Brush Brush(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
    private static Button Button(string label, bool primary) => new() { Content = label, Height = 30, Padding = new Thickness(10, 3, 10, 3),
        Margin = new Thickness(0, 0, 6, 0), Background = Brush(primary ? "#0F766E" : "#1E293B"), Foreground = Brushes.White, BorderBrush = Brush("#334155") };
    public void Dispose() { _closed = true; _generation++; _timer.Stop(); ApplyRequested = null; CancelRequested = null; }
}
