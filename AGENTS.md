# Quy trình bắt buộc cho PDF Pro

Đọc `DEVELOPMENT_RULES.md` trước khi thay đổi mã nguồn.

Người dùng đã yêu cầu: sau mỗi lần hoàn tất thay đổi, đưa code lên Git và phát hành
bản cập nhật để người dùng nhận được trong ứng dụng. Yêu cầu này là uỷ quyền thực hiện
commit, push và release; không hỏi lại cho mỗi đợt sửa.

1. Sao lưu trước khi sửa các tệp quan trọng.
2. Đồng bộ phiên bản trong `VERSION.txt`, `AssemblyInfo.cs`, `Cargo.toml` và Cargo.lock.
   Nếu phiên bản cho đợt này đã tăng nhưng chưa phát hành, tiếp tục dùng phiên bản đó.
3. Cập nhật CHANGELOG ngắn gọn; build và kiểm tra gói cùng SHA-256.
4. Chỉ stage các tệp thuộc công việc đang thực hiện. Giữ nguyên thay đổi ngoài phạm vi.
   Không đưa token, `publish_settings.json` hoặc `tools/publish_config.json` lên Git.
5. Commit, tạo tag `vX.Y.Z`, push nhánh `master` và tag. Không sửa hoặc force-push tag đã phát hành.
6. Theo dõi `.github/workflows/release.yml` đến khi hoàn tất. Workflow phải tạo GitHub Release,
   đăng bộ cài EXE, ZIP cập nhật, manifest và SHA256SUMS.txt, rồi thông báo phiên bản tới máy chủ WordPress.
   Bộ cài là thành phần bắt buộc. Thiếu một tệp hoặc sai hash phải dừng phát hành.
   Tạo bản nháp, tải lại và kiểm tra đủ bốn tệp trước khi công bố; sau đó mới gửi thông báo cập nhật.
7. Đọc lại API `GET /wp-json/pdfpro/v1/update-check`; xác nhận phiên bản, URL tải,
   kích thước và SHA-256 khớp đúng gói được tải thực tế.
8. Chỉ báo đã phát hành khi các bước trên thành công. Nếu có lỗi bên ngoài,
   báo rõ bước đã xong và bước còn bị chặn.

`tools/release.ps1` hỗ trợ quy trình này với danh sách `-ChangedPaths` cụ thể.
Dùng `-UseCurrentVersion` khi phiên bản đã chuẩn bị. GitHub Actions tạo gói dùng cho
người dùng; không lấy hash của gói local thay cho hash của gói được CI dựng.

Đóng gói trong quá trình phát hành với `-SkipPublish -SkipLocalDeploy`;
thông báo cập nhật chỉ được gửi sau khi file tải đã có trên GitHub Release.
Ứng dụng cập nhật bằng ZIP; bộ cài EXE dành cho cài mới hoặc cài lại. Hai gói phải cùng phiên bản và cùng đầu ra build.
