# Chamfer Trackball working agreement

The owner authorized rebuilding the editable source in this existing repository.
Keep source, tests and version history on GitHub. Do not create development folders
on the owner's PC. Deliver only the executable as the user-facing download.

Preserve standard mouse clicks and wheels by default. Never change Windows-wide
mouse button swapping to implement app bindings. The owner's latest requested controls
are Shift orbit, Ctrl+Shift pan, Ctrl plus ball for zoom, Alt plus ball for six-face
snapping, Alt+Shift for the new customizable held Twist, and Windows navigation with Alt for height. Reserve the Windows tap in the 3D
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
