using System;
using System.IO;
using System.Linq;
using Xunit;
using PdfViewerApp.Services;

namespace PdfViewerApp.Tests
{
    public class PdfCraftTextEditorTests
    {
        [Fact]
        public void EditMultipleBlocksConsecutively_ShouldSucceedWithoutErrors()
        {
            string pdfPath = @"E:\code\03_Desktop_Apps\pdf_pro\TAKAMI CATALOG 2025.pdf";
            if (!File.Exists(pdfPath)) return;

            string tempFile = Path.Combine(Path.GetTempPath(), $"test-edit-{Guid.NewGuid():N}.pdf");
            File.Copy(pdfPath, tempFile, true);

            try
            {
                // 1. Mở phiên làm việc PdfCraft
                ulong sessionId = PdfCoreInterop.pdf_session_open(tempFile);
                Assert.True(sessionId > 0, "Phiên làm việc phải mở thành công.");

                try
                {
                    // Tìm một trang có nhiều blocks độc lập
                    int targetPage = 1;
                    var blocks = PdfCoreInterop.GetSessionBlocks(sessionId, targetPage);
                    for (int p = 1; p <= 10; p++)
                    {
                        var b = PdfCoreInterop.GetSessionBlocks(sessionId, p);
                        if (b.Count >= 3)
                        {
                            targetPage = p;
                            blocks = b;
                            break;
                        }
                    }

                    Assert.NotEmpty(blocks);

                    // Sửa khối thứ nhất
                    bool edit1 = PdfCoreInterop.pdf_session_commit_block(sessionId, targetPage, 0, "Chỗ 1 đã được sửa thành công", 0);
                    Assert.True(edit1, "Sửa chỗ 1 phải thành công.");

                    // Lấy lại danh sách blocks mới nhất sau khi sửa chỗ 1
                    var blocksAfter1 = PdfCoreInterop.GetSessionBlocks(sessionId, targetPage);
                    Assert.NotEmpty(blocksAfter1);

                    // Sửa khối tiếp theo nếu có (Chỗ thứ 2 - kiểm chứng lỗi trước đây)
                    if (blocksAfter1.Count > 1)
                    {
                        int nextIndex = Math.Min(1, blocksAfter1.Count - 1);
                        bool edit2 = PdfCoreInterop.pdf_session_commit_block(sessionId, targetPage, nextIndex, "Chỗ 2 đã sửa mượt mà liên tiếp!", 0);
                        Assert.True(edit2, "Sửa chỗ thứ 2 PHẢI thành công (không còn lỗi không sửa được chỗ khác).");

                        // Lấy lại danh sách blocks mới nhất sau khi sửa chỗ 2
                        var blocksAfter2 = PdfCoreInterop.GetSessionBlocks(sessionId, targetPage);

                        // Sửa khối thứ 3 nếu có
                        if (blocksAfter2.Count > 2)
                        {
                            int thirdIndex = Math.Min(2, blocksAfter2.Count - 1);
                            bool edit3 = PdfCoreInterop.pdf_session_commit_block(sessionId, targetPage, thirdIndex, "Chỗ 3 tiếp tục sửa hoàn hảo", 0);
                            Assert.True(edit3, "Sửa chỗ thứ 3 liên tiếp phải thành công.");
                        }
                    }

                    // Xuất ra file PDF kết quả
                    string exportedFile = Path.Combine(Path.GetTempPath(), $"test-exported-{Guid.NewGuid():N}.pdf");
                    bool exported = PdfCoreInterop.pdf_session_export(sessionId, exportedFile, false);
                    Assert.True(exported, "Xuất file PDF phải thành công.");
                    Assert.True(File.Exists(exportedFile) && new FileInfo(exportedFile).Length > 0);

                    try { File.Delete(exportedFile); } catch { }
                }
                finally
                {
                    PdfCoreInterop.pdf_session_close(sessionId);
                }
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }
}
