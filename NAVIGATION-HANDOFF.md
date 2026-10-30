# Chamfer Trackball navigation handoff

## Status — Codex, 2026-10-29

This is a configuration change and implementation handoff, not a new application build.
The newest release verified in this repository is v1.5.5. The repository currently
contains release automation and release notes; its README says application source is
kept privately. Do not label this work v1.5.6 or v1.5.9 without building and testing
the application from the actual source.

## Requested behavior

- Ordinary pointing, all mouse buttons, and both scroll wheels retain native mouse
  behavior when no navigation mode is held. No wheel inversion or wheel-side swap.
- Hold **Shift** and roll to rotate. Release Shift to return to the mouse.
- Hold **Ctrl+Shift** for simultaneous pan and zoom.
- Hold **Alt** and roll to snap to aligned face, edge, and corner views. Do not
  assign this to the bottom-left mouse button.
- Hold **backtick (`)** for camera navigation, with the Alt navigation behavior
  demonstrated in the reference.
- Never use Ctrl by itself as a navigation trigger.
- Expose editable bindings and save them across restarts and upgrades.
- Get a real 3D pivot under the cursor from the target app so rotation stays
  around that point instead of a distant origin.

## Configuration completed and verified

The existing app's Reset to normal command was applied. Windows mouse-button
swapping was restored to off. Both programs now use normal actions for all
buttons and both wheels; custom button combinations and mode overrides are empty.

The 3D modelling program is selected, with these existing v20 settings:

```json
{
  "orbitKeys": "shift",
  "panKeys": "ctrl+shift",
  "zoomKeys": "none",
  "leftWheel": "normal",
  "rightWheel": "normal",
  "modeButtons": {},
  "combos": []
}
```

Normal mouse has orbitKeys, panKeys and zoomKeys set to "none". The global
shortcutsEverywhere option is false. Settings persisted through a clean quit and
relaunch, and the app's home screen displayed Shift for Orbit and Ctrl+Shift for
Pan. These are configuration/load checks; physical-device navigation in CAD apps
has not been validated.

## Still requires application source and implementation

Ctrl+Shift currently configures **pan only**. The existing UI prevents assigning
the same hold binding separately to pan and zoom. Combined pan/zoom, Alt snapping,
and backtick navigation have not been implemented by this configuration change.

Add explicit modes with most-specific chord priority: Ctrl+Shift must win over
Shift; plain Ctrl must pass through. Release the mode and any injected buttons or
keys on modifier release, focus loss, app switch, device disconnect, or shutdown.
Preserve normal clicks and native wheel behavior outside the held modes.

Cursor pivoting and accurate camera movement require native integration with
each supported 3D application's scene and camera APIs. Query the camera and
ray-picked surface point on mode entry; keep a stable pivot during a gesture.
Use a deliberate selection or camera-target fallback when the cursor misses.
Pan and zoom must use scene depth and projection rather than a fixed guessed
distance. Snap changes the camera, never model geometry.

Do not describe two-axis mouse motion or inferred circles as measured three-axis
ball rotation. The Rotatrix reference uses two optical sensors and a custom
controller; its native integrations supply scene/camera information.
For an ordinary X/Y mouse input path, a third independent input needs a wheel,
another control, or hardware that actually reports it. Declare and test the
available capabilities before promising simultaneous three-axis ball sensing.

## Verification and delivery needed next

Obtain the private Go/UI/build source without publishing credentials or private
source into this public release repository. Implement the modes and native app
adapters there. Test modifier precedence, normal mouse behavior, release/focus
cleanup, settings and combination persistence, restart/upgrade migration, pivot
stability, snapping, pan/zoom, and navigation in each supported application.
Build matching public/admin binaries, advance the actual app version once, and
publish verified artifacts and release notes here. Have Claude read this handoff
before continuing; its private project has not been synchronized by this commit.

References: [Rotatrix hardware](https://rotatrix.com/) and
[OpenAxis integration overview](https://openaxis.rotatrix.com/).


## Codex source rebuild — 2026-10-30

The owner authorized rebuilding the missing editable source in this same repository,
without creating development folders on the PC, and requested only an executable as
the download. Source, tests, a Windows build workflow, the existing interface and native
Blender/Fusion adapters have now been added for v1.5.6. The original handoff above records
the earlier configuration-only state; its source blocker is superseded by this rebuild.

The first CI run passed 31 core regression checks and interface/Python syntax checks;
a Windows compile error is being corrected. No new executable has been released yet.
Native adapters and physical M512 behavior still need testing in the owner's actual
applications. Pan/zoom uses ball X/Y plus a wheel for depth; no physical twist sensor
is claimed. Existing Chamfer3D v1 motion support remains; Alt snap/fly require a newer
host adapter there. Consult the README for precise compatibility and source layout.


## Codex verified executable delivery — 2026-10-30

v1.5.6 was built from commit 475c866b0d1c9f1b8a5f9337ddf1ebddc98a44d0.
The Windows workflow passed all 39 core regression checks, interface JavaScript/Python
syntax checks, single-executable publishing, actual WebView2 startup and settings UI,
held-key controls and saved draft-combination verification, native camera/pivot HTTP
handshake, stale-session cleanup and origin rejection checks. Off-center snapping
preserves the pivot's screen position and radius; speed and gesture preferences now
survive program switches. The internal settings page is served under a dedicated
trusted virtual origin. The Blender orthographic adapter uses absolute scale updates.

The release contains only ChamferTrackball.exe (52,160,080 bytes), as requested.
GitHub's uploaded asset digest matched the build's SHA-256:
4ccdef538250894a62a5f08f83b59262e3c892f7b2bcc9cb2183a9bb2db5f292.
Release: https://github.com/chamfer3dprinting/Chamfer-Trackball/releases/tag/v1.5.6
This is an owner prerelease; no claim of physical M512/CAD host validation is made.
Blender and Fusion integrations are embedded in the executable and install from its
CAD apps page; users must enable the installed integration in the host. Other apps
retain configurable mouse emulation, and the older Chamfer3D v1 link retains motion
support with its documented snap/fly limitation. No development source folders were
created on the owner's PC. Existing mouse settings were preserved during this build.
