@echo off
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
  echo [ERROR] vswhere.exe not found.
  exit /b 1
)
for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find Common7\Tools\VsDevCmd.bat`) do set "VSDEV=%%i"
if not defined VSDEV (
  echo [ERROR] VsDevCmd.bat not found. Install VS C++ tools.
  exit /b 1
)
call "%VSDEV%" -arch=amd64 >nul
exit /b 0
