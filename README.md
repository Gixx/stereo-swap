# StereoSwap

Windows 11 mini app: **L↔R channel swap** on a selected stereo render device.

Typical case: USB-to-Optical / soundbar has left and right swapped; jack/headphones are fine.

Windows has no built-in channel swap; Voicemeeter and similar tools are heavier than needed for this.

## Warning — use at your own risk

StereoSwap loads an **unsigned** audio processing object (APO) into Windows Audio (`audiodg`). On many Windows 11 systems this only works if you turn **off Memory Integrity** (Windows Security → Device security → Core isolation → Memory integrity).

Disabling Memory Integrity **weakens a system security feature**. **Use of this program is not recommended** for general or security-sensitive machines. If you proceed, you do so **entirely at your own risk**.

## Install without a terminal

1. Double-click [`packaging\Build-Release.bat`](packaging/Build-Release.bat) → `dist\StereoSwap\`
2. Double-click `Install-StereoSwap.bat` → tray app shortcuts
3. Double-click `Install-Apo.bat` → system APO (UAC / admin). **Reboot once** after the first install (unsigned APO allow flag).
4. Turn **Memory Integrity OFF** (see warning above) and reboot if it was on.
5. Open **StereoSwap** → select the Optical / soundbar device → enable **L↔R swap** (shared mode only).

Details: [`packaging\USAGE.txt`](packaging/USAGE.txt).

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

- Developer build: double-click [`apo\Build-Apo.bat`](apo/Build-Apo.bat)
- Install APO: [`packaging\Install-Apo.bat`](packaging/Install-Apo.bat) (admin)
- Or see [docs/apo-build.md](docs/apo-build.md), [docs/install.md](docs/install.md)

When swap is enabled in the tray, it runs `RegisterApo.ps1 -Action enable` (bind FxProperties + restart Audiosrv).

## Out of scope (intentionally)

- EQ, tone controls, volume mixer
- Fancy multi-device routing
- Exclusive-mode workaround / driver filter
- Voicemeeter replacement

## License

MIT — see [LICENSE](LICENSE).
