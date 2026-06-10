with open(r'src\PdfViewerApp\PdfViewerApp\MainWindow.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

typo_keywords = ['thệ', 'vị', 'mở', 'để', 'gộp', 'kích hoạt', 'vẽ', 'chế độ']
for i, line in enumerate(lines):
    for keyword in typo_keywords:
        if keyword in line:
            # Check if there are strange typos like thệ, vị (where it should be về), etc.
            if 'thệ' in line or 'vịv' in line or 'trang quản trềE' in line or 'đểhiển' in line:
                print(f"Line {i+1}: {line.strip()}")
            elif 'không thệ' in line or 'không thềE' in line:
                print(f"Line {i+1}: {line.strip()}")
