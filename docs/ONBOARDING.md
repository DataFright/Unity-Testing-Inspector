# UTI — Onboarding

Read this first if you're a new agent session picking up work on UTI itself (not a consuming
game project). This is a map, not a chronicle — it points you at the right doc for whatever
you need instead of repeating their content. It should stay short and rarely need edits; the
day-to-day history lives in the docs it points to, not here.

## Read order for a fresh session

1. **This doc** — orientation.
2. **`CLAUDE.md`** (project root, local-only — won't exist on a machine that only cloned the
   public repo) — the standing behavioral rules for working on this project. Read it in full;
   it overrides default behavior.
3. **`docs/HANDOFF.md`** (local-only, same as above) — what the last session actually did and
   whether anything's paused mid-task. Usually short; sometimes says "clean, nothing open."
4. Then whatever the actual task needs — see "Where to look for X" below.

If `CLAUDE.md`/`HANDOFF.md` aren't on disk, you're working from a fresh clone of the public
repo — everything you need is still in `docs/`, `TESTS/`, and the code itself.

## What UTI is, in three sentences

UTI is a lightweight Unity Package Manager package: attach a `BeanTracker` to any GameObject,
hit Play, and it captures position/rotation/custom fields over time into a ring buffer. Other
small, decoupled components read that buffer to log it (console/CSV/JSON), draw it live in the
Scene view, or render it into a persisted PNG. It doesn't assert or fail anything — the whole
point is making behavior visible so a developer can judge it themselves, faster than eyeballing
the Game view or reading raw logs.

## File map

```
UTI/                            (this repo — package source, not a Unity project itself)
  README.md                     short public pitch + install steps
  package.json                  UPM package manifest
  LICENSE                       MIT
  CLAUDE.md                     local-only — standing behavioral rules for Claude sessions
  .github/
    workflows/tests.yml         CI — runs the EditMode suite on push/PR
    ci-project/                 minimal, scrubbed Unity project shell CI resolves tests from
                                 (no scenes/game content — see its own README.md)
  Runtime/                      the actual package code (see "The six components" below)
    UTI.Runtime.asmdef
  TESTS/
    EditMode/                   all current automated tests (NUnit, run via Unity Test Runner)
    PlayMode/                   scaffold only — no tests populated yet (see Known Gaps below)
    TestTracker.md              feature-by-feature verification status, honest Planned/Pass/Partial
    ErrorHandlingTracker.md     one row per guarded system boundary, same rigor as TestTracker
  docs/
    ONBOARDING.md                this file
    DESIGN.md                   architecture — how the pieces fit, why they're built this way
    PROJECT_OVERVIEW.md         full pitch, Roadmap, and the complete Change Log since day one
    HANDOFF.md                  local-only — cross-session "what's happening / what's next"
    USAGE.md / READING_LOGS_AND_VISUALS.md / CONFIG.md
                                 end-user docs — for a dev USING UTI in their game, not for
                                 developing UTI. Copied verbatim into each consuming project's
                                 own <project root>/UTI/ folder by BeanConfig's Editor menu item.
```

UTI is **not** a standalone Unity project — it has no `Assets/`/`ProjectSettings/` of its own.
It's referenced as a local `"file:"` package (or a git URL) from real, separate Unity projects.
See "How UTI is tested" below for where those live.

## The six components (`Runtime/`)

| Component | File | Does |
|---|---|---|
| `BeanTracker` | `BeanTracker.cs` | Captures transform (+ optional custom `extras`) into a `BeanBuffer` ring buffer on an interval you choose. The hub — nothing else works without one. |
| `BeanLogger` | `BeanLogger.cs` | Subscribes to a tracker's samples, writes them to Console / CSV / JSON Lines via `IBeanOutput`. |
| `BeanVisualizer` | `BeanVisualizer.cs` | Draws the live path as a gizmo line in the Scene view. Play-Mode-only, not persisted. |
| `BeanSnapshotExporter` | `BeanSnapshotExporter.cs` | Renders the path through a real Camera into a saved PNG — the persisted, after-the-fact artifact `BeanVisualizer` alone can't provide. The largest, most complex file in the package. |
| `BeanMouseTracker` | `BeanMouseTracker.cs` | A small proxy that drives its own Transform to follow the mouse cursor, so an ordinary `BeanTracker` on the same object tracks mouse input through the exact same pipeline as anything else. |
| `BeanConfig` | `BeanConfig.cs` | Project-wide default settings (`<project root>/UTI/BeanConfig.txt`), applied to newly-added Bean components via Unity's `Reset()` hook. Also owns the Editor menu items (`UTI > Create Bean Config`, `UTI > Setup Project (Config + Docs)`). |

Supporting: `BeanSample` (the data struct), `BeanBuffer` (the ring buffer), `IBeanOutput` +
`BeanFileOutputBase`/`ConsoleBeanOutput`/`CsvBeanOutput`/`JsonlBeanOutput` (the output pipeline),
`BeanArtifactPaths` (shared "where do generated files go" logic).

**The pattern that repeats everywhere:** every component splits into a pure, static/testable
half (framing math, path resolution, decimation, parsing) and a thin untestable half that
actually touches `Camera`/`Input`/file I/O. Follow this split when adding anything new — it's
why the EditMode test suite can cover this much of a Unity package without Play Mode.

## How UTI is tested

UTI has **no demo/sample scenes of its own and none should be built** — see "Standing rules"
below. Instead:

- Three real, separate Unity projects reference UTI as a local package and serve as test beds:
  `little wings` (3D flight/combat), `project 2`/BoxJump (3D platformer), `2d project 3` (2D).
- **The Bring-Your-Own-Test Protocol** (`docs/DESIGN.md` §12): find a test/scenario that
  *already exists and already passes* in one of those projects, attach the relevant Beans to
  whatever it exercises, run it completely unmodified, report what UTI produced. Never write a
  new harness to make something "drivable" for UTI's benefit.
- **A Unity MCP connection may or may not be available, and its capability varies session to
  session in ways not fully understood** — sometimes read-only/relay-only, sometimes full script
  execution with real Play Mode entry. **Check what it can actually do at the start of each
  session; never trust a prior session's capability claims, including this one.**
- `TESTS/TestTracker.md` and `TESTS/ErrorHandlingTracker.md` only ever say "Pass"/"Verified"
  after a real, described run — never from reading the code alone. Keep that discipline.

## Core conventions

- **Namespace `UTI`**, `Bean*` naming for everything user-facing.
- **Guard system boundaries, not internal invariants.** Caller-supplied code (`CustomCapture`),
  file/disk I/O, and externally-edited config get a try/catch that logs a warning and degrades
  gracefully — UTI should never be the reason a playtest session crashes. A genuine internal
  programming error (e.g. `BeanBuffer`'s constructor given a bad capacity) is left to throw
  loudly on purpose. See `docs/DESIGN.md` §14 and `TESTS/ErrorHandlingTracker.md`.
- **Comments explain why, not what.** Terse, one place per non-obvious decision.
- **When you update a tracked doc, add a dated Change Log entry at its bottom** (see any of
  `docs/DESIGN.md`, `docs/PROJECT_OVERVIEW.md`, `TESTS/TestTracker.md`,
  `TESTS/ErrorHandlingTracker.md` for the pattern), trimmed to a reasonable entry cap so it
  doesn't grow unbounded.
- **Commit per logical unit of work going forward** (a feature, a fix, a doc pass) rather than
  batching a whole session into one commit — still only with the user's explicit per-commit
  permission, same standing rule as always.

## Standing rules not to violate

(Full detail in `CLAUDE.md` if you have it — this is the condensed version for a public-repo-only
checkout.)

- **Never build a new demo/sample Unity project or scene to test/showcase a UTI feature.** Real,
  substantial cost for low verification value, and it contradicts the Bring-Your-Own-Test
  Protocol's whole premise. If a genre gap turns up, track it for whenever a suitable object
  exists in a real test project — don't build one.
- **Before writing something up as a UTI limitation, confirm the root cause is actually in UTI's
  own code**, not the consuming project's. If a fix happened entirely on the other team's side,
  it's their finding, not UTI's to memorialize.
- **Only commit/push with the user's explicit permission for that specific commit.**
- Genre-specific names (e.g. `PlayerPlane`) are fine in test-bed fixtures, never in UTI's own API.

## Where to look for X

| You want... | Read |
|---|---|
| Architecture, why something is built the way it is | `docs/DESIGN.md` |
| The pitch, Roadmap, full project history | `docs/PROJECT_OVERVIEW.md` |
| What's actually verified vs. still Planned | `TESTS/TestTracker.md` |
| Which system boundaries are guarded and how well-verified the guard is | `TESTS/ErrorHandlingTracker.md` |
| What happened last session / open blockers | `docs/HANDOFF.md` (local-only) |
| The standing behavioral rules for working on this repo | `CLAUDE.md` (local-only) |
| How an end user sets up/uses UTI in their game | `docs/USAGE.md` |
| How to interpret CSV/JSON/console/PNG output | `docs/READING_LOGS_AND_VISUALS.md` |
| `BeanConfig.txt`'s fields | `docs/CONFIG.md` |

## Known gaps worth knowing about up front

- `TESTS/PlayMode/` exists but is empty — everything Play-Mode-dependent (gizmo rendering,
  snapshot capture, mouse tracking, physics-tick timing) is currently verified only through ad
  hoc live sessions, not a repeatable automated suite. CI (below) only covers EditMode.
- CI exists (`.github/workflows/tests.yml`) but hasn't actually run yet — it needs `UNITY_EMAIL`/
  `UNITY_PASSWORD` added as GitHub Actions secrets by the repo owner first (a live-login approach,
  not a portable license file — a first attempt at a file-based approach turned out to be
  machine-bound and had to be scrapped, see `docs/DESIGN.md` §12's Change Log). Check §12 for
  current status before assuming it's green, and be aware Unity's licensing infrastructure has
  changed more than once recently — don't trust CI-setup instructions from a prior session (or
  from general Unity CI documentation found online) without re-verifying against what Unity's
  licensing site/client actually does today.
- `extras` (`BeanSample`) is numeric-only (`Dictionary<string, float>`) — awkward for categorical
  state like an AI's patrol/chase/attack.

## Change Log

- 2026-08-08 — Created, following a full project review that flagged this as a real gap: new
  agent sessions had no single fast-orientation doc distinct from `CLAUDE.md` (rules),
  `HANDOFF.md` (ephemeral session state), and `DESIGN.md` (deep architecture).
- 2026-08-08 — Updated same day for CI's addition (`.github/workflows/tests.yml` +
  `.github/ci-project/` — see `docs/DESIGN.md` §12): file map and Known Gaps section both
  reflect that CI now exists but hasn't executed yet (needs Unity account secrets).
- 2026-08-08 — Updated again same day after CI's license approach was corrected mid-setup (a
  file-based license turned out to be machine-bound — see `docs/DESIGN.md` §12's Change Log);
  Known Gaps now names the actual secrets needed and flags that Unity's licensing infrastructure
  itself is a moving target worth re-verifying, not just UTI's own tooling.
