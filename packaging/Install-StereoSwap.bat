@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion

echo ========================================
echo  StereoSwap — Installer
echo ========================================
echo.
echo The app will be installed to:
echo   %LOCALAPPDATA%\Programs\StereoSwap
echo.
echo Start Menu and Desktop shortcuts will be created.
echo.

set "SRC=%~dp0"
set "SRC=%SRC:~0,-1%"
set "DST=%LOCALAPPDATA%\Programs\StereoSwap"

if not exist "%SRC%\StereoSwap.exe" (
  echo [ERROR] StereoSwap.exe not found in:
  echo   %SRC%
  echo.
  echo Run packaging\Build-Release.bat first.
  pause
  exit /b 1
)

if not exist "%DST%" mkdir "%DST%"
if not exist "%DST%\InstallHelper" mkdir "%DST%\InstallHelper"

echo Copying files...
copy /Y "%SRC%\StereoSwap.exe" "%DST%\StereoSwap.exe" >nul
if exist "%SRC%\USAGE.txt" copy /Y "%SRC%\USAGE.txt" "%DST%\USAGE.txt" >nul
if exist "%SRC%\InstallHelper\RegisterApo.ps1" copy /Y "%SRC%\InstallHelper\RegisterApo.ps1" "%DST%\InstallHelper\RegisterApo.ps1" >nul
if exist "%SRC%\Uninstall-StereoSwap.bat" copy /Y "%SRC%\Uninstall-StereoSwap.bat" "%DST%\Uninstall-StereoSwap.bat" >nul

set "STARTMENU=%APPDATA%\Microsoft\Windows\Start Menu\Programs\StereoSwap"
if not exist "%STARTMENU%" mkdir "%STARTMENU%"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('%STARTMENU%\StereoSwap.lnk'); $s.TargetPath = '%DST%\StereoSwap.exe'; $s.WorkingDirectory = '%DST%'; $s.Description = 'StereoSwap — L/R channel swap'; $s.Save(); $d = $ws.CreateShortcut([Environment]::GetFolderPath('Desktop') + '\StereoSwap.lnk'); $d.TargetPath = '%DST%\StereoSwap.exe'; $d.WorkingDirectory = '%DST%'; $d.Description = 'StereoSwap'; $d.Save()"

echo.
echo [Done] Installed.
echo.
echo Launch now? (Y/N)
set /p ANS=Answer: 
if /I "%ANS%"=="Y" start "" "%DST%\StereoSwap.exe"
if /I "%ANS%"=="I" start "" "%DST%\StereoSwap.exe"

echo.
echo Note: real channel swap needs the APO DLL later
echo (see USAGE.txt). The window / tray works now.
echo.
pause
