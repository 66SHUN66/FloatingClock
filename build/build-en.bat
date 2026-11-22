@echo off
chcp 65001 >nul
setlocal
rem Builds the English edition (/define:EN switches the default UI language).
set ROOT=%~dp0..
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe

if not exist "%ROOT%\dist" mkdir "%ROOT%\dist"

set ICON=
if exist "%ROOT%\assets\app.ico" set ICON=/win32icon:"%ROOT%\assets\app.ico"

"%CSC%" /nologo /target:winexe /optimize+ /define:EN %ICON% ^
  /out:"%ROOT%\dist\FloatingClock.exe" ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  "%ROOT%\src\FloatingClock.cs" "%ROOT%\src\ClockFormCore.cs" ^
  "%ROOT%\src\ClockFormUi.cs" "%ROOT%\src\SettingsForm.cs" ^
  "%ROOT%\src\SettingsControls.cs" "%ROOT%\src\FontPicker.cs" ^
  "%ROOT%\src\Lang.cs"

if errorlevel 1 (
  echo BUILD FAILED
  exit /b 1
)
echo OK: dist\FloatingClock.exe
