using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdfViewerApp.Services
{
    /// <summary>
    /// P/Invoke binding to the Rust Core (pdf_core.dll).
    /// Cung cấp động cơ biên tập PDF chuyên sâu kế thừa 100% từ pdfcraft:
    /// - Quản lý phiên làm việc tài liệu Document Session thuần Rust trong RAM
    /// - Sửa liên tiếp nhiều khối văn bản (block 1, block 2, block N) mượt mà
    /// - Dàn trang tự động, tạo font Type 3 vector bù tiếng Việt không nháy hình
    /// - Hỗ trợ hoàn tác / làm lại (Undo / Redo)
    /// </summary>
    public static class PdfCoreInterop
    {
        private const string DllName = "pdf_core";

        static PdfCoreInterop()
        {
            PdfInterop.EnsureInitialized();
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern bool replace_text_full(
            string pdf_path,
            string original_text,
            string replacement_text,
            string output_path);

        // -------------------------------------------------------------------
        // Động cơ Quản Lý Phiên Làm Việc (Document Session) thuần PdfCraft
        // -------------------------------------------------------------------

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong pdf_session_open([MarshalAs(UnmanagedType.LPUTF8Str)] string pdf_path);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pdf_session_get_blocks_json(ulong session_id, int page_num);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool pdf_session_commit_block(
            ulong session_id,
            int page_num,
            int block_index,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string new_text,
            double new_width);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool pdf_session_delete_block(
            ulong session_id,
            int page_num,
            int block_index);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool pdf_session_move_block(
            ulong session_id,
            int page_num,
            int block_index,
            double dx,
            double dy);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool pdf_session_export(
            ulong session_id,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string output_path,
            bool incremental);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool pdf_session_undo(ulong session_id);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool pdf_session_redo(ulong session_id);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool pdf_session_can_undo(ulong session_id);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool pdf_session_can_redo(ulong session_id);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pdf_session_close(ulong session_id);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pdf_free_string(IntPtr s);

        // -------------------------------------------------------------------
        // Standalone APIs (Tương thích ngược)
        // -------------------------------------------------------------------

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pdf_get_text_blocks_json([MarshalAs(UnmanagedType.LPUTF8Str)] string pdf_path, int page_num);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool pdf_generate_edit_preview(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pdf_path,
            int page_num,
            int block_index,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string output_path);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool pdf_commit_block_edit(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pdf_path,
            int page_num,
            int block_index,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string new_text,
            double new_width,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string output_path);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool pdf_move_block(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pdf_path,
            int page_num,
            int block_index,
            double dx,
            double dy,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string output_path);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool pdf_delete_block(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pdf_path,
            int page_num,
            int block_index,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string output_path);

        /// <summary>
        /// DTO nhận dữ liệu khối văn bản từ Rust PdfCore
        /// </summary>
        public sealed class PdfBlockDto
        {
            [JsonPropertyName("index")]
            public int Index { get; set; }

            [JsonPropertyName("text")]
            public string Text { get; set; } = "";

            [JsonPropertyName("editor_text")]
            public string EditorText { get; set; } = "";

            [JsonPropertyName("reflow_text")]
            public string ReflowText { get; set; } = "";

            [JsonPropertyName("rect")]
            public double[] Rect { get; set; } = Array.Empty<double>();

            [JsonPropertyName("font_name")]
            public string FontName { get; set; } = "";

            [JsonPropertyName("font_size")]
            public double FontSize { get; set; }

            [JsonPropertyName("bold")]
            public bool Bold { get; set; }

            [JsonPropertyName("italic")]
            public bool Italic { get; set; }

            [JsonPropertyName("color_rgb")]
            public double[] ColorRgb { get; set; } = Array.Empty<double>();

            [JsonPropertyName("rotation")]
            public double Rotation { get; set; }

            [JsonPropertyName("invisible")]
            public bool Invisible { get; set; }
        }

        /// <summary>
        /// Lấy danh sách các khối đoạn văn bản trên trang từ phiên làm việc đang mở trong RAM
        /// </summary>
        public static List<PdfBlockDto> GetSessionBlocks(ulong sessionId, int pageNum)
        {
            var result = new List<PdfBlockDto>();
            if (sessionId == 0 || pageNum < 1)
                return result;

            IntPtr ptr = pdf_session_get_blocks_json(sessionId, pageNum);
            if (ptr == IntPtr.Zero)
                return result;

            try
            {
                string? json = Marshal.PtrToStringUTF8(ptr);
                if (!string.IsNullOrEmpty(json))
                {
                    var list = JsonSerializer.Deserialize<List<PdfBlockDto>>(json);
                    if (list != null)
                        result = list;
                }
            }
            catch
            {
                // Phòng thủ khi JSON parse lỗi
            }
            finally
            {
                pdf_free_string(ptr);
            }

            return result;
        }

        /// <summary>
        /// Lấy danh sách các khối đoạn văn bản trên trang trực tiếp từ tệp PDF trên đĩa
        /// </summary>
        public static List<PdfBlockDto> GetTextBlocks(string pdfPath, int pageNum)
        {
            var result = new List<PdfBlockDto>();
            if (string.IsNullOrEmpty(pdfPath) || !File.Exists(pdfPath) || pageNum < 1)
                return result;

            IntPtr ptr = pdf_get_text_blocks_json(pdfPath, pageNum);
            if (ptr == IntPtr.Zero)
                return result;

            try
            {
                string? json = Marshal.PtrToStringUTF8(ptr);
                if (!string.IsNullOrEmpty(json))
                {
                    var list = JsonSerializer.Deserialize<List<PdfBlockDto>>(json);
                    if (list != null)
                        result = list;
                }
            }
            catch
            {
                // Phòng thủ khi JSON parse lỗi
            }
            finally
            {
                pdf_free_string(ptr);
            }

            return result;
        }
    }
}
