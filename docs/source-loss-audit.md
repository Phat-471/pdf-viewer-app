# Source Loss Audit

Date: 2026-06-06

## Summary

The source folders required to build the current WPF/Rust app are missing from `E:\code\pdf`:

- `E:\code\pdf\src\PdfViewerApp`
- `E:\code\pdf\src\PdfCore`

PowerShell history shows these paths existed or were expected previously:

- `dotnet build src/PdfViewerApp/PdfViewerApp.csproj -c Release`
- `dotnet run --project src/PdfViewerApp -c Release`
- `e:\code\pdf\src\PdfCore\Cargo.toml`

## Current Evidence

Current `E:\code\pdf` contains:

- release ZIPs
- `_smoke` binary output folders
- `libs`
- `tools`
- WordPress plugin files
- README/docs

Current `E:\code\pdf` does not contain:

- source `.cs` files
- `PdfViewerApp.csproj`
- `src\PdfCore\Cargo.toml`
- `PdfDocumentTab.xaml.cs`

## Recycle Bin Check

The Windows Recycle Bin was checked through Shell COM namespace for names matching:

- `src`
- `PdfViewerApp`
- `PdfCore`
- `pdf`

No matching item was returned.

## Archive Check

`E:\code\pdf.rar` was inspected with:

```powershell
tar -tf E:\code\pdf.rar
```

Result:

- It contains an older Flutter project.
- It contains `pdf/native/Cargo.toml` and `pdf/rust/Cargo.toml`.
- It does not contain `PdfViewerApp.csproj`, `PdfDocumentTab.xaml.cs`, or `src\PdfViewerApp`.

## Commands With Recursive Delete Found In Workspace

The current workspace contains some scripts with `Remove-Item -Recurse`, but the relevant targets are:

- `_smoke\UpdateCandidate_*` in `tools\smoke_test_update_candidate.ps1`
- temp backup folder in `tools\backup_source_snapshot.ps1`
- configured publish output directory in `tools\publish_single_exe.ps1`
- registry keys in uninstall scripts

No current script search found a `Remove-Item` target directly aimed at `E:\code\pdf\src`.

## Recovery Options

1. Run an elevated Administrator shell and check Volume Shadow Copy:

```powershell
vssadmin list shadows
```

2. If shadow copies exist, restore `E:\code\pdf\src` from a previous version.

3. If no filesystem snapshot exists, recover approximate C# source from:

- `E:\code\pdf\PdfViewerApp_v1.0.9.zip`
- `E:\code\pdf\_smoke\UpdateCandidate_PdfViewerApp_v1.0.9\PdfViewerApp.dll`
- `E:\code\pdf\_smoke\UpdateCandidate_PdfViewerApp_v1.0.9\PdfViewerApp.pdb`

4. Rust source cannot be accurately reconstructed from `pdf_core.dll`; it must be restored from backup, archive, Git, or rewritten from behavior.
