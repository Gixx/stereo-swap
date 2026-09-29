@echo off
chcp 65001 >nul
setlocal

echo ========================================
echo  StereoSwap — Uninstall
echo ========================================
echo.

set "DST=%LOCALAPPDATA%\Programs\StereoSwap"
set "STARTMENU=%APPDATA%\Microsoft\Windows\Start Menu\Programs\StereoSwap"
set "DESKTOP=%USERPROFILE%\Desktop\StereoSwap.lnk"

taskkill /IM StereoSwap.exe /F >nul 2>&1

if exist "%DESKTOP%" del /F /Q "%DESKTOP%"
if exist "%STARTMENU%" rmdir /S /Q "%STARTMENU%"
if exist "%DST%" (
  rmdir /S /Q "%DST%"
  echo Removed: %DST%
) else (
  echo No install found at: %DST%
)

echo.
echo Note: if the APO was bound to a render device,
echo you may still need RegisterApo.ps1 unbind/unregister (admin).
echo.
pause
