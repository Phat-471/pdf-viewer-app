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

### 🟡 Giai đoạn 2: Phát triển Tính năng Sửa Chữ PDF Trực Tiếp Mới (In Progress)
*(Dựa trên phân tích 3 loại file PDF thực tế)*

#### Task 2.1: Phân loại & Giải mã Đối tượng Chữ PDF (Vector PDF)
- [ ] Sử dụng Pdfium `FPDF_PAGEOBJECT` trong `PdfCore` (Rust) để truy vấn trực tiếp Text Object.
- [ ] Đọc chính xác Bounding Box, Matrix nghiêng, Cỡ chữ (FontSize), Màu sắc (Color) và Tên Font của chữ gốc.

#### Task 2.2: Sửa chữ cho PDF chuẩn & PDF Subset Font
- [ ] **Trường hợp Font chuẩn (Arial, Times New Roman, Tahoma)**: Thay thế chuỗi ký tự trực tiếp trên `FPDF_TEXT_OBJECT`.
- [ ] **Trường hợp Subset Font (CID Identity-H / CAD / Revit xuất ra)**: Nhúng (Re-embed) Font hệ thống tương thích vào PDF để thay thế ký tự mới mà không bị lỗi ô vuông / lệch font.

#### Task 2.3: Sửa chữ cho PDF Bản Quét (Scanned Image PDF)
- [ ] Áp dụng kỹ thuật **Inpainting (Khôi phục nét vẽ/ảnh nền xung quanh)** để xóa vết chữ cũ mịn màng, không tạo ô màu trắng che nét CAD.
- [ ] Ghi đè nét chữ mới bằng OCR positioning chuẩn xác.

#### Task 2.4: Giao diện Sửa chữ Trực tiếp trên Canvas (Direct Canvas Inline Editor)
- [ ] Cho phép click đúp trực tiếp vào chữ trên trang PDF để xuất hiện con trỏ soạn thảo tại đúng vị trí (thay vì mở Dialog riêng).

---

### 🔵 Giai đoạn 3: Tối ưu & Mở rộng (Planned)
- [ ] Tích hợp AI OCR nâng cao (PaddleOCR / Tesseract hOCR) cho văn bản tiếng Việt phức tạp.
- [ ] Tối ưu bộ nhớ Cache khi làm việc với file CAD / PDF dung lượng lớn (>500MB).
- [ ] Thêm tính năng xuất PDF sang Word/Excel giữ nguyên định dạng.

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
