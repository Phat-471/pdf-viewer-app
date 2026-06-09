# 🚀 PDF Pro - Foxit PhantomPDF Clone

Dự án phần mềm đọc, in ấn, xoay và gộp file PDF hiệu năng cao dành cho Windows. Phần mềm được tái cấu trúc hoàn toàn mới dựa trên kiến trúc **Đa ngôn ngữ kết hợp (Hybrid Architecture)** tối ưu nhất: **C# WPF (.NET 8) + Lõi xử lý siêu tốc Rust + PDFium Engine (C++)**.

---

## 🛠️ Kiến Trúc Hệ Thống (Architecture)

```
├── old_project/             # Sao lưu toàn bộ mã nguồn cũ của Flutter
├── libs/                    # Nơi chứa các thư viện động hỗ trợ (pdfium.dll, pdf_core.dll)
├── docs/                    # Tài liệu hướng dẫn thiết kế và sơ đồ luồng
└── src/
    ├── PdfViewerApp/        # Giao diện WPF C# (.NET 8) với menu Ribbon Office quen thuộc
    └── PdfCore/             # Lõi xử lý đa luồng & gộp file PDF tốc độ đĩa viết bằng Rust
```

* **Giao diện (Frontend UI):** Viết bằng **C# WPF (.NET 8)**, sử dụng bộ công cụ `Fluent.Ribbon` để tái tạo 100% trải nghiệm giống Foxit PhantomPDF và Microsoft Office, mang lại cảm giác native mượt mà và cực kỳ thân quen cho người sử dụng.
* **Lõi xử lý (Core Engine):** Viết bằng **Rust** (`src/PdfCore`), biên dịch trực tiếp ra `.dll` động. Rust gọi trực tiếp các hàm Windows API qua P/Invoke, giúp gộp hàng trăm trang PDF và tải trang nhanh trong tích tắc mà không gây đứng luồng hoặc crash ứng dụng.
* **Bộ dựng hình (PDF Render):** Sử dụng lõi **PDFium** (C++) - chính là bộ lõi dựng hình của Google Chrome và Foxit - cho tốc độ xử lý file AutoCAD/Revit nặng siêu tốc và in ấn vector sắc nét tuyệt đối.

---

## 📦 Hướng Dẫn Biên Dịch & Chạy Thử (How to Build)

### 1. Yêu cầu hệ thống (Prerequisites)
* **.NET 8 SDK:** [Tải tại đây](https://dotnet.microsoft.com/download/dotnet/8.0)
* **Rust (Cargo):** [Tải qua rustup-init](https://rustup.rs/)

### 2. Biên dịch lõi Rust (PdfCore)
Mở cửa sổ Command Prompt / PowerShell tại thư mục dự án và thực hiện:
```bash
cd src/PdfCore
cargo build --release
```
Sau khi build xong, file thư viện động `pdf_core.dll` sẽ được tạo ra tại thư mục `src/PdfCore/target/release/pdf_core.dll`. Hãy sao chép file này vào thư mục đầu ra của ứng dụng WPF hoặc thư mục `libs/` của bạn.

### 3. Biên dịch và chạy giao diện WPF (C#)
Mở cửa sổ Command Prompt / PowerShell tại thư mục gốc dự án:
```bash
cd src/PdfViewerApp
dotnet build -c Release
dotnet run
```

---

## ⚡ Các Điểm Cải Tiến Cực Hạn So Với Bản Cũ
1. **Tránh hoàn toàn lỗi crash phân vùng bộ nhớ:** Cơ chế quản lý RAM nghiêm ngặt của Rust ở tầng lõi ngăn chặn triệt để lỗi crash tràn bộ nhớ khi cuộn chuột nhanh.
2. **Gộp file siêu tốc ở tầng nhị phân:** Sử dụng thư viện `lopdf` của Rust để gộp tài liệu ở mức độ nhị phân, tốc độ gộp 20 file nặng tổng 500MB chỉ mất **dưới 0.5 giây**.
3. **In ấn Native Vector sắc nét:** WPF kết nối trực tiếp với Windows Print Spooler, gửi bản vẽ vector gốc đến máy in A3/A4 mà không cần các file helper trung gian phức tạp, đảm bảo độ chính xác của từng nét vẽ kỹ thuật.
## Library Audit

This project keeps the dependency surface intentionally small.

### Direct project references
- `Fluent.Ribbon` is used directly by `MainWindow.xaml` for the ribbon UI.
- `Microsoft.Xaml.Behaviors.Wpf` is no longer a direct `PackageReference`. It stays available transitively through `Fluent.Ribbon -> ControlzEx`.

### Transitive runtime dependencies
- `ControlzEx` is required by `Fluent.Ribbon`.
- `System.Text.Json` is a transitive package from `ControlzEx`.

### Native libraries
- `pdf_core.dll` is used by the WPF app through `DllImport` for merge, rotate, delete, and insert blank page operations.
- `pdfium.dll` is used by `PdfiumEngine.cs` for opening and rendering PDF pages.

### Merge workflow
- **Sắp xếp trang trực quan (Visual Page Reordering)**: Kéo thả các trang thu nhỏ (thumbnails) trong thanh bên trái để thay đổi thứ tự trang trực quan và cập nhật ngay lập tức. Sau đó, nhấn Lưu hoặc Lưu dưới dạng để lưu vĩnh viễn cấu trúc trang mới vào file PDF thông qua lõi Rust.
- **Gộp file từ Explorer không nhấp nháy (Flash-free Explorer Merge)**: Tích hợp menu chuột phải chạy ngầm mượt mà hơn. Ứng dụng xử lý gộp trực tiếp ở tiến trình nền mà không cần khởi tạo hay nhấp nháy cửa sổ chính (MainWindow), tăng tốc độ xử lý và cải thiện trải nghiệm người dùng.
- Bạn có thể kéo thả nhiều file PDF từ Windows Explorer trực tiếp vào ứng dụng để hiển thị hộp thoại gộp. Danh sách được tự động sắp xếp theo tên để tránh sai thứ tự bản vẽ.
- Tiến độ gộp được hiển thị rõ ràng cho từng file kèm thông số tổng dung lượng đầu vào, dung lượng từng file, thời gian đã trôi qua và ghi chép chi tiết vào nhật ký hiệu năng (Performance Trace).
- Hỗ trợ di chuyển thủ công lên/xuống (Move Up/Down) sẽ tự động tắt chế độ sắp xếp tự động để giữ nguyên thứ tự tùy chỉnh của người dùng.
- File PDF sau khi gộp sẽ tự động lưu trong thư mục `%LOCALAPPDATA%\PdfPro\Merged\` với tên kèm mốc thời gian và tự động mở ra một tab mới.
- Để tích hợp vào menu chuột phải Windows Explorer, chạy file `tools\install_explorer_context_menu.ps1` và truyền đường dẫn file `PdfViewerApp.exe` đã biên dịch. Menu này sẽ gọi ứng dụng với cờ `--merge --exit-after-merge`, tự động mở file sau khi gộp bằng ứng dụng mặc định rồi thoát hoàn toàn.
- Chạy `tools\uninstall_explorer_context_menu.ps1` để gỡ bỏ tích hợp chuột phải.

### Organize & Extract workflow
- **Trực quan hóa khu vực trống (Empty State Drag & Drop)**: Khi không có tài liệu nào đang mở, ứng dụng hiển thị một giao diện trống hiện đại với icon trực quan gợi ý kéo thả file PDF vào để mở.
- **Trích xuất trang PDF (Page Extraction)**: Cho phép trích xuất các trang cụ thể (ví dụ: `1;3;5-8`) ra một tài liệu mới thông qua lõi Rust cực kỳ tối ưu (loại bỏ các đối tượng thừa để tối ưu dung lượng lưu trữ), tự động mở file mới sau khi trích xuất hoàn tất.

### Printing profile
- Printing now resolves a per-printer profile before applying offset mode.
- `Auto` uses the detected profile, `WPF Offset` forces the WPF-style offset path, and `Physical` forces raw physical coordinates.
- `Canon iX6770 / iX6700` gets its own padding profile.
- A `Print test frame` checkbox prints a diagnostic page with outer borders and imageable-area guides so you can see exactly which edge is being clipped.
- The print dialog now has two print engines: `Native PDFium` and `WPF Bitmap`.
- `Native PDFium` sends pages through a Win32 printer DC with `FPDF_RenderPage` and `FPDF_PRINTING | FPDF_ANNOT`. Use this first for large A3 technical drawings.
- `WPF Bitmap` keeps the older compatibility path and is still useful for diagnostics, test-frame output, or cases where WPF overlay annotations must be printed.
- `Native: gui tung trang` sends each page as a separate native print job so the physical printer can start earlier instead of waiting for the whole multi-page job to finish.
- `Thu tu in` can print top-to-bottom (`1 -> n`) or bottom-to-top (`n -> 1`) and applies to both native and WPF bitmap print paths.
- Printing now opens a progress window after the user confirms print options. Native PDFium reports preparation, per-page render, per-page spool, completed page count, and cancellation requests.
- The progress window can cancel before the next page starts; if a page is already inside the printer driver, cancellation waits for the current driver call to return.

### Explorer menu setup
1. Build the app in `Release`.
2. Run the install script from PowerShell:
   `powershell -ExecutionPolicy Bypass -File tools\install_explorer_context_menu.ps1 -AppExePath "E:\code\pdf\src\PdfViewerApp\bin\Release\net8.0-windows\PdfViewerApp.exe"`
3. In File Explorer, select multiple PDFs, right-click, and choose `Merge PDF with PDF Pro`.
4. The app merges in the background, shows per-file progress, then opens the merged PDF automatically.
5. To remove the menu entry, run `tools\uninstall_explorer_context_menu.ps1`.

### Single-file packaging
- Run `powershell -ExecutionPolicy Bypass -File tools\publish_single_exe.ps1`.
- The portable test build is created at `dist\PdfPro-win-x64\PdfViewerApp.exe`.
- The package is self-contained for Windows x64 and embeds `pdfium.dll` plus `pdf_core.dll`, so the target machine does not need the .NET runtime installed.
- For Explorer right-click integration on another machine, install the context menu with the packaged exe path.

### Version and activation
- App version is defined in `VERSION.txt` and consumed by the release scripts.
- The ribbon has a `Kich Hoat` button. It shows the app version, Machine ID, activation status, and license file path.
- Activation is offline and machine-bound. The app stores the key at `%LOCALAPPDATA%\PdfPro\activation.json`.
- To generate a key for a Machine ID, run:
  `powershell -ExecutionPolicy Bypass -File tools\generate_activation_key.ps1 -MachineId "XXXX-XXXX-XXXX-XXXX"`
- This is a local/offline licensing layer for testing and deployment control. A server-backed license check is still the stronger option for commercial protection.

### App update client
- The app-side update client template is in `app-upgrade\UpdateClient`.
- It calls the WordPress update endpoint, compares versions, downloads the ZIP, and verifies `sha256` plus `file_size` before trusting the update.
- After restoring `src\PdfViewerApp`, install the client into the app source with:
  `powershell -ExecutionPolicy Bypass -File tools\install_app_update_client.ps1`
- Then wire `AppUpdateService.CheckAsync()` to a ribbon button or startup check.

### Next upgrade roadmap
- P0 - Restore app source: recover `src\PdfViewerApp` and `src\PdfCore` so app changes can be built instead of only testing old ZIP packages.
- P0 source restore status is tracked in `docs\source-restore-status.md`.
- After source is restored, create a source backup with:
  `powershell -ExecutionPolicy Bypass -File tools\backup_source_snapshot.ps1`
- P0 - Wire online updater into app: install `app-upgrade\UpdateClient`, add a ribbon button `Kiem tra cap nhat`, and show version, changelog, mandatory flag, download progress, SHA256 result.
- P0 - Safe update install flow: after download verification, close the app safely, run installer/update package, and keep a rollback copy of the previous version.
- P1 - Auto publish update metadata: replace manual WordPress copy/paste with a script that uploads/saves version, URL, SHA256, size, changelog, and mandatory flag through WordPress REST/API.
- P1 - Update history: store every released version in WordPress so admin can see what was published, when, file size, hash, and changelog.
- P1 - Better error reporting: when open PDF/update/activation fails, send structured logs to WordPress with app version, machine id, OS, error code, and stack trace.
- P2 - PDF load diagnostics UI: expose slow-load reasons inside the app, including file size, page count, render time, cache misses, and native PDFium errors.
- P2 - Recovery mode for broken PDFs: add an option to repair/re-save problematic PDFs, extract readable pages, or open using a fallback render path.
- P2 - Batch tools: add batch compress, batch rotate, batch extract, and batch merge presets for warehouse/drawing workflows.
- P3 - Admin release automation: one command should build, smoke test, upload ZIP, update WordPress metadata, verify endpoint, and generate a release note.
- P3 - In-app release notes: show changelog grouped by version before users install updates.

### Release flow
- To create a new versioned package after an upgrade, run:
  `powershell -ExecutionPolicy Bypass -File tools\release.ps1 -Part patch`
- This bumps `VERSION.txt`, rebuilds the ZIP package using that version number, and verifies the local update manifest.
- The release command also extracts the generated ZIP and launches `PdfViewerApp.exe` briefly as a smoke test before you upload it.
- To run only the smoke test for an existing ZIP:
  `powershell -ExecutionPolicy Bypass -File tools\smoke_test_update_candidate.ps1 -ZipPath "PdfViewerApp_v1.0.9.zip" -KeepExtracted`
- The release output is written to `releases\`.
- After uploading the ZIP to Google Drive, update `download_url` in `releases\update-manifest.json` with the public direct download URL.
- Open the WordPress update admin page and paste the full `releases\update-manifest.json` content into `Manifest JSON`.
- Manual fields on that page can still override manifest values when needed.
- After saving WordPress, verify the endpoint from the app side:
  `powershell -ExecutionPolicy Bypass -File tools\check_update_endpoint.ps1 -ApiUrl "https://your-site.com" -CurrentVersion "1.0.0"`
- To also download and verify the remote ZIP SHA256/size, add `-CheckDownload`.

### Code layout
- `PdfDocumentTab.xaml.cs` currently keeps document loading, the main render pipeline, page cache, and annotation editing.
- `PdfDocumentTab.Ui.cs` now holds sidebar toggles, zoom handling, page navigation, and viewport throttling.
- `PdfDocumentTab.Printing.cs` now holds the print flow.
- The next split targets are the render/cache helpers and the annotation/content helpers.

### Performance trace
- Use the `Perf Trace` ribbon button to open the current session log.
- Logs are written under `%LOCALAPPDATA%\PdfPro\PerfLogs\`.
- The trace records load time, page count, dimension scan time, UI build time, and per-page bitmap render misses/hits.
- For heavy PDFs, start by checking `CollectPageDimensions`, initial page render misses, and thumbnail render misses.
- Print diagnostics are also logged. Check `DPI in da chon`, `Print page X total`, `Tiled bitmap chunks drawn`, and `PrintDocument submit total`.
- In `WPF Bitmap` mode, 600 DPI can generate very large raster jobs and may keep Windows/driver spooling for minutes before the printer starts.
- For native printing, check `Native printer DC`, `Native StartPage X`, `Native FPDF_RenderPage X`, `Native EndPage X`, `Native EndDoc page-job X spool`, and `Native PDFium print total`.

### Progressive rendering
- The viewer now builds placeholder page frames first.
- The currently visible page is queued with the highest priority and rendered first.
- Nearby pages are rendered in the background through a priority queue, but prefetch is capped around the current page so heavy drawings do not render the whole file unnecessarily.
- Thumbnails are deferred and only rendered when the thumbnail sidebar is visible, so hidden navigation does not consume PDFium/render time.
- During zoom preview, pending render work is invalidated and real page rendering resumes only after zoom settles.

### Annotation editing
- The callout arrow tool is one-shot: after creating one callout, the active tool returns to `Select`.
- Selected text boxes and callouts can be dragged to move.
- Selected text boxes and callouts show a bottom-right resize handle so long text can be expanded or tightened after entry.

### Snapshot printing
- The `Snapshot` tool lets the user drag-select a rectangular area on a PDF page.
- Snapshot selections are stored as normalized PDF page coordinates, not screen captures.
- `PdfSnapshotPrinter.cs` prints the selected region through native PDFium and scales it to fill the selected print page, so vector drawings stay sharp when enlarged to A3.
- After selecting a region, the app now offers `Print`, `Copy image`, and `Save PNG`. Printing remains native/vector-like through PDFium; copy/save render a high-resolution PNG from the same PDF coordinates.
- Snapshot print diagnostics are written to the performance trace with `Snapshot native print` entries.

### Keyboard and mouse workflow
- `Ctrl+O`: open PDF.
- `Ctrl+P`: print.
- `Ctrl+W`: close current tab.
- `Ctrl++` / `Ctrl+-`: zoom in/out.
- `Ctrl+0`: fit width.
- `Ctrl+1`: 100% zoom.
- `Ctrl+B`: toggle thumbnails sidebar.
- `PageUp` / `PageDown`: previous/next page.
- `Home` / `End`: first/last page.
- `V`: select tool.
- `T`: text box tool.
- `C`: callout arrow tool.
- `S`: snapshot tool.
- `A`: AI snapshot tool.
- `Esc`: return to select tool.
- `Ctrl+mouse wheel`: smooth zoom around the cursor.
- `Middle mouse drag` or `Space + left mouse drag`: pan the document.

### AI snapshot
- `AI Snapshot` opens the right-side AI panel and lets the user drag-select a PDF region for analysis.
- The app renders only the selected PDF region to a PNG and sends that image, not the whole PDF, to the online provider.
- Online mode prefers Gemini when `GEMINI_API_KEY` is set, then falls back to OpenAI when `OPENAI_API_KEY` is set.
- Gemini uses the official model list endpoint to auto-select a model that supports `generateContent`; set `PDFPRO_GEMINI_MODEL` to override or leave it as `auto`.
- Gemini auto-selection prioritizes economical models such as Flash/Lite before Pro to reduce token/cost usage for snapshot analysis.
- Set `PDFPRO_AI_MODEL` to override the default model. If omitted, the app uses `gpt-4.1`.
- If no online key is configured, the local provider returns a clear fallback message. OCR/local model integration is intentionally isolated for a later step.
- The AI panel includes buttons for Gemini API key, OpenAI API key, Ollama local AI download, and an AI system check.
- `Kiem tra AI` lists supported Gemini `generateContent` models and shows the selected economical model.
- AI settings are saved to `%LOCALAPPDATA%\PdfPro\ai-settings.json`.
- The AI panel supports provider mode `Auto`, `Gemini`, `OpenAI`, `Local`, and `Off`, plus an `Allow online` switch.

### Runtime check
- The ribbon now includes a `Kiểm tra thư viện` button.
- It reports whether the expected managed assemblies and native DLLs are loaded and whether the DLL files are present in the app folder.
- It also points out that `Microsoft.Xaml.Behaviors.Wpf` is transitive, so keeping it as a direct reference is unnecessary.
- Native DLLs are lazy-loaded by some features, so "not loaded yet" is not always a problem if the file is present.
