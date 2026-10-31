# Chamfer Trackball working agreement

The owner authorized rebuilding the editable source in this existing repository.
Keep source, tests and version history on GitHub. Do not create development folders
on the owner's PC. Deliver only the executable as the user-facing download.

Preserve standard mouse clicks and wheels by default. Never change Windows-wide
mouse button swapping to implement app bindings. Never use Ctrl alone for navigation.
The requested held controls are Shift orbit, Ctrl+Shift pan plus zoom, Alt plus ball
for aligned snapping, and backtick navigation with Alt for height.

Keep existing release history. Advance the real application version for a delivered
update; do not reuse a release tag for a different executable. Build and test before
publishing. Treat a prerelease as distinct from a public stable release.

Do not claim that the M512's ordinary X/Y input measures independent three-axis ball
rotation. Native cursor pivots, snap and camera navigation require host camera/scene
APIs. Label native integration and mouse emulation accurately.

Before editing, read the current main branch. Preserve other agents' changes and
do not force-push. Add a dated note with changes, validation, limitations and next work.
Keep credentials and local user settings out of source. Use the GitHub connector for
repository updates; keep all source changes reviewable.

Manufacturer defaults confirmed by the owner's M512 diagram:
btn1 = Middle, btn2 = Back, btn3 = Left click, btn4 = Right click.
Preserve native wheel axes. The optional trackballSwapButtons and
trackballSwapWheels corrections default to false. Do not enable them based on
button numbers or the UI's descriptive labels; enable a correction only when
a physical test establishes that it is needed. Retain the owner's confirmed
choice across updates and preserve custom programs/combinations when restoring
native mouse inputs.

# 2026-10-31 — Reduce Admin lag when maximized

The owner reports the Admin app lags and becomes difficult to use, especially when maximized. The Home page was calling the WebView-to-C# status bridge on every animation frame, potentially 60–144 calls per second. Cap active Home/Controls status updates at 20 Hz, other visible pages at 4 Hz, and hidden pages at 1 Hz. Keep the native trackball controller on its 16 ms input timer. Add interface regression coverage for the 20 Hz cap and unchanged sensor interval.

Validation is included in the existing GitHub Actions build workflow. A physical maximized-window performance check remains for the owner after the new Admin build is ready.


# 2026-10-31 — Verify Trackball Admin responsiveness build

GitHub Actions run 41186358288 passed the core regressions, interface/syntax checks, Admin and Public executable builds, and actual WebView interface/bridge smoke checks. The Admin-only v1.5.13 executable is available at the Admin prerelease. Its release contains only ChamferTrackball-Admin.exe. The public Trackball candidate remains an unpublished draft; no public release was activated.

The updated UI caps active status refresh at 20 Hz and leaves the 16 ms device-input loop intact. The automated interface check confirms that the UI no longer polls on every animation frame. A physical maximized-window performance test remains for the owner. Next: update Admin to v1.5.13 and confirm the maximized window remains responsive.
