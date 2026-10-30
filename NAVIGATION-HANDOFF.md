# Chamfer Trackball handoff · 2026-10-30

Editable C# application, tests, embedded UI, Blender/Fusion adapters, logo and repeatable Windows builds now live in this repository. The earlier private-source/configuration-only handoff has been superseded. Work in GitHub and cloud builds; the owner requested no PC development folders and executable-only delivery. Do not use Claude for this task.

## Current owner requirements

- Admin displays **v1.5.5**, with unique increasing build revisions and source metadata. Do not overwrite the old v1.5.5 tag or any executable.
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

BuildInfo and MSBuild properties define the edition; Admin commands are rejected in the public build. Admin About & updates follows only admin-v1.5.5-build.N. Public updates follow only published, stable public-vX.Y.Z releases and ignore legacy previews/drafts.

The build workflow verifies both editions before storing an Admin prerelease and an unpublished candidate. The candidate version is stamped into the executable and Admin metadata. About checks at startup and every 15 minutes when enabled, and supports manual check/install. Downloads require the GitHub asset SHA-256 digest.

Admin publishing requires the owner's repository Contents read/write token, encrypted locally with Windows DPAPI. Do not read or export stored credentials. No token, connector credential or private signing key is embedded. Release for public copies the verified candidate into a new public release, validates the upload digest, then publishes. Withdraw returns the release to a draft; re-release retains its original binary.

## Navigation capabilities and limits

The standard M512 report supplies X/Y and wheels, not independent physical ball twist. The camera engine supports orbit, pan/zoom, wheel roll, 26 aligned snap directions and navigation. Do not advertise added hardware sensing or universal native CAD integration.

Blender/Fusion adapters exchange real scene camera and cursor/selection pivot data. Other apps have editable mouse-emulation profiles. Native pivot, snap and navigation require the relevant host adapter. Live installed Blender/Fusion behavior and physical M512 operation have not been validated here.

## Verification

The Windows workflow runs 70 core checks, interface and integration syntax validation, separate single-file publishes and real WebView2 smoke tests for both editions. Checks include logo/edition resources, public rejection of Admin commands, saved combinations and a loopback native camera/pivot handshake. It captures settings/guide screenshots in workflow artifacts. Do not put those images into public build logs.

Verified source: 8af36c29100ce74929b2e24d4f5313566119e024. GitHub Actions run [41053380169](https://github.com/chamfer3dprinting/Chamfer-Trackball/actions/runs/41053380169) passed all 70 core checks, syntax checks, both single-file publishes, and both actual WebView2/bridge smoke tests. Admin build 14 asset SHA-256: c348a4ab982946e12a9d1307f76f6c9bf2738a22d6c1ad2822bd3b5216e456b2. Public candidate v1.0.1 build 14 was verified as a draft; no public release was activated. Physical M512 behavior and live CAD hosts remain unverified.
