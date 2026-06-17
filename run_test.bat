@echo off
:: Di chuyen CWD den dung thu muc chua file bat nay (Giai quyet triet de loi khi Run as Administrator)
cd /d "%~dp0"

title PDF Pro - Build and Run Test
echo ===================================================
echo [PDF Pro] Dang kiem tra va bien dich tung buoc...
echo ===================================================

:: 1. Prepare pdfium.dll
echo [Buoc 1/4] Dang chuan bi pdfium.dll...
if not exist "libs" (
    mkdir libs
    echo   Da tao thu muc libs.
)

if not exist "libs\pdfium.dll" (
    if exist "old_project\build\windows\x64\runner\Release\pdfium.dll" (
        xcopy "old_project\build\windows\x64\runner\Release\pdfium.dll" "libs\" /Y /D
        echo   Da sao chep pdfium.dll tu thu muc old_project.
    ) else (
        echo   [THONG BAO] Khong tim thay pdfium.dll. Dang tien hanh tai xuong tu Github...
        powershell -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri 'https://github.com/bblanchon/pdfium-binaries/releases/latest/download/pdfium-win-x64.tgz' -OutFile 'pdfium.tgz'; New-Item -ItemType Directory -Force -Path 'pdfium_extracted'; tar -xzf pdfium.tgz -C pdfium_extracted; Copy-Item 'pdfium_extracted\bin\pdfium.dll' -Destination 'libs\pdfium.dll' -Force; Remove-Item 'pdfium.tgz' -Force; Remove-Item 'pdfium_extracted' -Recurse -Force"
        if exist "libs\pdfium.dll" (
            echo   [THANH CONG] Da tai va giai nen pdfium.dll!
        ) else (
            echo   [LOI] Khong the tai pdfium.dll. Vui long kiem tra lai ket noi mang hoac tai thu cong tu bblanchon/pdfium-binaries.
        )
    )
) else (
    echo   pdfium.dll da ton tai trong thu muc libs.
)

:: 2. Build Rust Core
echo.
echo [Buoc 2/4] Dang bien dich loi Rust (src\PdfCore)...
cd src\PdfCore
call cargo build --release
if %ERRORLEVEL% neq 0 (
    echo   [LOI] Bien dich Rust Core that bai! Ma loi: %ERRORLEVEL%
    cd /d "%~dp0"
    pause
    exit /b %ERRORLEVEL%
)
cd /d "%~dp0"
echo   Loi Rust bien dich thanh cong!

:: 3. Copy binaries to WPF output
echo.
echo [Buoc 3/4] Dang dong bo DLL vao thu muc dau ra WPF...
if not exist "src\PdfViewerApp\bin\Release\net8.0-windows10.0.26100.0" (
    mkdir "src\PdfViewerApp\bin\Release\net8.0-windows10.0.26100.0"
)
if not exist "src\PdfViewerApp\bin\Release\net8.0-windows" (
    mkdir "src\PdfViewerApp\bin\Release\net8.0-windows"
)
if exist "src\PdfCore\target\release\pdf_core.dll" (
    xcopy "src\PdfCore\target\release\pdf_core.dll" "libs\" /Y /D
    xcopy "src\PdfCore\target\release\pdf_core.dll" "src\PdfViewerApp\bin\Release\net8.0-windows10.0.26100.0\" /Y /D
    xcopy "src\PdfCore\target\release\pdf_core.dll" "src\PdfViewerApp\bin\Release\net8.0-windows\" /Y /D
    echo   Da sao chep pdf_core.dll.
) else (
    echo   [CANH BAO] Khong tim thay pdf_core.dll vua build!
)
if exist "libs\pdfium.dll" (
    xcopy "libs\pdfium.dll" "src\PdfViewerApp\bin\Release\net8.0-windows10.0.26100.0\" /Y /D
    xcopy "libs\pdfium.dll" "src\PdfViewerApp\bin\Release\net8.0-windows\" /Y /D
    echo   Da sao chep pdfium.dll.
)

:: 3.5 Copy Logo to Assets
if not exist "src\PdfViewerApp\Assets" mkdir "src\PdfViewerApp\Assets"
copy /Y "C:\Users\IT\.gemini\antigravity-ide\brain\89e6a9ca-03d0-4e13-8e4f-7b1bc6b11b43\hphat_logo_1780279208636.png" "src\PdfViewerApp\Assets\hphat_logo_1780279208636.png"

:: 4. Build WPF application
echo.
echo [Buoc 4/4] Dang bien dich ung dung WPF (src\PdfViewerApp)...
cd src\PdfViewerApp
call dotnet build -c Release
if %ERRORLEVEL% neq 0 (
    echo   [LOI] Bien dich WPF C# that bai! Ma loi: %ERRORLEVEL%
    cd /d "%~dp0"
    pause
    exit /b %ERRORLEVEL%
)

:: 5. Launch the application
echo ===================================================
echo [PDF HPhat] Bien dich thanh cong! Dang khoi chay...
echo ===================================================
if exist "bin\Release\net8.0-windows10.0.26100.0\PdfViewerApp.exe" (
    cd bin\Release\net8.0-windows10.0.26100.0
) else (
    cd bin\Release\net8.0-windows
)
if exist "PdfViewerApp.exe" (
    echo   Dang mo PdfViewerApp.exe...
    start PdfViewerApp.exe
) else (
    echo   [LOI] Khong tim thay file PdfViewerApp.exe trong thu muc dau ra!
)
cd /d "%~dp0"

echo.
echo === HOAN THANH TAT CA CAC BUOC ===
pause
exit /b 0
