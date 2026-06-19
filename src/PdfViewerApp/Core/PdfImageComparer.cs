using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PdfViewerApp
{
    public static class PdfImageComparer
    {
        /// <summary>
        /// So sánh hai hình ảnh và tạo ra một hình ảnh Diff làm nổi bật điểm khác biệt.
        /// Nền chung sẽ có màu xám nhạt mờ. 
        /// Nét cũ bị xóa (chỉ có trong imageA) tô màu Đỏ.
        /// Nét mới được thêm (chỉ có trong imageB) tô màu Xanh Lá (Green).
        /// </summary>
        public static BitmapSource? Compare(BitmapSource bitmapA, BitmapSource bitmapB)
        {
            if (bitmapA == null || bitmapB == null) return null;

            // Đảm bảo 2 hình ảnh cùng kích thước bằng cách co dãn bitmapB theo bitmapA nếu cần
            BitmapSource sourceB = bitmapB;
            if (bitmapA.PixelWidth != bitmapB.PixelWidth || bitmapA.PixelHeight != bitmapB.PixelHeight)
            {
                double scaleX = (double)bitmapA.PixelWidth / bitmapB.PixelWidth;
                double scaleY = (double)bitmapA.PixelHeight / bitmapB.PixelHeight;
                TransformedBitmap scaledB = new TransformedBitmap(bitmapB, new ScaleTransform(scaleX, scaleY));
                sourceB = scaledB;
            }

            // Chuyển đổi cả hai sang định dạng Bgr32 hoặc Pbgra32 để dễ dàng duyệt byte
            FormatConvertedBitmap formattedA = new FormatConvertedBitmap(bitmapA, PixelFormats.Bgra32, null, 0);
            FormatConvertedBitmap formattedB = new FormatConvertedBitmap(sourceB, PixelFormats.Bgra32, null, 0);

            int width = formattedA.PixelWidth;
            int height = formattedA.PixelHeight;
            int stride = width * 4;
            int byteCount = stride * height;

            byte[] pixelsA = new byte[byteCount];
            byte[] pixelsB = new byte[byteCount];
            byte[] diffPixels = new byte[byteCount];

            formattedA.CopyPixels(pixelsA, stride, 0);
            formattedB.CopyPixels(pixelsB, stride, 0);

            // Duyệt qua từng pixel (mỗi pixel chiếm 4 bytes: Blue, Green, Red, Alpha)
            for (int i = 0; i < byteCount; i += 4)
            {
                byte bA = pixelsA[i];
                byte gA = pixelsA[i + 1];
                byte rA = pixelsA[i + 2];
                byte aA = pixelsA[i + 3];

                byte bB = pixelsB[i];
                byte gB = pixelsB[i + 1];
                byte rB = pixelsB[i + 2];
                byte aB = pixelsB[i + 3];

                // Tính toán độ lệch màu giữa 2 pixel
                int diffR = Math.Abs(rA - rB);
                int diffG = Math.Abs(gA - gB);
                int diffB = Math.Abs(bA - bB);
                int diffA = Math.Abs(aA - aB);

                // Ngưỡng phát hiện khác biệt (tolerance)
                bool isDifferent = (diffR > 15 || diffG > 15 || diffB > 15 || diffA > 15);

                if (!isDifferent)
                {
                    // Pixel giống nhau: Chuyển thành màu xám nhạt mờ (để làm nền)
                    // Công thức chuyển xám: Y = 0.299R + 0.587G + 0.114B
                    byte gray = (byte)(0.299 * rA + 0.587 * gA + 0.114 * bA);
                    
                    // Tạo màu nền xám mờ nhạt
                    diffPixels[i] = (byte)Math.Min(255, gray + 40);     // Blue
                    diffPixels[i + 1] = (byte)Math.Min(255, gray + 40); // Green
                    diffPixels[i + 2] = (byte)Math.Min(255, gray + 40); // Red
                    diffPixels[i + 3] = 100;                            // Alpha (mờ)
                }
                else
                {
                    // Phát hiện khác biệt:
                    // Kiểm tra xem nét vẽ thuộc về ảnh cũ A (xóa) hay ảnh mới B (thêm)
                    // Với nền trắng bản vẽ: Nét vẽ thường có màu tối (RGB thấp)
                    // Nếu độ sáng của A tối hơn B đáng kể -> Nét cũ bị xóa
                    // Nếu độ sáng của B tối hơn A đáng kể -> Nét mới được vẽ thêm
                    int brightnessA = rA + gA + bA;
                    int brightnessB = rB + gB + bB;

                    if (brightnessA < brightnessB - 50)
                    {
                        // Nét vẽ cũ bị xóa -> Tô màu Đỏ (Red)
                        diffPixels[i] = 0;      // Blue
                        diffPixels[i + 1] = 0;  // Green
                        diffPixels[i + 2] = 230;// Red
                        diffPixels[i + 3] = 255;// Alpha
                    }
                    else if (brightnessB < brightnessA - 50)
                    {
                        // Nét vẽ mới thêm vào -> Tô màu Xanh Lá (Green)
                        diffPixels[i] = 0;      // Blue
                        diffPixels[i + 1] = 180;// Green
                        diffPixels[i + 2] = 0;  // Red
                        diffPixels[i + 3] = 255;// Alpha
                    }
                    else
                    {
                        // Thay đổi màu sắc đơn thuần -> Tô màu Xanh Dương (Blue) để báo hiệu
                        diffPixels[i] = 220;    // Blue
                        diffPixels[i + 1] = 0;  // Green
                        diffPixels[i + 2] = 0;  // Red
                        diffPixels[i + 3] = 255;// Alpha
                    }
                }
            }

            // Tạo BitmapSource mới từ mảng byte kết quả so sánh
            WriteableBitmap result = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            result.WritePixels(new Int32Rect(0, 0, width, height), diffPixels, stride, 0);
            result.Freeze();

            return result;
        }
    }
}
