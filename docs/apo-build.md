# StereoSwap APO — build notes

## Requirements

- Visual Studio 2022 with **Desktop development with C++**
- Windows 10/11 SDK (audio APO headers: `audioenginebaseapo.h`)
- CMake 3.20+

## Configure & build

```powershell
cd apo
cmake -B build -G "Visual Studio 17 2022" -A x64
cmake --build build --config Release
```

Output: `apo/build/bin/Release/StereoSwapApo.dll`

## Notes

- `MinimalApoBase` is an MVP stand-in so the project does not depend on the full WDK APO sample tree.
- Headers such as `audioenginebaseapo.h` must come from the installed Windows SDK. If CMake cannot find them, open the `.sln` from a **Developer PowerShell for VS** prompt.
- After building, use `install/RegisterApo.ps1` (admin) to register the COM CLSID and bind FxProperties on a chosen render device.
- The APO only runs in the shared audio engine path (`audiodg.exe`). Exclusive-mode streams bypass APOs — see the root README.
