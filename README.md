# muckChLab

A BepInEx 5.4.23.x visual and system utility plugin for Muck (Unity 2019.4).

## Scope

This repository contains a local presentation and configuration mod. It does not patch combat, enemy damage, item spawning, player movement, inventory, or weapon behavior. The source uses BepInEx and Unity APIs and does not call undocumented Muck classes.

## 52 visual and system settings

- Visual: crosshair, center dot, FPS, frame time, clock, date, watermark and its text, camera position, camera rotation, resolution, FOV override and slider, screen tint and intensity.
- Interface: hide HUD Canvas, control hints, compact layout, dim background, menu opacity, menu scale, window width, window height, center window, remember tab, separators, feature counter, open on startup, menu title, menu hotkey, bottom status line, build info.
- Graphics: FPS limit, VSync, MSAA toggle, MSAA level, shadows, shadow distance, texture quality, anisotropic filtering, LOD bias, pixel-light count, realtime reflection probes.
- Audio: master volume, mute, pause audio while menu is open, audio status.
- System: save config, load config, reset config, restore original graphics, debug logging, save on exit, Unity-version display, startup hint, reset confirmation, restore-graphics button.

The menu has six tabs: Visual, Interface, Graphics, Audio, System, and Info. Press Insert to open it; the hotkey can be cycled in Interface settings.

## Build

GitHub Actions downloads BepInEx 5.4.23.6 and Unity 2019.4.40 managed modules, builds muckChLab.dll, and uploads dist/muckChLab.zip as the muckChLab workflow artifact.

For local builds, install .NET SDK 8, extract BepInEx so the BepInEx core folder is available, and place Unity 2019.4 assemblies in Muck_Data/Managed. Then run:

    dotnet build muckChLab.csproj -c Release

Assembly-CSharp.dll is an optional conditional reference. The current source does not depend on game-specific method names or signatures.

## Install

1. Install BepInEx 5 for the Windows x64 build of Muck.
2. Copy BepInEx/plugins/muckChLab.dll from the ZIP into the game's BepInEx/plugins folder.
3. Launch the game and press Insert.

Configuration is stored in Unity PlayerPrefs.
