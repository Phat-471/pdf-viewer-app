#nullable enable
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace PdfViewerApp;

/// <summary>
/// In-place text editor matching the PdfCraft design philosophy:
/// - True in-place editing directly over the paragraph bounds.
/// - Zero bulky floating toolbars or popup cards obstructing adjacent lines.
/// - Auto-commit when clicking away (clicking outside on canvas or another block).
/// - Enter / Ctrl+Enter commits; Esc cancels and reverts.
/// - Automatic vector glyph fallback & font preservation powered by pdfcraft-edit.
/// </summary>
internal sealed class PdfInlineTextEditor : Canvas, IDisposable
{
    private readonly PdfNativeTextRegion _region;
    private readonly TextBox _input;
    private bool _closed, _applying;
    private Canvas? _page;
    private Rect _placementBounds;

    internal event Action? ApplyRequested;
    internal event Action? CancelRequested;

    internal string ReplacementText => _input.Text.Normalize(System.Text.NormalizationForm.FormC);
    internal PdfNativeTextFont? ReplacementFont => null;
    internal bool CanApply => !_closed && !_applying;
    internal bool IsFontPopupOpen => false;
    internal bool IsInputFocused => _input.IsKeyboardFocusWithin;
    internal void FocusInput() => _input.Focus();
    internal bool HasDraft => ReplacementText != _region.Text;

    internal void UndoDraft(bool redo)
    {
        if (redo) { if (_input.CanRedo) _input.Redo(); }
        else if (_input.CanUndo) _input.Undo();
        _input.Focus();
    }

    internal PdfInlineTextEditor(PdfNativeTextRegion region)
    {
        _region = region;
        bool isMultiLine = region.Text.Contains('\n') || region.Text.Contains('\r');

        _input = new TextBox
        {
            Text = region.Text,
            AcceptsReturn = isMultiLine,
            TextWrapping = isMultiLine ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MaxLength = 50000,
            Padding = new Thickness(4, 2, 4, 2),
            Background = Brushes.White,
            Foreground = new SolidColorBrush(Color.FromArgb((byte)(region.TextColor >> 24),
                (byte)(region.TextColor >> 16), (byte)(region.TextColor >> 8), (byte)region.TextColor)),
            BorderBrush = Brush("#0284C7"),
            BorderThickness = new Thickness(1.5),
            SelectionBrush = Brush("#7DD3FC"),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalContentAlignment = VerticalAlignment.Top,
            Effect = new DropShadowEffect
            {
                BlurRadius = 8,
                ShadowDepth = 1.5,
                Opacity = 0.35,
                Color = Colors.Black
            },
            ToolTip = "Sửa trực tiếp (PdfCraft) · Enter: áp dụng · Esc: hủy · Click ra ngoài để tự động lưu"
        };

        Children.Add(_input);

        // Auto-commit on LostFocus (clicking outside the editor)
        _input.LostKeyboardFocus += OnInputLostKeyboardFocus;

        _input.PreviewKeyDown += (sender, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                if (!_applying) CancelRequested?.Invoke();
            }
            else if (e.Key == Key.Enter)
            {
                // In single-line text, Enter commits immediately.
                // In multi-line text, Ctrl+Enter commits, while Enter inserts a newline.
                if (!isMultiLine || Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    e.Handled = true;
                    if (CanApply) ApplyRequested?.Invoke();
                }
            }
        };

        // Prevent click events inside the input box from bubbling up to page tools
        MouseDown += (_, e) => e.Handled = true;
        MouseUp += (_, e) => e.Handled = true;

        SetInputTypeface(region.SourceFontName);
    }

    internal PdfInlineTextEditor(PdfNativeTextRegion region, object? _unusedFont, object? _unusedPreview)
        : this(region)
    {
    }

    private void OnInputLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_closed || _applying) return;

        // If focus moved to another child inside this editor, don't auto-commit
        if (IsKeyboardFocusWithin) return;

        // Click-away auto-commit: if changes were made, apply them; otherwise cancel cleanly
        if (HasDraft)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                if (!_closed && !_applying && CanApply)
                {
                    ApplyRequested?.Invoke();
                }
            }));
        }
        else
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                if (!_closed && !_applying)
                {
                    CancelRequested?.Invoke();
                }
            }));
        }
    }

    internal void Place(Canvas page, Rect bounds, Point? click = null)
    {
        _page = page;
        _placementBounds = bounds;

        _input.Width = Math.Max(50, bounds.Width + 12);
        _input.FontSize = Math.Clamp(_region.DisplayFontSize * page.Height * _region.DisplayBounds.Height / Math.Max(.01, _region.Bounds.Height), 12, 160);
        _input.Height = Math.Max(26, Math.Max(bounds.Height + 6, _input.FontSize * Math.Max(1.2, _input.FontFamily.LineSpacing) + 6));

        Width = _input.Width;
        Height = _input.Height;

        SetLeft(this, bounds.Left - 2);
        SetTop(this, bounds.Top - 2);
        Panel.SetZIndex(this, 10000);

        if (click != null)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (_closed) return;
                _input.Focus();
                Point p = new(click.Value.X - bounds.Left, click.Value.Y - bounds.Top);
                int index = _input.GetCharacterIndexFromPoint(p, true);
                _input.CaretIndex = Math.Clamp(index, 0, _input.Text.Length);
            }));
        }
    }

    internal void SetApplying(bool applying)
    {
        _applying = applying;
        if (_closed) return;
        _input.IsReadOnly = applying;
    }

    internal void ReportApplyFailure(string message)
    {
        _input.ToolTip = message;
    }

    private static string CleanFontName(string name)
    {
        if (name.Length > 7 && name[6] == '+') name = name[7..];
        return name;
    }

    private void SetInputTypeface(string name)
    {
        name = CleanFontName(name);
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

    public void Dispose()
    {
        _closed = true;
        _input.LostKeyboardFocus -= OnInputLostKeyboardFocus;
        ApplyRequested = null;
        CancelRequested = null;
    }
}
