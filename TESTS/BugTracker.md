# Bug Tracker — UTI

Companion to `TestTracker.md`/`ErrorHandlingTracker.md`, but a different job: **this doc tracks
confirmed defects and gaps in UTI itself** — code bugs, and doc/UX gaps that mislead a consumer —
separately from whether a given test case currently passes. `TestTracker.md` answers "does T05
pass"; this answers "what's actually broken, and is it fixed yet." A failing test is *evidence* of
a bug, but the two get tracked in different places from here on.

**Added 2026-08-09, split out of `TestTracker.md`** — bugs had been accumulating as prose inside
test rows and Change Log entries there, which worked for a while but made it hard to answer "what's
still actually open" at a glance without reading the whole history. Older, already-fixed bugs below
keep their original full write-up in `TestTracker.md`/`ErrorHandlingTracker.md`'s own Change Logs
(not duplicated here in full) — this doc adds a short, current-status entry and links back. New
bugs found from here on should be logged here first.

**Scope: only confirmed UTI-side issues** — a bug found *in* a consuming project's own code while
testing UTI (e.g. T27's `project 2` test file hard-referencing `UTI.BeanTracker`) is explicitly
*not* tracked here, per this project's standing rule to confirm root cause before attributing a gap
to UTI. See `TestTracker.md`'s T27 notes for that example in practice.

| ID | Component | Type | Description | Status | Date Found |
|---|---|---|---|---|---|
| BUG-01 | `TESTS/EditMode/BeanSnapshotExporterTests.cs` | Code | Ambiguous bare `Object.` reference (`CS0104`) once the file has both `using System;` and `using UnityEngine;` — blocks compilation for every consuming project | **Fixed** — qualified to `UnityEngine.Object.X` at all call sites. First occurrence; see BUG-02 for the same bug class recurring elsewhere. | 2026-08-07 (exact date approximate — see `TestTracker.md` Change Log for the original entry) |
| BUG-02 | `TESTS/EditMode/BeanTrackerTests.cs`, `BeanLoggerTests.cs` | Code | Same `CS0104` ambiguous `Object.` bug class as BUG-01, recurring in two more files once they added `using System;` for new tests — never swept across the rest of `TESTS/` after the first fix | **Fixed 2026-08-08** — 28 call sites qualified across both files; full-repo sweep confirmed zero remaining bare `Object.` instances in any file with `using System;`. **No CI/analyzer enforcement added** — this is a manual-convention fix, re-check on every new test file that adds `using System;`. See `ErrorHandlingTracker.md` EH09, `TestTracker.md` Change Log (2026-08-08 12:40). | 2026-08-08 |
| BUG-03 | `BeanSnapshotExporter.CaptureSnapshot()` | Code | `Object.Destroy()` on temporary `BeanSnapshotPath` line/texture objects silently no-ops outside Play Mode (Unity API behavior) — calling `CaptureSnapshot()` from an Editor-only context leaked GameObjects into the scene permanently | **Fixed 2026-08-08** — new `SafeDestroy()` helper (`Application.isPlaying ? Destroy() : DestroyImmediate()`), applied at all three call sites. Re-verified live: identical capture afterward produced zero leaks. See `TestTracker.md` T26, `ErrorHandlingTracker.md` EH06. | 2026-08-08 (found live by this session's own testing) |
| BUG-04 | `BeanSnapshotExporter.CaptureSnapshot()` | Code | Reads the live `BeanTracker.Samples` ring buffer, not the CSV — a long idle tail after real movement finished could silently evict the entire interesting path from the buffer before a snapshot happens, with no warning either way | **Fixed 2026-08-08** — new pure `IsBufferAtCapacity(sampleCount, maxSamples)` check plus a `Debug.LogWarning` explaining the likely cause and the fix (raise `Max Samples`, call `StopTracking()` promptly). Makes the failure visible instead of silent; doesn't change framing/rendering itself. See `TestTracker.md` T28, `ErrorHandlingTracker.md` EH10. | 2026-08-08 (found live by the `project 2` team, T27 round) |
| BUG-05 | `BeanMouseTracker` | Code | Throws `InvalidOperationException` every frame in any project with Active Input Handling set to "Input System Package (New)" only (no legacy Input Manager) — an unconditional call into the legacy `Input` class. A common, modern project config, not an edge case. | **Fixed and fully verified — CLOSED 2026-08-21.** `Update()` guards the legacy call behind Unity's own `ENABLE_LEGACY_INPUT_MANAGER` compile symbol: warns once and holds position instead of throwing when legacy input isn't available. Zero new package dependency. Shipped in `v0.2.0`. **Both branches now confirmed live:** (1) New-Input-System-only, verified this session in `little wings` via `TestRunnerApi` PlayMode execution — zero errors, console evidence (two correctly-deduped single-fire warnings) matching both `BeanMouseTrackerPlayModeTests` passing (NUnit's own structured result didn't survive Play Mode's domain reload through the MCP bridge in use that session — see `CLAUDE_HISTORY.md`). (2) Legacy/Both mode, verified by the `little wings` team directly: flipped Active Input Handling to "Both," added a live `BeanMouseTracker`, entered Play, confirmed a clean console **and** read the component's own `warnedLegacyInputUnavailable` field via reflection (`false` — the warning path was never tripped, meaning the legacy `Input.mousePosition` call itself ran clean). Setting reverted afterward, confirmed on disk both ways. **Not done, still open as a real stretch goal:** the "ideally reads `Mouse.current`" Input System path — not started. **Follow-up idea from the `little wings` report, not yet acted on:** if this branch needs routine regression coverage later, the reflection-based Active-Input-Handling toggle they used could be wrapped into a small editor-only test utility inside UTI itself rather than reflected into ad hoc each time — see `TestTracker.md` T21. | 2026-08-09 (found), 2026-08-20 (fix + tests written), 2026-08-21 (both branches verified live, closed) |
| BUG-06 | `BeanLogger.OutputTargets` | Code / Docs | Locks in whatever `OutputTargets` was set at `AddComponent()`/first-`Open()` time — changing the field afterward used to silently do nothing until a manual `Close()`+`Open()`. Genuinely undocumented; `USAGE.md`'s scripted-usage guidance didn't mention it, so anyone scripting Bean setup (a test harness, a build tool) could silently get the wrong output targets. | **Fix applied and shipped in `v0.2.1`; live verification by `little wings` (2026-08-22) caught a real bug in the test, not (as far as confirmed) the fix.** Root cause and fix as described in `v0.2.1`'s Change Log. `little wings` ran the two new EditMode tests: `OutputTargets_SetToSameValue_DoesNotReopen` **passed**; `OutputTargets_ChangedAfterOpen_TakesEffectImmediately` **failed** (`Expected: True, But was: False` on `File.Exists(path)`). Traced the failure to the test itself, not the fix: it set `OutputTargets` (triggering the fix's synchronous auto-reopen) *before* setting `FilePath`, so `BuildActiveOutputs()` ran with `FilePath` still empty and the CSV was created at the default auto-generated path instead of the test's expected `path` — `File.Exists(path)` correctly returned false for a file that was never going to be there. Test reordered so `FilePath` is set before the `OutputTargets` change that triggers the reopen. **Still open pending a second live run** to confirm the reordered test actually passes — the fix's own logic hasn't been disproven, but hasn't been cleanly confirmed either yet. | 2026-08-09 (found), 2026-08-22 (fixed, shipped, live test caught a test bug — re-verification pending) |
| BUG-11 | `docs/DESIGN_HISTORY.md.meta`, `docs/PROJECT_OVERVIEW_HISTORY.md.meta` | Packaging | Both files existed on disk (valid, real GUIDs) but were never committed to git — every other doc's `.meta` file was tracked, only these two were missed, apparently since whenever they were last regenerated (2026-08-15). Caused a real Unity import warning in `little wings` on their `v0.2.1` install: two files in the package's immutable folder lacking `.meta` files, which Unity auto-generates fresh (and differently) for every consuming project instead of using one stable, shared GUID. | **Fixed 2026-08-22.** Found via `little wings`' own diligent reporting of a "benign" warning during their reinstall — confirmed via `git ls-files`/`git status` that every other doc's `.meta` file was tracked and only these two were missing. Both already existed on disk with valid content; just needed to be added to git. | 2026-08-22 |
| BUG-09 | `docs/READING_LOGS_AND_VISUALS.md` | Docs | CSV/JSON Lines output is buffered and only flushed every 32 rows or on `Close()` (`BeanFileOutputBase.cs`, `FlushInterval = 32`) — intentional, commented in source, but never explained to the end user. A file checked while a Bean is still actively running (especially in the first few samples) can appear 0 bytes or truncated, easily misread as data loss or a broken output. | **Fixed 2026-08-22.** Found as a byproduct of the (flawed) BUG-06 repro: `little wings` saw a CSV file created but staying 0 bytes after 2 real samples, which read exactly like a bug until traced to this intentional buffering. Added an explicit callout to `READING_LOGS_AND_VISUALS.md`'s CSV section explaining the flush cadence and that a short/empty file mid-run is expected, not lost data. Doc-only fix, no code change needed — the buffering behavior itself is correct as designed. | 2026-08-22 |
| BUG-10 | `docs/USAGE.md` | Docs | No end-user doc explains that `OnEnable()`-driven behavior (`Open()`, `StartTracking()`, etc.) doesn't run for a Bean component added via script *in the Editor* until Play Mode actually starts (`OnEnable` doesn't fire outside Play Mode without `[ExecuteAlways]`, which UTI's components deliberately don't use — same reasoning as `Reset()` being Editor-only, already documented in source comments but not end-user docs). Manually calling public methods like `Open()`/`Close()` in that Edit-Mode window works, but that state gets wiped and `OnEnable()` fires fresh once Play actually begins, which can look like duplicated/reset behavior (e.g. two output files instead of one) to someone who doesn't know this is happening. | **Fixed 2026-08-22.** Added a "Known constraints" callout in `USAGE.md` explaining Edit-Mode-vs-Play-Mode activation timing for anyone scripting Bean setup, and that this doesn't apply at all if the setup script itself runs during Play Mode. Doc-only fix. | 2026-08-22 |
| BUG-07 | `README.md`, `USAGE.md`, T30-style relay prompts | Docs | The `testables` manifest entry is presented as a standard, near-mandatory install step ("isn't optional if you want UTI's own tests to show up..."), when it's really a rare/CI-only mechanism — the one place it's genuinely required is UTI's own `CI~/Packages/manifest.json`. This framing directly pulled `bitshot`'s first real effort into re-running UTI's own 102-test internal suite (redundant — same tests already known green in this repo) instead of real-gameplay verification, the actual point of a Bring-Your-Own-Test round. | **Open, deferred per direct user instruction — note for later, don't fix yet.** Fix: reword both docs' `testables` sections to mark it clearly optional/advanced, and stop repeating the same framing in future relay prompts (this round's own prompt did too). See `TestTracker.md`'s T30 live findings log. | 2026-08-09 |
| BUG-08 | `USAGE.md` | Docs | Consuming a package from a test assembly (e.g. a `*.PlayMode.asmdef`) needs its own explicit `asmdef` reference to `UTI.Runtime` — expected Unity behavior, but not documented anywhere; only the unrelated `testables` entry (for running UTI's *own* tests) is covered. Confirmed twice now by two independent teams hitting the same friction. | **Fixed 2026-08-22.** Added a "Known constraints" callout in `USAGE.md` for anyone wiring Beans into their own test code. Doc-only fix. | 2026-08-08 (first hit), reconfirmed 2026-08-09, fixed 2026-08-22 |

## Reviewed and confirmed NOT a bug (so it doesn't get re-litigated)

- **`BeanTracker.Reset()` only fires when a component is added via the Editor (Inspector or its
  context menu), never on a runtime `AddComponent()` call.** This is documented, intentional
  behavior in the source comment itself (deliberately not `[ExecuteAlways]`-wired to `OnEnable`) —
  not a bug, even though `bitshot` was in the middle of testing this exact question when their
  session got sidetracked. If a future report calls this a bug, check the source comment first.
- **`BeanSnapshotExporter` defaults to `Camera.main`.** A completely standard, reasonable Unity
  default. `bitshot`'s own camera-lookup convention differs, which is just something to know (set an
  explicit camera reference) — not a UTI defect.
- **A consuming project's own code hard-referencing a UTI type outside of intentionally-added Bean
  components can break when UTI is removed** (found in `project 2`'s own `PlayerLevelTraversal
  PlayModeTests.cs`, T27). Root-caused and fixed entirely on `project 2`'s side — nothing in UTI
  encourages or requires this. Tracked in `TestTracker.md`'s T27 notes as a cautionary example, not
  a UTI bug.

## Change Log

- 2026-08-22 11:41 — `little wings` ran BUG-06's two new EditMode tests against `v0.2.1`: one
  passed, one failed. Traced the failure to the test's own assignment order (`OutputTargets` set
  before `FilePath`, so the fix's synchronous auto-reopen ran before `FilePath` was in place) —
  not evidence the fix itself is broken. Test reordered; re-verification still pending. Also added
  BUG-11: two doc `.meta` files existed on disk but were never committed, causing a real Unity
  import warning on every fresh install — found via `little wings`' own report, fixed same day.
- 2026-08-22 10:36 — **BUG-06 fixed.** `little wings`' Run 3 (components added and `OutputTargets`
  changed entirely during Play Mode) gave a clean, conclusive repro — zero CSV files across 40 real
  samples, proving the output was never built, not just written empty. `OutputTargets`' setter now
  auto-reopens on an actual value change. Two new EditMode tests added, not yet run live (no Unity
  MCP this session). Their Runs 1/2 (files created but empty) turned out to be BUG-09's flush
  batching, a separate already-fixed issue, not this bug — full reasoning in the BUG-06 row.
- 2026-08-22 10:21 — Fixed BUG-08 and BUG-10, per direct user request to clear the low-hanging open
  items — both doc-only "Known constraints" callouts added to `USAGE.md`. Open bugs now down to
  BUG-06 (active investigation) and BUG-07 (deliberately deferred).
- 2026-08-22 09:12 — `little wings`' BUG-06 repro (run per our own flawed instructions — entirely
  in Edit Mode, never actually exercising `OnEnable`) surfaced two real, separate findings: BUG-09
  (CSV/JSON's 32-row flush batching was never explained to end users — fixed, doc-only) and BUG-10
  (Edit-Mode-vs-Play-Mode component-activation timing is undocumented — open, low priority).
  BUG-06 itself confirmed real at the source level but still needs a corrected, runtime-based repro.
- 2026-08-21 20:25 — **BUG-05 CLOSED.** The `little wings` team independently confirmed the
  legacy/Both-mode branch: flipped Active Input Handling to "Both," ran a live `BeanMouseTracker`
  in Play Mode, clean console, and confirmed via reflection that the fallback warning was never
  tripped — the legacy path itself just worked. Combined with this session's New-Input-System-only
  verification, both branches are now confirmed live. See `TestTracker.md` T21 for the full report
  and a forward-looking test-infrastructure idea it surfaced.
- 2026-08-21 11:51 — BUG-05 verified live in `little wings` via Unity MCP script execution
  (real `TestRunnerApi` PlayMode run, not manual click-through): zero errors, exactly two
  correctly-deduped warnings matching both tests' expectations. NUnit's own pass/fail callback
  didn't survive Play Mode's domain reload through this bridge (confirmed console evidence used
  instead — see the row for detail). Legacy/Both-input-mode branch still unexercised; no project
  currently set up for it.
- 2026-08-20 15:31 — Added `TESTS/PlayMode/BeanMouseTrackerPlayModeTests.cs` for BUG-05 so this
  gets verified by an automated Test Runner pass instead of manual click-through. Written, not yet
  run. See `TestTracker.md` T21.
- 2026-08-20 14:54 — BUG-05 partially fixed: `BeanMouseTracker.Update()` now guards the legacy
  `Input.mousePosition` call behind `ENABLE_LEGACY_INPUT_MANAGER` and warns once instead of
  throwing every frame on New-Input-System-only projects. `Mouse.current` support deliberately not
  attempted — no Unity MCP connection this session to verify the asmdef reference compiles. Status
  left as unverified-live pending an Editor check.
- 2026-08-09 21:12 — Doc created, split out of `TestTracker.md` per direct user request ("bugs just
  fall into our test doc and the test should be for tracking test cases, not bugs"). Backfilled
  BUG-01 through BUG-04 from already-fixed issues previously tracked only as prose inside
  `TestTracker.md`'s T26/T28 rows and Change Log (CS0104 x2, leaked GameObjects, ring-buffer
  eviction) so this doc has a complete history, not just a starting point. Added BUG-05 through
  BUG-08 from `bitshot`'s 2026-08-09 evaluation round, previously logged only in `TestTracker.md`'s
  T30 live findings log. Going forward, new bugs get logged here first; `TestTracker.md`/
  `ErrorHandlingTracker.md` keep pointing here instead of restating full bug detail inline.
