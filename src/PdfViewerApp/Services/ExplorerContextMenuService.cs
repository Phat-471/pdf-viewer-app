using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using PdfViewerApp.Services;

namespace PdfViewerApp;

internal static class ExplorerContextMenuService
{
    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    internal static void RepairLabels()
    {
        RepairLabel("PdfPro.Merge", "Gh\u00e9p PDF b\u1eb1ng PDF HPhat");
        RepairLabel("PDFHP.Merge", "Gh\u00e9p file b\u1eb1ng PDF HP");
        RegisterShellAssociations();
    }

    private static void RepairLabel(string verb, string label)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Classes\SystemFileAssociations\.pdf\shell\" + verb, writable: true);
            if (key != null && !string.Equals(key.GetValue("MUIVerb") as string, label, StringComparison.Ordinal))
                key.SetValue("MUIVerb", label, RegistryValueKind.String);
        }
        catch (Exception ex)
        {
            Trace.WriteLine("Cannot repair PDF context menu label: " + ex.Message);
        }
    }

    internal static void RegisterShellAssociations()
    {
        try
        {
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                exePath = Process.GetCurrentProcess().MainModule?.FileName;
            }
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                return;
            }

            if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                exePath = Path.ChangeExtension(exePath, ".exe");
            }
            if (!File.Exists(exePath))
            {
                return;
            }

            string exeName = Path.GetFileName(exePath);
            string openCommand = $"\"{exePath}\" \"%1\"";
            string openLabel = "M\u1edf b\u1eb1ng PDF Pro";

            // 1. Applications\{exeName}
            using (var appKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\Applications\{exeName}"))
            {
                appKey?.SetValue("FriendlyAppName", "PDF Pro - HPhat Edition", RegistryValueKind.String);
            }
            using (var cmdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\Applications\{exeName}\shell\open\command"))
            {
                cmdKey?.SetValue("", openCommand, RegistryValueKind.String);
            }
            using (var suppKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\Applications\{exeName}\SupportedTypes"))
            {
                if (suppKey != null)
                {
                    suppKey.SetValue(".pdf", "", RegistryValueKind.String);
                    foreach (var ext in ImageToPdfService.SupportedImageExtensions)
                    {
                        suppKey.SetValue(ext, "", RegistryValueKind.String);
                    }
                }
            }

            // 2. ProgID: PDFPro.Image
            using (var progId = Registry.CurrentUser.CreateSubKey(@"Software\Classes\PDFPro.Image"))
            {
                progId?.SetValue("", "T\u1ec7p h\u00ecnh \u1ea3nh (PDF Pro)", RegistryValueKind.String);
            }
            using (var iconKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\PDFPro.Image\DefaultIcon"))
            {
                iconKey?.SetValue("", $"{exePath},0", RegistryValueKind.String);
            }
            using (var openCmd = Registry.CurrentUser.CreateSubKey(@"Software\Classes\PDFPro.Image\shell\open\command"))
            {
                openCmd?.SetValue("", openCommand, RegistryValueKind.String);
            }

            // 3. SystemFileAssociations for generic image class
            using (var imgShell = Registry.CurrentUser.CreateSubKey(@"Software\Classes\SystemFileAssociations\image\shell\PdfPro.Open"))
            {
                imgShell?.SetValue("MUIVerb", openLabel, RegistryValueKind.String);
                imgShell?.SetValue("Icon", $"\"{exePath}\"", RegistryValueKind.String);
            }
            using (var imgCmd = Registry.CurrentUser.CreateSubKey(@"Software\Classes\SystemFileAssociations\image\shell\PdfPro.Open\command"))
            {
                imgCmd?.SetValue("", openCommand, RegistryValueKind.String);
            }

            // 4. Per-extension associations
            foreach (var ext in ImageToPdfService.SupportedImageExtensions)
            {
                using (var owl = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ext}\OpenWithList\{exeName}")) { }
                using (var owp = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ext}\OpenWithProgids"))
                {
                    owp?.SetValue("PDFPro.Image", "", RegistryValueKind.String);
                }
                using (var extShell = Registry.CurrentUser.CreateSubKey($@"Software\Classes\SystemFileAssociations\{ext}\shell\PdfPro.Open"))
                {
                    extShell?.SetValue("MUIVerb", openLabel, RegistryValueKind.String);
                    extShell?.SetValue("Icon", $"\"{exePath}\"", RegistryValueKind.String);
                }
                using (var extCmd = Registry.CurrentUser.CreateSubKey($@"Software\Classes\SystemFileAssociations\{ext}\shell\PdfPro.Open\command"))
                {
                    extCmd?.SetValue("", openCommand, RegistryValueKind.String);
                }
            }

            // 5. Capabilities registration
            using (var cap = Registry.CurrentUser.CreateSubKey(@"Software\PDFPro\Capabilities"))
            {
                cap?.SetValue("ApplicationName", "PDF Pro", RegistryValueKind.String);
                cap?.SetValue("ApplicationDescription", "B\u1ed9 c\u00f4ng c\u1ee5 xem v\u00e0 x\u1eed l\u00fd PDF, h\u00ecnh \u1ea3nh chuy\u00ean nghi\u1ec7p", RegistryValueKind.String);
            }
            using (var capAssoc = Registry.CurrentUser.CreateSubKey(@"Software\PDFPro\Capabilities\FileAssociations"))
            {
                if (capAssoc != null)
                {
                    capAssoc.SetValue(".pdf", "PDFPro.PDF", RegistryValueKind.String);
                    foreach (var ext in ImageToPdfService.SupportedImageExtensions)
                    {
                        capAssoc.SetValue(ext, "PDFPro.Image", RegistryValueKind.String);
                    }
                }
            }
            using (var regApp = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            {
                regApp?.SetValue("PDF Pro", @"Software\PDFPro\Capabilities", RegistryValueKind.String);
            }

            // Notify Explorer of shell associations update
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            Trace.WriteLine("Cannot register shell associations: " + ex.Message);
        }
    }
}
