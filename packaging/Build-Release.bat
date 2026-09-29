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

echo [1/4] Building APO DLL...
call "%~dp0..\apo\Build-Apo.bat"
if errorlevel 1 (
  echo [WARN] APO build failed — tray will ship without StereoSwapApo.dll
)

echo [2/4] Publishing StereoSwap.exe (self-contained, win-x64)...
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

echo [3/4] Copying install helpers...
copy /Y "%~dp0..\install\RegisterApo.ps1" "%OUT%\InstallHelper\RegisterApo.ps1" >nul
copy /Y "%~dp0Install-StereoSwap.bat" "%OUT%\Install-StereoSwap.bat" >nul
copy /Y "%~dp0Install-Apo.bat" "%OUT%\Install-Apo.bat" >nul
copy /Y "%~dp0Uninstall-StereoSwap.bat" "%OUT%\Uninstall-StereoSwap.bat" >nul
copy /Y "%~dp0USAGE.txt" "%OUT%\USAGE.txt" >nul
if exist "%~dp0..\apo\build\bin\StereoSwapApo.dll" (
  copy /Y "%~dp0..\apo\build\bin\StereoSwapApo.dll" "%OUT%\InstallHelper\StereoSwapApo.dll" >nul
  copy /Y "%~dp0..\apo\build\bin\StereoSwapApo.dll" "%OUT%\StereoSwapApo.dll" >nul
)

echo [4/4] Done.
echo.
echo Output: %OUT%
echo.
echo 1^) Install-StereoSwap.bat  — tray app
echo 2^) Install-Apo.bat         — system APO ^(admin^)
echo 3^) Open StereoSwap → enable L/R swap
echo.
explorer "%OUT%"
pause
