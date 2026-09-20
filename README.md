# WARDOGS Mortar Fast Calc

![Application icon](src/AppIcon.png)

[Download the Windows package](https://github.com/pjourney/mortar-fast-calc/raw/refs/heads/main/WardogsFastCalc-Windows.zip)

Double-click **WardogsFastCalc.exe** in the extracted Windows package, or **dist/WardogsFastCalc.exe** in the source project. No installer, browser, account, or network connection is needed. This is a native C# / WPF desktop application for Windows 10/11 with .NET Framework 4.8 or later. It uses the framework already installed on this computer.

## Quick start

1. In WARDOGS, right-click your mortar's position on the map and choose **Mark Coordinates**. Copy its coordinate pair from the chat input.
2. Paste into **Your mortar**. Repeat for the **Target** field.
3. Leave **Distance override** blank for calculated distance, or enter your measured game distance in meters.
4. Match the app's compass bearing and **RNG** value in the game's mortar sight. The MIL value is an additional community-table estimate.

Try **Load example** first: `x98.43, y110.38` to `x94.53, y109.03` produces approximately **413 m**, **250.9° WSW**, and **569 MIL**.

The app automatically recalculates while typing. Invalid input clears the old result. An override is explicitly labeled and changes range and the MIL estimate; heading continues to come from the coordinate pair. **New** clears the target and distance while keeping your mortar position.

## Keyboard

| Key | Action |
| --- | --- |
| Tab / Shift+Tab | Next / previous control |
| Ctrl+1 | Select your mortar coordinates |
| Ctrl+2 | Select target coordinates |
| Ctrl+3 | Select distance override |
| Enter while editing | Save current setup |
| Ctrl+N | New target; retain mortar |
| Ctrl+Shift+C | Copy the result as a text callout |
| Ctrl+H | Focus saved targets |
| Up / Down, then Enter | Choose and recall a saved setup |
| Delete in saved targets | Remove the selected setup |
| Ctrl+T | Toggle always on top |
| F1 | Open the in-app guide |
| Alt+F4 | Close |

Shortcuts work while the calculator has focus. Alt+Tab between the game and calculator. “Keep on top” is useful alongside a windowed/borderless game; exclusive fullscreen can cover other windows.

## Interactive 3D alignment view

The interface uses a quiet monochrome palette and a shaded 3D mortar to make the calculated alignment visible. A compass plane and target marker show the direction, while the tube becomes steeper as the community MIL value increases.

Drag the model to orbit, scroll to zoom, or press **Ctrl+4** to focus the view. Arrow keys orbit, **+ / -** zoom, **Space** switches between perspective and overhead views, and **Home** resets the camera. The Top and Reset buttons provide the same view controls. Camera movement never changes your coordinates or calculated aiming values.

The model is schematic: bearing follows the calculated compass heading exactly, but tilt is an illustrative mapping of the game's MIL table rather than a literal angle. The target ring is not a distance scale. It shows no predicted shell path, obstructions, terrain, or live game state. Missing or invalid input removes the target marker; an out-of-range target retains its bearing with an amber marker and no elevation estimate.

## Input and scope

Accepted formats include `x98.43, y110.38`, `98.43 110.38`, `98.43 / 110.38`, and `(98.43, 110.38)`. Labeled decimal commas work: `x98,43 y110,38`. Unlabeled values use decimal points, X first. Paste the coordinate pair itself, without player names or timestamps.

The current Bakurani, Ozeti, and Zestafona community configurations share a scale of 100 meters per coordinate unit. X increases east and Y increases north. No map selector is necessary for this coordinate-only calculation. The input envelope is 0–164 for each game coordinate; this is not a geographic latitude/longitude tool.

L81 only: the researched community table covers 132–684 meters. Estimates use linear interpolation of that game's table. Outside the interval the app shows a range warning and suppresses MIL. It does not compute terrain-height, wind, or obstruction corrections. Community data may change with patches. The live game sight takes precedence; the application has not been validated by firing rounds in a live match.

## Saved data

The last inputs, keep-on-top preference, and up to 20 saved setups are stored at:

`%LOCALAPPDATA%\WardogsFastCalc\session.xml`

Saving a setup or closing the app writes state. Each saved setup includes the mortar coordinates, target coordinates, and optional distance override. The app does not access game memory, inject inputs, monitor the clipboard, or use the network. Copy reads no clipboard contents and runs only when requested.

## Build and tests

From the project directory, in PowerShell:

```powershell
.\build.ps1
$test = Start-Process .\dist\WardogsFastCalc.exe -ArgumentList '--test tests\test-results.txt' -PassThru -Wait
Get-Content .\tests\test-results.txt
$test.ExitCode
```

The build uses the Windows .NET Framework C# compiler with no downloaded NuGet dependencies. The executable embeds its XAML, L81 table, and application icon. Test mode runs calculation and WPF interaction checks, writes its report and rendered UI images, and exits with code 0 on success. Test sessions are isolated from real saved setups.

The vector icon source is `src/AppIcon.svg`. Run `src/build-icon.ps1` to regenerate the PNG and Windows ICO, which contains nine resolutions from 16 through 256 pixels. The executable includes native Windows icon resources and the window uses the same embedded asset.

Use `package.ps1` to create the Windows ZIP from an explicit file list. Never archive entire local test or build directories: they can contain private session data.

Security/privacy checks: `python scripts/audit_repo.py --history` (Python 3, standard library only). See **SECURITY_AUDIT.md** for findings, fixes, and the limits of history cleanup. Use a project-only or privacy-protecting Git identity when contributing.

Source: `src/`. Tests: `tests/Tests.cs`. Research and exact upstream commit: **RESEARCH.md**. Third-party license: **THIRD-PARTY-NOTICES.txt**.
