# StereoSwap

Windows 11 mini app: **L↔R channel swap** on a selected stereo render device.

Typical case: USB-to-Optical / soundbar has left and right swapped; jack/headphones are fine. Beacn Mix Create is excluded from the device list.

Windows has no built-in channel swap; Voicemeeter and similar tools are heavier than needed for this.

## Install without a terminal

1. Double-click [`packaging\Build-Release.bat`](packaging/Build-Release.bat) → produces `dist\StereoSwap\StereoSwap.exe`
2. Double-click `dist\StereoSwap\Install-StereoSwap.bat` → Start Menu + desktop shortcut
3. Launch the **StereoSwap** icon

Details: [`packaging\USAGE.txt`](packaging/USAGE.txt). Uninstall: `Uninstall-StereoSwap.bat`.

> The self-contained `.exe` does not require a separate .NET runtime. **L↔R audio swap** only takes effect after the APO DLL is built and bound (see below); the tray UI is usable without that.

## Architecture (MVP)

| Layer | Tech | Role |
|--------|------|------|
| APO | C++ DLL (`StereoSwapApo`) | L↔R in the `audiodg` shared graph, per-device FxProperties (Equalizer-APO-style) |
| UI | .NET 8+ WPF tray (`net10.0-windows` TFM) | Device list, swap toggle, L/R test tones, persistence, autostart |
| Install | PowerShell (`install/RegisterApo.ps1`) | COM registration + FxProperties bind/unbind |

**No** Windows Service, **no** Electron/web UI.

```
stereo-swap/
  apo/           Custom APO stub (CMake)
  ui/            StereoSwap.Tray (WPF)
  install/       RegisterApo.ps1
  packaging/     Build-Release / Install / Uninstall (.bat)
  dist/          Publish output (after Build-Release; not in git)
  docs/          Build / install notes
```

## Shared vs exclusive mode

| Mode | APO runs? | Notes |
|------|-----------|--------|
| **Shared** (default Windows mixing) | Yes | Intended path |
| **Exclusive** (WASAPI exclusive / some games, bit-perfect players) | **No** | App bypasses the `audiodg` effect chain — swap does not apply |

If there is no swap in exclusive mode: switch the player to shared mode, or disable “Allow applications to take exclusive control” for that device (Sound settings → device → Additional device properties).

## Prerequisites

- Windows 10/11 x64
- .NET 8+ SDK / Desktop runtime (WPF tray; csproj currently `net10.0-windows`)
- Visual Studio 2022 C++ + Windows SDK (APO DLL)
- CMake 3.20+ (APO)

## Quick start (UI)

**Recommended:** see *Install without a terminal* above.

Developer run (optional):

```powershell
cd ui\StereoSwap.Tray
dotnet run
```

Open the window from the tray icon: pick a device → L↔R toggle → Left/Right channel check tones.

Settings: `%LocalAppData%\StereoSwap\settings.json`  
APO config: `%ProgramData%\StereoSwap\config.json`

## APO build and binding

See [docs/apo-build.md](docs/apo-build.md), [docs/install.md](docs/install.md).

Short version (admin):

```powershell
# 1) Build the DLL, then:
.\install\RegisterApo.ps1 -Action register -DllPath <path\StereoSwapApo.dll>
.\install\RegisterApo.ps1 -Action bind -DeviceId '{0.0.0.00000000}.{endpoint-guid}'
```

## Out of scope (intentionally)

- EQ, tone controls, volume mixer
- Fancy multi-device routing
- Exclusive-mode workaround / driver filter
- Voicemeeter replacement

## License

MIT — see [LICENSE](LICENSE).
