# UTI — Project Overview, Roadmap & Full History
*(working title — funny on purpose, renameable at launch)*

**New here? Start at the [root README](../README.md)** — it has the short pitch and the actual
install steps. This file is the deeper, ongoing project record: the pitch/concept and the current
Roadmap. [USAGE.md](./USAGE.md) covers install/setup in end-user terms,
[READING_LOGS_AND_VISUALS.md](./READING_LOGS_AND_VISUALS.md) explains how to interpret what UTI
produces, [CONFIG.md](./CONFIG.md) covers project-wide defaults, and [DESIGN.md](./DESIGN.md) is the
architecture. **The complete Change Log since day one, and the full story behind every shipped
Roadmap item, lives in [PROJECT_OVERVIEW_HISTORY.md](./PROJECT_OVERVIEW_HISTORY.md)** (public — real
project provenance) — this file tracks the pitch and what's still open, not the full history.

## Elevator Pitch

Drop a **Bean** on any GameObject and watch where it actually goes. UTI is a lightweight, drop-in Unity toolkit for tracking, logging, and visualizing movement over time — for anything: a player, a car, an NPC, an AI ally, a projectile, a plane, a physics prop. No test framework to learn, no assertions to write. Just attach it, play, and look at the trail.

## The Problem

Unity Test Framework is great for code-level assertions (did this function return the right value), but it's clunky for verifying *behavior over time* — "did this thing end up roughly where it should have, following roughly the path it should have, in roughly the time it should have." That kind of thing usually gets checked by eyeballing the Game view once and hoping, because building custom logging/visualization for it is annoying enough that most people skip it.

UTI closes that gap by making behavior visible: attach a Bean, hit play, and see the trail.

## Core Components (v1 scope)

- **BeanTracker** — attach to any GameObject. Captures position (x/y/z), rotation, and other configurable fields on an interval (every update / every FixedUpdate / every N seconds — user's choice). Just captures data, doesn't do anything with it. Works the same whether it's on a player, a car, an NPC, an AI ally, a projectile, or a prop — or, via `BeanMouseTracker`, the mouse cursor itself.
- **BeanLogger** — takes what BeanTracker captures and outputs it (console, CSV/JSON Lines file, in-memory buffer, or a custom `IBeanOutput` sink). Decoupled from the tracker so people can swap or extend output without touching capture logic.
- **BeanVisualizer** — plots the captured movement back into the Scene view as a path/trail (gizmo-drawn line, points per tick, maybe color-coded by time or speed). Live, during Play Mode only.
- **BeanSnapshotExporter** — a fourth, decoupled piece: renders the recorded path through a real Camera into a saved PNG, so there's a durable artifact to review *after* the run ends, not just a live gizmo. See `USAGE.md`/`READING_LOGS_AND_VISUALS.md` for the full field reference on all four.

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

**Real validation, not just theory:** `astro aces` used a `CustomCapture` adapter logging mouse
delta/aim-error per physics tick to close a genuinely months-old, previously-unreproducible
flight-aim bug (2026-08-22) — tick-by-tick CSV resolution pinpointed one corrupted reading on the
exact same physics step every time, something no amount of screenshots or verbal bug reports had
surfaced. Their own words: "decisively... the reason this bug is closed at all." Full report:
`TESTS/BugTracker.md` BUG-12 and the Roadmap items above it note the real gaps their session also
surfaced.

## Roadmap (rough, unordered)

**Already shipped** (full build/verification story for each: `DESIGN.md` and
`PROJECT_OVERVIEW_HISTORY.md`): the persisted-artifact feature (`BeanSnapshotExporter`), unique
default filenames, a deliberate object-pooling answer (`BeanLogger.AppendAcrossReuse`),
`EveryFixedUpdate` test coverage, full `CustomCapture`/`extras` test coverage, JSON Lines export,
multi-angle snapshots, and `BeanConfig` covering snapshot-quality settings. Don't re-propose these.

**Promoted — near-term priority:** these five move ahead of the rest of the Roadmap. Four promoted
2026-08-22 per direct user request (one was suspected to already be covered by shipped features —
checked against the actual current source, not memory/docs, before promoting); the fifth
(`BeanMouseTracker`'s Input System gap) added the same day after a second independent team hit it.

- **A closer, "chase-cam" snapshot angle, distinct from `Above`/`Side`/`Behind`.** Those three are
  all designed to fit the *whole recorded path* with margin — useful for "show me the route," less
  useful for "show me this character and roughly where they're facing/heading" at a glance. Idea: a
  closer, elevated-behind-and-angled-down shot — like a third-person chase/follow camera — framed on
  the tracked object itself (near its final or a representative position) rather than stretched to
  cover the entire path's bounding box. **Confirmed NOT already achievable via existing config**, via
  full read of `Runtime/BeanSnapshotExporter.cs` (2026-08-22): `ComputeAboveFraming`/
  `ComputeSideFraming`/`ComputeBehindFraming` all compute `position = bounds.center + offsetDirection
  * distance`, where `bounds` is the whole path's bounding box and `distance` scales off
  `bounds.extents.magnitude` — none of the three targets the object's own current/final position.
  `minFramingRadius` only sets a floor on that distance for near-stationary paths; it doesn't change
  the framing *target*. Would likely be a new `BeanSnapshotAngle` value (naming TBD). Not designed,
  not started.
- **Optional validator/assert layer, opt-in.** Concrete framing from a 2026-08-22 user idea: a
  simple config-based condition — e.g. "Bean X should be at position Y by time T" — checked against
  the already-captured log after a run, pass/fail. Would make UTI read more like a genuine framework
  without forcing it: still opt-in, still built on the existing capture/log pipeline rather than a
  new required step, so it doesn't contradict the "empowerment, not solutions" philosophy above as
  long as it stays that way. Worth a real design pass before any code — where the assertion config
  lives, what the pass/fail output actually looks like (console line? separate report file?), and
  how it interacts with `BeanConfig`.
- **Input tracking beyond mouse position** — `BeanMouseTracker` only captures where the cursor *is*,
  not raw input events. A `Bean`-style way to log keyboard key down/up, mouse click down/up, and
  similar discrete input events would round this out. Concrete motivating case: a Unity Test
  Framework (or similar automated) test scripts a character that "should click," and the test
  fails — an input-event log lets a dev directly confirm whether the click genuinely fired,
  separating "game logic is broken" from "the input never fired" from "something in the test
  harness itself is interfering." Squarely in the "empower, don't solve" lane (see Design
  Philosophy) — UTI would prove *whether* the click fired, not diagnose or fix why it didn't if it
  fired-but-failed downstream. Same legacy-Input-Manager-vs-Input-System dependency question
  `BeanMouseTracker` already had to answer (see `DESIGN.md` §8.6) would need revisiting for
  discrete events specifically. Not started.
- **`BeanMouseTracker` should actually work on New-Input-System-only projects, not just degrade
  gracefully.** Distinct from the discrete-input-events item above — this is about completing
  existing functionality (cursor *position* tracking), not adding a new capability. BUG-05's fix
  (`v0.2.0`) stopped it from throwing, but it still can't read the mouse at all in that config — it
  reads legacy `Input.mousePosition` only, by deliberate design (see `DESIGN.md` §8.6), to avoid a
  hard dependency on the Input System package. **Real corroborating demand from two independent
  teams now:** `little wings` first, then `astro aces` (2026-08-22) — who had to write their own
  `Mouse.current`-based capture from scratch mid-debugging-session because `BeanMouseTracker` simply
  wasn't usable for their actual bug. Would need a `Unity.InputSystem`-referencing code path gated
  behind `ENABLE_INPUT_SYSTEM` (only defined when that package is actually installed, so it wouldn't
  reintroduce the hard dependency UTI avoided) — same asmdef `versionDefines` mechanism flagged as
  unverifiable without a live Editor back when BUG-05 was first fixed. Not designed, not started.
- **Safer guidance (or a code-level accommodation) for adding Beans to DOTS/Netcode-for-Entities
  subscene-baked "ghost" prefabs — raised in priority as real online-game integration coverage, not
  just a niche pipeline quirk.** Found in `bitshot` (a Bring-Your-Own-Test round): adding
  `BeanTracker`/`BeanVisualizer` directly to a networked ghost prefab *asset* left the real gameplay
  components on that same prefab never activating — a subscene re-bake the edit didn't trigger, a
  Netcode-for-Entities/DOTS pipeline detail specific to how that project bakes prefabs, not anything
  UTI's own code does. **Confirmed not a UTI defect** — the immediate fix needs zero code changes and
  already works: add Beans at runtime to an already-spawned instance instead of editing the shared
  prefab asset. Two real follow-ups: (1) document this pattern explicitly for anyone on an
  ECS/DOTS/networked-prefab pipeline; (2) genuinely open whether a low-effort code-level
  accommodation is worth adding — worth a proper look now given real interest in verifying UTI
  against actual online/multiplayer games, not just single-player genres. Scope check: this is
  specifically a DOTS/Netcode-for-Entities subscene-baking quirk, not "UTI doesn't support
  multiplayer" broadly — a GameObject-based netcode stack (Netcode for GameObjects, Mirror, Photon)
  wouldn't hit this failure mode at all. Full incident writeup: `TESTS/TestTracker_HISTORY.md`.

**Still open, no near-term priority:**

- Configurable capture fields beyond transform (velocity, custom component values via
  delegate/callback).
- Categorical/string-valued extras (or a documented convention for encoding state like an AI's
  current behavior — patrol/chase/attack — as a float), since `extras` today is numeric-only.
- Replay: play back a recorded run visually, not just as a static path.
- **A runtime (non-Editor-only) trail option** — e.g. `LineRenderer`-drawn — so a path is visible in
  the actual Game view/builds, not just the Scene view via gizmos. **Confirmed NOT already covered
  by `BeanSnapshotExporter`'s PNGs**, via full read of `Runtime/BeanVisualizer.cs` (2026-08-22): its
  only draw calls are `Gizmos.DrawLine`/`Gizmos.DrawSphere` inside `OnDrawGizmos()`/
  `OnDrawGizmosSelected()` — no `LineRenderer`, no other runtime-visible rendering path anywhere in
  the file. Gizmos are structurally Scene-view-only (never appear in the Game view or in a build),
  and `BeanSnapshotExporter` is a static, after-the-fact PNG, not a live trail — neither one is what
  this item describes. Could also let a project's own in-game map/minimap read from
  `BeanTracker.Samples`/`OnSample` directly, without UTI needing to know that system exists (keeps
  the "no required dependencies" promise intact). **De-prioritized back off near-term 2026-08-22**,
  same day it was promoted — direct user call, not a re-evaluation of its merit. Not designed, not
  started.

**Broader validation, partially done:** many-simultaneous-tracked-objects and a genre check that's
actually projectile/AI-heavy rather than flight/platformer/2D-generic. **Partial:**
independent-buffers is EditMode-tested (several `BeanTracker`s driven at once, no shared state) —
the gizmo-draw-cost-at-realistic-counts half (a wave of enemies, a screen full of bullets) is still
a live Play Mode check.

## Testing coverage — where a real project would help

Per the Bring-Your-Own-Test Protocol (`DESIGN.md` §12), UTI is verified against real, already-working
projects — never a scene built just for UTI's own benefit. The genre/scenario gaps below are real,
open validation needs; if you're building something in your own time that happens to fit one, that's
genuinely useful test coverage, not a detour. This is exactly what the Protocol asks for: a real
project someone was already building for its own sake, not a demo scene built to order.

- **Vehicle/car handling** — confirming `BeanTracker`/`BeanVisualizer` hold up against a real
  physics-driven car controller (T08).
- **NPC/AI-nav** — a patrol/chase/attack-style AI, ideally one that would exercise the
  categorical-state question `extras`' numeric-only limitation already flags.
- **Projectile-heavy** — a shooter/bullet-hell-style game, for the still-partial
  many-simultaneous-tracked-objects validation (gizmo draw cost with a screen full of bullets).
- **A genuine online/multiplayer game using Netcode-for-Entities (DOTS), with subscene-baked "ghost"
  prefabs** — directly needed for the newly-promoted DOTS/Netcode Roadmap item above. The `bitshot`
  finding that motivated it was a one-off Bring-Your-Own-Test round, not an ongoing test bed; a real
  project on this stack would let that item actually move past "documented workaround" into
  confirmed, regularly-verified behavior.

## Dream To-Do

Bigger, further-out ideas — not MVP, not scoped, not committed to. These are concepts worth
remembering, not a plan. Nothing here should be started without a real design pass first.

- **Load/performance testing — does running UTI meaningfully slow a project down for the dev using
  it?** Direct user idea, 2026-08-22. Broader than the existing gizmo-draw-cost-at-scale check above
  (which is narrowly about `BeanVisualizer`'s Scene-view line drawing) — this is about UTI's overall
  overhead (capture interval cost, buffer memory, multiple simultaneous outputs) in a real project,
  not just one component's worst case. Not scoped: would need a real project with a meaningful
  object count to test against, same Bring-Your-Own-Test constraint as everything else here.
- **A "flag the tick where field X changes discontinuously" helper.** Nice-to-have idea from
  `astro aces`' report (2026-08-22): they manually `grep`/`awk`'d a CSV to find the exact tick where
  a `CustomCapture` value spiked, which cracked a months-old bug. Wasn't hard to do by hand this
  time, but a small helper to scan a captured log/buffer for the first discontinuous jump in a named
  field could save that manual step. Related to the promoted assert/validator layer above (both are
  about *analyzing* an already-captured run) but distinct in purpose — an assert checks a known
  expected condition, this would surface an *unknown* anomaly. Not scoped, not designed.
- **A 3D-explorable scene artifact, not just a fixed-angle snapshot.** Instead of (or alongside)
  `BeanSnapshotExporter`'s flat PNG, export something a dev can actually rotate/pan/zoom through
  after the fact. Would completely solve the "one angle doesn't show everything" problem multi-angle
  capture only partially addresses, in a much more open-ended way. **Real cost, why this is a dream
  and not a roadmap item:** means exporting and simplifying real scene geometry to a portable format
  (decimated/low-poly, not a full scene dump), then either relying on an external 3D viewer or
  building/embedding one — a genuinely different category of feature, closer to a second product
  bolted onto UTI than an extension of the existing snapshot pipeline. Directly conflicts with the
  stated "deliberately basic, not polished" philosophy behind `BeanSnapshotExporter` unless scoped
  very carefully. Needs a real design pass (export format, geometry simplification approach, viewer
  story) before any code.
- **Demo/onboarding sample scenes** (car, NPC, player) under a `Samples~/` folder. Demoted here after
  being proposed and corrected: building our own demo/sample Unity project or scene purely to test
  or showcase UTI is out of scope — real, substantial time/token cost for very low verification
  value, and it contradicts the whole premise of the Bring-Your-Own-Test Protocol (`DESIGN.md` §12)
  — we are testers using other people's real projects, not developers building our own. Genre
  coverage is instead exercised opportunistically via whatever already exists in a real consuming
  project (see `TESTS/TestTracker.md` T08). If ever revisited, it would need to piggyback on a
  project that already exists rather than being built from scratch.
- **An editor-only test utility for toggling Active Input Handling programmatically.** Surfaced by
  the `little wings` team while verifying BUG-05's legacy-input branch (`TESTS/TestTracker.md` T21):
  no public Unity scripting API exists for this setting (`PlayerSettings.GetSerializedObject()` is
  `internal`), so they reached it via reflection into that internal method, then the public
  `SerializedObject`/`SerializedProperty` API from there — worked reliably, but is uglier than
  wanted for something that might need to happen more than once. Would only be worth building if
  `BeanMouseTracker`'s legacy-input path ends up needing *routine* regression coverage rather than
  the one-off manual check it's had so far — not true today. If that need materializes, wrap the
  same reflection trick into a small internal editor utility instead of reflecting into it ad hoc
  each time.

## Naming

"UTI" is a joke and a working title — memorable, a little absurd, very "programmer humor." Folder/namespace uses it for now; can rebrand before any real launch without much friction since it's isolated to this project folder.

The spelled-out name is **"Unity Testing Inspector"** — matches the live GitHub repo
(`github.com/DataFright/Unity-Testing-Inspector`) and every current file. (An earlier "Isolator"
naming drift across several files was found and standardized — see `PROJECT_OVERVIEW_HISTORY.md` if
curious.)

## Change Log

- 2026-08-22 15:58 — First report from a new team, `astro aces`: real validation (a months-old
  flight-aim bug closed via `CustomCapture`), plus three real findings — promoted a fifth Roadmap
  item (`BeanMouseTracker`'s Input System gap, now corroborated by two teams), logged BUG-12
  (`package.json`'s `unity: 6000.5` floor looks arbitrary, not yet safe to lower), and added a
  Dream To-Do idea for a discontinuity-detection log helper. Full detail: `TESTS/BugTracker.md`
  BUG-05/BUG-12.
- 2026-08-22 12:08 — **Version bump: 0.2.1 → 0.2.2.** `little wings` ran BUG-06's new EditMode
  tests live against `v0.2.1`; one failed due to a test-ordering bug (fixed) rather than the
  runtime fix itself. Also ships BUG-11 (two doc `.meta` files that existed on disk but were never
  committed, causing a real Unity import warning on every fresh install). Every install example
  updated to the new tag.
- 2026-08-22 10:51 — **Version bump: 0.2.0 → 0.2.1**, shipping BUG-06's fix
  (`BeanLogger.OutputTargets` now re-opens on an actual change instead of silently no-op'ing).
  Every install example across `README.md`/`docs/USAGE.md`/`TESTS/TestTracker.md` updated to the
  new tag.
- 2026-08-22 10:33 — Runtime trail de-prioritized back off the near-term-priority tier (moved back
  to general Roadmap), per direct user call the same day it was promoted. The other four promoted
  items are unaffected.
- 2026-08-22 10:20 — **Five Roadmap items promoted to near-term priority**, per direct user
  request: runtime trail, chase-cam snapshot angle, the assert layer, input tracking beyond mouse,
  and DOTS/Netcode ghost-prefab guidance (raised further as real online-game coverage, not just a
  niche quirk). Two of the five were suspected already-covered by shipped features — checked against
  the actual current source first and confirmed genuinely unbuilt before promoting (see the Roadmap
  section for the exact code citations). Added a "Testing coverage" section inviting real projects
  in the genres/scenarios still needed, and a Dream To-Do entry for general load/performance testing.
- 2026-08-22 09:08 — Refined the existing "optional validator/assert layer" Roadmap item with a
  concrete framing from a direct user idea: a simple config-based position/time condition, checked
  against the captured log after a run. Still Roadmap "Still open," not started.
- 2026-08-21 20:35 — **BUG-05 closed** (`BeanMouseTracker` throwing on New-Input-System-only
  projects, shipped fixed in `v0.2.0`) — both branches now verified live, the second by the
  `little wings` team directly after migrating to the tag-pinned install. Added a Dream To-Do entry
  for a possible future editor-only Active-Input-Handling test utility, surfaced by their report.
  Full detail: `TESTS/BugTracker.md` BUG-05.
- 2026-08-21 19:32 — **First real version bump: 0.1.0 → 0.2.0** (had been unbumped since the
  initial commit despite everything shipped since). Paired with a policy change, per direct user
  instruction: every consuming/test project now installs from the GitHub URL pinned to a release
  tag, never a local `"file:"` path — the tag pin is what makes "what version is this project
  running" a real, checkable question. `README.md`/`docs/USAGE.md` updated to match; full reasoning
  in `docs/DESIGN.md`'s Target Environment table and `CLAUDE.md`'s "Public repo" note.
- 2026-08-11 09:41 — **Restructured for a full docs condense pass**: moved the "already shipped"
  Roadmap narrative (persisted-artifact decision, robustness fixes, the JSON export story) and the
  full historical Change Log to `PROJECT_OVERVIEW_HISTORY.md`, leaving this file as current
  pitch/Roadmap/Naming only. See that file's Change Log for the full writeup covering every doc
  touched in this pass (`CLAUDE.md`, `docs/DESIGN.md`, `TESTS/TestTracker.md`, and this file, each
  now paired with an adjacent `_HISTORY.md`).
- 2026-08-09 21:52 — Added a Roadmap "Feature ideas" entry for DOTS/Netcode-for-Entities
  ghost-prefab compatibility, per direct user request to track it as a future TODO. Confirmed not a
  current UTI defect. Full incident writeup: `TESTS/TestTracker_HISTORY.md`.
- 2026-08-09 10:05 — Full outside project review: graded B+ overall — strong marks for
  architecture/decoupling/error-handling discipline and the honesty of Pass/Planned tracking, held
  back by doc volume disproportionate to actual git history (the finding this restructure pass
  addresses), `BeanVisualizer`'s then-still-unconfirmed render (T05, since resolved), and thin
  multi-project validation. Two findings acted on immediately: the Isolator/Inspector naming drift
  fixed; CI's `cache-installation` speedup wired in (later reverted — see `DESIGN_HISTORY.md`).
- 2026-08-08 22:50 — CI confirmed fully green — first real, verified pass end to end. Full detail:
  `DESIGN_HISTORY.md`.
- 2026-08-08 19:00 — Project review pass, then follow-up work kicked off: new `docs/ONBOARDING.md`,
  a new commit-per-logical-unit standing practice, and CI added. Full detail:
  `PROJECT_OVERVIEW_HISTORY.md`.

Full Change Log since day one, and the complete story behind every shipped Roadmap item:
`PROJECT_OVERVIEW_HISTORY.md`.
