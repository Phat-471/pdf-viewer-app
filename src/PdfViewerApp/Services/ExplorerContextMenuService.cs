using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace PdfViewerApp;

internal static class ExplorerContextMenuService
{
    // Only repair labels of known entries. Commands and other apps are preserved.
    internal static void RepairLabels()
    {
        RepairLabel("PdfPro.Merge", "Gh\u00e9p PDF b\u1eb1ng PDF HPhat");
        RepairLabel("PDFHP.Merge", "Gh\u00e9p file b\u1eb1ng PDF HP");
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
}
