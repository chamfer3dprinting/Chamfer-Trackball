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
