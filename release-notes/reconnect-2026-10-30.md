# Trackball reconnection — 2026-10-30

The owner reported reversed buttons and wheel axes after restarting v1.5.9.
The correction preferences remained enabled, and Windows still enumerated the exact
saved trackball. The app reported identification required because its connected state
started false and waited for a new matching Raw Input packet.

Changes:
- Detect the exact saved device at startup and periodically, with no device guessing.
- Check ownership of the process's mouse Raw Input registration and restore the dedicated
  input window if another host component replaces it.
- Resume after transient input interruptions and device reinsertion, clearing held navigation
  state before recovery.
- Preserve all user control settings and per-device correction preferences.
- Add real Windows registration-replacement and startup/reconnect checks to both editions'
  existing executable verification.

Validation: cloud core and interface checks plus actual Windows recovery tests must pass
before delivering the new executable. Physical M512 clicks/wheels and Chamfer Studio
motion remain for the owner to confirm. No Windows-wide mouse preferences are changed.

Verified delivery:
- Admin v1.5.10 was built from source 19fd6a53968958dfdf2d25194911f586df18b7c6.
- Workflow run 41146239617 passed 129 core checks, interface/release policies and both
  actual Windows interfaces. The recovery fixture verified startup presence, exact saved
  identity, unplug/reinsert recovery, corrections with 3D navigation disabled, and replacement
  of the process's raw mouse registration.
- The first recovery fixture caught a registration flag mismatch before release. Windows
  reports INPUTSINK without DEVNOTIFY in GetRegisteredRawInputDevices; the corrected ownership
  check was verified using the actual Win32 API and the repeated executable tests.
- Button and wheel corrections are independent of the 3D-navigation enable switch. Custom
  shortcuts/navigation are still disabled when the switch is off.
- Verified executable SHA-256:
  3c0b1726d6bb2fdb5a1d407908034d7378b365f30240659b5cb6781c6351e77d.
- Installed v1.5.10 with a rollback executable retained. The complete settings file matched
  exactly before and after the update. The app immediately displayed Trackball identified
  after restart. Physical button/wheel testing was requested and remains pending.

Evidence: https://github.com/chamfer3dprinting/Chamfer-Trackball/actions/runs/41146239617

Manufacturer-default correction:
- The owner subsequently reported that buttons/wheels were still wrong and supplied
  the manufacturer's diagram: btn1 Middle, btn2 Back, btn3 Left, btn4 Right.
- The desktop capture confirmed that an optional software swap was converting native
  right-button packets into injected left-button packets. It did not establish which
  physical button produced those packets, and contained no wheel test; do not treat it
  as a physical btn3 or wheel calibration.
- Disabled both optional swaps to restore native clicks and wheel axes. This matches
  the existing GitHub factory defaults; no executable change was needed.
- Verified the saved flags are false and that all other settings matched exactly,
  including the owner's existing combination and active Normal mouse program.
- Physical confirmation of the restored defaults remains pending. Do not automatically
  re-enable correction flags in a later update.
