# Chamfer Trackball Admin update

- Make the saved pointer speed / effective DPI slider affect actual selected-trackball movement, with immediate delivery and preserved fractional counts.
- Apply program glide to pointing and 3D navigation. Stop tails on clicks, focus/configuration changes and held-mode release. Neutral speed with soft stop off uses native pointing.
- Preserve complete speed, feel, combinations, gestures and held-key choices in new, duplicate, deleted and switched programs. Keep the confirmed native buttons and wheel axes as defaults.
- Use Windows + ball for scene navigation and Windows + Alt for height in the 3D modelling program. Releasing Windows does not open Start there, even without ball movement. Other system shortcuts and normal Windows behavior outside 3D views remain available.
- Add a live physical-keyboard display and held-key highlights in the floating guide.
- Correct Studio snapping to its Y-up cube convention, allow continued rolling from Top to Bottom, and prevent face-boundary flicker. Alt + wheel makes quarter turns without leaving the selected face.
- Keep Shift orbit, Ctrl+Shift pan, Ctrl zoom, Alt snap and ordinary pointing without a held command. Preserve device identification, custom controls and existing programs.

The software DPI value is an estimate based on the hardware DPI selected in settings. The M512's standard X/Y sensor does not independently report physical twist. Native Snap and Navigate need a compatible host integration. Admin prereleases and unpublished public candidates remain separate; this update does not publish a public stable release.

Automated checks cover count scaling, pointer glide, program persistence/copying, keyboard routing, camera face transitions, cursor pivots, and actual WebView controls. Physical device feel still requires owner testing.
