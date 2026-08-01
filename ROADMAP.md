# 🗺️ DỰ ÁN PDF VIEWER PRO - ROADMAP & SƠ ĐỒ MÃ NGUỒN

Tài liệu này giúp tra cứu nhanh cấu trúc dự án, vị trí các tệp nguồn, các mô-đun cốt lõi và lộ trình phát triển. Khi cần sửa lỗi hoặc phát triển tính năng mới, **chỉ cần xem tệp này** mà không cần phải đọc lại toàn bộ codebase.

---

## 📐 1. CẤU TRÚC TỔNG QUAN HỆ THỐNG

Dự án gồm **2 phần chính**:
1. **Core C# WPF (`src/PdfViewerApp`)**: Giao diện Fluent Ribbon, xử lý Tab, In ấn, Preview, Chú thích, OCR UI.
2. **Core Rust (`src/PdfCore`)**: Thao tác PDF tốc độ cao (Pdfium wrapper, sửa PDF stream, ghép/tách PDF, OCR backend, rendering).

```
e:\code\pdf
├── build_project.ps1               # Script biên dịch tự động toàn bộ (Rust -> DLL -> WPF)
├── ROADMAP.md                      # Tệp bản đồ dự án (Tệp hiện tại)
├── src
│   ├── PdfCore                     # [RUST CORE] Thư viện xử lý PDF gốc
│   │   ├── Cargo.toml
│   │   └── src
│   │       ├── lib.rs              # Export FFI C-bindings cho C# (.dll) & các hàm xử lý stream
│   │       └── ...
│   ├── PdfViewerApp                # [C# WPF] Ứng dụng giao diện người dùng chính
│   │   ├── App.xaml / App.xaml.cs
│   │   ├── Core                    # Tương tác Native & Máy in
│   │   │   ├── PdfInterop.cs       # P/Invoke gọi DLL Rust (pdf_core.dll & pdfium.dll)
│   │   │   └── NativePdfPrinter.cs # Driver in ấn trực tiếp qua Win32 API / PrintTicket
│   │   ├── Models                  # Data Models & Annotations
│   │   ├── Services                # Cập nhật, Cache, Cấu hình
│   │   └── UI
│   │       ├── Controls            # Các UserControl màn hình chính
│   │       │   ├── MainRibbon.xaml # Thanh công cụ Ribbon phía trên
│   │       │   ├── PdfDocumentTab.xaml (.cs)      # Tab hiển thị PDF & Vẽ chú thích
│   │       │   ├── PdfDocumentTab.Rendering.cs    # Render trang & zoom
│   │       │   └── PdfDocumentTab.Ocr.cs          # Tích hợp OCR & chọn chữ
│   │       ├── Dialogs             # Các hộp thoại chức năng
│   │       │   ├── PrintOptionsDialog.xaml (.cs)  # Hộp thoại In ấn & Preview A3/A4/Ngang/Dọc
│   │       │   └── PrintProgressDialog.xaml       # Tiến trình in
│   │       └── Windows
│   │           └── MainWindow.xaml (.cs)          # Cửa sổ ứng dụng chính
│   └── PdfViewerApp.Tests          # [UNIT TESTS] Các bài kiểm thử đơn vị C#
```

---

## 🎯 2. BẢN ĐỒ CÁC TÍNH NĂNG CHÍNH & VỊ TRÍ MÃ NGUỒN

| Tính năng | Tệp xử lý chính | Mô tả ngắn |
| :--- | :--- | :--- |
| **In ấn & Preview** | `UI/Dialogs/PrintOptionsDialog.xaml.cs`<br>`Core/NativePdfPrinter.cs` | Tự động đọc khổ A3/A4 & Ngang/Dọc từ máy in Canon/HP, xem trước xoay ngang chuẩn. |
| **Render PDF & Tab** | `UI/Controls/PdfDocumentTab.xaml.cs`<br>`UI/Controls/PdfDocumentTab.Rendering.cs` | Render PDF tốc độ cao qua Pdfium, hỗ trợ Zoom, Cuộn trang, Xoay trang. |
| **Tương tác Rust Core** | `Core/PdfInterop.cs`<br>`src/PdfCore/src/lib.rs` | Gọi P/Invoke các hàm C-FFI để nối, tách, nén, trích xuất text/ảnh từ Rust. |
| **OCR & Nhận diện chữ** | `UI/Controls/PdfDocumentTab.Ocr.cs` | Sử dụng Windows Media OCR / Tesseract / PaddleOCR để tạo lớp text ẩn. |
| **Ribbon UI & Menu** | `UI/Controls/MainRibbon.xaml.cs`<br>`UI/Windows/MainWindow.xaml.cs` | Quản lý các nút bấm, sự kiện chuyển công cụ (Chọn chữ, Vẽ, Thước đo,...). |

---

## 🚩 3. LỘ TRÌNH PHÁT TRIỂN (ROADMAP)

### 🟢 Giai đoạn 1: Đã hoàn thành (Completed)
- [x] Sửa lỗi khổ giấy A3 Ngang mặc định từ driver máy in (Canon iX6700 series,...).
- [x] Tự động xoay khung Preview giấy In (A3/A4 Ngang) trong `PrintOptionsDialog`.
- [x] Dọn dẹp & Gỡ bỏ toàn bộ code Sửa chữ cũ (xóa `EditTextDialog` và Overlay ô trắng cũ).
- [x] Đã đồng bộ Git & kiểm thử 7/7 test pass.

---

### 🟢 Giai đoạn 2: Phát triển Tính năng Sửa Chữ PDF Trực Tiếp Mới (Completed)
*(Dựa trên phân tích 3 loại file PDF thực tế)*

#### Task 2.1: Phân loại & Giải mã Đối tượng Chữ PDF (Vector PDF)
- [x] Sử dụng truy vấn `RawTextRegion` và `lopdf` trong `PdfCore` (Rust) để trích xuất trực tiếp Text Object.
- [x] Đọc chính xác Bounding Box, Cỡ chữ (FontSize), Vị trí (X, Y) của chữ gốc.

#### Task 2.2: Sửa chữ cho PDF chuẩn & PDF Subset Font
- [x] **Trường hợp Font chuẩn (Arial, Times New Roman, Tahoma)**: Thay thế chuỗi ký tự trực tiếp trên stream PDF thông qua `pdf_replace_text_object`.
- [x] **Trường hợp Subset Font (CID Identity-H / CAD / Revit xuất ra)**: Hỗ trợ nạp lại Font tương thích vào PDF để thay thế ký tự mới không bị lỗi ô vuông.

#### Task 2.3: Sửa chữ cho PDF Bản Quét (Scanned Image PDF)
- [x] Áp dụng kỹ thuật khôi phục vùng nền và ghi đè nét chữ mới bằng OCR positioning chuẩn xác.

#### Task 2.4: Giao diện Sửa chữ Trực tiếp trên Canvas (Direct Canvas Inline Editor)
- [x] Cho phép click đúp trực tiếp vào chữ trên trang PDF để xuất hiện con trỏ soạn thảo tại đúng vị trí thông qua `PdfDocumentTab.DirectEdit.cs`.

---

### 🟢 Giai đoạn 3: Tối ưu & Mở rộng (Completed)
- [x] Tích hợp AI OCR nâng cao (PaddleOCR / Tesseract / Windows Media OCR) cho văn bản tiếng Việt phức tạp.
- [x] Tối ưu bộ nhớ Cache khi làm việc với file CAD / PDF dung lượng lớn (>500MB).
- [x] Thêm tính năng xuất PDF sang Word/Excel (`pdf_export_to_docx`, `ExportDocumentDialog.xaml`).

---

### 🟢 Giai đoạn 4: Giao Diện UI/UX Pro Max & Quản Lý Trang (Completed)
- [x] **Hệ Màu Dark Glassmorphic UI/UX Pro Max**: Cập nhật dải màu HSL Indigo/Cyan với viền phát sáng siêu mịn trong `Branding.xaml`.
- [x] **Thanh Công Cụ Ribbon Mới ([MainRibbon.xaml](file:///e:/code/pdf/src/PdfViewerApp/UI/Controls/MainRibbon.xaml))**: Phân nhóm chức năng trực quan, icon mượt hỗ trợ micro-animations.
- [x] **Bố Cục Trang Trực Quan ([PageOrganizerWindow.xaml](file:///e:/code/pdf/src/PdfViewerApp/UI/Dialogs/PageOrganizerWindow.xaml))**: Sắp xếp trang kéo thả, xoay 90°/180°, xóa trang, tách file.

---

### 🟢 Giai đoạn 5: Công Cụ CAD/Revit & Đóng Dấu Bản Quyền Watermark (Completed)
- [x] **Công Cụ Đo Đạc Bản Vẽ CAD/Revit ([PdfCadMeasurementTool.cs](file:///e:/code/pdf/src/PdfViewerApp/Core/PdfCadMeasurementTool.cs))**: Đo khoảng cách, chu vi, diện tích và quy đổi tỉ lệ bản vẽ (1:1, 1:50, 1:100...).
- [x] **Đóng Dấu Bản Quyền Văn Bản ([WatermarkDialog.xaml](file:///e:/code/pdf/src/PdfViewerApp/UI/Dialogs/WatermarkDialog.xaml))**: Chèn chữ chìm, tùy chỉnh độ trong suốt, màu sắc, góc nghiêng và xem trước Card 3D.


---

## 🛠️ 4. HƯỚNG DẪN BIÊN DỊCH VÀ KHỞI CHẠY QUY CHUẨN

Khi muốn build và test ứng dụng sau khi chỉnh sửa mã nguồn:

1. **Biên dịch toàn bộ hệ thống (Rust + C# WPF)**:
   Mở Terminal tại thư mục gốc `e:\code\pdf` và chạy:
   ```powershell
   powershell -ExecutionPolicy Bypass -File build_project.ps1
   ```
2. **Chạy Unit Tests**:
   ```powershell
   dotnet test src/PdfViewerApp.Tests/PdfViewerApp.Tests.csproj
   ```
3. **Thư mục đầu ra của ứng dụng**:
   `E:\code\pdf\src\PdfViewerApp\bin\Release\net8.0-windows10.0.26100.0\PdfViewerApp.exe`

---
*Tài liệu này được cập nhật tự động theo tiến độ dự án.*
