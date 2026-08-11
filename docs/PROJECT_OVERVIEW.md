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

## Roadmap (rough, unordered)

**Already shipped** (full build/verification story for each: `DESIGN.md` and
`PROJECT_OVERVIEW_HISTORY.md`): the persisted-artifact feature (`BeanSnapshotExporter`), unique
default filenames, a deliberate object-pooling answer (`BeanLogger.AppendAcrossReuse`),
`EveryFixedUpdate` test coverage, full `CustomCapture`/`extras` test coverage, JSON Lines export,
multi-angle snapshots, and `BeanConfig` covering snapshot-quality settings. Don't re-propose these.

**Still open:**

- Configurable capture fields beyond transform (velocity, custom component values via
  delegate/callback).
- Categorical/string-valued extras (or a documented convention for encoding state like an AI's
  current behavior — patrol/chase/attack — as a float), since `extras` today is numeric-only.
- Replay: play back a recorded run visually, not just as a static path.
- Optional validator/assert layer, opt-in, once the core kit has legs.
- A runtime (non-Editor-only) trail option — e.g. `LineRenderer`-drawn — so a path is visible in
  the actual Game view/builds, not just the Scene view via gizmos. Could also let a project's own
  in-game map/minimap read from `BeanTracker.Samples`/`OnSample` directly, without UTI needing to
  know that system exists (keeps the "no required dependencies" promise intact).
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
- **A more "dynamic"/cinematic snapshot angle, distinct from `Above`/`Side`/`Behind`.** Those three
  are all designed to fit the *whole recorded path* with margin — useful for "show me the route,"
  less useful for "show me this character and roughly where they're facing/heading" at a glance.
  Idea: a closer, elevated-behind-and-angled-down shot — like a third-person chase/follow camera —
  framed on the tracked object itself (near its final or a representative position) rather than
  stretched to cover the entire path's bounding box. Would likely be a new `BeanSnapshotAngle`
  value (naming TBD). Not designed or started — explicitly not meant to be built yet.
- **Safer guidance (or a code-level accommodation) for adding Beans to DOTS/Netcode-for-Entities
  subscene-baked "ghost" prefabs.** Found in `bitshot` (a Bring-Your-Own-Test round): adding
  `BeanTracker`/`BeanVisualizer` directly to a networked ghost prefab *asset* left the real gameplay
  components on that same prefab never activating — a subscene re-bake the edit didn't trigger, a
  Netcode-for-Entities/DOTS pipeline detail specific to how that project bakes prefabs, not
  anything UTI's own code does. **Confirmed not a UTI defect** — the immediate fix needs zero code
  changes and already works: add Beans at runtime to an already-spawned instance instead of editing
  the shared prefab asset. Two real follow-ups, neither started: (1) document this pattern
  explicitly for anyone on an ECS/DOTS/networked-prefab pipeline; (2) genuinely open whether a
  low-effort code-level accommodation is worth adding. Scope check: this is specifically a
  DOTS/Netcode-for-Entities subscene-baking quirk, not "UTI doesn't support multiplayer" broadly —
  a GameObject-based netcode stack (Netcode for GameObjects, Mirror, Photon) wouldn't hit this
  failure mode at all. Full incident writeup: `TESTS/TestTracker_HISTORY.md`.

**Broader validation, partially done:** many-simultaneous-tracked-objects and a genre check that's
actually projectile/AI-heavy rather than flight/platformer/2D-generic. **Partial:**
independent-buffers is EditMode-tested (several `BeanTracker`s driven at once, no shared state) —
the gizmo-draw-cost-at-realistic-counts half (a wave of enemies, a screen full of bullets) is still
a live Play Mode check.

## Dream To-Do

Bigger, further-out ideas — not MVP, not scoped, not committed to. These are concepts worth
remembering, not a plan. Nothing here should be started without a real design pass first.

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

## Naming

"UTI" is a joke and a working title — memorable, a little absurd, very "programmer humor." Folder/namespace uses it for now; can rebrand before any real launch without much friction since it's isolated to this project folder.

The spelled-out name is **"Unity Testing Inspector"** — matches the live GitHub repo
(`github.com/DataFright/Unity-Testing-Inspector`) and every current file. (An earlier "Isolator"
naming drift across several files was found and standardized — see `PROJECT_OVERVIEW_HISTORY.md` if
curious.)

## Change Log

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
