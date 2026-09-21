# Change history

Entries describe application changes; the Git commit identifies the corresponding source revision. Downloadable packages also include documentation updates made after that revision.

## September 20, 2026 — Saved targets and desktop layout

Application revision: [eabb432](https://github.com/pjourney/mortar-fast-calc/commit/eabb432).

- Added optional target names, renaming, favorites, and case-insensitive search by name or coordinates.
- Added separate **Use target** and **Restore setup** actions. Enter and double-click now reuse the target from the current mortar position and clear the distance override. Shift+Enter restores the entire saved setup.
- Preserved old unnamed saves. Equivalent coordinate formatting no longer creates duplicate setups; favorites are protected from automatic removal at the 20-target limit.
- Kept bearing, range, and MIL visible while the controls scroll. Narrow windows stack the panels; wide windows place inputs beside the 3D scene and saved list.
- Added window-size, position, and maximized-state persistence, with bounds adjusted to available monitor work areas.
- Made disk-save failures visible and used unique temporary filenames for session writes.
- Expanded validation to 202 assertions, including saved-target workflows, six layout sizes, and window-placement recovery.

The keyboard behavior change is intentional: **Enter uses the selected target; Shift+Enter restores the saved setup.** See the [user guide](README.md#saved-targets).

## September 20, 2026 — Calculation and keyboard fixes

Application revision: [256f0cb](https://github.com/pjourney/mortar-fast-calc/commit/256f0cb).

- Fixed exact coordinate-derived 132 m and 684 m ranges being rejected due to floating-point error.
- Fixed saved overrides containing surrounding spaces or only whitespace disappearing after restart.
- Enabled the main keyboard's Shift+plus shortcut for 3D zoom.
- Preserved saved-list focus and selection after deletion so repeated Delete presses work.
- Expanded the suite to 149 assertions at this revision.

## September 20, 2026 — Audited distribution

- Published the native WPF calculator with the procedural 3D alignment view, keyboard controls, custom Windows icon, and pinned community L81 data.
- Replaced recursive packaging with an explicit file list and isolated synthetic test sessions from user data.
- Hardened local XML parsing and added the repository privacy scanner.
- Applied a project-only Git identity. The limitations of repository-history cleanup remain documented in [SECURITY_AUDIT.md](SECURITY_AUDIT.md).

Game-data provenance remains dated September 19, 2026. These application changes did not introduce new in-game measurements or change the pinned community table.
