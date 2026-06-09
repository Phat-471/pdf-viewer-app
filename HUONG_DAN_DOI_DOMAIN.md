# Hướng Dẫn Thay Đổi Tên Miền Xác Thực Bản Quyền (Domain Activation)

Tài liệu này hướng dẫn cách thay đổi tên miền kích hoạt bản quyền từ `hongmien.vn` sang tên miền mới của bạn khi cần thiết.

---

## Bước 1: Cấu hình trên Website mới (WordPress)

Hệ thống xác thực phía máy chủ chạy dưới dạng một Plugin WordPress. Bạn hãy thực hiện các bước sau:

1. Nén thư mục `wp-pdfpro-licensing` (nằm trong thư mục gốc của dự án này) thành tệp tin `.zip`.
2. Đăng nhập vào trang quản trị WordPress mới của bạn (ví dụ: `https://tenmienmoi.com/wp-admin`).
3. Truy cập vào **Plugins** -> **Add New** -> **Upload Plugin** -> Chọn tệp tin `.zip` vừa nén và nhấn **Install Now**, sau đó bấm **Activate**.
4. Plugin sẽ tự tạo cấu trúc bảng trong Cơ sở dữ liệu để lưu trữ thông tin License và thông tin máy kích hoạt.

---

## Bước 2: Cấu hình lại mã nguồn ứng dụng Client (C# / WPF)

Bạn cần mở mã nguồn dự án bằng Visual Studio hoặc trình chỉnh sửa mã nguồn và thay thế các đường dẫn sau:

### 1. Cấu hình các API kích hoạt và cập nhật
* **File liên quan**: `src/PdfViewerApp/PdfViewerApp/ActivationLicense.cs`
* **Vị trí cần sửa**:
  * Tìm dòng khai báo `ApiActivateUrl` (thường ở dòng 28):
    ```csharp
    public const string ApiActivateUrl = "https://hongmien.vn/wp-json/pdfpro/v1/activate";
    ```
    *Thay thế `https://hongmien.vn` bằng tên miền mới:*
    ```csharp
    public const string ApiActivateUrl = "https://tenmienmoi.com/wp-json/pdfpro/v1/activate";
    ```
  * Các hàm `Deactivate()` (dòng 242) và kiểm tra cập nhật `CheckForUpdatesAsync()` (dòng 477) cũng có chứa chuỗi `"https://hongmien.vn/wp-json/pdfpro/v1/activate"`. Hãy tìm kiếm từ khóa `hongmien.vn` trong file này và thay thế hết.

### 2. Cấu hình API báo cáo lỗi tự động (Crash/Error Report)
* **File liên quan**: `src/PdfViewerApp/PdfViewerApp/App.cs`
* **Vị trí cần sửa** (thường ở dòng 139):
  ```csharp
  string requestUri = "https://hongmien.vn/wp-json/pdfpro/v1/report-error";
  ```
  *Thay thế bằng:*
  ```csharp
  string requestUri = "https://tenmienmoi.com/wp-json/pdfpro/v1/report-error";
  ```

### 3. Cấu hình giao diện và liên kết hỗ trợ
* **File liên quan**: `src/PdfViewerApp/PdfViewerApp.AboutDialog.xaml`
* **Vị trí cần sửa** (thường ở dòng 193):
  ```xml
  <Hyperlink NavigateUri="https://hongmien.vn" ...>hongmien.vn</Hyperlink>
  ```
  *Thay thế bằng liên kết mới của bạn:*
  ```xml
  <Hyperlink NavigateUri="https://tenmienmoi.com" ...>tenmienmoi.com</Hyperlink>
  ```

---

## Bước 3: Biên dịch và đóng gói ứng dụng mới

Sau khi hoàn tất thay đổi tên miền trong code:

1. Chạy lệnh Build phiên bản Release:
   ```powershell
   dotnet build -c Release
   ```
2. Đóng gói thư mục output trong `src/PdfViewerApp/bin/Release/net8.0-windows/win-x64/publish` thành file `.zip` mới để phân phối cho người dùng.
