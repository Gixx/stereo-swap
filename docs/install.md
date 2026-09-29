# StereoSwap — install helper notes

## Scripts

| Script | Role |
|--------|------|
| `RegisterApo.ps1` | Admin: install / enable / disable / bind / unbind |
| `packaging/Install-Apo.bat` | Double-click wrapper (UAC) for COM install |
| `apo/Build-Apo.bat` | Build `StereoSwapApo.dll` |

## Typical flow (no typing)

1. `packaging\Build-Release.bat`
2. `dist\StereoSwap\Install-StereoSwap.bat`
3. `dist\StereoSwap\Install-Apo.bat` (UAC)
4. Tray → select device → enable L↔R swap (UAC; restarts audio briefly)

## PowerShell (optional)

```powershell
cd install
.\RegisterApo.ps1 -Action install -DllPath ..\apo\build\bin\StereoSwapApo.dll
.\RegisterApo.ps1 -Action enable -DeviceId '{0.0.0.00000000}.{YOUR-ENDPOINT-GUID}'
```

## Uninstall

```powershell
.\RegisterApo.ps1 -Action disable -DeviceId '...' -Unregister
```

## Caution

FxProperties edits affect the system audio graph. Prefer a spare USB DAC while developing. If an endpoint goes silent, run disable/unbind and restart the Audio service.
