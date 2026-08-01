using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PdfViewerApp;

/// <summary>
/// Centralized P/Invoke interop declarations for pdfium.dll and pdf_core.dll.
/// </summary>
public static class PdfInterop
{
    // =========================================================================
    // pdfium.dll API Declarations
    // =========================================================================
    public static class Pdfium
    {
        [DllImport("pdfium.dll")]
        public static extern void FPDF_InitLibrary();

        [DllImport("pdfium.dll")]
        public static extern void FPDF_DestroyLibrary();

        [DllImport("pdfium.dll", EntryPoint = "FPDF_LoadDocument")]
        public static extern nint FPDF_LoadDocument([MarshalAs(UnmanagedType.LPUTF8Str)] string file_path, [MarshalAs(UnmanagedType.LPUTF8Str)] string? password);

        [DllImport("pdfium.dll", EntryPoint = "FPDF_CloseDocument")]
        public static extern void FPDF_CloseDocument(nint document);

        [DllImport("pdfium.dll", EntryPoint = "FPDF_GetPageCount")]
        public static extern int FPDF_GetPageCount(nint document);

        [DllImport("pdfium.dll", EntryPoint = "FPDF_LoadPage")]
        public static extern nint FPDF_LoadPage(nint document, int page_index);

        [DllImport("pdfium.dll", EntryPoint = "FPDF_ClosePage")]
        public static extern void FPDF_ClosePage(nint page);

        [DllImport("pdfium.dll", EntryPoint = "FPDF_GetPageWidth")]
        public static extern double FPDF_GetPageWidth(nint page);

        [DllImport("pdfium.dll", EntryPoint = "FPDF_GetPageHeight")]
        public static extern double FPDF_GetPageHeight(nint page);

        [DllImport("pdfium.dll", EntryPoint = "FPDFPage_GetRotation")]
        public static extern int FPDFPage_GetRotation(nint page);

        [DllImport("pdfium.dll", EntryPoint = "FPDF_GetPageSizeByIndex")]
        public static extern int FPDF_GetPageSizeByIndex(nint document, int page_index, out double width, out double height);

        [DllImport("pdfium.dll", EntryPoint = "FPDFBitmap_CreateEx")]
        public static extern nint FPDFBitmap_CreateEx(int width, int height, int format, nint first_scan, int stride);

        [DllImport("pdfium.dll", EntryPoint = "FPDFBitmap_FillRect")]
        public static extern void FPDFBitmap_FillRect(nint bitmap, int left, int top, int width, int height, uint color);

        [DllImport("pdfium.dll", EntryPoint = "FPDF_RenderPageBitmap")]
        public static extern void FPDF_RenderPageBitmap(nint bitmap, nint page, int start_x, int start_y, int size_x, int size_y, int rotate, int flags);

        [DllImport("pdfium.dll", EntryPoint = "FPDF_RenderPage")]
        public static extern void FPDF_RenderPage(nint dc, nint page, int start_x, int start_y, int size_x, int size_y, int rotate, int flags);

        [DllImport("pdfium.dll", EntryPoint = "FPDFBitmap_Destroy")]
        public static extern void FPDFBitmap_Destroy(nint bitmap);

        [DllImport("pdfium.dll", EntryPoint = "FPDFText_LoadPage")]
        public static extern nint FPDFText_LoadPage(nint page);

        [DllImport("pdfium.dll", EntryPoint = "FPDFText_ClosePage")]
        public static extern void FPDFText_ClosePage(nint text_page);

        [DllImport("pdfium.dll", EntryPoint = "FPDFText_CountChars")]
        public static extern int FPDFText_CountChars(nint text_page);

        [DllImport("pdfium.dll", EntryPoint = "FPDFText_GetText")]
        public static extern int FPDFText_GetText(nint text_page, int start_index, int count, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder result);

        [DllImport("pdfium.dll", EntryPoint = "FPDFText_GetCharIndexAtPos")]
        public static extern int FPDFText_GetCharIndexAtPos(nint text_page, double x, double y, double xTolerance, double yTolerance);

        [DllImport("pdfium.dll", EntryPoint = "FPDFText_GetCharBox")]
        public static extern bool FPDFText_GetCharBox(nint text_page, int index, out double left, out double right, out double bottom, out double top);

        [DllImport("pdfium.dll", EntryPoint = "FPDFText_GetFontSize")]
        public static extern double FPDFText_GetFontSize(nint text_page, int index);

        [DllImport("pdfium.dll", EntryPoint = "FPDFText_GetFontInfo")]
        public static extern uint FPDFText_GetFontInfo(nint text_page, int index, nint buffer, uint buflen, out int flags);

        [DllImport("pdfium.dll", EntryPoint = "FPDFText_GetFontWeight")]
        public static extern int FPDFText_GetFontWeight(nint text_page, int index);
    }

    // =========================================================================
    // pdf_core.dll (Rust Core) API Declarations
    // =========================================================================
    public static class PdfCore
    {
        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool rotate_pdf_page(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath, 
            int pageNumber, 
            int rotationDelta, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool delete_pdf_page(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath, 
            int pageNumber, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool insert_blank_page(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath, 
            int targetPage, 
            bool insertBefore, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool reorder_pdf_pages(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string orderSemicolon, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool extract_pdf_pages(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pagesSemicolon, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool replace_pdf_text(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath, 
            int pageNumber, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string originalText, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string replacementText, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

        // Thay thế văn bản giữ nguyên font/kích thước (parse content stream, chỉ đổi chuỗi Tj/TJ).
        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool replace_text_in_page(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath, 
            int pageNumber, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string originalText, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string replacementText, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool overlay_pdf_image(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath, 
            int pageNumber, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string imagePath, 
            double x, 
            double y, 
            double width, 
            double height, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool make_pdf_searchable(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string ocrDataRaw, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool add_pdf_watermark(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
            double angle,
            double opacity,
            double fontSize,
            double r,
            double g,
            double b,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath
        );

        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool add_pdf_page_numbers(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string formatStr,
            int position,
            double fontSize,
            double r,
            double g,
            double b,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath
        );

        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int extract_pdf_images(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string outputDir
        );

        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool compress_pdf(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath,
            byte imageQuality,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath
        );

        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool optimize_pdf_lossless(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath,
            bool removeMetadata,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath
        );


        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void MergeProgressCallback(uint current, uint total);

        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool merge_pdfs(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pathsSemicolon, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool merge_pdfs_with_progress(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pathsSemicolon, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath, 
            MergeProgressCallback? progressCallback);

        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool repair_pdf(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

        [StructLayout(LayoutKind.Sequential)]
        public struct RawTextRegion
        {
            public double X;
            public double Y;
            public double Width;
            public double Height;
            public double FontSize;
            public int ObjType; // 1: Vector, 2: Subset CID, 3: Scanned OCR
        }

        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int pdf_get_page_text_objects(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath,
            int pageNum,
            [Out] RawTextRegion[] outRegions,
            int maxCount);

        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool pdf_replace_text_object(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath,
            int pageNum,
            double x,
            double y,
            double width,
            double height,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string newText,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

        [DllImport("pdf_core.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool pdf_export_to_docx(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pdfPath,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string outputDocxPath);
    }
}

