# Source Restore Status

Date: 2026-06-06

## Current State

The current workspace `E:\code\pdf` does not contain the app source folders required by the build scripts:

- `src\PdfViewerApp`
- `src\PdfViewerApp\PdfViewerApp.csproj`
- `src\PdfCore`
- `src\PdfCore\Cargo.toml`

Because these folders are missing, a new desktop app build cannot be produced safely from source.

## Searches Already Performed

Checked direct source markers:

- `PdfViewerApp.csproj`
- `PdfDocumentTab.xaml.cs`
- `PdfCore\Cargo.toml`
- `Cargo.toml` under PDF-related paths

Locations checked:

- `E:\code`
- `C:\Users\IT\Desktop`
- `C:\Users\IT\Documents`
- `C:\Users\IT\Downloads`
- `F:\2026\D\hungphat`
- `F:\2026\2026\D`

Result: no current WPF/Rust `PdfViewerApp` source was found.

## Archive Check

Found archive:

- `E:\code\pdf.rar`

Archive contents were inspected with `tar -tf`.

Result:

- Contains old Flutter project files.
- Contains `pdf/native/Cargo.toml` and `pdf/rust/Cargo.toml`.
- Does not contain `PdfViewerApp.csproj`, `PdfDocumentTab.xaml.cs`, or `src\PdfViewerApp`.

## Current Safe Candidate

Existing update candidate ZIP:

- `E:\code\pdf\PdfViewerApp_v1.0.9.zip`

Smoke test result:

- Extracted successfully.
- Contains `PdfViewerApp.exe`, `pdf_core.dll`, and `pdfium.dll`.
- App stayed open for the startup smoke test and closed successfully.

This confirms the old binary package can run, but it does not replace the missing source.

## Next Required Action

Recover or provide the real source folders:

- `src\PdfViewerApp`
- `src\PdfCore`

After source is restored, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\backup_source_snapshot.ps1
powershell -ExecutionPolicy Bypass -File .\tools\install_app_update_client.ps1
powershell -ExecutionPolicy Bypass -File .\tools\release.ps1 -Part patch
```
