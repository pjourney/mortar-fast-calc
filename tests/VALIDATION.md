# Validation

Windows host, September 20, 2026.

- Built with the installed .NET Framework compiler; no external dependencies downloaded.
- 202 automated assertions pass in the compiled executable. Full output: `test-results.txt`.
- Bug-pass regressions: exact coordinate-derived 132 m and 684 m endpoints remain in range despite floating-point error; genuinely out-of-range distances remain rejected. Saved distance values with surrounding spaces or only whitespace survive a session restart. Shift+plus zooms the scene, and deleting a selected history row retains focus and selection for repeated keyboard deletion. Each bug was reproduced by a failing check before its fix.
- Calculation coverage: coordinate formats, decimal conventions, malformed/ambiguous input, all eight compass directions, published example, range endpoints, interpolation, manual overrides, coincident points, nonfinite values, and culture-independent output.
- WPF coverage: live updates, invalidation of stale values, tab order, select-all focus, keyboard commands, saved setup recall, duplicates, range warnings, session persistence, history limits, and corrupt-file recovery.
- 3D coverage: heading correspondence on all eight compass directions, target invalidation, out-of-range state, relative tilt, routed arrow-key camera movement, Ctrl+4 focus, overhead toggle, camera reset, bounded zoom, and independence from calculated aiming values.
- Security coverage: DTD and external entity rejection, document and field size limits, expected XML root, and invalid history filtering. Sessions are isolated in a unique temporary directory and cleaned after WPF shutdown.
- Saved-target coverage: names, favorites and their ordering/eviction protection, case-insensitive search, coordinate search, search keyboard navigation, target-only reuse, complete setup restoration, rename confirmation/cancellation, old-session migration, and visible disk-save failures.
- Responsive layout tested at 560 × 500, 760 × 780, 999 × 700, 1000 × 700, 1200 × 650, and 1400 × 1000 logical pixels. Checked the breakpoint, horizontal overflow, fixed aiming results while scrolling, and keyboard access to history at the minimum size. Inspected default, short/wide, minimum-size, saved-target, and narrow-history renders.
- Window placement coverage: size restoration, maximized state after a minimized close, real off-screen startup recovery, and simulated disconnected-monitor, negative-coordinate monitor, oversized-window, and small high-DPI work-area cases. Keyboard modifier input is isolated for synthetic routed-event tests so physical user key presses cannot change test results.
- Launched the actual Windows executable and visually confirmed Load example → 413 m / 250.9° WSW / approximately 569 MIL, Enter-to-save, and Ctrl+3 focus on the override field. UI Automation text was stale on this host; visual screenshots were used to verify those interactions.

Limit: no live-match shot validation, physical monitor hot-plug, mixed-monitor DPI transition, or different-Windows-version test was performed. Monitor work-area edge cases above use synthetic rectangles; the existing system-DPI-aware manifest remains unchanged.
