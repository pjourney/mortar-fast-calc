# Build and maintenance

## Requirements

Build on 64-bit Windows with PowerShell and the installed .NET Framework 4.x compiler at `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. The supported application runtime is Windows 10/11 with .NET Framework 4.8 or later. No .NET SDK, Visual Studio project, or NuGet restore is required.

Use Git for repository work and Python 3 for the privacy scanner. Neither is needed to run the app. WPF interaction tests open temporary windows and require an interactive Windows desktop. The released application is unsigned.

## Source map

| File | Responsibility |
| --- | --- |
| [Core.cs](../src/Core.cs) | Coordinate parsing, grid distance, compass bearing, community-table interpolation, and callout formatting |
| [App.cs](../src/App.cs) | Application entry point, input/output bindings, shortcuts, named targets, search, favorites, and session XML |
| [Desktop.cs](../src/Desktop.cs) | Responsive panel layout, window placement, monitor work-area fitting, and display-change handling |
| [MortarScene.cs](../src/MortarScene.cs) | Procedural WPF 3D geometry and independent camera controls |
| [MainWindow.xaml](../src/MainWindow.xaml) | Native controls, fixed aiming strip, styles, and saved-target row template |
| [l81.csv](../src/l81.csv) | Embedded, pinned L81 community data |
| [Tests.cs](../tests/Tests.cs) | Calculation, WPF interaction, persistence, layout, and security checks |
| [build.ps1](../build.ps1) / [package.ps1](../package.ps1) | Compiler invocation and explicit release file list |

The project uses the C# language features supported by the bundled .NET Framework compiler. When adding a source file, add it to the compiler command in `build.ps1` and the package allowlist in `package.ps1`.

## Build and test

Run from the project root:

```powershell
.\build.ps1 -OutputDirectory build-preview
$test = Start-Process .\build-preview\WardogsFastCalc.exe -ArgumentList '--test tests\test-results.txt' -WindowStyle Hidden -PassThru -Wait
Get-Content .\tests\test-results.txt
if ($test.ExitCode -ne 0) { throw 'Tests failed' }
```

`build-preview` is ignored by Git, so this does not replace an open release executable. Test mode writes its report and screenshots beside the chosen report path. Session fixtures use a unique temporary directory and are removed after WPF shutdown. It does not load the real `%LOCALAPPDATA%\WardogsFastCalc\session.xml`.

The current recorded result is **202 assertions passed**. Inspect the generated images after layout changes; passing geometry checks alone does not establish visual quality. [VALIDATION.md](../tests/VALIDATION.md) lists tested sizes, workflows, and limitations. Synthetic key-event tests inject a modifier-key reader to avoid interference from physical keys held during the run; normal application input reads the real keyboard state.

## Behavior to preserve

- An invalid coordinate or override clears stale output and the 3D target marker.
- Bearing uses the game's positive-Y-north convention and clockwise compass headings. A manual range changes the MIL estimate, not the bearing.
- Grid distances within `1e-9` meters of either table endpoint are normalized to that endpoint to absorb floating-point arithmetic error. This does not round all ranges to whole meters or change the manual-override range check.
- Saved-list Enter reuses only the target/name and clears the override. Shift+Enter restores the saved origin, target, name, and override.
- Favorites sort ahead of non-favorites and are protected from automatic eviction. Explicit removal can still delete them.
- The aiming strip sits outside the scrolling controls. Layout changes must preserve input state, saved-list selection, and keyboard access.
- Session XML must retain DTD rejection, a disabled resolver, document and field limits, and compatibility with older unnamed saves. New fields must tolerate missing values.

Window placement is stored in WPF layout units. Monitor work areas are converted from device pixels using the window's device transform. The manifest remains system-DPI-aware; physical hot-plug and mixed-DPI transitions are outside the verified test coverage.

## Package a release

After a successful build and test run, close the running release normally so it saves its session, then run:

```powershell
Copy-Item .\build-preview\WardogsFastCalc.exe .\dist\WardogsFastCalc.exe -Force
.\package.ps1
```

The ZIP contains `WardogsFastCalc.exe` at its root, documentation, source, tests, and research attribution. The executable in the repository lives in `dist/`. `dist/SHA256.txt` records the distribution executable's SHA-256 hash; it is not a hash of the ZIP and is not a digital signature.

Verify that the ZIP contains the same executable:

```powershell
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead((Join-Path $PWD 'WardogsFastCalc-Windows.zip'))
try {
    $entry = $archive.GetEntry('WardogsFastCalc.exe').Open()
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $zipHash = [BitConverter]::ToString($sha.ComputeHash($entry)).Replace('-', '')
    } finally {
        $entry.Dispose()
        $sha.Dispose()
    }
    if ($zipHash -ne (Get-FileHash .\dist\WardogsFastCalc.exe -Algorithm SHA256).Hash) {
        throw 'Packaged executable differs from the distribution build'
    }
} finally {
    $archive.Dispose()
}
```

For documentation-only changes, keep the already-tested executable and rerun `package.ps1` so downloaded documentation matches the repository. Review local Markdown links and ensure newly added documents are explicitly listed in the package script.

## Review before pushing

In a Git checkout:

```powershell
git diff --check
python scripts/audit_repo.py --history
```

Stage the reviewed files, then scan the exact staged content with `python scripts/audit_repo.py --staged --history`. The scanner enumerates tracked files, so new files must be added to Git before that final scan. Verify the commit uses a project-only or privacy-protecting identity. Do not put authentication tokens in remote URLs, command files, source, or documentation.

The source ZIP excludes `.git`, so the repository scanner requires a Git checkout rather than a bare extracted package. Screenshots committed to the repository must use synthetic examples; local session files and private backups must remain outside the package. See [SECURITY_AUDIT.md](../SECURITY_AUDIT.md) for scan scope and history-cleanup limits.

## Updating game data

Follow the pinned-source provenance in [RESEARCH.md](../RESEARCH.md). Verify a new game-data source before replacing the CSV; record its revision and date, preserve licensing, and update calculation tests. An app UI release does not by itself establish that the game table is still accurate. Do not describe the illustrative 3D tilt as a measured barrel angle or a predicted trajectory.
