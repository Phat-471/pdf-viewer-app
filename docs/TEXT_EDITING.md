# Sửa chữ gốc trong PDF

## Cách dùng

1. Mở PDF, chọn **Biên tập → Sửa chữ gốc**.
2. Chọn khung xanh quanh đoạn chữ. Khung cam có thông báo về giới hạn của vùng chữ.
3. Đặt con trỏ vào chữ và nhập trực tiếp trên trang. Có thể kéo chọn ký tự, dùng phím mũi tên, copy/paste và Ctrl+Z trong ô nhập. Thanh công cụ hiển thị font và cỡ chữ thực tế sau ma trận phóng đại của PDF.
4. Khi ngừng gõ, ảnh **Bản xem trước PDF** tự làm mới. Ảnh này được dựng bằng font của đối tượng PDF; ô nhập là bề mặt tạm và có thể hiển thị khác font nhúng. Nút **Áp dụng** chỉ bật khi bản xem trước khớp nội dung và lựa chọn font hiện tại; ảnh cũ được xóa khi gõ tiếp.
5. Mặc định **Giữ font · tự tìm bản đầy đủ**: nếu font nhúng không mã hóa được nội dung mới, tự tìm TTF đầy đủ cùng họ và kiểu chữ. Muốn giữ riêng font nhúng, chọn **Chỉ dùng font nhúng gốc**. Nếu bản đầy đủ cũng thiếu tiếng Việt, chọn **Đổi sang Noto Sans · tiếng Việt** hoặc **TTF…**. Đổi sang họ font khác sẽ thay hình dáng và chiều rộng chữ; thông báo hiển thị màu vàng.
6. Nhấn **Áp dụng** hoặc Enter/Ctrl+Enter, rồi **Lưu** hoặc **Lưu dưới dạng**. Esc hủy nội dung đang nhập. Ctrl+Z/Ctrl+Y sau khi áp dụng hoàn tác/làm lại thay đổi của tài liệu. Ctrl+S và Ctrl+P hoạt động trong ô nhập; lưu và in cần bản xem trước hợp lệ trước khi nhận nội dung đang nhập.

Để trống nội dung để xóa đối tượng chữ. Chữ mới dài hơn không tự giảm cỡ chữ; cần xem trước để tránh đè lên nội dung bên cạnh. Đóng tài liệu có chữ chưa lưu sẽ có lựa chọn lưu, bỏ thay đổi hoặc hủy đóng.

## Phạm vi hỗ trợ

- Sửa đối tượng chữ trực tiếp trên trang, giữ tài nguyên font, cỡ chữ, màu và ma trận vị trí của đối tượng gốc.
- Chọn từng đối tượng; không thay toàn bộ các đoạn có cùng nội dung trong trang.
- Font subset có thể thiếu ký tự hoặc bảng mã. Chế độ tự động chỉ dùng bản đầy đủ cùng họ và kiểu chữ; chọn họ khác cần thao tác rõ ràng của người dùng.
- Font đầy đủ chỉ được nhúng khi có quyền nhúng để chỉnh sửa và có đủ ký tự. Hỗ trợ TTF dạng tĩnh, chưa hỗ trợ OTF, TTC và font biến thiên. Font được giữ trong danh sách sửa để lưu, hoàn tác, in và Snapshot dùng cùng dữ liệu.
- Khi nạp font đầy đủ, giữ cỡ chữ, màu và ma trận vị trí; không tự co chữ. Với dòng ngang có ánh xạ một ký tự/một glyph rõ ràng và số ký tự không đổi, giữ vị trí từng ký tự gốc, kể cả khi dòng được xoay. Ví dụ thay LEVEL 1 thành LEVEL 2 giữ vị trí các ký tự còn lại. Khi độ dài thay đổi hoặc không đọc được vị trí rõ ràng, dùng khoảng cách của font; thanh công cụ thông báo cơ chế đang dùng.
- Bản đầy đủ cùng tên có thể khác phiên bản font nhúng. Có thể khác từng pixel; cần kiểm tra bản xem trước. Ô nhập giữ cỡ đọc tối thiểu 12 px để dễ thao tác, cỡ lưu trong PDF vẫn giữ nguyên.
- Khi thay font, giữ nguyên thẻ đánh dấu nội dung và tham số của thẻ trong cùng tài liệu, gồm thông tin vùng nhìn của Revit. Chưa sửa trực tiếp chữ có lớp cắt, độ trong suốt hoặc nét viền. Nếu chữ mới giao với đối tượng vẽ sau nó khi nạp font, yêu cầu rút ngắn nội dung để giữ kết quả hiển thị và lưu nhất quán.
- Font không nhúng phụ thuộc font có trên máy; không bảo đảm giống nhau giữa các máy.
- PDF scan hoặc chữ đã chuyển thành nét vẽ không có đối tượng chữ gốc để sửa. OCR không khôi phục chính xác font từ ảnh.
- Chữ trong khối dùng chung, PDF không cho sửa và PDF có chữ ký số được thông báo rõ; chưa hỗ trợ sửa các trường hợp này.
- Mỗi đối tượng nhận một dòng. Chưa hỗ trợ thêm dòng, dàn lại đoạn văn hoặc emoji.

## Cơ chế

`PdfNativeTextEditor` chọn đối tượng theo chỉ số trang và chỉ số đối tượng, đối chiếu nội dung gốc trước khi sửa. Khi dùng font nhúng, PDFium thay nội dung trên chính đối tượng. Khi chọn font đầy đủ, nhúng font Unicode và tạo đối tượng thay thế với cỡ chữ, màu và ma trận gốc. Không phủ nền trắng hoặc dùng ảnh thay chữ.

`PdfRenderSession` nhận danh sách sửa bất biến. Khóa cache gồm nội dung sửa để ảnh cũ không xuất hiện sau khi sửa hoặc hoàn tác. Vùng chọn được quy đổi bằng PDFium để theo đúng góc xoay và vùng cắt trang.

`PreviewSession` mở tài liệu và trang một lần cho mỗi lượt sửa. Mỗi bản xem trước tạo đối tượng chữ tạm, kiểm tra nội dung Unicode, dựng bitmap riêng của chữ rồi trả đối tượng gốc về trang. Không dựng toàn bộ bản vẽ cho mỗi phím gõ. Việc dựng chạy trên nền dưới khóa PDFium; kết quả cũ không cho phép áp dụng nội dung mới. Tài liệu xem trước được đóng khi áp dụng, hủy hoặc đóng tài liệu.

Vùng chữ được tìm từ cuối danh sách đối tượng và dừng khi tìm đủ chữ; cách này giảm số lần gọi native trên bản vẽ Revit đặt chữ sau hàng triệu nét vẽ. Trường hợp chữ trong khối dùng chung vẫn được đánh dấu chưa hỗ trợ.

Lưu tạo PDF tạm, mở lại để đối chiếu số trang, từng nội dung chữ, font, cỡ chữ, màu, ma trận và vị trí ký tự đọc được, rồi thay file đích bằng thao tác thay file nguyên tử. Lỗi lưu giữ danh sách sửa để có thể thử lại. In và Snapshot tạo bản làm việc có nội dung sửa, không tự ghi lên PDF gốc.

Tham khảo thao tác **Edit Text/Edit Object** của [Foxit PhantomPDF 10.0](https://cdn01.foxitsoftware.com/pub/foxit/manual/phantom/en_us/FoxitPhantomPDF10.0_Manual.pdf), trang 153–154 và 162. Ảnh người dùng là PhantomPDF với biểu tượng tím; chưa xác định phiên bản cài đặt từ ảnh. Bản hiện tại sửa từng đối tượng một dòng, chưa có dàn lại đoạn văn như PhantomPDF.

## Mẫu đã đọc

Đọc cấu trúc 11 PDF trong thư mục người dùng cung cấp. Các trang mẫu không có Form XObject. File A-2251 có một trang, 338 lệnh chữ, 6 font nhúng dạng subset, gồm Arial Narrow và Arial Narrow Bold.

Đã đọc thêm file hóa đơn người dùng chỉ định trong `E:\hp`: Noto Sans nhúng dạng subset, không có bảng cmap trong chương trình font. Vùng tên công ty dùng cỡ chữ 9 pt, không có lớp cắt hoặc độ trong suốt trên đối tượng.

Các nhãn STAIRCASE 04 và R.01M.221 trong bản vẽ Revit lưu cỡ chữ 1 pt với hệ số phóng đại gần 8, tương ứng khoảng 8 pt khi hiển thị. Mỗi đối tượng có hai thẻ, gồm thẻ vùng nhìn có tham số dạng từ điển. Arial Narrow 2.40 cài trên máy mẫu thiếu ký tự “ạ”; bản đầy đủ cùng tên không thể viết đủ “chào bạn”. Chọn Noto Sans là lựa chọn đổi font rõ ràng, không tự thay font gốc.

Đã biên dịch mã nguồn. Chưa chạy kiểm thử thao tác sửa/lưu hoặc in trên các PDF mẫu; cần kiểm chứng thực tế trước khi kết luận tương thích đầy đủ.

## Sửa lỗi trong 1.13.2

Đọc tên font, quyền nhúng và ký tự hỗ trợ trực tiếp từ các bảng `name`, `OS/2`, `cmap` của đúng file TTF. Trên máy mẫu, `GlyphTypeface` trả tên “Noto Sans Thin Condensed / Regular” cho hai file Noto Sans Regular và Bold, trong khi bảng tên của chúng đúng là Noto Sans Regular và Bold. Dữ liệu Windows này không còn quyết định font được nhúng. Tìm font ưu tiên bộ font đi kèm, thư mục font hệ thống và font của người dùng; so sánh cả tên PostScript, giữ họ và kiểu chữ, chỉ lưu cache kết quả tìm được.

Làm mới trang sau khi áp dụng sẽ xóa cả lớp ảnh nét cao. Khóa của ảnh nét cao, ảnh trang và thumbnail có phiên sửa chữ để không dùng lại chữ cũ. Ô nhập đủ chiều cao cho cả dòng chữ và dấu tiếng Việt; thanh cuộn ngang không che dòng nhập.

Nếu áp dụng lỗi, danh sách sửa trước đó được khôi phục và nội dung đang nhập vẫn được giữ. Chỉ thêm bước hoàn tác và đóng ô sửa sau khi trang đã mở lại với nội dung mới. Nhấn Lưu để ghi thay đổi vào PDF.

Bộ đọc TTF hỗ trợ bảng Unicode cmap 4, 12 và 13 cho ký tự BMP; vẫn chưa hỗ trợ OTF, TTC, font biến thiên hoặc emoji. Đã đối chiếu dữ liệu font và đọc mã; chưa chạy kiểm thử thao tác sửa/lưu trên giao diện.

## Sửa lỗi trong 1.13.3

Font đầy đủ được nạp bằng `FPDFText_LoadCidType2Font`: mỗi mã Unicode BMP có CID riêng, bảng `CIDToGIDMap` trỏ tới glyph từ đúng bảng cmap của file TTF, còn `ToUnicode` ánh xạ ngược một CID về đúng một mã Unicode. Dấu cách U+0020 và khoảng trắng không ngắt U+00A0 vẫn có hai mã riêng dù font dùng chung glyph. Cơ chế này dùng chung cho xem trước, áp dụng, lưu, in và Snapshot; không phụ thuộc tên hoặc đường dẫn của PDF.

PDFium có [lỗi bảng mã tự sinh khi nhiều Unicode dùng chung glyph](https://pdfium.googlesource.com/pdfium/+/6c8196a1478e4c37dac543210c73e276d4c06386%5E!/). Dữ liệu Times New Roman trên máy có U+0020 và U+00A0 cùng glyph; font có đủ ký tự tiêu đề trong mẫu được đọc. Đây là căn cứ để thay cách tạo bảng mã, chưa phải kết luận đã chạy thành công thao tác sửa trên mẫu.

Đối chiếu nội dung chỉ lấy ký tự thực sự thuộc đối tượng, bỏ ký tự do bộ đọc tự suy diễn và không ghép khoảng trắng từ đối tượng lân cận. Vẫn yêu cầu nội dung đọc lại khớp chính xác, không bỏ dấu hoặc quy đổi khoảng trắng để cho qua lỗi. Nếu đã nạp font đầy đủ nhưng nội dung đọc lại khác, thông báo nêu vị trí và mã Unicode khác nhau; không kết luận chung là font thiếu ký tự.

Ô nhập nhận diện tên Times New Roman, Courier New và các hậu tố kiểu chữ thông dụng. Sau khi tự tìm được font đầy đủ, ô nhập và thông báo cập nhật theo font đã dùng cho bản xem trước. Đã biên dịch; chưa chạy kiểm thử sửa/lưu/in trên giao diện.
