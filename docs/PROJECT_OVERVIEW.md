# UTI — Project Overview, Roadmap & Full History
*(working title — funny on purpose, renameable at launch)*

**New here? Start at the [root README](../README.md)** — it has the short pitch and the actual
install steps. This file is the deeper, ongoing project record: the full pitch/concept, the
complete Roadmap, and every Change Log entry since day one. [USAGE.md](./USAGE.md) covers
install/setup in end-user terms, [READING_LOGS_AND_VISUALS.md](./READING_LOGS_AND_VISUALS.md)
explains how to interpret what UTI produces, [CONFIG.md](./CONFIG.md) covers project-wide defaults,
and [DESIGN.md](./DESIGN.md) is the architecture.

## Elevator Pitch

Drop a **Bean** on any GameObject and watch where it actually goes. UTI is a lightweight, drop-in Unity toolkit for tracking, logging, and visualizing movement over time — for anything: a player, a car, an NPC, an AI ally, a projectile, a plane, a physics prop. No test framework to learn, no assertions to write. Just attach it, play, and look at the trail.

## The Problem

Unity Test Framework is great for code-level assertions (did this function return the right value), but it's clunky for verifying *behavior over time* — "did this thing end up roughly where it should have, following roughly the path it should have, in roughly the time it should have." That kind of thing usually gets checked by eyeballing the Game view once and hoping, because building custom logging/visualization for it is annoying enough that most people skip it.

UTI closes that gap by making behavior visible: attach a Bean, hit play, and see the trail.

## Core Components (v1 scope)

- **BeanTracker** — attach to any GameObject. Captures position (x/y/z), rotation, and other configurable fields on an interval (every update / every FixedUpdate / every N seconds — user's choice). Just captures data, doesn't do anything with it. Works the same whether it's on a player, a car, an NPC, an AI ally, a projectile, or a prop — or, via `BeanMouseTracker`, the mouse cursor itself.
- **BeanLogger** — takes what BeanTracker captures and outputs it (console, CSV/JSON Lines file, in-memory buffer, or a custom `IBeanOutput` sink). Decoupled from the tracker so people can swap or extend output without touching capture logic.
- **BeanVisualizer** — plots the captured movement back into the Scene view as a path/trail (gizmo-drawn line, points per tick, maybe color-coded by time or speed). Live, during Play Mode only.
- **BeanSnapshotExporter** — a fourth, decoupled piece added mid-project (see Change Log): renders the recorded path through a real Camera into a saved PNG, so there's a durable artifact to review *after* the run ends, not just a live gizmo. See `USAGE.md`/`READING_LOGS_AND_VISUALS.md` for the full field reference on all four.

## Design Philosophy

- **General-purpose.** A kit for any genre — racing, platformer, AI-nav, flight sim, physics sandbox — tracking a player, a car, an NPC, an AI ally, a projectile, whatever. "Bean track all."
- **Empowerment, not solutions.** Makes behavior visible so the developer can judge it for
  themselves and narrow down where a problem actually lives — UTI's job is to help answer "did the
  click actually fire, and did it reach the character" so a dev can tell "my game logic is broken"
  apart from "the input never fired" apart from "something's interfering with the test itself."
  It's not a test framework and doesn't try to replace one (see "Complements automated test
  tooling" under Example Use Cases) — it doesn't assert, fail builds, or fix anything itself; it
  just gives you the evidence to fix it yourself, faster.
- **Drop-in simplicity.** Attach a component, hit play, done. Minimal setup, sane defaults, no required dependencies.
- **Decoupled pieces.** Tracker / Logger / Visualizer are separable so people can use just the part they need (e.g., logging only, no visualization, or vice versa).

## Example Use Cases

- **Player** — see the actual route a player took through a level during playtesting.
- **Car/vehicle** — confirm a car follows its intended route around a track or through traffic AI.
- **NPC / AI ally** — verify a companion or NPC's patrol, follow, or combat positioning behaves as expected.
- **Projectile** — trace a bullet/arrow/spell's actual trajectory versus the intended one.
- **Plane/vehicle sim** — confirm a flight path matches a mission route.
- **Physics prop** — after tuning physics, compare an object's trajectory visually to a prior run.
- **General QA** — attach to any suspicious object during playtesting to see what it's actually doing, no debugger required.
- **Complements automated test tooling** — e.g. a Unity Test Framework test scripts a character
  that "should click"; the test fails. UTI doesn't replace that test, but sitting alongside it can
  show whether the click genuinely fired and reached the thing it was supposed to reach — narrows
  "my game logic is wrong" down from "the input never fired" or "something in the test setup is
  interfering," instead of guessing from a red X. (Real input-event logging, not just position —
  see Roadmap's "Input tracking beyond mouse position" — not built yet.)

This list isn't exhaustive on purpose — anything with a Transform is a valid target for a Bean.

## Roadmap (rough, unordered)

*Strategy note, 2026-08-07: the `project 2` real-bug-diagnosis round (a stuck/frozen character
controller, first real test of UTI on someone else's actual problem rather than our own
verification) is meant to double as a source for this list, not just a feature test — whatever
friction or missing capability turns up there should get folded back in here afterward, the same
way `little wings`' own verification rounds kept surfacing real gaps (BeanSnapshotExporter,
BeanArtifactPaths, the broadside-framing fix) that weren't part of any original plan.*

### Persisted visualization artifact — decided and in progress

`BeanVisualizer` alone only draws live, via Editor Gizmos, in the Scene view, while Play Mode is
active — there was no way to review a completed run's path afterward, hand it to a teammate, or
look at it without the Editor open to that exact live moment. That undercut the actual point of
visualization: a developer can already watch an object move by just looking at the Game view while
playing, so a trail that only exists during that same live window doesn't add much beyond what
their own eyes already see. The real value is reviewing it *after* the run.

**Decided 2026-08-07:** the artifact needs real scene context, not just an abstract path — per
user feedback, "say someone falls through the floor, well you have to see the floor." A path
plotted in empty space can't show that; a real rendered image of the scene can. Built as
`BeanSnapshotExporter` — a new, decoupled Bean piece that renders the tracker's path through an
actual Camera (not gizmos) into a PNG on disk, so the surrounding geometry a dev needs for context
comes along automatically.

**In progress 2026-08-07 (T16/T17, little wings):** a real capture produced a correct path line
over real scene geometry, but chasing "is this actually usable" turned up real follow-on issues:
the auto-framed shot's path line was invisible (fixed lineWidth too thin at the distance
auto-framing pulls the camera back to); the whole feature defaulted its output to
`Application.persistentDataPath`, a hidden per-user Windows folder with no relation to the project
directory, which is exactly what "developer can review results afterward" is supposed to prevent;
and the fix for that was itself refined once more per feedback — everything UTI generates
(`BeanLogger`'s CSVs included) now lives in one shared `UTI/` folder at the project root
(`UTI/BeanLogs/`, `UTI/BeanSnapshots/`), not scattered loose subfolders. Filenames for both are
now unique per run *and* per GameObject instance, so repeated runs and same-named clones
(bullets, enemies) don't overwrite each other. None of this has been verified with a real capture
yet. See `DESIGN.md` §8.4/§8.5 and `TESTS/TestTracker.md` (T16/T17) for full detail and status.

### Robustness fixes (found by re-reading v1 against real genre usage — see DESIGN.md §13)

These aren't new features so much as closing gaps between "works in a clean one-Bean demo" and
"holds up in a shooter/AI/bullets scene" — the genres this is explicitly meant to serve:

- ~~Unique default CSV filenames~~ — **fixed and confirmed live 2026-08-07**: filenames now
  include a random uniqueness token and a capture timestamp, so two clones from the same prefab
  (bullets, enemies) no longer stomp each other's output file, and reruns on the same object don't
  overwrite earlier ones either.
- ~~A deliberate answer for object pooling~~ — **decided and built 2026-08-08 (T14)**: truncate
  remains the default (a fresh log per reuse), now a documented decision rather than an accident,
  plus a new opt-in `BeanLogger.AppendAcrossReuse` for pooled objects that should keep one running
  CSV across `SetActive` cycles instead. Not yet live-verified.
- ~~Route `EveryFixedUpdate` through a testable path~~ — **built 2026-08-08 (T12)**: `BeanTracker`
  now exposes `SimulateFixedFrame()`, mirroring `SimulateFrame()`, so `EveryFixedUpdate` capture is
  deterministically testable without a real physics tick. Not yet live-verified.
- ~~Actual test coverage for `CustomCapture`/`extras` end-to-end~~ — **built 2026-08-08 (T11)**:
  new EditMode tests cover delegate → `Capture()` → sample `Extras` → `CsvBeanOutput`'s `extras`
  column with real data, not just "null when unassigned."

### Feature ideas

- Configurable capture fields beyond transform (velocity, custom component values via delegate/callback).
- Categorical/string-valued extras (or a documented convention for encoding state like an AI's
  current behavior — patrol/chase/attack — as a float), since `extras` today is numeric-only.
- ~~Export formats beyond CSV — JSON, specifically as JSON Lines (one object per sample), not a
  single JSON array.~~ — **built and verified live 2026-08-08**: new `JsonlBeanOutput`
  (`IBeanOutput`), `BeanLogger.OutputTargets` gained a `Json` flag alongside `Console`/`Csv`, and
  `BeanConfig` gained `DefaultOutputTargets` so a project can pick its preferred format(s) once.
  The real motivation over CSV was never "easier to parse" — plain CSV parsed without friction all
  session, including by an AI agent reading it directly — it's **structured `extras`**:
  `CsvBeanOutput` packs `extras` into one flat `key=value;key=value` string column (a fixed CSV
  schema can't handle a varying key set), where JSON's `extras` is a real nested object with
  natively-typed values instead. Fit the existing `IBeanOutput` extension point directly, no core
  `BeanTracker` changes needed. Once both file-based outputs existed side by side, their identical
  `StreamWriter`-lifecycle code got pulled into a shared `BeanFileOutputBase` (see `DESIGN.md`
  §8.2). Verified live in `project 2`: clean compile, 0 console errors, 97/97 EditMode tests
  passing, both before and after the refactor. See `DESIGN.md` §8.2/§8.7,
  `READING_LOGS_AND_VISUALS.md`'s JSON Lines section.
- Replay: play back a recorded run visually, not just as a static path.
- Optional validator/assert layer, opt-in, once the core kit has legs.
- A runtime (non-Editor-only) trail option — e.g. `LineRenderer`-drawn — so a path is visible in
  the actual Game view/builds, not just the Scene view via gizmos. Could also let a project's own
  in-game map/minimap read from `BeanTracker.Samples`/`OnSample` directly, without UTI needing to
  know that system exists (keeps the "no required dependencies" promise intact).
- **Input tracking beyond mouse position** — `BeanMouseTracker` (2026-08-07) only captures where
  the cursor *is*, not raw input events. A `Bean`-style way to log keyboard key down/up, mouse
  click down/up, and similar discrete input events (not just a continuously-sampled position)
  would round this out. Concrete motivating case: a Unity Test Framework (or similar automated)
  test scripts a character that "should click," and the test fails — an input-event log lets a
  dev directly confirm whether the click genuinely fired, separating "game logic is broken" from
  "the input never fired" from "something in the test harness itself is interfering," instead of
  guessing from a red X. This is squarely in the "empower, don't solve" lane (see Design
  Philosophy) — UTI would prove *whether* the click fired, not diagnose or fix why it didn't if it
  fired-but-failed downstream. Not designed yet — same legacy-Input-Manager-vs-Input-System
  dependency question `BeanMouseTracker` already had to answer (see `DESIGN.md` §8.6) would need
  revisiting for discrete events specifically. Noted 2026-08-07, not started.
- ~~Multi-angle snapshots, grouped and labeled~~ — **built 2026-08-08, not yet live-verified.**
  `BeanSnapshotExporter.CaptureAngles` (default `[Auto]`, fully backward compatible) now accepts
  `Above`/`Side`/`Behind` as well — set more than one and a single `CaptureSnapshot()` call writes
  one PNG per angle, sharing a group timestamp:
  `{timestamp}.1_{name}_{angleName}_..._bean_snapshot.png`, `.2_...`, `.3_...` (naming settled).
  Directly motivated by real friction (see `project 2`'s T23 report, `TESTS/TestTracker.md`): a
  single auto-framed angle can end up unhelpfully tight or miss context a different angle would
  have shown. See `DESIGN.md` §8.4.
- ~~`BeanConfig` covering snapshot-quality settings~~ — **built 2026-08-08 as the T23 fix, not yet
  live-verified.** `MinFramingRadius` (previously a fixed `2f` literal) is now a per-Bean field,
  defaultable project-wide via `BeanConfig.txt`'s new `DefaultMinFramingRadius` key — a dev can
  tune the framing floor once for their game's actual scale instead of it being a fixed constant.
  See `DESIGN.md` §8.4/§8.7, `CONFIG.md`.
- **A more "dynamic"/cinematic snapshot angle, distinct from `Above`/`Side`/`Behind`.** Those three
  (built 2026-08-08) are all designed to fit the *whole recorded path* with margin — useful for
  "show me the route," less useful for "show me this character and roughly where they're facing/
  heading" at a glance. Idea, from a real Unity Scene-view reference screenshot shared 2026-08-08:
  a closer, elevated-behind-and-angled-down shot — like a third-person chase/follow camera, not a
  wide establishing shot — framed on the tracked object itself (near its final or a representative
  position) rather than stretched to cover the entire path's bounding box, oriented to show what's
  ahead of it. General motivation: the existing angles are all fairly flat/functional; something
  with a bit more perspective could be easier for a human dev to glance at and immediately parse
  ("that's the character, that's roughly where they were headed") than a pure top-down or
  straight broadside shot. Would likely be a new `BeanSnapshotAngle` value (naming TBD) alongside
  the existing four. Noted 2026-08-08, not designed or started — explicitly not meant to be built
  yet, just captured for later.

### Broader validation

- Push past the "one Bean, clean demo" shape UTI's been verified in so far: many simultaneous
  tracked objects in one scene (independent buffers/output, and gizmo-draw cost at realistic
  counts — a wave of enemies, a screen full of bullets), and at least one genre check that's
  actually projectile/AI-heavy rather than just flight/platformer/2D-generic. `little wings`
  being a combat game may already cover part of this if it has projectiles/enemies to attach a
  Bean to — worth checking before assuming a fourth test project is needed. **Partial (2026-08-08,
  T15):** independent-buffers is now EditMode-tested (several `BeanTracker`s driven at once, no
  shared state) — the gizmo-draw-cost-at-realistic-counts half is still a live Play Mode check.

## Dream To-Do

Bigger, further-out ideas — not MVP, not scoped, not committed to. These are concepts worth
remembering, not a plan. Nothing here should be started without a real design pass first.

- **A 3D-explorable scene artifact, not just a fixed-angle snapshot.** Instead of (or alongside)
  `BeanSnapshotExporter`'s flat PNG, export something a dev can actually rotate/pan/zoom through
  after the fact — genuinely "walk around" the captured scene and path rather than being stuck with
  whichever angle(s) got picked ahead of time. Would completely solve the "one angle doesn't show
  everything" problem multi-angle capture only partially addresses, in a much more open-ended way.
  **Real cost, why this is a dream and not a roadmap item:** this means exporting and simplifying
  real scene geometry to a portable format ("scaled down and simplified to be a smaller file" —
  decimated/low-poly, not a full scene dump), then either relying on an external 3D viewer or
  building/embedding one — a genuinely different category of feature from anything built so far,
  closer to a second product bolted onto UTI than an extension of the existing snapshot pipeline.
  Directly conflicts with the stated "deliberately basic, not polished... fast sanity check, not a
  portfolio-quality screenshot" philosophy behind `BeanSnapshotExporter` (`DESIGN.md` §8.4) unless
  scoped very carefully. Needs a real design pass (export format, geometry simplification approach,
  viewer story) before any code — noted 2026-08-08, purely conceptual.
- **Demo/onboarding sample scenes** (car, NPC, player) under a `Samples~/` folder, previously scoped
  as an active test item (T08). **Demoted to Dream To-Do 2026-08-08** after proposing it and being
  corrected: building our own demo/sample Unity project or scene purely to test or showcase UTI is
  out of scope — real, substantial time/token cost for very low verification value, and it
  contradicts the whole premise of the Bring-Your-Own-Test Protocol (`DESIGN.md` §12) — we are
  testers using other people's real projects, not developers building our own. Genre coverage
  (car/NPC/projectile) is instead exercised opportunistically via whatever already exists in a real
  consuming project (see `TESTS/TestTracker.md` T08). If ever revisited, it would need to piggyback
  on a project that already exists rather than being built from scratch.

## Naming

"UTI" is a joke and a working title — memorable, a little absurd, very "programmer humor." Folder/namespace uses it for now; can rebrand before any real launch without much friction since it's isolated to this project folder.

## Change Log

- 2026-08-08 — **CI's first live run found and fixed a real bug.** License activation
  (`UNITY_EMAIL`/`UNITY_PASSWORD` via `buildalon/activate-unity-license`) worked on the first try.
  The next step failed: `buildalon/unity-setup`'s own version-detection glob silently skips
  dot-prefixed directories, so it couldn't find `ProjectVersion.txt` while the CI project shell
  lived at `.github/ci-project/`. Moved to `CI~/` (tilde-suffixed, same UPM "don't import this"
  convention `Samples~/` already uses, but visible to a plain glob) — see `DESIGN.md` §4/§12's
  Change Log for the full root-cause writeup. Not yet re-verified with another live run.
- 2026-08-08 — **Project review pass, then follow-up work kicked off.** A full outside review of
  the codebase, file structure, docs, and test/error-handling coverage (code quality strong,
  decoupling and testable/untestable splits real, error-boundary discipline unusually consistent
  for a project this size; weaknesses: no CI, `TESTS/PlayMode/` unpopulated so anything
  Play-Mode-dependent relies on non-repeatable live sessions, doc volume disproportionate to code
  size, git history far less granular than the prose Change Logs it sits alongside) turned into a
  prioritized punch list. Immediate results: new `docs/ONBOARDING.md` (a short, stable map for a
  fresh agent session — file structure, the six components, testing approach, conventions,
  standing rules — distinct from `CLAUDE.md`'s rules and `HANDOFF.md`'s ephemeral state), and a new
  standing practice (`CLAUDE.md`) to commit per logical unit of work going forward instead of
  batching a session into one commit. Also added CI: `.github/workflows/tests.yml` runs the
  EditMode suite via a new minimal, scrubbed project shell (`.github/ci-project/` — see
  `DESIGN.md` §4/§12) on push to `main` — not yet live-verified, needs `UNITY_EMAIL`/
  `UNITY_PASSWORD` secrets added to the repo first. Its license-activation approach changed mid-
  setup after due diligence found the original plan (an exported license file) is machine-bound
  and wouldn't validate on GitHub's runners — see `DESIGN.md` §12's Change Log for the full story.
  Remaining items (closing three "code-reviewed only" error-handling rows, a few small code
  cleanups) tracked as session tasks, to be folded into this Change Log as they land.
- 2026-08-08 — **JSON Lines export Roadmap item marked built and verified live** (moved from
  "Feature ideas" — see the Roadmap section above). Also extracted `BeanFileOutputBase`, a shared
  base class between `CsvBeanOutput` and `JsonlBeanOutput` once their `StreamWriter`-lifecycle code
  turned out to be identical. Verified in `project 2` via direct Unity MCP access: clean compile, 0
  console errors, 97/97 EditMode tests, both before and after the refactor. Full detail in
  `DESIGN.md`'s Change Log and `TESTS/TestTracker.md`.
- 2026-08-08 — **Standing rule: never build our own demo/sample Unity projects to test UTI.**
  Proposed building `Samples~/` car/NPC/player demo scenes to close test-row T08; corrected
  directly: real time/token cost for low verification value, and contradicts the whole premise of
  the Bring-Your-Own-Test Protocol (`DESIGN.md` §12) — we're testers using real projects, not
  developers building our own. The demo-scene idea moved to this file's own Dream To-Do section
  above; T08 repurposed in `TESTS/TestTracker.md`; rule also written into `CLAUDE.md` and session
  memory.
- 2026-08-08 — T12/T14 (§"Robustness fixes" in the Roadmap above) fully closed — the last two
  Play-Mode-only test gaps, live-verified in `project 2` after this session's Unity MCP connection
  turned out to support real Play Mode entry and `GameObject.SetActive` (both hard-blocked in every
  prior session). `EveryFixedUpdate` samples landed exactly on the physics tick; pooled-object
  `SetActive` reuse correctly truncated (default) and correctly accumulated (`AppendAcrossReuse`)
  across a real reuse cycle. Pure verification, no behavior changed. Full detail in
  `TESTS/TestTracker.md`'s Change Log.
- 2026-08-08 — Sharpened the JSON export Feature idea (JSON Lines format, structured `extras` as
  the real motivation) and added a new **Dream To-Do** section — bigger, further-out, not-MVP
  concepts distinct from the regular Roadmap — seeded with a 3D-explorable-scene-artifact idea per
  user request, deliberately not scoped or started.
- 2026-08-08 — **Went public: repo created at github.com/DataFright/Unity-Testing-Inspector, MIT
  licensed, first commit pushed.** Reorganized the file structure for a real public repo: this file
  is the former `README.md`, renamed to `PROJECT_OVERVIEW.md` and moved into a new `docs/` folder
  alongside `DESIGN.md`, `HANDOFF.md`, and the three end-user docs — the root `README.md` is now a
  short public pitch + a real fresh-clone install guide (Package Manager git URL, replacing the old
  hardcoded local `file:` path that only ever worked on one machine). `BeanConfig
  .CopyEndUserDocsIfMissing()` updated to match (source path now `docs/<filename>`), verified live.
- 2026-08-08 — **Real bug found by the `project 2` team's fresh-install round, root-caused live and
  fixed same day (T28, `TESTS/TestTracker.md`).** `BeanSnapshotExporter` frames/draws from the live
  sample buffer, not the CSV — a long idle tail after real movement finished can silently evict the
  entire recorded path from that fixed-capacity buffer before a snapshot happens, producing an
  invisible path line and a tight, context-free close-up despite real movement having occurred.
  Reproduced directly (not just theorized): 200 samples of real 9m movement + 3000 stationary
  samples dropped the live buffer's recorded span to exactly zero. Fixed with a console warning
  when this could be happening; docs updated with the symptom and fix.
- 2026-08-08 — Noted a new Feature idea (a more dynamic/cinematic snapshot angle — a closer,
  elevated chase-cam-style shot oriented toward where the tracked object is heading, distinct from
  the whole-path-fitting `Above`/`Side`/`Behind`/`Auto` built earlier the same day), from a real
  Scene-view reference screenshot the user shared. Per user request, not built.
- 2026-08-08 — New `TESTS/ErrorHandlingTracker.md` tracks every guarded system boundary the same
  way `TESTS/TestTracker.md` tracks features. Also fixed a bug reported by the `project 2` team: a
  second occurrence of the ambiguous-`Object`-reference `CS0104` compile error (two test files
  needing `using System;` for this round's new tests) — already fixed as a side effect earlier the
  same session, confirmed via a full-repo sweep, tracked going forward instead of left reactive.
- 2026-08-08 — First live-verified round: this session's Unity MCP connection turned out to be
  attached directly to `project 2` with real script execution (not just relay/read-only access like
  every prior round). Closed almost this round's entire punch list for real — full 84/84 EditMode
  suite passed, T22/T23/T24 confirmed live (T23 with real before/after PNGs showing the close-up bug
  is genuinely fixed), and a brand-new bug (T26: leaked GameObjects from calling a capture outside
  Play Mode) was found and fixed by this same live testing. See `TESTS/TestTracker.md`'s Change Log
  for the full story, including what this connection can't do (Play Mode entry, `SetActive`).
- 2026-08-08 — Integrity pass: guarded every real system boundary UTI touches with no crash-the-
  game risk left unhandled — a throwing `CustomCapture` delegate, a `BeanLogger` output that fails
  to open/write/close (disk full, permissions), a `BeanSnapshotExporter` multi-angle capture where
  one angle's file write fails, and a locked/unreadable `BeanConfig.txt`. Each now degrades
  gracefully (logs a warning, keeps everything else working) instead of propagating an unhandled
  exception into whatever Unity callback triggered it. New tests cover the testable cases
  (`BeanTracker`/`BeanLogger`); see `DESIGN.md` §14 for the full boundary-by-boundary writeup.
- 2026-08-08 — Closed most of the punch list left by the `project 2` round below in one pass, all
  code-complete and unit-tested but **not yet live-verified** (see `TESTS/TestTracker.md`'s updated
  relay prompt): T23 fixed (`MinFramingRadius` → `BeanConfig.DefaultMinFramingRadius`); multi-angle
  snapshots built (`CaptureAngles`: `Auto`/`Above`/`Side`/`Behind`, grouped naming settled); T11
  (`CustomCapture`/extras end-to-end), T12 (`EveryFixedUpdate` via new `SimulateFixedFrame()`), and
  T15's EditMode half (multi-Bean independence) all closed with new tests; T14 decided and built
  (`BeanLogger.AppendAcrossReuse`, opt-in, off by default). Also: new `UTI > Setup Project (Config
  + Docs)` Editor menu item bootstraps `BeanConfig.txt` *and* copies the three end-user docs into a
  project's own `UTI/` folder in one step, so that gap (flagged again this round from `project 2`)
  becomes a one-time fix instead of a per-project recurrence. Found and fixed a real doc bug while
  restoring `USAGE.md`/`READING_LOGS_AND_VISUALS.md`/`CONFIG.md` (which turned out to exist only as
  copies inside `little wings`, not in this package repo itself): `USAGE.md` §8 had gone stale,
  still describing the old `ScriptableObject`-based `BeanConfig` after it was rebuilt as a plain
  text file. All three docs refreshed in both `little wings` and `project 2`.
- 2026-08-08 — First full closing report from `project 2` (see `TESTS/TestTracker.md` Change
  Log for the complete story): UTI's CSV decisively pinned down a real game bug (a jump-trigger
  distance geometrically unreachable given the player's own collision radius) to five decimal
  places — the dev said directly they wouldn't have found it from behavior alone. Also surfaced a
  real, unfixed bug (T23: auto-framing produces a useless close-up on a near-stationary path) and
  confirmed a known doc gap (the three end-user docs still aren't copied into `project 2`'s own
  `UTI/` folder). Added two new Feature ideas per user request: multi-angle snapshots with
  grouped/labeled naming, and `BeanConfig` covering snapshot-quality settings like
  `MinFramingRadius` (would directly address T23). Neither built yet.
- 2026-08-07 — Sharpened the input-tracking Feature idea with a concrete motivating case (a Unity
  Test Framework test that "should click" but fails — confirming whether the click actually fired
  narrows down where the real bug is) and tied it explicitly to a restated Design Philosophy
  point: UTI empowers/narrows down, it doesn't diagnose or fix root causes itself. Added a new
  Example Use Case, "Complements automated test tooling," to match. Not built — still just a
  sharpened note.
- 2026-08-07 — Noted a new Feature idea (input tracking beyond mouse position — keyboard/click
  events, not just continuous cursor position) per user request, not built. Also added a Roadmap
  strategy note: the upcoming `project 2` real-bug-diagnosis round is meant to double as a source
  for future roadmap items, not just a feature test.
- 2026-08-07 — `BeanConfig` rebuilt as a plain text file (`<project root>/UTI/BeanConfig.txt`,
  bootstrapped via a new "UTI > Create Bean Config" Editor menu item) instead of a
  `ScriptableObject` asset in `Assets/UTI/`, after pushback that config should live in the same
  place as the rest of a project's UTI footprint, not split into `Assets/`. `CONFIG.md` moves to
  the same `<project root>/UTI/` copy convention as `USAGE.md`/`READING_LOGS_AND_VISUALS.md`.
- 2026-08-07 — `USAGE.md`/`READING_LOGS_AND_VISUALS.md` copied into `little wings`'s own
  `<project root>/UTI/` folder, per feedback that these are end-user docs (for a dev using UTI,
  not for developing UTI) and belong where that dev is actually looking, not just in this package
  repo. `project 2` still needs the same copy step next round.
- 2026-08-07 — After clarification that "give the dev the ability to choose and change stuff"
  meant a centralized settings file, not per-Bean fields (or a new capture mode — reverted
  `EveryNTicks`, added earlier the same session, once that became clear): new `BeanConfig`
  holding preferred defaults for `BeanTracker`/`BeanSnapshotExporter`, applied automatically to
  any *newly added* Bean via Unity's `Reset()` hook. New `CONFIG.md` explains it, meant to be copied alongside the
  asset into each game project.
- 2026-08-07 — T13/T16/T17 verified Pass live in `little wings` (25/25 tests, real PNG+CSV
  confirmed on the filesystem under the new `UTI/` folder). Three additions per user feedback: (1)
  `BeanSnapshotExporter` gained `DimensionMode` (`Auto`/`Force2D`/`Force3D`) so the flat/2D-vs-3D
  framing decision can be overridden instead of only auto-guessed. (2) Fixed a real bug the same
  verification round found: the auto-frame camera's offset direction was a fixed diagonal, which
  foreshortened into an unreadable stripe whenever a path happened to travel roughly parallel to
  it — now derived from the path's own travel direction (always broadside) instead. (3) New
  `BeanMouseTracker` — a small proxy that drives its own Transform to follow the mouse cursor
  (screen- or world-space), so a normal `BeanTracker` on the same object tracks mouse input
  through the exact same pipeline as any other Bean. Also added two new docs, `USAGE.md` and
  `READING_LOGS_AND_VISUALS.md`, covering setup and how to interpret UTI's output. None of today's
  new code has a real Play Mode verification yet.
  Next up: testing in `project 2` (a vertical platformer — jump-to-survive-rising-lava — chosen
  specifically because it's a better stress test for `BeanVisualizer`/`BeanSnapshotExporter` than
  `little wings`' open sky, and directly exercises the new broadside-framing fix on largely
  vertical paths).
- 2026-08-07 — Fixed a relayed compile error (`CS0619`, `GetInstanceID()` reported as
  obsolete-as-error against this project's Unity version) by swapping the filename-uniqueness key
  from `GameObject.GetInstanceID()` to a random GUID-fragment token instead — sidesteps needing to
  verify that specific deprecation claim, and works regardless of whether it was real.
- 2026-08-07 — Refined the output-location fix once more per feedback: everything UTI generates
  now nests under one shared `UTI/` folder at the project root (`UTI/BeanLogs/`,
  `UTI/BeanSnapshots/`) instead of two loose sibling folders. Also closed the CSV filename
  collision gap (see Robustness fixes below) to match the PNG side, and confirmed CSV/console
  output are already plain text, so both a human and an AI reading the file directly can parse
  them without extra tooling — only the PNG snapshot needs vision to interpret.
- 2026-08-07 — Moved UTI's default output location for both `BeanLogger` and
  `BeanSnapshotExporter` off `Application.persistentDataPath` (a hidden per-user AppData folder,
  unrelated to the project directory) and onto the project root instead (`BeanLogs/`/
  `BeanSnapshots/` subfolders) — found when the generated snapshot from the round below turned out
  to be undiscoverable in the actual project folder. Also fixed the auto-framed shot's path line
  being invisible (width wasn't scaled to the pulled-back camera distance). Neither fix verified
  with a real capture yet.
- 2026-08-07 — `BeanSnapshotExporter` verified end-to-end in little wings (T16/T17): a real capture
  showed a correct path line over real scene geometry, no shader/render-pipeline issues. The first
  capture was framed too tight to actually be useful, which prompted same-session fixes: a
  dedicated `BeanSnapshots/` output folder, timestamp-prefixed filenames so repeat runs on the same
  Bean can be compared instead of overwriting each other, and camera auto-framing (orthographic
  for flat/2D paths, elevated 3/4 perspective for real 3D ones) sized to the whole recorded path.
- 2026-08-07 — Persisted visualization artifact: decision made and built. New `BeanSnapshotExporter`
  renders the tracked path through a real Camera (not gizmos) into a saved PNG, chosen specifically
  because a static path-only plot can't show surrounding scene geometry (the floor/walls a dev
  needs to actually debug with — e.g. seeing why something fell through the floor), and a real
  camera render captures that automatically. Code-complete, pure-logic unit tests passing; one real
  Play Mode capture (T17) still needed to confirm the rendered image looks right. See `DESIGN.md`
  §8.4.
- 2026-08-07 — Elevated the "no persisted visualization artifact" gap from a buried Roadmap bullet
  ("Replay") to its own flagged section at the top of the Roadmap — a real human developer using
  UTI to debug their game currently has no way to review `BeanVisualizer`'s path after the fact,
  since it's live-Editor-Gizmos-only. Surfaced by user feedback while chasing T05's repeated
  screenshot-tooling failures. Not implemented yet — tracked for a real design pass, not built now.
