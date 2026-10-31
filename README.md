# Chamfer Trackball

Editable Windows source for Chamfer Trackball lives here. GitHub Actions builds and verifies the single-file executables; no development folder is needed on the owner's PC.

## Editions and release channels

- **Admin versions advance for each delivered update**, for example v1.5.7 → v1.5.8. The sidebar, About and update notifications display the real executable version. About & updates follows `admin-vX.Y.Z` owner prereleases. Internal build/source metadata stays available for diagnostics.
- **Public v1.0.1** is an unpublished candidate. Verified binaries are stored in GitHub draft releases `candidate-v1.0.1-build.N`.
- After a public release, the next source build automatically advances the public patch version (1.0.1 → 1.0.2), while remaining a draft. Withdrawn/reserved public tags also reserve their version so a later binary cannot reuse it. Only **Release for public** in the authenticated Admin app creates and activates `public-v1.0.1`. Public apps ignore every Admin, candidate, draft and legacy preview release.
- Existing tags and binaries are retained. A changed binary always receives a new build tag. Update downloads must match GitHub's SHA-256 digest. Click Update to save the current settings, download, install and restart. Semantic version comparisons reject current-version reinstalls and downgrades. Compatibility entries let existing build-number Admin apps install the new version through their existing Update button.
- Admin release operations require the owner's GitHub token with repository Contents read/write access. The token is encrypted for the current Windows user using DPAPI and is never included in source, logs, settings JSON or executables. The public edition rejects Admin commands.

## Buttons, wheels and guide

The requested physical layout is btn1 **Middle** top-left; btn2 **Back** top-right; btn3 **Left click** bottom-left; btn4 **Right click** bottom-right. The left wheel scrolls vertically and the other wheel retains horizontal scrolling. Scroll direction is preserved.

In Buttons & keys, choose **Identify trackball**, then roll only the trackball. The raw device identity is saved privately in its existing settings file. Identification preserves the original clicks and wheel axes. Optional click and wheel-axis corrections affect that device only when enabled. Other mouse buttons and wheels are replayed unchanged. No Windows-wide button swap or wheel setting is used. The checkboxes allow the owner to disable each correction independently.

A hook withholds physical button/wheel edges; Raw Input supplies their device identity before the app applies or bypasses the mapping. Injected echoes are ignored. Button-up uses the mapping chosen for button-down, including across configuration changes. Unmatched events time out and replay unchanged, disabling correction for the session. Driver-level filtering is not installed. Physical device behavior on the owner's Windows system still requires verification.

The draggable, always-on-top guide shows the same four-button arrangement, current program/mode and roll/wheel functions. It does not activate or take keyboard focus. Minimize/restore it in its header; restore a closed guide from the tray or settings. The original Chamfer cube logo is embedded in the guide, settings, tray and executable.

## 3D controls

- Hold **Shift** and roll: orbit.
- Hold **Ctrl+Shift** and roll: pan; use the wheel simultaneously for zoom.
- Hold **Ctrl** and roll up/down: zoom.
- Hold **Alt** and roll: snap to Top, Bottom, Left, Right, Front or Back. Diagonal edge and corner views are excluded.
- Hold **Windows** and roll: navigate; add **Alt** for height. Release does not open Start in this 3D context.
- The 3D modelling program starts with ordinary pointer movement. Hold a navigation key to move the camera; release it to point again. Ctrl+C, Ctrl+V and other typing shortcuts stay normal. Held orbit, pan and zoom bindings are editable and saved; complete and incomplete button combinations survive restart.
- **Ball navigation** is an optional quick setup: roll to orbit; tap Back to switch only between orbit and pan; draw small clockwise/anticlockwise circles to zoom in/out. Straight rolling returns to orbit or pan. The Circle size and Zoom sensitivity settings tune the gesture. Held keys/buttons override circle recognition.

Factory startup and Reset to normal select **Normal mouse**, with original buttons/wheels and no navigation bindings. Reset also disables optional device corrections. Identifying a trackball does not enable swaps automatically. Settings writes are queued so a slow save cannot replace a newer combination or binding edit.

The M512's standard mouse report supplies X/Y and separate wheels. It cannot independently measure ball twist about a third axis. Camera rotation supports three axes using ball X/Y plus a wheel; this is not a claim of added hardware sensing.

Blender and Fusion adapters are embedded and can be installed from CAD apps inside the executable, then enabled in the host. They exchange scene camera/pivot data, ray-pick a cursor pivot when available and support aligned snapping and navigation. Other applications use editable orbit/pan/zoom emulation profiles. True scene pivot and navigation require a native adapter for that application. Live CAD hosts have not been verified in this environment.


## Current controls and motion settings

The Normal mouse program preserves the manufacturer's clicks and wheel axes. The confirmed btn3 + btn4 rotation toggle is retained as a program default and only acts in a 3D context. The 3D modelling program also points normally until a command is held:

| Hold with the ball | Action |
| --- | --- |
| Shift | Orbit |
| Ctrl + Shift | Pan; the wheel zooms |
| Ctrl | Zoom in/out by rolling up/down |
| Alt | Straight rolls change faces; a clockwise/counterclockwise curved roll turns the face by 90° |
| Alt + wheel | Turn the aligned face by 90 degrees |
| Windows | Move through the scene |
| Windows + Alt | Move vertically through the scene |

Windows release does not open Start when reserved by the active program in a 3D view. Other system shortcuts and Windows input outside that context remain normal. All held controls are editable. Snap and Navigate require a native integration.

Pointer speed/effective DPI now changes the identified trackball's delivered pointer counts. Hardware DPI selection only calibrates the estimate. Pointer speed, Orbit/Pan/Zoom speed, soft stop and glide are saved per program and retained by Add, Duplicate, switching and restart. Pointer gain does not multiply camera speed. Smooth 3D speed has a 1% low end, 100% at the center and a zero stop value. Normal mouse pointer DPI and glide are independent program settings. Soft stop applies to pointing and navigation; clicks and mode changes stop the tail. A live keyboard on Home and in the guide highlights currently held shortcut keys without recording typed text.

Studio uses its Y-up view cube for upright snaps; existing Z-up adapters keep their coordinate convention. While snapping, a clockwise/counterclockwise X/Y loop turns the selected face like the cube arrows; roll straight again to change faces. Recognition adapts to the size of the roll. The floating guide and Home show “Curve detected” then “Clockwise 90°” or “Counterclockwise 90°” when a turn is recognized. The standard M512 sensor still supplies only X/Y ball movement. A pure in-place twist with no X/Y report cannot be sensed; the face turn is a gesture inferred from a curved roll path.


### Saved presets

In Buttons & keys, choose **Save current as preset**, give the setup a name, and save. The Speed & direction page has the same Save control. Choose a saved preset and click **Apply** to use it in the active program. Rename or delete a preset from the same card; saving an existing name asks before replacing it.

Presets include buttons, held keys, wheels, combinations, gestures, pointer speed, Orbit/Pan/Zoom speed and motion feel. They survive app restarts and control resets. Applying one preserves the program name, other programs and selected trackball. Hardware DPI calibration and general app settings stay outside a program preset.

## Build and verification

The Windows workflow runs core regressions, JavaScript/Python syntax checks, builds Admin and public editions, then starts each real WebView2 interface and tests its bridge, logo and saved combination. It captures the settings interface and floating guide. Only successful current-main builds upload an Admin prerelease and an unpublished public candidate.

Build: `dotnet publish src/Chamfer.Trackball.App -c Release -r win-x64 --self-contained true -p:Edition=Admin -p:BuildNumber=N -p:SourceRevision=COMMIT`. Omit Edition or set Public for the public candidate. Microsoft Edge WebView2 Runtime is required by the embedded settings interface.

## Validation · 2026-10-30

This change adds per-device mapping regressions for unchanged ordinary mice, paired releases, wheel axes and deltas, fail-open tickets and isolated update channels, on top of the existing saved-combination and camera/pivot checks. The workflow result records actual build/UI validation; physical M512 behavior and live Blender/Fusion host behavior need owner testing. No public channel promotion is performed by the build workflow.

Verified build 14: [Windows workflow run](https://github.com/chamfer3dprinting/Chamfer-Trackball/actions/runs/41053380169) passed all 70 checks, syntax validation, both publishes and both actual UI smoke tests. Source: `8af36c29100ce74929b2e24d4f5313566119e024`. Admin executable SHA-256: `c348a4ab982946e12a9d1307f76f6c9bf2738a22d6c1ad2822bd3b5216e456b2`. Public v1.0.1 build 14 remains a draft.

## Ball navigation verification · 2026-10-30

Verified Admin build 17: [Windows workflow run](https://github.com/chamfer3dprinting/Chamfer-Trackball/actions/runs/41057812611) passed 94 core regressions, the concurrent-save regression, interface/integration syntax checks, both single-file publishes and both real WebView2 smoke tests. Source: `57fbb9559096cc44f8d977cd5bcb0183e8a3994a`. Admin asset SHA-256: `5746b8aaa57b2136ae9312436d7fa6a1456155ccbcac5c1a4d9f0f46e6c4501f`. Public candidate v1.0.1 build 17 remains a draft. Physical gesture feel and live CAD hosts still require owner verification.

## Versioning and public release checks · 2026-10-30

The owner requested visible vX.Y.Z updates instead of build-number updates. Admin and public editions retain separate release channels. The cloud workflow chooses a new Admin patch after all reserved Admin/legacy version tags, stamps that value into the executable, then creates the matching public candidate. Public versions advance after any reserved public tag, including withdrawn releases.

Admin Release for public lists candidates across paginated history, verifies the download, uploads the unchanged public binary into a draft, verifies GitHub's digest, then publishes. Withdraw returns the release to a draft; re-release preserves the original binary. Isolated tests exercise the actual release service with simulated authenticated GitHub responses, including public update discovery and permission/digest failures. Live publishing remains the owner's Admin action. Cloud validation passed; see the verified version below.

Verified v1.5.7: [Windows run 41068244612](https://github.com/chamfer3dprinting/Chamfer-Trackball/actions/runs/41068244612) passed 101 core checks, semantic-version/reserved-tag tests, one-click Update and concurrent-save checks, both publishes, both real WebView2 interface tests, and the actual Admin release service's in-memory authenticated API tests. Those cover paginated candidate discovery, verified download/upload, public updater discovery, duplicate prevention, withdrawal/re-release, corrupt assets and permission failures. Source: `09f4117eadd6a6c7108422d20a176eae7c91e829`. Admin SHA-256: `c1deff50d79fc52b001c05ba78f19d07e17babba41a792a3c827b58a1b35c624`. Public v1.0.1 candidate revision 18 remains a draft, SHA-256 `a8e9f8b36053ecf70bcc0e3dbe634d09fd3fcb5c017345d768a048df53f1a9a7`. Live public publishing was not performed.
