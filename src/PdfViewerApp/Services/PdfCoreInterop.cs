using System;
using System.Runtime.InteropServices;

namespace PdfViewerApp.Services
{
    /// <summary>
    /// P/Invoke binding to the Rust Core (pdf_core.dll).
    /// Provides low-level PDF operations implemented in Rust.
    /// </summary>
    public static class PdfCoreInterop
    {
        private const string DllName = "pdf_core";

        static PdfCoreInterop()
        {
            // Ensure the native library can be resolved from the application base directory.
                NativeLibrary.SetDllImportResolver(typeof(PdfCoreInterop).Assembly, (name, assembly, path) =>
            {
                if (name != DllName && name != DllName + ".dll")
                    return IntPtr.Zero;

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string candidate = System.IO.Path.Combine(baseDir, "pdf_core.dll");
                if (System.IO.File.Exists(candidate) &&
                    NativeLibrary.TryLoad(candidate, out IntPtr handle))
                {
                    return handle;
                }
                return IntPtr.Zero;
            });
        }

        /// <summary>
        /// Replace every occurrence of <paramref name="originalText"/> with
        /// <paramref name="replacementText"/> across the ENTIRE document.
        /// Font, font size, color and fill are preserved. When the new text is
        /// longer/shorter, spacing is automatically adjusted on the same line (reflow).
        /// </summary>
        /// <returns>true if at least one replacement was made and saved.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern bool replace_text_full(
            string pdf_path,
            string original_text,
            string replacement_text,
            string output_path);
    }
}
