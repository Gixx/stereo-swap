@echo off
REM Stop leftover StereoSwap debug hosts so F5 can overwrite the DLL.
taskkill /F /IM StereoSwap.exe >nul 2>&1
for /f "tokens=2 delims=," %%P in ('tasklist /FI "IMAGENAME eq dotnet.exe" /FO CSV /NH 2^>nul') do (
  wmic process where "ProcessId=%%~P" get CommandLine 2>nul | findstr /I "StereoSwap" >nul
  if not errorlevel 1 taskkill /F /PID %%~P >nul 2>&1
)
REM Fallback: powershell without nested quote hell
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0kill-stereoswap.ps1" >nul 2>&1
exit /b 0
