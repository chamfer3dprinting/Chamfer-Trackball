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
