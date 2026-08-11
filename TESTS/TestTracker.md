# Test Tracker — UTI

Tests are added whenever a new feature lands, one row per capability. "Test Project" records which
of the three test beds (`little wings`, `project 2`, `2d project 3`) a check actually ran in.
"Status" reflects the last time it was actually run — never marked Pass/Fail from reading the code
alone. **Full investigation narrative, completed relay prompts, and the complete Change Log live in
[TestTracker_HISTORY.md](./TestTracker_HISTORY.md)** (gitignored, local-only) — this file is the
scannable current-status table.

**Unity MCP capability varies session to session — always probe fresh, never trust a prior
session's capability claims.** See `CLAUDE.md` for the current standing note; the full
session-by-session capability log is in `CLAUDE_HISTORY.md`. Screenshot/Scene-view capture tooling
has been the one consistently weak spot across every session regardless of script-execution
capability — see T05's row.

| ID | Area | Description | Steps | Expected Result | Test Project | Status | Date Added |
|---|---|---|---|---|---|---|---|
| T01 | Core | `BeanSample` + `BeanBuffer` store/retrieve correctly | Run `UTI.Tests.BeanBufferTests` (EditMode) | All 4 tests pass (order preserved, overwrite-when-full, `Clear()`, `Extras` null by default) | little wings | **Pass** | 2026-08-06 |
| T02 | BeanTracker | Capture loop, interval timing, stop, event firing | Run `UTI.Tests.BeanTrackerTests` (4 EditMode tests, via `SimulateFrame()`) | All 4 pass | little wings | **Pass** | 2026-08-06 |
| T03 | BeanLogger | Console output reflects captured samples | Add `BeanLogger` (Console), Play, move the object | One log line per sample, correct position | little wings | **Pass** — 400 lines captured, format matches `ConsoleBeanOutput.Format` exactly, positions track a real flight path | 2026-08-07 |
| T04 | BeanLogger | CSV output writes a valid file | Add `BeanLogger` (CSV), Play, stop, inspect file | CSV exists under `BeanLogs/` with header + one row per sample | little wings | **Pass** — default location later moved to the project root's `UTI/BeanLogs/` folder; writing logic unchanged, not yet re-confirmed at the new location | 2026-08-07 |
| T05 | BeanVisualizer | Scene view shows the recorded path | Add `BeanVisualizer`, Play, view Scene window | Line drawn through captured points, matches actual movement | little wings, project 2, bitshot | **Pass — confirmed for real, 2026-08-09 (attempt 8).** User personally ran `project 2`'s box-jump test and shared a first-hand screenshot: Scene view + Game view side by side in Play Mode, a clear line tracing the actual jump path. Eight attempts total across three projects, one reverted premature Pass, before this one cleared the "real, verified report" bar. One un-root-caused caveat: the trail vanishes quickly, needing more than one try to screenshot (leading suspect: `project 2`'s `GameManager` scene-reset behavior). Full 8-attempt investigation: `TestTracker_HISTORY.md`. | 2026-08-06, resolved 2026-08-09 |
| T06 | BeanVisualizer | Decimation kicks in above `maxPointsToDraw` | Track an object past `maxPointsToDraw` samples | Gizmo still draws without slowdown; path still recognizable | little wings | **Partial** — decimation math confirmed correct (matches T10); "no noticeable slowdown" has strong objective evidence (steady ~330–370fps over a real ~103s run, past the buffer's 1000-sample cap); "path still recognizable" at this specific scale not directly confirmed. | 2026-08-07 |
| T07 | Package | UTI installs as a local package in all three test projects | Add a `"file:"` dependency + `testables` entry in each project's `manifest.json` | Package resolves, compiles, `UTI` namespace usable | little wings, project 2 | **Pass** in both; `2d project 3` still Planned. | 2026-08-06 |
| T08 | Genre coverage | UTI generalizes across genres (car/vehicle, NPC, projectile) using objects that already exist in real projects | Attach Beans whenever a suitable object exists in a real round — never build one for this | Beans attached and visibly tracking/logging/visualizing | little wings, project 2, 2d project 3 | Planned — waits on a suitable object existing in a real round. Building demo scenes for this is explicitly out of scope (see `CLAUDE.md`). | 2026-08-06 |
| T09 | BeanLogger | Wiring, console output, CSV output (EditMode) | Run `UTI.Tests.BeanLoggerTests` (4 tests) | All 4 pass | little wings | **Pass** | 2026-08-06 |
| T10 | BeanVisualizer | Decimation and color-mode math (EditMode, pure functions) | Run `UTI.Tests.BeanVisualizerTests` (6 tests) | All 6 pass | little wings | **Pass** | 2026-08-06 |
| T11 | BeanTracker | `CustomCapture` delegate populates `extras` end-to-end | Run the `BeanTrackerTests`/`BeanLoggerTests` extras cases | `Extras`/CSV column reflect the delegate's output | little wings, project 2 | **Pass** — verified live in `project 2` (84/84 EditMode suite). | 2026-08-06 |
| T12 | BeanTracker | `EveryFixedUpdate` capture mode | `SimulateFixedFrame()` EditMode tests + a live Play Mode check | Captures only while tracking in `EveryFixedUpdate` mode; samples land on the physics tick | little wings, project 2 | **Pass, both halves.** Real Rigidbody-driven object: 732 samples, exactly `Time.fixedDeltaTime` apart. | 2026-08-06 |
| T13 | BeanLogger | CSV doesn't collide when two Beans share a GameObject name | Instantiate two identically-named prefab clones with CSV output | Each writes to its own file | little wings | **Pass** (unit-level + confirmed live via real distinct-token filenames); a literal two-simultaneous-clones scene hasn't been run. | 2026-08-06 |
| T14 | BeanTracker/BeanLogger | Pooled object reuse (`SetActive` cycling) | New `AppendAcrossReuse` EditMode tests + a live `SetActive` cycle check | Default truncates each reopen; `AppendAcrossReuse=true` preserves rows across reuse | little wings, project 2 | **Pass, both halves.** Real 2-cycle `SetActive` test: the `false` file held only cycle 2's rows, the `true` file held all rows across both cycles under one header. | 2026-08-06 |
| T15 | Multi-Bean | Several simultaneously-tracked objects stay independent; gizmo draw stays cheap at scale | New EditMode test + a live Play Mode check at realistic counts | Buffers independent; no Scene-view slowdown | little wings, project 2 | **Partial** — EditMode half Pass (84/84 suite); gizmo-cost-at-scale (a wave of enemies/bullets) still needs a live Play Mode check. | 2026-08-06 |
| T16 | BeanSnapshotExporter/BeanLogger | Pure logic: path extraction, bounds/flatness, framing, unique paths (EditMode) | Run `BeanSnapshotExporterTests` + `BeanArtifactPathsTests` + `BeanLoggerTests` | All pass | little wings | **Pass** — 25/25 across all three files. | 2026-08-07 |
| T17 | BeanSnapshotExporter | Manual capture produces a usable PNG with tracked path + real scene geometry | Add `BeanSnapshotExporter`, track, capture, open the PNG in the project's own `UTI/BeanSnapshots/` | File exists, shows scene + path, correctly framed | little wings | **Pass.** Real capture confirmed on the filesystem; `LastLineWidth` confirmed the width-scaling fix is active. Surfaced T18. | 2026-08-07 |
| T18 | BeanSnapshotExporter | Auto-frame camera shouldn't foreshorten a path parallel to the old fixed offset direction | Track a path close to the old fixed direction, capture, inspect | Renders with visible margin, not foreshortened | little wings | **Fixed, unit-tested.** Not yet re-verified live with a genuinely diagonal *moving* path. | 2026-08-07 |
| T19 | BeanSnapshotExporter | `DimensionMode` override + broadside-framing pure logic (EditMode) | Run the updated `BeanSnapshotExporterTests` (20 tests) | All pass | little wings | Planned — tests written, not yet relayed for live confirmation. | 2026-08-07 |
| T21 | BeanMouseTracker | Screen/world mouse-position resolution (EditMode) | Run `BeanMouseTrackerTests` (3 tests) | All pass | little wings | Planned — pure functions covered; live `Update()`-driven mouse-follow check in Play Mode not yet done. | 2026-08-07 |
| T22 | BeanConfig | `ParseLines`/`ApplyConfigDefaults` on `BeanConfig`/`BeanTracker`/`BeanSnapshotExporter` (EditMode) | Run `BeanConfigTests` + `BeanTrackerTests` + `BeanSnapshotExporterTests` | All pass | little wings, project 2 | **Pass.** EditMode logic, the `Reset()` hook, and the `UTI > Setup Project` menu item all confirmed live. | 2026-08-07 |
| T23 | BeanSnapshotExporter | Auto-framing shouldn't produce an unhelpfully tight shot on a near-stationary path | Raise `MinFramingRadius`, re-run a near-stationary reproduction, capture | Meaningful scene context at a larger radius | project 2 | **Pass — verified live**, not just unit-tested. Same path at `MinFramingRadius=2` vs. `=9`, both PNGs viewed directly. | 2026-08-08 |
| T24 | BeanSnapshotExporter | Multi-angle capture (`Auto`/`Above`/`Side`/`Behind`) pure logic (EditMode) | Run the updated `BeanSnapshotExporterTests` | All pass | little wings, project 2 | **Pass — built and verified live.** 4 distinct PNGs from one call, all viewed directly; `Auto` matched the pre-existing single-angle shot exactly. | 2026-08-08 |
| T25 | BeanTracker/BeanLogger | Fault isolation: a throwing `CustomCapture` delegate, a `BeanLogger` output throwing on `Open`/`Write`/`Close` | Run the fault-isolation cases in `BeanTrackerTests`/`BeanLoggerTests` | Warnings logged, tracking/other outputs keep working | little wings, project 2 | **Pass — built and verified live**, part of the 84/84 EditMode pass; real console warnings inspected directly. | 2026-08-08 |
| T26 | BeanSnapshotExporter | `Object.Destroy()`'s no-op outside Play Mode shouldn't leak temp GameObjects | Call `CaptureSnapshot()` from an Editor context, check for leftovers | No leaked objects regardless of Play Mode state | project 2 | **Pass — found and fixed live.** `SafeDestroy()` helper added; re-verified live with zero leaks. | 2026-08-08 |
| T27 | Package/Install, BeanVisualizer, BeanSnapshotExporter | Full uninstall + fresh reinstall, then live during the team's own real jump test | Full protocol (see `TestTracker_HISTORY.md`): uninstall, reinstall, Setup Project, add 4 Beans, run the real test, try multi-angle capture | Clean reinstall; live gizmo line tracks the path; multi-angle capture reads clearly | project 2 | **Pass, mostly.** Uninstall/reinstall clean both directions, `Setup Project` worked exactly as documented, `USAGE.md` alone was sufficient, real Play Mode traversal succeeded with the CSV independently verified as physically correct. `BeanVisualizer`'s live trail stayed unconfirmed (the team's own tooling limitation, not a UTI finding). Multi-angle capture surfaced two real findings — see T28. Full report: `TestTracker_HISTORY.md`. | 2026-08-08 |
| T28 | BeanSnapshotExporter | `CaptureSnapshot()` reads the live ring buffer, not the CSV — a long idle tail can silently evict the real path before a snapshot | Track real movement well past `MaxSamples`, then stay stationary for `MaxSamples`+ more, then capture | Buffer should still reflect the real path, or warn if it can't | project 2 | **Pass — found live, root-caused, fixed same day.** Reproduced directly: 200 real-movement samples + 3000 stationary samples dropped the buffer's Z-span to exactly 0. New `IsBufferAtCapacity()` warning added. Full detail: `TestTracker_HISTORY.md`, `DESIGN_HISTORY.md` §8.4. | 2026-08-08 |
| T29 | BeanLogger | JSON Lines output, `BeanConfig.DefaultOutputTargets`, the `BeanFileOutputBase` refactor | Run the expanded `BeanLoggerTests` + `BeanConfigTests` | All pass | project 2 | **Pass — built and verified live**, both before and after the refactor (97/97 EditMode). | 2026-08-08 |
| T30 | Package/Install, BeanVisualizer, BeanSnapshotExporter | First fully external install: a fourth test project installs from the public GitHub URL and attaches Beans to its own existing gameplay, to independently verify T05/`BeanSnapshotExporter` | See relay prompt in `TestTracker_HISTORY.md` | Package installs cleanly from the GitHub URL; independent report on gizmo visibility and snapshot output | first project | Planned — relay prompt sent, not yet run. **Possibly moot now that T05 resolved via a different path** (the user's own direct testing, 2026-08-09) — worth confirming next session whether it's still worth running. | 2026-08-09 |

## Open items (not yet Pass)

- **T05/T06** — T05 itself is Pass; T06's "path still recognizable at realistic decimation scale"
  half remains Partial (see row above).
- **T08** — waits on a suitable car/NPC/projectile object showing up in a real test-bed round.
- **T19/T21** — pure-logic tests written and green, live Play Mode confirmation not yet done.
- **T30** — relay prompt sent, not yet run; possibly superseded by T05's resolution.

## Manifest snippet for adding UTI to a new test project

```json
{
  "dependencies": {
    "com.uti.core": "file:<path to your local UTI clone>",
    ...
  },
  "testables": [
    "com.uti.core"
  ]
}
```

Save, switch to the Editor, let it recompile. "UTI (Unity Testing Inspector)" should show up under
Window > Package Manager > In Project. Then: **T01** — Window > General > Test Runner > EditMode >
run `UTI.Tests.BeanBufferTests`. **T07** — confirm it resolved with no console errors.

## Change Log

- 2026-08-11 09:41 — **Restructured for a full docs condense pass**: shrunk every table cell to a
  concise status + pointer (T05's cell alone was previously ~2,000 words), moved the "T05
  Investigation Notes" section, the "T30 live findings log," completed relay prompts, and the full
  historical Change Log to `TestTracker_HISTORY.md`. Folded
  `PROJECT2_FRESH_INSTALL_REPORT_2026-08-08.md` into that history file as an appendix (its content
  was already fully redundant with T27's row) and removed the standalone file. Genericized the local
  machine path in the manifest snippet above. See `docs/PROJECT_OVERVIEW_HISTORY.md` for the full
  restructure writeup covering all affected docs.
- 2026-08-09 22:33 — T05 closed — Pass, confirmed for real. See T05's row above.
- 2026-08-09 21:12 — Bug tracking split into `TESTS/BugTracker.md` — this doc answers "does the test
  pass," that one answers "what's actually broken." Backfilled with every confirmed UTI-side bug
  found across this project's history.
- 2026-08-09 21:02 — `bitshot` round (T30-style, independent Bring-Your-Own-Test) complete for now:
  inconclusive on T05 itself (their player never moved, a bug in their own input handling, not UTI),
  but strong new cross-project confirmation of T11/T12/T13/T16/T17-style mechanics regardless.
- 2026-08-09 15:31 — T05: closed the actually-testable half with real, mutation-verified proof —
  `BeanVisualizer.DrawPath()`'s own draw-call logic, via a new injectable `IGizmoDrawer` seam. See
  `DESIGN.md` §8.3.

Full Change Log since day one, and every completed investigation/relay-prompt narrative:
`TestTracker_HISTORY.md`.
