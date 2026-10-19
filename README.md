# Joystick Keeper

Keeps Star Citizen bindings on the right device no matter what order your controllers were plugged in.

Windows numbers game controllers in the order it finds them, and Star Citizen's `actionmaps.xml` stores bindings by
that number (`js1_button3`, `js2_x`, …). Plug things in differently and your right-stick bindings land on the throttle.

## How it works

1. **Save your setup once.** Joystick Keeper lists every controller, lights a row up when you move that device, and
   reads your current `actionmaps.xml` to fill in which `js` slot each device's bindings were made on.
2. **Start the game through Joystick Keeper.** "Fix bindings & open RSI Launcher" works out the slot each device will
   actually get today, renumbers the `js` prefixes in `actionmaps.xml` to match (backup first), and opens the RSI Launcher.
3. **Close the game.** Joystick Keeper notices `StarCitizen.exe` exit and renumbers the file back, keeping anything you
   rebound in-game. If it's closed early, it puts the file back the next time it starts.

Xbox-style (XInput) controllers are skipped, since Star Citizen treats them as a gamepad, not a joystick.

Files live in `%APPDATA%\JoystickKeeper` (`profile.json`, `applied.json` while a session is active, `backups\`).

## Build

```
dotnet publish src/JoystickKeeper -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
dotnet run --project tests/JoystickKeeper.Tests
```

## Releases and updates

Pushing a tag like `v0.5.0` makes GitHub Actions run the tests, build `JoystickKeeper.exe` with that version, and
attach it to a new release. The app checks the latest release when it starts; if it's newer, an
"Update to vX" button appears. Clicking it downloads the new exe, closes the app, swaps the file and reopens it.
Updating is blocked while a Star Citizen session has the bindings remapped.
