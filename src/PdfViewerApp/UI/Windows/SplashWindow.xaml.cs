using System.Windows;
using System.Windows.Markup;

namespace PdfViewerApp;

public partial class SplashWindow : Window, IComponentConnector
{
	public SplashWindow()
	{
		InitializeComponent();
		VersionTextBlock.Text = "v" + ActivationLicense.AppVersion;
	}
}
