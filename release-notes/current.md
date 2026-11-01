## Restore navigation outside calibration and recover input after UI pauses

- An open calibration review no longer disables navigation in every 3D app.
- Switching from a calibration recording or Test to another app releases input and keeps completed samples.
- Recover automatically after a brief UI pause once the input loop, device and registration are healthy.
- Show actual ball input received in Home and calibration, including waiting, other-mouse movement, idle and recovery.
- Verify selected sensor delivery through the input driver and controller, alongside native camera and interface checks.
- Preserve existing shortcuts, buttons, wheels, programs, presets and speed values.
- M512 twist remains an experimental estimate; software calibration does not add SlimBlade dual-sensor hardware.

## Calibration startup and reconnect

- Initialize the saved calibration summary and direction controls on startup and program changes.
- Keep the wizard open while the selected trackball reconnects. Roll the ball once to restore recording and test controls.
- Preserve completed samples and the learned result across temporary input interruptions; re-record only an interrupted movement.
- Keep the owner's existing programs, presets, buttons, wheels and speed settings.

## Record, test and save twist calibration

- Add **Buttons & keys → Twist calibration** for normal rolls, clockwise knob turns and anticlockwise turns.
- Preview recognition, pattern overlap and 90° turns before applying the result. Re-record individual samples or cancel without changing the saved calibration.
- Detect a separately reported twist scroll signal when available; otherwise learn an experimental ball-motion estimate. Similar roll/twist paths can still overlap.
- Save calibration per program and with named presets. Keep Twist speed and Reverse effective, with clear guidance for the learned direction.
- Keep Alt alone for directional face changes and estimated orientation turns; reconsider a later knob motion within the same hold.
- Preserve native clicks/wheel axes, saved devices, programs, presets, bindings and other speed settings. Raw calibration recordings stay in memory.
- This Admin update does not publish a public stable release.

## Recognize twist after face movement in the same Alt hold

- Reconsider a later clean knob motion after directional face snapping, without requiring an Alt release, pause or mode switch.
- Keep exact 90° orientation steps and existing up/down/left/right neighboring-face behavior.
- Make Home and the guide clearly say “Snap faces”, “Reading Alt motion”, or “Estimated twist 90°”.
- Preserve saved devices, programs, presets, buttons, wheel mappings and all speed values.
- Alt-only twist remains an experimental X/Y estimate; similar horizontal rolls can still be confused with knob motion.

## Alt-only faces and experimental 90° knob snaps

- Hold Alt for directional face changes and estimated knob turns. No Alt-tap switch or Shift requirement is added.
- Up/down/left/right moves through adjacent cube faces at a bounded repeat rate. Continued strokes keep their world axis through Top and Bottom instead of bouncing.
- Knob turns snap orientation in 90° steps instead of rotating freely. The guide reports estimated twist and progress toward the next step.
- The experimental estimator uses a brief X/Y path observation. Uncertain or cross-axis motion falls back to faces; normal horizontal rolling can still resemble a twist.
- Turn off “Estimate 90° knob turns with Snap” for directional-only control. A separate custom Twist hold remains optional.
- Preserve existing programs, presets, speed, clicks and wheels. Keep Normal mouse controls unchanged.

## Knob-style Twist hold

- Hold Alt+Shift in a supported native 3D view and turn the ball in place like a volume knob to turn its orientation. Release to point normally.
- Alt plus ball up/down/left/right still changes cube faces. Existing Shift orbit, Ctrl+Shift pan, Ctrl zoom and Windows navigation remain unchanged.
- Change the Twist shortcut in Buttons & keys. Set its independent speed, Horizontal/Vertical direction and Reverse option in Speed & direction.
- Show the Twist shortcut and current turn angle in Home and the floating guide. The built-in preview now displays orientation turns.
- Twist stops with the ball. Existing glide remains available for the other modes.
- Save all Twist settings with programs and presets. Existing custom chords are preserved; an occupied Alt+Shift chord is not reassigned.
- This uses the existing X/Y movement with an explicit hold. It does not measure an independent third rotation axis. Mouse clicks and wheel axes remain unchanged.

## Face orientation recognition

- Recognize curved face-turn rolls across small and larger sensor paths instead of rejecting anything wider than 80 raw counts.
- Keep the selected cube face while the gesture develops; clockwise/counterclockwise motion turns its orientation by 90°. Continue circling for more turns, or roll straight to change cube faces.
- Show live “Curve detected” and clockwise/counterclockwise turn feedback in Home, Buttons & keys and the floating guide.
- Keep Alt + wheel as an alternative quarter turn. Preserve all saved programs, bindings, speeds, presets and native mouse controls.
- This uses the shape of X/Y rolling. A pure in-place twist with no X/Y report still cannot be measured by the M512.

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
