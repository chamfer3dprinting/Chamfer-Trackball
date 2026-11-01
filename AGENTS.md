# Chamfer Trackball working agreement

The owner authorized rebuilding the editable source in this existing repository.
Keep source, tests and version history on GitHub. Do not create development folders
on the owner's PC. Deliver only the executable as the user-facing download.

Preserve standard mouse clicks and wheels by default. Never change Windows-wide
mouse button swapping to implement app bindings. The owner's latest requested controls
are Shift orbit, Ctrl+Shift pan, Ctrl plus ball for zoom, Alt plus ball for six-face
snapping, Alt plus the physical LEFT scroll wheel for 90-degree face-orientation snaps, an optional separate Twist hold, and Windows navigation with Alt for height. Automatic ball-only twist estimation is now off in the 3D default. Reserve the Windows tap in the 3D
context so releasing it never opens Start; keep system shortcuts and non-3D Windows input normal. Ctrl alone is permitted for
Zoom in 3D apps; other Ctrl keyboard shortcuts must remain normal. The 3D program
points normally until a held navigation command is used.

Keep existing release history. Advance the real application version for a delivered
update; do not reuse a release tag for a different executable. Build and test before
publishing. Treat a prerelease as distinct from a public stable release.

Do not claim that the M512's ordinary X/Y input measures independent three-axis ball
rotation. Native cursor pivots, snap and camera navigation require host camera/scene
APIs. Label native integration and mouse emulation accurately.

Before editing, read the current main branch. Preserve other agents' changes and
do not force-push. Add a dated note with changes, validation, limitations and next work.
Keep credentials and local user settings out of source. Use the GitHub connector for
repository updates; keep all source changes reviewable.

Manufacturer defaults confirmed by the owner's M512 diagram:
btn1 = Middle, btn2 = Back, btn3 = Left click, btn4 = Right click.
Preserve native wheel axes. The optional trackballSwapButtons and
trackballSwapWheels corrections default to false. Do not enable them based on
button numbers or the UI's descriptive labels; enable a correction only when
a physical test establishes that it is needed. Retain the owner's confirmed
choice across updates and preserve custom programs/combinations when restoring
native mouse inputs.

# 2026-10-31 — Reduce Admin lag when maximized

The owner reports the Admin app lags and becomes difficult to use, especially when maximized. The Home page was calling the WebView-to-C# status bridge on every animation frame, potentially 60–144 calls per second. Cap active Home/Controls status updates at 20 Hz, other visible pages at 4 Hz, and hidden pages at 1 Hz. Keep the native trackball controller on its 16 ms input timer. Add interface regression coverage for the 20 Hz cap and unchanged sensor interval.

Validation is included in the existing GitHub Actions build workflow. A physical maximized-window performance check remains for the owner after the new Admin build is ready.


# 2026-10-31 — Verify Trackball Admin responsiveness build

GitHub Actions run 41186358288 passed the core regressions, interface/syntax checks, Admin and Public executable builds, and actual WebView interface/bridge smoke checks. The Admin-only v1.5.13 executable is available at the Admin prerelease. Its release contains only ChamferTrackball-Admin.exe. The public Trackball candidate remains an unpublished draft; no public release was activated.

The updated UI caps active status refresh at 20 Hz and leaves the 16 ms device-input loop intact. The automated interface check confirms that the UI no longer polls on every animation frame. A physical maximized-window performance test remains for the owner. Next: update Admin to v1.5.13 and confirm the maximized window remains responsive.


# 2026-10-31 — Enable and smooth ball-only 3D navigation

Enable orbit, the M512 Back-button orbit/pan toggle, and circle zoom in the dedicated 3D modelling program by default. Upgrade existing v22 settings once without changing the separate Normal mouse program, other programs, or user key bindings. Implement the saved short soft-stop motion feel and coalesce obsolete absolute camera poses per native session so a delayed client applies the newest view instead of replaying stale frames. Make the Home and Circle zoom instructions describe the setup and gesture directly.

GitHub Actions run 41187617347 passed the core regression checks, interface and integration syntax checks, Admin and Public builds, and actual WebView interface/bridge smoke checks. The first run, 41187467690, exposed an outdated reset assertion that expected every program's buttons to be normal; the existing assertion was aligned with the separate 3D Back toggle and the rerun passed. The build produced Admin prerelease v1.5.14; the public build is still an unpublished draft, and no public stable release was activated. The physical M512 still needs owner confirmation for zoom feel and responsiveness in Chamfer Studio. Standard M512 reports do not provide independent physical twist. Next: install Admin v1.5.14, select 3D modelling, test orbit/pan/circle zoom, and adjust glide or circle size from the UI if needed.


# 2026-10-31 — Remove idle work that can make Trackball feel stuck

The owner reports the Admin app still feels laggy and can stick. Replace the persistent native WebSocket sender's 8 ms polling loop with a semaphore wake-up when a message is queued and a 1 s idle heartbeat. Reduce foreground/profile checks to 20 Hz while no navigation mode is active; retain 16 ms updates while navigating. Cache the guide's visibility setting by configuration revision instead of deep-copying settings every 100 ms.

This addresses measurable background work in the app and gives native clients lower-latency wake-up. GitHub Actions run 41188280272 passed the core regression checks, interface and integration syntax checks, Admin and Public builds, and actual WebView interface/bridge smoke checks. It produced Admin prerelease v1.5.15; no public stable release was activated and public v1.0.1 remains latest. The owner still needs to confirm the maximized-window responsiveness and Chamfer Studio camera feel on the physical PC; no live hardware/frame-rate benchmark was available.


# 2026-10-31 — Reduce circle-zoom CPU work during ball movement

The circle recognizer previously rechecked up to 96 possible path suffixes and recomputed turning angles in nested loops for each new motion sample while orbiting. Cache each turn when the sample arrives and use prefix sums plus a single bounds pass to evaluate circle candidates in linear time. Keep the existing gesture thresholds and outputs unchanged.

GitHub Actions run 41189725100 passed the core regression checks, interface and integration syntax checks, Admin and Public executable builds, and actual WebView interface/bridge smoke checks. It produced Admin prerelease v1.5.16 with only ChamferTrackball-Admin.exe; no public stable release was activated. Automated checks passed, but this does not measure live M512 performance or prove that the reported whole-PC freeze is resolved. Next: the owner can update through About & updates and test the trackball in a 3D app; if the PC still stalls, collect the active mode and whether keyboard input continues.

# 2026-10-31 — Resume 3D ball navigation after a click

The owner reports that the selected 3D modelling program is detected but ball navigation and circle zoom remain inactive after clicking in the viewport. The normal click safely returns control to the pointer, but ball-only mode had no path to resume on subsequent M512 movement. Re-arm the configured mode on the next selected-trackball movement when the active app is a CAD profile and no held command overrides it. Preserve normal click delivery and held-key behavior.

Validation: PR workflow run 41209423686 passed the regression suite, syntax checks, separate Admin/Public builds, and WebView2 smoke checks. Main workflow run 41209648153 also passed and published Admin prerelease v1.5.17. Public stable v1.0.1 remains unchanged. Limitation: live M512 input and Chamfer Studio camera behavior still need physical confirmation. Next: update through About & updates and confirm orbit, Back-button pan switching, and circle zoom.

# 2026-10-31 — Keep global input responsive and recognize real ball circles

The owner confirms that circle motion stays in Orbit and reports another freeze while using the app. Sample circle recognition once per 16 ms navigation frame, with a separate delivery buffer so a delayed camera handshake cannot classify the same movement twice. Accept small, slightly uneven loops and brief pauses, apply the first recognized winding to zoom, and bound sampling work and coordinate growth during long rolls.

Move global mouse/keyboard hooks to a dedicated message thread. Hook callbacks queue key replay, controller notifications and recovery to the app instead of injecting input or running navigation inline. Synchronize shared key/mouse state; let physical input through when the UI heartbeat is stale. Add Windows smoke coverage that blocks the settings thread while checking the hook pump and verifies deferred taps, used holds and ordered key releases without sending test keystrokes.

Focused validation before the full workflow: actual C# gesture checks passed for both circle directions, small and uneven first loops, quantized 1000 Hz input batched at 16 ms, long straight motion, figure-eight/large-curve rejection and recovery to normal rolling. The input driver, dedicated hook pump and keyboard dispatch checks compiled in memory without a local source checkout. PR workflow run 41233810130 and main workflow run 41234077756 passed 153 core regression checks, syntax checks, both executable builds and actual WebView smoke checks. Main published Admin prerelease v1.5.18. The reported physical-device freeze and Chamfer Studio zoom still require confirmation after installation; automated checks do not prove either live issue resolved. The v1.5.18 PC-control installation step was interrupted before completion. Next: install the current Admin build, restore the device selection and confirm circle zoom and responsiveness on the M512.

# 2026-10-31 — Preserve the selected trackball when settings are saved

The owner reports continued failure. The saved configuration has 3D ball navigation enabled with no selected input device. A stale WebView settings object can erase a newly identified trackball because the general Save method replaces the whole configuration. Add a dedicated IdentifyDevice write for Raw Input; ordinary control saves preserve the current persisted identity. Keep the user's programs, combinations, native clicks/wheel axes and chosen key bindings.

Add identification and cancellation to Home, expose device readiness/listening state, show an app setup warning when CAD is detected without a ready trackball, and extend identification to 60 seconds. Cover the stale-empty-save and old-device-save races, program changes, restart, reset and invalid identity in the core suite. The actual WebView smoke check also saves an older UI object after a device is identified and confirms that the ID survives; interface checks exercise the Home buttons.

PR workflow 41241163778 and main workflow 41241400653 passed 158 core checks, interface/integration syntax, both builds and actual WebView smoke checks. Admin v1.5.19 was installed to the existing Downloads folder, and a desktop shortcut was added. The owner rolled the M512 and the app identified it; identification survived a settings write and an app restart. A temporary Z zoom binding was applied and verified in Home. Computer Use then stopped the Studio browser test because it could not confidently verify the Chrome URL. Physical camera feel remained unverified.

# 2026-10-31 — Default to held controls, Ctrl zoom and six-face snap

The owner explicitly replaces the temporary Z zoom binding with Ctrl and confirms Alt for snapping. They request ordinary pointing when no navigation key is held and snaps restricted to Top, Bottom, Left, Right, Front and Back. This request supersedes the earlier blanket Ctrl-alone restriction.

Set factory 3D ball behavior to pointer and Zoom to Ctrl. Migrate the old 3D default from automatic orbit to pointer while keeping existing custom chords; configure this owner's active program to Ctrl Zoom and Alt Snap. Retain the optional explicit Ball navigation preset and native button/wheel mappings. Permit Ctrl alone only for Zoom in the resolver, validator and shortcut recorder; exact modifiers keep Ctrl+Shift Pan separate and typing shortcuts normal.

Restrict native snap candidates to the six signed world axes, preserving camera radius and cursor pivot. Update interface and control documentation. Focused actual C# camera checks pass for all six faces, diagonal starting poses, perspective/orthographic views and the existing off-center pivot behavior. PR workflow 41244064016 and main workflow 41244421172 passed 175 core regressions, syntax checks, both executable builds and both actual WebView interface checks. Admin v1.5.20 was downloaded with GitHub SHA-256 cdbdc5c5ab7671a745e77de7545f5191b639bb4ca58afba61feb45eddaec0321 and installed into the existing Downloads executable used by the desktop shortcut. The owner's active 3D program was set to pointer, Ctrl Zoom and Alt Snap, preserving Shift Orbit, Ctrl+Shift Pan, device identity and native click/wheel corrections off. After restart, Home reports v1.5.20 Admin, Trackball identified, Mouse with no held key, and the requested held bindings. Physical camera behavior in Studio was not retested in this turn; the six-face camera behavior is covered by the passing focused and full regression checks.

# 2026-10-31 — Apply program motion settings and show physical keyboard holds

The owner confirms the current native click/wheel layout and held controls as the defaults. Replace the old 3D backtick navigation default with Windows + ball, including Alt height. Consume an unused or used Windows hold in the 3D context so release cannot open Start, normalize both Windows keys, and retain Windows+D and ordinary Windows input elsewhere. Keep custom chords and the selected trackball across migration to schema 25.

Connect the per-program pointer speed / effective DPI slider to selected-device Raw Input, with immediate fractional movement delivery and a separate soft-stop tail. Apply saved glide to pointer and 3D movement, stop it at clicks/mode/focus/configuration changes, preserve the hook heartbeat fail-open, and bypass software routing at neutral speed with glide off. Hardware DPI selection calibrates an estimate rather than changing the physical sensor. Keep Orbit/Pan/Zoom gains independent of pointer gain. Added, copied, deleted and reset programs transfer the complete fields and motion feel instead of partial button/key subsets.

Studio's native view cube is Y-up. Use that host convention (and optional scene worldUp metadata), retain Z-up for other existing adapters, and track a continuous virtual orbit with face-selection hysteresis for six-face snap. Continued rolling can pass from Top through an adjacent face to Bottom. Snap does not coast. While snapping, the wheel makes quarter turns of the aligned face. This is software wheel input, not a claim of physical twist sensing. Add a live Home keyboard and held shortcut indicators to the floating guide; key states are transient and are not logged as text.

Validation pending the PR and main Windows workflows. New regressions cover actual pointer count scaling/subpixels/tails, per-program persistence, Windows suppression and system-shortcut routing without emitting test Windows keystrokes, Y-up faces/opposite-face rolling/quarter turns/pivot invariance, complete program UI copies and keyboard highlighting. Physical M512 feel remains for owner confirmation after installation.

# 2026-10-31 — Verify motion/Windows update and start physical keys cleanly

PR workflow 41246841371 and main workflow 41247111879 passed 242 core checks, interface/integration checks, both builds and both actual WebView/native-protocol smoke tests. Admin v1.5.21 (source eae9a690ce4dbeb65bcbfce93e22620067d8fd94) was installed into the existing Downloads executable, SHA-256 90fc4b824cbd3d7d38957fa17bcf20fa245bd109e1cee83b807f2037f027d54b. Home showed the identified trackball, pointer mode without a held command, Windows navigation and the new keyboard. The owner's saved speed sliders remain at 1 and glide 5. Native clicks/wheel corrections remain off. Public stable remains public-v1.0.1; the public candidate remains unpublished.

The actual keyboard display shows Backtick continuously held after launch. The constructor seeds that ordinary key (and modifiers) from aggregate Windows GetAsyncKeyState, which also contains injected/old OS state and cannot establish a physical hold. Start empty and accept only observed physical hook edges. This prevents an inherited nonmodifier from rejecting every exact navigation chord. Holding a key before startup requires releasing and pressing it again. Add a regression guard against restoring aggregate startup seeds. Physical M512 navigation/feel and Windows release remain for owner confirmation; automated Windows routing checks do not emit system keystrokes.

The owner also requests named presets that can be added and saved. Add a persistent preset library for the full active program (buttons, keys, wheels, gestures, pointer/3D speed and motion feel) with Save, explicit Replace, Apply, Rename and Delete. Keep library writes separate from general settings saves so stale pages cannot erase it; retain presets across control resets. Applying a preset keeps the active program's name, other programs and device identity. Bound names and library size. Program deletion repairs references in the preset library as well as live buttons, mode buttons, combinations, gestures and wheels. Renaming retains the reset template. UI and actual bridge smoke checks exercise the preset controls; core checks cover restart, replacement, isolation, reset, stale saves and referenced-program deletion.

# 2026-10-31 — Verify and install saved presets in Admin v1.5.22

PR workflow 41248635458 and main workflow 41248971194 passed 262 core regressions, interface/integration checks, both executable builds and both actual WebView smoke checks, including preset Save/Apply/Rename/Delete. Source d1cd17633a6c3565a441c80802d58578b9de79bf produced Admin prerelease v1.5.22. The downloaded Admin executable matched GitHub SHA-256 df8a9d8eb37f33d8afb69a3e7884829a95d9a8d2cc600560eb3097a20d34730a and replaced the existing Downloads executable used by the desktop shortcut. Public stable remains public-v1.0.1; the public candidate is unpublished.

On the owner's PC, v1.5.22 opens with the trackball identified, ordinary pointing without a held command, and Shift Orbit / Ctrl+Shift Pan / Ctrl Zoom / Alt Snap / Windows Navigate. The inherited Backtick highlight is absent. Save current as preset was used through the actual app interface for both current program baselines. Both saved entries persisted on disk and after a full app restart. Applying the matching 3D preset preserved program names, the identified trackball, native correction flags off and the separate Normal mouse combination. Admin is left open at the Saved presets card with 3D modelling active.

The earlier v1.5.21 physical Backtick tap did not clear the inherited startup highlight, which led to removing aggregate OS startup seeds. New physical keyboard hold/release, Windows Start suppression, live Chamfer Studio navigation and perceived M512 smoothness have not been physically confirmed after v1.5.22. Those behaviors have automated routing/camera coverage; this is not a claim of a live hardware performance test. Next: owner confirmation in the Studio viewport, then adjust only the relevant program's speed/glide if requested.

# 2026-10-31 — Widen smooth 3D speed and turn snapped faces from the ball

The owner confirms the general setup and presets, but says the 3D speed controls stay too fast and asks for a ball turn gesture matching the cube's clockwise/counterclockwise rotate arrows. Their saved low slider values do reach the active program. The previous curve's minimum is about 18% of default, which is insufficient for fine navigation. Extend smooth Orbit/Pan/Zoom/scene motion to a 1% minimum and a zero stop value, retain 100% at the center, and use percentage labels. Keep the previous directional face-switch stroke response separate from the smooth-speed curve. Show active backend gains and exercise actual UI saves into those gains, including Normal mouse pointer DPI/glide across program switches.

While Alt snapping, reserve a consistently curved small X/Y path for face orientation. A deliberate curved loop turns the selected face by one quarter turn in the gesture's visual direction; continued circles keep turning, and straight rolling resumes directional face changes. Use raw X/Y for recognition before sensitivity, freeze the face during the turn candidate, retain wheel quarter turns and preserve deliberate orientation when Alt is reheld. This is an inferred roll-path gesture: a physically pure twist that produces no X/Y report cannot be detected by software.

Validation pending the Windows workflow. Add camera-output checks at zero/1%/default speed, monotonic slider checks, clockwise/counterclockwise direction and face preservation across all six faces, curved-gesture/straight recovery, pivot radius, speed-independent recognition, existing zoom-gesture regressions, and actual Normal mouse/3D settings bridge tests. Preserve device identity, native clicks/wheels, saved presets and the owner program choices. Live hardware feel and turn recognition still need owner confirmation after installation.

# 2026-10-31 — Verify and install fine speed / ball face turns in Admin v1.5.23

PR workflow 41251577294 and main workflow 41251743703 passed 302 core regressions, interface checks, both executable builds and both actual WebView smoke tests. Those exercise Normal mouse pointer DPI/glide and new 1%/zero 3D gains reaching the controller across program switches, while preserving the saved combination in its owning program. Additional in-memory C# checks passed 36 quantized 1000 Hz sensor paths grouped into 16 ms frames, across clockwise/counterclockwise curves, several radii and gesture speeds. The earlier high-gain face change exposed by the new tests was corrected by reserving a curved turn candidate earlier; straight reversals and sharp corners remain directional strokes.

Admin v1.5.23, source e0bb793c90379a1d36d3fd463bcba4c9732842f5, was downloaded with GitHub SHA-256 8d9daa126524ff7387dd3e3e8f30b8f3c6b575e4c6069840eac8441ca9b9b267 and installed over the existing Downloads executable used by the desktop shortcut. After restart, Home reports Trackball identified and ordinary pointing with no held command. The actual Normal mouse DPI control was changed to Default, its applied pointer gain changed to 100%, and its prior value was restored. The 3D program stayed independent. Both saved presets, custom speed values, native corrections off and Shift/Ctrl+Shift/Ctrl/Alt/Windows bindings were preserved. The active 3D backend gains agree with the percentage labels. Admin is left open at Speed & direction with 3D modelling active.

Public stable remains public-v1.0.1; no public stable promotion occurred. Physical M512 speed feel and the new curved-roll face turn in Chamfer Studio still need owner confirmation. A pure in-place twist with no X/Y motion remains unobservable; instructions describe a small curved roll rather than claiming a third sensor axis. Next: owner test of slow navigation and Alt curved-roll clockwise/counterclockwise face orientation, then tune the gesture only from that feedback.


# 2026-10-31 — Recognize natural-size face orientation rolls

The owner reports that face orientation does not work after Admin v1.5.23. The turn recognizer had a fixed 80-raw-count diameter, rejecting natural larger rolls; increasing only that number would exceed the bounded resampled history. Add parallel 80/320/1280/5120-count size bands with scaled resampling and one gesture owner, so overlapping bands cannot turn twice. Keep ordinary Circle zoom thresholds unchanged. Retain a bounded four-frame preceding face history when curvature starts, and leave turn mode promptly on a straight stroke. Preserve all owner settings and native buttons/wheels.

Expose thread-safe live curve/turn feedback through the native client gate to Home, Buttons & keys and the floating guide. Add six-face quantized 1000 Hz input tests batched at 16 ms, larger radii, slow/fast turns, both directions, overlapping-band turn counts, straight recovery, elliptical rolls and no-input behavior. Extend the actual Studio WebSocket smoke test to check a larger curved roll, its serialized camera Up vector and clockwise feedback. A pure in-place physical twist with no X/Y input remains unobservable; guide text describes curved rolling and retains Alt + wheel as an alternative.

Focused in-memory C# checks passed 74 aggregate checks covering 336 six-face quantized paths and the additional continuation/recovery cases. The first PR workflow exposed a sharp-corner reservation regression: large-band resampling rounds a corner, so reject sufficiently resolved sharp raw-heading changes before resampling. Tiny quantized vectors retain the existing tolerant path recognition. Full PR/main Windows workflow validation and installation are pending. Physical M512 gesture recognition remains for owner confirmation after installation.


# 2026-10-31 — Verify and install face orientation repair in Admin v1.5.24

PR workflow 41253854045 and main workflow 41254110164 passed all 376 core regression checks, interface/integration syntax, separate executable builds and both actual WebView/native-protocol smoke checks. The new Studio socket test checks a larger clockwise roll, a 90-degree camera Up change with the same selected face and matching turn feedback. A focused reproduction of v1.5.23 with a radius-180 roll reserved curvature but produced zero quarter turns, matching the owner's description that the face stays still; the new tests accept that roll. The expanded detector/camera processing averaged 0.0415 ms over 12,000 simulated frames on the owner PC. This timing is not a live hardware/UI smoothness benchmark.

Source 9f50854c7ef41ee40802df0fc1b6ff924a95eb6c produced Admin prerelease v1.5.24. The downloaded executable matched GitHub SHA-256 21eb1f7c9550a3c06afcc6f50297febee6865628f0bf8eabb5161c2f7a4ef719 and replaced the existing Downloads executable used by the desktop shortcut. The preceding executable is kept as .previous. After restarting, the actual Home page shows v1.5.24 Admin, Trackball identified, pointer mode with no held command, the saved Shift/Ctrl+Shift/Ctrl/Alt/Windows shortcuts and the new face-orientation hint. Program settings, both saved presets and selected-device hashes match before/after installation exactly; both native correction flags remain false. No source folder was created on the PC.

Public stable remains public-v1.0.1; no public stable promotion occurred. The owner has been asked to try Alt plus one continuous clockwise roll in the Studio viewport and report the floating guide's feedback. Physical M512 recognition still needs that confirmation. A pure in-place twist without an X/Y report remains unobservable; curved rolling or Alt plus the wheel provides the orientation input. Next: inspect the reported Curve detected/Clockwise feedback if a physical turn still fails.


# 2026-10-31 — Owner confirms orientation works and clarifies knob-style twist

After installation, the owner says the feature works and clarifies that “twist” means turning the ball in place like a volume knob. This differs from recognizing a curved X/Y roll path. Read-only HID capability inspection of the selected mouse collection reports an 8-byte report ID 1 with Generic Desktop X and Y (16 bits each), Wheel (8 bits), and Consumer AC Pan (8 bits). It advertises no independent Rx/Ry/Rz ball-twist axis. Matching receiver collections include an opaque vendor-page 0xFF01 input, report ID 2 with seven payload bytes; no rotation semantics for it are established. This inspection does not prove anything about undocumented firmware or internal sensors.

No input reports were captured and no device output reports or settings changes were sent. The metadata query succeeded outside the restricted shell after the restricted attempt received access denied. Keep the confirmed working v1.5.24 curved-roll controls and all owner defaults. Do not promise reliable knob-style independent twist from ordinary X/Y input. Supporting that exact motion requires a verified separate hardware/firmware signal or a deliberate additional mode selection.


# 2026-10-31 — Use the measured M512 knob motion with an explicit Twist hold

The owner asks to detect the actual ball motion used to turn a volume knob. A temporary read-only Raw Input listener received the selected M512's physical clockwise, anticlockwise and normal left/right passes. Those passes all project onto nearly the same horizontal sensor axis, so the motion is observable but does not uniquely identify intent. The owner explicitly chooses “Add the Twist hold” and reiterates that Alt up/down/left/right must keep switching faces. The earlier categorical statement that knob motion always requires another sensor was too broad: separate intent makes the existing signal useful.

Add customizable twistKeys, default Alt+Shift in the 3D program and off in Normal mouse. Reuse native Roll to turn view orientation without changing the view direction. Keep Alt Snap and every other existing key/button/wheel mapping unchanged. Twist has independent speed, axis and reverse controls, positive raw X mapping to a clockwise model turn for the tested M512, and no glide. Latch the held intent through the final pending frame on release and restart the camera session if a latched Roll mode changes into/out of the new held interpretation. Show the current turn angle, extend the guide for six shortcut rows and rotate the preview.

Migrate to schema 26 without taking an already assigned Alt+Shift chord; preserve old custom keys, speeds, hardware identity and saved presets. New presets include Twist fields. Applying an older feel snapshot merges its fields so it cannot erase newer Twist tuning. No source folder, device firmware write, input blocking or keyboard capture was used by the temporary listener; the listener has stopped and removed its own per-process registration.

Focused in-memory C# checks pass 41 cases for exact held-mode resolution, both directions on all six faces, unchanged view direction/pivot, zero/fine/large movement, persistence, old preset compatibility, migration conflicts and all four unchanged Alt face-switch directions. Interface syntax and new feedback/preview helper checks pass. Full PR/main Windows workflows and installation are pending. Physical held-Twist feel remains for owner confirmation after the new Admin build.


# 2026-10-31 — Verify and install held knob Twist in Admin v1.5.25

PR workflow 41256846510 and main workflow 41257113936 passed all 417 core regression checks, interface/integration syntax, separate Admin/Public executable builds and both actual WebView/native-protocol smoke checks. These cover the new held-mode resolver, both knob directions on all six faces without changing camera direction or pivot radius, the four preserved Alt directional strokes, separate speed/axis/reverse settings, schema/preset compatibility, actual Studio socket orientation delivery and real settings-to-controller gains. The first PR run only failed to compile an added test local named clockwise because an earlier top-level test uses that name; it was renamed before the passing workflows.

Source 5b962afe938faf56167316fe1f352de406d7cf55 produced Admin prerelease v1.5.25. The downloaded executable matched GitHub SHA-256 af2c132323596ce739147ed1e1d8f321c9dc93340a6c71d9d1e41d77a6139433 and replaced the existing Downloads executable used by the desktop shortcut. The preceding executable is retained as .previous. The actual installed Home page shows v1.5.25 Admin, Trackball identified, Mouse with no held key and Twist assigned to Alt+Shift alongside the unchanged Shift/Ctrl+Shift/Ctrl/Alt/Windows controls.

Through the actual Speed & direction UI, Twist was changed from 50 to 49, the backend gain display changed from 100% to 91%, then the value was restored to 50. The saved file now contains schema 26, twistKeys alt+shift, twistSens 50, twistAxis x and invertTwist false. Removing only the added Twist fields yields an identical hash of all existing program settings before/after installation. Saved presets and selected-device hashes also match exactly; native click/wheel corrections remain false. The owner's current pointer/orbit/pan/zoom settings are 5/10/10/11 and remain unchanged. No source/development folder was created on the PC.

The receiver's measured knob motion and ordinary left/right strokes overlap on X; the explicit Twist hold supplies the intended interpretation. X is the measured useful axis; the initial clockwise direction convention remains an owner-calibratable default through Reverse twist. Do not present this as independent three-axis sensing. Public stable remains public-v1.0.1 and no stable public promotion occurred. The owner has been asked to physically test Alt+Shift knob turning and the preserved Alt directional snaps. Those physical held-control results are pending; automated camera/input coverage is not a live hand-feel test.


# 2026-10-31 — Snap knob orientation in 90° steps and estimate it under Alt

The owner confirms v1.5.25 Twist responds but asks for discrete 90-degree orientation, reports buggy Alt directional snapping and explicitly rejects any Alt-tap mode switch or Shift requirement. After explaining that the M512 X/Y signal cannot independently identify all six camera degrees of freedom, the owner chooses “Try Alt-only estimation.” No Alt-tap keyboard behavior is added.

Replace the old curved-roll reservation inside directional FaceSnap with stable dominant-axis strokes, one adjacent face per step and a 140 ms repeat limit. Retain the stroke's world rotation axis across upright faces; an initial focused test caught Top/side bouncing when that axis was recomputed after every canonical snap. Preserve continuous Top-to-Bottom travel, wheel quarter turns and cursor pivots. Quantize the optional explicit Twist hold into 90-degree increments and report progress. A snapped Twist session initially aligns the nearest cube face.

Add a bounded, experimental X/Y geometry estimator for native Alt Snap sessions. Consistent, nearly horizontal strokes may become estimated knob turns; uncertain or cross-axis strokes use directional faces. The brief observation window is resolved by existing 16 ms controller frames, including idle frames while a decision is pending. A candidate that becomes uncertain before a turn replays its bounded pending movement as directional faces. No sensor-axis or six-DOF claim is made. The owner approved the possibility of confusing horizontal rolls with twists.

Separate clockwise, anticlockwise and normal-rolling samples were captured read-only from the selected M512, with no keyboard capture, input blocking or device writes. The listener has stopped. Private traces stay in memory, not source. The actual C# replay recognized the clockwise sample and clearer anticlockwise passes while all five selected normal comparison strokes remained faces; shorter/uncertain knob strokes conservatively fell back or did not reach a 90-degree threshold. This is a small calibration check, not a general accuracy guarantee.

Schema 27 enables the approved estimate in the existing Alt-based 3D program and removes only the preceding factory Alt+Shift default; custom separate Twist chords remain. Normal mouse has estimation off. The checkbox, program copies, presets and reset paths include the option. Focused in-memory C# checks pass 75 cases for adjacent faces, repeat limits, pole continuity, exact quarter turns, estimator fallback and accepted input ambiguity. Full Windows workflows and installation are pending. Physical Alt-only feel still needs owner confirmation after installation.


# 2026-10-31 — Restore the visible guide/current device and reconsider held twist

The owner confirms v1.5.26 face snaps respond but says knob turning still does not. Two prior PC-control turns were stopped with physical Escape. In the resumed work, v1.5.26 is installed with schema 27 and Alt-only estimation enabled. The guide was disabled and its saved location was on another part of the desktop. After quitting through General, change only modePanel and guideX/guideY to show it beside the visible Admin window. Verify programs, presets and device identity unchanged by that positioning write.

The previously selected Raw Input collection was absent, with no current matching receiver collection. The owner rolled the physical M512 using Home Identify; the actual app now reports Trackball identified and saves the new connection. Preserve native correction flags false and all existing program/preset settings. Do not replace identity based only on a device name guess.

A focused replay exposes an independent recognition bug: once stage 2 chooses faces, it never re-evaluates new motion during that continuous Alt hold. Thirty vertical frames followed immediately by seventy clear horizontal knob frames stays faces with zero turns in v1.5.26. Let a later clean candidate reopen the bounded observation window after the recent face decision; do not require key release or a pause. Preserve the uncertainty fallback and known X/Y ambiguity. Add explicit regressions for vertical/noisy start to knob, repeated twist/faces/twist transitions, current-face preservation and tiny-noise rejection.

Home and the floating guide previously reported only Snap at the main label even while estimating twist. Pass a distinct snapLabel derived from the native state so they visibly distinguish Snap faces, Reading Alt motion and Estimated twist 90°. Do not change internal navigation mode, key bindings or sensitivity to implement the labels.

Focused actual C# checks pass eight new transition/label cases. The exact old reproduction now changes faces -> twist and emits quarter turns in the same hold. Full in-memory interface checks pass. Extend the real Studio socket smoke test through twist/faces/twist in one session. Full PR/main Windows verification and the next Admin installation are pending; live physical recognition after the fix remains for owner confirmation.


# 2026-10-31 — Record, test and save per-program twist calibration

The owner referenced the SlimBlade Pro and requested in-app detection/calibration. Kensington's official product specifications state that model uses dual optical sensors for tracking and twist scrolling (https://www.kensington.com/en-ca/p/products/control/trackballs/slimblade-pro-trackball/). Do not imply the M512 acquires additional optical axes through software.

Add a bounded three-step wizard in Buttons & keys: five seconds each of normal directional rolls, clockwise knob turns and anticlockwise turns, with a 600 ms warm-up. Collect only the already selected trackball's raw X/Y and vertical scroll reports, without requiring held navigation or capturing keyboard text. Consume selected-trackball wheel events only during recording/test so they do not scroll the wizard; ordinary pointing and all native button mappings remain available. Cancel on program/device changes. Test locally before Apply; Cancel leaves the saved model untouched. Recordings stay in memory and are discarded when closed.

Prefer a separate reported scroll channel when clockwise/anticlockwise signs oppose and ordinary rolls do not scroll. Otherwise learn bounded X/Y feature prototypes, an axis/direction and comfortable movement for each 90-degree turn. Use robust per-direction feature medians, balanced class separation and a minimum decision margin so tied/overlapping signals fall back to faces. Label this as an estimate with observed pattern overlap, never a measurement of independent 6DOF. A reported scroll channel is distinct from the X/Y estimate. Pure unreported motion remains unavailable.

Store only the validated learned model, per program and in saved presets. Schema 28 adds a null calibration field without altering controls or gains. Dedicated Apply/Clear transactions preserve device identity, programs, presets and native correction flags, and protect the result against stale ordinary saves. The learned model is shared by Alt estimation and an optional explicit Twist hold. Reverse and Twist speed still apply; the manual X/Y selector is disabled with an explanation while using a saved calibration.

Validation: 37 focused actual C# calibration/persistence checks pass in memory, including identical face/twist ambiguity, reported-wheel native orientation, learned direction/amount, sensitivity/inversion, warm-up/timeout/cancel, save/restart/program isolation/presets/stale saves and invalid model rejection. Full in-memory interface behavior checks pass for record/review/live preview/cancel/apply/clear. Extend both real WebView smoke tests with the wizard states and actual bridge failures/clear, and the native server smoke with a calibrated scroll-to-90-degree camera operation. Full cloud checks and installation are pending. Live physical calibration quality must be tested by the owner after installation; do not substitute synthetic samples for the owner's calibration. Deliver only the next Admin prerelease and leave public stable unchanged.


# 2026-11-01 — Verify release and initialize/reconnect the calibration wizard

PR #11 and merged main run 41271484428 passed 443 core checks, syntax/version/release policy, both builds and both real WebView/native smoke tests. Admin v1.5.27 was installed through its own Check again / Update now control; the local ProductVersion/source matched 23022492ac723142b52cca06507ddc99fc9c5cdb and SHA-256 f6c5af55f0495c8ca6ba8dd10cae74210684fbc37e094df7294fa705e4ef5bfc. Programs, presets, selected device, active program, guide and native correction flags matched the prior installation. Public stable remains public-v1.0.1.

Local visual inspection caught a startup integration gap: renderCalibration was called in a quick setup but not renderAll, leaving its summary blank and Clear enabled on initial load/program reload. Initialize it from renderAll and add a regression exercising that exact render entry point. The input fail-open/reconnect state also prevented opening the new wizard. Let a selected/enabled trackball open the wizard while reconnecting; disable only recording/test until physical input resumes. Keep review/complete samples open, interrupt and discard only an unfinished sample on lost input, preserve a completed unsaved model, and still cancel when the actual selected device or program changes. Do not manufacture physical input or substitute synthetic data for owner calibration.

Add two core interruption regressions, dynamic UI reconnect/ready tests, startup-render verification and a real WebView bridge test opening/cancelling the wizard without physical hardware. Final follow-up cloud validation and Admin update are pending. Physical gesture quality still requires the owner's own samples and test preview.


# 2026-11-01 — Release calibration's global navigation lock and verify sensor delivery

The owner reports that none of the controls work in Studio or other apps after v1.5.28. Current settings retain the expected held keys, selected device, native correction flags false and no saved calibration. Windows enumerates the selected mouse collection and the app is responsive. Standalone input captures observed no reports, but their timing/restrictions do not establish a hardware fault; do not claim the owner failed to roll the ball. A browser check subsequently reports the actual Studio adapter connected on protocol 2, so do not present an earlier absent TCP snapshot as an established cause of the ongoing failure.

The SlimBlade images describe hardware-supported dual-sensor ball-twist scrolling, confirmed by Kensington's official product specifications. The M512 X/Y calibration is an estimate and cannot create an independent unreported rotation measurement. Do not promise that another calibration makes it equivalent to a SlimBlade or adds six independently measured axes.

Fix a reproducible routing problem: IsCad previously required !calibration.Active, disabling all CAD navigation for as long as the wizard stayed open, even in review or after switching to Studio. Reserve input only while recording/testing. When the foreground process leaves Admin, interrupt only the incomplete sample or live preview, preserve completed samples/model, and release navigation. Invoke that foreground check before collecting motion/wheels and during the controller tick.

Fix recovery from a transient UI pause with a selected device still present. Previously fail-open set failed/recoverOnInput, but an unchanged registration/presence left input unavailable until another physical report. Resume only after 250 ms of uninterrupted healthy watchdog ticks, a running hook pump, owned registration and present selected device. Continued stalls/device/hook/registration faults reset that grace period; non-recoverable failures remain stopped. Preserve fail-open native mouse behavior and existing bindings.

Add cumulative selected/delivered/other-device movement counts and age to status, without exposing device identifiers or saving recordings. Home and calibration display whether movement actually arrives, is idle, comes from another mouse, or is recovering. Presence alone remains distinct from live input.

Extract the existing decoded mouse-report handler without changing its flag/button/wheel mapping. Extend the actual Windows smoke test to route a synthetic selected raw report through InputDriver into Controller.Motion while pointer capture is off; reject other-device motion and verify counters. This is a synthetic pipeline test, not an owner's physical-device verification. Also exercise the actual Controller.IsCad calibration transitions and transient recovery without physical movement, plus core foreground/recovery and dynamic interface regressions. Full cloud validation, next Admin release/install, and physical receiving/Studio behavior confirmation are pending. Keep all existing controls/settings and leave public stable unchanged.


# 2026-11-01 — A slight knob nudge triggers a full cube quarter turn

The owner physically confirmed Shift + ball orbit works in Studio on Admin v1.5.29. They now report twist requires physically turning the ball too far and request a slight clockwise/anticlockwise knob nudge as the trigger for a complete 90-degree cube orientation step.

Separate input gesture distance from output camera angle. Keep the existing Alt-only intent classifier/uncertainty thresholds, but feed accepted twist into a short-nudge latch rather than accumulating a proportional physical quarter turn. At neutral Twist sensitivity the raw-axis trigger is about 30 counts, compared with roughly 1,571 previously. Emit exactly one full 90-degree output for a stroke, ignore continuation/large-packet excess, and rearm after a 220 ms pause, a deliberate opposite nudge, or key release. Alternating jitter must not accumulate. Zero sensitivity and Reverse still work; no other keys or speeds change.

Calibration continues to provide recognition and learned axis/sign, but a previous long recorded turn distance no longer makes the owner repeat a large turn. Use the same short trigger in the separate Twist hold, Alt estimate, calibration preview and native camera path. Leave ordinary Alt + wheel quarter turns alone. Describe progress as trigger progress rather than physical ball angle and explain small nudges/pause/reversal in controls and calibration prompts. Preserve existing calibration data, programs, presets, native buttons/wheels and all saved settings; no schema migration is required.

Validation: all 479 core regression checks passed in memory before the final slow-reversal refinement; all 26 focused short-nudge cases pass after that refinement. Coverage includes for small CW/CCW triggers, exact face/distance preservation, one output per stroke, pause/reverse/key-release rearming, large packets, jitter, zero/inverted sensitivity, older long-distance calibration and preview parity. Update the prior cross-axis fallback regression: a valid short nudge now triggers before a later path change, and later cross-axis motion must resume face selection without extra twist steps. Full interface checks pass. Real Windows/native smoke now uses 30-count explicit nudges and a 64-count short estimated stroke. Full cloud verification and the next Admin installation are pending. Alt-only X/Y intent remains an experimental estimate; do not claim independent hardware twist sensing.


Clear the calibrated classifier's short history on a deliberate reversal along the learned axis. Otherwise the old clockwise stroke and the new anticlockwise nudge cancel in the observation window and can incorrectly fall back to face selection. Add a calibrated-preview clockwise/anticlockwise short-nudge regression. Keep cross-axis directional fallback unchanged.

Very slow nudges may arrive as one/two-count packets. Recognize an aligned reversal at sub-four-count packet sizes, while keeping its accumulated trigger threshold and cross-axis fallback. Verify a slow calibrated reverse nudge as a separate regression.

The first nudge must also pass intent recognition at the shorter distance. Reduce the uncalibrated minimum travel from 60 to 30 counts, retaining geometric confidence checks. While continuous tiny reports are still below that distance, allow a bounded 640 ms/48-sample observation instead of rejecting the gesture at 160 ms. Stopped sub-trigger movement still resolves without firing; directional cross-axis input still falls back promptly. Verify the first 32-count nudge, a slow one-count-per-report first nudge, and timeout of a stopped tiny movement.

A short separate-Twist nudge can arrive before the host's camera handshake. Check pose readiness before consuming the gesture, keeping that input pending until the pose is available. Suppress camera commands for a latched/no-step continuation. The actual Controller smoke now verifies first-nudge retention, an exact native quarter turn after the handshake, and no duplicate camera command for continuation.


# 2026-11-01 — Recognize tiny, brief knob turns

The owner confirms v1.5.30 improves the gesture but asks for a much slighter knob turn to reliably trigger a cube orientation step. The prior detector still required at least 30 raw-axis counts and normally four samples/112 ms; a short coalesced packet or small burst could fall back to faces. Once faces were selected, its retry required a four-count packet and discarded the preceding small packets.

Lower the neutral trigger to eight raw-axis counts, retaining exact one-step-per-stroke output, pause/reverse/key-release rearming and saved gain/inversion. Accept a coherent short burst after 48 ms of observation or 32 ms of settling, including one Windows-coalesced report. Tolerate one/two-count cross-axis quantization at tiny distances without broadly accepting sustained diagonal face strokes. Reconsider a complete recent tiny burst after face selection, not just its last report. Clear old opposite-direction evidence before the new reverse nudge. Bound history to eight recent reports and the existing pending limit; no raw recordings are persisted.

For saved X/Y calibration, add a nudge decision using its geometric features without penalizing a different recorded movement amount/speed. Keep ordinary calibrated face patterns and identical-pattern overlap fallback. Preserve the existing full calibration model/schema and all owner settings. This remains the approved experimental Alt-only intent estimate; do not claim independent hardware twist measurement or guarantee recognition of physically unreported motion.

Validation: 506 full core checks pass in memory, including 23 new tiny-burst cases for both signs, one-packet/one-count input, quantization wobble, after-face transitions, exact face/distance/orientation, reverse/pause/continuation, stopped subthreshold movement, jitter, gains/inversion and older long calibration. Actual Controller/native smoke now uses eight-count explicit and one-packet estimated nudges, checks the immediate opposite turn and continuing-stroke latch. Full Windows workflows, next Admin-only release/install and owner physical feel confirmation are pending. Leave public stable unchanged; do not create local development folders.


# 2026-11-01 — Use Alt plus the left scroll wheel for exact orientation snaps

The owner's latest clarification selects Alt + ball for neighboring cube faces and Alt + the small left scroll wheel for 90-degree orientation snaps. Preserve the top-left button as middle click. Do not ship the superseded button-held proposal. The confirmed native wheel layout is left = vertical and right = horizontal, with device correction flags false; do not swap axes or buttons.

Disable ambiguous X/Y twist estimation in the 3D template and quick setups. Schema 29 migrates only the existing 3D template (including a renamed program with that defaultKind), persists the migration atomically, and leaves separate custom programs, saved presets, learned calibration, optional custom Twist chords and speed settings intact. A later explicit opt-in to the legacy estimate remains possible.

Route only the left/vertical wheel through native Snap orientation. A full 120-unit notch makes one exact quarter turn; partial reports accumulate, reverse notches rotate back, and releasing the gesture discards unfinished wheel progress. Preserve other navigation modes' existing two-wheel behavior; the right wheel in Snap takes its normal/configured path. Update Home, held-key instructions and the floating guide to describe the wheel action without estimated-twist labels in the default setup.

Add core checks for wheel routing, partial/full/reverse notches, all six cube faces, face/distance preservation, zero proportional gain, four-step return, atomic one-time default migration and preservation of custom programs/settings. The actual Controller/native smoke exercises wheel messages through the pending frame/camera path and verifies right-wheel pass-through and Alt-ball face selection without using the estimator. All 539 core checks and 26 interface behavior checks pass in memory. Full Windows/native verification, the next Admin-only release/install and physical wheel feel confirmation are pending. Public stable stays unchanged; no local development folders are permitted.

# 2026-11-01 — Zoom toward the cursor without changing orbit

The owner confirms orbit and zoom respond and asks zoom to follow the pointer like their 3D controller. Add an optional zoomPivot to native scenes, distinct from the orbit pivot. Host adapters pick a visible surface under the cursor or intersect its ray with the target-depth plane over empty space. Preserve legacy provider behaviour, transform the optional anchor with camera-only operations, and keep clicks/wheels and all user settings unchanged. Chamfer Studio's companion bridge adds the same field and fixes legacy orthographic cursor zoom.

Validation: 571 core checks pass in memory, including perspective/orthographic inward/outward anchor projection, independent orbit, pan/roll/navigation/snap transforms and legacy compatibility. Studio's isolated browser checks pass actual surface/empty-space cursor picking, both projections, both protocols, frame coalescing, modal/session guards and unchanged model/history. The real native HTTP handshake adds cursor-anchor and malformed-input smoke coverage to both executable editions. Physical M512 cursor-targeting and third-party host execution remain owner checks. Deliver the next Admin version only; do not promote the public draft. No new Trackball development folder is created on the PC.

# 2026-11-01 — Remember multiple trackballs and correct navigation cadence

The owner plans to buy a SlimBlade Pro and wants the existing M512 to remain usable and detected. They also report that movement lacks Rotatrix's smooth feel. Preserve the current controls, native click/wheel layout, cursor zoom and saved speeds; do not replace the M512 identity when another device is identified.

Add schema 30 with a bounded list of explicitly identified trackballs, names and per-device click/wheel corrections. Migrate the saved selected identity and flags once. Stale UI saves, program changes, presets and resets must preserve the device list. Normal resets clear device corrections while keeping identities. Runtime input switches to a remembered device on actual relative movement/button/scroll input, keeps unknown mice ordinary, avoids stealing a held drag, and falls back/reconnects among present remembered devices. Selection and names have dedicated app mutations and UI controls. Input switching does not write settings on every report. Physical SlimBlade hardware is not connected yet; do not label that path hardware-tested or claim a full raw three-axis stream from its stock firmware.

Measure a concrete timing issue: on this PC a standalone 16 ms WinForms timer gave median 31.02 ms, while an 8 ms request gave median 15.54 ms. Use the shared 8 ms navigation policy to avoid the two-tick quantization; keep UI status capped at 20 Hz, idle work reduced, direct pointer delivery, native pose coalescing, gains and glide unchanged. No system timer-resolution change or extra smoothing delay is introduced. Retain a read-only timing diagnostic.

Validation: 599 full core checks and 27 interface checks pass in memory, including legacy migration, both devices after restart, separate corrections, stale saves, presets/reset preservation, runtime switching, unknown-mouse isolation, drag locking, reconnect and movement totals at the faster cadence. Actual raw-input and WebView smoke coverage exercises two remembered sensor fixtures, select/rename and separate correction flags. Windows builds/UI checks and next Admin release/install are pending. Physical M512 feel and future SlimBlade reports remain owner checks. Keep public stable unchanged; source and history stay on GitHub with no new local development folder.

The first Windows run (41343694379) passes all core/interface/build checks and the Admin UI/native suite. The public-edition reconnect fixture fails because it inherits the Admin suite's two remembered test identities, while the older single-device fixture overrides only the preferred-device delegate. Isolate the roster around the existing reconnect/key and raw-motion fixtures, then restore it and re-enumerate presence. Keep all exact reconnect assertions and the new two-device smoke checks intact. Rerun both executable editions before release.
