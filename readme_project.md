# 📄 TÀI LIỆU TỔNG QUAN VÀ KHẢO SÁT CHI TIẾT DỰ ÁN PDF PRO

Tài liệu này cung cấp toàn bộ thông tin chi tiết nhất về dự án **PDF Pro**, cấu trúc ngôn ngữ lựa chọn, hướng phát triển tương lai và danh sách tính năng từ cơ bản đến nâng cao để bất kỳ ai khi nhìn vào cũng có thể hiểu ngay lập tức.

---

## 1. THÔNG TIN CHUNG DỰ ÁN (PROJECT OVERVIEW)

* **Tên dự án:** PDF Pro (Tên thương mại: **PDF by Phát**)
* **Mục tiêu dự án:** Phát triển một ứng dụng Desktop chuyên nghiệp trên Windows chuyên dùng để đọc, biên tập, in ấn bản vẽ CAD/Revit kỹ thuật khổ lớn (A3/A4) và gộp/xoay các tệp PDF nặng siêu tốc.
* **Hình mẫu định hướng:** **Foxit PhantomPDF Standard** và **Adobe Acrobat Reader**.
* **Tiêu chí cốt lõi:**
  1. **Siêu nhẹ (Lightweight):** Khởi động tức thì (<0.2s), chiếm dụng tài nguyên CPU/RAM cực thấp.
  2. **Hiệu năng cực hạn (Extreme Performance):** Mở được bản vẽ Revit hàng trăm MB mượt mà không độ trễ.
  3. **Không crash (Zero-Crash):** Sử dụng các cơ chế quản lý luồng vẽ và giải phóng RAM chặt chẽ.
  4. **Thân thiện (Familiar UI/UX):** Giao diện dạng thanh menu Ribbon giống Microsoft Office/Foxit quen thuộc.

---

## 2. KIẾN TRÚC CÔNG NGHỆ & NGÔN NGỮ (TECH STACK)

Để khắc phục triệt để các hạn chế của phiên bản cũ (sử dụng Flutter bị crash bộ nhớ), dự án mới sử dụng **Kiến trúc Lai Đa Ngôn Ngữ (Hybrid Architecture)** tối ưu nhất cho Windows:

| Thành phần | Công nghệ lựa chọn | Lý do lựa chọn & Lợi thế mang lại |
| :--- | :--- | :--- |
| **Giao diện (Frontend UI)** | **C# WPF (.NET 9)** | * **Native Windows:** Tận dụng 100% sức mạnh vẽ giao diện Windows, hỗ trợ GPU DirectX render hoạt ảnh cực mịn.<br>* **Ribbon Control:** Tích hợp bộ thư viện Microsoft Ribbon chuyên nghiệp giống Foxit.<br>* **Virtualization:** Chỉ render những phần hiển thị trên màn hình, giúp cuộn chuột qua tài liệu dài 1000 trang không tốn RAM. |
| **Lõi xử lý (Core Engine)** | **Rust** (Dynamic Link Library `.dll`) | * **Bảo mật bộ nhớ:** Cơ chế Memory-Safety của Rust loại bỏ hoàn toàn các lỗi rò rỉ bộ nhớ hoặc lỗi crash phân vùng bộ nhớ khi zoom/cuộn chuột.<br>* **Xử lý nhị phân siêu tốc:** Đảm nhận tác vụ nặng như cắt, nối, gộp file trực tiếp ở mức byte nhị phân với tốc độ đọc ghi đĩa SSD vật lý. |
| **Bộ dựng hình (PDF Engine)** | **PDFium (C++)** | * **Bộ lõi của Chrome & Foxit:** Là bộ giải mã PDF tốt nhất hành tinh, tương thích 100% với các bản vẽ CAD/Revit phức tạp, render nét vẽ vector sắc mịn tuyệt đối. |
| **Hệ thống In ấn (Print Engine)** | **Windows Print Spooler API (C#)** | * In ấn trực tiếp qua hàng đợi hệ thống của Windows, gửi lệnh in vector nguyên bản chất lượng cao đến máy in vật lý mà không cần qua file trung gian, hỗ trợ in A3/A4 hoàn hảo. |

---

## 3. CHỨC NĂNG DỰ ÁN CHI TIẾT (FEATURES MATRIX)

### 3.1. Nhóm Tính Năng Đọc & Hiển Thị (High-Performance PDF Viewer)
* **Khởi động siêu tốc:** Mở file PDF ngay khi click đúp chuột trong Windows Explorer.
* **Hiển thị On-Demand (Tiling / Virtualized View):** Chỉ nạp và render trang đang nằm trong khung nhìn (Viewport) giúp xử lý file nặng hàng GB cực nhẹ nhàng.
* **Cuộn chuột mượt mà (Smooth Scrolling):** Bộ nhớ đệm (Cache) nạp trước trang tiếp theo bằng luồng Rust chạy ngầm.
* **Thu phóng chuyên nghiệp (Zoom Control):** Zoom mượt bằng `Ctrl + Cuộn chuột` từ 10% đến 1000%, tự động khít chiều ngang (Fit Width) hoặc khít toàn bộ trang (Fit Page).
* **Đa chế độ đọc:**
  * Chế độ ban đêm (Night Mode): Đảo màu nền đen chữ trắng bảo vệ mắt.
  * Chế độ đọc tập trung (Read Mode): Ẩn toàn bộ thanh công cụ để tập trung đọc.

### 3.2. Nhóm Tính Năng Biên Tập & Tổ Chức Trang (Page Organizer)
* **Gộp File Siêu Tốc (Binary PDF Merge):** Lựa chọn nhiều file PDF và nối lại thành 1 file duy nhất ở tốc độ phần cứng, sắp xếp thứ tự file trước khi gộp.
* **Trích xuất trang (Extract Pages):** Chọn và xuất một hoặc nhiều trang bất kỳ ra một file PDF mới.
* **Xoay bản vẽ kỹ thuật:** Xoay 90°, 180°, 270° từng trang lẻ hoặc tất cả trang (rất cần thiết đối với bản vẽ CAD nằm ngang).
* **Chèn trang trống / Xóa trang:** Xóa bỏ các trang lỗi hoặc chèn thêm trang ghi chú trắng vào bất kỳ vị trí nào.

### 3.3. Nhóm Tính Năng In Ấn Vector Sắc Nét (Native Printing)
* **In Native Vector:** Gửi trực tiếp cấu trúc vector PDF đến máy in mà không chuyển thành ảnh raster (giúp nét vẽ bản vẽ thiết kế mảnh, sắc nét, không bị răng cưa hay nhòe mực).
* **Tự động xoay trang bản vẽ (Auto-Rotate & Center):** Tự động nhận diện bản vẽ nằm dọc hay ngang để xoay giấy in tương ứng.
* **Hỗ trợ khổ giấy kỹ thuật chuyên sâu:** A3, A4, A5, Letter, Legal và tùy chỉnh kích thước giấy bất kỳ.
* **Tùy chọn in ấn đa dạng:** In hai mặt (Duplex), in thang màu xám (Grayscale tiết kiệm mực), in nhiều bản sao (Copies) có sắp xếp (Collate).

### 3.4. Nhóm Tính Năng Đánh Dấu & Trợ Lý AI (Annotations & AI)
* **Stamp & Chữ ký số:** Người dùng có thể dán ảnh chữ ký cá nhân hoặc dán các con dấu mẫu (ĐÃ DUYỆT, KHẨN, COPY) lên văn bản.
* **Tìm kiếm nội dung siêu tốc:** Trích xuất text từ PDF thời gian thực, tìm kiếm từ khóa và đánh dấu nổi bật (Highlight) kết quả.
* **AI Copilot (Tùy chọn):** Kết nối với mô hình AI cục bộ (Ollama) hoặc đám mây (Gemini) để tóm tắt nhanh tài liệu vẽ kỹ thuật hoặc hỏi đáp thông minh.

---

## 4. HƯỚNG PHÁT TRIỂN & LỘ TRÌNH DỰ ÁN (ROADMAP)

### Giai đoạn 1: Xây dựng lõi và Khung xương giao diện (Tháng 1)
* [x] Đập đi toàn bộ mã nguồn cũ bị lỗi của Flutter, di dời vào thư mục lưu trữ an toàn `old_project`.
* [x] Tạo dự án khung C# WPF (.NET 9) Ribbon và dự án lõi Rust (`PdfCore`).
* [x] Thiết lập thành công kết nối FFI gọi DLL nhị phân giữa C# và Rust.
* [x] Hoàn thiện giao diện thanh Ribbon trực quan giống Foxit PhantomPDF.

### Giai đoạn 2: Tối ưu hóa bộ render và Gộp file siêu tốc (Tháng 2)
* [x] Tích hợp sâu thư viện PDFium C++ vào lõi Rust để render trang PDF thành Bitmap độ nét cao đưa lên GPU WPF hiển thị.
* [x] Hoàn thiện tính năng gộp file (Merge) nhị phân trực tiếp ở tầng đĩa.
* [x] Phát triển thanh Sidebar bên trái hiển thị danh sách Thumbnail trang và các chức năng chuột phải (Xoay, Xóa, Chèn trang).
* [x] Tối ưu hóa cơ chế gộp file từ Explorer chuột phải: loại bỏ hiện tượng nhấp nháy giao diện bằng cách kiểm tra Mutex sớm trong `App.xaml.cs`.
* [x] Chuyển đổi cơ chế xoay trang sang **xoay trực quan tức thì trên giao diện (In-Memory Rotate)** và cung cấp nút **Lưu (Save) / Lưu Dưới Dạng (Save As)** chủ động trên thanh Ribbon và Menu Tệp.

### Giai đoạn 3: In ấn hoàn hảo và Đóng gói sản phẩm (Tháng 3)
* [ ] Phát triển hệ thống In ấn Vector A3/A4 không qua file trung gian, khít trang tuyệt đối.
* [ ] Thực hiện kiểm thử hiệu năng với các file Revit/CAD cực nặng (>500MB).
* [ ] Đóng gói bộ cài đặt cài đặt Windows tự động chuyên nghiệp bằng Inno Setup và phát hành chính thức bản cập nhật siêu tối ưu!

---

## 5. HƯỚNG DẪN BACKUP VÀ PHỤC HỒI (BACKUP & ROLLBACK POLICY)

Để đảm bảo tính an toàn tuyệt đối khi nâng cấp hoặc sửa đổi mã nguồn dự án:
1. **Trước khi thay đổi bất kỳ tệp tin nào**:
   - Phải tạo một bản sao dự phòng của tệp tin đó với định dạng tên `.bak` hoặc `.backup` ngay tại thư mục chứa tệp tin (Ví dụ: `MainWindow.xaml.cs` -> sao chép thành `MainWindow.xaml.cs.bak`).
2. **Khi gặp lỗi biên dịch hoặc lỗi runtime không mong muốn**:
   - Có thể nhanh chóng khôi phục lại mã nguồn cũ bằng cách xóa tệp tin lỗi và đổi tên tệp tin `.bak` trở lại định dạng ban đầu.
3. **Khi tính năng mới đã hoạt động hoàn hảo và ổn định**:
   - Có thể dọn dẹp các tệp tin backup dự phòng để giữ thư mục làm việc sạch sẽ.

## Library Audit Notes

This project now has a small runtime audit path to help you check what is actually loaded.

### What is used directly
- `Fluent.Ribbon` is used in `MainWindow.xaml` for the Ribbon shell.
- `pdf_core.dll` is used by `DllImport` from the WPF app for merge, rotate, delete, and insert blank page functions.
- `pdfium.dll` is used by `PdfiumEngine.cs` for loading and rendering pages.

### What is transitive
- `ControlzEx` comes in through `Fluent.Ribbon`.
- `Microsoft.Xaml.Behaviors.Wpf` is not a direct package anymore. It is kept transitively by `ControlzEx`.
- `System.Text.Json` is also transitive through `ControlzEx`.

### Merge workflow
- Dragging multiple PDFs from Explorer now sorts them by file name before quick merge starts.
- Merge progress is shown per file.
- The merged PDF is written automatically to `%LOCALAPPDATA%\PdfPro\Merged\` and opened after merge completes.
- The `tools\install_explorer_context_menu.ps1` script registers an Explorer right-click verb that launches the app in `--merge --exit-after-merge` mode, opens the merged file with the default PDF app, and exits.
- **Tối ưu hóa chạy ngầm**: File được gom gửi vào hàng đợi thông qua tệp tin văn bản trong thư mục `%LOCALAPPDATA%\PdfPro\ExplorerMergeQueue`. Chỉ có tiến trình đầu tiên (Mutex Owner) xử lý gộp và hiển thị hộp thoại, các tiến trình phụ được tắt ngay lập tức nhờ kiểm tra sớm trong sự kiện `Startup` tại [App.xaml.cs](file:///e:/code/pdf/src/PdfViewerApp/App.xaml.cs).

### Explorer menu setup
1. Build `PdfViewerApp` in `Release`.
2. Run `tools\install_explorer_context_menu.ps1` with the full path to `PdfViewerApp.exe`.
3. In Explorer, select multiple PDFs, right-click, and choose `Merge PDF with PDF Pro`.
4. The app sorts files naturally by name, merges them, shows progress, and opens the merged result automatically.
5. Run `tools\uninstall_explorer_context_menu.ps1` to remove the shell verb.

### Code layout
- `PdfDocumentTab.xaml.cs` currently keeps document loading, the main render pipeline, page cache, in-memory page rotation dictionary (`_pageRotations`), and annotation editing.
- `PdfDocumentTab.Ui.cs` now holds sidebar toggles, zoom handling, page navigation, and viewport throttling.
- `PdfDocumentTab.Printing.cs` now holds the print flow.
- The next split targets are the render/cache helpers and the annotation/content helpers.

### What to check after launching
- Click `Kiểm tra thư viện` in the ribbon.
- The report shows whether the managed assemblies and native DLLs are loaded and whether the DLL files are present in the app folder.
- If `Microsoft.Xaml.Behaviors.Wpf` appears in the runtime output, that is expected because it is a transitive dependency, not a direct reference.
- Native DLLs can stay unloaded until you open or process a PDF, so file presence matters more than immediate process-module visibility.

### Progressive render notes
- The render flow now starts with placeholder page frames.
- The visible page is queued first, then nearby pages are rendered in priority order.
- Thumbnails are intentionally deferred so they do not compete with the first visible page render.
- If you profile heavy files, compare `LoadDocument`, `CollectPageDimensions`, the first visible page render, and the deferred thumbnail phase separately.

### Printing profile notes
- Printing now resolves a per-printer profile before applying coordinate offset mode.
- `Auto` uses the detected profile, `WPF Offset` is a manual override, and `Physical` uses raw coordinates.
- `Canon iX6770 / iX6700` has its own safety padding profile.
- `Print test frame` is a diagnostic mode that prints border guides so clipping can be identified directly on paper.
