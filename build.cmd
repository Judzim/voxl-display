@echo off
rem Rebuilds VoxlDisplay.exe with the C# compiler that ships with Windows.
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
"%CSC%" /nologo /target:winexe /win32icon:voxl.ico /win32manifest:app.manifest ^
  /out:VoxlDisplay.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll VoxlDisplay.cs
if errorlevel 1 (echo build failed & exit /b 1)
echo built VoxlDisplay.exe
