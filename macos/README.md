# WinKnotch for macOS — native preview

A native AppKit/SwiftUI version of WinKnotch. The deployment target is **macOS 12 or later**, with a universal executable for **Intel and Apple silicon**. That includes Intel Macs running Sonoma through OpenCore Legacy Patcher; physical testing on that setup is still needed.

## Build on a Mac

Install Apple's Xcode Command Line Tools if needed:

```sh
xcode-select --install
```

From this folder:

```sh
bash build.sh
open build/WinKnotch.app
```

The script also creates `build/WinKnotch-macOS.zip`. Copy WinKnotch.app to Applications before enabling Start at Login. It is ad-hoc signed for local testing, not Developer ID signed or notarized.

## Connect your music

Use the waveform menu-bar icon to choose **Connect Apple Music** or **Connect Spotify**. Approve macOS Automation access when prompted. Only players you connect are queried. Player permissions can be changed under Privacy & Security → Automation.

The most recently active playing player takes over. The collapsed island displays artwork and twelve animated bars while music plays, and defaults to a clock when idle. Hover to expand, use the bottom-left menu button for Media / Clock / Apps, or use the bottom-right gear for settings. Clicking outside collapses it. Clicking the track opens the player.

## Included

- Media artwork, diffused artwork background, playback, seeking, and output volume.
- Clock with seconds and optional 24-hour time.
- Stopwatch, countdown timer with seconds, and a time-of-day wake-up alarm.
- Dismissible alarm sounds that stop automatically after three minutes.
- User-selected app drawer, high-resolution app icons, dock/grid/list layouts, and drag reordering.
- Existing running apps activate instead of opening another instance.
- Dark, light, and custom themes with a rectangular color block and brightness/hex controls.
- Adjustable opacity/size, automatic hiding, display selection, top/bottom placement, and movable Floating mode.
- Menu-bar recovery/recentering and Start at Login on macOS 13+.

Use an empty part of the floating island to drag it. Use the waveform menu to move to another display. Applications in the drawer are chosen with the native file picker and saved per Mac user.

## Checks

```sh
build/WinKnotch.app/Contents/MacOS/WinKnotch --self-test
build/WinKnotch.app/Contents/MacOS/WinKnotch --smoke-test
```

The first command checks timer durations, theme contrast, canceled app reordering, color conversion, and AppleScript return values. The second briefly launches the native panel and exits. Real player Automation, artwork, and sleep/wake behavior need testing on a personal Mac with Music or Spotify installed.

## Preview limitations

This is a native preview, not complete Windows feature parity. Side-edge layouts, sticky ripple/particle animations, the Windows controller integration, global custom recovery hotkeys, and a favorite-song button are not included yet. The menu-bar recovery command remains available.

The visualizer is animated, not a live audio spectrum. Some cloud tracks may not expose album artwork through Music's scripting interface. Timer/alarm state does not survive quitting the app, and a sleeping Mac cannot run its alarms. Startup at login uses macOS Login Items; on macOS 12, add the app manually in Users & Groups → Login Items.
