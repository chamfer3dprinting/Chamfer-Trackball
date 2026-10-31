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


# 2026-10-31 — Enable and smooth ball-only 3D navigation

Enable orbit, the M512 Back-button orbit/pan toggle, and circle zoom in the dedicated 3D modelling program by default. Upgrade existing v22 settings once without changing the separate Normal mouse program, other programs, or user key bindings. Implement the saved short soft-stop motion feel and coalesce obsolete absolute camera poses per native session so a delayed client applies the newest view instead of replaying stale frames. Make the Home and Circle zoom instructions describe the setup and gesture directly.

GitHub Actions run 41187617347 passed the core regression checks, interface and integration syntax checks, Admin and Public builds, and actual WebView interface/bridge smoke checks. The first run, 41187467690, exposed an outdated reset assertion that expected every program's buttons to be normal; the existing assertion was aligned with the separate 3D Back toggle and the rerun passed. The build produced Admin prerelease v1.5.14; the public build is still an unpublished draft, and no public stable release was activated. The physical M512 still needs owner confirmation for zoom feel and responsiveness in Chamfer Studio. Standard M512 reports do not provide independent physical twist. Next: install Admin v1.5.14, select 3D modelling, test orbit/pan/circle zoom, and adjust glide or circle size from the UI if needed.


# 2026-10-31 — Remove idle work that can make Trackball feel stuck

The owner reports the Admin app still feels laggy and can stick. Replace the persistent native WebSocket sender's 8 ms polling loop with a semaphore wake-up when a message is queued and a 1 s idle heartbeat. Reduce foreground/profile checks to 20 Hz while no navigation mode is active; retain 16 ms updates while navigating. Cache the guide's visibility setting by configuration revision instead of deep-copying settings every 100 ms.

This addresses measurable background work in the app and gives native clients lower-latency wake-up. The latest source must pass the existing CI regression, interface, build, and WebView smoke checks. The owner's physical maximized-window and Chamfer Studio test remains the final check; do not claim it is resolved until they confirm. Admin prerelease only; no public stable release.
