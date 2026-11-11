@echo off
rem One double-click installer: asks for admin once, then runs install.ps1.
rem The script path goes in as a single quoted argument, otherwise a folder name with a
rem space in it would split and PowerShell would look for the wrong file.
setlocal
set "HERE=%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell -Verb RunAs -Wait -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"%HERE%install.ps1\"'"
echo.
echo Installation finished. Press any key.
pause >nul
