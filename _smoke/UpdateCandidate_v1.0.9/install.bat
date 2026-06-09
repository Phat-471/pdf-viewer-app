@echo off
title Cai dat PDF Pro - HPhat Edition
chcp 65001 > nul
echo.
echo ==============================================
echo   Đang chạy trình cài đặt PDF Pro...
echo ==============================================
echo.

powershell -ExecutionPolicy Bypass -File "%~dp0install.ps1"

echo.
pause
