# Snapshot

## Quy trình sử dụng

1. Chọn **Snapshot**, kéo chọn một vùng trên trang PDF.
2. Xem trước vùng chọn trên giấy. Chọn khổ A0–A4 hoặc Letter, hướng ngang/dọc.
3. Chọn **Vừa trang, giữ tỷ lệ** hoặc nhập tỷ lệ từ 10% đến 400%.
   Tỷ lệ 100% giữ kích thước của vùng chọn trong PDF. Lề giấy là 10 mm.
4. Chọn thao tác:
   - **In…**: chọn máy in, số bản và chất lượng in. Bố cục lấy từ Snapshot.
   - **Copy ảnh**: dán ảnh bằng Ctrl+V. Có thể dùng Ctrl+C trong hộp thoại Snapshot.
   - **Lưu PNG**: chọn nơi lưu ảnh.
5. Hộp thoại vẫn mở sau khi in, copy hoặc lưu. **Chọn lại vùng** quay về trang PDF để kéo vùng mới.

## Ảnh xuất

- **Theo bố cục giấy** giữ khổ giấy, tỷ lệ và lề đã chọn; PNG có thông tin DPI tương ứng kích thước giấy.
- **Chỉ vùng chọn** xuất riêng nội dung được chọn, không thêm nền giấy hay lề.
- Cạnh dài tối đa: 1600, 2400 hoặc 4800 pixel. Vùng rất nhỏ có thể bị giới hạn độ phân giải render.
- Đường nét đứt của lề chỉ xuất hiện trong xem trước.
- Nếu tỷ lệ lớn hơn vùng giấy, nội dung ngoài lề bị cắt; hộp thoại sẽ báo để giảm tỷ lệ hoặc đổi khổ.

## In và hiệu năng

- Tải xem trước, tạo ảnh và chuẩn bị in chạy ở nền. Có thể đóng hộp thoại trong lúc chờ.
- Xem trước và ảnh xuất dùng lại trang PDF đã đọc; đổi khổ/tỷ lệ không cần đọc lại trang.
- Các bản in của cùng một lệnh dùng chung ảnh đã render. Bộ nhớ ảnh in được giới hạn.
- Nếu trình điều khiển không áp dụng khổ hoặc hướng giấy đã chọn, ứng dụng báo lỗi trước khi gửi trang in.
- Lề thực tế còn phụ thuộc vùng in được của máy in. Chưa kiểm chứng bằng bản in trên máy in vật lý.

## Menu chuột phải

Mục lỗi trong ảnh là `PDFHP.Merge` của ứng dụng PDF HP cũ. Bản mới sửa nhãn tiếng Việt
của mục này và `PdfPro.Merge` khi chúng đã tồn tại; giữ nguyên lệnh của từng mục.
Có thể sửa ngay bằng `tools/repair_explorer_context_menu.ps1`, có sao lưu nhãn trước khi ghi.
Đóng menu đang mở rồi mở lại để thấy nhãn mới.
