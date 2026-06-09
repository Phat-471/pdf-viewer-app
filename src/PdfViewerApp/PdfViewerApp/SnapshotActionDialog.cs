using System.Windows;
using System.Windows.Markup;

namespace PdfViewerApp;

public partial class SnapshotActionDialog : Window, IComponentConnector
{
	internal SnapshotAction SelectedAction { get; private set; }

	internal SnapshotActionDialog(int pageNumber, PdfSnapshotSelection snapshot)
	{
		InitializeComponent();
		SnapshotInfoText.Text = $"Trang {pageNumber}, vi tri X={snapshot.X:P1}, Y={snapshot.Y:P1}, rong={snapshot.Width:P1}, cao={snapshot.Height:P1}.";
	}

	private void Print_Click(object sender, RoutedEventArgs e)
	{
		SelectedAction = SnapshotAction.Print;
		base.DialogResult = true;
	}

	private void Copy_Click(object sender, RoutedEventArgs e)
	{
		SelectedAction = SnapshotAction.CopyImage;
		base.DialogResult = true;
	}

	private void Save_Click(object sender, RoutedEventArgs e)
	{
		SelectedAction = SnapshotAction.SavePng;
		base.DialogResult = true;
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		SelectedAction = SnapshotAction.None;
		base.DialogResult = false;
	}
}
