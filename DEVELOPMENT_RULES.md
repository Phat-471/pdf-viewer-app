# NGUYÊN TẮC PHÁT TRIỂN DỰ ÁN (DEVELOPMENT RULES)

Tài liệu này quy định các nguyên tắc cốt lõi về quản lý phiên bản, sao lưu mã nguồn, phong cách thiết kế giao diện và quy trình đóng gói của dự án PDF Pro. Tất cả các thay đổi trong tương lai đều phải tuân thủ nghiêm ngặt theo các nguyên tắc dưới đây.

---

## 1. Quy tắc quản lý Phiên bản (Versioning Rules)
Để dễ dàng phân biệt quy mô của các đợt cập nhật, mã phiên bản (phiên bản của cả WPF Client và WordPress Plugin) phải được tuân theo định dạng chuẩn `Major.Minor.Patch` (Ví dụ: `1.0.12`):

*   **Cập nhật nhỏ / Sửa lỗi (Patch Update):**
    *   *Áp dụng khi:* Sửa lỗi nhỏ, tối ưu hiệu năng nhẹ, chỉnh sửa nhỏ về giao diện mà không thêm tính năng lớn.
    *   *Cách tăng:* Chỉ tăng chữ số cuối cùng (Patch).
    *   *Ví dụ:* `v1.0.10` -> `v1.0.11` -> `v1.0.12`.
*   **Cập nhật lớn / Thêm tính năng mới (Minor/Major Update):**
    *   *Áp dụng khi:* Bổ sung một tính năng mới hoàn chỉnh (như in ấn CAD, zoom mượt, chèn chữ ký, đóng dấu watermark) hoặc thay đổi lớn về cấu trúc.
    *   *Cách tăng:* Tăng chữ số ở giữa (Minor) và reset chữ số cuối về 0.
    *   *Ví dụ:* `v1.0.12` -> `v1.1.0` -> `v1.1.1`.

> [!IMPORTANT]
> Khi thay đổi phiên bản, phải cập nhật đồng bộ ở 3 nơi:
> 1. [VERSION.txt](file:///e:/code/pdf/VERSION.txt) (File định danh chính).
> 2. [AssemblyInfo.cs](file:///e:/code/pdf/src/PdfViewerApp/Properties/AssemblyInfo.cs) (Phiên bản của phần mềm C#).
> 3. [Cargo.toml](file:///e:/code/pdf/src/PdfCore/Cargo.toml) (Phiên bản lõi Rust).

---

## 2. Quy tắc Sao lưu Mã nguồn (Backup Rules)
Để phòng ngừa rủi ro mất mát dữ liệu hoặc lỗi phát sinh không mong muốn trong quá trình chỉnh sửa code:

*   **Bắt buộc:** Trước khi sửa đổi bất kỳ tệp tin mã nguồn quan trọng nào (như `.cs`, `.xaml`, `.php`), **phải** tạo một bản sao lưu của tệp tin đó vào thư mục [backups/](file:///e:/code/pdf/backups/).
*   **Cách đặt tên thư mục backup:** Đặt tên thư mục theo định dạng tên phiên bản hiện tại kết hợp tên tính năng đang làm.
    *   *Ví dụ:* `backups/v1.0.10_print_backup/` chứa các tệp gốc của phiên bản `1.0.10` trước khi sửa tính năng in ấn.

---

## 3. Quy tắc Thiết kế Giao diện (UI/UX Design Rules)
Toàn bộ giao diện của ứng dụng WPF và trang quản trị (Admin Dashboard) của WordPress Plugin đều phải tuân thủ triết lý tối giản, cao cấp và đồng bộ:

*   **Chủ đề giao diện:** Giao diện tối cao cấp (Premium Dark Mode).
*   **Bảng màu chủ đạo:**
    *   Màu nền chính (Background): `#0F172A` (Màu xanh đá phiến tối).
    *   Màu nền panel con (Border/Card): `#111827` hoặc `#1E293B`.
    *   Màu chữ chính (Foreground): `#F8FAFC` (Trắng sáng dịu).
    *   Màu chữ phụ/chú thích: `#94A3B8` hoặc `#64748B` (Xám dịu).
*   **Các nút điều khiển (Buttons & Inputs):**
    *   Nút hành động chính (Primary Button): Bo góc 6px, sử dụng dải màu chuyển (Gradient) từ xanh ngọc `#0F766E` sang xanh neon `#14B8A6`. Hiệu ứng hover giảm nhẹ độ mờ để tạo cảm giác phản hồi.
    *   Nút phụ (Secondary Button): Nền tối `#1E293B`, viền mỏng `#334155`.
    *   Thanh tiến trình (ProgressBar) & Chỉ thị nổi bật: Sử dụng màu xanh sáng nổi bật `#38BDF8`.

---

## 4. Quy trình Biên dịch và Đóng gói (Build & Release Workflow)
Mỗi khi hoàn thành công việc sửa đổi mã nguồn:

1.  **Kiểm tra biên dịch:** Chạy file kịch bản `build_project.ps1` để đảm bảo hệ thống không có bất kỳ lỗi biên dịch nào.
2.  **Chạy thử nghiệm:** Thực hiện chạy kiểm thử thông qua `run_test.bat` để đảm bảo các chức năng hoạt động đúng mong muốn.
3.  **Tự động đóng gói:** Chạy file kịch bản `package_project.ps1` để biên dịch gói Rust Core dạng Release, xuất bản WPF Client ở dạng tự chạy độc lập (Self-contained win-x64), nén thành file ZIP trong thư mục `releases/` và tự động cập nhật mã hash SHA-256 vào tệp `update-manifest.json`.

---

## 5. Quy trình Tự động Cập nhật Phiên bản và Git (Auto-Update & Git Workflow)

> [!IMPORTANT]
> **Yêu cầu bắt buộc đối với tất cả Trợ lý Lập trình AI (AI Agents như Codex/Claude Code, Antigravity, Cursor):**
> Sau khi kết thúc bất kỳ thay đổi nào liên quan đến mã nguồn (tính năng mới hoặc sửa lỗi), AI Agent **PHẢI** tự động thực hiện quy trình sau mà không cần người dùng nhắc nhở:
>
> 1. **Tự động tăng số phiên bản (Auto-Bump Version):**
>    - Đọc file [VERSION.txt](file:///e:/code/pdf/VERSION.txt) hiện tại.
>    - Tăng số phiên bản Patch (Ví dụ: `1.2.2` -> `1.2.3`). Nếu thay đổi lớn hoặc thêm tính năng mới đáng kể thì tăng Minor version và reset Patch về 0 (Ví dụ: `1.2.2` -> `1.3.0`).
>    - Ghi phiên bản mới vào [VERSION.txt](file:///e:/code/pdf/VERSION.txt).
> 
> 2. **Cập nhật CHANGELOG.txt:**
>    - Viết nội dung thay đổi cực kỳ ngắn gọn, khái quát dưới dạng danh sách gạch đầu dòng vào [CHANGELOG.txt](file:///e:/code/pdf/CHANGELOG.txt) (Ví dụ: "- Nâng cấp server", "- Tối ưu trải nghiệm").
>    - **Lưu ý quan trọng:** Tuyệt đối không ghi chi tiết kỹ thuật hoặc mô tả chi tiết chức năng bên trong (tránh việc kẻ xấu biết rõ cấu trúc hệ thống để thực hiện các cuộc tấn công). Chỉ ghi nội dung thay đổi của phiên bản mới này, ghi đè hoặc ghi đè toàn bộ tệp bằng các thay đổi mới nhất này để công cụ đóng gói đọc chính xác.
> 
> 3. **Biên dịch & Đóng gói Thử nghiệm:**
>    - Chạy kịch bản `.\package_project.ps1` trên PowerShell để tự động đồng bộ phiên bản vào `AssemblyInfo.cs` và đóng gói thử nghiệm nhằm phát hiện lỗi biên dịch.
> 
> 4. **Tự động Commit & Push lên Git:**
>    - Thực hiện lưu trữ tất cả tệp thay đổi và tệp manifest cập nhật:
>      ```powershell
>      git add .
>      git commit -m "Update: [Tóm tắt thay đổi] - Phiên bản v[Phiên bản mới]"
>      ```
>    - Tạo thẻ Git Tag mới khớp với phiên bản vừa tăng:
>      ```powershell
>      git tag "v[Phiên bản mới]"
>      ```
>    - Đẩy mã nguồn và thẻ tag lên kho chứa từ xa (GitHub):
>      ```powershell
>      git push origin master
>      git push origin "v[Phiên bản mới]"
>      ```
>      *(Việc đẩy tag `v*` sẽ tự động kích hoạt tiến trình GitHub Actions CI/CD để xây dựng ứng dụng và phát hành bản cập nhật lên máy chủ bản quyền WordPress).*
