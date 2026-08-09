# UTI — Architecture & Design

Tracking doc for how the pieces fit together. Pairs with [PROJECT_OVERVIEW.md](./PROJECT_OVERVIEW.md) (the pitch/concept) — this is the "how we'll build it" side. See the [root README](../README.md) for the short pitch and install steps.

## Target Environment

Unity **6000.5.6f1** (Unity 6 LTS) — confirmed as the actual installed Editor version across all three test projects (`little wings`, `project 2`, `2d project 3`), not the 6000.3.21f1 originally assumed. This is the real baseline for API compatibility decisions.

## 1. Data Flow

```
BeanTracker (captures)
    │  emits BeanSample on each tick
    ├──> BeanLogger (outputs: console / CSV / custom)
    └──> BeanVisualizer (reads buffer, draws path in Scene view)
```

BeanTracker doesn't know Logger or Visualizer exist. They just read from it (event or buffer pull). Keeps the "decoupled pieces" promise from the README real, not just aspirational.

## 2. Core Types

### `BeanSample` (struct)
The unit of data one tick produces.
- `int tickIndex`
- `float timestamp` (`Time.time` at capture)
- `Vector3 position`
- `Quaternion rotation`
- `Dictionary<string, float> extras` — optional, for custom captured fields beyond transform (velocity, health, whatever a user wants to hook in). **Null by default, not an empty dictionary** — only allocated when a custom capture delegate is actually assigned. The common case (no custom fields) must not allocate a `Dictionary` every tick just to leave it empty; that's a GC hit for zero benefit.

### `BeanTracker` (MonoBehaviour)
Captures data on any GameObject it's attached to.
- Config (Inspector): capture mode (`EveryUpdate` / `EveryFixedUpdate` / `EveryNSeconds`), interval value, capture rotation on/off, max buffer size (ring buffer once full — default something like 1000 samples so long play sessions don't balloon memory).
- Public surface: `event Action<BeanSample> OnSample`, `IReadOnlyList<BeanSample> Samples`.
- Optional custom capture: a `Func<GameObject, Dictionary<string,float>>` slot users can assign to feed the `extras` field without subclassing anything.

### `BeanLogger` (MonoBehaviour)
Attaches alongside (or references) a `BeanTracker`. Subscribes to `OnSample`, writes it out.
- Config: output targets (`[Flags]` enum — `Console` / `Csv` / `Json`, any combination), file path
  (default: `UTI/BeanLogs/` under the project root, timestamp+unique-token-named for uniqueness —
  see `BeanArtifactPaths` and §8.5, changed 2026-08-07 from `Application.persistentDataPath`).
- Output is behind an `IBeanOutput` interface so someone can write their own sink (an analytics
  endpoint, whatever) without touching core code — `ConsoleBeanOutput`/`CsvBeanOutput`/
  `JsonlBeanOutput` (added 2026-08-08, see §8.2) are UTI's own three built-in implementations.

### `BeanVisualizer` (MonoBehaviour)
Draws the recorded path in the Scene view.
- Reads `BeanTracker.Samples` directly in `OnDrawGizmos`/`OnDrawGizmosSelected` — no event subscription needed, just draws whatever's in the buffer right now.
- Config: line color, optional color-by-speed or color-by-time, point markers on/off, cap on how many points to draw (perf guard for long sessions).

## 3. Extensibility Points

- `IBeanOutput` — plug in custom log destinations.
- Custom capture delegate on `BeanTracker` — plug in custom data beyond transform.
- `OnSample` event — anyone can subscribe for their own purposes (analytics, custom visualization, replay systems) without needing `BeanLogger`/`BeanVisualizer` at all.

## 4. Package Structure (Unity Package Manager format)

Building it as a real UPM package from the start so it's installable/shareable and Asset-Store-ready later without restructuring.

```
UTI/
  package.json
  README.md
  DESIGN.md
  CLAUDE.md
  Runtime/
    UTI.Runtime.asmdef
    BeanSample.cs
    BeanBuffer.cs
    BeanTracker.cs
    IBeanOutput.cs
    BeanLogger.cs
    ConsoleBeanOutput.cs
    BeanFileOutputBase.cs
    CsvBeanOutput.cs
    JsonlBeanOutput.cs
    BeanVisualizer.cs
    BeanSnapshotExporter.cs
    BeanArtifactPaths.cs
    BeanMouseTracker.cs
    BeanConfig.cs
  Editor/
    (future: custom inspectors, editor windows — no asmdef until there's actual Editor-only code)
  TESTS/
    TestTracker.md         (human-readable test tracking doc, project convention)
    EditMode/
      UTI.Tests.asmdef
      BeanBufferTests.cs
      BeanTrackerTests.cs
      BeanLoggerTests.cs
      BeanVisualizerTests.cs
      BeanSnapshotExporterTests.cs
      BeanArtifactPathsTests.cs
      BeanMouseTrackerTests.cs
      BeanConfigTests.cs
  Samples~/
    (Dream To-Do only, not planned — see PROJECT_OVERVIEW.md. Building our own demo/sample scenes
    purely to test/showcase UTI is deliberately out of scope; see §12's Bring-Your-Own-Test
    Protocol. The trailing ~ is UPM convention so these wouldn't import by default if ever built.)
```

**`.github/` and `CI~/` (added 2026-08-08)** — CI, not package content. `.github/workflows/tests.yml`
runs the EditMode suite on push to `main`; `CI~/` is a minimal, scrubbed Unity project shell
(ProjectSettings + a trimmed `Packages/manifest.json` referencing UTI via a relative `file:` path)
that exists solely so Unity's Test Runner has a real project to resolve tests from — see its own
`README.md`. No scenes, no game content; the "no demo/sample Unity projects" rule (§12) still
applies, this is test infrastructure, not a showcase. **`CI~/` originally lived at
`.github/ci-project/` — moved after the first live CI run failed** (see §12's CI subsection): the
CI action's own version-detection glob skips dot-prefixed directories, so it couldn't see a project
living under `.github/` at all. The trailing `~` is the same UPM convention `Samples~/` already
uses — excluded from a consuming project's package payload/asset import either way, dot-prefix or
tilde-suffix, but only the tilde form is also visible to a generic (non-Unity-aware) glob.

Also at the package root, all added 2026-08-07 alongside `README.md`/`DESIGN.md`/
`TESTS/TestTracker.md` — but note none of these three are meant to stay *only* here, they're
end-user docs (for the dev using UTI in their game, not for UTI's own development) and are meant
to be copied into each game project:
- `USAGE.md` (setup/usage guide) and `READING_LOGS_AND_VISUALS.md` (output-format reference) —
  copy into each game project's **`<project root>/UTI/`** (the generated-output folder, §8.5),
  so they sit right next to the actual logs/snapshots a dev is already looking at. **Corrected
  2026-08-07** — originally left living only in this package repo, which meant a dev working in
  `little wings`/`project 2`/`2d project 3` had no reason to ever see them; done manually for
  `little wings` this session (files copied directly into
  `C:\Users\sirsw\Unity Projects\little wings\UTI\`), still needs doing for `project 2` as part of
  its first relay round (see `TESTS/TestTracker.md`).
- `CONFIG.md` (§8.7, `BeanConfig` explainer) — same convention as the two above: copy into each
  game project's `<project root>/UTI/`, alongside the `BeanConfig.txt` it explains. (Originally
  slated for `Assets/UTI/` when `BeanConfig` was a `ScriptableObject` asset — that requirement
  went away when `BeanConfig` became a plain text file instead, same day; see §8.7.)

Note: `TESTS/` (docs) and a hypothetical `Tests/` (code) would collide on Windows' case-insensitive filesystem — learned this the hard way when `BeanBufferTests.cs` landed in the same physical folder as `TestTracker.md`. Fixed by nesting C# test code under `TESTS/EditMode/` instead of a sibling `Tests/` folder.

## 5. Namespace

`UTI` — matches the working title. One identifier to rename later if/when the project name changes, so it's not costly to keep the joke for now.

## 6. Configuration Philosophy

- Everything configurable from the Inspector on the component itself. No required ScriptableObject setup for v1 — stay drop-in.
- Defaults should work with zero configuration: add `BeanTracker`, hit play, it just starts capturing.
- Shared/synced config across many Beans (e.g., a project-wide default interval) could become a ScriptableObject later, but that's a v2 problem.

## 7. Open Questions (resolved)

All of v1's open questions got settled during implementation:

- Ring buffer default size — `1000` (`BeanTracker.maxSamples` default), unchanged since §8.1.
- `BeanLogger`/`BeanVisualizer` vs `BeanTracker` locality — same-GameObject default via `GetComponent`, with a `[SerializeField] tracker` override on both, as leaned toward. Implemented and covered by T09/T10.
- CSV default location — originally `Application.persistentDataPath`, confirmed working (T09);
  **changed 2026-08-07** to the project root (`BeanLogs/` subfolder) instead — see §8.5.
- (Not originally listed, resolved during implementation) `BeanVisualizer.maxPointsToDraw` default — `200`, chosen as a generous-but-bounded gizmo perf cap; see `DESIGN.md` Change Log for the milestone 4 entry.

No open questions remain blocking v1; anything new belongs here as it comes up (e.g. during milestone 6 sample-scene work).

## 8. Construction Plan

### 8.1 `BeanTracker`

**Fields (Inspector-exposed):**
- `captureMode` (enum: `EveryUpdate`, `EveryFixedUpdate`, `EveryNSeconds`)
- `captureInterval` (float, only relevant for `EveryNSeconds`)
- `captureRotation` (bool)
- `maxSamples` (int, default ~1000)
- `startTrackingOnEnable` (bool, default true — drop-in simplicity, but overridable for people who want to trigger tracking on a gameplay event like "race start")

**Internal storage:** a real circular buffer (fixed-size array + head index + count), not a `List<T>` with `RemoveAt(0)` — at high capture rates, shifting a list every tick is wasteful. The buffer exposes samples in chronological order via a small read-only wrapper.

**Buffer allocation is lazy, not `OnEnable`-driven.** Originally the buffer was allocated in `OnEnable`. That broke under EditMode testing: without `[ExecuteAlways]`, `OnEnable` never fires in Edit Mode, so `AddComponent<BeanTracker>()` outside Play Mode left `buffer` null, NRE-ing on first use. `[ExecuteAlways]` was rejected as the fix — it would make `Update()`/`FixedUpdate()` run continuously in the Editor just from a Bean sitting in a scene, contradicting the "hit play, see the trail" design and burning CPU while idling. Fixed instead with a lazy `Buffer` property (`buffer ??= new BeanBuffer(...)`) — valid whether or not `OnEnable` has run, no behavior change during actual Play Mode use.

**Lifecycle:**
- `OnEnable`: start tracking if `startTrackingOnEnable` (buffer allocation is lazy, not here — see above).
- `Update` / `FixedUpdate`: depending on `captureMode`, either capture directly or accumulate `intervalTimer` and capture once it crosses `captureInterval`. `Update` delegates to a public `SimulateFrame(deltaTime)` so this logic is testable deterministically without Play Mode.
- `Capture()`: build a `BeanSample` from the transform (+ optional custom delegate output), push into the buffer, increment `tickIndex`, invoke `OnSample`.

**Public API:** `StartTracking()`, `StopTracking()`, `ClearSamples()`, `SimulateFrame(float deltaTime)`, `Samples` (read-only), `OnSample` event, and a `Func<GameObject, Dictionary<string,float>>` slot for custom extras.

### 8.2 `BeanLogger`

**Fields:** `tracker` (auto-`GetComponent` if unset), `outputTargets` (`[Flags]` enum so any combination of Console/CSV/JSON can be active at once), `filePath` (default: `UTI/BeanLogs/` under the project root — see §8.5), `appendAcrossReuse` (§13/T14).

**`IBeanOutput` contract:** `Open(BeanTracker)`, `Write(BeanSample)`, `Close()`. `BeanLogger` just owns a list of active `IBeanOutput`s built from `outputTargets`, plus any custom ones a user assigns via `CustomOutputs`.

- **Console output** — formatted `Debug.Log` per sample. Meant for low-frequency/dev use; if capture rate is high, this will flood the console fast (worth a code comment, not a feature to solve in v1).
- **CSV output** — `StreamWriter` opened in `Open()` (writes a header row: tick, timestamp, x, y, z, qx–qw, extras — only on a genuinely new file, so append-mode reopens don't insert a second header mid-file), buffered writes with periodic flush (not flush-per-row — that's a perf trap at high tick rates), closed/flushed in `Close()`.
- **JSON Lines output (`JsonlBeanOutput`, added 2026-08-08)** — one JSON object per sample line (`.jsonl`, not a single top-level array — a JSON array can't be safely appended to mid-run the same way CSV already streams). No header. The actual motivation over CSV: `extras` becomes a real nested object with natively-typed values instead of CSV's one flat `key=value;key=value` string column that needs a second parse pass. See `READING_LOGS_AND_VISUALS.md`'s JSON Lines section for the exact shape.
- **`BeanFileOutputBase` (added 2026-08-08)** — `CsvBeanOutput` and `JsonlBeanOutput` turned out to be identical in everything *except* per-line formatting and whether a header exists (directory creation, the `StreamWriter` open/flush-interval/close lifecycle), so that shared shell was pulled into one internal-use base class once JSON existed and the duplication became real rather than hypothetical. `public` only because C# requires a base class to be at least as accessible as its subclass (CS0060) — not part of UTI's own extensibility surface, which is still `IBeanOutput`.
- **Resolving where a file lands (`ResolveFilePath`)** — one shared static method (now with an `extension` parameter, default `"csv"` for backward compatibility) backs both CSV's and JSON's default path — explicit `filePath` if set, otherwise `{timestamp}_{objectName}_{uniqueToken}_bean.{extension}` under `UTI/BeanLogs/`. If **both** CSV and JSON are active at once, an explicit `filePath` can't safely back two different files without one overwriting the other's content — same precedent as `BeanSnapshotExporter`'s multi-angle capture (§8.4) — so both fall back to their own default-named path instead of colliding.
- **`ApplyConfigDefaults(BeanConfig)` / `Reset()` (added 2026-08-08)** — `BeanLogger` didn't participate in `BeanConfig`'s new-component-defaulting pattern until JSON output needed a project-wide default target; now it follows the exact same split as `BeanTracker`/`BeanSnapshotExporter` (§8.7): `Reset()` (Editor-only, fires when the component is added) calls `ApplyConfigDefaults(BeanConfig.Load())`, and the public `ApplyConfigDefaults` is separated out purely so it's testable without touching the real filesystem.

**Lifecycle:** subscribe to `tracker.OnSample` in `OnEnable`, call `Open()` on active outputs. Call `Close()` in `OnDisable`, `OnDestroy`, *and* `OnApplicationQuit` — standalone builds don't reliably hit all three, so this is a belt-and-suspenders situation for making sure the file gets flushed.

### 8.3 `BeanVisualizer`

**Fields:** `tracker` (auto-fetched), `pathColor`, `colorMode` (enum: `None`, `BySpeed`, `ByTime`), `drawPoints` (bool), `maxPointsToDraw` (perf cap).

**Drawing:** `OnDrawGizmos`/`OnDrawGizmosSelected` reads `tracker.Samples` directly and draws `Gizmos.DrawLine` between consecutive points (optional `Gizmos.DrawSphere` per point). If the buffer has more samples than `maxPointsToDraw`, decimate (step through at a computed interval) rather than draw everything — a 1000-point gizmo path redrawing every editor frame is a real performance trap.

**Flagged gap, not just a gotcha:** gizmos only draw while the `BeanTracker`'s data still exists in memory — and Play Mode state (including our buffer) is discarded on exiting Play Mode. So `BeanVisualizer` alone is a *live, during-Play* thing only, with zero persisted artifact — no way to review a run after it ends, share it, or look at it without the Editor open to that exact live moment. Originally logged here as a "stretch goal, not v1" — re-flagged 2026-08-07 per user feedback as a real gap worth an actual design pass, since it undercuts the core "see the trail" pitch (a developer can already watch the Game view live; the point of visualization is reviewing *after*).

**Decided 2026-08-07 — the persisted artifact must include real scene context, not just an abstract path.** A plotted line in empty space isn't enough to actually debug with: per user feedback, "say someone falls through the floor, well you have to see the floor." A CSV-replay viewer that only knows sample positions has no floor, no walls, no props to show — it would need UTI to somehow also capture/reconstruct scene geometry, which is a much bigger and more fragile undertaking than tracking a Transform. A static image *does* have this for free, because it's a real render of the actual scene. See §8.4 for the chosen design (`BeanSnapshotExporter`).

Also worth naming: this design directly resolves T05's verification deadlock too. T05 has been blocked three times not on UTI's own logic but on *external* screenshot tooling reading the Editor from outside (stale/cached captures across two different tools). A component that renders and saves its own PNG from inside Unity doesn't depend on that tooling at all — verifying T05 becomes "open the file `BeanSnapshotExporter` wrote and look at it," not "get some other tool to successfully screenshot Unity."

### 8.4 `BeanSnapshotExporter`

**Fields:** `tracker` (auto-fetched like Logger/Visualizer), `captureCamera` (defaults to `Camera.main` if unset — deliberately reuses whatever camera a dev already trusts to show their scene correctly, rather than UTI inventing its own view), `pathColor`, `lineWidth`, `captureWidth`/`captureHeight` (render target resolution), `autoFrameCamera`, `dimensionMode` (enum `Auto`/`Force2D`/`Force3D`, **added 2026-08-07** — overrides the flat/2D-vs-3D auto-guess, see below), `captureOnStopTracking` (bool, default true), `filePath` (mirrors `BeanLogger.FilePath` — explicit override or default under the project root, see §8.5).

**How it captures scene + path together:** `CaptureSnapshot()` builds a temporary `LineRenderer` from `BeanTracker.Samples` positions (via the pure, testable `BuildPathPositions`), points the capture camera at an off-screen `RenderTexture`, calls `camera.Render()`, reads the pixels back, and writes a PNG. Because it's a real `Camera.Render()` — not `Gizmos` — the floor, walls, and any other geometry in frame get captured exactly as they'd look to a developer watching Play Mode, with the recorded path drawn directly into that same image. The temporary line object and render texture are cleaned up in a `finally` block regardless of outcome. This is also a genuine improvement over gizmos on its own merits, not just "gizmos + saved to disk": it works in an actual Player build, not just the Editor Scene view, since it doesn't depend on Editor-only gizmo drawing at all.

**Decoupled like the others:** a fourth independent piece alongside Tracker/Logger/Visualizer — `BeanTracker` gained a small `OnStopTracking` event (fired once on an actual tracking→stopped transition) purely so this component can auto-capture without polling; nothing else needed to change. A dev who doesn't want snapshots just doesn't add the component, same as any other Bean piece.

**Not designed as a replacement for `BeanVisualizer`.** Live gizmos are still useful for watching a path develop in real time during Play Mode; `BeanSnapshotExporter` is for reviewing after the fact. Both are meant to coexist on the same GameObject.

**Deliberately basic, not polished.** Decided 2026-08-07: the point is a fast sanity check a dev can create and glance at, not a portfolio-quality screenshot — "we don't need perfect colors or great details, we need it to be fast to create, open, and good enough to quickly see and understand." Default `captureWidth`/`captureHeight` is `640x360`, not full HD — small enough to render, encode, write, and open quickly, still large enough to make out the path against the scene. No color grading, anti-aliasing, or lighting tuning attempted; whatever the camera already sees is what gets captured. Both are still just serialized fields, so a specific case that genuinely needs more can turn them up per-Bean.

**Deferred to later (not v1 scope for this feature):** multiple camera angles per capture, periodic/interval snapshots instead of one at stop-tracking, and true frame-by-frame replay (already tracked separately in README.md's Roadmap "Feature ideas"). One camera, one image, whole path baked in as a line, captured once at the natural "run just ended" moment is the v1 cut — matches the existing "drop-in simplicity, sane defaults" philosophy (§6) rather than building a full scene-capture pipeline up front.

**Superseded 2026-08-07 — see below.** The paragraph above assumed a fixed `{gameObject.name}_bean_snapshot.png` default, which collided both across duplicate GameObject names *and* across repeat runs on the same object. Both are now fixed for snapshots, and — same session — for `BeanLogger`'s CSV path too (§13/T13 closed on the code/unit-test level); see §8.5.

### Snapshot folder, unique naming, and camera auto-framing (decided 2026-08-07)

Three real gaps found the first time a snapshot actually got looked at (T17, `little wings`,
`PlayerPlane`):

1. **No dedicated location.** The PNG landed as a loose file directly under
   `Application.persistentDataPath`, same folder as everything else — nothing marked it as "the
   generated artifacts." Fixed: `BeanSnapshotExporter` now defaults into a
   `BeanSnapshots/` subfolder (auto-created, same as the CSV directory pattern already used by
   `CsvBeanOutput`) — **superseded again same session, see §8.5**: that subfolder itself moved out
   of `persistentDataPath` entirely once it turned out to be undiscoverable in practice.
2. **No way to keep more than one.** The old default filename was fixed per-GameObject-name, so
   capturing the same Bean five times to compare results would silently overwrite the same file
   four times over — exactly the "ran it 5 times, want to compare" case the user described.
   Fixed: `ResolveSnapshotPath` now prefixes the filename with a millisecond-precision UTC
   timestamp (`yyyyMMdd_HHmmss_fff_{name}_bean_snapshot.png`), timestamp first so a folder with
   many runs (including different Beans) sorts chronologically. Every real capture gets its own
   file; nothing is silently lost.
3. **The first real capture was nearly useless as a "quickly understand this" artifact** — a
   tight follow-camera produced a close-up of mostly ground with the path as a tiny distant blob.
   The whole point of this feature is a glance-and-understand artifact (see the "deliberately
   basic" note above), and a badly-framed shot fails that regardless of resolution. Fixed:
   `autoFrameCamera` (default on) computes a bounding box of the recorded path
   (`ComputePathBounds`) and repositions/reorients a *copy* of the capture camera's transform
   (restored afterward — gameplay camera behavior is untouched) to frame the whole path with
   margin, via `ComputeFraming`:
   - **Flat/2D path** (bounding box depth along Z below a small threshold — matches this
     project's existing convention that 2D scenes keep Z at 0, see §1) → an **orthographic,
     front-on** view sized to fit the path's X/Y extent. Matches how 2D games are normally viewed.
   - **Real 3D path** → a **perspective, elevated angle** view positioned far enough back to fit
     the whole bounding box, using the camera's own field of view.
   Both branches are exposed as pure, testable functions (`ComputePathBounds`, `IsFlatPath`,
   `ComputeFraming`) — the actual camera repositioning + render remains in the untestable
   `CaptureSnapshot()`, same split as the rest of this component.

**Revised again 2026-08-07, same day — two more fixes from the very next live verification round
(T17/T18) in `little wings`:**

- **The 3D branch's offset direction was a fixed diagonal** (`IsoDirection = (1, 0.75, 1)`), "a
  generic isometric-ish look, not tied to the path's direction of travel" per the paragraph above
  — that assumption turned out wrong in practice. A test path that happened to travel roughly
  parallel to that fixed direction rendered foreshortened into a thick vertical stripe instead of
  a wide diagonal line, since the camera was looking almost straight down the path's own length.
  A second path at a different angle framed correctly, confirming the underlying math was sound
  and the fixed direction was the actual problem. **Fixed:** `ComputeFraming` now takes the path's
  own `travelDirection` (first sample to last) and derives the camera's horizontal offset as
  *perpendicular* to it (rotated 90° in the XZ plane), so the shot is always broadside regardless
  of which way the path happens to run. Falls back to a fixed forward broadside when there's no
  horizontal travel to derive a direction from (e.g. a purely vertical path — directly relevant
  for the next test target, `project 2`, a vertical platformer).
- **Flat/2D-vs-3D was auto-guess-only.** Per user feedback ("can mod UTI for 3D vs 2D games"),
  added `dimensionMode` (`Auto`/`Force2D`/`Force3D`) so a dev can override the Z-depth heuristic
  outright — useful for a 2D scene that doesn't keep everything at Z=0, or a 3D path that happens
  to come out flat and shouldn't be treated as 2D. The decision itself is now factored out into
  `ResolveIsFlat(dimensionMode, bounds)`, a separate pure function from `ComputeFraming` (which
  now just takes a plain `isFlat` bool) — cleaner to test each piece independently, and the
  override logic doesn't need to know anything about camera framing math.

Neither fix has a real Play Mode re-verification yet as of this Change Log entry.

**Known limitation, accepted for v1:** the 2D/3D detection is a Z-extent threshold, not a real
scene-orientation check — a 2D game built on a non-standard plane would fool it. Every test
project so far (`little wings`, `project 2`, `2d project 3`) matches the assumed convention, so
this isn't fixed further unless a real case breaks it.

### T23 fix and multi-angle capture (2026-08-08, not yet live-verified)

**`MinFramingRadius` is now configurable, closing T23.** It was a fixed `2f` literal shared by
both the orthographic half-size floor and the perspective distance floor — found too tight against
`project 2`'s actual scale on a near-stationary path (leg geometry and a wall, no usable context;
see `TESTS/TestTracker.md` T23). Now a serialized instance field (`BeanSnapshotExporter
.minFramingRadius`, still defaulting to `2f`) threaded as a parameter into `ComputeFraming`/
`ComputeFramingForAngle` instead of a compile-time constant, and defaultable project-wide via
`BeanConfig.DefaultMinFramingRadius` (§8.7) the same way `DimensionMode` already was. No behavior
change for a project that doesn't touch it — same `2f` default as before.

**Multi-angle capture, the Feature idea from README.md's Roadmap, built as `CaptureAngles`.** A new
`BeanSnapshotAngle` enum (`Auto`/`Above`/`Side`/`Behind`) and a `BeanSnapshotExporter.captureAngles`
array (default `{ Auto }`, one entry — fully backward compatible: same single file, same naming as
before this feature existed). `Auto` is untouched original behavior (flat-vs-3D auto-guess).
`Side` is exactly the same broadside-3D placement `Auto` already used for a real 3D path — factored
out into its own `ComputeSideFraming` so `ComputeFraming`'s 3D branch and the `Side` angle share one
implementation rather than duplicating the broadside-offset math. `Above` is a new straight-down
perspective shot (up-hint derived from the path's own horizontal travel direction, since `Vector3
.up` is degenerate as an up-hint when looking straight down). `Behind` is a perspective shot
opposite the path's travel direction (falls back to a fixed backward offset for a purely vertical
path, same fallback shape as the existing broadside logic). All three non-`Auto` angles share one
`ComputePerspectiveDistance` helper for the camera-distance formula, differing only in offset
direction — `ComputeFramingForAngle(bounds, travelDirection, isFlat, aspect, fieldOfView,
minFramingRadius, angle)` is the new dispatch point, pure and testable like `ComputeFraming` always
was.

**Naming, settling the README's "exact scheme TBD":** a single-angle capture (still the default)
keeps the original `ResolveSnapshotPath` untouched byte-for-byte. More than one angle uses a new
`ResolveMultiAngleSnapshotPath`, naming each file
`{timestamp}.{n}_{objectName}_{angleName}_{uniqueToken}_bean_snapshot.png` (1-based `n`) — every
file from one `CaptureSnapshot()` call shares the group's timestamp and `uniqueToken`, so they sort
and group together, with the angle name making each individually identifiable. **Deliberate
decision:** an explicit `filePath` override is only honored for a single-angle capture — one fixed
path can't safely back more than one output file without one angle silently overwriting another
mid-capture, so multi-angle capture always uses the default `UTI/BeanSnapshots/` location
regardless of `filePath`.

**`CaptureSnapshot()` now loops over the configured angles**, reusing one `RenderTexture` across
all of them (recreated per angle would be wasteful — the render target dimensions don't change
between angles) but recomputing framing/line width and recreating the path `LineRenderer` each
iteration (those genuinely differ per angle). `LastSnapshotPath` still holds the last file written
(backward-compatible with single-angle usage); a new `LastSnapshotPaths` lists every file from the
most recent call.

### 8.5 Where UTI's generated files actually live (`BeanArtifactPaths`)

**Decided 2026-08-07, correcting a real mistake.** Both `CsvBeanOutput` and `BeanSnapshotExporter`
originally defaulted to `Application.persistentDataPath`. That's the technically-correct Unity API
for "a writable, per-user location that survives app updates," but it resolves to a hidden,
per-user Windows folder (`AppData/LocalLow/{Company}/{Product}`) that has nothing to do with the
project folder a developer actually has open — confirmed the hard way when the person who
designed the feature went looking for their own generated files in the project directory and
couldn't find them. For a tool whose entire purpose is "a developer looks at the results
afterward," a technically-correct-but-undiscoverable default defeats the point as thoroughly as
the original no-persisted-artifact gap did.

Fixed with a new shared helper, `BeanArtifactPaths`. `ProjectRootDirectory` resolves to
`Application.dataPath/..` — the project root in the Editor (one level up from `Assets/`, so
alongside `Library/`, `Logs/`, `Temp/`) or the folder next to the executable in a Player build.
`RootDirectory` nests one level further into its own `UTI/` folder there — **revised again
2026-08-07** after the first pass (loose `BeanLogs/`/`BeanSnapshots/` subfolders directly at
project root) per feedback that everything UTI generates should live under one clearly-labeled
folder, not scattered as sibling folders next to the project's own `Library`/`Logs`/`Temp`. Both
`BeanLogger` (`UTI/BeanLogs/`) and `BeanSnapshotExporter` (`UTI/BeanSnapshots/`) default through
this same helper, so both artifact types land in one predictable, easy-to-find place.

**Also fixed the same session:** both `BeanLogger` and `BeanSnapshotExporter`'s default filenames
are now timestamp *and* random-token unique (`{timestamp}_{objectName}_{uniqueToken}_...`, via
`BeanArtifactPaths.NewUniqueToken()`) — closing the CSV half of the filename-collision gap from
§13/T13 to match the PNG side, and guarding against two same-named clones (e.g. `Bullet(Clone)`)
opening in the same millisecond, not just sequential reruns. **Revised once more, same day:**
originally keyed on `GameObject.GetInstanceID()` instead of a random token — reverted after a
relayed build reported it as a hard compile error (`CS0619`, obsolete-as-error) against this
project's Unity version, a claim that couldn't be independently confirmed as a real Unity API
change. A fresh GUID-based token sidesteps needing to resolve that dispute at all and gives an
equal-or-stronger uniqueness guarantee regardless.

**Known tradeoff, accepted:** writing next to a Player build's executable can fail if that build
is installed somewhere write-protected (e.g. `Program Files`) without elevated permissions. Not a
concern for UTI's actual use case — a dev/testing tool used from the Editor or a local dev build,
not a shipped product — so not solved further unless a real case needs it.

### 8.6 `BeanMouseTracker` (added 2026-08-07)

Per user feedback: "let's be able to add our tracker Bean to our mouse so we can track exact mouse
movements as well for help debugging." Rather than teaching `BeanTracker` a second, non-Transform
data source (which would mean every downstream piece — Logger, Visualizer, SnapshotExporter — also
needs to know about it), `BeanMouseTracker` is a small proxy: each `Update()`, it writes the mouse
cursor's position into *its own* Transform, and a completely ordinary `BeanTracker` on the same
GameObject captures it exactly like it would any other moving object. Nothing downstream needed to
change at all — this is the "decoupled pieces" philosophy (§ Design Philosophy in README.md)
paying off directly.

**Fields:** `trackingSpace` (enum `Screen`/`World`), `worldCamera` (only used in `World` mode,
defaults to `Camera.main`), `worldDistanceFromCamera` (float, default 10 — only used in `World`
mode).

**Screen mode** writes raw `Input.mousePosition` (pixels, Z=0) directly — genre/camera-agnostic,
works identically in any project. **World mode** projects that same screen position into 3D world
space via `camera.ScreenToWorldPoint` at a fixed distance in front of the camera — useful for
tracking where a mouse-aimed reticle actually points in the game world (e.g. `little wings`' own
mouse-steered flight aim), not just where the OS cursor sits on screen.

**Deliberately uses the legacy Input Manager (`Input.mousePosition`), not the Input System
package's `Mouse.current`.** Referencing `UnityEngine.InputSystem` types would require adding that
package as a hard reference in `UTI.Runtime.asmdef`, breaking "no required dependencies" for any
project that doesn't have Input System installed — a real risk given `project 2`/`2d project 3`
aren't confirmed to have it. The tradeoff: `BeanMouseTracker` won't receive input if a project's
Active Input Handling (Project Settings > Player) is set to "Input System Package (New)" only —
documented plainly in `USAGE.md` rather than solved with a conditional-compile dependency, which
would be real added complexity for a problem ("switch a Project Setting") the dev can fix in ten
seconds themselves.

**Pure functions `ResolveScreenPosition`/`ResolveWorldPosition`** are the testable pieces (given
explicit screen coordinates and, for World mode, a `Camera` — which itself can be constructed in
EditMode without Play Mode, since `ScreenToWorldPoint` is pure matrix math); `Update()`'s actual
`Input.mousePosition` read is the untestable piece, same category as every other live-input/render
call elsewhere in UTI.

### 8.7 `BeanConfig` (added 2026-08-07)

Per direct user correction: what was actually wanted wasn't a new capture mode (§8.6's Change Log
entry above has that history) but "give the dev the ability to choose and change some stuff" —
project-wide preferred settings, editable in one place, instead of configuring every Bean you drop
into the scene individually.

**Where it lives — revised same day, after a second round of feedback.** First built as a real
Unity asset (a `ScriptableObject` at `Assets/UTI/BeanConfig.asset`), since a `ScriptableObject`
only works as an editable, discoverable asset if it lives inside a project's `Assets/` folder.
User pushed back: the whole point was one place for a project's UTI footprint, and splitting
config into `Assets/` while logs/snapshots/docs live in the plain `UTI/` folder defeated that.
**Fixed by dropping the Unity-asset requirement entirely** — `BeanConfig` is now a plain text file
(`Key=Value` lines) at `<project root>/UTI/BeanConfig.txt`, read via ordinary `File.Exists`/
`File.ReadAllLines`, no `AssetDatabase`/`ScriptableObject` involved at all. Bootstrapped via a
new Editor menu item, **UTI > Create Bean Config** (`[MenuItem]`, wrapped in `#if UNITY_EDITOR`),
which writes a commented template with the compiled-in defaults spelled out — never overwrites an
existing file. `CONFIG.md` explains it and is meant to be copied alongside the generated file into
each game project's `UTI/` folder (§8.5), same convention as `USAGE.md`/`READING_LOGS_AND_VISUALS.md`.

**Fields (all currently mirrored 1:1 onto settings that already existed per-Bean):**
`DefaultCaptureMode`/`DefaultCaptureInterval` (→ `BeanTracker`), `DefaultDimensionMode` (→
`BeanSnapshotExporter`). Deliberately minimal — only the two things actually asked about
(tick/time capture choice, 2D/3D) rather than mirroring every single field on every component,
which would be scope nobody asked for. **Added 2026-08-08:** `DefaultMinFramingRadius` (→
`BeanSnapshotExporter.MinFramingRadius`) — the T23 fix (see §8.4/§13), letting a project set its
own scale-appropriate framing floor once instead of the old fixed `2f` literal. **Also added
2026-08-08:** `DefaultOutputTargets` (→ `BeanLogger.OutputTargets`) — the first key to affect
`BeanLogger` rather than `BeanTracker`/`BeanSnapshotExporter`, added alongside JSON Lines output
(§8.2) so a project can pick its preferred output format(s) once instead of setting `Output
Targets` on every `BeanLogger` by hand. Required giving `BeanLogger` its own `Reset()`/
`ApplyConfigDefaults()` pair for the first time — see §8.2.

**Standard install step, added 2026-08-08 — `UTI > Setup Project (Config + Docs)`.** A second
Editor menu item alongside `UTI > Create Bean Config`: does that (bootstraps `BeanConfig.txt`) and
also copies `USAGE.md`/`READING_LOGS_AND_VISUALS.md`/`CONFIG.md` from the package's own root into
`<project root>/UTI/`, resolving the package's real root via
`UnityEditor.PackageManager.PackageInfo.FindForAssembly` (works regardless of how the `"file:"`
dependency resolves, no hardcoded path). Added because the doc-copy step kept being a real,
recurring gap across every new test project (`little wings`, then `project 2` again — see
`HANDOFF.md`) despite being simple in principle; automating it into one menu click, run once per
new project, closes that permanently instead of relying on someone remembering to do it by hand
each time. Never overwrites a file already present, same convention as `CreateTemplateIfMissing`.

**How it actually applies — deliberately at component-add time, not live at runtime.** Unity
calls `MonoBehaviour.Reset()` when a component is first added in the Editor (and from its
right-click "Reset" menu entry) — the intended hook for "seed sensible initial values," which is
exactly what a project-wide default should do. `BeanTracker.Reset()`/
`BeanSnapshotExporter.Reset()` call `BeanConfig.Load()` (reads `BeanConfig.txt` if present, else
returns `null`) and apply its values to that component's own serialized fields. Considered and
rejected: reading config live at runtime so it could silently override an already-configured
Bean's behavior — would mean the Inspector no longer tells you the truth about what a specific
Bean actually does, turning "why is this Bean acting like Force2D, I never set that" into a real
debugging trap. The chosen design trades a small amount of magic (new Beans auto-populate) for a
much stronger guarantee (existing Beans' Inspector values are always exactly what's used, no
hidden overrides).

**Parsing:** `BeanConfig.ParseLines(IEnumerable<string> lines)` is the pure, testable piece —
comments (`#`), blank lines, unrecognized keys, and malformed values are all silently ignored
rather than erroring, so one typo doesn't break loading the rest of the file. Switching away from
a `ScriptableObject` incidentally made this *more* testable than the original design: no
`AssetDatabase`/Editor-only lookup to work around, just plain string parsing.

**Testability:** `ApplyConfigDefaults(BeanConfig config)` on `BeanTracker`, `BeanLogger` (added
2026-08-08, see §8.2), and `BeanSnapshotExporter` is `public`, separated from `Reset()` itself
specifically so it's testable directly (`new BeanConfig { ... }`, no filesystem or Editor event
needed) — same testable/untestable split used everywhere else in UTI. `BeanConfig.Load()`'s actual
file read remains untested (same category as everywhere else UTI touches real I/O); `ParseLines`
covers the actual logic.

## 9. Build Order (milestones)

1. `BeanSample` (data struct) + circular buffer implementation.
2. `BeanTracker` — capture loop, `OnSample` event, public start/stop/clear API. **Done, verified Pass** (T02) — all 4 `BeanTrackerTests` green in little wings after two fail→fix rounds (see Change Log).
3. `IBeanOutput` interface + `BeanLogger` — console output first (fastest to verify), then CSV output. **Done, verified Pass** (T09) — all 4 `BeanLoggerTests` green in little wings.
4. `BeanVisualizer` — gizmo path draw, then decimation + color modes once the basic line works. **Automated logic verified Pass** (T10) — all 6 `BeanVisualizerTests` green in little wings. Actual Scene-view rendering (T05/T06) still needs a manual Play Mode check.
5. `package.json` + `Runtime`/`Tests` asmdefs — turn the folder into a real installable UPM package. *(Done early, out of order, so BeanBufferTests could actually run — see Change Log. `Editor.asmdef` still deferred until there's real Editor-only code to isolate.)*
5.5. `BeanSnapshotExporter` — persisted scene+path artifact (§8.4). **Verified Pass live in little wings (T16/T17)** — real capture confirmed on the filesystem, correct path line, correct scene geometry. Since verification: gained `DimensionMode` override and a broadside-framing fix (§8.4), neither re-verified yet.
5.6. `BeanArtifactPaths` + `BeanMouseTracker` + `BeanConfig` — shared output-location helper (§8.5), mouse-input tracking proxy (§8.6), and project-wide default-settings asset (§8.7), all added 2026-08-07. Unit-tested; `BeanMouseTracker`'s live input-reading path and `BeanConfig`'s live Editor `Reset()` behavior not yet verified in Play Mode/the Editor.
6. ~~Sample scenes (car, NPC, player) under `Samples~/` demonstrating each use case from the
   README.~~ **Descoped 2026-08-08** — building our own demo/sample Unity scenes purely to
   test/showcase UTI is out of scope (see §12); genre coverage is instead exercised opportunistically
   via whatever car/NPC/projectile-like objects already exist in a real consuming project (see
   `TESTS/TestTracker.md` T08).

## 10. Definition of Done (v1)

A user can add `BeanTracker` + `BeanLogger` + `BeanVisualizer` to any GameObject in a fresh scene, hit Play, and — with zero required configuration —see:
- Console and/or CSV output from `BeanLogger` reflecting the object's actual movement.
- A path line in the Scene view from `BeanVisualizer` matching that movement.

That's the bar for "v1 works." Anything beyond that (custom extras, decimation tuning, color modes, replay) is valuable but not blocking.

## 11. How UTI Gets Tested

UTI's source of truth lives in this folder and stays independent — it is not embedded in or merged with any single game project. Instead, existing Unity projects add it as a **local package dependency** via a `"file:"` path in their own `Packages/manifest.json`, pointing back at this folder. Current test beds:

- `C:\Users\sirsw\Unity Projects\little wings` — 3D flight/combat game.
- `C:\Users\sirsw\project 2` (BoxJump) — 3D platformer, already has EditMode/PlayMode test projects set up.
- `C:\Users\sirsw\2d project 3` — 2D game.

All three reference the same live source, so a change here is immediately visible in each without copying files around — and testing across all three exercises UTI's "general-purpose across genres" goal directly (3D flight, 3D platforming, 2D).

**Gotcha confirmed in little wings:** a `"file:"` dependency alone isn't enough for the package's tests to show up in Test Runner — Unity needs the package id listed under a `testables` array in `manifest.json` too, or EditMode/PlayMode tests silently show 0 found. See `TESTS/TestTracker.md` for the exact manifest snippet.

## 12. Verification Strategy

**Whatever tooling access a given session has (none, read-only, or real execution — see
`CLAUDE.md`, it varies and should be checked fresh each time), the loop is the same:**

1. Code gets written for a milestone.
2. Precise Editor steps are handed over (what to add, what scene/project to test in, what to look
   for) — or, if the session has real execution access, run directly and the result is verified
   before being reported, not assumed.
3. A real report comes back — either from the session's own direct testing, or relayed from
   whoever ran it in one of the three test projects.
4. Iterate based on that report.

`TESTS/TestTracker.md` status values should reflect this honestly — "Planned" until actually run,
"Pass"/"Fail" only after a real report back, not assumed from reading the code.

### CI (added 2026-08-08)

`.github/workflows/tests.yml` runs the EditMode suite (`TESTS/EditMode/`) headlessly on every push
to `main`, against the minimal project shell at `CI~/` (see §4). This automates the
"code gets written, EditMode tests get run" half of the loop above — it does **not** replace live
verification of anything Play-Mode-dependent (`BeanVisualizer`'s actual gizmo draw,
`BeanSnapshotExporter`'s actual capture, `BeanMouseTracker`'s actual input read), which still needs
a real session against one of the three consuming projects, same as before.

**Push-only trigger, deliberately, not `pull_request`.** Only someone with write access can push
to `main`, so an outside contributor's PR can never run this workflow with access to its secrets,
regardless of GitHub's fork-PR approval settings. PR-triggered runs can be added later, scoped more
carefully, if the project ever gets outside contributors.

**Real Unity license, activated live each run — not a portable file, and this took two attempts to
get right.** First attempt used `game-ci/unity-test-runner` with a `UNITY_LICENSE` secret holding
an exported license file (the classic, widely-documented approach). That failed a due-diligence
check before ever reaching CI: Unity's licensing backend changed since most of that documentation
was written — manual activation of a **Personal** license through `license.unity3d.com/manual` was
discontinued (that page is Pro/Plus-serial-only now), and the newer license format Unity 6000.x
issues locally (`UnityEntitlementLicense.xml`, replacing the old portable `Unity_lic.ulf`) has
machine-binding identifiers baked into the signed entitlement — a license exported from one
machine will not validate on a different one, and all GitHub-hosted runners present a shared
HardwareId different from any real machine. **Fixed** by switching to `buildalon/unity-setup` +
`buildalon/activate-unity-license` + `buildalon/unity-action` — actively maintained actions built
for the post-transition licensing client, which log in live each run (`UNITY_EMAIL`/
`UNITY_PASSWORD` secrets) rather than relying on a pre-exported file, so the resulting license is
always correctly bound to whichever runner is actually executing. The real tradeoff this
introduces: an actual Unity account password now lives in this repo's secrets, not just a license
blob — the push-only trigger above exists specifically to keep that exposure as narrow as
reasonably possible.

**First live run failed on a real, findable bug — not a licensing problem this time.** Once the
`UNITY_EMAIL`/`UNITY_PASSWORD` secrets were added, the license-activation step actually worked (no
error there at all — confirms the approach above is sound). It failed one step later, in
`buildalon/unity-setup`'s own version-detection: `Error: No accessible file found for glob pattern:
.../**/ProjectVersion.txt`. Root cause: that glob searches the whole repo for `ProjectVersion.txt`
to confirm the Unity version, and generic (non-Unity-aware) glob implementations skip
dot-prefixed directories by default — the project shell lived at `.github/ci-project/` at the
time, so the file was never reachable no matter how correct everything else was. **Fixed** by
moving the whole shell to `CI~/` (see §4) — same UPM "don't import this" convention as `Samples~/`,
but visible to a plain glob unlike a dot-prefixed folder. Not yet re-verified as of the day this
was written — one more real run is still needed to confirm the move actually fixes it and nothing
else is wrong, same "don't claim Pass before a real report" discipline as everything else this
project tracks.

### The Bring-Your-Own-Test Protocol (formalized 2026-08-08, from a real `project 2` round)

**The ideal way to verify UTI in any consuming project: find a test that already exists and
already passes, add the relevant Beans to whatever GameObject it exercises, run that test
completely unmodified, and report what UTI produced.** This also means never building a new
demo/sample Unity project or scene purely to test or showcase a UTI feature — confirmed 2026-08-08
after proposing exactly that (a `Samples~/` car/NPC/player demo scene for T08) and being corrected
directly: we are testers using other people's real projects, not developers building our own, and
standing up even a simple project/scene from scratch costs real, substantial time and tokens for
very low verification value. See `TESTS/TestTracker.md` T08 and `CLAUDE.md`. This isn't just a convenience — it's the
actual test of UTI's core promise (drop-in, zero required changes, works with whatever's already
driving a GameObject). If verifying UTI ever requires writing new test/harness logic, that's a
process mistake to correct, not a normal step — UTI existing at all shouldn't require a single line
of new test code anywhere.

Concretely:

1. **Confirm install** — package resolves, `UTI > Setup Project (Config + Docs)` run, zero console
   errors. (See `TESTS/TestTracker.md` T27 for the full fresh-install version of this step.)
2. **Confirm the consuming project already has a genuinely working test or scenario** — not one
   built for UTI's benefit. A pre-existing, already-passing test is what proves UTI didn't need to
   change anything about how the project actually runs.
3. **Add the relevant Bean components** (`BeanTracker` + whichever of `BeanLogger`/
   `BeanVisualizer`/`BeanSnapshotExporter` are useful) to the GameObject(s) that test already
   exercises. Nothing about the test itself changes.
4. **Run that existing test completely unmodified**, however it's normally run — a human pressing
   Play, the real Test Runner, CI. Do not rewrite its movement/input logic into a new harness "to
   make it drivable" — if the test can't be run through whatever tooling is doing the verifying,
   that's a tooling-access limitation to route around (hand it to a human, or to whichever agent
   *can* actually drive Play Mode), not a reason to reimplement the test.
5. **Report back what UTI actually produced** — the CSV, the console log, the PNG, the live
   Scene-view line — that's the actual deliverable this whole protocol exists to produce.

**Where this came from:** a real `project 2` round (`TESTS/TestTracker.md` T27) hit exactly the
failure mode step 4 warns against — an agent's tooling couldn't drive the project's real PlayMode
test through Test Runner (Play Mode entry is blocked as unsupported "user interaction" in that
kind of MCP connection — the same limitation this session's own Unity MCP access confirmed
directly for `TestRunnerApi` in PlayMode specifically), so it started rewriting the test's
movement/jump-trigger logic into a temporary harness script instead — which immediately introduced
its own new bug (a steering error the original, real test didn't have). The fix isn't a UTI change
at all: a human (or whichever agent can actually enter Play Mode) should just press Play with the
real, already-working test running and the Beans already attached.

## 13. Known Limitations & Risks (v1)

v1's automated tests (18/18 green at the time this section was first written — 97/97 as of
2026-08-08, see `TESTS/TestTracker.md` T29) cover the "one Bean, hit Play, see output" path
solidly, but that path doesn't exercise how UTI behaves under the load patterns common to some of
its target
genres — particularly shooters, AI-heavy scenes, and anything using object pooling for bullets/
projectiles. These are real gaps found by reading the actual implementation, not speculative:

- ~~CSV file path collisions on duplicate GameObject names.~~ **Fixed 2026-08-07** (unit-tested,
  not yet confirmed with a live multi-clone Play Mode check). `BeanLogger.ResolveFilePath()` now
  defaults to `{timestamp}_{objectName}_{uniqueToken}_bean.csv` under `UTI/BeanLogs/` — two
  simultaneously-tracked clones (`Bullet(Clone)`, `Enemy(Clone)`, ...) get distinct files even if
  opened in the same frame/millisecond, since `uniqueToken` is a fresh random GUID fragment per
  capture, not tied to any Unity object-identity API. See §8.5.
- ~~**Object pooling isn't accounted for.**~~ **Decided and built 2026-08-08 (T14).** The default
  stays truncate-on-reopen (a fresh log per reuse) — now a documented decision (this paragraph),
  not an accident. New opt-in `BeanLogger.AppendAcrossReuse` (off by default) lets a pooled object
  keep one running CSV across `SetActive(false)`→`SetActive(true)` cycles instead. The wrinkle this
  needed: `BeanLogger.BuildActiveOutputs()` resolves a *fresh, newly-timestamped* path on every
  `Open()` — a bare append flag alone wouldn't have appended to the same file across reuse, since
  each re-enable would still get a brand-new filename. Fixed by caching the resolved path on this
  `BeanLogger` instance's first `Open()` and reusing it on subsequent `Open()` calls only when
  `AppendAcrossReuse` is true; `CsvBeanOutput` itself gained an `append` constructor parameter and
  only writes the header row when the target file doesn't already exist, so a reopened append-mode
  file doesn't get a second header partway through. Not yet live-verified.
- ~~**`EveryFixedUpdate` has no automated test coverage.**~~ **Fixed 2026-08-08 (T12).**
  `BeanTracker.FixedUpdate()` now calls a new public `SimulateFixedFrame()` (mirroring
  `SimulateFrame(deltaTime)`'s existing pattern) instead of calling `Capture()` directly, so
  `EveryFixedUpdate` capture is exercised deterministically in EditMode without a real physics
  tick. Not yet live-verified against real physics-driven movement.
- ~~**`CustomCapture`/`extras` is untested beyond "null by default."**~~ **Fixed 2026-08-08
  (T11).** New EditMode tests exercise the actual pipeline: a delegate assigned, `SimulateFrame()`
  invoking it, the result landing in `BeanSample.Extras`, and `CsvBeanOutput` serializing it into
  the `extras` column with real `key=value` data — not just the previous "null when unassigned"
  coverage.
- **`extras` is numeric-only** (`Dictionary<string, float>`). Fine for velocity, health, ammo
  count — awkward for AI state that's naturally categorical (patrol/chase/attack), which would
  need the caller to pre-encode it as a float (e.g. an enum cast) rather than UTI supporting it
  directly. Still open — no change this round.
- **Multi-Bean scenes are untested — partially closed 2026-08-08 (T15).** New EditMode test drives
  several `BeanTracker` instances independently via `SimulateFrame`, confirming each one's buffer
  reflects only its own object (true by construction — no shared static state — now actually
  checked, not just assumed). Still open: whether `BeanVisualizer`'s gizmo draw stays cheap when a
  scene has many active Beans at once (each decimates independently; total draw calls scale with
  Bean count, not verified against a realistic count like a wave of enemies) — that half stays a
  live Play Mode check, not EditMode-testable.
- ~~**`BeanSnapshotExporter`'s `MinFramingRadius` (`2f`, §8.4) is a fixed literal, not
  scale-aware.**~~ **Fixed AND verified live 2026-08-08 (T23) — see §8.4's "T23 fix and multi-angle
  capture" and §8.7.** Now a per-Bean serialized field, defaultable project-wide via `BeanConfig
  .DefaultMinFramingRadius`. Confirmed live in `project 2` via a real capture, viewed directly: the
  same near-stationary path produced the reported useless close-up at `MinFramingRadius=2` and a
  properly-framed shot at `=9`. See `TESTS/TestTracker.md` T23.
- ~~**`BeanSnapshotExporter` frames/draws from the live sample buffer, which can silently evict
  the real path.**~~ **Found live by the `project 2` team, root-caused and fixed 2026-08-08 (T28).**
  `CaptureSnapshot()` reads `BeanTracker.Samples` — the live ring buffer (capacity `MaxSamples`,
  default 1000), not the CSV. A long idle tail after real movement finished (e.g. tracking left
  running well past "the interesting part") can silently evict the *entire* real path from that
  fixed-capacity buffer before a snapshot ever happens, leaving only near-identical stationary
  samples behind — which frames as a tight, context-free close-up (superficially resembling T23,
  but a genuinely different mechanism) and draws as an invisible near-zero-length path line, with
  no error anywhere to explain why. Confirmed live, not just theorized: reproduced directly in
  `project 2` (200 samples of real 9m movement + 3000 stationary samples → the live buffer's
  Z-span dropped to exactly 0, first and last buffered positions identical). **Fixed** with a new
  pure `IsBufferAtCapacity(sampleCount, maxSamples)` check and a `Debug.LogWarning` in
  `CaptureSnapshot()` when the buffer is full, explaining what may have happened and how to avoid
  it (raise `Max Samples`, or call `StopTracking()` promptly). Doesn't change framing/rendering
  behavior — makes the failure mode visible instead of silent. **`BeanVisualizer.DrawPath()` reads
  the exact same live `Samples` buffer** and is subject to the identical eviction, though no code
  fix was made there — a live trail only ever showing recent history is normal/expected behavior
  for a live view, unlike `BeanSnapshotExporter`, whose entire purpose is summarizing the *whole*
  run after the fact. Documented as a shared "best practice: stop tracking promptly" callout across
  both in `USAGE.md`'s "Known constraints" section rather than a code change to `BeanVisualizer`.
  See `TESTS/TestTracker.md` T28.

None of these block v1's Definition of Done (§10) as originally scoped — they're the gap between
"works for one Bean in a clean demo" and "holds up in the genres this is meant to serve." See
README.md's Roadmap for the corresponding fix/feature ideas, and `TESTS/TestTracker.md` (T11+)
for the new test rows tracking them.

## 14. Error Handling & Fault Isolation (added 2026-08-08)

**Tracked at-a-glance in `TESTS/ErrorHandlingTracker.md`** (EH01–EH10) — same spirit as
`TESTS/TestTracker.md`, one row per guarded boundary with an honest verification status, added
per explicit request to track error handling with the same rigor as feature tests rather than only
narratively in this section.

**Philosophy: guard system boundaries, not internal invariants.** UTI is a debug/QA tool attached
alongside real gameplay — it should never be the reason a playtest session or a game crashes.
Before this pass, every place UTI crosses a genuine boundary (caller-supplied code, disk I/O, an
externally-edited config file) had no handling at all: a single failure would propagate up through
whatever Unity callback triggered it (`Update()`, `OnEnable()`, a Reset() menu action) and, in the
worst case, silently break far more than the one thing that actually failed. Internal invariants
(e.g. `BeanBuffer`'s constructor already throwing `ArgumentOutOfRangeException` on a bad capacity)
are untouched — that's a genuine programming error, not a runtime boundary, and should fail loudly
and immediately during development, not be swallowed.

**`BeanTracker.Capture()` — `CustomCapture` is caller-supplied code.** A throwing delegate used to
propagate out of `Capture()` entirely, meaning nothing after the delegate call ran: no sample
added, `tickIndex` never advanced, `OnSample` never fired — every single frame the broken delegate
was invoked, silently corrupting the whole recording rather than just that one field. Now wrapped
in try/catch: a failure logs a warning naming the object and continues, capturing that sample with
`Extras = null` instead of losing the sample (and everything downstream of it) entirely.

**`BeanLogger` — every `IBeanOutput` call is file I/O or otherwise external.** `Open()`/`Write()`/
`Close()` on each active output are now individually try/caught rather than a bare `foreach`. Before
this, one broken output (e.g. a CSV path with a permission problem) failing inside `Open()`'s loop
would skip `tracker.OnSample += HandleSample; isOpen = true;` entirely — silently disabling every
*other* output too (Console included), even ones that had already opened successfully, with no
signal beyond a single uncaught exception in the console. Now a failing output logs a warning
naming its type and is dropped from `activeOutputs` (`Open`/`Write` failures) or just logged past
(`Close` failures, since there's nothing left to drop from) — every other output keeps working
uninterrupted. Verified with `TESTS/EditMode/BeanLoggerTests.cs`'s new `ThrowingBeanOutput` test
double (`Open_OneOutputThrowsOnOpen_...`, `HandleSample_OneOutputThrowsOnWrite_...`,
`Close_OneOutputThrowsOnClose_...`).

**`BeanSnapshotExporter.CaptureSnapshot()` — one angle's disk write shouldn't lose the others.**
The per-angle render/encode/write block (introduced by multi-angle capture, §8.4/T24) is now
wrapped in its own try/catch/finally: a write failure on one angle (disk full mid-capture, a
permissions issue) logs a warning naming the angle and moves on to the next one, instead of
aborting the whole `CaptureSnapshot()` call and losing angles that already wrote successfully. The
`finally` also guarantees the per-angle `Texture2D` is always destroyed, even on failure - without
it, a failure mid-write would leak that texture. **Update, live-verified 2026-08-08:** this
session's own Unity MCP connection could drive `CaptureSnapshot()` directly (see the T22/T23/T24
entries in `TESTS/TestTracker.md`'s Change Log), which surfaced a real, related bug in the process
(T26): `Object.Destroy()` on the temporary line/texture objects is a documented no-op outside Play
Mode, silently leaking a `BeanSnapshotPath` GameObject into the scene every single capture when
called from an Editor context. Fixed with a new `SafeDestroy()` helper (`Application.isPlaying ?
Destroy() : DestroyImmediate()`) applied to all three `Destroy()` call sites in `CaptureSnapshot()`,
re-verified live immediately after (zero leaks, zero errors on an identical capture). Not
EditMode-testable itself (needs a live Camera), but about as thoroughly live-verified as a fix in
this category gets.

**`BeanConfig.Load()` — `BeanConfig.txt` is an externally-edited file.** A locked or
permission-denied file used to throw straight out of `Reset()` (the Editor's "add component" hook)
with a raw stack trace. Now a read failure is caught and treated the same as "file doesn't exist" —
returns `null` (compiled-in defaults apply) with a warning, matching the method's own existing
"returns null if it doesn't exist yet" contract rather than adding a new failure mode alongside it.
`CreateTemplateIfMissing()` and the new `CopyEndUserDocsIfMissing()` (§8.7, the `UTI > Setup
Project` menu item) are similarly wrapped — a write/copy failure logs a clear warning instead of an
unhandled exception in an Editor menu action. **Not independently unit-tested** — like `Load()`'s
happy path, this touches the real project-root `UTI/` folder rather than an injectable path, so
it stays in the same "verified by reading, not by an automated test" category `DESIGN.md` already
documents for the rest of `BeanConfig`'s real file I/O (§8.7's Testability paragraph).

## Change Log

- 2026-08-08 — **CI's first live run found a real bug: `CI~/` moved from `.github/ci-project/`**
  (see §4/§12). License activation itself worked on the first try with `UNITY_EMAIL`/
  `UNITY_PASSWORD` — the failure was one step later, `buildalon/unity-setup`'s own
  `ProjectVersion.txt`-detection glob silently skipping the dot-prefixed `.github/` directory the
  project shell lived in. Moved to a tilde-suffixed name instead (`CI~/`, matching `Samples~/`'s
  existing UPM convention), which keeps the same "don't import into a consuming project" property
  while staying visible to a generic glob. Not yet re-verified with another real run.
- 2026-08-08 — **CI's license activation approach corrected before its first real run** (see §12).
  The original plan (`game-ci/unity-test-runner` + a `UNITY_LICENSE` secret holding an exported
  license file) hit two dead ends during due diligence, neither found by reading code — both found
  by actually trying the real-world steps and checking the result: (1) `license.unity3d.com/manual`
  no longer supports Personal-license activation, Pro/Plus-serial-only now; (2) the newer license
  format Unity 6000.x issues (`UnityEntitlementLicense.xml`) is machine-bound, so a file exported
  from any one machine can't be reused on GitHub's runners. Switched to `buildalon/unity-setup` +
  `buildalon/activate-unity-license` + `buildalon/unity-action`, which activate a Personal license
  live each run via `UNITY_EMAIL`/`UNITY_PASSWORD` secrets instead of a portable file - the
  workflow trigger was narrowed to push-only (not `pull_request`) specifically because this means a
  real account password is now a repo secret, not just a license blob.
- 2026-08-08 — **Added CI (§12) and `docs/ONBOARDING.md`, following a project review.** New
  `.github/workflows/tests.yml` + `.github/ci-project/` (a minimal, scrubbed Unity project shell —
  see §4) run the EditMode suite headlessly on push/PR; not yet exercised live (needs a Unity
  license secret added to the repo first). `ci-project/ProjectSettings` started as a copy of a real
  consuming project's settings, then had every identifying field (company/product/project name,
  Unity Cloud project ID, organization ID) scrubbed before being committed — confirmed clean via a
  full-folder string sweep before anything was written. Also added `docs/ONBOARDING.md`, a short
  stable map for a fresh agent session, distinct from this file's deep architecture and
  `HANDOFF.md`'s ephemeral state.
- 2026-08-08 — **JSON Lines export shipped and verified live, plus a duplication refactor.** New
  `JsonlBeanOutput` (`IBeanOutput`), wired into `BeanLogger` as `BeanOutputTargets.Json` alongside
  `Console`/`Csv`; `BeanConfig` gained `DefaultOutputTargets` and `BeanLogger` gained its first
  `Reset()`/`ApplyConfigDefaults()` pair to consume it (§8.2/§8.7). `ResolveFilePath` generalized
  with an `extension` parameter (default `"csv"`, every existing call site keeps compiling
  unchanged); an explicit `FilePath` is ignored for both formats when CSV and JSON are active
  together, same collision precedent as `BeanSnapshotExporter`'s multi-angle capture. Once both
  `CsvBeanOutput` and `JsonlBeanOutput` existed side by side, their `StreamWriter`-lifecycle code
  (directory creation, flush-interval batching, `Close()`) turned out to be identical except for
  per-line formatting and whether a header exists — extracted into a shared `BeanFileOutputBase`
  (§8.2) rather than left duplicated across two files. **Verified live in `project 2`** via this
  session's direct Unity MCP connection, both before and after the refactor: clean compile, 0
  console errors, 97/97 EditMode tests passing (10 new tests covering JSON output, `ApplyConfigDefaults`,
  and the CSV+JSON collision fallback). This closes the "unverified, suspected of spamming the
  console" open item from a prior session's `HANDOFF.md` — it wasn't the JSON code; both compiled
  clean the whole time.
- 2026-08-08 — **T08 descoped from "build demo scenes" to "use existing projects" — standing rule
  added.** Proposed building `Samples~/` car/NPC/player demo scenes; corrected directly by the user:
  building a new demo/sample Unity project purely to test/showcase UTI is out of scope (real
  time/token cost, low verification value, contradicts the Bring-Your-Own-Test Protocol's whole
  premise). §4/§9/§12 updated to reflect this; `TESTS/TestTracker.md` T08 repurposed; the old
  demo-scene idea moved to `PROJECT_OVERVIEW.md`'s Dream To-Do section; rule also written into
  `CLAUDE.md` and session memory so it isn't re-proposed.
- 2026-08-08 — **T12/T14 Play Mode gaps closed live** (§13's last two open robustness rows). This
  session's Unity MCP connection turned out to support real Play Mode entry and `GameObject
  .SetActive`, both hard-blocked in every prior session — see `TESTS/TestTracker.md`'s Change Log
  for the full write-up (732 fixed-tick samples at exactly `Time.fixedDeltaTime` for T12; a real
  2-cycle `SetActive` pooling test with matching truncate/append CSV row counts for T14). No
  `Runtime/` source changes — this was pure verification, both behaviors already matched their
  documented design. EditMode suite re-confirmed 105/105 with no regressions afterward.
- 2026-08-08 — **Went public.** Repo created at github.com/DataFright/Unity-Testing-Inspector (MIT),
  first commit pushed. This file moved from the package root to `docs/DESIGN.md` as part of a
  cleanup for the public repo — root now holds only `README.md` (short pitch + fresh-clone install
  guide, replacing the old hardcoded-local-machine `file:` instructions with the real Package
  Manager git-URL flow), `LICENSE`, `package.json`, `CLAUDE.md`, `Runtime/`, and `TESTS/`; everything
  else (`PROJECT_OVERVIEW.md` — the renamed former `README.md` — plus this file, `HANDOFF.md`, and
  the three end-user docs) lives under `docs/`. `BeanConfig.CopyEndUserDocsIfMissing()`'s source
  path updated to match (`docs/<filename>` instead of the package root); verified live via
  `project 2`'s own Unity MCP connection that all three docs resolve correctly at the new location
  with valid content, and the old root-level path is genuinely empty (no stale duplicates).
- 2026-08-08 — **T28 (§13): found live by the `project 2` team, root-caused live by this session,
  fixed same day.** `BeanSnapshotExporter.CaptureSnapshot()` reads the live `BeanTracker.Samples`
  ring buffer, not the CSV — a long idle tail after real movement finished can silently evict the
  entire real path from that fixed-capacity buffer before a snapshot happens, explaining both an
  invisible path line and a tight, context-free close-up as one mechanism, not two bugs and not a
  config mistake. Confirmed by direct reproduction in `project 2` itself (200 real-movement samples
  + 3000 stationary → live buffer Z-span dropped to exactly 0). Fixed with a new pure
  `IsBufferAtCapacity()` + a `Debug.LogWarning` in `CaptureSnapshot()`. See §13 and
  `TESTS/TestTracker.md` T28 for full detail.
- 2026-08-08 — Rewrote the stale §12 (Verification Strategy — still described a no-tooling-at-all
  world) and formalized **the Bring-Your-Own-Test Protocol**: the standard way to verify UTI in any
  consuming project going forward — find a test that already exists and already passes, add the
  relevant Beans to what it exercises, run it completely unmodified, report what UTI produced.
  Written up directly from a real `project 2` T27 round hitting the failure mode it warns against:
  an agent's tooling couldn't drive the project's real PlayMode test through Test Runner (blocked
  Play Mode entry, same limitation this session's own Unity MCP access confirmed for `TestRunnerApi`
  in PlayMode specifically), so it started rewriting the test's movement logic into a temporary
  harness instead — which immediately introduced a new bug the original test didn't have. Not a UTI
  defect; the actual fix is procedural (have a human, or whichever agent can enter Play Mode, run
  the real existing test) — this section exists so future rounds don't repeat the detour.
- 2026-08-08 — New `TESTS/ErrorHandlingTracker.md` (EH01–EH09), tracking every guarded system
  boundary at-a-glance, same rigor as `TESTS/TestTracker.md` — per explicit request, cross-referenced
  from §14. Also: a bug report from the `project 2` team flagged a second occurrence of the
  ambiguous-`Object`-reference `CS0104` class (`BeanTrackerTests.cs`/`BeanLoggerTests.cs`, both newly
  needing `using System;` for this round's tests) — already fixed as a side effect earlier the same
  session, now formally confirmed via a full-repo sweep and tracked as EH09 rather than left as a
  one-off fix. `CLAUDE.md`'s Unity MCP note also corrected — it still described the old
  read-only/`little wings`-only limitation, now stale given this session's direct `project 2`
  execution access (see the entry below).
- 2026-08-08 — **Live verification round via this session's own Unity MCP connection, attached
  directly to `project 2`** (a real capability upgrade from every prior round's read-only/relay-only
  access - confirmed by testing, not assumed). Full EditMode suite: 84/84 passed, cross-checked
  against `TestResults.xml` with fresh timestamps to rule out a stale result. T22's `Reset()`-hook
  and the new `UTI > Setup Project` menu item confirmed live. T23 confirmed with real, viewed PNGs -
  the same near-stationary path produced the reported bug at the old default and a correctly-framed
  shot at a larger configured radius. T24 confirmed with 4 real, visually-distinct viewed PNGs.
  Found and fixed a genuinely new bug via this live testing (T26, §14): `Object.Destroy()` silently
  no-ops outside Play Mode, leaking `CaptureSnapshot()`'s temporary objects into the scene - fixed
  with a new `SafeDestroy()` helper, re-verified live. Also confirmed this connection's real limits
  by testing them directly: named `System.Reflection` types are sandboxed off (public-only
  reflection still works), and both Play Mode entry and `GameObject.SetActive` are blocked as
  unsupported "user interaction." Full detail in `TESTS/TestTracker.md`'s Change Log.
- 2026-08-08 — Added a new §14, Error Handling & Fault Isolation: guarded every real system
  boundary that previously had no handling at all. `BeanTracker.Capture()` now isolates a throwing
  `CustomCapture` delegate (logs a warning, keeps capturing without extras rather than silently
  corrupting the whole recording). `BeanLogger.Open()`/`HandleSample()`/`Close()` now isolate each
  `IBeanOutput` individually, so one broken output (a CSV permission error, say) can no longer
  silently disable every other output too - verified with a new `ThrowingBeanOutput` test double in
  `BeanLoggerTests.cs`. `BeanSnapshotExporter.CaptureSnapshot()`'s per-angle write loop now isolates
  one angle's disk-write failure from the others (not independently testable - needs a live
  Camera). `BeanConfig.Load()`/`CreateTemplateIfMissing()`/`CopyEndUserDocsIfMissing()` now treat a
  locked/permission-denied file the same as "operation failed, degrade gracefully" instead of
  throwing out of an Editor `Reset()`/menu action. Deliberately scoped to genuine boundaries
  (caller code, disk I/O, externally-edited config) per CLAUDE.md's "validate at boundaries, trust
  internal invariants" guidance - `BeanBuffer`'s existing constructor validation, for example, was
  left untouched since a bad capacity there is a real programming error, not a runtime boundary.
- 2026-08-08 — Closed most of the `project 2` round's punch list in one pass (see §8.4/§8.7/§13
  above for full detail; all code-complete and unit-tested, **none live-verified yet** — see the
  updated relay prompt in `TESTS/TestTracker.md`): T23 fixed (`MinFramingRadius` now a per-Bean
  field, defaultable via `BeanConfig.DefaultMinFramingRadius`); multi-angle snapshots built
  (`CaptureAngles`, `Auto`/`Above`/`Side`/`Behind`, grouped naming settled); T11/T12 closed with
  new EditMode tests (`CustomCapture`/extras end-to-end, `EveryFixedUpdate` via new
  `SimulateFixedFrame()`); T15's EditMode half closed (multi-Bean buffer independence); T14
  decided and built (`BeanLogger.AppendAcrossReuse`, opt-in, default unchanged) — required caching
  the resolved CSV path across reuse cycles, since the existing per-`Open()` fresh-timestamp
  resolution would otherwise have defeated append entirely. New `UTI > Setup Project (Config +
  Docs)` Editor menu item (§8.7) makes the recurring doc-copy gap a one-time fix. Also found: the
  three end-user docs (`USAGE.md`/`READING_LOGS_AND_VISUALS.md`/`CONFIG.md`) existed only as
  copies inside `little wings`, never actually saved back into this package repo — restored them,
  fixing a real staleness bug found in the process (`USAGE.md` §8 still described the old
  `ScriptableObject`-based `BeanConfig`, contradicting `CONFIG.md`'s already-correct plain-text
  description) — and refreshed both `little wings`' and `project 2`'s copies.
- 2026-08-08 — First full closing report from `project 2` — full story in `TESTS/TestTracker.md`'s
  Change Log. Headline result: UTI's CSV pinned a real game bug (a jump-trigger distance that was
  geometrically unreachable given the player's own collision radius) to five decimal places,
  something the dev said they wouldn't have found from behavior alone — the clearest real-world
  validation of the core pitch so far. Also surfaced a new, real, unfixed bug (§13:
  `MinFramingRadius` too small for that project's scale, T23) and confirmed the `.meta`-file
  concern from an earlier report self-resolved (Unity auto-generates them on first import).
- 2026-08-07 — `BeanConfig` (§8.7) rebuilt as a plain text file instead of a `ScriptableObject`
  asset, after user pushback on the previous entry's `Assets/UTI/` decision: the whole point of
  config was one place for a project's UTI footprint, and splitting it into `Assets/` while
  everything else lives in the plain `<project root>/UTI/` folder defeated that. Now
  `BeanConfig.txt`, read via plain `File`/`ParseLines`, no `AssetDatabase` involved — and `CONFIG.md`
  moves to the same `UTI/`-folder convention as `USAGE.md`/`READING_LOGS_AND_VISUALS.md` (previous
  entry) instead of its own separate `Assets/UTI/` destination. New "UTI > Create Bean Config"
  Editor menu item bootstraps the file with a commented template.
- 2026-08-07 — Corrected where `USAGE.md`/`READING_LOGS_AND_VISUALS.md` actually belong, per user
  feedback: they're end-user docs for a dev *using* UTI, not for developing UTI itself, so they
  need to live where that dev is actually looking — each game project's `<project root>/UTI/`
  folder (alongside the logs/snapshots it explains), not just this package repo. Copied both
  directly into `little wings`'s `UTI/` folder this session; `project 2` still needs this done
  (added to its prepared relay round).
- 2026-08-07 — Reverted `BeanTracker.EveryNTicks` (added earlier the same session) after user
  clarification: the actual ask was "give the dev the ability to choose and change [existing]
  settings," not a new capture mode — `EveryUpdate`/`EveryFixedUpdate` (tick-based) vs
  `EveryNSeconds` (time-based) already covered that choice, it just wasn't clearly documented
  (now is, in `USAGE.md`). Replaced with the actually-requested mechanism: new `BeanConfig`
  (§8.7), a `[CreateAssetMenu]` ScriptableObject holding a project's preferred defaults
  (`DefaultCaptureMode`/`DefaultCaptureInterval`/`DefaultDimensionMode`), applied via
  `BeanTracker.Reset()`/`BeanSnapshotExporter.Reset()` (Unity's Editor "component just added"
  hook) so every *new* Bean in that project starts pre-configured to match, still fully editable
  per-instance same as any other field. Also clarified where things live: the existing `UTI/`
  folder (project root, outside `Assets/`) is deliberately for generated *output* (logs/
  snapshots) that Unity shouldn't import as tracked assets — a config *asset* has to live inside
  `Assets/` to actually function as an editable Unity asset, so `BeanConfig` lives at
  `Assets/UTI/BeanConfig.asset` per game project instead. New `CONFIG.md` at the package root
  explains the mechanism and is meant to be copied alongside the created asset into each game
  project's `Assets/UTI/` folder.
- 2026-08-07 — T13/T16/T17 verified Pass live in little wings (25/25 tests across
  `BeanArtifactPathsTests`/`BeanLoggerTests`/`BeanSnapshotExporterTests`; real PNG+CSV confirmed
  on disk under the new `UTI/` folder, `LastLineWidth=3.47` confirming the width-scaling fix is
  live). That same verification round found a real bug (§8.4: the auto-frame camera's fixed
  offset direction foreshortened a path traveling parallel to it), fixed the same day — camera
  offset is now derived from the path's own travel direction instead. Three other additions per
  user feedback, all same session: `BeanSnapshotExporter.DimensionMode` (§8.4, explicit
  `Auto`/`Force2D`/`Force3D` override for the flat/2D-vs-3D framing guess); new `BeanMouseTracker`
  (§8.6, tracks the mouse cursor through the exact same pipeline as any other Bean via a small
  proxy Transform); and two new root-level docs, `USAGE.md` and `READING_LOGS_AND_VISUALS.md`.
  (A `BeanTracker.EveryNTicks` capture mode was also added and then reverted the same session —
  see the entry below; not what was actually being asked for.) None of today's new code has a
  real Play Mode verification yet.
  Next test target: `project 2`, a vertical platformer (jump to survive rising lava) — chosen
  specifically to stress-test `BeanVisualizer`/`BeanSnapshotExporter` against something more
  structurally interesting than `little wings`' open sky, and to directly exercise the new
  broadside-framing fallback on largely-vertical paths.
- 2026-08-07 — Fixed a real compile-blocking regression relayed back from little wings:
  `Object.GetInstanceID()`, used by the previous entry's uniqueness fix, was reported as
  `CS0619` (obsolete-as-error) against that project's Unity build. That specific deprecation claim
  couldn't be independently verified — `GetInstanceID()` is a foundational, extremely widely-used
  Unity API, and no record of it being replaced by a `GetEntityId()` method was found — but rather
  than gamble on an unconfirmed API, both `ResolveFilePath`/`ResolveSnapshotPath` were changed to
  take a `uniqueToken` string instead of an `instanceId` int, filled by a new
  `BeanArtifactPaths.NewUniqueToken()` (a short GUID fragment). Sidesteps the dispute entirely
  regardless of whether the claim is real, and is arguably stronger anyway (true randomness, no
  dependency on Unity's own object-identity internals). Still unverified with a real capture.
- 2026-08-07 — Two more refinements to UTI's generated-file handling, both per user feedback
  after the `persistentDataPath` fix below: (1) nested `BeanLogs/`/`BeanSnapshots/` one level
  further into a shared `UTI/` folder at the project root (`BeanArtifactPaths.RootDirectory` now
  points at `<project root>/UTI/`, with a new `ProjectRootDirectory` for the bare project root),
  so everything UTI generates lives in one clearly-labeled place instead of two loose sibling
  folders. (2) Closed the CSV half of the §13/T13 filename-collision gap to match the PNG side:
  both `BeanLogger.ResolveFilePath` and `BeanSnapshotExporter.ResolveSnapshotPath` now include a
  unique token alongside the timestamp (later revised from `GameObject.GetInstanceID()` to a
  random GUID fragment — see the entry above), so two same-named clones opening in the same
  millisecond still get distinct files, not just sequential reruns of the same object. Also
  confirmed (not changed) that both output formats — CSV and Console — are already plain text with
  a labeled/structured layout, so both a human and an AI reading the file directly can already
  parse them without special tooling; the PNG snapshot is the one artifact that needs vision to
  interpret, CSV/console don't. Neither fix verified with a real capture yet — see
  `TESTS/TestTracker.md`.
- 2026-08-07 — Moved UTI's default output location off `Application.persistentDataPath` entirely,
  for both `BeanLogger` (CSV) and `BeanSnapshotExporter` (PNG). Root cause: the designer went
  looking for a generated snapshot inside the `little wings` project folder and genuinely couldn't
  find it — `persistentDataPath` resolves to a hidden per-user `AppData/LocalLow/...` folder with
  no relation to the project directory, which defeats the entire "developer can review results
  afterward" point of this tooling. New shared `BeanArtifactPaths.RootDirectory` resolves to the
  project root instead (`Application.dataPath/..`, alongside `Library/Logs/Temp` — visible in the
  project folder a dev already has open); `BeanLogger` now defaults into `BeanLogs/` there,
  `BeanSnapshotExporter` into `BeanSnapshots/`. Both were on `persistentDataPath` before this - the
  CSV path was never flagged as a problem earlier only because nobody had gone looking for a CSV
  file the way they went looking for the snapshot. See §8.5.
- 2026-08-07 — Found and fixed a real bug in the auto-framing fix below, from the very next
  verification round: the folder (`BeanSnapshots/`) and unique timestamped filenames were
  confirmed working, but the auto-framed capture itself came back showing ground/sky/horizon and
  a tiny distant plane with **no visible path line**. Root cause: `lineWidth`'s default (`0.1`
  world units) was tuned for a close manual shot and becomes sub-pixel/invisible once
  `autoFrameCamera` correctly pulls the camera back far enough to fit an entire path in frame -
  the framing itself was working, the line just couldn't be seen at that scale. Fixed by scaling
  the line's actual render width to the computed framing distance/orthographic size
  (`LineWidthScaleFactor = 0.02`, only ever grows the line for a wide shot, never shrinks a
  manually-tuned one), and exposed the real number used (`LastLineWidth`) so a verification report
  can state it directly instead of inferring visibility from the image alone. Not yet re-verified
  - needs one more real capture.
- 2026-08-07 — T17's real capture (see TestTracker Change Log) surfaced three follow-on gaps in
  `BeanSnapshotExporter`, all fixed same-session: snapshots now default into a `BeanSnapshots/`
  subfolder instead of a loose file; filenames are now timestamp-prefixed so repeat captures on
  the same Bean (e.g. comparing 5 runs) don't overwrite each other; and a new `autoFrameCamera`
  (default on) computes the recorded path's bounding box and frames an orthographic front-on shot
  for flat/2D paths or an elevated 3/4 perspective shot for real 3D ones, sized to fit the whole
  path with margin — fixing the first real capture's problem of a tight follow-cam showing almost
  no usable context. New pure functions `ComputePathBounds`/`IsFlatPath`/`ComputeFraming` are unit
  tested; `ResolveSnapshotPath`'s signature changed to take a capture timestamp. See §8.4.
- 2026-08-07 — `BeanSnapshotExporter` tuned for speed over fidelity per user feedback: default
  capture resolution dropped from 1920x1080 to 640x360. The artifact is meant to be a fast,
  "good enough to understand at a glance" sanity check, not a polished screenshot — no quality
  tuning attempted beyond whatever the camera already renders. See §8.4.
- 2026-08-07 — Decided and built the persisted-visualization-artifact feature flagged earlier
  today: new `BeanSnapshotExporter` (§8.4) captures a real `Camera.Render()` — not gizmos — with
  the tracked path drawn in via a temporary `LineRenderer`, then writes it to disk as a PNG. Chosen
  over a CSV-replay viewer specifically because user feedback ("say someone falls through the
  floor, well you have to see the floor") made clear the artifact needs real scene geometry around
  the path, not just an abstract line — a real camera render gets that for free, a pure-data replay
  viewer would not. Added a small `OnStopTracking` event to `BeanTracker` so the exporter can
  auto-capture on tracking-stop without polling. Pure pieces (`BuildPathPositions`,
  `ResolveSnapshotPath`) are EditMode-tested (T16); the actual camera capture is not
  EditMode-testable (needs a live Camera), same category as `BeanVisualizer`'s gizmo draw — T17
  needs a real Play Mode check next. Side benefit: this also gives T05 a way out of its
  screenshot-tooling deadlock — verifying "can you see the path" becomes "open the PNG this wrote,"
  not "get an external tool to successfully screenshot the Editor."
- 2026-08-07 — Re-flagged §8.3's live-only-visualization gotcha as a real, flagged design gap
  (not a "stretch goal, not v1" aside) per user feedback: `BeanVisualizer` has no persisted
  artifact today, only a live Editor Gizmo draw, which undercuts the point of visualizing a run at
  all if it can't be reviewed after the fact. Tracked in README.md Roadmap; not designed or built
  yet. Also: a second T05/T06 live-Play verification round in little wings (real ~103s run, not
  scripted) got strong objective evidence for editor responsiveness (steady ~330-370fps via Unity's
  own frame-timing, zero console errors, measured after the buffer hit its 1000-sample cap) and a
  new real cross-check (CSV logged 36,789 rows over ~103s, consistent with that frame rate) — but
  the actual gizmo *render* still hasn't been seen; screenshot tooling failed a third time. Full
  detail in `TESTS/TestTracker.md`.
