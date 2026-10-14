# Sửa chữ gốc trong PDF

## Cách dùng

1. Mở PDF, chọn **Biên tập → Sửa chữ gốc**.
2. Chọn khung xanh quanh đoạn chữ. Khung cam có thông báo về giới hạn của vùng chữ.
3. Nhập nội dung mới. Hộp thoại hiển thị tên font và cỡ chữ lấy từ PDF.
4. Nhấn **Xem trước**. Bản xem trước được dựng từ PDF, gồm cả nội dung lân cận.
5. Nhấn **Áp dụng**, rồi **Lưu** hoặc **Lưu dưới dạng**. Ctrl+Z hoàn tác; Ctrl+Y làm lại.

Để trống nội dung để xóa đối tượng chữ. Chữ mới dài hơn không tự giảm cỡ chữ; cần xem trước để tránh đè lên nội dung bên cạnh. Đóng tài liệu có chữ chưa lưu sẽ có lựa chọn lưu, bỏ thay đổi hoặc hủy đóng.

## Phạm vi hỗ trợ

- Sửa đối tượng chữ trực tiếp trên trang, giữ tài nguyên font, cỡ chữ, màu và ma trận vị trí của đối tượng gốc.
- Chọn từng đối tượng; không thay toàn bộ các đoạn có cùng nội dung trong trang.
- Font subset có thể thiếu ký tự. Nội dung không mã hóa được bằng font gốc bị từ chối, không tự thay font.
- Font không nhúng phụ thuộc font có trên máy; không bảo đảm giống nhau giữa các máy.
- PDF scan hoặc chữ đã chuyển thành nét vẽ không có đối tượng chữ gốc để sửa. OCR không khôi phục chính xác font từ ảnh.
- Chữ trong khối dùng chung, PDF không cho sửa và PDF có chữ ký số được thông báo rõ; chưa hỗ trợ sửa các trường hợp này.
- Mỗi đối tượng nhận một dòng. Chưa hỗ trợ thêm dòng, dàn lại đoạn văn hoặc emoji.

## Cơ chế

`PdfNativeTextEditor` chọn đối tượng theo chỉ số trang và chỉ số đối tượng, đối chiếu nội dung gốc trước khi sửa. PDFium thay nội dung chữ trên chính đối tượng. Không phủ nền trắng, không dùng ảnh thay chữ và không suy đoán font từ chiều cao OCR.

`PdfRenderSession` nhận danh sách sửa bất biến. Khóa cache gồm nội dung sửa để ảnh cũ không xuất hiện sau khi sửa hoặc hoàn tác. Vùng chọn được quy đổi bằng PDFium để theo đúng góc xoay và vùng cắt trang.

Lưu tạo PDF tạm, mở lại để đối chiếu số trang, nội dung, font và cỡ chữ, rồi thay file đích bằng thao tác thay file nguyên tử. Lỗi lưu giữ danh sách sửa để có thể thử lại. In và Snapshot tạo bản làm việc có nội dung sửa, không tự ghi lên PDF gốc.

## Mẫu đã đọc

Đọc cấu trúc 11 PDF trong thư mục người dùng cung cấp. Các trang mẫu không có Form XObject. File A-2251 có một trang, 338 lệnh chữ, 6 font nhúng dạng subset, gồm Arial Narrow và Arial Narrow Bold.

Đã biên dịch mã nguồn. Chưa chạy kiểm thử thao tác sửa/lưu hoặc in trên các PDF mẫu; cần kiểm chứng thực tế trước khi kết luận tương thích đầy đủ với Revit.
