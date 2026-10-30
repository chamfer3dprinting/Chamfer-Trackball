# Chamfer Trackball

Editable Windows source for Chamfer Trackball lives here. GitHub Actions builds and verifies the single-file executables; no development folder is needed on the owner's PC.

## Editions and release channels

- **Admin v1.5.5** keeps the owner's requested version number, with a unique increasing build number and source revision for each change. About & updates follows `admin-v1.5.5-build.N` owner prereleases.
- **Public v1.0.1** is an unpublished candidate. Verified binaries are stored in GitHub draft releases `candidate-v1.0.1-build.N`.
- Only **Release for public** in the authenticated Admin app creates and activates `public-v1.0.1`. Public apps ignore every Admin, candidate, draft and legacy preview release.
- Existing tags and binaries are retained. A changed binary always receives a new build tag. Update downloads must match GitHub's SHA-256 digest.
- Admin release operations require the owner's GitHub token with repository Contents read/write access. The token is encrypted for the current Windows user using DPAPI and is never included in source, logs, settings JSON or executables. The public edition rejects Admin commands.

## Buttons, wheels and guide

The requested physical layout is btn1 **Middle** top-left; btn2 **Back** top-right; btn3 **Left click** bottom-left; btn4 **Right click** bottom-right. The left wheel scrolls vertically and the other wheel retains horizontal scrolling. Scroll direction is preserved.

In Buttons & keys, choose **Identify trackball**, then roll only the trackball. The raw device identity is saved privately in its existing settings file. The click and wheel-axis corrections then affect that device only. Other mouse buttons and wheels are replayed unchanged. No Windows-wide button swap or wheel setting is used. The checkboxes allow the owner to disable each correction independently.

A hook withholds physical button/wheel edges; Raw Input supplies their device identity before the app applies or bypasses the mapping. Injected echoes are ignored. Button-up uses the mapping chosen for button-down, including across configuration changes. Unmatched events time out and replay unchanged, disabling correction for the session. Driver-level filtering is not installed. Physical device behavior on the owner's Windows system still requires verification.

The draggable, always-on-top guide shows the same four-button arrangement, current program/mode and roll/wheel functions. It does not activate or take keyboard focus. Minimize/restore it in its header; restore a closed guide from the tray or settings. The original Chamfer cube logo is embedded in the guide, settings, tray and executable.

## 3D controls

- Hold **Shift** and roll: orbit.
- Hold **Ctrl+Shift** and roll: pan; use the wheel simultaneously for zoom.
- Hold **Alt** and roll: snap to aligned face/edge/corner views.
- Hold **backtick** and roll: navigate; add **Alt** for height.
- Ctrl by itself remains normal. All held bindings are editable and saved; complete and incomplete button combinations survive restart.

The M512's standard mouse report supplies X/Y and separate wheels. It cannot independently measure ball twist about a third axis. Camera rotation supports three axes using ball X/Y plus a wheel; this is not a claim of added hardware sensing.

Blender and Fusion adapters are embedded and can be installed from CAD apps inside the executable, then enabled in the host. They exchange scene camera/pivot data, ray-pick a cursor pivot when available and support aligned snapping and navigation. Other applications use editable orbit/pan/zoom emulation profiles. True scene pivot and navigation require a native adapter for that application. Live CAD hosts have not been verified in this environment.

## Build and verification

The Windows workflow runs core regressions, JavaScript/Python syntax checks, builds Admin and public editions, then starts each real WebView2 interface and tests its bridge, logo and saved combination. It captures the settings interface and floating guide. Only successful current-main builds upload an Admin prerelease and an unpublished public candidate.

Build: `dotnet publish src/Chamfer.Trackball.App -c Release -r win-x64 --self-contained true -p:Edition=Admin -p:BuildNumber=N -p:SourceRevision=COMMIT`. Omit Edition or set Public for the public candidate. Microsoft Edge WebView2 Runtime is required by the embedded settings interface.

## Validation · 2026-10-30

This change adds per-device mapping regressions for unchanged ordinary mice, paired releases, wheel axes and deltas, fail-open tickets and isolated update channels, on top of the existing saved-combination and camera/pivot checks. The workflow result records actual build/UI validation; physical M512 behavior and live Blender/Fusion host behavior need owner testing. No public channel promotion is performed by the build workflow.
