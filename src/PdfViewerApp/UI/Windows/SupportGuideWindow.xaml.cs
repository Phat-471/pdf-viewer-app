using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace PdfViewerApp;

public partial class SupportGuideWindow : Window
{
    public SupportGuideWindow(bool selectFeedbackTab = false)
    {
        InitializeComponent();
        
        // Load version info
        try
        {
            VersionTextBlock.Text = $"Phiên bản: v{ActivationLicense.AppVersion} (HPhat Edition)";
        }
        catch
        {
            VersionTextBlock.Text = "Phiên bản: v1.2.4 (HPhat Edition)";
        }

        if (selectFeedbackTab)
        {
            MainTabControl.SelectedItem = FeedbackTab;
        }
        else
        {
            MainTabControl.SelectedItem = GuideTab;
        }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Không thể mở liên kết: " + ex.Message, "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        e.Handled = true;
    }

    private void ZaloSupport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://zalo.me/0974194305",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Không thể mở Zalo: " + ex.Message, "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
