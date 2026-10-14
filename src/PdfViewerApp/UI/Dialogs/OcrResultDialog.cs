using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace PdfViewerApp;

internal sealed class OcrResultDialog : Window
{
    internal OcrResultDialog(string text)
    {
        Title = "Kết quả đọc chữ"; Width = 720; Height = 520; MinWidth = 480; MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)); Foreground = Brushes.White;
        var panel = new DockPanel { Margin = new Thickness(20) }; Content = panel;
        var hint = new TextBlock { Text = "Có thể chọn và copy một phần kết quả. Kiểm tra lại mã bản vẽ, số liệu và thứ tự đọc trước khi sử dụng.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(hint, Dock.Top); panel.Children.Add(hint);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(actions, Dock.Bottom); panel.Children.Add(actions);
        var result = new TextBox { Text = string.IsNullOrWhiteSpace(text) ? "Không tìm thấy chữ trong phạm vi đã chọn." : text,
            IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(12), Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)), Foreground = Brushes.White, FontSize = 14 };
        panel.Children.Add(result);
        foreach (string name in new[] { "Copy tất cả", "Lưu TXT…", "Đóng" })
        {
            var button = new Button { Content = name, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(6, 0, 0, 0), IsEnabled = name == "Đóng" || !string.IsNullOrWhiteSpace(text) };
            button.Click += (_, _) =>
            {
                try
                {
                    if (name == "Đóng") Close();
                    else if (name == "Copy tất cả") Clipboard.SetText(text);
                    else { var save = new SaveFileDialog { Filter = "Văn bản (*.txt)|*.txt", FileName = "OCR.txt" }; if (save.ShowDialog(this) == true) File.WriteAllText(save.FileName, text, Encoding.UTF8); }
                }
                catch (Exception ex) { MessageBox.Show(this, "Không thực hiện được thao tác. Hãy thử lại. " + ex.Message, "Kết quả OCR"); }
            };
            actions.Children.Add(button);
        }
    }
}
