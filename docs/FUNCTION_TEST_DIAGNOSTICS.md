# Kiểm thử chức năng và đọc nhật ký lỗi

> Tài liệu này hướng dẫn kiểm tra từng chức năng của PDF Pro và gửi lại bằng chứng nếu thao tác không chạy. Mỗi lần chạy ứng dụng có một tệp nhật ký cục bộ.

## Cách kiểm tra

1. Mở một bản sao PDF thử nghiệm. Dùng bản sao vì các thao tác trang, bảo mật và lưu có thể thay đổi tài liệu.
2. Thử **một chức năng mỗi lần**. Với công cụ chú thích, chọn công cụ, thao tác trên trang, nhập chữ nếu cần, rồi bấm ra ngoài để xác nhận.
3. Chờ chức năng kết thúc hoặc thông báo lỗi trước khi thử chức năng tiếp theo.
4. Mở bảng lệnh bằng `Ctrl+K`, tìm **Performance Trace**, rồi chọn **Sao chép báo cáo**. Dán báo cáo vào tin nhắn hỗ trợ.

Tệp nhật ký nằm tại `%LOCALAPPDATA%\PdfPro\PerfLogs\perf_<thời gian>_<PID>.log`. Nếu bảng lệnh không mở được, gửi tệp mới nhất trong thư mục này.

## Cách đọc kết quả

| Dòng nhật ký | Ý nghĩa |
|---|---|
| `TEST_TRACE|ACTION` | Ứng dụng đã nhận lần bấm nút hoặc thao tác trên trang. Dòng có tên điều khiển, công cụ đang dùng và số trang. |
| `TEST_TRACE|DISPATCHED` | Sự kiện nút đã chạy qua handler giao diện. Tác vụ bất đồng bộ có thể vẫn đang thực hiện. |
| `TEST_TRACE|PASS` | Mã ứng dụng đã báo kết quả thành công cho bước đó. |
| `TEST_TRACE|INFO` | Bước đã chạy nhưng chưa xác nhận thành công/thất bại, hoặc thao tác được hủy/thiếu dữ liệu đầu vào. |
| `TEST_TRACE|FAIL` | Ứng dụng đã báo lỗi ở bước đó. |
| `TEST_TRACE|ERROR` | Có exception; dòng này kèm tên bước, thông báo lỗi và stack trace để tìm tệp/dòng mã gây lỗi. |

Nếu chỉ thấy `ACTION` mà không có trạng thái tiếp theo, thao tác đã vào giao diện nhưng chưa báo hoàn tất. Gửi đoạn nhật ký từ `ACTION` đó đến trước lần thử kế tiếp.

## Danh sách chức năng nên thử

Thử từng mục và ghi chú kết quả hiển thị trên trang. Bảng lệnh và nhật ký sẽ tự ghi lại thao tác để đối chiếu.

1. **Mở, hiển thị và điều hướng:** mở PDF, chuyển trang, phóng to/thu nhỏ, cuộn, đóng rồi mở lại tài liệu.
2. **Chọn và lấy chữ:** chọn chữ trong PDF, `Ctrl+C`, dán sang Notepad; thử cả PDF có chữ và PDF scan.
3. **Chú thích và nhập chữ:** hộp văn bản, ghi chú, callout, bút, hình, tô sáng; nhập chữ, bấm ra ngoài, chọn lại chú thích, lưu và mở lại PDF.
4. **Sửa chữ gốc:** chọn vùng chữ, sửa nội dung, áp dụng, lưu và mở lại.
5. **Quản lý trang:** xoay, sắp xếp, chèn, nhân đôi, xóa, tách, trích xuất và gộp trang/tệp.
6. **OCR và xuất:** chạy OCR, xuất văn bản, tạo PDF có thể tìm kiếm, xuất DOCX nếu có.
7. **In:** xem trước, in cả trang, in vùng Snapshot và in hàng loạt.
8. **Xử lý PDF:** nén, watermark, đánh số trang, trích ảnh, bảo mật và so sánh tài liệu.
9. **AI:** chỉ chọn AI Snapshot khi muốn gửi vùng chụp; thử lần lượt vùng nhỏ, vùng hợp lệ và chat toàn trang. Kiểm tra model/provider và cấu hình mạng nếu log dừng ở bước AI request.
10. **Ứng dụng và dịch vụ:** lưu cài đặt, kiểm tra thư viện, trạng thái kích hoạt và kiểm tra cập nhật.

## Thay đổi liên quan đến việc tự mở AI

Phím `A` đơn lẻ không còn là phím tắt AI Snapshot vì vùng PDF có thể đang được focus khi người dùng bấm phím. Hãy mở AI Snapshot bằng nút có nhãn **AI Snapshot** hoặc tìm lệnh này trong `Ctrl+K`. Panel AI chỉ tự mở sau khi chọn công cụ đó, chạy kiểm tra AI, hoặc nhận ảnh chụp hợp lệ từ tab PDF đang hoạt động. Đóng panel khi đang ở chế độ AI Snapshot sẽ thoát chế độ này về **Chọn**. Yêu cầu ảnh từ tab cũ hoặc khi công cụ khác đang chọn sẽ bị bỏ qua và ghi vào nhật ký.

Các dòng `TEST_TRACE` được lưu cục bộ. Cơ chế gửi crash telemetry hiện có vẫn tuân theo cài đặt telemetry của ứng dụng. Khi chia sẻ báo cáo, kiểm tra lại nội dung trước khi gửi; lỗi từ thư viện bên thứ ba có thể chứa đường dẫn cục bộ hoặc tên tệp trong stack trace.
