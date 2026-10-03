@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Extract-OfficialAssets.ps1" -ModernContent "C:\XboxGames\Among Us\Content"
echo.
pause
