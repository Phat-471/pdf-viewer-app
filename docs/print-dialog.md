# Màn hình in PDF Pro

Màn hình in dùng bố cục hai cột: bản xem trước bên trái, thiết lập bên phải.
Các lựa chọn xử lý driver và bản vẽ CAD nằm trong mục Nâng cao.

## Cách dùng

1. Chọn máy in và số bản.
2. Chọn tất cả, trang hiện tại hoặc danh sách như `1–3, 5`.
3. Chọn khổ giấy, hướng giấy và tỉ lệ.
4. Quan sát đường nét đứt và cảnh báo cắt nội dung trước khi in.

Nút phóng to/thu nhỏ ở dưới bản xem trước chỉ đổi cách xem, không đổi tỉ lệ in.
Kích thước thật dùng 100% kích thước PDF gốc. Tỉ lệ tuỳ chỉnh cho phép từ 10% đến 400%.
Vừa vùng in sẽ thay đổi tỉ lệ bản vẽ; tỉ lệ áp dụng hiển thị dưới bản xem trước.

Snapshot dùng cùng màn hình in. Khổ giấy, hướng giấy, tỉ lệ và căn giữa được giữ
cho bố cục copy/lưu sau khi xác nhận in. Ảnh xuất theo bố cục giấy có lề tối thiểu
10 mm và giới hạn vùng in của máy in đã chọn. Xuất riêng vùng chọn giữ nguyên ảnh cắt.

## In file nặng

- Bật **Tối ưu bản vẽ CAD/Revit (file nặng)** ở ngay trên mục Nâng cao để chuyển
  vector thành ảnh theo DPI đã chọn. Tuỳ chọn này cũng dùng được khi in Snapshot.
- Trong **Nâng cao → Phương thức in**, chọn **In trực tiếp PDF** khi máy in hỗ trợ PDF.
  Các trang đã chọn, số bản và thứ tự trang được áp dụng. Khổ giấy, tỉ lệ, màu sắc
  và DPI do máy in xử lý; bản xem trước chỉ hiển thị trang PDF gốc.
- In trực tiếp PDF dùng cho toàn trang. Snapshot dùng phương thức PDF gốc hoặc ảnh
  để giữ đúng vùng chọn, khổ giấy và tỉ lệ.
- Snapshot cho phép gửi từng bản in thành lệnh riêng, bù lề và in khung kiểm tra.
  Mục đảo thứ tự trang được ẩn vì Snapshot chỉ có một trang.

## Giới hạn

- Khi chưa đọc được driver, bản xem trước ghi rõ lề tạm tính. Kết quả in phụ thuộc driver.
- Màu sắc trên giấy phụ thuộc khả năng máy in và thiết lập driver.
- Bản xem trước hiển thị nội dung PDF gốc. Chú thích trong ứng dụng được kèm khi chọn
  phương thức in ảnh; bản xem trước có thông báo về giới hạn này.
- 400 DPI là mặc định khi in ảnh. In PDF gốc giữ nét vector; DPI vẫn phụ thuộc máy in.
- Huỷ lệnh phải đợi driver kết thúc bước đang xử lý. Trang đã gửi có thể vẫn được in.
- Thông báo hoàn tất xác nhận đã gửi lệnh đến hệ thống in, chưa xác nhận bản giấy đã in xong.

Đợt này biên dịch và kiểm tra gói phát hành; chưa chạy thử giao diện hoặc in trên máy in vật lý.
