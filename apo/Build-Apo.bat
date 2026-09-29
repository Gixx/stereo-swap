@echo off
setlocal
call "%~dp0find-vsdev.bat" || exit /b 1
cd /d "%~dp0"
if exist build rmdir /s /q build
cmake -B build -G "Ninja" -DCMAKE_BUILD_TYPE=Release 2>nul
if errorlevel 1 (
  cmake -B build -G "Visual Studio 17 2022" -A x64
  if errorlevel 1 (
    cmake -B build -G "Visual Studio 18 2026" -A x64
  )
)
cmake --build build --config Release
if errorlevel 1 exit /b 1
echo.
echo DLL:
dir /s /b "%~dp0build\*.dll"
