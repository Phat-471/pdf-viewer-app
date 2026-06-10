import re

replacements = [
    # 1. Specific words first (highly specific)
    ("quản trềEWordPress", "quản trị WordPress"),
    ("trang quản trềE", "trang quản trị"),
    ("quản trềEtrước", "quản trị trước"),
    ("quản trềEviên", "quản trị viên"),
    ("quản trềE", "quản trị"),
    
    ("thiết bềEkích hoạt", "thiết bị kích hoạt"),
    ("thiết bềEthành công", "thiết bị thành công"),
    ("thiết bềEthành", "thiết bị thành"),
    ("thiết bềE", "thiết bị"),
    
    ("bềEqua", "bỏ qua"),
    ("bềEkhóa", "bị khóa"),
    ("bềEtrống", "bị trống"),
    ("bềE", "bị"),
    
    ("sềElượng", "số lượng"),
    ("sềEmáy", "số máy"),
    ("sềERSA", "số RSA"),
    ("sềE", "số"),
    
    ("HềEtrợ", "Hỗ trợ"),
    ("hềEthống", "hệ thống"),
    ("hềE", "hệ"),
    
    ("chế đềESửa", "chế độ Sửa"),
    ("chế đềEvẽ", "chế độ vẽ"),
    ("chế đềE", "chế độ"),
    
    ("chữ ký sềERSA", "chữ ký số RSA"),
    ("chữ ký sềE", "chữ ký số"),
    
    ("ềEcột", "ở cột"),
    
    ("đềEhiển thềEtrên", "để hiển thị trên"),
    ("đềEhiển thị", "để hiển thị"),
    ("đềEtiếp tục", "để tiếp tục"),
    ("đềEtiếp", "để tiếp"),
    ("đềExác minh", "để xác minh"),
    ("đềExóa", "để xóa"),
    ("đềEgộp", "để gộp"),
    ("đềEthực hiện", "để thực hiện"),
    ("đềEthực", "để thực"),
    ("đềEvẽ", "để vẽ"),
    ("đềEtạo", "để tạo"),
    ("đềEsửa", "để sửa"),
    ("đềEtrang", "để trang"),
    
    ("mềEapp", "mở app"),
    ("mềEFile", "mở File"),
    ("mềEmột", "mở một"),
    ("mềEđược", "mở được"),
    ("mềElại", "mở lại"),
    ("mềE", "mở"),
    
    ("hiển thềEtrên", "hiển thị trên"),
    ("Hiển thềEgiao", "Hiển thị giao"),
    ("hiển thềE", "hiển thị"),
    ("Hiển thềE", "Hiển thị"),
    
    ("không thềE", "không thể"),
    ("Không thềE", "Không thể"),
    
    ("chềEdẫn", "chỉ dẫn"),
    ("vềEtrí", "vị trí"),
    ("hợp lềE", "hợp lệ"),
    
    ("thực thềE", "thực thi"),
    
    # 2. General character-level fixes
    ("đềE", "để"),
    ("thềE", "thị"),
    ("trềE", "trị"),
    ("lềE", "lệ"),
    ("tềE", "tệ"),
    ("vềE", "vị"),
    
    # 3. Last resort standalone replacements (must be at the very bottom!)
    ("ềE", "ở"),
    ("đểlàm", "để làm"),
]

files_to_clean = [
    r"e:\code\pdf\src\PdfViewerApp\PdfViewerApp\MainWindow.cs",
    r"e:\code\pdf\wp-pdfpro-licensing\admin-menu.php",
    r"e:\code\pdf\wp-pdfpro-licensing\api-handlers.php",
]

for file_path in files_to_clean:
    with open(file_path, "r", encoding="utf-8") as f:
        content = f.read()

    original = content
    # Apply specific replacements
    for target, replacement in replacements:
        content = content.replace(target, replacement)

    if content != original:
        with open(file_path, "w", encoding="utf-8") as f:
            f.write(content)
        print(f"Cleaned {file_path}")
    else:
        print(f"No changes in {file_path}")
