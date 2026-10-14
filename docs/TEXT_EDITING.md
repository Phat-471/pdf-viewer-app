# Sửa chữ gốc trong PDF

## Cách dùng

1. Mở PDF, chọn **Biên tập → Sửa chữ gốc**.
2. Chọn khung xanh quanh đoạn chữ. Khung cam có thông báo về giới hạn của vùng chữ.
3. Nhập nội dung mới. Hộp thoại hiển thị tên font và cỡ chữ lấy từ PDF.
4. Bản xem trước tự làm mới khi ngừng nhập; có thể nhấn **Xem trước**. Ảnh mờ là bản trước đó, chưa được áp dụng.
5. Nếu font nhúng thiếu ký tự, chọn **Dùng font đầy đủ** để nạp bản cùng tên. Noto Sans đầy đủ được kèm theo ứng dụng, có hỗ trợ tiếng Việt. Nếu chưa tìm thấy font phù hợp, chọn **Chọn font TTF…** và kiểm tra tên font cùng bản xem trước.
6. Nhấn **Áp dụng**, rồi **Lưu** hoặc **Lưu dưới dạng**. Ctrl+Z hoàn tác; Ctrl+Y làm lại.

Để trống nội dung để xóa đối tượng chữ. Chữ mới dài hơn không tự giảm cỡ chữ; cần xem trước để tránh đè lên nội dung bên cạnh. Đóng tài liệu có chữ chưa lưu sẽ có lựa chọn lưu, bỏ thay đổi hoặc hủy đóng.

## Phạm vi hỗ trợ

- Sửa đối tượng chữ trực tiếp trên trang, giữ tài nguyên font, cỡ chữ, màu và ma trận vị trí của đối tượng gốc.
- Chọn từng đối tượng; không thay toàn bộ các đoạn có cùng nội dung trong trang.
- Font subset có thể thiếu ký tự hoặc bảng mã. Nội dung không mã hóa được bằng font nhúng được thông báo và có lựa chọn nạp font đầy đủ; không tự thay font.
- Font đầy đủ chỉ được nhúng khi có quyền nhúng để chỉnh sửa và có đủ ký tự. Hỗ trợ TTF dạng tĩnh, chưa hỗ trợ OTF, TTC và font biến thiên. Font được giữ trong danh sách sửa để lưu, hoàn tác, in và Snapshot dùng cùng dữ liệu.
- Khi nạp font đầy đủ, giữ cỡ chữ, màu và ma trận vị trí; không tự co chữ. Bản đầy đủ cùng tên có thể khác phiên bản font nhúng, nên cần xem trước. Khoảng cách chữ đặc biệt của nguồn có thể khác khi dựng lại đối tượng.
- Chưa nạp font đầy đủ cho chữ có lớp cắt, thẻ cấu trúc, độ trong suốt hoặc nét viền. Nếu chữ mới giao với đối tượng vẽ sau nó, yêu cầu rút ngắn nội dung để giữ kết quả hiển thị và lưu nhất quán.
- Font không nhúng phụ thuộc font có trên máy; không bảo đảm giống nhau giữa các máy.
- PDF scan hoặc chữ đã chuyển thành nét vẽ không có đối tượng chữ gốc để sửa. OCR không khôi phục chính xác font từ ảnh.
- Chữ trong khối dùng chung, PDF không cho sửa và PDF có chữ ký số được thông báo rõ; chưa hỗ trợ sửa các trường hợp này.
- Mỗi đối tượng nhận một dòng. Chưa hỗ trợ thêm dòng, dàn lại đoạn văn hoặc emoji.

## Cơ chế

`PdfNativeTextEditor` chọn đối tượng theo chỉ số trang và chỉ số đối tượng, đối chiếu nội dung gốc trước khi sửa. Khi dùng font nhúng, PDFium thay nội dung trên chính đối tượng. Khi chọn font đầy đủ, nhúng font Unicode và tạo đối tượng thay thế với cỡ chữ, màu và ma trận gốc. Không phủ nền trắng hoặc dùng ảnh thay chữ.

`PdfRenderSession` nhận danh sách sửa bất biến. Khóa cache gồm nội dung sửa để ảnh cũ không xuất hiện sau khi sửa hoặc hoàn tác. Vùng chọn được quy đổi bằng PDFium để theo đúng góc xoay và vùng cắt trang.

Lưu tạo PDF tạm, mở lại để đối chiếu số trang, từng nội dung chữ, font, cỡ chữ, màu và vị trí, rồi thay file đích bằng thao tác thay file nguyên tử. Lỗi lưu giữ danh sách sửa để có thể thử lại. In và Snapshot tạo bản làm việc có nội dung sửa, không tự ghi lên PDF gốc.

## Mẫu đã đọc

Đọc cấu trúc 11 PDF trong thư mục người dùng cung cấp. Các trang mẫu không có Form XObject. File A-2251 có một trang, 338 lệnh chữ, 6 font nhúng dạng subset, gồm Arial Narrow và Arial Narrow Bold.

Đã đọc thêm file hóa đơn người dùng chỉ định trong `E:\hp`: Noto Sans nhúng dạng subset, không có bảng cmap trong chương trình font. Vùng tên công ty dùng cỡ chữ 9 pt, không có lớp cắt hoặc độ trong suốt trên đối tượng.

Đã biên dịch mã nguồn. Chưa chạy kiểm thử thao tác sửa/lưu hoặc in trên các PDF mẫu; cần kiểm chứng thực tế trước khi kết luận tương thích đầy đủ.
