@echo off
:: Принудительно переходим в папку, где лежит этот .bat файл
cd /d "%~dp0"

echo Starting React projects...

:: Запуск проектов с явным указанием текущей папки файла
start "Vite: mainweb" /D "%~dp0mainweb.front" cmd /k "npm run dev"
start "Vite: authsystem" /D "%~dp0authsystem.front" cmd /k "npm run dev"
::start "Vite: svod_reports" /D "%~dp0svod_reports.front" cmd /k "npm run dev"

echo All commands sent!
timeout /t 10
