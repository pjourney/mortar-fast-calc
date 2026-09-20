# Validation

Windows host, September 20, 2026.

- Built with the installed .NET Framework compiler; no external dependencies downloaded.
- 149 automated assertions pass in the compiled executable. Full output: `test-results.txt`.
- Bug-pass regressions: exact coordinate-derived 132 m and 684 m endpoints remain in range despite floating-point error; genuinely out-of-range distances remain rejected. Saved distance values with surrounding spaces or only whitespace survive a session restart. Shift+plus zooms the scene, and deleting a selected history row retains focus and selection for repeated keyboard deletion. Each bug was reproduced by a failing check before its fix.
- Calculation coverage: coordinate formats, decimal conventions, malformed/ambiguous input, all eight compass directions, published example, range endpoints, interpolation, manual overrides, coincident points, nonfinite values, and culture-independent output.
- WPF coverage: live updates, invalidation of stale values, tab order, select-all focus, keyboard commands, saved setup recall, duplicates, range warnings, session persistence, history limits, and corrupt-file recovery.
- 3D coverage: heading correspondence on all eight compass directions, target invalidation, out-of-range state, relative tilt, routed arrow-key camera movement, Ctrl+4 focus, overhead toggle, camera reset, bounded zoom, and independence from calculated aiming values.
- Security coverage: DTD and external entity rejection, document and field size limits, expected XML root, and invalid history filtering. Sessions are isolated in a unique temporary directory and cleaned after WPF shutdown.
- Inspected perspective and overhead renders and the compact layout. The scene shrinks on shorter windows to keep the main aiming values visible.
- Rendered and inspected default and minimum-size layouts, including an out-of-range state. The smaller window scrolls; all controls remain keyboard-accessible.
- Launched the actual Windows executable and visually confirmed Load example → 413 m / 250.9° WSW / approximately 569 MIL, Enter-to-save, and Ctrl+3 focus on the override field. UI Automation text was stale on this host; visual screenshots were used to verify those interactions.

Limit: no live-match shot validation and no multi-monitor DPI or different-Windows-version test was performed.
