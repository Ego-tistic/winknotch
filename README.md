# WinTide

A customizable Windows island for music, clocks, timers, and quick app launching.

Attach it to a screen edge, tuck it into a corner, or let it float. Expand it for controls, then collapse it back into a compact display.

## Features

- Media controls for players exposing Windows media sessions, including Spotify and Apple Music.
- Album artwork, an animated visualizer, playback seeking, and system volume.
- Clock, stopwatch, countdown timer, and time-of-day alarms with dismissible sounds.
- App drawer with grid, list, and dock views. Choose your apps and bring existing windows to the foreground.
- Dark, light, and custom color themes with a rectangular color picker, brightness control, and hex entry.
- Adjustable opacity and size, automatic hiding, edge placement, and optional startup at sign-in.
- Smooth hover magnification and animated attachment, detachment, expansion, and collapse.

## Run

Download the Windows executable from [Releases](https://github.com/Ego-tistic/wintide/releases) and place it in a folder on your PC. Double-click it to launch.

The app is portable and unsigned. Settings are stored per Windows account under `HKCU\Software\BezelPlayer`. If startup at sign-in is enabled, keep the executable in the same location.

Developed and tested on Windows 11. Other Windows versions have not been validated.

## Controls

| Action | Control |
| --- | --- |
| Expand | Hover over the island |
| Open settings | Gear button at the bottom right |
| Show Media, Clock, and Apps tabs | Menu button at the bottom left |
| Adjust volume | Mouse wheel over the island; dedicated scroll areas keep their normal behavior |
| Switch tabs | Hold Shift and scroll |
| Move the island | Enable an unlocked placement mode, then hold right-click for half a second and drag |
| Recover a misplaced island | Ctrl+Alt+I by default; configurable in Settings |
| Quit | Right-click and select Quit |

## Build

Build on 64-bit Windows with .NET Framework 4.8. No external NuGet packages are needed. The script uses the Windows metadata installed with Windows, or the Windows SDK as a fallback.

```powershell
.\build.ps1
```

The executable is written to `artifacts\WinTide.exe`.

## Verification

Run these from the repository root in Windows PowerShell on an interactive Windows desktop:

```powershell
New-Item -ItemType Directory -Force tests/output | Out-Null
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tests/check-themes.ps1
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tests/check-settings-sliders.ps1
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tests/check-windows-drawer.ps1
```

These checks exercise theme contrast and persistence, slider geometry, button magnification, and restoration of an existing minimized window. Settings checks temporarily use the app's registry key and restore its original values afterward. Close the running app before testing.

GitHub Actions builds the Windows executable and makes it available as a build artifact.

## Current limitations

- Media controls and seeking depend on what each player exposes. Apple Music also has a Windows UI Automation fallback.
- The visualizer is animated artwork, rather than a real audio spectrum analyzer.
- Optional controller shortcuts do not prevent a game from receiving the same controller input.
- Exclusive fullscreen games may cover the island. A borderless windowed game is more likely to display it.
- Custom color contrast accounts for opacity using a dark-background estimate; it does not sample every underlying desktop color.

## Project layout

```text
src/        Windows WPF application source
tests/      Interactive Windows verification scripts
build.ps1   Standalone Windows build
```
