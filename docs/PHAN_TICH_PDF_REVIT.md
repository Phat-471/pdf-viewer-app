# PDF Revit: đọc, thu phóng, in và OCR

Ngày phân tích: 13/10/2026. Bản sửa trong workspace: 1.7.1.

## 1. File đã phân tích

`KUL074-KM-01-01M-SD-A-2251.pdf`, 43.384.832 byte, một trang,
2383,94 × 1683,78 point, gần khổ A1 ngang.

Đo trực tiếp bằng `libs/pdfium.dll`, trong tiến trình riêng, không gửi lệnh in.
Các số dưới đây là một lần đo trên máy hiện tại, không phải cam kết thời gian cho mọi file.

| Thao tác | Thời gian |
|---|---:|
| Mở document | 0,5 ms |
| Đọc kích thước bằng chỉ số trang | 0,1 ms |
| Phân tích nội dung trang | 5.747,7 ms |
| Dựng toàn trang 1600 × 1130 pixel | 8.266,1 ms |
| Dựng ô 1024 × 1024 tại tỷ lệ toàn trang 6063 × 4282 | 944,5 ms |
| Dựng ô tiếp theo, dùng lại trang đã đọc | 1.154,3 ms |
| Đọc lớp chữ từ trang đã phân tích | 283,0 ms |

PDFium đọc được **5.205 ký tự**. Đây là lượng ký tự trong lớp chữ,
chưa đánh giá đầy đủ thứ tự đọc hay độ đúng của mọi ký tự.

Kết quả cho thấy chi phí chính nằm ở nội dung đồ hoạ phức tạp.
Dung lượng file và số trang không đủ để dự đoán tốc độ hiển thị.

## 2. Nguyên nhân trong mã nguồn

- Đọc thông tin trang bằng cách mở nội dung từng trang làm chậm bước mở file.
- Mỗi lần dựng ảnh, thumbnail hoặc preview có thể mở và phân tích lại cùng trang.
- Preview in và một số truy vấn driver máy in chạy ngay trên luồng giao diện.
  Hộp thoại chưa kịp vẽ đã bị chặn, phù hợp với ảnh chụp cửa sổ trắng `Not Responding`.
- Zoom dựng lại các khung trang và lớp chú thích; ảnh toàn trang có giới hạn độ phân giải.
  Phóng lớn ảnh này làm nét và chữ bị mờ.
- Hàng đợi in cũ có thể mất lỗi từ tiến trình chuẩn bị trang hoặc mắc kẹt khi phía gửi lệnh in gặp lỗi.
- In ảnh giữ nhiều bản sao bộ nhớ và ép GC trên mỗi trang.

## 3. Những thay đổi đã thực hiện

### Đọc và hiển thị

- Đọc kích thước trang bằng API metadata khi có thể; việc mở và đọc chạy nền.
- Giữ tối đa hai trang native đã phân tích trong mỗi phiên render.
- Giữ ảnh toàn trang làm nền, với ngân sách 2,5 triệu pixel.
- Dựng thêm các ô chi tiết 1024 pixel cho vùng đang nhìn, theo zoom và DPI màn hình.
  Các ô được dựng lần lượt từ trung tâm; vùng chưa hoàn tất vẫn có ảnh nền.
- Tái sử dụng ảnh đã dựng cho thumbnail và preview in.
- Quản lý cache ảnh bằng LRU; xoá lớp chi tiết của trang ở xa màn hình.
- Dùng chung mutex cho PDFium. Tài liệu API của PDFium yêu cầu tránh gọi đồng thời
  từ nhiều luồng: [fpdfview.h](https://pdfium.googlesource.com/pdfium/+/refs/heads/chromium/7095/public/fpdfview.h).

### Zoom

- Dùng transform trên ảnh hiện có trong lúc thao tác.
- Khi thao tác dừng, cập nhật kích thước các khung trang hiện có.
- Giảm số lần cập nhật thước và truy vấn viewport.
- Giữ điểm neo thu phóng; vô hiệu hoá kết quả chi tiết cũ khi zoom thay đổi.

### In

- Hiện hộp thoại trước khi tải máy in; danh sách máy in, mặc định driver và preview chạy nền.
- Tạo PrintQueue, PrintTicket và các đối tượng WPF phục vụ in trên cùng một luồng STA riêng.
  Giao diện chính không thực hiện `PrintDocument` đồng bộ.
- In vùng chọn cũng được chuyển sang luồng STA riêng.
- Đường in native xử lý một trang mỗi lần, truyền lỗi về giao diện và giải phóng trang khi có lỗi.
- In ảnh native dựng trực tiếp BGR, giảm việc nhân đôi buffer.
- Giới hạn ảnh native 48 triệu pixel, ảnh WPF 64 triệu pixel; tính giới hạn sau khi khớp khổ giấy.
- In vector giữ nội dung vector, phù hợp với bản vẽ cần nét khi phóng hoặc in khổ lớn.

## 4. Cách dùng và giới hạn

1. Với bản vẽ Revit, ưu tiên **Native PDFium** để in vector.
2. Nếu driver xử lý vector quá chậm, thử chế độ tối ưu CAD/in dạng ảnh ở 300–600 DPI.
   Ảnh rất lớn có thể bị giảm độ phân giải để giữ ngân sách bộ nhớ.
3. Nếu cần in chú thích được thêm trong ứng dụng, dùng **WPF Bitmap**.
   Đường in native hiện không vẽ lớp chú thích riêng của ứng dụng.
4. Sau khi zoom, vùng chi tiết xuất hiện dần. Thời gian khoảng một giây là cho một ô đã đo,
   không phải toàn bộ viewport.
5. Lần mở đầu vẫn cần phân tích và dựng trang. Không thể loại bỏ chi phí này chỉ bằng cache RAM.

Chưa kiểm chứng bản sửa với máy in vật lý và driver đang dùng trong ảnh chụp.
Luồng nền giúp giao diện phản hồi; nó không bảo đảm driver hoặc spooler luôn hoàn tất nhanh.
Huỷ tác vụ chỉ có hiệu lực giữa các bước có kiểm tra cancellation;
lệnh native đang dựng trang hoặc lời gọi driver đang bị chặn có thể cần chờ.

Đã build WPF/Rust và đóng gói bản local. Không gửi lệnh in trong quá trình đo.
Các thay đổi được sao lưu tại `backups/v1.7.0_revit_render_print_backup/`.
`profile.json` chứa số đo; `base.png` và `detail.png` là ảnh dựng trực tiếp từ PDF.

## 5. OCR

Dự án đã có Tesseract và Windows.Media.Ocr. Đường OCR hiện dựng toàn trang rộng 1800 pixel.
Với trang rộng khoảng 33,1 inch như file này, độ phân giải chỉ khoảng **54 DPI**,
không phù hợp để nhận diện chữ nhỏ trên bản vẽ A1.

Hướng cải thiện đề xuất, chưa triển khai trong bản sửa này:

- Ưu tiên lấy lớp chữ gốc cho PDF có chữ; file mẫu đã có 5.205 ký tự.
- Chỉ chạy OCR cho trang scan hoặc vùng không có lớp chữ dùng được.
- Với bản vẽ, chọn vùng cần đọc hoặc chia ô ở 300 DPI, có phần giao nhau,
  gộp kết quả và loại chữ trùng; không co cả trang A1 xuống 1800 pixel.
- Cho phép chọn `eng`, `vie`, hoặc `vie+eng`; chọn chế độ phân đoạn phù hợp chữ rời trên bản vẽ.
- Có tiến độ và huỷ; lưu cache theo nội dung file, vùng, ngôn ngữ và cấu hình OCR.
- Kiểm tra `OcrEngine.MaxImageDimension` trước khi dùng Windows OCR.

Tesseract khuyến nghị ảnh ít nhất 300 DPI và xử lý ảnh nghiêng:
[ImproveQuality](https://tesseract-ocr.github.io/tessdoc/ImproveQuality.html).
Windows OCR cung cấp giới hạn kích thước ảnh và danh sách ngôn ngữ có trên thiết bị:
[OcrEngine](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine).

OCR phục vụ nhận diện chữ. Chất lượng nét vẽ và tốc độ zoom cần giải quyết ở đường render.
