# Cài đặt, cập nhật và phát hành PDF Pro

Mỗi phiên bản phải có đủ bốn tệp trong GitHub Release:

| Tệp | Mục đích |
| --- | --- |
| `PDFPro_Setup_vX.Y.Z.exe` | Cài mới hoặc cài lại ứng dụng; nâng cấp tại thư mục đã cài. |
| `PdfViewerApp_vX.Y.Z.zip` | Gói cập nhật trong ứng dụng, gồm EXE, DLL và các script cài đặt/gỡ cài đặt. |
| `update-manifest.json` | Phiên bản, URL, kích thước và SHA-256 của ZIP và bộ cài. |
| `SHA256SUMS.txt` | SHA-256 của ba tệp trên, để đối chiếu tính toàn vẹn. |

GitHub tự tạo `Source code (zip)` và `Source code (tar.gz)`. Đây là mã nguồn của tag, không dùng để cài ứng dụng.

## Quy trình

1. Đồng bộ phiên bản, cập nhật CHANGELOG và chỉ đưa những tệp thuộc đợt sửa lên Git.
2. Đóng gói ZIP và EXE từ cùng thư mục publish sạch. Thiếu trình biên dịch Inno Setup hoặc một tệp bắt buộc thì dừng.
3. Tạo manifest sau khi cả hai gói đã hoàn thành. Ghi SHA256SUMS sau khi đã chốt URL trong manifest.
4. Tải đủ bốn tệp lên bản nháp trên GitHub; giữ thêm một bản trong Actions Artifacts trong 14 ngày để khôi phục khi cần.
5. Tải lại cả bốn tệp, kiểm tra kích thước, hash, tên phiên bản và nội dung ZIP. Chỉ công bố khi mọi kiểm tra thành công.
6. Kiểm tra lại các asset công khai rồi gửi metadata ZIP tới WordPress.
7. Đọc lại API cập nhật và tải ZIP qua URL công khai để xác nhận hash. Chỉ báo hoàn tất khi bước này thành công.

Các trường `file`, `download_url`, `size`, `sha256` ở cấp ngoài của manifest luôn mô tả ZIP để tương thích ứng dụng cũ. `schema_version=2` bổ sung đối tượng `installer` cho EXE. Không đổi URL cập nhật trong ứng dụng sang EXE hoặc gói mã nguồn.

`package_project.ps1` chỉ đóng gói; không tự thông báo WordPress và không thay ứng dụng đang chạy. Dùng `tools/release.ps1 -ChangedPaths` với danh sách tệp cụ thể. Các tham số `-SkipPublish` và `-SkipLocalDeploy` được giữ để tương thích lời gọi cũ.

`tools/publish_release.ps1` chỉ thông báo khi cả bốn asset công khai khớp gói đã kiểm tra. Gói local chưa đăng hoặc có hash khác gói CI sẽ bị từ chối.

## Cài từ ZIP

Giải nén toàn bộ ZIP vào một thư mục rồi chạy `install.bat`. Không cần .NET SDK, Rust hoặc quyền quản trị để cài ứng dụng. ZIP và EXE cùng dùng thư mục `%LOCALAPPDATA%\PDF Pro`; nếu chỉ có thư mục cũ `PDFPro`, cả hai nâng cấp tại thư mục đó. Không xóa bản cũ trước khi kiểm tra gói. Khi thay tệp thất bại, script ZIP khôi phục các tệp vừa thay từ bản sao lưu.

`uninstall.bat` dùng trình gỡ của Inno Setup nếu có; nếu cài bằng ZIP, chỉ xóa các tệp đã ghi nhận trong `installed-files.json` và giữ dữ liệu người dùng. Bản ZIP cũ chưa có danh sách sẽ chỉ gỡ các tệp ứng dụng đã biết. Đăng ký/gỡ máy in ảo là thao tác riêng với `install.ps1 -RegisterVirtualPrinter` hoặc `uninstall.ps1 -RemoveVirtualPrinter`, cần quyền Windows tương ứng.

## Khi có lỗi

- Lỗi build hoặc tải tệp lên: bản mới chưa được thông báo trong ứng dụng. Sửa nguyên nhân trước khi tiếp tục.
- Kiểm tra bản nháp thất bại: giữ trạng thái nháp; không công bố hoặc cập nhật WordPress.
- GitHub đã công bố nhưng WordPress thất bại: tải đủ bốn asset của đúng tag về `releases/`, dùng `tools/publish_release.ps1` để gửi lại metadata và kiểm tra API. Không build lại hoặc thay tệp của bản đã công bố; không tăng phiên bản chỉ để thử lại một lỗi kết nối.
- Không sửa tag đã phát hành. Nếu sửa mã nguồn, tạo phiên bản mới.
- Bộ cài nâng cấp tại chỗ bằng cơ chế của Inno Setup; không xóa thư mục bản cũ hoặc khóa Registry khi mới mở trình cài đặt. Hủy ở màn hình hướng dẫn sẽ giữ bản đang cài.
