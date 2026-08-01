using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PdfViewerApp;

namespace PdfViewerApp;

public partial class PdfDocumentTab
{

        private TextBox? _inlineEditorTextBox;
        private PdfInterop.PdfCore.RawTextRegion? _activeEditingRegion;

        /// <summary>
        /// Khởi tạo và kích hoạt chế độ Inline Text Editor trực tiếp trên Canvas
        /// </summary>
        public void EnableInlineTextEditing()
        {
            if (_inlineEditorTextBox != null) return;

            _inlineEditorTextBox = new TextBox
            {
                Visibility = Visibility.Collapsed,
                AcceptsReturn = false,
                BorderBrush = Brushes.DodgerBlue,
                BorderThickness = new Thickness(1.5),
                Background = Brushes.White,
                Foreground = Brushes.Black,
                Padding = new Thickness(2, 0, 2, 0)
            };

            _inlineEditorTextBox.KeyDown += InlineEditorTextBox_KeyDown;
            _inlineEditorTextBox.LostFocus += InlineEditorTextBox_LostFocus;

            if (_activeCanvas != null)
            {
                _activeCanvas.Children.Add(_inlineEditorTextBox);
            }
        }

        /// <summary>
        /// Kích hoạt ô soạn thảo tại vị trí (x, y) trên trang PDF
        /// </summary>
        public void BeginEditRegion(PdfInterop.PdfCore.RawTextRegion region, string initialText)
        {
            EnableInlineTextEditing();
            if (_inlineEditorTextBox == null) return;

            _activeEditingRegion = region;
            _inlineEditorTextBox.Text = initialText;
            _inlineEditorTextBox.FontSize = region.FontSize > 0 ? region.FontSize : 14;
            _inlineEditorTextBox.Width = Math.Max(region.Width, 120);
            _inlineEditorTextBox.Height = Math.Max(region.Height, 28);

            Canvas.SetLeft(_inlineEditorTextBox, region.X);
            Canvas.SetTop(_inlineEditorTextBox, region.Y);

            _inlineEditorTextBox.Visibility = Visibility.Visible;
            _inlineEditorTextBox.Focus();
            _inlineEditorTextBox.SelectAll();
        }

        private void InlineEditorTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitInlineEdit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelInlineEdit();
                e.Handled = true;
            }
        }

        private void InlineEditorTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitInlineEdit();
        }

        public void CommitInlineEdit()
        {
            if (_inlineEditorTextBox == null || _inlineEditorTextBox.Visibility != Visibility.Visible) return;
            if (_activeEditingRegion == null || string.IsNullOrEmpty(CurrentPdfPath)) return;

            string newText = _inlineEditorTextBox.Text;
            string tempOutputPath = CurrentPdfPath + ".tmp.pdf";

            bool success = PdfInterop.PdfCore.pdf_replace_text_object(
                CurrentPdfPath,
                SelectedPageNumber,
                _activeEditingRegion.Value.X,
                _activeEditingRegion.Value.Y,
                _activeEditingRegion.Value.Width,
                _activeEditingRegion.Value.Height,
                newText,
                tempOutputPath);

            if (success && File.Exists(tempOutputPath))
            {
                try
                {
                    File.Copy(tempOutputPath, CurrentPdfPath, true);
                    File.Delete(tempOutputPath);
                }
                catch { }
            }

            _inlineEditorTextBox.Visibility = Visibility.Collapsed;
            _activeEditingRegion = null;
        }

        public void CancelInlineEdit()
        {
            if (_inlineEditorTextBox != null)
            {
                _inlineEditorTextBox.Visibility = Visibility.Collapsed;
            }
            _activeEditingRegion = null;
        }
    }


