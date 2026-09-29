@echo off
chcp 65001 >nul
setlocal

echo ========================================
echo  StereoSwap — Install system APO
echo ========================================
echo.
echo This copies StereoSwapApo.dll to Program Files,
echo registers COM, and (optionally) is used by the tray
echo when you enable L/R swap (UAC prompt).
echo.

set "DLL="
if exist "%~dp0..\apo\build\bin\StereoSwapApo.dll" set "DLL=%~dp0..\apo\build\bin\StereoSwapApo.dll"
if exist "%~dp0StereoSwapApo.dll" set "DLL=%~dp0StereoSwapApo.dll"
if exist "%~dp0InstallHelper\StereoSwapApo.dll" set "DLL=%~dp0InstallHelper\StereoSwapApo.dll"

if not defined DLL (
  echo [ERROR] StereoSwapApo.dll not found.
  echo Build it first: double-click apo\Build-Apo.bat
  pause
  exit /b 1
)

set "SCRIPT=%~dp0..\install\RegisterApo.ps1"
if exist "%~dp0InstallHelper\RegisterApo.ps1" set "SCRIPT=%~dp0InstallHelper\RegisterApo.ps1"
if exist "%~dp0RegisterApo.ps1" set "SCRIPT=%~dp0RegisterApo.ps1"

echo DLL: %DLL%
echo.
echo UAC prompt will appear — accept to install.
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Start-Process powershell -Verb RunAs -Wait -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"%SCRIPT%\" -Action install -DllPath \"%DLL%\"'"

echo.
echo Done. Open StereoSwap tray, select device, enable L/R swap
echo (another UAC may appear to bind the device).
echo.
pause
