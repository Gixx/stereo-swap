# StereoSwap — plan (MVP)

Local copy of the project plan (also in Cursor Project store).

## Goal

L↔R channel swap on one selected Windows stereo render device, with a tray UI.

## Decisions

- Custom APO (C++) + per-device FxProperties
- .NET 8+ WPF tray (no service / Electron)
- Beacn Mix Create excluded by name
- Exclusive mode: APO does not run — documented limitation

## Next

1. Build the APO DLL locally (VS C++ + SDK)
2. Register + bind on a spare USB DAC
3. Shared-mode listening test with an L↔R test tone
