# 🔍 BÁO CÁO KIỂM TRA TỔNG THỂ DỰ ÁN PDF PRO

## Phân tích dựa trên source code thực tế — Ngày 17/06/2026

---

## TÓM TẮT ĐÁNH GIÁ

| Hạng mục | Điểm | Nhận xét |
|:---|:---:|:---|
| **Kiến trúc tổng thể** | 🟡 6/10 | Ý tưởng tốt (C# + Rust), nhưng file quá lớn, thiếu tách lớp |
| **Hiệu năng** | 🟢 7/10 | Render engine ổn, nhưng có bottleneck ở global lock |
| **Bảo mật** | 🔴 4/10 | Lộ private key RSA, hardcode secret, master key bypass |
| **Chất lượng code** | 🟡 5/10 | God class pattern, thiếu unit test, code lặp nhiều |
| **Dependencies** | 🟢 7/10 | Đang dùng versions hợp lý, cần nâng .NET 9 |
| **DevOps** | 🟢 7/10 | Scripts đầy đủ, thiếu CI/CD tự động |

---

## 🔴 VẤN ĐỀ NGHIÊM TRỌNG (CẦN SỬA NGAY)

### 1. LỘ PRIVATE KEY RSA TRONG SOURCE CODE

> [!CAUTION]
> File [wp-pdfpro-licensing.php](file:///e:/code/pdf/wp-pdfpro-licensing/wp-pdfpro-licensing.php#L112-L153) chứa **toàn bộ Private Key RSA 2048-bit** dạng plaintext. Ai có source code đều có thể giả mạo chữ ký license.

**Hiện trạng:**
```php
// Line 112-139: Private key hardcode trực tiếp!
$private_key = "-----BEGIN PRIVATE KEY-----\nMIIEvAIBADANBgk...";
```

**Khuyến nghị:**
- Di chuyển private key ra biến môi trường hoặc WordPress options (encrypted)
- Sử dụng `wp_options` với `wp_salt()` để mã hóa key khi lưu
- Rotate cặp key RSA mới ngay lập tức vì key cũ đã bị expose trên Git

---

### 2. HARDCODE ACTIVATION SECRET & MASTER KEY BYPASS

> [!CAUTION]
> File [ActivationLicense.cs](file:///e:/code/pdf/src/PdfViewerApp/PdfViewerApp/ActivationLicense.cs#L22-L24) có activation secret hardcode và **master key bypass** cho phép kích hoạt bất kỳ máy nào.

**Hiện trạng:**
```csharp
// Line 22: Secret dùng tạo offline key
private const string ActivationSecret = "HPhat.PdfPro.LocalActivation.2026";

// Line 293: Master key bypass - AI CÓ KEY NÀY ĐỀU KÍCH HOẠT ĐƯỢC!
string b2 = NormalizeKey("PDFPRO-3A03-3629-06B1-D3AF-2018");
```

**Khuyến nghị:**
- Xóa master key bypass hoàn toàn
- Di chuyển activation logic sang server-only validation
- Obfuscate mã nguồn C# trước khi phát hành (sử dụng ConfuserEx hoặc .NET Reactor)

---

### 3. PUBLIC KEY RSA NHÚNG CỨNG TRONG CLIENT

> [!WARNING]
> [ActivationLicense.cs:L24](file:///e:/code/pdf/src/PdfViewerApp/PdfViewerApp/ActivationLicense.cs#L24) nhúng public key cứng. Nếu cần rotate key sẽ phải update toàn bộ client.

**Khuyến nghị:** Đã có cơ chế fetch public key từ server (`/public-key` endpoint) — nhưng cần ưu tiên remote key hơn hardcode key.

---

## 🟠 VẤN ĐỀ CẤU TRÚC CODE (ƯU TIÊN CAO)

### 4. GOD CLASS — PdfDocumentTab.cs (7,159 dòng / 229 KB)

> [!WARNING]
> [PdfDocumentTab.cs](file:///e:/code/pdf/src/PdfViewerApp/PdfViewerApp/PdfDocumentTab.cs) là file **lớn nhất dự án** với 7,159 dòng code. Đây là "God Class" chứa MỌI THỨ: render, annotation, zoom, pan, OCR, AI, text selection, measurement, ink, shapes, signatures, stamps, printing...

**Ảnh hưởng:**
- Cực kỳ khó bảo trì và debug
- Không thể viết unit test cho từng phần
- Merge conflict liên tục khi nhiều người cùng sửa

**Khuyến nghị tách thành các module:**

| Module mới | Dòng ước tính | Trách nhiệm |
|:---|:---:|:---|
| `PdfRenderEngine.cs` | ~800 | Render, bitmap cache, viewport |
| `AnnotationManager.cs` | ~1500 | Quản lý tất cả annotation types |
| `TextSelectionHandler.cs` | ~400 | Chọn text, copy, highlight |
| `ZoomPanController.cs` | ~500 | Zoom, pan, smooth zoom |
| `OcrEngine.cs` | ~300 | Windows OCR integration |
| `DrawingToolHandler.cs` | ~800 | Shapes, ink, signatures, stamps |
| `MeasurementTool.cs` | ~400 | Distance, area, perimeter |
| `PdfDocumentTab.cs` | ~2000 | Coordinator, UI bindings only |

### 5. GOD CLASS — MainWindow.cs (3,626 dòng / 120 KB)

> [!WARNING]
> [MainWindow.cs](file:///e:/code/pdf/src/PdfViewerApp/PdfViewerApp/MainWindow.cs) cũng quá lớn. Chứa cả logic update, theme, keyboard shortcuts, tab management, merge, Google Drive URL resolve...

**Khuyến nghị tách:**
- `ThemeManager.cs` — quản lý theme
- `KeyboardShortcutHandler.cs` — xử lý phím tắt
- `TabManager.cs` — quản lý tabs
- `UpdateOrchestrator.cs` — logic cập nhật

### 6. DUPLICATE DllImport DECLARATIONS

Cùng một hàm `extract_pdf_pages` được khai báo `[DllImport]` ở **cả hai file**:
- [MainWindow.cs:L106](file:///e:/code/pdf/src/PdfViewerApp/PdfViewerApp/MainWindow.cs#L106)
- [PdfDocumentTab.cs:L3837](file:///e:/code/pdf/src/PdfViewerApp/PdfViewerApp/PdfDocumentTab.cs#L3837)

**Khuyến nghị:** Tạo class `PdfCoreInterop.cs` tập trung tất cả P/Invoke declarations.

---

## 🟡 TỐI ƯU HIỆU NĂNG

### 7. GLOBAL LOCK BOTTLENECK — PdfiumEngine

> [!IMPORTANT]
> [PdfiumEngine.cs](file:///e:/code/pdf/src/PdfViewerApp/PdfViewerApp/PdfiumEngine.cs) sử dụng **một global lock duy nhất** (`RenderLock`) cho MỌI thao tác PDF. Khi render trang nặng, toàn bộ UI bị block.

```csharp
// Line 21: MỘT lock cho TẤT CẢ operations
private static readonly object RenderLock = new object();

// Line 295: Render bị lock - trang nặng sẽ block GetPageCount, GetPageSize...
lock (RenderLock) {
    // render page... có thể mất 200-500ms cho trang CAD nặng
}
```

**Khuyến nghị:**
- Tách thành `RenderLock` và `MetadataLock` (cho GetPageCount, GetPageSize)
- Hoặc sử dụng `ReaderWriterLockSlim` — nhiều thread đọc metadata song song, chỉ lock khi render
- Di chuyển render sang background thread hoàn toàn (đã có RenderQueue nhưng vẫn lock trên UI thread)

### 8. BITMAP CACHE GIỚI HẠN CỨNG 384MB

```csharp
// Line 200: Hardcode 384MB cache
private const long MaxBitmapCacheBytes = 402653184L; // 384 MB
```

**Khuyến nghị:**
- Cho phép cấu hình cache size trong Settings (dựa trên RAM máy)
- Máy có 16GB RAM có thể cache 1-2GB; máy 4GB nên giảm xuống 256MB
- Sử dụng `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` để tự động tính

### 9. HttpClient TẠO MỚI LIÊN TỤC

> [!NOTE]
> Tìm thấy **~30 lần** `new HttpClient()` rải rác khắp project — gây socket exhaustion và DNS cache issues.

**Khuyến nghị:** Sử dụng `IHttpClientFactory` pattern hoặc ít nhất tạo 1 `static HttpClient` dùng chung.

### 10. INVERT COLORS LOOP CHƯA TỐI ƯU

```csharp
// PdfiumEngine.cs Line 368-374: Loop byte-by-byte chậm
for (int i = 0; i < array.Length; i += 4) {
    array[i] = (byte)(255 - array[i]);       // B
    array[i + 1] = (byte)(255 - array[i + 1]); // G
    array[i + 2] = (byte)(255 - array[i + 2]); // R
}
```

**Khuyến nghị:** Dùng `System.Numerics.Vector<byte>` (SIMD) hoặc `Span<byte>` + unrolled loop — nhanh hơn **3-5x** cho ảnh lớn.

---

## 🔵 NÂNG CẤP ĐỀ XUẤT

### 11. NÂNG CẤP .NET 8 → .NET 9

| Lợi ích | Chi tiết |
|:---|:---|
| **Hiệu năng** | JIT cải thiện 10-15%, GC tốt hơn |
| **WPF** | Native AOT support tốt hơn, startup nhanh hơn |
| **Security** | Bản vá bảo mật mới nhất |
| **Long-term** | .NET 8 LTS hết support 11/2026; .NET 9 STS đến 05/2026 → chờ .NET 10 LTS (11/2025 release) |

> [!TIP]
> **Khuyến nghị thực tế:** Chờ .NET 10 LTS (release 11/2025, support đến 11/2028). Hoặc nâng lên .NET 9 ngay nếu muốn tận dụng perf gains.

### 12. NÂNG CẤP RUST DEPENDENCIES

| Crate | Hiện tại | Mới nhất | Lợi ích |
|:---|:---|:---|:---|
| `lopdf` | 0.31.0 | 0.34+ | Bug fixes, better PDF 2.0 support |
| `image` | 0.24.7 | 0.25+ | WebP support, performance |
| `rayon` | 1.12.0 | ✅ Đã mới | — |

### 13. THÊM OBFUSCATION CHO RELEASE BUILD

**Hiện trạng:** App publish dạng single-file .NET → dễ dàng decompile bằng ILSpy/dnSpy, lộ toàn bộ activation logic.

**Khuyến nghị:**
- Tích hợp ConfuserEx hoặc .NET Reactor vào build pipeline
- Ít nhất obfuscate các class: `ActivationLicense`, `AppUpdateService`, `AiSettings`

### 14. THÊM UNIT TEST

**Hiện trạng:** Dự án **không có bất kỳ unit test nào**. Chỉ có `run_test.bat` là smoke test.

**Khuyến nghị ưu tiên test cho:**
- `ActivationLicense` — logic xác thực bản quyền
- `AppUpdateService` — logic cập nhật
- `PdfiumEngine` — render engine
- Rust `pdf_core` — merge, split, rotate (`cargo test`)

---

## 📋 BẢNG ĐỀ XUẤT HÀNH ĐỘNG THEO ƯU TIÊN

| # | Hạng mục | Mức độ | Effort | Ưu tiên |
|:---:|:---|:---:|:---:|:---:|
| 1 | 🔴 Xóa private key khỏi source, rotate key | Critical | 2h | **P0** |
| 2 | 🔴 Xóa master key bypass | Critical | 30m | **P0** |
| 3 | 🔴 Obfuscate activation code | Critical | 4h | **P0** |
| 4 | 🟠 Tách PdfDocumentTab.cs thành modules | High | 3-5 ngày | **P1** |
| 5 | 🟠 Tạo PdfCoreInterop.cs (gom DllImport) | High | 2h | **P1** |
| 6 | 🟠 Tách MainWindow.cs | High | 2-3 ngày | **P1** |
| 7 | 🟡 Sửa PdfiumEngine lock strategy | Medium | 1 ngày | **P2** |
| 8 | 🟡 Singleton HttpClient | Medium | 2h | **P2** |
| 9 | 🟡 Dynamic bitmap cache size | Medium | 1h | **P2** |
| 10 | 🟡 SIMD invert colors | Low | 2h | **P3** |
| 11 | 🔵 Nâng .NET 9 / chờ .NET 10 | Medium | 1 ngày | **P2** |
| 12 | 🔵 Nâng Rust crates | Low | 2h | **P3** |
| 13 | 🔵 Thêm unit tests | Medium | 3-5 ngày | **P2** |
| 14 | 🔵 CI/CD pipeline (GitHub Actions) | Low | 1 ngày | **P3** |

---

## 📊 ĐIỂM MẠNH CỦA DỰ ÁN

Không chỉ có vấn đề — dự án có nhiều **điểm rất tốt**:

| Điểm mạnh | Chi tiết |
|:---|:---|
| ✅ **Kiến trúc hybrid C#+Rust** | Thiết kế thông minh — WPF cho UI, Rust cho xử lý nặng |
| ✅ **Render pipeline** | Priority queue, bitmap cache LRU, viewport-based rendering |
| ✅ **Hệ thống theme** | 6+ themes, registry pattern, toàn bộ UI sync |
| ✅ **Silent update** | Background download + install on exit — UX tuyệt vời |
| ✅ **Rollback system** | Tự động backup trước update, restore được bản cũ |
| ✅ **Single instance** | Named pipe IPC — forward file args tới instance đang chạy |
| ✅ **AI integration** | 3 providers (Gemini/OpenAI/Ollama) với auto-discovery |
| ✅ **Crash telemetry** | Tự động báo lỗi về server |
| ✅ **OCR** | Dùng Windows.Media.Ocr native — không cần Tesseract |
| ✅ **GDI printing** | In trực tiếp qua GDI32 — nhanh và chính xác |
