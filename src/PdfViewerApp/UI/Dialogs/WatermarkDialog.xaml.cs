using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PdfViewerApp;

public partial class WatermarkDialog : Window
{
    public string WatermarkText { get; private set; } = "BẢN QUYỀN / BẢN VẼ MẪU";
    public double WatermarkFontSize { get; private set; } = 48.0;
    public double WatermarkAngle { get; private set; } = 45.0;
    public double WatermarkOpacity { get; private set; } = 0.35;
    public string WatermarkColorHex { get; private set; } = "#EF4444";

    public string? WatermarkedPdfPath { get; private set; }

    public WatermarkDialog()
    {
        InitializeComponent();
        UpdatePreview();
    }

    public WatermarkDialog(string? pdfPath) : this()
    {
    }

    private void WatermarkSettings_Changed(object sender, EventArgs e)
    {
        if (!IsInitialized) return;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (WatermarkTextBox == null || WatermarkPreviewText == null) return;

        WatermarkText = string.IsNullOrWhiteSpace(WatermarkTextBox.Text) ? "WATERMARK" : WatermarkTextBox.Text;
        WatermarkOpacity = OpacitySlider?.Value ?? 0.35;

        if (OpacityValueText != null)
        {
            OpacityValueText.Text = $"{(int)(WatermarkOpacity * 100)}%";
        }

        if (FontSizeComboBox?.SelectedItem is ComboBoxItem fontItem && double.TryParse(fontItem.Tag?.ToString(), out var fs))
        {
            WatermarkFontSize = fs;
        }

        if (AngleComboBox?.SelectedItem is ComboBoxItem angleItem && double.TryParse(angleItem.Tag?.ToString(), out var angle))
        {
            WatermarkAngle = angle;
        }

        if (ColorComboBox?.SelectedItem is ComboBoxItem colorItem)
        {
            WatermarkColorHex = colorItem.Tag?.ToString() ?? "#EF4444";
        }

        WatermarkPreviewText.Text = WatermarkText;
        WatermarkPreviewText.Opacity = WatermarkOpacity;
        
        try
        {
            Color color = (Color)ColorConverter.ConvertFromString(WatermarkColorHex);
            WatermarkPreviewText.Foreground = new SolidColorBrush(color);
        }
        catch
        {
            WatermarkPreviewText.Foreground = Brushes.Red;
        }

        if (WatermarkPreviewText.RenderTransform is RotateTransform rotateTransform)
        {
            rotateTransform.Angle = WatermarkAngle;
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
        DialogResult = true;
    }
}
