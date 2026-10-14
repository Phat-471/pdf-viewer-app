# Đọc chữ và OCR

## Chọn phạm vi

- Công cụ xuất OCR cho phép chọn trang hiện tại, toàn tài liệu hoặc vùng Snapshot đang chọn.
- Trong Snapshot, chọn **Đọc chữ OCR…** để đọc riêng vùng đã chọn, rồi copy hoặc lưu TXT.
- Xuất PDF có thể tìm kiếm áp dụng cho trang hiện tại hoặc toàn tài liệu. Các trang còn lại được giữ trong file xuất.

## Thiết lập

- Ngôn ngữ: tiếng Việt, tiếng Anh hoặc Việt + Anh. Bộ cài và ZIP chứa dữ liệu
  `tessdata_fast` 4.1.0 chính thức; không cần tải dữ liệu để dùng hai ngôn ngữ này.
- DPI: 150, 300 hoặc 400. Mặc định 300. DPI cao giữ được nhiều chi tiết chữ nhỏ hơn,
  đồng thời tăng số ô ảnh và thời gian xử lý.
- **Ưu tiên lớp chữ gốc**: lấy chữ và vị trí từ PDF khi có chữ dùng được.
  Với PDF vừa có chữ vừa có vùng scan, chọn riêng vùng scan hoặc bỏ tuỳ chọn này để chạy OCR ảnh.
- **Chữ rời trên bản vẽ CAD/Revit** dùng chế độ nhận diện chữ rời của Tesseract.
  Bỏ chọn khi xử lý trang văn bản thông thường.
- Thiết lập được nhớ cho lần sau.

## Xử lý và cache

- Ảnh được dựng theo DPI vật lý, chia ô tối đa 2048 pixel với phần giao nhau 160 pixel.
  Không thu cả trang A1 xuống 1800 pixel. Kết quả trùng trong vùng giao nhau được lọc.
- Windows OCR là phương án dự phòng cho một ngôn ngữ nếu Tesseract không dùng được;
  kích thước ô được giới hạn bởi `OcrEngine.MaxImageDimension`. Không tự thay ngôn ngữ đã chọn.
- Có tiến độ theo từng ô và nút huỷ. Đóng hoặc đổi tài liệu huỷ OCR của tài liệu cũ.
  Lời gọi native đang xử lý phải kết thúc trước khi huỷ có hiệu lực.
- Kết quả hoàn tất được lưu trên máy, theo SHA-256 nội dung PDF, trang/vùng,
  ngôn ngữ, DPI, chế độ nhận diện và dữ liệu OCR. Không lưu kết quả bị huỷ hoặc rỗng.
- PDF tìm kiếm thêm lớp chữ ẩn có ánh xạ Unicode; không thêm lại lớp chữ gốc
  khi bật ưu tiên chữ gốc. Vị trí ảnh OCR được quy đổi về hệ toạ độ PDF.
- Kết quả TXT/DOCX/PDF được ghi qua file tạm, chỉ thay file đích khi hoàn tất.

## Giới hạn

- Chữ quá nhỏ, bản scan mờ, chữ nghiêng và đường bản vẽ chồng lên chữ có thể gây sai nhận diện.
- Thứ tự đọc trên bản vẽ nhiều cột và các chữ xoay có thể cần chỉnh lại trong kết quả.
- Trang vượt 40.000 pixel ở DPI đã chọn sẽ được yêu cầu giảm DPI.
- Cache ảnh và chữ dùng chung ngân sách 256 MB tại `%LOCALAPPDATA%/PdfPro/document-cache-v2`.
  Đây là dữ liệu trên máy, không được gửi tới dịch vụ OCR trực tuyến.
- Đợt này biên dịch WPF/Rust và kiểm tra gói phát hành; chưa chạy thử giao diện hoặc
  đo lại tốc độ/độ chính xác trên tập PDF thực tế.

Tài liệu tham khảo: [Tesseract về chất lượng ảnh và chế độ nhận diện](https://tesseract-ocr.github.io/tessdoc/ImproveQuality.html),
[dữ liệu tessdata_fast](https://github.com/tesseract-ocr/tessdata_fast/tree/4.1.0),
[Windows OCR](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine).
