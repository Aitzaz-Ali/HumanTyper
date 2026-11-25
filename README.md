# Human Typer

A tiny Windows app that types text into **any** text box with human-like timing. Paste your text, click into the target field, press a hotkey, and it types it out character by character.

Useful for forms and remote-desktop/VM windows that block pasting, demos, and screen recordings where you want typing to look natural.

<p align="center">
  <img src="screenshots/light.png" width="48%" alt="Human Typer in light theme">
  <img src="screenshots/dark.png" width="48%" alt="Human Typer in dark theme">
</p>

## Features

- **Global hotkey** (default `Ctrl+Alt+T`): works from any app, and you can change it in the window
- **Human-like rhythm**: variable speed (20-200 WPM) that drifts over time, longer pauses after punctuation, spaces and new lines, and occasional "thinking" pauses
- **Realistic typos**: optional wrong-neighbor-key typos that get noticed and corrected with backspace
- **Type N characters, then pause**: pause for a set time, or wait until you press the hotkey to resume
- **Clipboard mode**: copy text anywhere and have the hotkey type the clipboard directly
- **System tray**: runs in the background; right-click the tray icon for the menu
- **Light and dark themes**: follows your Windows setting automatically, or switch manually with the Theme button
- **Full Unicode**: emoji and accented characters work; newlines are sent as Enter
- No installer and no dependencies: one small `.exe`

## Download

Grab `HumanTyper.exe` from the [Releases](../../releases) page and run it. Windows SmartScreen may warn about an unsigned app: choose **More info > Run anyway**, or build it yourself from source (below).

## Usage

1. Paste your text into the window (or enable clipboard mode).
2. Click into the target text box in any program.
3. Press the hotkey. Press it again, or press `Esc`, to stop.

Settings are saved to `%APPDATA%\HumanTyper\settings.ini`.

## Build from source

Requires only Windows (uses the .NET Framework compiler included with it):

```
build.bat
```

This produces `HumanTyper.exe` next to the source file.

## Limitations

- Apps running as Administrator ignore keystrokes from a non-elevated program. Run `HumanTyper.exe` as administrator to type into them.
- Auto-indenting code editors add their own indentation on top of typed text.
- If another program already owns your hotkey, pick a different combination in the window.

## Releases

Pushing a tag like `v1.0.0` triggers the GitHub Actions workflow in `.github/workflows/build.yml`, which compiles the app and attaches `HumanTyper.exe` to a GitHub release.

```
git tag v1.0.0
git push origin v1.0.0
```

## Responsible use

Don't use this to bypass rules you've agreed to, such as exam or assessment proctoring, or anti-automation terms of a service. You're responsible for how you use it.

## License

[GNU GPL v3.0](LICENSE). You're free to use, modify and share this app, but any distributed modified version must also be released under GPL-3.0 with its source code.
