# Chamfer Trackball handoff · 2026-10-30

Editable C# application, tests, embedded UI, Blender/Fusion adapters, logo and repeatable Windows builds now live in this repository. The earlier private-source/configuration-only handoff has been superseded. Work in GitHub and cloud builds; the owner requested no PC development folders and executable-only delivery. Do not use Claude for this task.

## Current owner requirements

- Superseding owner request: Admin displays a real increasing **vX.Y.Z** application version. Existing tags/executables are retained; internal build/source metadata is for diagnostics only.
- The first public candidate is **v1.0.1**, stored as a GitHub draft. Publishing is the owner's explicit Admin action. Later source builds choose the next public patch after the previous public release.
- btn1 Middle top-left; btn2 Back top-right; btn3 Left click bottom-left; btn4 Right click bottom-right.
- Left wheel scrolls up/down; the other retains horizontal scrolling. Preserve wheel direction.
- Corrections apply only to the identified trackball. Ordinary mice retain their inputs; never change Windows-wide SwapMouseButtons or wheel settings.
- The original Chamfer cube logo is embedded in the executable, tray, settings UI and floating guide.
- The guide is draggable, topmost without focus activation, shows live program/mode/buttons, and has minimize, restore and close controls.
- Shift = orbit; Ctrl+Shift = pan plus simultaneous wheel zoom; Alt + ball = aligned snapping; backtick + ball = navigation, Alt for height. Never Ctrl alone.
- Held bindings and button combinations, including incomplete drafts, persist through saving and restart.

## Device handling

Buttons & keys → Identify trackball, then roll only its ball. This stores the Raw Input device path privately in the existing AppData settings and enables trackball click/wheel-axis correction. No device ID is guessed from enumeration order, a generic receiver name or the last mouse seen. Re-identify after a different connection path or a fail-open notification.

InputDriver withholds physical button/wheel events with WH_MOUSE_LL and uses WM_INPUT to route each device. Other mice are replayed unchanged. Injected echoes are ignored. DeviceMapping pairs releases with their original mapped downs. MouseGate requires a withheld-event ticket before replay and times out unmatched events to normal input. Absolute pointing devices are preserved during capture as well. This avoids a kernel driver, but real hardware behavior is still an owner verification item.

## Updates and publishing

BuildInfo and MSBuild properties define the edition; Admin commands are rejected in the public build. Admin About & updates follows increasing admin-vX.Y.Z application versions; compatibility aliases allow older clients to migrate. Public updates follow only published, stable public-vX.Y.Z releases and ignore legacy previews/drafts.

The build workflow verifies both editions before storing an Admin prerelease and an unpublished candidate. The candidate version is stamped into the executable and Admin metadata. About checks at startup and every 15 minutes when enabled, and supports manual check/install. Downloads require the GitHub asset SHA-256 digest.

Admin publishing requires the owner's repository Contents read/write token, encrypted locally with Windows DPAPI. Do not read or export stored credentials. No token, connector credential or private signing key is embedded. Release for public copies the verified candidate into a new public release, validates the upload digest, then publishes. Withdraw returns the release to a draft; re-release retains its original binary.

## Navigation capabilities and limits

The standard M512 report supplies X/Y and wheels, not independent physical ball twist. The camera engine supports orbit, pan/zoom, wheel roll, 26 aligned snap directions and navigation. Do not advertise added hardware sensing or universal native CAD integration.

Blender/Fusion adapters exchange real scene camera and cursor/selection pivot data. Other apps have editable mouse-emulation profiles. Native pivot, snap and navigation require the relevant host adapter. Live installed Blender/Fusion behavior and physical M512 operation have not been validated here.

## Verification

The Windows workflow runs 70 core checks, interface and integration syntax validation, separate single-file publishes and real WebView2 smoke tests for both editions. Checks include logo/edition resources, public rejection of Admin commands, saved combinations and a loopback native camera/pivot handshake. It captures settings/guide screenshots in workflow artifacts. Do not put those images into public build logs.

Verified source: 8af36c29100ce74929b2e24d4f5313566119e024. GitHub Actions run [41053380169](https://github.com/chamfer3dprinting/Chamfer-Trackball/actions/runs/41053380169) passed all 70 core checks, syntax checks, both single-file publishes, and both actual WebView2/bridge smoke tests. Admin build 14 asset SHA-256: c348a4ab982946e12a9d1307f76f6c9bf2738a22d6c1ad2822bd3b5216e456b2. Public candidate v1.0.1 build 14 was verified as a draft; no public release was activated. Physical M512 behavior and live CAD hosts remain unverified.

## Ball navigation update · 2026-10-30

Added an optional Ball navigation quick setup: free orbit, Back alternates orbit/pan, and deliberate clockwise/anticlockwise X/Y circles produce zoom. Circle recognition requires a small, almost-closed, consistently winding path; broad arcs, straight motion and figure-eights are rejected. It leaves zoom after straight motion, a pause, focus/program changes or held-key/button overrides. Circle size and existing Zoom sensitivity are editable per program.

Normal mouse is the factory/reset startup program. Reset disables both optional device corrections; identification now preserves the correction choices instead of forcing swaps on. Windows settings are never changed. Existing saved configurations are not silently reset.

Queued UI writes preserve edits made during an earlier save. Custom held navigation keys are deferred only for a matching chord in a ready identified-device CAD session, so they do not type while navigating; an unused tap is replayed. Orbit, pan and zoom remain independently recordable.

Validation added: circle directions and continuous operation, rejection of straight/half-circle/large-circle/figure-eight paths, exit/re-entry and invalid-input handling; custom chords and circle settings across restart/program switches; normal reset and disabled corrections; concurrent-save regression; actual UI control presence. Cloud build and actual UI smoke tests passed; see the verified build below. Physical M512 gesture feel and live CAD hosts still need owner verification. This adds no independent hardware twist measurement or universal native adapter.

Verified Admin build 17: [Windows workflow run](https://github.com/chamfer3dprinting/Chamfer-Trackball/actions/runs/41057812611) passed 94 core regressions, the concurrent-save regression, interface/integration syntax checks, both single-file publishes and both real WebView2 smoke tests. Source: `57fbb9559096cc44f8d977cd5bcb0183e8a3994a`. Admin asset SHA-256: `5746b8aaa57b2136ae9312436d7fa6a1456155ccbcac5c1a4d9f0f46e6c4501f`. Public candidate v1.0.1 build 17 remains a draft. Physical gesture feel and live CAD hosts still require owner verification.

## Semantic versions and Admin release · 2026-10-30

The owner superseded the fixed v1.5.5/build-number display. Admin now uses the assembly's three-part version, selected above all reserved Admin/legacy version tags. About/sidebars hide internal build numbers. Update is one click in the new interface: save current settings, verify the download, replace the app and restart. A compatibility release entry serves older build-number clients with the exact same new executable.

Admin and updater queries paginate the full release history. Candidate selection chooses the latest verified candidate revision for the chosen public version. Reserved/withdrawn public tags cannot be reused for a different binary. The authenticated production release service is exercised against an in-memory GitHub API during executable smoke tests: candidate discovery, byte/digest checks, draft upload, activation, public updater discovery, duplicate protection, withdrawal/re-release and failure handling. No live public release is activated by these tests. Cloud validation passed; see the verified version below.

Verified v1.5.7: [Windows run 41068244612](https://github.com/chamfer3dprinting/Chamfer-Trackball/actions/runs/41068244612) passed 101 core checks, semantic-version/reserved-tag tests, one-click Update and concurrent-save checks, both publishes, both real WebView2 interface tests, and the actual Admin release service's in-memory authenticated API tests. Those cover paginated candidate discovery, verified download/upload, public updater discovery, duplicate prevention, withdrawal/re-release, corrupt assets and permission failures. Source: `09f4117eadd6a6c7108422d20a176eae7c91e829`. Admin SHA-256: `c1deff50d79fc52b001c05ba78f19d07e17babba41a792a3c827b58a1b35c624`. Public v1.0.1 candidate revision 18 remains a draft, SHA-256 `a8e9f8b36053ecf70bcc0e3dbe634d09fd3fcb5c017345d768a048df53f1a9a7`. Live public publishing was not performed.
