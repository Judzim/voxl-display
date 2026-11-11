@echo off
rem One double-click installer: asks for admin once, then runs install.ps1.
setlocal
set "HERE=%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File','%HERE%install.ps1' -Wait"
echo.
echo Installation finished. Press any key.
pause >nul
