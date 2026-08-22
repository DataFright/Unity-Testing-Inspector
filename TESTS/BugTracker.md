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
| BUG-05 | `BeanMouseTracker` | Code | Throws `InvalidOperationException` every frame in any project with Active Input Handling set to "Input System Package (New)" only (no legacy Input Manager) — an unconditional call into the legacy `Input` class. A common, modern project config, not an edge case. | **Fixed (partial) 2026-08-20** — `Update()` now guards the legacy call behind Unity's own `ENABLE_LEGACY_INPUT_MANAGER` compile symbol; when legacy input isn't available it logs one `Debug.LogWarning` and holds position instead of throwing every frame. Zero new package dependency (that symbol is Unity-engine-defined, not tied to the Input System package being installed). **Not done:** the "ideally reads `Mouse.current`" stretch goal — would need an asmdef `Unity.InputSystem` reference this session couldn't live-verify (no Unity MCP connection this session; see `HANDOFF.md`). **Verified live 2026-08-21 in `little wings`** (`activeInputHandler: 1` = New Input System only, confirmed by reading `ProjectSettings/ProjectSettings.asset` directly — exactly the config that used to throw). Ran `TESTS/PlayMode/BeanMouseTrackerPlayModeTests.cs` for real via `TestRunnerApi` (Unity MCP script execution, real Play Mode entry). **Caveat on how this was confirmed:** the NUnit result callback (`ICallbacks.RunFinished`) never fired back to the calling script — Play Mode entry triggers a domain reload that unloads the ephemeral script holding the callback registration, so no structured pass/fail count was captured (a real limitation of this MCP bridge, not of the tests themselves; matches an earlier session's identical finding for PlayMode specifically). Confirmed instead via direct console evidence: Play Mode cleanly entered then exited on its own (test run completed, Editor not stuck), zero errors logged, and exactly two `BeanMouseTracker`-sourced warnings appeared — one per test's own component instance, each firing exactly once (correct dedup, not spam), stack trace pointing at the exact fixed line (`BeanMouseTracker.cs:49`). That's the expected signature of both tests passing: `Update_NeverThrows_...` produces no error and a single warning from its own instance; `Update_WithoutLegacyInputAvailable_WarnsExactlyOnceAndHoldsScreenOrigin` explicitly requires exactly one warning per instance. **Still not exercised:** the legacy/Both-mode branch (`ENABLE_LEGACY_INPUT_MANAGER` defined) — `little wings` doesn't have that config, so `Update_WithLegacyInputAvailable_...` never compiled in this run. Would need a project set to Input Manager (Old) or Both to cover that half. **Not attempted:** the `Mouse.current`/Input System stretch goal — still not started. | 2026-08-09 (found), 2026-08-20 (fix + tests written), 2026-08-21 (verified live, New-Input-System branch only) |
| BUG-06 | `BeanLogger.OnEnable()` | Code / Docs | Locks in whatever `OutputTargets` was set at `AddComponent()` time — changing the field afterward silently does nothing until a manual `Close()`+`Open()`. Genuinely undocumented; `USAGE.md`'s scripted-usage guidance doesn't mention it, so anyone scripting Bean setup (a test harness, a build tool) can silently get the wrong output targets. | **Open.** Confirmed 2026-08-09 while `bitshot` scripted Bean setup for their eval tests. **Suggested fix, not yet applied:** document the gotcha at minimum; consider having the `OutputTargets` setter re-open automatically as the more ergonomic real fix. See `TestTracker.md`'s T30 live findings log. | 2026-08-09 |
| BUG-07 | `README.md`, `USAGE.md`, T30-style relay prompts | Docs | The `testables` manifest entry is presented as a standard, near-mandatory install step ("isn't optional if you want UTI's own tests to show up..."), when it's really a rare/CI-only mechanism — the one place it's genuinely required is UTI's own `CI~/Packages/manifest.json`. This framing directly pulled `bitshot`'s first real effort into re-running UTI's own 102-test internal suite (redundant — same tests already known green in this repo) instead of real-gameplay verification, the actual point of a Bring-Your-Own-Test round. | **Open, deferred per direct user instruction — note for later, don't fix yet.** Fix: reword both docs' `testables` sections to mark it clearly optional/advanced, and stop repeating the same framing in future relay prompts (this round's own prompt did too). See `TestTracker.md`'s T30 live findings log. | 2026-08-09 |
| BUG-08 | `USAGE.md` | Docs | Consuming a package from a test assembly (e.g. a `*.PlayMode.asmdef`) needs its own explicit `asmdef` reference to `UTI.Runtime` — expected Unity behavior, but not documented anywhere; only the unrelated `testables` entry (for running UTI's *own* tests) is covered. Confirmed twice now by two independent teams hitting the same friction. | **Open, low priority.** Suggested fix: one-line callout in `USAGE.md` for anyone wiring Beans into their own test code. | 2026-08-08 (first hit), reconfirmed 2026-08-09 |

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
