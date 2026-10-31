# Chamfer Trackball Admin update

- Make slow 3D movement substantially slower: 1% at the low end, 100% at the center, up to 400%, and 0 to stop the selected motion. The previous minimum was about 18%. Show active pointer/orbit/pan/zoom settings.
- In Alt Snap, roll straight to change cube faces. Curl into a small clockwise/counterclockwise loop to turn the selected face by 90° in the same direction, like the view cube arrows. Straight movement resumes face changes. Wheel turning remains available.
- Keep turn recognition independent of Orbit sensitivity and retain face orientation across release/rehold.
- Verify Normal mouse pointer DPI and glide independently, including program switching. Preserve saved presets, key bindings and native button/wheel defaults.


- Add named saved presets: Save current as preset, Apply, Rename and Delete. Presets include full program controls, pointer/3D speeds and glide, survive restart and control resets, and require explicit replacement of an existing name.
- Keep a renamed program’s default template, and repair every program target in controls and saved presets when deleting a program.

- Start keyboard state cleanly and highlight only physical key edges observed after launch. An inherited Backtick/OS key state cannot disable the held navigation shortcuts. If a key was already held before opening the app, release and press it again.

- Make the saved pointer speed / effective DPI slider affect actual selected-trackball movement, with immediate delivery and preserved fractional counts.
- Apply program glide to pointing and 3D navigation. Stop tails on clicks, focus/configuration changes and held-mode release. Neutral speed with soft stop off uses native pointing.
- Preserve complete speed, feel, combinations, gestures and held-key choices in new, duplicate, deleted and switched programs. Keep the confirmed native buttons and wheel axes as defaults.
- Use Windows + ball for scene navigation and Windows + Alt for height in the 3D modelling program. Releasing Windows does not open Start there, even without ball movement. Other system shortcuts and normal Windows behavior outside 3D views remain available.
- Add a live physical-keyboard display and held-key highlights in the floating guide.
- Correct Studio snapping to its Y-up cube convention, allow continued rolling from Top to Bottom, and prevent face-boundary flicker. Alt + wheel makes quarter turns without leaving the selected face.
- Keep Shift orbit, Ctrl+Shift pan, Ctrl zoom, Alt snap and ordinary pointing without a held command. Preserve device identification, custom controls and existing programs.

The software DPI value is an estimate based on the hardware DPI selected in settings. The M512's standard X/Y sensor does not independently report physical twist. Native Snap and Navigate need a compatible host integration. Admin prereleases and unpublished public candidates remain separate; this update does not publish a public stable release.

Automated checks cover count scaling, pointer glide, program persistence/copying, keyboard routing, camera face transitions, cursor pivots, and actual WebView controls. Physical device feel still requires owner testing.
