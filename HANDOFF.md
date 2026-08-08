# HANDOFF — read this first if resuming a paused UTI session

Session paused 2026-08-08. Three things happened this session, in order: (1) an integrity pass —
built and unit-tested T11/T12/T14/T15/T22/T23/T24, plus a full round of fault-isolation error
handling (T25) — (2) something new: **this session's Unity MCP connection turned out to be
attached directly to `project 2`, with real script execution**, not the read-only/relay-only access
every prior session had, which closed almost the entire punch list live in this same session
(including finding and fixing a brand-new bug, T26) — and (3) a real bug report from the
`project 2` team (CS0104, second occurrence — already fixed, now tracked as EH09) plus a new
`TESTS/ErrorHandlingTracker.md` tracking every guarded system boundary the same way
`TESTS/TestTracker.md` tracks features, and T27, a full uninstall/reinstall round handed to the
`project 2` team for a genuine first-time-user docs walkthrough — and (4) T27 came back with a
full report and **one more real bug (T28), found by the team and root-caused live by this
session**: `BeanSnapshotExporter` reads the live sample buffer, not the CSV, so a long idle tail
after real movement can silently evict the whole path before a snapshot happens — reproduced
directly, fixed with a console warning (EH10), docs updated. Delete or let this go stale once
superseded — `TESTS/TestTracker.md`/`TESTS/ErrorHandlingTracker.md`/`DESIGN.md`'s Change Logs are
the durable record.

## The headline: T23 is provably fixed, not just "believed correct"

Drove a real capture directly in `project 2`'s actual open scene (the box-jump platformer): a
near-stationary path (30 samples, ~1 point) at the old `MinFramingRadius=2` default produced
*exactly* the originally-reported bug — a flat red wall face filling the frame, no context. The
same path, same call, with `MinFramingRadius=9` produced a properly framed shot showing the full
level (ground, boxes, the player capsule). Both PNGs were viewed directly, not inferred from logs
or file existence checks. Multi-angle capture (T24) got the same treatment — `[Auto, Above, Side,
Behind]` on the same path wrote 4 distinct, correctly-composed PNGs, `Auto` exactly matching the
single-angle shot (confirming zero behavior change for existing usage).

**A brand-new bug was found and fixed by this live testing itself (T26):** driving
`CaptureSnapshot()` from an Editor context (not Play Mode) left `BeanSnapshotPath` GameObjects
leaked permanently in the scene — `Object.Destroy()` is a documented no-op outside Play Mode.
Fixed with a new `SafeDestroy()` helper, re-verified live immediately after (zero leaks). This
wouldn't have been found by any of the EditMode unit tests, since `CaptureSnapshot()` isn't
EditMode-testable at all — it took actually running it live to surface.

**Full EditMode suite: 84/84 passed, 0 failed.** Confirmed via `TestRunnerApi`, cross-checked
against the real `TestResults.xml` (not just a summary log), and re-confirmed with a fresh
timestamp after the T26 fix landed mid-session — deliberately guarding against the
stale-vs-fresh-result confusion flagged in earlier rounds.

## What this session's Unity MCP connection can and can't do (confirmed by testing, not assumed)

This matters for every future session — the old documented limits ("read-only checks only,
relayed through a separate agent") turned out to be specific to whichever project/connection prior
sessions had, not a universal ceiling:

**Can do:** run the real EditMode test suite (`TestRunnerApi`) and read its actual results file;
invoke Editor menu items (`EditorApplication.ExecuteMenuItem`); create/inspect/destroy GameObjects
and components; reflect into another assembly's **public** members (`Type.GetMethod`/`GetProperty`
with no `BindingFlags`, `Assembly.GetType(string)`) — enough to drive `BeanTracker`/
`BeanSnapshotExporter`'s entire public API (`StartTracking`, `SimulateFrame`, `CaptureSnapshot`,
`ApplyConfigDefaults`, etc.) without ever needing a direct `using UTI;` reference (which doesn't
compile in the scratch context). Real files written to disk (PNGs, `BeanConfig.txt`) can then be
read directly with a normal file-read tool — no export/relay step needed.

**Can't do (hit these directly, not from old notes):** a `using System.Reflection;` import (or even
a fully-qualified `System.Reflection.BindingFlags`/`MethodInfo` reference without the `using`) is
flagged as an unauthorized namespace — so anything `private` (like `Reset()` itself) can't be
invoked directly; worked around by calling the public `ApplyConfigDefaults(BeanConfig.Load())` that
`Reset()` itself just calls. `System.Xml.Linq`/`System.Text.RegularExpressions` aren't referenced in
the scratch-compile context either — plain `string.IndexOf`/`Substring` parsing works fine instead.
**Entering Play Mode and `GameObject.SetActive()` are both blocked outright** as unsupported "user
interaction" — these are hard limits, not workarounds waiting to be found. This is why T12/T14's
Play Mode halves are still open.

## What's actually still left

Only two real gaps, both because they need genuine Play Mode (blocked above):

1. **T12's Play Mode half** — confirm `EveryFixedUpdate` samples land on the physics tick under
   real physics, not just that `SimulateFixedFrame()` behaves correctly in EditMode (already
   verified).
2. **T14's Play Mode half** — confirm a real `SetActive(false)`→`SetActive(true)` cycle during
   Play Mode truncates/appends correctly, not just the EditMode-equivalent logic (already
   verified).

Lower priority, unchanged from before this round: T05/T06 (`BeanVisualizer`'s actual gizmo render —
low urgency given T23/T24's strong indirect confidence), T08 (sample scenes, still not started),
T21's live `Update()`-driven mouse-follow check (pure functions already verified).

**T27, waiting on the `project 2` team, not this session:** a full UTI uninstall + fresh reinstall,
followed as a genuine first-time-user `USAGE.md` walkthrough — deliberately not run here, since a
session that already knows how UTI works can't fairly simulate a first-time user. Full instructions
are in `TESTS/TestTracker.md`'s "Fresh Install Verification Round" section, including a heads-up
that `project 2`'s real `Player` GameObject already has `BeanTracker`/`BeanLogger`/
`BeanSnapshotExporter` attached from earlier rounds (remove those before uninstalling the package,
or Unity leaves "missing script" placeholders behind).

## Real bugs found and fixed this session

1. **T23** (confirmed, see headline above) — `MinFramingRadius` was a fixed `2f` literal, too tight
   for `project 2`'s scale on a near-stationary path. Now a per-Bean field, defaultable via
   `BeanConfig.DefaultMinFramingRadius`.
2. **T26** (confirmed, see headline above) — `Object.Destroy()` no-ops outside Play Mode, leaking
   temporary GameObjects from `CaptureSnapshot()`. Fixed with `SafeDestroy()`.
3. **Doc staleness** — `USAGE.md`/`READING_LOGS_AND_VISUALS.md`/`CONFIG.md` had never actually been
   saved into the UTI package repo itself (only as copies inside `little wings`). Restored, and
   `USAGE.md` §8 was found still describing the old, reverted `ScriptableObject`-based `BeanConfig`
   — fixed to match `CONFIG.md`'s already-correct plain-text description. Corrected versions copied
   into both `little wings` and `project 2`.
4. **CS0104, second occurrence** — reported by the `project 2` team: `BeanTrackerTests.cs`/
   `BeanLoggerTests.cs` both gained `using System;` this round (for `Guid`/
   `InvalidOperationException`/`DateTime` in the new fault-isolation tests), making bare
   `Object.DestroyImmediate` ambiguous against `using UnityEngine;` — same class of bug as a prior
   session's `BeanSnapshotExporterTests.cs` fix, just never swept across the rest of `TESTS/` at the
   time. Already fixed as a side effect of writing those tests (qualified to
   `UnityEngine.Object.DestroyImmediate`); confirmed via a full-repo sweep this round and tracked as
   `TESTS/ErrorHandlingTracker.md` EH09 so it doesn't need re-discovering a third time.
5. **`CLAUDE.md` itself was stale** — its Unity MCP note still described the old, more limited
   read-only/`little wings`-only access. Corrected to reflect that capability varies by session and
   should be checked directly rather than assumed from prior notes.

## Things worth knowing before touching anything else

- **New standing protocol for verifying UTI in any project: `DESIGN.md` §12, "The Bring-Your-Own-
  Test Protocol."** Find a test that already exists and already passes, add the relevant Beans to
  what it exercises, run it completely unmodified, report what UTI produced — never write new
  harness/test code just to make UTI drivable by whatever's doing the verifying. Came directly from
  a real `project 2` T27 mishap (see below) where an agent's blocked Play Mode access led to
  rewriting a working test's movement logic instead, introducing a new bug in the process.
- UTI is standalone — not embedded in `little wings`/`project 2`/`2d project 3`. Those three
  reference it as a local package for testing. See `DESIGN.md` §11.
- **UTI's actual code/API must stay genre-agnostic.** Genre-specific names are fine in a test-bed
  project's own fixtures, never in UTI's real naming, types, or docs.
- Everything UTI touches in a game project lives under one `<project root>/UTI/` folder — output
  (`BeanLogs/`, `BeanSnapshots/`), `BeanConfig.txt`, and the three copied end-user docs. Nothing
  needs `Assets/`. New: `UTI > Setup Project (Config + Docs)` bootstraps all of this in one click
  for a brand-new project — confirmed working live this session.
- `BeanMouseTracker` needs the legacy Input Manager enabled (Active Input Handling: "Both" or
  legacy-only) — won't receive input under "Input System Package (New)" only.
- **This session's Unity MCP connection is a real capability upgrade** — see the section above
  before assuming "no direct execution access" the way earlier HANDOFF.md rounds documented. Check
  whether it's still attached to `project 2` (or wherever) at the start of a future session before
  falling back to pure relay.
- `TESTS/` (docs) and a sibling `Tests/` (code) would collide on Windows' case-insensitive
  filesystem — C# test code lives under `TESTS/EditMode/`.
- `package.json` id `com.uti.core` is a placeholder, matching UTI's working-title status.
- `project 2` lives at `C:\Users\sirsw\project 2`; `little wings` at
  `C:\Users\sirsw\Unity Projects\little wings`; `2d project 3` at `C:\Users\sirsw\2d project 3`.

## What to tell the user next time, verbatim if useful

"Big session — the T23 close-up bug and multi-angle snapshots both got real, definitive proof this
time, not just unit tests: this session's Unity MCP connection turned out to be attached directly
to `project 2`, so I could actually run the capture live and look at the resulting PNGs. Same exact
near-stationary path produced the old useless close-up at the compiled-in default, and a properly
framed shot once `Min Framing Radius` was raised — that's about as close to definitive as evidence
gets. Also found and fixed a brand-new bug that only showed up from actually running it: a leaked-
GameObject issue when capturing outside Play Mode. Full EditMode suite is 84/84 green.

Since then: fixed a real bug report from the `project 2` team (a second occurrence of that CS0104
compile error from a few rounds back — already caught and fixed this session, now tracked so it
doesn't recur a third time), added a dedicated error-handling tracker alongside the test tracker
(same rigor, one row per guarded boundary), and put together a full uninstall-and-reinstall test for
the `project 2` team to run themselves — a genuine first-time-user docs walkthrough, which needs
fresh eyes rather than a session that already knows how everything works.

Only two real gaps left, and both need genuine Play Mode, which this connection can't enter —
`EveryFixedUpdate`'s physics-tick timing and a real pooled-object `SetActive` cycle. Want me to look
at those next, wait on the `project 2` team's fresh-install report, or move on to something else?"
