# StereoSwap — install helper notes

## Scripts

| Script | Role |
|--------|------|
| `RegisterApo.ps1` | Admin: COM register + FxProperties bind/unbind |

## Typical MVP flow

1. Build `StereoSwapApo.dll` (`docs/apo-build.md`)
2. Admin PowerShell:

```powershell
cd install
.\RegisterApo.ps1 -Action register -DllPath ..\apo\build\bin\Release\StereoSwapApo.dll
.\RegisterApo.ps1 -Action bind -DeviceId '{0.0.0.00000000}.{YOUR-ENDPOINT-GUID}'
```

3. Start the tray app, select the same device, enable swap (writes `%ProgramData%\StereoSwap\config.json`).
4. Play audio in **shared** mode and verify L↔R.

## Uninstall

```powershell
.\RegisterApo.ps1 -Action unbind -DeviceId '...'
.\RegisterApo.ps1 -Action unregister
```

## Caution

FxProperties edits affect the system audio graph. Wrong CLSID / missing DLL can silence an endpoint until unbound. Prefer a spare USB DAC while developing.
