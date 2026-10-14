using System;
using System.Windows;
using System.Windows.Controls;

namespace PdfViewerApp;

public partial class OcrOptionsDialog : Window
{
    internal PdfOcrOptions Options { get; private set; } = new();
    internal string Scope => (ScopeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Current";
    internal OcrOptionsDialog(bool hasRegion, bool regionOnly = false)
    {
        InitializeComponent();
        RegionItem.IsEnabled = hasRegion;
        if (regionOnly) { ScopeCombo.SelectedItem = RegionItem; ScopeCombo.IsEnabled = false; }
        var preferences = AppPreferences.Load();
        string language = preferences.OcrLanguage.StartsWith("vi", StringComparison.OrdinalIgnoreCase) ? "vie" : preferences.OcrLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "eng" : "vie+eng";
        if (preferences.OcrLanguage == "vie+eng") language = "vie+eng";
        foreach (ComboBoxItem item in LanguageCombo.Items) if (Equals(item.Tag, language)) LanguageCombo.SelectedItem = item;
        foreach (ComboBoxItem item in DpiCombo.Items) if (item.Tag?.ToString() == preferences.OcrDpi.ToString()) DpiCombo.SelectedItem = item;
        NativeCheck.IsChecked = preferences.OcrPreferNative; SparseCheck.IsChecked = preferences.OcrSparse;
    }
    private void Start_Click(object sender, RoutedEventArgs e)
    {
        string language = (LanguageCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "vie+eng";
        int dpi = int.Parse((DpiCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "300");
        Options = new PdfOcrOptions(language, dpi, NativeCheck.IsChecked == true, SparseCheck.IsChecked == true);
        try { var preferences = AppPreferences.Load(); preferences.OcrLanguage = language; preferences.OcrDpi = dpi; preferences.OcrPreferNative = Options.PreferNative; preferences.OcrSparse = Options.Sparse; preferences.Save(); }
        catch (Exception ex) { PdfPerfLogger.Log("OCR preferences: " + ex.Message); }
        DialogResult = true;
    }
}
