import re

replacements = [
    # C# specific or generic
    ("quản trềEviên", "quản trị viên"),
    ("quản trềE", "quản trị"),
    ("thiết bềE", "thiết bị"),
    ("bềEqua", "bỏ qua"),
    ("bềEkhóa", "bị khóa"),
    ("sềElượng", "số lượng"),
    ("sềEmáy", "số máy"),
    ("HềEtrợ", "Hỗ trợ"),
    ("chế đềE", "chế độ"),
    ("chữ ký sềE", "chữ ký số"),
    ("ềEcột", "ở cột"),
    ("đềEtiếp", "để tiếp"),
    ("mềEapp", "mở app"),
    ("đềEhiển thềEtrên", "để hiển thị trên"),
    ("Hiển thềEgiao", "Hiển thị giao"),
    ("hiển thềE", "hiển thị"),
    ("không thềE", "không thể"),
    ("Không thềE", "Không thể"),
    ("mềEFile", "mở File"),
    ("mềEmột", "mở một"),
    ("đềEgộp", "để gộp"),
    ("đềEthực", "để thực"),
    ("đềEvẽ", "để vẽ"),
    ("đềEtạo", "để tạo"),
    ("đềEsửa", "để sửa"),
    ("chềEdẫn", "chỉ dẫn"),
    ("hỏi AI", "hỏi AI"),
    ("vềEtrí", "vị trí"),
    ("hợp lềE", "hợp lệ"),
    ("thông báo mềE", "thông báo mở"),
    ("tải vềEkhi mềEapp", "tải về khi mở app"),
    ("đăng nhập đềE", "đăng nhập để"),
    ("quản trềEtrước", "quản trị trước"),
    ("Sinh Khóa RSA ềE", "Sinh Khóa RSA ở"),
    ("chế đềESửa", "chế độ Sửa"),
    ("bản ghi kích hoạt đềExác minh", "bản ghi kích hoạt để xác minh"),
    ("bềEtrống", "bị trống"),
    ("đềExác minh", "để xác minh"),
    ("đềExóa", "để xóa"),
    ("đềEtiếp tục", "để tiếp tục"),
    ("thiết bềEthành", "thiết bị thành"),
    ("chữ ký sềERSA", "chữ ký số RSA"),
    ("thực thềE", "thực thi"),
    ("đềEtrang", "để trang"),
    ("hủy chế đềE", "hủy chế độ"),
    ("chuyển sang công cụ", "chuyển sang công cụ"),
    ("đầu đềEtạo", "đầu để tạo"),
    ("kéo đềEtạo", "kéo để tạo"),
    ("bản vẽ đềEhỏi", "bản vẽ để hỏi"),
    ("thông báo ứng dụng", "thông báo ứng dụng"),
    ("Không thềEmềEfile", "Không thể mở file"),
    ("chưa mềEđược", "chưa mở được"),
    ("Vui lòng mềElại", "Vui lòng mở lại"),
    ("Vui lòng mềE", "Vui lòng mở"),
    ("đềEhiển", "để hiển"),
    ("thềEtrên", "thị trên"),
    ("trang quản trềEWordPress", "trang quản trị WordPress"),
    ("vềEv{previousVersion}", "về v{previousVersion}"),
    ("rest_route=/pdfpro/v1/...", "rest_route=/pdfpro/v1/..."),
    ("bản quyền đềEtiếp tục", "bản quyền để tiếp tục"),
]

# Add more generic mappings
generic_replacements = {
    "đềE": "để",
    "mềE": "mở",
    "bềE": "bị",
    "sềE": "số",
    "hềE": "hệ",
    "thềE": "thị",
    "trềE": "trị",
    "lềE": "lệ",
    "tềE": "tệ",
}

files_to_clean = [
    r"e:\code\pdf\src\PdfViewerApp\PdfViewerApp\MainWindow.cs",
    r"e:\code\pdf\wp-pdfpro-licensing\admin-menu.php",
    r"e:\code\pdf\wp-pdfpro-licensing\api-handlers.php",
]

for file_path in files_to_clean:
    with open(file_path, "r", encoding="utf-8") as f:
        content = f.read()

    original = content
    # Apply specific replacements first
    for target, replacement in replacements:
        content = content.replace(target, replacement)

    # Apply generic replacements
    for target, replacement in generic_replacements.items():
        content = content.replace(target, replacement)

    if content != original:
        with open(file_path, "w", encoding="utf-8") as f:
            f.write(content)
        print(f"Cleaned {file_path}")
    else:
        print(f"No changes in {file_path}")
