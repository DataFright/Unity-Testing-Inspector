# PROJECT_OVERVIEW.md — History

The complete Change Log since day one, and the full story behind every Roadmap item that's since
shipped. Split out of `PROJECT_OVERVIEW.md` so that file stays focused on the current pitch and
what's still open. Public — this is genuine project provenance, not internal working notes.

## Shipped Roadmap items — the full story

### Persisted visualization artifact (`BeanSnapshotExporter`)

`BeanVisualizer` alone only draws live, via Editor Gizmos, in the Scene view, while Play Mode is
active — there was no way to review a completed run's path afterward, hand it to a teammate, or
look at it without the Editor open to that exact live moment. That undercut the actual point of
visualization: a developer can already watch an object move by just looking at the Game view while
playing, so a trail that only exists during that same live window doesn't add much beyond what
their own eyes already see. The real value is reviewing it *after* the run.

**Decided:** the artifact needs real scene context, not just an abstract path — per user feedback,
"say someone falls through the floor, well you have to see the floor." A path plotted in empty
space can't show that; a real rendered image of the scene can. Built as `BeanSnapshotExporter` — a
new, decoupled Bean piece that renders the tracker's path through an actual Camera (not gizmos) into
a PNG on disk, so the surrounding geometry a dev needs for context comes along automatically.

**Built, then refined through real use (T16/T17, `little wings`):** a real capture produced a
correct path line over real scene geometry, but chasing "is this actually usable" turned up real
follow-on issues: the auto-framed shot's path line was invisible (fixed `lineWidth` too thin at the
distance auto-framing pulls the camera back to); the whole feature defaulted its output to
`Application.persistentDataPath`, a hidden per-user Windows folder with no relation to the project
directory, which is exactly what "developer can review results afterward" is supposed to prevent;
and the fix for that was itself refined once more per feedback — everything UTI generates
(`BeanLogger`'s CSVs included) now lives in one shared `UTI/` folder at the project root, not
scattered loose subfolders. Filenames for both are unique per run *and* per GameObject instance, so
repeated runs and same-named clones (bullets, enemies) don't overwrite each other. Full technical
detail: `DESIGN_HISTORY.md` §8.4/§8.5.

### Robustness fixes (found by re-reading v1 against real genre usage)

These weren't new features so much as closing gaps between "works in a clean one-Bean demo" and
"holds up in a shooter/AI/bullets scene" — the genres UTI is explicitly meant to serve. All fixed
and confirmed live: unique default CSV filenames (a random uniqueness token plus a capture
timestamp, so clones from the same prefab don't stomp each other's output); a deliberate answer for
object pooling (`BeanLogger.AppendAcrossReuse`, opt-in, truncate-on-reopen as the documented
default); `EveryFixedUpdate` routed through a testable path (`SimulateFixedFrame()`); real test
coverage for `CustomCapture`/`extras` end-to-end. Full technical detail: `DESIGN_HISTORY.md` §13
history.

### JSON Lines export

The real motivation over CSV was never "easier to parse" — plain CSV parsed without friction the
whole time, including by an AI agent reading it directly — it was **structured `extras`**:
`CsvBeanOutput` packs `extras` into one flat `key=value;key=value` string column (a fixed CSV schema
can't handle a varying key set), where JSON's `extras` is a real nested object with natively-typed
values instead. Fit the existing `IBeanOutput` extension point directly, no core `BeanTracker`
changes needed. Once both file-based outputs existed side by side, their identical `StreamWriter`
-lifecycle code got pulled into a shared `BeanFileOutputBase`. Verified live in `project 2`: clean
compile, 0 console errors, 97/97 EditMode tests passing, both before and after the refactor. Full
technical detail: `DESIGN_HISTORY.md` §8.2.

### Multi-angle snapshots and `BeanConfig` snapshot-quality settings

`BeanSnapshotExporter.CaptureAngles` (default `[Auto]`, fully backward compatible) accepts
`Above`/`Side`/`Behind` as well — set more than one and a single `CaptureSnapshot()` call writes one
PNG per angle, sharing a group timestamp. Directly motivated by real friction (`project 2`'s T23
report): a single auto-framed angle could end up unhelpfully tight or miss context a different
angle would have shown. Alongside it, `MinFramingRadius` (previously a fixed `2f` literal) became a
per-Bean field, defaultable project-wide via `BeanConfig.txt`'s `DefaultMinFramingRadius` — the T23
fix. Full technical detail: `DESIGN_HISTORY.md` §8.4 history.

## Naming — the Isolator/Inspector drift

A full-project review found the spelled-out product name had drifted: `README.md`'s title,
`package.json`'s `displayName`, and `TestTracker.md` all said "Isolator," while the actual live
GitHub repo (`github.com/DataFright/Unity-Testing-Inspector`), `package.json`'s own
`repository`/`documentationUrl`/etc., `CLAUDE.md`, `DESIGN.md`, and `USAGE.md` all said "Inspector."
Standardized on **Inspector** across the four inconsistent files since the repo URL is the fixed,
already-shared external fact — cheaper to update display text than rename a live public repo.

## Full Change Log (since day one)

- 2026-08-11 09:41 — **Full docs restructure: condensed the doc-heaviest files, split narrative
  history into adjacent `_HISTORY.md` files.** UTI's docs (13 files, ~4,093 lines) had grown to
  outweigh all Runtime + test code combined (~3,668 lines), with `docs/DESIGN.md` (1,180 lines) and
  `TESTS/TestTracker.md` (1,120 lines) the two worst offenders — not from inaccuracy, but from the
  same narrative (the CI setup saga, T05's eight-attempt investigation, `BeanSnapshotExporter`'s
  iteration history) getting told in full 3–4 times over across a table cell, a dedicated
  narrative section, that doc's own Change Log, and a sibling doc's Change Log. This was already
  flagged as the project's own weak point in the 2026-08-09 review below but never acted on until
  this pass. Restructured `CLAUDE.md`, `docs/DESIGN.md`, `TESTS/TestTracker.md`, and this file, each
  now paired with an adjacent `_HISTORY.md` holding the full narrative and complete historical
  Change Log — `CLAUDE_HISTORY.md` and `TESTS/TestTracker_HISTORY.md` gitignored (internal
  session-log-flavored content, and the source of a local-machine-path cleanup done in the same
  pass), `docs/DESIGN_HISTORY.md` and this file public (real engineering/provenance value). Folded
  the standalone `TESTS/PROJECT2_FRESH_INSTALL_REPORT_2026-08-08.md` into `TestTracker_HISTORY.md`
  as an appendix and removed it. Also pruned `docs/HANDOFF.md`'s superseded session entries and
  updated `docs/ONBOARDING.md`'s file map/lookup table for the new history files. Verified the CLAUDE.md-specific
  restructuring research supplied by the user against the official Claude Code docs before acting
  on any of it — confirmed nested-CLAUDE.md lazy-loading and the 4-hop import depth limit, found
  `.claude/rules/` (a path-scoped mechanism the supplied research didn't cover at all), and
  confirmed neither nested CLAUDE.md nor `.claude/rules/` was worth adopting for a project this
  small and tightly-coupled — the actual fix was a content-editing problem, not a
  memory-loading-mechanism one.
- 2026-08-09 21:52 — Added a Roadmap "Feature ideas" entry for DOTS/Netcode-for-Entities
  ghost-prefab compatibility, per direct user request to track it as a future TODO rather than let
  it drop once the immediate `bitshot` incident was resolved. Confirmed not a current UTI defect
  (root cause is `bitshot`'s own subscene-baking pipeline, not UTI's code) — scoped honestly as a
  documentation gap plus a genuinely open "would a code-level accommodation be worth it" question,
  not as "UTI doesn't work with multiplayer." Full incident writeup: `TESTS/TestTracker_HISTORY.md`.
- 2026-08-09 10:05 — **Full outside project review, then two follow-ups landed.** A fresh review
  (code + docs + tests, no prior-session context) graded the project overall B+ — strong marks for
  architecture/decoupling/error-handling discipline and the honesty of the Pass/Planned tracking,
  held back by doc volume disproportionate to actual git history, `BeanVisualizer`'s
  still-never-visually-confirmed render (T05, since resolved), and thin multi-project validation.
  Two findings acted on immediately: (1) the Isolator/Inspector naming drift — see this file's
  Naming section above — fixed across `README.md`/`package.json`/`TestTracker.md`/`USAGE.md`; (2)
  CI's already-known `cache-installation` speedup actually wired in, not just noted as backlog
  (later reverted — see `DESIGN_HISTORY.md` §12 history).
- 2026-08-08 22:50 — **CI confirmed fully green — first real, verified pass end to end.** Four
  distinct real bugs found and fixed to get here, each root-caused from real evidence rather than
  guessed at — full blow-by-blow in `DESIGN_HISTORY.md` §12 history. `Status: Success`, 12m 32s,
  real UTI tests actually running and completing. CI now runs the EditMode suite automatically on
  every push to `main`.
- 2026-08-08 20:38 — CI's first live run found and fixed a real bug: `buildalon/unity-setup`'s own
  version-detection glob silently skips dot-prefixed directories, so it couldn't find
  `ProjectVersion.txt` while the CI project shell lived at `.github/ci-project/`. Moved to `CI~/`.
- 2026-08-08 19:00 — **Project review pass, then follow-up work kicked off.** A full outside review
  of the codebase, file structure, docs, and test/error-handling coverage (code quality strong,
  decoupling and testable/untestable splits real, error-boundary discipline unusually consistent
  for a project this size; weaknesses: no CI, `TESTS/PlayMode/` unpopulated, doc volume
  disproportionate to code size, git history far less granular than the prose Change Logs it sits
  alongside) turned into a prioritized punch list. Immediate results: new `docs/ONBOARDING.md` (a
  short, stable map for a fresh agent session), and a new standing practice (`CLAUDE.md`) to commit
  per logical unit of work going forward instead of batching a session into one commit. Also added
  CI: `.github/workflows/tests.yml` runs the EditMode suite via a new minimal, scrubbed project
  shell on push to `main` — not yet live-verified at this point, needed `UNITY_EMAIL`/
  `UNITY_PASSWORD` secrets added to the repo first. Its license-activation approach changed mid-setup
  after due diligence found the original plan (an exported license file) is machine-bound and
  wouldn't validate on GitHub's runners — see `DESIGN_HISTORY.md` §12 history for the full story.
- 2026-08-08 16:35 — JSON Lines export Roadmap item marked built and verified live (moved from
  "Feature ideas"). Also extracted `BeanFileOutputBase`, a shared base class between
  `CsvBeanOutput` and `JsonlBeanOutput` once their `StreamWriter`-lifecycle code turned out to be
  identical. Verified in `project 2` via direct Unity MCP access: clean compile, 0 console errors,
  97/97 EditMode tests, both before and after the refactor.
- 2026-08-08 16:05 — **Standing rule: never build our own demo/sample Unity projects to test UTI.**
  Proposed building `Samples~/` car/NPC/player demo scenes to close test-row T08; corrected
  directly: real time/token cost for low verification value, and contradicts the whole premise of
  the Bring-Your-Own-Test Protocol (`DESIGN.md` §12) — we're testers using real projects, not
  developers building our own. The demo-scene idea moved to this file's own Dream To-Do section;
  T08 repurposed in `TESTS/TestTracker.md`; rule also written into `CLAUDE.md` and session memory.
- 2026-08-08 15:40 — T12/T14 (Roadmap "Robustness fixes") fully closed — the last two Play-Mode-only
  test gaps, live-verified in `project 2` after that session's Unity MCP connection turned out to
  support real Play Mode entry and `GameObject.SetActive` (both hard-blocked in every prior
  session). `EveryFixedUpdate` samples landed exactly on the physics tick; pooled-object
  `SetActive` reuse correctly truncated (default) and correctly accumulated (`AppendAcrossReuse`)
  across a real reuse cycle. Pure verification, no behavior changed.
- 2026-08-08 15:15 — Sharpened the JSON export Feature idea (JSON Lines format, structured `extras`
  as the real motivation) and added a new **Dream To-Do** section — bigger, further-out, not-MVP
  concepts distinct from the regular Roadmap — seeded with a 3D-explorable-scene-artifact idea per
  user request, deliberately not scoped or started.
- 2026-08-08 14:45 — **Went public: repo created at github.com/DataFright/Unity-Testing-Inspector,
  MIT licensed, first commit pushed.** Reorganized the file structure for a real public repo: this
  file is the former `README.md`, renamed to `PROJECT_OVERVIEW.md` and moved into a new `docs/`
  folder alongside `DESIGN.md`, `HANDOFF.md`, and the three end-user docs — the root `README.md` is
  now a short public pitch + a real fresh-clone install guide (Package Manager git URL, replacing
  the old hardcoded local `file:` path that only ever worked on one machine).
  `BeanConfig.CopyEndUserDocsIfMissing()` updated to match (source path now `docs/<filename>`),
  verified live.
- 2026-08-08 13:25 — **Real bug found by the `project 2` team's fresh-install round, root-caused
  live and fixed same day (T28, `TESTS/TestTracker.md`).** `BeanSnapshotExporter` frames/draws from
  the live sample buffer, not the CSV — a long idle tail after real movement finished can silently
  evict the entire recorded path from that fixed-capacity buffer before a snapshot happens,
  producing an invisible path line and a tight, context-free close-up despite real movement having
  occurred. Reproduced directly (not just theorized): 200 samples of real 9m movement + 3000
  stationary samples dropped the live buffer's recorded span to exactly zero. Fixed with a console
  warning when this could be happening; docs updated with the symptom and fix.
- 2026-08-08 12:50 — Noted a new Feature idea (a more dynamic/cinematic snapshot angle) from a real
  Scene-view reference screenshot the user shared. Per user request, not built.
- 2026-08-08 12:15 — New `TESTS/ErrorHandlingTracker.md` tracks every guarded system boundary the
  same way `TESTS/TestTracker.md` tracks features. Also fixed a bug reported by the `project 2`
  team: a second occurrence of the ambiguous-`Object`-reference `CS0104` compile error.
- 2026-08-08 11:35 — First live-verified round: that session's Unity MCP connection turned out to be
  attached directly to `project 2` with real script execution (not just relay/read-only access like
  every prior round). Closed almost the entire punch list for real — full 84/84 EditMode suite
  passed, T22/T23/T24 confirmed live (T23 with real before/after PNGs showing the close-up bug is
  genuinely fixed), and a brand-new bug (T26: leaked GameObjects from calling a capture outside Play
  Mode) was found and fixed by this same live testing.
- 2026-08-08 10:50 — Integrity pass: guarded every real system boundary UTI touches with no
  crash-the-game risk left unhandled — a throwing `CustomCapture` delegate, a `BeanLogger` output
  that fails to open/write/close, a `BeanSnapshotExporter` multi-angle capture where one angle's
  file write fails, and a locked/unreadable `BeanConfig.txt`. Each now degrades gracefully instead
  of propagating an unhandled exception. See `DESIGN_HISTORY.md` §14 history for the full
  boundary-by-boundary writeup.
- 2026-08-08 10:20 — Closed most of the punch list left by the `project 2` round below in one pass,
  all code-complete and unit-tested but not yet live-verified at that point: T23 fixed
  (`MinFramingRadius` → `BeanConfig.DefaultMinFramingRadius`); multi-angle snapshots built; T11
  (`CustomCapture`/extras end-to-end), T12 (`EveryFixedUpdate` via new `SimulateFixedFrame()`), and
  T15's EditMode half all closed with new tests; T14 decided and built. Also: new `UTI > Setup
  Project (Config + Docs)` Editor menu item bootstraps `BeanConfig.txt` *and* copies the three
  end-user docs into a project's own `UTI/` folder in one step. Found and fixed a real doc bug while
  restoring `USAGE.md`/`READING_LOGS_AND_VISUALS.md`/`CONFIG.md` (which turned out to exist only as
  copies inside `little wings`, not in this package repo itself): `USAGE.md` §8 had gone stale,
  still describing the old `ScriptableObject`-based `BeanConfig`.
- 2026-08-08 09:25 — First full closing report from `project 2`: UTI's CSV decisively pinned down a
  real game bug (a jump-trigger distance geometrically unreachable given the player's own collision
  radius) to five decimal places — the dev said directly they wouldn't have found it from behavior
  alone. Also surfaced a real, unfixed bug (T23: auto-framing produces a useless close-up on a
  near-stationary path) and confirmed a known doc gap (the three end-user docs still weren't copied
  into `project 2`'s own `UTI/` folder). Added two new Feature ideas per user request: multi-angle
  snapshots with grouped/labeled naming, and `BeanConfig` covering snapshot-quality settings like
  `MinFramingRadius` (would directly address T23). Neither built yet at this point.
- 2026-08-07 — Sharpened the input-tracking Feature idea with a concrete motivating case (a Unity
  Test Framework test that "should click" but fails) and tied it explicitly to a restated Design
  Philosophy point: UTI empowers/narrows down, it doesn't diagnose or fix root causes itself. Added
  a new Example Use Case, "Complements automated test tooling," to match.
- 2026-08-07 — Noted a new Feature idea (input tracking beyond mouse position) per user request, not
  built. Also added a Roadmap strategy note: the upcoming `project 2` real-bug-diagnosis round was
  meant to double as a source for future roadmap items, not just a feature test.
- 2026-08-07 — `BeanConfig` rebuilt as a plain text file (`<project root>/UTI/BeanConfig.txt`,
  bootstrapped via a new "UTI > Create Bean Config" Editor menu item) instead of a
  `ScriptableObject` asset in `Assets/UTI/`, after pushback that config should live in the same
  place as the rest of a project's UTI footprint, not split into `Assets/`. `CONFIG.md` moved to
  the same `<project root>/UTI/` copy convention as `USAGE.md`/`READING_LOGS_AND_VISUALS.md`.
- 2026-08-07 — `USAGE.md`/`READING_LOGS_AND_VISUALS.md` copied into `little wings`'s own
  `<project root>/UTI/` folder, per feedback that these are end-user docs and belong where that dev
  is actually looking, not just in this package repo.
- 2026-08-07 — After clarification that "give the dev the ability to choose and change stuff" meant
  a centralized settings file, not per-Bean fields (or a new capture mode — reverted `EveryNTicks`,
  added earlier the same session, once that became clear): new `BeanConfig` holding preferred
  defaults for `BeanTracker`/`BeanSnapshotExporter`, applied automatically to any *newly added* Bean
  via Unity's `Reset()` hook.
- 2026-08-07 — T13/T16/T17 verified Pass live in `little wings` (25/25 tests, real PNG+CSV
  confirmed on the filesystem under the new `UTI/` folder). Three additions per user feedback:
  `BeanSnapshotExporter.DimensionMode` (`Auto`/`Force2D`/`Force3D`); a fixed auto-frame camera
  offset-direction bug (now derived from the path's own travel direction, always broadside); new
  `BeanMouseTracker`. Also added `USAGE.md` and `READING_LOGS_AND_VISUALS.md`.
- 2026-08-07 — Fixed a relayed compile error (`CS0619`, `GetInstanceID()` reported as
  obsolete-as-error) by swapping the filename-uniqueness key from `GameObject.GetInstanceID()` to a
  random GUID-fragment token instead.
- 2026-08-07 — Refined the output-location fix once more per feedback: everything UTI generates now
  nests under one shared `UTI/` folder at the project root instead of two loose sibling folders.
- 2026-08-07 — Moved UTI's default output location for both `BeanLogger` and `BeanSnapshotExporter`
  off `Application.persistentDataPath` and onto the project root instead. Also fixed the
  auto-framed shot's path line being invisible (width wasn't scaled to the pulled-back camera
  distance).
- 2026-08-07 — `BeanSnapshotExporter` verified end-to-end in `little wings` (T16/T17): a real
  capture showed a correct path line over real scene geometry. The first capture was framed too
  tight, which prompted same-session fixes: a dedicated `BeanSnapshots/` output folder,
  timestamp-prefixed filenames, and camera auto-framing.
- 2026-08-07 — Persisted visualization artifact: decision made and built. See "Shipped Roadmap
  items" above for the full story.
- 2026-08-07 — Elevated the "no persisted visualization artifact" gap from a buried Roadmap bullet
  to its own flagged section — a real human developer using UTI to debug their game had no way to
  review `BeanVisualizer`'s path after the fact. Surfaced by user feedback while chasing T05's
  repeated screenshot-tooling failures.
