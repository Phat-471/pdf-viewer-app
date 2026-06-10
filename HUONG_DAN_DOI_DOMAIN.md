# Hướng Dẫn Thay Đổi Tên Miền & Quy Trình Tự Động Cập Nhật (Auto-Update)

Tài liệu này hướng dẫn chi tiết cách thay đổi tên miền kích hoạt bản quyền từ `hongmien.vn` sang tên miền mới, kèm theo **Quy trình cấu hình và nâng cấp ứng dụng tự động (Auto-Update)** thông qua GitHub Actions CI/CD.

---

## PHẦN 1: HƯỚNG DẪN NÂNG CẤP APP & TÍNH NĂNG MỚI (AUTO-UPDATE)

Hệ thống cập nhật phiên bản mới đã được tự động hóa hoàn toàn. Bất kỳ khi nào bạn hoàn thành viết code hoặc sửa lỗi và muốn phát hành bản cập nhật cho khách hàng:

### 1. Quy trình nâng cấp ứng dụng (3 bước tự động)

* **Bước 1: Chạy script đóng gói tự động trên máy tính của bạn**
  Mở PowerShell tại thư mục gốc của dự án và chạy:
  ```powershell
  # Nâng số Patch (Ví dụ: 1.2.0 -> 1.2.1)
  powershell -File tools\release.ps1 -Part patch -SkipSmokeTest

  # Hoặc nâng số Minor (Ví dụ: 1.2.0 -> 1.3.0)
  powershell -File tools\release.ps1 -Part minor -SkipSmokeTest
  ```
  *Script này sẽ tự động thay đổi phiên bản trong file `VERSION.txt`, file `AssemblyInfo.cs`, tự động compile Rust core & C# WPF, và tạo file nén đóng gói cùng tệp cấu trúc manifest trong thư mục `releases/`.*

* **Bước 2: Commit và Push mã nguồn lên nhánh chính của GitHub**
  ```bash
  git add VERSION.txt src/PdfViewerApp/Properties/AssemblyInfo.cs
  git commit -m "Bump version to X.Y.Z"
  git push origin master
  ```

* **Bước 3: Tạo Tag phiên bản và đẩy lên để kích hoạt CI/CD**
  ```bash
  # Đẩy tag phiên bản mới (Ví dụ v1.2.1)
  git tag v1.2.1
  git push origin v1.2.1
  ```
  *Khi đẩy Tag lên, GitHub Actions sẽ tự động kích hoạt tiến trình Build, tải file đóng gói lên mục Release của GitHub, và gọi API gửi thông tin tải trực tiếp về trang WordPress của bạn.*

### 2. Các tính năng mới của hệ thống Auto-Update
* **Tự động đồng bộ link tải:** Không cần phải upload thủ công lên Google Drive và copy paste. Link tải trực tiếp từ GitHub Releases sẽ tự động được đồng bộ về WordPress.
* **Xác thực mã băm SHA256 và kích thước tệp:** Ứng dụng Client sẽ tự động tải file về, đối chiếu SHA256 để đảm bảo tệp tải về toàn vẹn trước khi tiến hành cài đặt đè.
* **Cơ chế chịu lỗi (Failover & Retry):** Quy trình gọi API WordPress có sẵn bộ đếm thử lại (3 lần) khi có sự cố mạng và được cấu hình `continue-on-error` để không làm gián đoạn bản phát hành chính trên GitHub.

---

## PHẦN 2: QUY TRÌNH THAY ĐỔI TÊN MIỀN (DOMAIN ACTIVATION)

Khi bạn muốn chuyển hệ thống từ `hongmien.vn` sang một tên miền mới (ví dụ: `tenmienmoi.com`), hãy làm theo 4 bước sau:

### Bước 1: Cấu hình trên Website mới (WordPress)
1. Nén thư mục `wp-pdfpro-licensing` (nằm trong thư mục gốc của dự án này) thành tệp tin `.zip`.
2. Đăng nhập vào trang quản trị WordPress mới của bạn (ví dụ: `https://tenmienmoi.com/wp-admin`).
3. Truy cập vào **Plugins** -> **Add New** -> **Upload Plugin** -> Chọn tệp tin `.zip` và kích hoạt.
4. Truy cập vào menu quản lý **PDF Pro Licensing** -> Chọn mục **Update Configuration** để lấy mã **Publish Token**.

### Bước 2: Cấu hình lại mã nguồn Client (C# / WPF)
Mở mã nguồn dự án bằng Visual Studio hoặc editor của bạn và thay thế các đường dẫn sau:

1. **Cấu hình các API kích hoạt và cập nhật:**
   * **File**: `src/PdfViewerApp/PdfViewerApp/ActivationLicense.cs`
   * Tìm và thay thế giá trị tên miền cũ ở dòng `ApiActivateUrl`:
     ```csharp
     public const string ApiActivateUrl = "https://tenmienmoi.com/wp-json/pdfpro/v1/activate";
     ```
   * *Tìm kiếm và thay thế toàn bộ từ khóa `hongmien.vn` trong file này.*

2. **Cấu hình API báo cáo lỗi tự động (Crash/Error Report):**
   * **File**: `src/PdfViewerApp/PdfViewerApp/App.cs`
   * Thay thế đường dẫn báo cáo lỗi:
     ```csharp
     string requestUri = "https://tenmienmoi.com/wp-json/pdfpro/v1/report-error";
     ```

3. **Cấu hình giao diện và liên kết hỗ trợ:**
   * **File**: `src/PdfViewerApp/PdfViewerApp.AboutDialog.xaml`
   * Sửa đường dẫn Hyperlink:
     ```xml
     <Hyperlink NavigateUri="https://tenmienmoi.com" ...>tenmienmoi.com</Hyperlink>
     ```

### Bước 3: Cấu hình lại Quy trình Tự động Cập nhật (GitHub & Cloudflare)

Để quy trình GitHub Actions có thể gửi thông tin về tên miền mới:

1. **Cập nhật Secrets trên GitHub:**
   Vào Repository GitHub của bạn -> **Settings** -> **Secrets and variables** -> **Actions** -> Chọn sửa hoặc tạo mới 2 Secrets:
   * `PDFPRO_UPDATE_SITE_URL`: Nhập địa chỉ tên miền mới (Ví dụ: `https://tenmienmoi.com`).
   * `PDFPRO_PUBLISH_TOKEN`: Nhập Token mới sao chép từ admin WordPress của trang web mới.

2. **Cập nhật cấu hình cục bộ (Local config):**
   * Mở file `tools/publish_config.json` và cập nhật `"site_url"` thành tên miền mới, `"publish_token"` thành token mới của bạn (dành cho mục đích chạy lệnh cập nhật thủ công dưới máy local khi cần).

3. **Bỏ qua tường lửa Cloudflare trên Tên miền mới:**
   Nếu tên miền mới của bạn sử dụng Cloudflare, bạn **bắt buộc** phải cấu hình bỏ qua tường lửa cho API để tránh việc GitHub Actions bị chặn:
   * Truy cập trang quản trị Cloudflare của tên miền mới.
   * Chọn **Security** -> **WAF** -> **Custom Rules** -> Nhấn **Create rule**.
   * Thiết lập rule:
     * **Name**: `Allow PDFPro Update API`
     * **Field**: `URI Path` | **Operator**: `equals` | **Value**: `/wp-json/pdfpro/v1/update-publish`
     * **Action**: Chọn `Skip` -> Tích chọn toàn bộ các dịch vụ hiện ra (*Bot Fight Mode*, *Security Level*, *WAF Managed Rules*).
   * Nhấn **Deploy** và đảm bảo trạng thái rule là **Active**.
