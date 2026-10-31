# Responsive navigation and held commands — 2026-10-30

The owner confirmed native mouse buttons/wheels and free-ball orbit/pan/circle zoom,
then reported lag and failing held pan/zoom, Alt snap and backtick navigation in
Chamfer Studio. Keep both optional native-input swaps OFF and preserve all user programs.

Changes prepared:
- Aggregate sensor packets into display-rate movement frames without dropping deltas.
- Preserve fractional emulated movement and wheel output.
- Allow ball-circle zoom in held Pan + zoom; preserve held Orbit rotation.
- Apply independent zoom sensitivity to simultaneous pan/wheel zoom and align speed with
  the existing interface labels. Use the same logarithmic zoom amount in the preview.
- Let held commands override the pointer pause after clicking.
- Stop background status animation and avoid rewriting unchanged status labels.
- Negotiate native protocol 2 with Studio's camera link. Preserve legacy protocol 1.
- Do not capture navigation while the native host is unfocused or has a modal/text field.
- Studio's updated camera link transfers real scene poses and applies session-scoped
  camera updates for Snap and Navigate. It coalesces absolute camera poses at animation
  frames and leaves geometry, History, Undo and project saves alone.

Validation is required before delivery: core frame/conservation/precision/key-policy
regressions, actual Windows protocol negotiation and UI mutation checks, real Studio
browser camera tests, and the existing release/build checks. Record final versions,
source hashes, deployment and physical confirmation below.
