# Error Handling Tracker — UTI

Companion to `TestTracker.md`, same spirit: one row per guarded system boundary, "Status" reflects
what's actually been verified — never marked Verified from reading the code alone. See `DESIGN.md`
§14 for the full philosophy and boundary-by-boundary reasoning; this doc is the at-a-glance tracker.

**Philosophy (see `DESIGN.md` §14 in full): guard system boundaries, not internal invariants.**
UTI is a debug/QA tool attached alongside real gameplay — it should never be the reason a playtest
session or a game crashes. A boundary is caller-supplied code, disk/file I/O, or an
externally-edited config file. An internal invariant (a genuine programming error, not a runtime
boundary) is deliberately left to fail loudly — see "Deliberately not guarded" at the bottom.

| ID | Component | Boundary | Guard behavior | Verification | Status | Date Added |
|---|---|---|---|---|---|---|
| EH01 | `BeanTracker.Capture()` | `CustomCapture` delegate — caller-supplied code | Wrapped in try/catch: logs a warning naming the object, captures the sample with `Extras = null` instead of skipping the whole capture (tick/`OnSample` would otherwise never fire for that frame) | `UTI.Tests.BeanTrackerTests.SimulateFrame_CustomCaptureThrows_StillCapturesSampleWithoutExtras` | **Verified live** — EditMode test passing (84/84 suite, `project 2`, 2026-08-08), and the exact expected console warning was inspected directly in a real Unity session, not just asserted by the test | 2026-08-08 |
| EH02 | `BeanLogger.Open()` | Each active `IBeanOutput.Open()` — file/disk I/O (permission denied, invalid path) | Per-output try/catch; a failing output logs a warning naming its type and is dropped from `activeOutputs`, so one broken output can't silently disable every other one (previously a single throw here skipped `isOpen = true`/the `OnSample` subscription entirely) | `UTI.Tests.BeanLoggerTests.Open_OneOutputThrowsOnOpen_OtherOutputsStillOpenAndReceiveSamples` | **Verified live** — same 84/84 pass, real console warning inspected | 2026-08-08 |
| EH03 | `BeanLogger.HandleSample()` | Each active `IBeanOutput.Write()` — file/disk I/O (disk full mid-run) | Per-output try/catch; a failing output logs a warning and is dropped, rather than propagating up through `BeanTracker.Capture()`'s own `OnSample?.Invoke()` and breaking every other subscriber too | `UTI.Tests.BeanLoggerTests.HandleSample_OneOutputThrowsOnWrite_OtherOutputStillReceivesSamplesAndBrokenOneIsDropped` | **Verified live** — same 84/84 pass, real console warning inspected; also confirms the broken output isn't retried (write count stays 1) | 2026-08-08 |
| EH04 | `BeanLogger.Close()` | Each active `IBeanOutput.Close()` — file/disk I/O (final flush hitting a full disk) | Per-output try/catch; a failing `Close()` logs a warning and moves on (nothing left to "drop" — this is teardown) | `UTI.Tests.BeanLoggerTests.Close_OneOutputThrowsOnClose_OtherOutputStillCloses` | **Verified live** — same 84/84 pass, real console warning inspected | 2026-08-08 |
| EH05 | `BeanSnapshotExporter.CaptureSnapshot()` | Per-angle render/encode/write (disk I/O — disk full, permissions) in the multi-angle loop | Wrapped in try/catch/finally per angle: a write failure logs a warning naming the angle and moves on to the next one instead of losing angles that already succeeded; `finally` guarantees the per-angle `Texture2D` is always destroyed | Not EditMode-testable (needs a live Camera) | **Code-reviewed only** — the happy path (all angles succeed) was verified live 2026-08-08 (see T23/T24 in `TESTS/TestTracker.md`), but the failure branch itself (an angle actually failing mid-capture) hasn't been deliberately triggered live yet | 2026-08-08 |
| EH06 | `BeanSnapshotExporter.CaptureSnapshot()` | `Object.Destroy()` on temporary line/texture objects — silently no-ops outside Play Mode (Unity API behavior, not UTI's own boundary, but the same "don't leave a mess behind" spirit) | New `SafeDestroy()` helper: `Application.isPlaying ? Destroy() : DestroyImmediate()`, applied to all three `Destroy()` call sites in `CaptureSnapshot()` | Live capture from an Editor context (no Play Mode) | **Verified live** — this is T26 (`TESTS/TestTracker.md`), a bug *found* by live testing itself (leaked `BeanSnapshotPath` GameObjects), fixed, and re-verified live immediately after (zero leaks, zero errors on an identical capture) | 2026-08-08 |
| EH07 | `BeanConfig.Load()` | `BeanConfig.txt` — an externally-edited file (locked, permission-denied) | Read wrapped in try/catch; a failure is treated the same as "file doesn't exist" — returns `null` (compiled-in defaults apply) with a warning, matching the method's existing documented contract rather than adding a new failure mode | Touches the real project-root config path, not independently unit-tested | **Code-reviewed only** — the happy path (`Load()` reading a real, valid file) was verified live 2026-08-08 (see T22), but a genuinely locked/unreadable file hasn't been deliberately triggered live yet | 2026-08-08 |
| EH08 | `BeanConfig.CreateTemplateIfMissing()` / `CopyEndUserDocsIfMissing()` | Editor menu action file I/O (`UTI > Create Bean Config` / `UTI > Setup Project`) | Wrapped in try/catch; a write/copy failure logs a clear warning instead of an unhandled exception breaking the Editor menu action | Touches the real project-root `UTI/` folder, not independently unit-tested | **Code-reviewed only** — the happy path was verified live 2026-08-08 (both menu items ran successfully against `project 2`'s real filesystem), the failure branch itself hasn't been deliberately triggered | 2026-08-08 |
| EH09 | Compile-safety, whole package (`Runtime/` + `TESTS/`) | Ambiguous bare `Object.` reference — a file with both `using System;` and `using UnityEngine;` makes an unqualified `Object` a hard `CS0104` compile error (see bug report below) | Convention, not runtime code: qualify as `UnityEngine.Object.X` at every call site in any file that has both usings. No automated enforcement yet (see "Prevention" in the 2026-08-08 bug report entry, `TESTS/TestTracker.md` Change Log) | Full-repo sweep (`Runtime/` + `TESTS/`) for bare `Object.` in any file with `using System;` present | **Fixed and swept clean 2026-08-08** — `BeanTrackerTests.cs`/`BeanLoggerTests.cs` (16 + 12 call sites) qualified; confirmed via sweep that `BeanSnapshotExporterTests.cs` (fixed a prior session) and `BeanMouseTrackerTests.cs`/`BeanVisualizerTests.cs` (no `using System;`, never ambiguous) are all clean. No CI/analyzer enforcement exists yet — this is a manual-convention row, re-check on every new file that adds `using System;` to a test file with bare `Object.` calls. | 2026-08-08 |
| EH10 | `BeanSnapshotExporter.CaptureSnapshot()` | Live `BeanTracker.Samples` ring buffer being at capacity — not a boundary in the caller-code/disk-I/O sense, but the same "make a silent failure visible" spirit; a full buffer means older samples have already been overwritten, so the snapshot may not reflect the full recorded path | New pure `IsBufferAtCapacity(sampleCount, maxSamples)` check; logs a `Debug.LogWarning` naming the object and explaining the likely cause (a long idle tail evicting real movement) and the fix (raise `Max Samples`, call `StopTracking()` promptly) | `UTI.Tests.BeanSnapshotExporterTests.IsBufferAtCapacity_*` (3 tests) | **Found live by the `project 2` team (T27), root-caused and fixed live by this session (T28) — verified live, not just unit-tested.** Reproduced directly in `project 2`: 200 samples of real 9m movement + 3000 stationary samples dropped the live buffer's recorded span to exactly 0, confirming the exact failure mode. 87/87 EditMode suite green after the fix. See `TESTS/TestTracker.md` T28, `DESIGN.md` §13. | 2026-08-08 |

## Deliberately NOT guarded (internal invariants, not boundaries)

- **`BeanBuffer`'s constructor** throws `ArgumentOutOfRangeException` on a non-positive capacity —
  left as a hard throw on purpose. A bad capacity here is a genuine programming/config error inside
  UTI's own code, not a runtime boundary (no caller code, no I/O, no external file involved) — it
  should fail loudly and immediately during development, not be swallowed into a warning that lets
  a broken Bean silently limp along.
- Everything else internal to UTI (buffer indexing, decimation math, framing math, CSV formatting)
  has no defensive guards either, for the same reason — these are pure functions over UTI's own
  data, covered by `TESTS/TestTracker.md`'s regular test rows instead.

## Change Log

- 2026-08-08 19:10 — No new row needed for `JsonlBeanOutput`: EH02/EH03/EH04 already guard every active
  `IBeanOutput`'s `Open()`/`Write()`/`Close()` generically (the try/catch loops in `BeanLogger`
  don't know or care what type the output is), so adding a second file-based output alongside
  `CsvBeanOutput` didn't add a new boundary, just another concrete type already covered by an
  existing one. Re-confirmed live 2026-08-08 as part of the JSON export verification pass (97/97
  EditMode suite, `project 2`) — see `TESTS/TestTracker.md` T29.
- 2026-08-08 13:40 — Added EH10: `CaptureSnapshot()` now warns when the live sample buffer is at
  capacity, closing a real bug the `project 2` team found live (T28, `TESTS/TestTracker.md`) — a
  long idle tail silently evicting the real recorded path before a snapshot happens. Root-caused
  and fixed same day, verified live via direct reproduction in `project 2`.
- 2026-08-08 12:45 — Initial tracker created: EH01–EH08 for this session's fault-isolation pass (see
  `DESIGN.md` §14), EH09 for the CS0104 ambiguous-`Object`-reference bug class flagged in a bug
  report from the `project 2` team the same day. Companion to `TESTS/TestTracker.md`, added per
  explicit request to track error handling with the same rigor as tests.
