using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;
using PdfViewerApp.Services;

namespace PdfViewerApp.Tests
{
    public class ImageToPdfTests
    {
        [Theory]
        [InlineData("test.jpg", true)]
        [InlineData("test.jpeg", true)]
        [InlineData("test.jfif", true)]
        [InlineData("test.png", true)]
        [InlineData("test.bmp", true)]
        [InlineData("test.webp", true)]
        [InlineData("test.pdf", false)]
        [InlineData("test.docx", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsSupportedImage_ValidatesExtensions(string? path, bool expected)
        {
            bool result = ImageToPdfService.IsSupportedImage(path);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ConvertImageToPdf_ConvertsImageSuccessfully()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "PdfProTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string imagePath = Path.Combine(tempDir, "sample.png");

            try
            {
                // Create a sample 100x100 bitmap
                int width = 100;
                int height = 100;
                var pixelFormat = PixelFormats.Bgr32;
                int rawStride = (width * pixelFormat.BitsPerPixel + 7) / 8;
                byte[] rawPixels = new byte[rawStride * height];
                for (int i = 0; i < rawPixels.Length; i += 4)
                {
                    rawPixels[i] = 255;     // Blue
                    rawPixels[i + 1] = 0;   // Green
                    rawPixels[i + 2] = 0;   // Red
                    rawPixels[i + 3] = 255; // Alpha
                }

                var bitmap = BitmapSource.Create(width, height, 96, 96, pixelFormat, null, rawPixels, rawStride);
                using (var fileStream = new FileStream(imagePath, FileMode.Create))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(fileStream);
                }

                // Act: Convert image to PDF
                string outputPath = ImageToPdfService.ConvertImageToPdf(imagePath);

                // Assert
                Assert.True(File.Exists(outputPath), "Output PDF file should exist.");
                var fileInfo = new FileInfo(outputPath);
                Assert.True(fileInfo.Length > 0, "Output PDF should have non-zero size.");

                // Cleanup generated PDF
                try { File.Delete(outputPath); } catch { }
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
