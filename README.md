# Chamfer Trackball

## Multiple trackballs and motion feel

In **Buttons & keys → Your trackballs**, use **Add or identify trackball** and roll only the device you are adding. Each identified trackball stays remembered, including the M512 when you add a SlimBlade Pro. Connected devices switch automatically when moved; ordinary mice remain ordinary mice. A held button or drag keeps its device until release. Click and wheel corrections are separate per trackball; keyboard bindings, speed settings and other controls use the selected program. Rename each saved trackball or choose which device's corrections to edit. A new USB/Bluetooth device identity may need one identification.

Navigation requests an 8 ms WinForms timer interval. A standalone Windows timing test on the owner's PC measured roughly 31 ms for the former 16 ms request and 15.5 ms for the new request. This improves camera update cadence without changing the system timer resolution or adding smoothing delay. The standalone diagnostic is `tests/TimerCadenceProbe.ps1`. Physical motion feel and future SlimBlade reports still need testing on the device. Stock twist scrolling alone does not establish access to the full raw three-axis stream used by modified Rotatrix hardware; no stock-device polling-rate or simultaneous-axis guarantee is claimed.

## Camera walk

Select **3D modelling**, turn **3D controls on**, and use the Home cube or a supported 3D viewport.

- **Backtick (`)** + roll travels forward/back and sideways. Twist scroll up to turn right and scroll down to turn left.
- **Backtick + 1** + roll looks up/down and left/right. Twist scroll up to raise the camera and scroll down to lower it.
- **Backtick + Ctrl** + forward/back roll also raises and lowers the camera.

With Continuous navigation enabled, a Backtick twist starts a turn in the scroll direction. Heading and travel continue independently while held. Return the ball or twist to neutral to stop that axis, or release Backtick to stop. With Backtick + 1 held, the latest twist direction sets a continuous climb or descent. Twist the other way to reverse immediately. Release 1 or Backtick to stop changing height. Rotation speed controls looking, Pan / 3D speed controls travel and height, and Twist speed controls heading turns.

Change these extra holds under **Buttons & keys → Walk: look around / Walk: raise / lower**. Record only the additional key; the Camera walk hold is added automatically. Both save with programs and named presets. Existing saved programs/presets without them keep the previous controls until assigned. The owner update assigns 1 and Ctrl to the active 3D program after preserving its previous setup.

These separate look/height layers follow the [Rotatrix free-camera control model](https://openaxis.rotatrix.com/guide/free-camera/), with the owner's Backtick mapping. They do not add Rotatrix custom-firmware sensor reports to a stock trackball.

## Cube faces and orientation

Hold **Alt** and roll to snap to a neighboring face from your current angle. While holding Alt, twist the Kensington ball or scroll with the left wheel to rotate the current view by 90° around the cursor. A partial movement keeps the view in place. Click the **top-right button (button 2)** for the nearest flat face. **Shift** orbits and holds a continuous Kensington twist roll. **Ctrl + Shift** pans with ball roll and zooms with twist or the wheel.

Editable Windows source for Chamfer Trackball lives here. GitHub Actions builds and verifies the single-file executables; no development folder is needed on the owner's PC.

## Zoom toward the cursor

Native zoom uses the visible surface under your pointer. In empty space it uses the cursor ray at the current view depth, so both inward and outward zoom keep the point under your pointer fixed on screen. Orbit keeps its existing pivot. This works with the updated Chamfer Studio bridge and the included Blender/Fusion adapters; reinstall those adapters from Integrations after updating. Mouse emulation follows the target application's own zoom behaviour.

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

- Hold **Shift** and roll to orbit. Twist the SlimBlade Pro ball to keep rolling the current view around the cursor. Twist the other way to reverse, or release Shift to stop.
- Hold **Ctrl+Shift** and roll: pan; use the wheel simultaneously for zoom.
- Assign an optional Zoom shortcut in Buttons & keys for zoom by rolling up/down.
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
| Shift | Orbit with ball roll. Twist for continuous view roll, reverse with an opposite twist |
| Ctrl + Shift | Pan; the wheel zooms |
| Alt | Up/down/left/right selects neighboring faces; experimental knob estimation turns orientation in 90° steps |
| Alt + wheel | Turn the aligned face by 90 degrees |
| Optional Twist shortcut | Assign a separate hold for deterministic 90° knob steps if desired |
| Windows | Move through the scene |
| Windows + Alt | Move vertically through the scene |

Windows release does not open Start when reserved by the active program in a 3D view. Other system shortcuts and Windows input outside that context remain normal. All held controls are editable. Snap, Twist and Navigate require a native integration. Alt plus up/down/left/right switches neighboring faces. The 3D default uses the same Alt hold for experimental knob estimation; there is no tap switch or Shift requirement. Twist interprets the selected X/Y component of the existing ball movement, because the measured knob and ordinary rolling patterns overlap. It does not claim independent three-axis sensing.

Pointer speed/effective DPI now changes the identified trackball's delivered pointer counts. Hardware DPI selection only calibrates the estimate. Pointer speed, Orbit/Pan/Zoom/Twist speed, soft stop and glide are saved per program and retained by Add, Duplicate, switching and restart. Pointer gain does not multiply camera speed. Twist has its own speed, Horizontal/Vertical direction and Reverse setting in Speed & direction. The tested M512 uses Horizontal; clockwise is positive raw X. Held Twist stops immediately with the ball and does not coast. Smooth 3D speed has a 1% low end, 100% at the center and a zero stop value. Normal mouse pointer DPI and glide are independent program settings. Soft stop applies to pointing and navigation; clicks and mode changes stop the tail. A live keyboard on Home and in the guide highlights currently held shortcut keys without recording typed text.

Studio uses its Y-up view cube for upright snaps; existing Z-up adapters keep their coordinate convention. While snapping, directional motion advances one neighboring face at a time, with a bounded repeat interval and a consistent stroke axis through Top and Bottom. The experimental Alt-only estimator recognizes highly consistent near-horizontal motion as a possible knob turn, applies 90° orientation steps and shows “Estimated twist” in Home and the guide. Uncertain motion falls back to faces. An ordinary horizontal roll can look identical to a twist; disable the estimation checkbox for directional-only snapping, or assign a separate Twist hold for explicit intent. The standard M512 sensor still supplies only X/Y ball movement. A pure in-place twist with no X/Y report cannot be sensed; the face turn is a gesture inferred from a curved roll path.


### Saved presets

In Buttons & keys, choose **Save current as preset**, give the setup a name, and save. The Speed & direction page has the same Save control. Choose a saved preset and click **Apply** to use it in the active program. Rename or delete a preset from the same card; saving an existing name asks before replacing it.

Presets include buttons, held keys, wheels, combinations, gestures, pointer speed, Orbit/Pan/Zoom/Twist speed and motion feel. They survive app restarts and control resets. Applying one preserves the program name, other programs and selected trackball. Hardware DPI calibration and general app settings stay outside a program preset.

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

While Alt remains held, a later clear knob candidate is reconsidered after directional face movement; a first noisy/vertical stroke does not lock the whole hold to faces. Home and the floating guide explicitly show **Snap faces**, **Reading Alt motion**, or **Estimated twist 90°**. This remains an estimate from X/Y, and a sufficiently similar horizontal roll is ambiguous.


## Twist calibration

In **Buttons & keys → Twist calibration**, record normal up/down/left/right rolls, clockwise knob turns and anticlockwise turns. Each recording lasts five seconds; use only the selected trackball, no keys/buttons or scroll wheels. Lift your fingers to reposition between knob turns. **Test** previews recognition and 90° steps before **Apply to program**. Cancel leaves your saved calibration intact. Save a named preset to reuse the result.

The app detects whether knob turns report a separate scroll signal or learns an experimental X/Y pattern. The preview reports overlapping patterns when normal rolls and twists are too similar; uncertain X/Y motion falls back to face selection. Calibration cannot recover an independently unreported axis. Kensington lists dual optical sensors for its [SlimBlade Pro twist scrolling](https://www.kensington.com/en-ca/p/products/control/trackballs/slimblade-pro-trackball/); this does not change the M512 hardware.

Calibration saves per program and preset, works with Alt Snap or a separate Twist hold, and respects Twist speed and Reverse. Re-record or Clear calibration to change the learned direction. Recordings stay in memory; only the learned model is saved. Native mouse clicks/wheel axes, existing programs, selected device and other controls are preserved.


Calibration only reserves input during recording or the local Test preview. Switching to another app stops that capture, keeps completed samples, and restores navigation. Home and calibration now show actual received ball movement separately from device presence. Temporary UI-pause recovery requires a stable healthy input loop before resuming; native mouse input remains available while recovering.


A slight clockwise or anticlockwise knob nudge now triggers a **complete 90° cube rotation**. You do not need to physically turn the ball through 90°. Pause briefly (about a quarter second), reverse deliberately, or release/rehold the key for another step. Continuing the same stroke emits one step. This applies to Alt's experimental estimate, an optional Twist hold and calibration Test; older saved calibration distances do not force a large physical turn. Twist sensitivity and Reverse remain available.
