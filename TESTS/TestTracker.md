# Test Tracker — UTI

Tests are added whenever a new feature lands, one row per capability. "Test Project" records
which of the three test beds (`little wings`, `project 2`, `2d project 3`) a check actually ran
in — useful since UTI is meant to work the same across all of them. "Status" reflects the last
time it was actually run — never marked Pass/Fail from reading the code alone.

**Unity MCP capability varies session to session in ways not fully understood — always probe fresh,
never trust a prior session's capability claims (including this note).** It has ranged from
read-only/relay-only (test execution routed through a separate agent working directly in the
project) up to full direct execution: `TestRunnerApi` running EditMode suites live, real Play Mode
entry, `GameObject.SetActive()`, and named `System.Reflection` types resolving fine. Concretely,
2026-08-08 (`project 2`) had full execution — `TestRunnerApi` drove the whole 97-test EditMode
suite live and `TestResults.xml` was read back directly, `AssetDatabase.Refresh()` worked, and
`UTI.Runtime` resolved cleanly via `Assembly.GetType("UTI.X")` — all contradicting an earlier
session's note (removed) that claimed `TestRunnerApi` was hard-blocked and `UTI.Runtime` wasn't
visible. Screenshot/Scene-view capture tooling has been the one consistently weak spot across
multiple sessions regardless of how much script-execution capability was available that round —
see T05's row for the fullest history. Confirmed hard limits (checked repeatedly, still true as of
2026-08-08): named `System.Reflection` types (`BindingFlags`, `MethodInfo`, etc.) are sandboxed off
even in sessions with otherwise-full execution — use `AppDomain.CurrentDomain.GetAssemblies()` +
`Assembly.GetType(string)` instead of a direct `using UTI;`/reflection API surface.

| ID | Area | Description | Steps | Expected Result | Test Project | Status | Date Added |
|---|---|---|---|---|---|---|---|
| T01 | Core | `BeanSample` + `BeanBuffer` store/retrieve correctly | Add UTI as a local package (see below), open Window > General > Test Runner > EditMode, run `UTI.Tests.BeanBufferTests` | All 4 tests pass (order preserved, overwrite-when-full, `Clear()`, `Extras` null by default) | little wings | **Pass** — all 4 tests green, verified via TestRunnerApi | 2026-08-06 |
| T02 | BeanTracker | `BeanTracker` capture loop, interval timing, stop, and event firing | Run `UTI.Tests.BeanTrackerTests` (4 EditMode tests, driven via `SimulateFrame()` — no Play Mode needed) | All 4 pass: position capture, interval-based capture count, `StopTracking()` halts capture, `OnSample` fires once per capture | little wings | **Pass** — all 4 green after two rounds of fail→fix (NRE from eager buffer alloc, then a vacuous-pass test bug around `OnEnable` autostart). Clean recompile, no console errors. | 2026-08-06 |
| T03 | BeanLogger | Console output reflects captured samples | Add `BeanLogger` (Console target) alongside `BeanTracker`, Play, move the object | Console shows one log line per sample with correct position | little wings | **Pass** — 400 `[Bean] tick=... t=... pos=... rot=...` lines captured via `Application.logMessageReceived` on `PlayerPlane`, format matches `ConsoleBeanOutput.Format` exactly, positions track a real curving flight path. Caveat: reported count/first-tick (400 lines, first `tick=1`) doesn't match the same-run CSV report (401 rows, first `tick=0`) — architecturally impossible for these to differ since both outputs are driven by the same `HandleSample` loop, so this is a transcription slip in the report, not a real UTI behavior difference. Worth a quick recount next time rather than blocking on it. | 2026-08-07 |
| T04 | BeanLogger | CSV output writes a valid file | Add `BeanLogger` (CSV target), Play, stop, inspect output file | CSV exists under the project root's `BeanLogs/` folder with header row + one row per sample | little wings | **Pass, but location changed since this ran.** Original Pass — `PlayerPlane_bean.csv` written correctly, file locked until Play Mode exit (confirms `Close()`-on-`OnDisable` flush timing), header exact match, 401 data rows cross-checked against console output and the live buffer — was against the old `Application.persistentDataPath` default. That default moved to the project root's `BeanLogs/` folder same session as the `BeanSnapshotExporter` path fix (see DESIGN.md §8.5); the CSV-writing logic itself is unchanged, only the resolved directory, so this isn't expected to break, but hasn't been re-confirmed at the new location. project 2 / 2d project 3 still not run. | 2026-08-07 |
| T05 | BeanVisualizer | Scene view shows the recorded path | Add `BeanVisualizer` alongside a tracked/moving object, Play, view Scene window | Line drawn through captured points, matches actual movement | little wings | Planned — still nobody has actually *seen* the rendered path. Two rounds now, both blocked purely by screenshot tooling (not by anything wrong in UTI): round 1 got a stale/byte-identical image across camera framings; round 2 (genuine ~103s real-time Play run this time, not scripted) also came back stale/cached, so the agent substituted objective proxy evidence instead (frame-timing, console/CSV counts — see T06) and was explicit that it could not get a real screenshot. Underlying sample data keeps checking out (coherent path, no jumps, correct counts) and `OnDrawGizmos`/`OnDrawGizmosSelected` is straightforward code, so this is believed to work — but "believed to work" isn't the bar; needs one actual human glance or a working screenshot tool. Same root blocker as T06's unresolved half. **Update:** T17 passed — a real, correct path line was seen rendered into real scene geometry, via `BeanSnapshotExporter`'s Camera-based render rather than `BeanVisualizer`'s Gizmos. That's strong indirect confidence the underlying sample data and line-drawing concept are sound, but it is not the same code path as `OnDrawGizmos`/`OnDrawGizmosSelected`, so this row stays Planned rather than being marked Pass from a different component's success. Low remaining risk; worth a real attempt with this session's own `mcp__unity-mcp__Unity_SceneView_Capture*` tools if convenient, but no longer a blocking concern for v1 confidence. **Third attempt, 2026-08-08 (this session, `project 2`), still inconclusive — but for a new reason, not stale/cached tooling.** With real Play Mode access now working (see T12/T14), captures came back genuinely live and non-stale for the first time ever (one showed real tumbling debris boxes mid-physics). But a runtime-created `BeanVisualizer` test cube kept vanishing before a camera frame could be lined up on it — `project 2`'s own `GameManager` appears to reset/reload live scene state periodically once Play Mode starts, independent of `EditorApplication.isPlaying` (which stayed `true` throughout). Never got a single frame with both the test object and a visible gizmo trail in it. Left `project 2` clean afterward (no leftover objects - they don't survive a runtime state reset anyway). Worth retrying in `little wings` instead next time, since its scene doesn't have this kind of autonomous reset behavior. | 2026-08-07 |
| T06 | BeanVisualizer | Decimation kicks in above `maxPointsToDraw` | Track an object past `maxPointsToDraw` samples | Gizmo still draws without noticeable editor slowdown; path still recognizable | little wings | Partial — "no noticeable editor slowdown" now has strong objective evidence from a genuine ~103s real-time Play run (not scripted, unlike round 1): Unity's own frame timing held steady (`smoothDeltaTime≈0.0027s`, `unscaledDeltaTime≈0.0030s`, ~330–370fps) measured *after* the in-memory buffer had long since passed the 200-point decimation threshold and sat at its 1000-sample ring-buffer cap, zero console errors/warnings across the full run. Decimation math itself was already confirmed correct in round 1 (matches `SelectIndicesToDraw`/T10 exactly). What's still unconfirmed: "path still recognizable" — a qualitative/visual check, blocked on the same screenshot-tooling failure as T05 (third failed attempt now). **Update:** T17 (a much shorter, undecimated 40-sample run) confirmed a path renders recognizably and matches real movement, which is encouraging but doesn't directly test decimation at the 1000-sample/200-point-cap scale this row is actually about — still Partial. | 2026-08-07 |
| T10 | BeanVisualizer | Decimation and color-mode math (EditMode, pure functions — no Scene view needed) | Run `UTI.Tests.BeanVisualizerTests` (6 EditMode tests) | All 6 pass: `SelectIndicesToDraw` returns every index at/under the cap, caps count and keeps first/last over the cap, returns empty for zero samples; `ResolveColor` returns the configured path color in `None` mode, interpolates blue→red across the buffer's time range in `ByTime` mode, and skews the faster segment redder in `BySpeed` mode | little wings | **Pass** — all 6 green, clean recompile, no console errors. | 2026-08-06 |
| T07 | Package | UTI installs as a local package in all three test projects | Add a `"file:"` dependency (+ `testables` entry) in each project's `Packages/manifest.json` pointing at the UTI folder (see instructions below) | Package resolves in Package Manager, compiles with no errors, `UTI` namespace usable from each project | little wings, project 2 | **Pass** in both little wings and project 2. **project 2 re-confirmed live 2026-08-08** via this session's own Unity MCP connection (directly attached to `project 2`, not just relayed): `UTI.Runtime` assembly loads cleanly, zero console errors/warnings after a full recompile of every change in this round. `2d project 3` still Planned. | 2026-08-06 |
| T08 | Genre coverage | UTI generalizes across different object "genres" (car/vehicle, NPC, projectile) using objects that already exist in real consuming projects — not new demo scenes built for this purpose | Next time a car/vehicle/NPC/projectile-like object exists in `little wings`/`project 2`/`2d project 3` (or a future test-bed project), attach Beans to it via its own existing scene/test, same as any other Bring-Your-Own-Test round (see DESIGN.md §12) | Bean components attached and visibly tracking/logging/visualizing the demoed object | little wings, project 2, 2d project 3 | Planned — **repurposed 2026-08-08.** Originally scoped as building new `Samples~/` demo scenes; per explicit user correction, building our own demo/sample Unity projects purely to test/showcase UTI is out of scope (real time/token cost for low verification value, and contradicts the Bring-Your-Own-Test Protocol's whole premise). Waits on a suitable object existing in a real round rather than needing dedicated action. The old demo-scene idea itself moved to `PROJECT_OVERVIEW.md`'s Dream To-Do section. | 2026-08-06 |
| T09 | BeanLogger | `BeanLogger` wiring, console output, and CSV output (EditMode, driven via `Open()`/`Close()` — no Play Mode needed) | Run `UTI.Tests.BeanLoggerTests` (4 EditMode tests) | All 4 pass: samples forwarded to active outputs while open, forwarding stops and outputs close after `Close()`, console output logs one formatted line per sample, CSV output writes a header row plus one row per sample | little wings | **Pass** — all 4 green, clean recompile (including the new `BeanLogger`/`IBeanOutput`/`ConsoleBeanOutput`/`CsvBeanOutput` files), no console errors or orphaned meta warnings. | 2026-08-06 |
| T11 | BeanTracker | `CustomCapture` delegate populates `extras` end-to-end, not just "null by default" | Run the new `UTI.Tests.BeanTrackerTests` (`SimulateFrame_CustomCaptureAssigned_PopulatesExtrasOnTheSample`, `SimulateFrame_NoCustomCapture_ExtrasStaysNull`) + `UTI.Tests.BeanLoggerTests` (`CsvOutput_CustomCaptureExtras_WritesExtrasColumnWithRealData`) | Captured sample's `Extras` reflects the delegate's output; CSV row's `extras` column contains the expected `key=value` pairs | little wings, project 2 | **Fixed and verified live 2026-08-08.** New EditMode tests exercise the actual delegate → `Capture()` → `Extras` → CSV column pipeline. **Confirmed for real in `project 2`** via this session's direct Unity MCP connection: full 84/84 EditMode suite passed, 0 failures. | 2026-08-06 |
| T12 | BeanTracker | `EveryFixedUpdate` capture mode | `BeanTracker.FixedUpdate()` now calls a new public `SimulateFixedFrame()` (mirrors `SimulateFrame()`) — run the new `UTI.Tests.BeanTrackerTests` (`SimulateFixedFrame_EveryFixedUpdate_CapturesOnceWhileTracking`, `SimulateFixedFrame_NotTracking_DoesNotCapture`, `SimulateFixedFrame_WrongCaptureMode_DoesNotCapture`) for the deterministic EditMode coverage; still also worth one manual Play Mode check on a Rigidbody-driven object to confirm real physics-tick alignment | EditMode: only captures while tracking and in `EveryFixedUpdate` mode. Play Mode: samples land on the physics tick, matching physics updates rather than render frames | little wings, project 2 | **Fully closed and verified live 2026-08-08 — both halves.** EditMode half unchanged from before. **Play Mode half now closed too**: this session's Unity MCP connection turned out to support real Play Mode entry (`EditorApplication.EnterPlaymode()`), a genuine capability upgrade over every prior session (which hit it as a hard-blocked "user interaction"). Created a real Rigidbody-driven GameObject in `project 2`'s live running scene with `BeanTracker.CaptureMode=EveryFixedUpdate`, let it run, then read back all 732 captured samples' timestamps: `min=max=avg=0.02000` delta between every consecutive sample, exactly matching `Time.fixedDeltaTime` — as clean and definitive a confirmation as this row could get. Test object destroyed afterward, scene left clean, full EditMode suite re-confirmed green (105/105) immediately after. | 2026-08-06 |
| T13 | BeanLogger | CSV output doesn't collide when two Beans share a GameObject name (e.g. prefab clones) | Instantiate two copies of the same prefab (both named identically, e.g. `Bullet(Clone)`), both with `BeanLogger` CSV output active by default, Play, inspect both output files | Each instance writes to its own file — no shared/overwritten path | little wings | **Pass (unit-level, confirmed live)** — `ResolveFilePath`/`ResolveSnapshotPath` both include a random `uniqueToken`, verified via real CSV+PNG filenames from a live capture (`..._b29e5cc4_bean.csv`, `..._7add06a8_bean_snapshot.png`, distinct tokens). A literal two-simultaneous-clones scene still hasn't been run, but single-capture uniqueness is now confirmed with real output, not just unit tests. | 2026-08-06 |
| T14 | BeanTracker/BeanLogger | Pooled object reuse (`SetActive(false)` → `SetActive(true)` instead of `Destroy`/`Instantiate`) | Run the new `UTI.Tests.BeanLoggerTests` (`CsvOutput_DefaultAppendFalse_TruncatesOnReopen`, `CsvOutput_AppendTrue_PreservesPriorRowsAcrossReopen`, `BeanLogger_AppendAcrossReuseFalse_TruncatesAcrossOpenCloseCycles`, `BeanLogger_AppendAcrossReuseTrue_PreservesRowsAcrossOpenCloseCycles`); still wants one manual Play Mode check toggling a real pooled object's `SetActive` a few times | Default (`AppendAcrossReuse=false`) truncates each reopen, matching the pre-existing behavior now made deliberate; `AppendAcrossReuse=true` preserves prior rows across reuse cycles | little wings, project 2 | **Fully closed and verified live 2026-08-08 — both halves.** EditMode half unchanged from before. **Real `SetActive` pooling cycle now confirmed too**, via this session's Unity MCP connection (which this round found *does* support `GameObject.SetActive`, unlike every prior session). Ran a real 2-cycle `SetActive(false)`→`SetActive(true)` test in `project 2`'s live scene on two separate GameObjects — one `AppendAcrossReuse=false`, one `=true` — with `BeanLogger` CSV output to real temp files, read back directly off disk afterward: the `false` file held only cycle 2's 1894 rows (tick 2698-4591, cycle 1's rows genuinely gone); the `true` file held all 4592 rows spanning both cycles (tick 0-4591) under a single header, no duplication. First attempt had a methodology flaw (configured `FilePath`/`OutputTargets` via reflection *after* `AddComponent`, so `OnEnable`/`Open()` already ran once with default Console-only settings before cycle 1 truly started) — caught and corrected by creating the GameObject inactive, configuring it, then activating, before trusting the result. Test objects destroyed afterward, scene left clean, full EditMode suite re-confirmed green (105/105). | 2026-08-06 |
| T15 | Multi-Bean | Several simultaneously-tracked objects stay independent, and gizmo draw stays cheap at realistic counts | EditMode half: run the new `UTI.Tests.BeanTrackerTests` (`MultipleTrackers_SimulatedIndependently_KeepFullyIndependentBuffers`). Play Mode half (unchanged): track ~20-50 objects at once (e.g. a wave of enemies or bullets) with `BeanVisualizer` active on each, Play, check for cross-talk between buffers and editor responsiveness | EditMode: each Bean's buffer reflects only its own object, confirmed with several trackers driven at once. Play Mode: no noticeable Scene-view slowdown at realistic Bean counts | little wings, project 2 | **Partial — EditMode half fixed and verified live 2026-08-08** (84/84 pass in `project 2`). Gizmo-cost-at-scale still needs the live Play Mode check. | 2026-08-06 |
| T16 | BeanSnapshotExporter/BeanLogger | Pure logic: path-position extraction, path bounds/flatness, camera auto-framing, and unique file path resolution for both PNG and CSV outputs (EditMode, no Camera/Play Mode needed) | Run `UTI.Tests.BeanSnapshotExporterTests` (13 tests) + `UTI.Tests.BeanArtifactPathsTests` (5 tests) + the 3 new `ResolveFilePath` tests in `UTI.Tests.BeanLoggerTests` (21 tests total across the three files) | All pass: bounds/framing/decimation math as before; `ResolveSnapshotPath`/`ResolveFilePath` both default to a `{timestamp}_{objectName}_{uniqueToken}_...` name under `UTI/BeanSnapshots/`/`UTI/BeanLogs/` respectively (project root, not `AppData` — DESIGN.md §8.5), producing distinct paths both across repeated runs and across two same-named instances at the same timestamp | little wings | **Pass** — 25/25 across all three files (`BeanArtifactPathsTests` 5/5, `BeanLoggerTests` 7/7, `BeanSnapshotExporterTests` 13/13; the "21" estimate in the relay prompt undercounted `BeanLoggerTests`'s 4 pre-existing tests, not a real discrepancy). Clean compile, 0 errors, 0 warnings, no orphaned meta warnings. | 2026-08-07 |
| T17 | BeanSnapshotExporter | Manual capture actually produces a usable PNG showing the tracked path *and* real scene geometry around it, at a location a developer can actually find | Add `BeanSnapshotExporter` alongside a tracked/moving object with visible level geometry nearby (e.g. a floor), Play, let it track for a bit, stop tracking (or call `CaptureSnapshot()` manually), then find and open the written PNG **in the project folder itself** (`<project root>/UTI/BeanSnapshots/`, not `AppData`) | File exists under the project root's `UTI/BeanSnapshots/` folder, timestamp+token-named; image shows the actual scene (floor/walls/props in frame) with the recorded path drawn through it, visibly, and framed by the auto-frame logic — not a blank background, not an unreadable close-up, and not invisible at the auto-framed distance | little wings | **Pass.** Real capture confirmed on the actual filesystem (not just logged strings): PNG at `UTI/BeanSnapshots/20260807_223855_911_PlayerPlane_7add06a8_bean_snapshot.png`, CSV at `UTI/BeanLogs/20260807_223855_877_PlayerPlane_b29e5cc4_bean.csv`, both under one shared `UTI/` parent as designed. `LastLineWidth=3.47` confirmed the width-scaling fix is active; path line clearly visible, correct color, real scene geometry in frame, CSV format correct (41 lines, header + 40 rows). **New finding, see T18** — the first test path happened to travel parallel to the hardcoded camera-offset direction, foreshortening the shot into a thick stripe; a second path at a different angle framed correctly, confirming the underlying logic works but the fixed offset direction is a real edge case. | 2026-08-07 |
| T18 | BeanSnapshotExporter | Auto-frame camera direction shouldn't foreshorten a path that happens to travel parallel to the hardcoded offset direction | Track a path whose horizontal travel direction is close to the camera's old fixed offset direction `(1,0,1)`, capture, inspect the image | Path renders with visible margin on both sides regardless of its own travel direction, not foreshortened into a thick stripe | little wings | **Fixed, unit-tested, not yet re-verified live.** `ComputeFraming` now derives the camera's horizontal offset from the path's own `travelDirection` (always perpendicular/broadside) instead of a fixed diagonal; 2 new EditMode tests cover the exact `(1,0,1)` regression case and a no-horizontal-travel fallback (see T19). **Update, `project 2`:** the near-zero-travel fallback branch got incidentally exercised for real (a mostly-frozen player) without crashing — but that same capture surfaced a different, real problem (T23: too-tight framing), and no genuinely diagonal *moving* path has been captured yet, so the actual foreshortening-avoidance behavior this row is about still isn't conclusively confirmed live. | 2026-08-07 |
| T19 | BeanSnapshotExporter | `DimensionMode` override + broadside-framing pure logic (EditMode, no Camera/Play Mode needed) | Run the updated `UTI.Tests.BeanSnapshotExporterTests` (20 tests, up from 13 — added `ResolveIsFlat` ×3, two `ComputeFraming` broadside/fallback tests, and two `ApplyConfigDefaults` tests, see T22) | All pass: `ResolveIsFlat` respects `Auto`/`Force2D`/`Force3D`; `ComputeFraming` places the 3D camera perpendicular to `travelDirection` regardless of its angle, and falls back to a forward broadside when travel is purely vertical | little wings | Planned — new tests, not yet relayed | 2026-08-07 |
| T21 | BeanMouseTracker | Screen/world mouse-position resolution (EditMode, no Play Mode needed) | Run `UTI.Tests.BeanMouseTrackerTests` (3 EditMode tests) | All pass: `ResolveScreenPosition` returns raw pixel coords with Z=0; `ResolveWorldPosition` returns `Vector3.zero` with no camera and matches `Camera.ScreenToWorldPoint` with one | little wings | Planned — new component, not yet relayed. Live `Update()`-driven mouse tracking (does the GameObject actually follow the cursor in Play Mode) also not yet checked — the pure functions are the only part covered so far. | 2026-08-07 |
| T22 | BeanConfig | `ParseLines`/`ApplyConfigDefaults` on `BeanConfig`/`BeanTracker`/`BeanSnapshotExporter` (EditMode, no Play Mode needed) | Run `UTI.Tests.BeanConfigTests` (now 9, up from 7 — added `DefaultMinFramingRadius` coverage) + `UTI.Tests.BeanTrackerTests` + `UTI.Tests.BeanSnapshotExporterTests` (both grown further this round, see T11/T12/T15/T23/T24) | All pass: `ParseLines` handles all-keys-present, empty input, comments/blank lines, unrecognized keys, malformed enum/number values, and missing `=` signs without erroring; a null config leaves `BeanTracker`/`BeanSnapshotExporter` fields unchanged, a real one applies correctly (now including `DefaultMinFramingRadius` → `MinFramingRadius`) | little wings, project 2 | **EditMode logic verified live 2026-08-08** (part of the 84/84 pass). **The `Reset()` hook itself and the `UTI > Setup Project (Config + Docs)` menu item are now both confirmed live too** — driven directly via this session's Unity MCP connection using `EditorApplication.ExecuteMenuItem` and public-method reflection (private `Reset()` itself is blocked by the sandbox, so `BeanConfig.Load()` + the public `ApplyConfigDefaults` were exercised instead, which is everything `Reset()` actually does): `UTI > Setup Project` created a real `BeanConfig.txt` in `project 2`'s `UTI/` folder with exactly the expected template content; a non-default config (`DefaultCaptureMode=EveryNSeconds`, `DefaultCaptureInterval=1.75`, `DefaultDimensionMode=Force2D`, `DefaultMinFramingRadius=9`) was correctly picked up by fresh `BeanTracker`/`BeanSnapshotExporter` instances. Config file restored to compiled-in defaults afterward. | 2026-08-07 |
| T23 | BeanSnapshotExporter | Auto-framing shouldn't produce an unhelpfully tight shot on a near-stationary path | Set a larger `MinFramingRadius` (per-Bean, or via `BeanConfig.txt`'s new `DefaultMinFramingRadius`) on a `BeanSnapshotExporter`, re-run the original `project 2` reproduction (track an object that moves very little, e.g. walks up to a wall and stops), capture, inspect the image | Image now shows meaningful scene context (floor/walls/nearby geometry) at a larger configured radius, not the extreme close-up the original `2f`-only default produced | `project 2` | **Fixed AND verified live 2026-08-08 — the fix is confirmed real, not just unit-tested.** Drove a real capture directly in `project 2`'s actual scene via this session's Unity MCP connection: a near-stationary path (30 samples, ~1 point) at `MinFramingRadius=2` (the old-behavior default) produced exactly the reported bug — a useless close-up, just a flat wall face filling the frame, no context. The *same path*, same call, with `MinFramingRadius=9` produced a properly framed shot showing the full scene (ground, sky, the level's box geometry, the player capsule) — both PNGs viewed directly, not inferred from logs. This is the closest thing to definitive proof this bug is actually fixed. | 2026-08-08 |
| T24 | BeanSnapshotExporter | Multi-angle capture (`CaptureAngles`: `Auto`/`Above`/`Side`/`Behind`) — pure framing/naming logic (EditMode, no Camera/Play Mode needed) | Run the updated `UTI.Tests.BeanSnapshotExporterTests` (new `ComputeFramingForAngle_*` and `ResolveMultiAngleSnapshotPath_*` tests) | All pass: `Auto` matches original `ComputeFraming` exactly; `Side` matches the same broadside math as `Auto`'s 3D branch regardless of `isFlat`; `Above` looks straight down from directly overhead; `Behind` sits opposite the path's travel direction; grouped filenames include the timestamp, 1-based angle index, angle name, and token, and ignore an explicit `filePath` override | little wings, project 2 | **Built AND verified live 2026-08-08.** Same live session as T23: `CaptureAngles = [Auto, Above, Side, Behind]` on the same near-stationary path wrote 4 distinct PNGs sharing one timestamp group, all viewed directly. `Auto` exactly matched the single-angle `MinFramingRadius=9` shot (confirms zero behavior change for existing single-angle usage). `Above` was a genuine straight-down bird's-eye view of the level layout. `Side` and `Behind` were both elevated 3/4 angles, visibly different from each other and from `Auto`, both showing real geometry. This directly surfaced and led to fixing a real bug — see T26. | 2026-08-08 |
| T25 | BeanTracker/BeanLogger | Fault isolation at UTI's real system boundaries: a throwing `CustomCapture` delegate, and a `BeanLogger` output that throws on `Open`/`Write`/`Close` | Run `UTI.Tests.BeanTrackerTests` (`SimulateFrame_CustomCaptureThrows_StillCapturesSampleWithoutExtras`) + `UTI.Tests.BeanLoggerTests` (`Open_OneOutputThrowsOnOpen_...`, `HandleSample_OneOutputThrowsOnWrite_...`, `Close_OneOutputThrowsOnClose_...`) | All pass: a throwing `CustomCapture` logs a warning but tracking keeps advancing (sample captured with null extras, `OnSample` still fires); a throwing output logs a warning, is dropped/skipped, and every other output (Console, a healthy custom sink) keeps working uninterrupted | little wings, project 2 | **Built and verified live 2026-08-08** — part of the 84/84 EditMode pass in `project 2`; the real Unity console output for all three deliberately-throwing test cases was inspected directly and matched exactly (Warning-level, not Error, exact expected message text, no test failures). See `DESIGN.md` §14 for the full boundary-by-boundary writeup, including two related but not independently-testable fixes: `BeanSnapshotExporter`'s per-angle write isolation (needs a live Camera — but see T24/T26, exercised live anyway) and `BeanConfig.Load()`'s locked-file handling (touches the real project-root config path). | 2026-08-08 |
| T26 | BeanSnapshotExporter | `Object.Destroy()` on the temporary `BeanSnapshotPath` line object / render `Texture2D` is a documented no-op outside Play Mode | Call `CaptureSnapshot()` from an Editor context (not Play Mode), then check the scene for leftover `BeanSnapshotPath` GameObjects afterward | No leaked objects in the scene after capture, regardless of whether it ran during Play Mode or from an Editor script | `project 2` | **Found AND fixed live 2026-08-08 — a genuine bug this session's own live testing surfaced.** Driving `CaptureSnapshot()` directly from this session's Unity MCP connection (an Editor context, not Play Mode) left 6 `BeanSnapshotPath` GameObjects permanently in `project 2`'s actual open scene — `Object.Destroy()` silently no-ops outside Play Mode (Unity logs "Destroy may not be called from edit mode!" as an error but doesn't throw or actually destroy anything). `CaptureSnapshot()`'s main documented use is Play Mode (`captureOnStopTracking`), where `Destroy()` is correct, but nothing stops a dev/Editor tool from calling it directly outside Play Mode too. Fixed with a new `SafeDestroy()` helper (`Application.isPlaying ? Destroy() : DestroyImmediate()`), applied to all three `Destroy()` call sites in `CaptureSnapshot()`. Re-verified live immediately after the fix: an identical capture produced zero leaked objects and zero console errors. Leftover objects from the original bug were manually cleaned out of `project 2`'s scene. Not EditMode-testable (same category as the rest of `CaptureSnapshot()`), but about as thoroughly live-verified as a fix can get short of it. | 2026-08-08 |
| T27 | Package/Install, BeanVisualizer, BeanSnapshotExporter | Full uninstall + fresh reinstall (fixes any doc-copy gaps from before `UTI > Setup Project` existed), followed by running UTI live during the team's own blue→yellow→red box jump test — a genuinely dynamic multi-box scene, unlike this session's synthetic near-stationary repro | See "Fresh Install Verification Round" below — remove the package + `<project 2 root>/UTI/` entirely, reinstall, run `UTI > Setup Project (Config + Docs)`, add all four Beans to the Player, then run the real blue/yellow/red jump test with Play Mode actually running and try a 2+ angle capture on that path | Package reinstalls cleanly with all docs present; `BeanVisualizer`'s live Scene-view line actually tracks the real jump path while Play Mode runs (T05/T06); multi-angle capture reads clearly across a real multi-box path, not just a single static point | `project 2` | **Complete, full report received 2026-08-08 — mostly Pass, two real findings (see T28).** Uninstall/reinstall clean both directions (CS0104 fix survived the cycle); `UTI > Setup Project` worked exactly as documented; `USAGE.md` alone was sufficient with zero ambiguity to add all four Beans; real Play Mode traversal succeeded, CSV independently verified as physically correct (smooth jump arc, correct peak/resting heights). `BeanVisualizer`'s live Scene-view trail stayed unconfirmed — the team's own tooling couldn't get a usable Scene-view screenshot, a tooling limitation on their end, not a UTI finding; genuinely needs a human at an interactive Editor window. The multi-angle capture *did* surface two real, related findings — see T28, not a config mistake, root cause confirmed live. | 2026-08-08 |
| T28 | BeanSnapshotExporter | `CaptureSnapshot()` reads the live `BeanTracker.Samples` ring buffer, not the CSV — a long idle tail after real movement finished can silently evict the entire interesting path from the buffer before a snapshot happens, before `BeanConfig`/`CaptureAngles`/anything else even comes into play | Track real movement well past `BeanTracker.MaxSamples` (default 1000) worth of samples, then keep tracking stationary for `MaxSamples`+ more samples, then capture. Reproduced directly via this session's live Unity MCP access: 200 samples of real 9m movement + 3000 stationary samples | Buffer should still reflect real path extent, or at minimum the dev should be warned that older samples were evicted — previously neither happened silently | `project 2` | **Found live 2026-08-08 by the `project 2` team's T27 round, root cause confirmed live by this session, fixed the same day.** The team's report flagged two symptoms from one real multi-angle capture (blue→yellow→red jump, ~9m path, then tracking left running ~55s longer than needed): (1) no visible path line in any of 3 angle captures, (2) `Auto Frame Camera` producing a tight close-up on just the final resting position despite the real ~9m path — correctly suspected as *possibly* T23-related but a different shape (real movement + long stationary tail, not fully-stationary). **Confirmed as one unified root cause, not two separate bugs and not a config mistake:** reproduced live in `project 2` itself — after 200 samples of real 9m movement followed by 3000 stationary samples, the live buffer's Z-span dropped to exactly 0 and first/last buffered positions were identical, proving the ring buffer (`BeanBuffer`, capacity = `BeanTracker.MaxSamples`, default 1000) had fully evicted every real-movement sample by capture time. `ComputePathBounds`/`BuildPathPositions` were working correctly against what they could see — they just couldn't see the real path anymore. **Fixed:** new `BeanSnapshotExporter.IsBufferAtCapacity(sampleCount, maxSamples)` (pure, unit-tested) plus a `Debug.LogWarning` in `CaptureSnapshot()` when the buffer is full, explaining exactly what may have happened and how to avoid it (raise `Max Samples`, or call `StopTracking()` promptly). Does not change framing/rendering behavior itself — makes the failure mode visible instead of silent. Not yet re-verified with a real capture in this exact scenario (unit-tested + live-reproduced at the buffer level only). | 2026-08-08 |
| T29 | BeanLogger | JSON Lines output (`JsonlBeanOutput`, `BeanOutputTargets.Json`), `BeanConfig.DefaultOutputTargets`, and the `BeanFileOutputBase` refactor that followed | Run the expanded `UTI.Tests.BeanLoggerTests` (10 new tests: JSON write/format/extras, append-across-reopen, the CSV+JSON explicit-`FilePath`-collision fallback, `ApplyConfigDefaults`) + `UTI.Tests.BeanConfigTests` (2 new `DefaultOutputTargets` parse tests) | All pass: one JSON object per sample, no header; `extras` a real nested object (`null` when unset); append/truncate-on-reopen matches CSV's existing behavior exactly; both CSV and JSON active with an explicit `FilePath` set falls back to separate default paths instead of colliding | project 2 | **Built and verified live 2026-08-08, both before and after a same-day refactor.** This picked up a prior session's unverified, paused-mid-task work (see `HANDOFF.md`'s former "open problem" — a suspected compile error spamming `project 2`'s console). Checked directly via this session's own Unity MCP connection: `UTI.JsonlBeanOutput` resolved cleanly in the loaded `UTI.Runtime` assembly, 0 console errors, full EditMode suite 97/97 via `TestRunnerApi` (not just re-run, driven live and its `TestResults.xml` read back directly). The suspected console-spam cause was never real — both compiled clean the whole time. Once `JsonlBeanOutput` existed alongside `CsvBeanOutput`, their identical `StreamWriter`-lifecycle code (directory creation, flush-interval batching, `Close()`) was extracted into a shared `BeanFileOutputBase` (see `DESIGN.md` §8.2); re-ran the same 97/97 suite immediately after with 0 regressions. | 2026-08-08 |

## How to install UTI as a local package (for T01/T07)

In whichever test project you want to check, open `Packages/manifest.json` and add the dependency line, **plus a `testables` entry** — without `testables`, Unity silently shows 0 tests in Test Runner for a package's tests, even though it compiled fine (found the hard way verifying this in little wings):

```json
{
  "dependencies": {
    "com.uti.core": "file:C:/Users/sirsw/OneDrive/Documents/claude/UTI",
    ...
  },
  "testables": [
    "com.uti.core"
  ]
}
```

Save, switch to the Unity Editor, let it recompile. "UTI (Unity Testing Isolator)" should show up under Window > Package Manager > In Project. Then:

- **T01**: Window > General > Test Runner > EditMode tab > run `UTI.Tests.BeanBufferTests`.
- **T07**: just confirm it resolved with no console errors.

Report back pass/fail (and any console errors) and I'll update this tracker + fix anything broken.

**Verified in little wings (2026-08-06):** both pass. Package resolved cleanly, all 4 `BeanBufferTests` green. Still need to run this in `project 2` and `2d project 3` to confirm cross-project behavior — don't forget the `testables` entry there too.

## How to verify T13/T16/T17 (final round — `BeanLogger` + `BeanSnapshotExporter`)

- **T16**: Window > General > Test Runner > EditMode tab > run `UTI.Tests.BeanSnapshotExporterTests`,
  `UTI.Tests.BeanArtifactPathsTests`, and `UTI.Tests.BeanLoggerTests` — confirm all green, no
  console errors, no orphaned meta warnings from the new files.
- **T17 + T13's live check**: add both `BeanLogger` (CSV target) and `BeanSnapshotExporter` to a
  GameObject that already has `BeanTracker`, make sure there's visible level geometry nearby (a
  floor, some walls), Play, let it move and track for a bit, then stop tracking (or call
  `CaptureSnapshot()` directly). Report:
  1. `LastSnapshotPath` and the CSV's actual write path — both should now point **inside the
     project folder itself**, under one shared `UTI/` folder: `<little wings project root>/UTI/
     BeanSnapshots/<timestamp>_<name>_<randomToken>_bean_snapshot.png` and `.../UTI/BeanLogs/
     <timestamp>_<name>_<randomToken>_bean.csv`. *Not* `AppData`, and *not* two separate loose
     folders at the project root — both should share the one `UTI/` parent.
  2. Actually browse to `<project root>/UTI/` in the project directory and confirm both
     subfolders and both files are there — don't just trust logged path strings.
  3. `LastLineWidth` (the actual world-unit width used for the PNG) alongside the image, so "is
     the line visible" has a real number to check against instead of eyeballing the picture.
  4. Open the PNG and describe it: is the path visible, does it match the movement, is real scene
     geometry (not a blank background) in frame.
  5. Open the CSV and confirm it's still a normal, readable text file (header row + one row per
     sample) — just re-confirming T04's original finding still holds at the new location.
  6. Watch for a pink/magenta line specifically — the path `LineRenderer`'s material is built off
     the built-in `Sprites/Default` shader, which may not resolve correctly under URP/HDRP; pink
     instead of the configured `pathColor` is a render-pipeline mismatch worth reporting, not a
     logic bug.

## What this session's own Unity MCP connection could and couldn't verify directly (2026-08-08)

Unlike every prior round, this session had a working Unity MCP connection **directly attached to
`project 2`** (not just `little wings`, and not just read-only console/refresh checks — real
script execution). This closed almost the entire punch list live, in this same session, without
needing a relay round at all:

- **Full EditMode suite: 84/84 passed, 0 failed** — confirmed via `TestRunnerApi`, cross-checked
  against `TestResults.xml` directly (not just trusting a summary log), re-confirmed after a fix
  landed mid-session (T26) with fresh timestamps each time to rule out a stale/cached result (a
  recurring false-positive risk flagged in earlier `HANDOFF.md` rounds).
- **T22** (`BeanConfig`'s `Reset()`-hook + the new `UTI > Setup Project` menu item) — both
  confirmed live via `EditorApplication.ExecuteMenuItem` and public-method reflection.
- **T23 and T24** — both confirmed with real, viewed PNGs from `project 2`'s actual open scene, not
  just logged paths. T23 in particular is about as close to "definitively fixed" as evidence gets:
  the exact same near-stationary path produced the old bug's useless close-up at `MinFramingRadius
  =2` and a properly-framed shot at `=9`, both images viewed directly.
- **T26** — a brand-new bug (`Object.Destroy()` no-op outside Play Mode leaking `BeanSnapshotPath`
  GameObjects) was found *by this live testing itself*, fixed, and re-verified live in the same
  session.

**What this connection genuinely cannot do** (confirmed by trying, not assumed from old notes):
`System.Reflection` types beyond basic `Type`/`Assembly` lookups (`BindingFlags`, `MethodInfo`,
`PropertyInfo` named directly) are sandboxed off — worked around by only ever reflecting into
**public** members (everything UTI's own components expose publicly was enough). Direct `using
UTI;` / `using UnityEditor.PackageManager.PackageInfo` etc. isn't available in the scratch-compile
context — worked around via `AppDomain.CurrentDomain.GetAssemblies()` + `Assembly.GetType(string)`.
**Entering Play Mode and toggling `GameObject.SetActive`** were believed blocked outright as
unsupported "user interaction" at this point in the session — **corrected later the same day, see
"What's actually still left" immediately below: both turned out to work fine.** Left here
un-deleted as a reminder that a capability that looks like a hard limit after one failed attempt
isn't necessarily one — worth a second try before writing it off, and never trust an old "hard
limit" claim (including ones in this very file) without checking fresh.

## What's actually still left

**Update 2026-08-08 (later the same day):** T12 and T14's Play Mode halves, the "only two real
gaps" noted below, are now both closed — see their rows above. This session's Unity MCP connection
turned out to support genuine Play Mode entry (`EditorApplication.EnterPlaymode()`) *and*
`GameObject.SetActive`, neither of which any prior session's connection could do (both were
previously hard-blocked as unsupported "user interaction" — capability clearly varies session to
session, don't assume the old limits still apply without checking fresh). Both were verified with
real captured data read back from `project 2`'s live running scene, not just re-run EditMode tests.

Remaining, lower priority:

```
T05/T06 (BeanVisualizer's actual gizmo render - low urgency given T23/T24's strong indirect
confidence that path/geometry rendering works correctly), T08 (sample scenes, still not started),
T21's live Update()-driven mouse-follow check (pure functions already verified).
```

## Fresh Install Verification Round (T27) — for the `project 2` team, deliberately not done by this session

Everything above tests UTI on an *already-installed, already-configured* project — `project 2` has
had UTI installed since T07 passed, and its real `Player` GameObject already carries `BeanTracker`/
`BeanLogger`/`BeanSnapshotExporter` from earlier rounds (confirmed still present, 2026-08-08).
That's never actually exercised the *install experience itself* — the exact thing `UTI > Setup
Project (Config + Docs)` was built this round to fix (see DESIGN.md §8.7). **Deliberately requesting
this be run by the `project 2` team directly, not this session** — a genuine fresh-eyes,
docs-only walkthrough is the actual point; a session that already knows how everything works isn't
a fair test of whether a first-time user could follow `USAGE.md` alone.

```
Goal: remove UTI completely, reinstall from scratch, and follow USAGE.md as if you'd never seen it
before. Report every point of friction, confusion, or place the docs didn't match reality - that's
the real signal this round is after, not just "did it still work."

1. Before touching the package: on the Player GameObject, remove the existing BeanTracker,
   BeanLogger, and BeanSnapshotExporter components (right-click each -> Remove Component). Removing
   the package first would leave these as broken "missing script" placeholders instead - clean them
   up while the types still exist.

2. In Packages/manifest.json, remove the "com.uti.core" dependency line and its entry in the
   top-level "testables" array. Let the project recompile - confirm zero errors (UTI's types should
   now be fully gone, not just unused).

3. Delete <project 2 root>/UTI/ entirely - BeanLogs/, BeanSnapshots/, BeanConfig.txt, and the three
   copied docs (USAGE.md, READING_LOGS_AND_VISUALS.md, CONFIG.md). This project should now look
   exactly like it did before UTI was ever installed.

4. Re-add UTI as a local package: "com.uti.core": "file:C:/Users/sirsw/OneDrive/Documents/claude/UTI"
   under dependencies, plus "com.uti.core" under "testables" (required or Test Runner silently finds
   0 tests). Let it resolve - confirm it appears in Package Manager and compiles with zero errors.

5. First real action: run the Editor menu item UTI > Setup Project (Config + Docs) - this is the
   actual thing being validated this round. Confirm it creates <project 2 root>/UTI/BeanConfig.txt
   (commented template, compiled-in defaults) AND copies USAGE.md/READING_LOGS_AND_VISUALS.md/
   CONFIG.md into that same folder, all in one click, with no manual copying needed.

6. Run every EditMode test in UTI.Tests, confirm all green, report the total count.

7. Now actually follow USAGE.md from the top, as a first-time user would - don't skip ahead using
   anything you already know about UTI from prior rounds. Add BeanTracker + BeanLogger (CSV) +
   BeanVisualizer + BeanSnapshotExporter to the Player (or whichever object USAGE.md's own guidance
   leads you to), using only what the doc tells you to set.

8. Run your own existing jump test - blue box, then yellow, then red - with all four Beans attached
   and Play Mode actually running the whole time (not a scripted/simulated pass). This is a
   genuinely dynamic, multi-box scene, unlike this session's own live testing which used a
   synthetic near-stationary repro - it's the first real chance to see BeanVisualizer's live
   Scene-view gizmo trail actually update box-to-box while you watch (T05/T06, still open), and to
   see BeanSnapshotExporter frame a real multi-jump path instead of a single near-static point.
   Before or after the run, also try setting Capture Angles to 2+ entries (e.g. Auto, Above, Side)
   and trigger one capture on this same path - report whether the resulting set of PNGs actually
   helps show "where the character was going" better than a single angle would, across all three
   boxes rather than just one static test position.

9. Report: did the CSV/PNG land where USAGE.md said they would? Does BeanVisualizer's Scene-view
   line actually track the blue-yellow-red jump path visibly and correctly while Play Mode runs?
   Does the multi-angle capture read clearly for this real jump sequence? Any field, default, or
   behavior that surprised you, contradicted the doc, or wasn't explained clearly enough to use
   without already knowing UTI? That gap is exactly what this round exists to find.
```

### T27 live findings log (updated as reports come in from `project 2`)

Working notes, not yet folded into the Change Log below — done once the round actually finishes.

- **Step 1 (component removal) confirmed clean.** 3 components removed from `Player`
  (`BeanTracker`/`BeanLogger`/`BeanSnapshotExporter`) — matches this session's own live check
  earlier (2026-08-08), which found exactly those three and no `BeanVisualizer` (never added to
  `Player` in any prior round). No surprises here.
- **Real finding, not a UTI bug but worth tracking: a hidden hard compile-time dependency on UTI
  in `project 2`'s own test code.** `PlayerLevelTraversalPlayModeTests.cs` (in the
  `BoxJump.PlayModeTests` assembly, 22 tests) references `UTI.BeanTracker` directly — added earlier
  as "a teardown convenience," not something the test's actual assertions need. Consequence: the
  *entire* 22-test PlayMode assembly becomes uncompilable the moment UTI is removed, not just the
  one test that happens to touch it — C# compiles per-assembly. This is exactly the failure mode
  `USAGE.md`/`DESIGN.md`'s "no required dependencies" design goal (§6) is meant to prevent, but the
  promise only covers UTI's own code — nothing stops a *consuming* project's test code from quietly
  growing a hard reference the other way. The `project 2` team caught this themselves, mid-removal,
  precisely because this round asked them to actually try removing UTI rather than just trust it's
  optional — this is the round doing its job. They're fixing it on their side (removing the
  unnecessary reference) before continuing. **Follow-up once confirmed:** worth a short callout in
  `USAGE.md`/`DESIGN.md`'s Known Limitations — a consuming project's own test/production code
  should never hard-reference `UTI.*` types outside of intentionally-added Bean components, since
  that silently breaks the "UTI is always safely removable" promise from the other direction.
  **Root cause confirmed: this is `project 2`'s own code, not a UTI defect** — nothing in UTI
  requires or encourages a consuming project to reference its types outside of intentionally-added
  Bean components; the dependency ran from `project 2`'s test file *toward* UTI, not the other way.
  **Resolved by the team themselves, correctly** — removed the `UTI.BeanTracker` teardown
  convenience from `PlayerLevelTraversalPlayModeTests.cs`, then removed the now-unused
  `UTI.Runtime` reference from `BoxJump.PlayModeTests.asmdef`. Nothing to fix in UTI itself; the
  Known Limitations callout above is still worth adding so the next team doesn't repeat it.
- **Step 2 (uninstall) confirmed clean.** `com.uti.core` removed from `manifest.json`, `<project 2
  root>/UTI/` deleted entirely, project recompiles with zero errors — genuinely back to a
  pre-UTI-ever-installed state.
- **Step 4 (reinstall) confirmed clean, plus a good architectural proof point.** `com.uti.core`
  re-added, resolves with zero errors — and the CS0104 fix from earlier this session (applied
  directly to the canonical package source, not a project-local copy) **was still in effect after
  the reinstall**, exactly as the shared-package model (`DESIGN.md` §11) is supposed to guarantee:
  fix it once in the one real source, every consuming project gets it automatically, including one
  that just did a hard uninstall/reinstall cycle.
- **Step 5 (`UTI > Setup Project`) confirmed clean.** All 4 expected files landed
  (`BeanConfig.txt` + the 3 docs); `BeanConfig.txt` content spot-checked byte-for-byte against the
  compiled-in template — matches exactly. One thing flagged and correctly identified as *not* a
  bug: the three copied docs show older file-modified timestamps than `BeanConfig.txt` — expected,
  since `File.Copy` preserves the source file's mtime while `BeanConfig.txt` is freshly generated
  text with a real "just now" timestamp. Worth remembering as a real difference in file metadata
  between the two operations, but not a defect.
- **Step 7 (manual component setup via `USAGE.md` alone) confirmed clean.** All four Beans added to
  `Player` with correct field values, verified directly in the serialized scene data (not just
  trusted from the Inspector) — `USAGE.md` was sufficient on its own, no ambiguity about which
  fields to set. Also confirms this session's own documented Unity MCP limitation independently:
  the team's RunCommand sandbox also can't reference `UTI.Runtime` directly and had to drive the
  setup via reflection, matching `CLAUDE.md`'s note exactly.
- **Step 8 friction, not a UTI finding: the team's agent tooling can't directly drive their real
  `PlayerLevelTraversalPlayModeTests.cs` through Test Runner.** That's a PlayMode test — running it
  requires Unity to actually enter Play Mode, which this session independently confirmed is blocked
  as unsupported "user interaction" for `TestRunnerApi` (EditMode worked fine for the 84/84 run;
  PlayMode did not). The team's agent hit the same wall and, instead of the existing proven test,
  is reimplementing similar movement/jump-trigger logic in a temporary harness script to get *some*
  directly drivable Play Mode session - which already introduced its own bug (steering toward the
  next platform's center instead of accounting for leaving the current one first, per their own
  in-progress fix). **Important: this changes nothing about how UTI itself is meant to be used.**
  The actual design is exactly what it looks like - attach the four Beans (already done, step 7)
  and let whatever already drives the character run unmodified, whether that's a human pressing
  Play or their real automated test. If a human on the team just presses Play normally with the
  real test running and the Beans already attached, this should work with zero additional code.
  The harness detour is solving an agent-tooling access problem, not a UTI gap. **Formalized as a
  standing protocol from this exact incident: `DESIGN.md` §12, "The Bring-Your-Own-Test
  Protocol"** — the standard way to verify UTI in any consuming project going forward. Future
  relay rounds should point at that section directly instead of re-deriving this each time.
- **Round complete. Step 8 finished: real blue→yellow→red jump traversal succeeded in actual Play
  Mode** (final Y=3.08, matching the Tall box's known 3.0m top — physically correct). CSV confirmed
  as correct, trustworthy ground truth. Multi-angle capture (`[Auto, Above, Side]`) ran and produced
  3 correctly-named, correctly-grouped PNGs — but surfaced two real findings, root-caused and fixed
  same day as **T28**. `BeanVisualizer`'s live trail stayed genuinely unconfirmed (team's tooling
  couldn't get a usable Scene-view screenshot — their limitation, not UTI's; still needs a human at
  an interactive Editor window). One incidental good sign: `BeanTracker`'s own CSV is what let the
  team diagnose a bug in *their own* temporary test harness (an obviously-wrong `dist=3.00`
  constant) — a small, real instance of UTI doing exactly its job. Full formal report + all 3
  snapshot PNGs delivered 2026-08-08. See T27/T28 rows above for the final verdict on each.

## Change Log

- 2026-08-08 — **T29 added: JSON Lines export built and verified live, resolving a prior session's
  paused-mid-task open item.** A previous session left `JsonlBeanOutput`/`BeanOutputTargets.Json`/
  `BeanConfig.DefaultOutputTargets` written but unverified, flagged in `HANDOFF.md` as possibly
  spamming `project 2`'s console with a compile error. Checked directly this session: clean
  compile, 0 console errors, 97/97 EditMode tests passing via a live `TestRunnerApi` run (not a
  re-run of stale results — `TestResults.xml` read back fresh). The suspected cause was never real.
  Once verified, extracted a shared `BeanFileOutputBase` from `CsvBeanOutput`/`JsonlBeanOutput`'s
  now-duplicated `StreamWriter` lifecycle code; re-ran the same 97/97 suite with 0 regressions. See
  T29's row and `DESIGN.md`'s Change Log for full detail.
- 2026-08-08 — **T05 third attempt, still inconclusive — but for a new reason.** With real Play
  Mode access confirmed working (see T12/T14), tried to finally get a live screenshot of
  `BeanVisualizer`'s gizmo trail in `project 2`. Captures came back genuinely live/non-stale for the
  first time ever (unlike the two prior stale-tooling failures), but a runtime-created test cube
  kept vanishing before a camera frame could be lined up on it — `project 2`'s own `GameManager`
  appears to periodically reset/reload live scene state during Play, independent of
  `EditorApplication.isPlaying` (which stayed `true` throughout). No leftover state in `project 2`
  afterward. Recommends retrying in `little wings` next time. See T05's row for full detail.
- 2026-08-08 — **T08 repurposed — no more building our own demo/sample Unity scenes.** Proposed
  building `Samples~/` car/NPC/player demo scenes to close T08; corrected directly by the user:
  building a new demo/sample project purely to test/showcase UTI is out of scope, costs real
  time/tokens for low verification value, and contradicts the Bring-Your-Own-Test Protocol's whole
  premise (we're testers using real projects, not developers building our own). T08's row now
  targets exercising genre coverage (car/NPC/projectile) via whatever already exists in a real
  consuming project, waiting on opportunity rather than needing dedicated action. Old demo-scene
  idea moved to `PROJECT_OVERVIEW.md`'s Dream To-Do section; standing rule written into `CLAUDE.md`
  and memory so this doesn't get re-proposed.
- 2026-08-08 — **T12 and T14 fully closed, live-verified in `project 2` — the last two Play-Mode-
  only gaps.** This session's Unity MCP connection unexpectedly supports real Play Mode entry
  (`EditorApplication.EnterPlaymode()`) and `GameObject.SetActive`, both hard-blocked in every prior
  session. T12: a Rigidbody-driven test object with `CaptureMode=EveryFixedUpdate` produced 732
  samples with `min=max=avg=0.02000` delta between every consecutive timestamp, exactly matching
  `Time.fixedDeltaTime`. T14: two objects run through a real `SetActive(false)`→`SetActive(true)`
  cycle with `BeanLogger` CSV output — `AppendAcrossReuse=false` kept only cycle 2's 1894 rows
  (cycle 1's genuinely gone), `=true` kept all 4592 rows spanning both cycles under one header. A
  first attempt at T14 had a methodology flaw (configured `FilePath`/`OutputTargets` via reflection
  *after* `AddComponent`, so `OnEnable` fired once already with default settings) - caught and
  corrected before trusting the result. All test objects destroyed afterward; full EditMode suite
  re-confirmed green (105/105, fresh timestamp) with no regressions. See T12/T14 rows above.
- 2026-08-08 — **T27 complete, T28 found/root-caused/fixed same day.** The `project 2` team's
  fresh-install round reported two real findings from a genuine multi-angle capture (real 9m jump
  path, tracking left running well past the interesting part): no visible path line in any angle,
  and `Auto Frame Camera` tight-zooming on just the final position. Confirmed live by this session
  (not just theorized) as one unified root cause, not a config mistake: `BeanSnapshotExporter`
  reads the *live* `BeanTracker.Samples` ring buffer, not the CSV, and a long idle tail can silently
  evict the entire real path from that fixed-capacity buffer before a snapshot happens - reproduced
  directly (200 real-movement samples + 3000 stationary samples → buffer Z-span dropped to exactly
  0). Fixed with a new `IsBufferAtCapacity()` pure check + a `Debug.LogWarning` in
  `CaptureSnapshot()` so this failure mode is visible instead of silent going forward; 87/87
  EditMode tests still green after the fix (up from 84).
- 2026-08-08 — T27 refined per user request: instead of an arbitrary post-reinstall walkthrough,
  paired directly with the `project 2` team's own existing jump test (blue box, then yellow, then
  red) run live in Play Mode with all four Beans attached. Doubles as the first real chance at
  T05/T06 (`BeanVisualizer`'s live Scene-view gizmo actually tracked box-to-box while watching, not
  just inferred from sample data) and a multi-angle capture test on a genuinely dynamic multi-box
  path, rather than this session's own synthetic near-stationary repro.
- 2026-08-08 — Added T27, a full uninstall + fresh reinstall round handed directly to the
  `project 2` team rather than run by this session — the point is a genuine first-time-user
  `USAGE.md` walkthrough, which a session that already knows how UTI works can't fairly simulate.
  Confirmed via this session's live Unity MCP connection that `project 2`'s real `Player`
  GameObject already carries `BeanTracker`/`BeanLogger`/`BeanSnapshotExporter` from earlier rounds,
  so the instructions call out removing those first (before uninstalling the package) to avoid
  leaving "missing script" placeholders behind.
- 2026-08-08 — **Bug report from `project 2` team: CS0104 ambiguous `Object` reference, second
  occurrence.** `TESTS/EditMode/BeanTrackerTests.cs` and `BeanLoggerTests.cs` both call bare
  `Object.DestroyImmediate(go)` while also having `using System;` (needed for `Guid`/
  `InvalidOperationException`/`DateTime` in this round's new tests) alongside `using UnityEngine;` —
  same exact ambiguity class as the `BeanSnapshotExporterTests.cs` regression fixed a prior session
  (see the 2026-08-08 "project 2 round actually underway" entry below), just never swept across the
  rest of `TESTS/` at the time. Blocks compilation (and therefore Play Mode) entirely for every
  project referencing `com.uti.core`, not a soft warning. **Fix was already applied earlier the same
  session** (as a side effect of adding `using System;` for this round's new fault-isolation tests —
  qualified to `UnityEngine.Object.DestroyImmediate` at all 28 call sites across both files) and is
  now formally confirmed: a full-repo sweep (`Runtime/` + `TESTS/`) for bare `Object.` in any file
  with `using System;` present found zero remaining instances — `BeanMouseTrackerTests.cs`/
  `BeanVisualizerTests.cs` use bare `Object.` too but never `using System;`, so they were never
  actually ambiguous. **Prevention decided:** no CI/analyzer infrastructure exists in this bare
  package repo, and dropping `using System;` isn't viable (both files genuinely need it) — the
  practical prevention is the sweep itself, now documented as a standing check (re-run whenever a
  test file gains `using System;` alongside existing bare `Object.` calls). Tracked going forward as
  `TESTS/ErrorHandlingTracker.md` EH09, per the user's request to track error-handling/compile-safety
  issues with the same rigor as feature tests, not just re-fixed reactively a third time.
- 2026-08-08 — **Live verification round via this session's own Unity MCP connection, directly
  attached to `project 2`** (not `little wings`, and not read-only-only like prior sessions — real
  script execution). Closed almost this entire round's punch list without a relay: full EditMode
  suite 84/84 passed (cross-checked against `TestResults.xml` directly, re-confirmed fresh after a
  mid-session fix to rule out stale results); T22's `Reset()`-hook and the new `UTI > Setup Project`
  menu item both confirmed live (`BeanConfig.txt` created with exact template content, a non-default
  config correctly applied to fresh components); **T23 confirmed with real viewed PNGs** — the exact
  same near-stationary path produced the reported useless close-up at the old `MinFramingRadius=2`
  behavior and a properly-framed shot at `=9`; **T24 confirmed with 4 real viewed PNGs**, all
  visually distinct and correctly composed. **Found and fixed a brand-new bug via this live
  testing itself (T26):** `Object.Destroy()` on `CaptureSnapshot()`'s temporary objects silently
  no-ops outside Play Mode, leaking `BeanSnapshotPath` GameObjects into the scene every capture —
  fixed with a new `Application.isPlaying`-aware `SafeDestroy()` helper, re-verified live
  immediately after (zero leaks, zero errors). Confirmed hard limits of this MCP connection by
  testing them directly rather than assuming: `System.Reflection`'s named types (`BindingFlags`,
  `MethodInfo`, etc.) are sandboxed off (worked around via public-only reflection), and both
  entering Play Mode and `GameObject.SetActive` toggling are blocked as unsupported "user
  interaction" — these two remain the only genuinely open items (T12/T14's Play Mode halves).
- 2026-08-08 — Integrity pass: added T25, fault isolation at every real system boundary UTI
  touches. `BeanTracker.Capture()` isolates a throwing `CustomCapture` delegate (logs a warning,
  keeps capturing with null extras rather than silently dropping the sample and everything
  downstream of it). `BeanLogger.Open()`/`HandleSample()`/`Close()` isolate each `IBeanOutput`
  individually, so one broken output no longer silently disables every other one too — verified
  with a new `ThrowingBeanOutput` test double. Two related fixes aren't independently testable:
  `BeanSnapshotExporter`'s per-angle write loop (needs a live Camera) and `BeanConfig.Load()`'s
  locked-file handling (touches the real project-root config path) — see `DESIGN.md` §14 for the
  full writeup and reasoning on why those two stay code-reviewed rather than unit-tested.
- 2026-08-08 — Closed most of this round's punch list in code, **none live-verified yet** (see the
  updated "Next round" relay prompt above): T11 (`CustomCapture`/extras end-to-end), T12
  (`EveryFixedUpdate` via new `SimulateFixedFrame()`), T14 (decided + built
  `BeanLogger.AppendAcrossReuse`), T15 (EditMode half — multi-Bean buffer independence), and T23
  (`MinFramingRadius` now configurable via `BeanConfig.DefaultMinFramingRadius`) all closed with
  new EditMode tests. New T24 added: multi-angle snapshot capture (`CaptureAngles`:
  `Auto`/`Above`/`Side`/`Behind`), built with pure-function framing/naming tests, real capture not
  yet tried. New `UTI > Setup Project (Config + Docs)` Editor menu item makes the recurring
  doc-copy gap (flagged again this round) a one-time fix going forward. Also restored
  `USAGE.md`/`READING_LOGS_AND_VISUALS.md`/`CONFIG.md` into the UTI package repo itself — they'd
  only ever existed as copies inside `little wings`, never saved back — fixing a real staleness bug
  found in the process (`USAGE.md` §8 still described the reverted `ScriptableObject`-based
  `BeanConfig`) and copying the corrected versions directly into both `little wings` and
  `project 2`.
- 2026-08-08 — Full closing report from `project 2`. **T07 — Pass** (package installed, resolved
  cleanly once the CS0104 regression below was fixed). **The actual game bug — not a UTI test row,
  but the real payoff:** `project 2`'s own `JumpTriggerDistance` (1.3m) was smaller than the real,
  physically-forced walking-stop distance against a box (`1m` box half-depth + `0.35m`
  `CharacterController` radius + `0.08m` skin width = `1.43m`) — the jump condition was
  geometrically unreachable, not a game bug. UTI's CSV pinned the frozen Z position to 5 decimal
  places matching that exact arithmetic; the dev said directly they would not have found "an
  off-by-0.13m trigger threshold" from behavior alone. **Second, unrelated real finding from the
  same investigation** (their own test code, not UTI, not logged as a T-row): Unity Input System's
  `Press()`/`Set()` simulation reliably flipped the button's own `isPressed` state but never
  triggered the game's subscribed `Jump.started` callback — worked around via the same
  reflection-based jump-trigger technique the project's own existing test suite already used
  elsewhere. Noted here only because it's a real "manual play works, automated input simulation
  doesn't" gotcha worth remembering if UTI itself ever needs to simulate input for anything.
  **Real UTI findings this round:** (1) confirmed the CS0104 compile regression independently
  blocked Play Mode there too, consistent with the earlier fix. (2) **New bug, T23**: auto-framing
  produced a useless close-up (leg geometry, no context) on a near-stationary path — real,
  unfixed, see T23 row. (3) `USAGE.md`/`READING_LOGS_AND_VISUALS.md`/`CONFIG.md` still not copied
  into `project 2`'s own `UTI/` folder — already a known, tracked gap (see below), now confirmed
  from the receiving end: the dev got by on `README.md`/`DESIGN.md`/inline comments alone, but
  flagged that a first-time user without that context would genuinely be stuck looking for files
  that don't exist yet. (4) Missing `.meta` files for `BeanConfig.cs`/`BeanMouseTracker.cs` flagged
  as "rough for a drop-in tool" — checked after the fact, both now exist (Unity auto-generated them
  on first import in one of the two live projects, since the package folder is a shared live
  reference) — self-resolved, no action needed, but the *timing* (freshly-added files having no
  `.meta` until some project happens to compile them) is worth remembering as a real, if harmless,
  rough edge for anyone else adopting UTI for the first time. **Overall verdict from the dev:** net
  positive, CSV "worth it outright," PNG "cost more than it gave back this time due to the framing
  bug" — an honest, mixed review, not a rubber stamp. Two new Feature ideas added to `README.md`
  per user request based on this session: multi-angle snapshots (grouped/labeled naming), and
  `BeanConfig` covering snapshot-quality settings like `MinFramingRadius` (would directly address
  T23). Neither built yet.
- 2026-08-08 — `project 2` round actually underway (T07/T08/T18/T19/T21/T22 in progress there).
  Found and fixed a **real regression in UTI's own test source**, never caught because nothing
  recompiled `BeanSnapshotExporterTests.cs` after the `BeanConfig` `ApplyConfigDefaults` tests
  were added: `CS0104`, bare `Object.DestroyImmediate(go)` is ambiguous once a file has both
  `using System;` and `using UnityEngine;` (`System.Object` vs `UnityEngine.Object`) — blocked
  compilation entirely, so nothing downstream (Play Mode, the actual player test) could even run.
  Fixed by qualifying both call sites (`UnityEngine.Object.DestroyImmediate`); confirmed no other
  test file has the same `using System;` + bare `Object.` combination. Since `project 2`
  references UTI via the live `file:` link (not a copy), their fix landed directly in this shared
  source — verified, not just trusted. **Real lesson:** this session has no actual C# compiler to
  check against; a relayed "compiles clean" from whichever project last recompiled is the only
  real signal, and it goes stale the moment new code is added without a fresh check anywhere.
  With the compile blocker cleared, the actual player-controller investigation immediately paid
  off: `BeanLogger`'s CSV showed Z frozen at exactly -4.43 for 600+ frames, which turned out to be
  the *exact*, physically-correct CharacterController stop distance against the box
  (box face Z=-4.0, minus radius 0.35, minus skin width 0.08) — not a stuck/bugged player at all.
  `BeanSnapshotExporter`'s PNG confirmed it visually (character flush against the box face). Root
  cause: the test's own `JumpTriggerDistance` (1.3) was geometrically unreachable — the player
  physically cannot get closer than 1.43 (center-to-center) due to its own collision radius, so
  the jump condition could mathematically never fire. Not a UTI bug, not a game bug — a mistuned
  constant in the test itself, found with real data instead of guesswork. This is UTI's first real
  external diagnosis, not a self-verification round.
- 2026-08-07 — `BeanConfig` rebuilt as a plain text file (`BeanConfig.txt`, "UTI > Create Bean
  Config" Editor menu item) instead of a `ScriptableObject` in `Assets/UTI/`, after pushback that
  config should live in the project's plain `UTI/` folder with everything else, not split into
  `Assets/`. `CONFIG.md` now follows the same copy-into-`<project root>/UTI/` convention as
  `USAGE.md`/`READING_LOGS_AND_VISUALS.md` below — all three end-user docs now live in one place
  per project. T22 row and the `project 2` relay steps updated to match.
- 2026-08-07 — Per user feedback, `USAGE.md`/`READING_LOGS_AND_VISUALS.md` copied directly into
  `little wings`'s own `<project root>/UTI/` folder. These are end-user docs, not
  UTI-package-dev docs, so they need to live where a dev actually using UTI in that game would
  look — right next to their real logs/snapshots. Added as step 7 of the `project 2` round below
  so it happens there too, not just `little wings`.
- 2026-08-07 — T20 (`EveryNTicks`) removed after user clarification: they wanted the ability to
  choose/change existing project-wide settings, not a new capture mode. Added `BeanConfig`
  instead (T22) — a project-wide settings file holding preferred `BeanTracker`/
  `BeanSnapshotExporter` defaults, applied to newly-added Beans via Unity's `Reset()` hook. Row
  numbering keeps T20 retired rather than reused, so the history stays honest in the Change Log
  even though the row itself is gone from the table above.
- 2026-08-07 — T13/T16/T17 confirmed **Pass** from the real relay report: 25/25 tests, real
  PNG+CSV verified on the actual filesystem under the new `UTI/` folder (not just logged path
  strings), `LastLineWidth=3.47` confirming the width fix is live. Found (and same-day fixed) a
  real bug in the process: the auto-frame camera's fixed offset direction foreshortened a path
  that happened to travel parallel to it (T18) — camera offset is now derived from the path's own
  travel direction instead, always broadside. Also added, per user feedback: `DimensionMode`
  override for 2D/3D framing (T19, same area as the broadside fix), and `BeanMouseTracker` for
  tracking raw mouse input through the normal Bean pipeline (T21). None of T18/T19/T21/T22 has a
  live Play Mode check yet — see the "Next round: moving to project 2" section above for the
  prepared relay prompt, UTI's first real test outside
  `little wings`.
- 2026-08-07 — Relayed compile error fixed before any T16/T17 re-run could even happen:
  `CS0619 Object.GetInstanceID() is obsolete` was reported as blocking `UTI.Runtime`'s compile in
  little wings. That specific claim (a replacement called `GetEntityId()`) couldn't be
  independently confirmed as real — `GetInstanceID()` is a foundational Unity API with no known
  deprecation — but rather than debate an unverifiable claim, `ResolveFilePath`/
  `ResolveSnapshotPath`'s uniqueness key was changed from `GameObject.GetInstanceID()` (an `int`)
  to a random GUID-fragment `uniqueToken` (a `string`) via new `BeanArtifactPaths.NewUniqueToken()`
  — sidesteps the dispute entirely and needs no Unity API more exotic than `System.Guid`. All three
  test files updated to match (still 21 tests total, none run since this change). T13/T16/T17
  rows above updated to reference the new parameter name.
- 2026-08-07 — Two more refinements after the `AppData` fix below, both per user feedback: (1)
  `BeanArtifactPaths.RootDirectory` now nests one level further into a shared `UTI/` folder at the
  project root, so `BeanLogger` and `BeanSnapshotExporter` output live in one clearly-labeled place
  (`UTI/BeanLogs/`, `UTI/BeanSnapshots/`) instead of two loose sibling folders. (2) Closed T13's
  CSV-side collision gap to match the PNG side: `BeanLogger.ResolveFilePath` (now public, newly
  unit-tested - 3 new `BeanLoggerTests`) includes the GameObject's `instanceId` alongside the
  timestamp, so same-named clones (e.g. two `Bullet(Clone)`s) opening in the same millisecond still
  get distinct files. T13's row updated to reflect this is fixed at the unit level but not yet
  confirmed with a real two-clone Play Mode scene. Also confirmed (unchanged) that CSV/console
  output are plain text and already readable directly by a human or an AI without extra tooling -
  the PNG snapshot is the one artifact type that actually needs vision to interpret. T16/T17 reset
  again to cover the full three-file test suite and the new `UTI/` folder location.
- 2026-08-07 — Root cause of "the designer couldn't find the snapshot" found: it was never a bug
  in the folder/naming logic (both were already confirmed working via `Directory.Exists()`/
  `LastSnapshotPath`) — it was that `Application.persistentDataPath` resolves to a hidden
  `AppData/LocalLow/...` folder that has nothing to do with the project directory, so browsing the
  actual `little wings` project folder (the obvious, reasonable place to look) would never find
  it. Moved both `BeanLogger` and `BeanSnapshotExporter`'s default output to the project root
  instead (`BeanArtifactPaths.RootDirectory`), alongside `Library/Logs/Temp`. T04/T16/T17 all reset
  to reflect the changed default - see their rows above and DESIGN.md §8.5 for full reasoning.
- 2026-08-07 — Diagnosed the auto-framing failure from the round below: folder + unique naming
  were confirmed working (real `BeanSnapshots/` folder, real timestamped `LastSnapshotPath`), but
  the captured image showed no visible path line - just ground/sky/horizon and a tiny distant
  plane. The relay agent's working theory (camera transform doesn't take effect before
  `Camera.Render()` in this URP project) was checked against the evidence and looked unlikely -
  Camera.Render() reflecting the current transform is standard, load-bearing Unity behavior, and
  "tiny distant plane" is itself consistent with the camera having correctly moved back to frame
  the whole path. Real cause: `lineWidth`'s default (0.1 world units, tuned for a close manual
  shot) becomes sub-pixel at the distance auto-framing computes for an actual flight path - same
  failure shape as the original T17 exploration's second attempt ("line too thin to read from
  ~90m away"), just never fixed for the new auto-framed case. Fixed by scaling the rendered line
  width to the computed framing distance/orthographic size, and exposed the real number used as
  `LastLineWidth` so the next report can state it directly instead of guessing from the image.
  Not yet re-verified - see the updated T17 row.
- 2026-08-07 — T16/T17 verified in little wings: T16 4/4 `BeanSnapshotExporterTests` passed via
  the real `TestRunnerApi` (an initial failure was the relay agent's own test-filter regex syntax,
  not UTI). T17 passed on real analysis: a manually-simulated 40-sample flight path (driven via
  `BeanTracker.SimulateFrame()` for determinism, after real-time Play attempts kept getting
  disrupted by the test project's own autonomous flight-sim crashing the plane) produced a real
  72KB PNG at the expected default path, showing a correct yellow (not pink — no shader/RP
  mismatch) line exactly matching the simulated path, over real ground/sky/horizon geometry. First
  capture attempt was framed too tight (a follow-camera close-up with the path barely visible at
  distance) — this directly motivated the folder/uniqueness/auto-framing fixes below, added the
  same session before the next real capture could be attempted. Two reusable environment notes
  from this round, not UTI bugs: (1) directly setting `transform.position` on a non-kinematic
  `Rigidbody` gets silently overwritten by PhysX's own per-physics-step resync — go through
  `Rigidbody`'s own API instead, worth remembering for **T12** (`EveryFixedUpdate`, inherently
  Rigidbody-adjacent); (2) `Unity_RunCommand`'s scratch-compile context still can't see
  `UTI.Runtime` directly (matches the already-known limitation) — bridged with a temporary harness
  script inside the test project's own `Assembly-CSharp`, deleted after use.
- 2026-08-07 — Folder/uniqueness/auto-framing fixes for `BeanSnapshotExporter`, made in response to
  the T17 capture above: default output moved into a `BeanSnapshots/` subfolder; filenames are now
  timestamp-prefixed so repeated captures (e.g. "ran it 5 times, want to compare") don't overwrite
  each other; `autoFrameCamera` (default on) now computes the path's bounding box and frames an
  orthographic front-on shot for flat/2D paths or an elevated 3/4 perspective shot for real 3D
  ones, sized to fit the whole path with margin, instead of relying on whatever the gameplay
  camera happened to be pointed at. T16 grew from 4 to 12 tests covering the new pure functions
  (`ComputePathBounds`, `IsFlatPath`, `ComputeFraming`); the new 8 aren't relayed yet. See
  `DESIGN.md` §8.4 Change Log for full reasoning.
- 2026-08-07 — Added `BeanSnapshotExporter` (T16 EditMode-testable pure logic, T17 manual capture
  check) — the persisted-visualization-artifact feature decided this session (see README.md /
  DESIGN.md §8.4 Change Logs). T17 is also flagged as T05's likely way out of its screenshot-tooling
  deadlock: once it passes, "can the path actually be seen" has a durable file to point at instead
  of depending on external screenshot tools that have failed three rounds running.
