@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0.."

echo ========================================
echo  StereoSwap — Release build
echo ========================================
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
  echo [ERROR] "dotnet" is not on PATH.
  echo Install the .NET SDK: https://dotnet.microsoft.com/download
  pause
  exit /b 1
)

set OUT=%~dp0..\dist\StereoSwap
if exist "%OUT%" rmdir /s /q "%OUT%"
mkdir "%OUT%" 2>nul
mkdir "%OUT%\InstallHelper" 2>nul

echo [1/3] Publishing StereoSwap.exe (self-contained, win-x64)...
dotnet publish "%~dp0..\ui\StereoSwap.Tray\StereoSwap.Tray.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -o "%OUT%"
if errorlevel 1 (
  echo [ERROR] Publish failed.
  pause
  exit /b 1
)

echo [2/3] Copying install helper and usage notes...
copy /Y "%~dp0..\install\RegisterApo.ps1" "%OUT%\InstallHelper\RegisterApo.ps1" >nul
copy /Y "%~dp0Install-StereoSwap.bat" "%OUT%\Install-StereoSwap.bat" >nul
copy /Y "%~dp0Uninstall-StereoSwap.bat" "%OUT%\Uninstall-StereoSwap.bat" >nul
copy /Y "%~dp0USAGE.txt" "%OUT%\USAGE.txt" >nul

echo [3/3] Done.
echo.
echo Output: %OUT%
echo.
echo Next: open dist\StereoSwap and double-click Install-StereoSwap.bat
echo.
explorer "%OUT%"
pause
