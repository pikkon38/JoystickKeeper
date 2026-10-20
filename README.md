# Joystick Keeper

Keeps Star Citizen bindings on the right device no matter what order your controllers were plugged in.

Windows numbers game controllers in the order it finds them, and Star Citizen's `actionmaps.xml` stores bindings by
that number (`js1_button3`, `js2_x`, …). Plug things in differently and your right-stick bindings land on the throttle.

## How it works

1. **Save your setup once.** Joystick Keeper lists the controllers that are plugged in and lights a row up when you
   move that device. Pick the `js` slot each device's bindings were made with (or click "Fill in from Star Citizen
   bindings" to read them from `actionmaps.xml`), then click Save setup.
2. **Click Update bindings** whenever your devices were plugged in differently, with the game closed. Joystick Keeper
   works out the slot each device has today and renumbers the `js` prefixes in `actionmaps.xml` to match (backup first).
   The file stays that way; the next update starts from it, so anything you rebound in-game is kept.
3. **Reset to saved order** puts the file back in your saved numbering, if you ever want it.

Xbox-style (XInput) controllers are skipped, since Star Citizen treats them as a gamepad, not a joystick.

Files live in `%APPDATA%\JoystickKeeper` (`profile.json`, `applied.json` while the file is renumbered, `backups\`).

## Build

```
dotnet publish src/JoystickKeeper -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
dotnet run --project tests/JoystickKeeper.Tests
```

## Install

Download `JoystickKeeper-Setup.exe` from the latest release. It installs for your user only (no admin prompt) into
`%LOCALAPPDATA%\Programs\JoystickKeeper`, adds Start menu and optional desktop shortcuts, and an uninstall entry.
Or download `JoystickKeeper.exe` and run it from any folder.

## Releases and updates

The release version is the `<Version>` in `src/JoystickKeeper/JoystickKeeper.csproj`. When a change that bumps it is
merged into `main`, GitHub Actions runs the tests, builds `JoystickKeeper.exe` and `JoystickKeeper-Setup.exe`, and
publishes them as release `v<Version>`. The app checks the latest release when it starts; if it's newer, an
"Update to vX" button appears. Clicking it downloads the new exe, closes the app, swaps the file and reopens it.
