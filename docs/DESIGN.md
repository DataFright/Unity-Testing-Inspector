# UTI — Architecture & Design

Tracking doc for how the pieces fit together and why they're built this way, as of *now*. Pairs
with [PROJECT_OVERVIEW.md](./PROJECT_OVERVIEW.md) (the pitch/concept/Roadmap) — this is the
"how we'll build it" side. See the [root README](../README.md) for the short pitch and install
steps. **Full narrative history — multi-round design revisions, the CI setup saga, old resolved
limitations — lives in [DESIGN_HISTORY.md](./DESIGN_HISTORY.md)**, not here; this file states what's
true now.

## Target Environment

Unity **6000.5.6f1** (Unity 6 LTS) — the actual installed Editor version across all three test
projects (`little wings`, `project 2`, `2d project 3`). This is the real baseline for API
compatibility decisions.

## 1. Data Flow

```
BeanTracker (captures)
    │  emits BeanSample on each tick
    ├──> BeanLogger (outputs: console / CSV / JSON Lines / custom)
    ├──> BeanVisualizer (reads buffer, draws path in Scene view)
    └──> BeanSnapshotExporter (reads buffer, renders path + scene into a PNG)
```

`BeanTracker` doesn't know any of the others exist — they just read from it (event or buffer pull).
Keeps the "decoupled pieces" promise real, not just aspirational.

## 2. Core Types

### `BeanSample` (struct)
The unit of data one tick produces: `TickIndex` (int), `Timestamp` (`Time.time` at capture),
`Position`, `Rotation`, and `Extras` (`Dictionary<string, float>`, optional custom fields). `Extras`
is **null by default, not an empty dictionary** — only allocated when a custom capture delegate is
actually assigned, so the common case (no custom fields) doesn't allocate a `Dictionary` every tick
for zero benefit.

### `BeanTracker` (MonoBehaviour)
Captures data on any GameObject it's attached to. Config (Inspector): capture mode (`EveryUpdate` /
`EveryFixedUpdate` / `EveryNSeconds`), interval value, capture-rotation on/off, max buffer size
(ring buffer, default 1000 samples). Public surface: `event Action<BeanSample> OnSample`,
`IReadOnlyList<BeanSample> Samples`, an `OnStopTracking` event, and an optional
`Func<GameObject, Dictionary<string,float>> CustomCapture` slot for custom extras.

### `BeanLogger` (MonoBehaviour)
Attaches alongside (or references) a `BeanTracker`. Subscribes to `OnSample`, writes it out via one
or more `IBeanOutput`s (`[Flags]` `OutputTargets`: `Console` / `Csv` / `Json`, any combination).
Default file location: `UTI/BeanLogs/` under the project root, timestamp+unique-token-named — see
`BeanArtifactPaths` (§8.5). `ConsoleBeanOutput`/`CsvBeanOutput`/`JsonlBeanOutput` are UTI's own
three built-in `IBeanOutput` implementations; anyone can write their own sink without touching core
code.

### `BeanVisualizer` (MonoBehaviour)
Draws the recorded path in the Scene view via `OnDrawGizmos`/`OnDrawGizmosSelected`, reading
`tracker.Samples` directly — no event subscription, just draws whatever's currently in the buffer.
Config: line color, `ColorMode` (`None`/`BySpeed`/`ByTime`), point markers on/off, a decimation cap
(`maxPointsToDraw`) for perf on long sessions. Live/during-Play only — see §8.3 for why there's a
separate persisted-artifact component (`BeanSnapshotExporter`) instead of extending this one.

## 3. Extensibility Points

- `IBeanOutput` — plug in custom log destinations (`BeanLogger.CustomOutputs`).
- Custom capture delegate on `BeanTracker` — plug in custom data beyond transform.
- `OnSample` event — anyone can subscribe for their own purposes (analytics, custom visualization,
  replay systems) without needing `BeanLogger`/`BeanVisualizer` at all.

## 4. Package Structure (Unity Package Manager format)

```
UTI/
  package.json
  README.md
  LICENSE
  CLAUDE.md / CLAUDE_HISTORY.md          (local-only, gitignored)
  .github/workflows/tests.yml            CI - runs the EditMode suite on push to main
  CI~/                                   minimal, scrubbed Unity project shell CI resolves
                                          tests from (no scenes/game content - see its own
                                          README.md)
  Runtime/                               the actual package code (see §2/§8)
    UTI.Runtime.asmdef
  Editor/                                (future: custom inspectors/editor windows - no
                                          asmdef yet, no Editor-only code to isolate)
  TESTS/
    EditMode/                            all current automated tests (NUnit, Unity Test Runner)
    PlayMode/                            scaffold only - no tests populated yet
    TestTracker.md / TestTracker_HISTORY.md
    ErrorHandlingTracker.md
    BugTracker.md
  docs/
    ONBOARDING.md / DESIGN.md (this file) / DESIGN_HISTORY.md
    PROJECT_OVERVIEW.md / PROJECT_OVERVIEW_HISTORY.md
    HANDOFF.md                          (local-only, gitignored)
    USAGE.md / READING_LOGS_AND_VISUALS.md / CONFIG.md
                                          end-user docs - for a dev USING UTI in their game, not
                                          for developing UTI. Copied verbatim into each consuming
                                          project's own <project root>/UTI/ folder by
                                          BeanConfig's Editor menu item.
  Samples~/                              (not built - see §12's Bring-Your-Own-Test Protocol;
                                          the trailing ~ is UPM convention so this wouldn't
                                          import by default if it ever existed)
```

`CI~/` is test infrastructure only (no game content) — tilde-suffixed rather than dot-prefixed
specifically so it stays visible to a plain (non-Unity-aware) glob; see `CI~/README.md` and
§12 for why that distinction mattered. `TESTS/` (docs) and a hypothetical `Tests/` (code) would
collide on Windows' case-insensitive filesystem, which is why the C# test code nests under
`TESTS/EditMode/` rather than a sibling `Tests/` folder.

## 5. Namespace

`UTI` — matches the working title. One identifier to rename later if/when the project name changes.

## 6. Configuration Philosophy

- Everything configurable from the Inspector on the component itself. No required ScriptableObject
  setup — stay drop-in.
- Defaults work with zero configuration: add `BeanTracker`, hit play, it just starts capturing.
- Project-wide shared defaults live in `BeanConfig` (§8.7), a plain text file, not a ScriptableObject.

## 7. Open Questions (resolved)

All of v1's open questions got settled during implementation: ring buffer default size (`1000`),
`BeanLogger`/`BeanVisualizer` vs `BeanTracker` locality (same-GameObject default via `GetComponent`,
with a `[SerializeField] tracker` override on both), CSV default location (project root's
`UTI/BeanLogs/`, not `Application.persistentDataPath` — see §8.5), `BeanVisualizer.maxPointsToDraw`
default (`200`). No open questions remain blocking v1.

## 8. Construction Plan

### 8.1 `BeanTracker`

**Fields:** `captureMode` (`EveryUpdate`/`EveryFixedUpdate`/`EveryNSeconds`), `captureInterval`
(only relevant for `EveryNSeconds`), `captureRotation` (bool), `maxSamples` (int, default 1000),
`startTrackingOnEnable` (bool, default true).

**Internal storage:** a real circular buffer (fixed-size array + head index + count), not a
`List<T>` with `RemoveAt(0)` — shifting a list every tick at high capture rates is wasteful.

**Buffer allocation is lazy** (`buffer ??= new BeanBuffer(...)`), not `OnEnable`-driven — without
`[ExecuteAlways]`, `OnEnable` never fires in Edit Mode, so an EditMode-created tracker would NRE on
first use if the buffer were allocated there. `[ExecuteAlways]` itself was rejected: it would make
`Update()`/`FixedUpdate()` run continuously in the Editor just from a Bean sitting in a scene,
burning CPU while idling and contradicting "hit play, see the trail."

**Lifecycle:** `OnEnable` starts tracking if `startTrackingOnEnable`. `Update`/`FixedUpdate`
delegate to public `SimulateFrame(deltaTime)`/`SimulateFixedFrame()` so capture timing is testable
deterministically without Play Mode. `Capture()` builds a `BeanSample` from the transform (+
optional custom delegate output), pushes it, increments `tickIndex`, invokes `OnSample`.

**Public API:** `StartTracking()`, `StopTracking()`, `ClearSamples()`, `SimulateFrame(float)`,
`SimulateFixedFrame()`, `Samples`, `OnSample`/`OnStopTracking` events, `CustomCapture` delegate slot.

### 8.2 `BeanLogger`

**Fields:** `tracker` (auto-`GetComponent` if unset), `outputTargets` (`[Flags]`, any combination of
Console/CSV/JSON), `filePath` (default: `UTI/BeanLogs/` under the project root — §8.5),
`appendAcrossReuse` (§13, for pooled objects).

**`IBeanOutput` contract:** `Open(BeanTracker)`, `Write(BeanSample)`, `Close()`. `BeanLogger` owns a
list of active `IBeanOutput`s built from `outputTargets`, plus any custom ones via `CustomOutputs`.

- **Console output** — formatted `Debug.Log` per sample. Low-frequency/dev use; floods the console
  fast at a high capture rate (by design — CSV/JSON is the fit for high-rate capture).
- **CSV output** — `StreamWriter`, header row written only on a genuinely new file (so append-mode
  reopens don't insert a second header), buffered writes with periodic flush.
- **JSON Lines output** (`JsonlBeanOutput`) — one JSON object per sample line (`.jsonl`, not a
  single top-level array — an array can't be safely appended to mid-run the way CSV already
  streams). The real motivation over CSV: `extras` becomes a real nested object with natively-typed
  values instead of CSV's one flat `key=value;key=value` string column.
- **`BeanFileOutputBase`** — shared base class for `CsvBeanOutput`/`JsonlBeanOutput` (identical
  `StreamWriter` lifecycle, differing only in per-line formatting and whether a header exists).
  `public` only because C# requires a base class at least as accessible as its subclass (CS0060) —
  not part of UTI's own extensibility surface, which stays `IBeanOutput`.
- **`ResolveFilePath`** — one shared static method (`extension` parameter, default `"csv"`) backs
  both CSV's and JSON's default path. If both CSV and JSON are active at once, an explicit
  `filePath` can't safely back two different files, so both fall back to their own default-named
  path instead of colliding (same precedent as `BeanSnapshotExporter`'s multi-angle capture, §8.4).
- **`ApplyConfigDefaults(BeanConfig)`/`Reset()`** — same `BeanConfig`-defaulting pattern as
  `BeanTracker`/`BeanSnapshotExporter` (§8.7): `Reset()` is Editor-only (fires when the component is
  added), calls `ApplyConfigDefaults(BeanConfig.Load())`; the public `ApplyConfigDefaults` is
  separated out purely so it's testable without touching the real filesystem.

**Lifecycle:** subscribes to `tracker.OnSample` in `OnEnable`, calls `Open()` on active outputs.
`Close()` runs in `OnDisable`, `OnDestroy`, *and* `OnApplicationQuit` — belt-and-suspenders, since
standalone builds don't reliably hit all three.

### 8.3 `BeanVisualizer`

**Fields:** `tracker` (auto-fetched), `pathColor`, `colorMode`, `drawPoints` (bool),
`maxPointsToDraw` (perf cap).

**Drawing:** `OnDrawGizmos`/`OnDrawGizmosSelected` reads `tracker.Samples` and draws
`Gizmos.DrawLine` between consecutive points (optional `Gizmos.DrawSphere` per point). Past
`maxPointsToDraw`, decimates (steps through at a computed interval) rather than drawing every
sample — a 1000-point gizmo path redrawing every editor frame is a real perf trap.

**`DrawPath()` takes an injectable `internal IGizmoDrawer`** instead of calling `Gizmos.*` directly
— a real `UnityGizmoDrawer` wraps the actual API for the live path (zero behavior change), and a
recording fake in the test assembly lets an EditMode test assert the exact draw-call sequence
(which segments, which colors, decimation, point-spheres). `Runtime/AssemblyInfo.cs` carries
`InternalsVisibleTo("UTI.Tests")` for this. This proves `BeanVisualizer`'s own logic; it does not
and cannot prove `Gizmos.DrawLine` renders visible pixels in a live Scene view — that's Unity's
engine contract, verified separately (`TESTS/TestTracker.md` T05).

**Fundamental limitation, not a bug:** gizmos only draw while the `BeanTracker`'s data still exists
in memory, and Play Mode state (including the buffer) is discarded on exiting Play Mode — so
`BeanVisualizer` alone is a *live, during-Play* view only, with no persisted artifact. That's what
`BeanSnapshotExporter` (§8.4) is for: a real `Camera.Render()` capture saved to disk, so there's
something to review after the run ends, share, or look at without the Editor open to that exact
live moment. Both are meant to coexist on the same GameObject — one for watching live, one for
reviewing after.

### 8.4 `BeanSnapshotExporter`

**Fields:** `tracker` (auto-fetched), `captureCamera` (defaults to `Camera.main` — reuses whatever
camera a dev already trusts to show their scene, rather than UTI inventing its own view),
`pathColor`, `lineWidth`, `captureWidth`/`captureHeight` (default `640×360` — deliberately low-res;
this is a fast sanity artifact, not a portfolio screenshot, bump per-Bean if a case needs more),
`autoFrameCamera` (bool, default on), `dimensionMode` (`Auto`/`Force2D`/`Force3D`),
`minFramingRadius` (float, default `2`), `captureAngles` (`BeanSnapshotAngle[]`, default
`[Auto]`), `captureOnStopTracking` (bool, default true), `filePath`.

**How it captures:** `CaptureSnapshot()` builds a temporary `LineRenderer` from
`BeanTracker.Samples` (via the pure, testable `BuildPathPositions`), points the capture camera at
an off-screen `RenderTexture`, calls `camera.Render()`, reads the pixels back, writes a PNG. Because
it's a real `Camera.Render()` — not gizmos — floor/walls/other geometry in frame get captured
exactly as a developer would see them in Play Mode, with the path drawn directly into that same
image. Works in an actual Player build too, not just the Editor Scene view. The temporary line
object and render texture are cleaned up in a `finally` block regardless of outcome, via a
`SafeDestroy()` helper (`Application.isPlaying ? Destroy() : DestroyImmediate()` —
`Object.Destroy()` is a documented no-op outside Play Mode, so calling `CaptureSnapshot()` from an
Editor script needs the immediate variant or it leaks temp GameObjects into the scene).

**Auto-framing** (`autoFrameCamera`, on by default): computes a bounding box of the recorded path
(`ComputePathBounds`) and repositions/reorients a *copy* of the capture camera's transform (restored
afterward) to frame the whole path with margin, via `ComputeFraming`/`ComputeFramingForAngle`:
- **Flat/2D path** (bounding-box depth along Z below a threshold — matches this project's
  convention that 2D scenes keep Z at 0) → an orthographic, front-on view sized to the path's X/Y
  extent.
- **Real 3D path** → a perspective view, camera offset derived from the path's own
  `travelDirection` (rotated 90° in the XZ plane, so the shot is always broadside regardless of
  which way the path runs — falls back to a fixed forward broadside for a purely vertical path).
- `dimensionMode` overrides the flat/2D-vs-3D auto-guess outright (`ResolveIsFlat`) for a 2D scene
  that doesn't keep everything at Z=0, or a 3D path that happens to come out flat.
- `minFramingRadius` is the floor on how close the camera is allowed to sit (both the orthographic
  half-size and the perspective distance), so a near-stationary path still frames with real margin
  instead of an unhelpful close-up. Defaultable project-wide via `BeanConfig.DefaultMinFramingRadius`
  (§8.7).
- Line width auto-scales up (`LineWidthScaleFactor`) to stay visible once auto-framing pulls the
  camera back far enough that the configured `lineWidth` would be sub-pixel — only ever grows a
  wide shot's line, never shrinks a manually-tuned one. `LastLineWidth` exposes the actual value
  used.

**Multi-angle capture** (`captureAngles`): `BeanSnapshotAngle` (`Auto`/`Above`/`Side`/`Behind`).
`Auto` is the original single-shot behavior above. `Side` is the same broadside 3D placement `Auto`
uses for a real 3D path (factored into `ComputeSideFraming`, shared by both). `Above` looks straight
down (up-hint derived from the path's own horizontal travel direction, since `Vector3.up` is
degenerate looking straight down). `Behind` sits opposite the path's travel direction. All three
non-`Auto` angles share one `ComputePerspectiveDistance` formula, differing only in offset
direction. More than one angle in `captureAngles` writes one PNG per angle in a single
`CaptureSnapshot()` call, sharing a group timestamp/token:
`{timestamp}.{n}_{objectName}_{angleName}_{uniqueToken}_bean_snapshot.png` (1-based `n`). An
explicit `filePath` override is only honored for a single-angle capture — one fixed path can't
safely back more than one output file, so multi-angle capture always uses the default
`UTI/BeanSnapshots/` location. `LastSnapshotPath` holds the last file written (backward-compatible
with single-angle usage); `LastSnapshotPaths` lists every file from the most recent call.

**Reads the live sample buffer, not the CSV** — `IsBufferAtCapacity(sampleCount, maxSamples)` warns
in the console when the tracker's ring buffer is full, since older samples may already have been
evicted (a long idle tail after real movement finished can silently push the interesting path out
before a snapshot happens). The CSV, if `BeanLogger` is attached, keeps the full history regardless.

**Known limitation, accepted:** the 2D/3D detection is a Z-extent threshold, not a real
scene-orientation check — a 2D game built on a non-standard plane would fool it. Every test project
so far matches the assumed convention.

Full revision history for this component (the persistentDataPath fix, the broadside-framing fix,
the T23 close-up bug, three rounds of auto-framing refinement): `DESIGN_HISTORY.md`.

### 8.5 Where UTI's generated files actually live (`BeanArtifactPaths`)

`BeanArtifactPaths.ProjectRootDirectory` resolves to `Application.dataPath/..` — the project root in
the Editor (one level up from `Assets/`, alongside `Library/`/`Logs/`/`Temp/`) or the folder next to
the executable in a Player build. `RootDirectory` nests one level further into its own `UTI/`
folder there, so `BeanLogger` (`UTI/BeanLogs/`) and `BeanSnapshotExporter` (`UTI/BeanSnapshots/`)
both land in one predictable, easy-to-find place — deliberately **not**
`Application.persistentDataPath` (a hidden, per-user AppData folder with no relation to the project
directory a developer actually has open).

Default filenames are timestamp *and* random-token unique
(`{timestamp}_{objectName}_{uniqueToken}_...`, via `BeanArtifactPaths.NewUniqueToken()`, a GUID
fragment) — so two same-named clones (e.g. `Bullet(Clone)`) capturing in the same millisecond still
get distinct files, and repeated runs on the same object don't overwrite each other.

**Known tradeoff, accepted:** writing next to a Player build's executable can fail if that build is
installed somewhere write-protected (e.g. `Program Files`) without elevated permissions — not a
concern for UTI's actual use case (a dev/testing tool run from the Editor or a local dev build), so
not solved further unless a real case needs it.

### 8.6 `BeanMouseTracker`

A small proxy, not a change to `BeanTracker` itself: each `Update()`, it writes the mouse cursor's
position into *its own* Transform, and an ordinary `BeanTracker` on the same GameObject captures it
exactly like any other moving object — nothing downstream (`BeanLogger`, `BeanVisualizer`,
`BeanSnapshotExporter`) needs to know or care that the source is a mouse.

**Fields:** `trackingSpace` (`Screen`/`World`), `worldCamera` (World mode only, defaults to
`Camera.main`), `worldDistanceFromCamera` (float, default 10, World mode only).

**Screen mode** writes raw `Input.mousePosition` (pixels, Z=0) directly. **World mode** projects
that screen position into 3D world space via `camera.ScreenToWorldPoint` at a fixed distance —
useful for tracking where a mouse-aimed reticle actually points in the game world.

**Deliberately uses the legacy Input Manager (`Input.mousePosition`), not the Input System
package's `Mouse.current`** — referencing `UnityEngine.InputSystem` types would add a hard package
dependency to `UTI.Runtime.asmdef`, breaking "no required dependencies" for any project without
Input System installed. Tradeoff: won't receive input if a project's Active Input Handling is set to
"Input System Package (New)" only — documented in `USAGE.md` rather than solved with a
conditional-compile dependency.

`ResolveScreenPosition`/`ResolveWorldPosition` are the testable pure pieces; `Update()`'s actual
`Input.mousePosition` read is the untestable piece, same category as every other live-input/render
call elsewhere in UTI.

### 8.7 `BeanConfig`

A plain text file (`Key=Value` lines) at `<project root>/UTI/BeanConfig.txt` — **not** a Unity asset
(`ScriptableObject`), so it lives in the same `UTI/` folder as everything else UTI generates rather
than needing to live inside `Assets/`. Read via ordinary `File.Exists`/`File.ReadAllLines`, no
`AssetDatabase` involved. Bootstrapped via **UTI > Create Bean Config** (or **UTI > Setup Project
(Config + Docs)**, which also copies the three end-user docs into `<project root>/UTI/` — see §4),
both `[MenuItem]`s wrapped in `#if UNITY_EDITOR`; never overwrites an existing file.

**Fields:** `DefaultCaptureMode`/`DefaultCaptureInterval` (→ `BeanTracker`), `DefaultOutputTargets`
(→ `BeanLogger.OutputTargets`), `DefaultDimensionMode` (→ `BeanSnapshotExporter`),
`DefaultMinFramingRadius` (→ `BeanSnapshotExporter.MinFramingRadius`).

**Applies at component-add time, not live at runtime.** `BeanTracker.Reset()`/
`BeanLogger.Reset()`/`BeanSnapshotExporter.Reset()` (Unity's "component just added in the Editor"
hook) call `BeanConfig.Load()` and apply its values to that component's own serialized fields.
**Deliberately not applied live at runtime** — that would mean the Inspector no longer tells you the
truth about what a specific Bean actually does, turning "why is this Bean acting like Force2D, I
never set that" into a real debugging trap. The tradeoff: a small amount of magic (new Beans
auto-populate) for a much stronger guarantee (existing Beans' Inspector values are always exactly
what's used, no hidden overrides).

**Parsing:** `BeanConfig.ParseLines(IEnumerable<string> lines)` is the pure, testable piece —
comments (`#`), blank lines, unrecognized keys, and malformed values are all silently ignored rather
than erroring, so one typo doesn't break loading the rest of the file.

**Testability:** `ApplyConfigDefaults(BeanConfig config)` on `BeanTracker`/`BeanLogger`/
`BeanSnapshotExporter` is `public`, separated from `Reset()` so it's testable directly (`new
BeanConfig { ... }`, no filesystem/Editor event needed). `BeanConfig.Load()`'s actual file read is
untested (same category as everywhere else UTI touches real I/O); `ParseLines` covers the logic.

Why this ended up as a plain text file instead of a `ScriptableObject`, and the full field-addition
history: `DESIGN_HISTORY.md`.

## 9. Build Order (milestones)

Built in this order: `BeanSample`/`BeanBuffer` (data struct + circular buffer) → `BeanTracker`
(capture loop) → `IBeanOutput`/`BeanLogger` (console output first, then CSV) → `BeanVisualizer`
(gizmo path draw) → `package.json` + `Runtime`/`Tests` asmdefs (done early, out of order, so
`BeanBufferTests` could actually run) → `BeanSnapshotExporter` (persisted scene+path artifact) →
`BeanArtifactPaths`/`BeanMouseTracker`/`BeanConfig` (shared output-location helper, mouse-input
proxy, project-wide defaults). Sample scenes were descoped entirely rather than built — see §12's
Bring-Your-Own-Test Protocol.

**Current verification status for every piece:** `TESTS/TestTracker.md`. This section is about
build order/architecture, not pass/fail status — the tracker doc is the single source of truth for
what's actually confirmed working.

## 10. Definition of Done (v1)

A user can add `BeanTracker` + `BeanLogger` + `BeanVisualizer` to any GameObject in a fresh scene,
hit Play, and — with zero required configuration — see:
- Console and/or CSV output from `BeanLogger` reflecting the object's actual movement.
- A path line in the Scene view from `BeanVisualizer` matching that movement.

That's the bar for "v1 works." Anything beyond that (custom extras, decimation tuning, color modes,
replay) is valuable but not blocking.

## 11. How UTI Gets Tested

UTI's source of truth lives in this folder and stays independent — not embedded in or merged with
any single game project. Existing Unity projects add it as a **local package dependency** via a
`"file:"` path in their own `Packages/manifest.json`, pointing back at this folder. Current test
beds: `little wings` (3D flight/combat), `project 2`/BoxJump (3D platformer, already has
EditMode/PlayMode test projects set up), `2d project 3` (2D). All three reference the same live
source, so a change here is immediately visible in each — testing across all three exercises UTI's
"general-purpose across genres" goal directly.

**Gotcha:** a `"file:"` dependency alone isn't enough for the package's tests to show up in Test
Runner — the package id also needs to be listed under a `testables` array in `manifest.json`, or
EditMode/PlayMode tests silently show 0 found.

## 12. Verification Strategy

**Whatever tooling access a given session has (none, read-only, or real execution — varies, check
fresh each time, see `CLAUDE.md`), the loop is the same:** code gets written for a milestone →
precise Editor steps are handed over, or run directly if the session has execution access → a real
report comes back (this session's own testing, or relayed from whoever ran it in a test project) →
iterate based on that report. `TESTS/TestTracker.md` status values reflect this honestly —
"Planned" until actually run, "Pass"/"Fail" only after a real report, never assumed from reading the
code.

**CI** (`.github/workflows/tests.yml`) runs the EditMode suite (`TESTS/EditMode/`) headlessly on
every push to `main`, against the minimal project shell at `CI~/` (§4) — automating the "code
written, EditMode tests run" half of the loop above. It does **not** replace live verification of
anything Play-Mode-dependent (`BeanVisualizer`'s actual gizmo draw, `BeanSnapshotExporter`'s actual
capture, `BeanMouseTracker`'s actual input read), which still needs a real session against a
consuming project. Push-only trigger, deliberately — only someone with write access can push to
`main`, so an outside contributor's PR can never run this workflow with access to its secrets.
License activation uses a live login each run (`UNITY_EMAIL`/`UNITY_PASSWORD` via
`buildalon/activate-unity-license`), not a portable license file — Unity's newer licensing client
binds exported license files to the machine that generated them, so a file from a dev machine
would never validate on a GitHub runner. Unity installs with `modules: None` (no IL2CPP — this job
never builds a Player) via a fresh, uncached install each run — `cache-installation` is
deliberately **off**: two separate attempts to re-enable it both produced a cache-restored install
that failed to run Unity at all, in two different ways, from what should've been the same cached
bytes, pointing at non-deterministic cache-restore corruption rather than anything about
`modules: None` itself. **The original goal of this round — skip the ~13-minute install on repeat
runs — is not met.** A fresh install (with or without the module) still takes roughly that long on
a GitHub-hosted runner; the genuinely fast (~4 min) installs only happened on the two cache hits
that then failed to actually run. `modules: None` stays anyway (smaller download/install
regardless of timing, and never needed for this job), but CI is back to reliable, not fast — the
real fix for install time would need the cache-restore corruption itself understood and fixed, not
attempted yet. The test-run step also launches Unity directly with its own process-polling
diagnostics rather than through `buildalon/unity-action`, kept deliberately (not just a leftover)
since it already proved its worth catching a real failure fast. Confirmed green across two
consecutive runs (14m33s and similar). Full setup/debugging history (ten live runs across two
separate incidents, the cache-restore-corruption finding) is in `DESIGN_HISTORY.md` — re-enabling
`cache-installation` isn't recommended without first understanding why the restore was
non-deterministically broken.

### The Bring-Your-Own-Test Protocol

**The standard way to verify UTI in any consuming project: find a test that already exists and
already passes, add the relevant Beans to whatever GameObject it exercises, run that test
completely unmodified, and report what UTI produced.** This also means never building a new
demo/sample Unity project or scene purely to test or showcase a UTI feature — we are testers using
other people's real projects, not developers building our own. This isn't just a convenience — it's
the actual test of UTI's core promise (drop-in, zero required changes, works with whatever's already
driving a GameObject). If verifying UTI ever requires writing new test/harness logic, that's a
process mistake to correct, not a normal step.

Concretely:

1. **Confirm install** — package resolves, `UTI > Setup Project (Config + Docs)` run, zero console
   errors.
2. **Confirm the consuming project already has a genuinely working test or scenario** — not one
   built for UTI's benefit.
3. **Add the relevant Bean components** to the GameObject(s) that test already exercises. Nothing
   about the test itself changes.
4. **Run that existing test completely unmodified**, however it's normally run — a human pressing
   Play, the real Test Runner, CI. Do not rewrite its movement/input logic into a new harness "to
   make it drivable" — if the test can't be run through whatever tooling is doing the verifying,
   that's a tooling-access limitation to route around (hand it to a human, or to whichever agent
   *can* actually drive Play Mode), not a reason to reimplement the test.
5. **Report back what UTI actually produced** — the CSV, the console log, the PNG, the live
   Scene-view line.

**Where this came from:** a real round hit exactly the failure mode step 4 warns against — an
agent's tooling couldn't drive a project's real PlayMode test through Test Runner (Play Mode entry
is blocked as unsupported "user interaction" in that kind of MCP connection), so it started
rewriting the test's movement logic into a temporary harness script instead, which immediately
introduced its own new bug. The fix wasn't a UTI change at all: a human (or whichever agent can
actually enter Play Mode) just needs to press Play with the real, already-working test running and
the Beans already attached. Full incident writeup: `TESTS/TestTracker_HISTORY.md`.

## 13. Known Limitations & Risks (v1)

Real gaps found by reading the actual implementation, not speculative — the difference between
"works in a clean one-Bean demo" and "holds up in the genres this is meant to serve":

- **`extras` is numeric-only** (`Dictionary<string, float>`). Fine for velocity, health, ammo count
  — awkward for AI state that's naturally categorical (patrol/chase/attack), which needs the caller
  to pre-encode it as a float rather than UTI supporting it directly.
- **Multi-Bean gizmo-draw cost at realistic counts is unverified.** Independent-buffers is
  EditMode-tested (several `BeanTracker`s driven at once, no shared state) — whether
  `BeanVisualizer`'s gizmo draw stays cheap with many active Beans at once (a wave of enemies, a
  screen full of bullets) is still a live Play Mode check, not EditMode-testable.
- **2D/3D detection is a Z-extent threshold, not a real scene-orientation check** (§8.4) — a 2D game
  built on a non-standard plane would fool it. `dimensionMode` lets a dev override the guess.
- **`BeanSnapshotExporter`/`BeanVisualizer` read the live sample buffer, not the CSV** — a long idle
  tail after real movement finished can push the interesting path out of the fixed-capacity buffer
  before a snapshot happens (§8.4's `IsBufferAtCapacity` warning covers the exporter side; the
  visualizer's live trail just reflects whatever's currently buffered, which is expected for a live
  view). Best practice: call `StopTracking()` promptly, or raise `Max Samples` for a long session —
  see `USAGE.md`'s "Known constraints" section.

None of these block v1's Definition of Done (§10) — see `PROJECT_OVERVIEW.md`'s Roadmap for
corresponding fix/feature ideas, and `TESTS/TestTracker.md` for the rows tracking them. Full history
of every already-fixed limitation (filename collisions, object pooling, `EveryFixedUpdate`
coverage, `MinFramingRadius`, the buffer-eviction bug): `DESIGN_HISTORY.md`.

## 14. Error Handling & Fault Isolation

**Tracked at-a-glance in `TESTS/ErrorHandlingTracker.md`** (EH01–EH10) — one row per guarded
boundary with an honest verification status.

**Philosophy: guard system boundaries, not internal invariants.** UTI is a debug/QA tool attached
alongside real gameplay — it should never be the reason a playtest session or a game crashes. Every
place UTI crosses a genuine boundary (caller-supplied code — `CustomCapture`; disk I/O — any
`IBeanOutput`, `BeanSnapshotExporter`'s per-angle write; an externally-edited config file —
`BeanConfig.txt`) is wrapped so a failure there logs a warning and degrades gracefully instead of
propagating an unhandled exception into whatever Unity callback triggered it. Internal invariants
(e.g. `BeanBuffer`'s constructor throwing on a bad capacity) are deliberately left untouched — a
genuine programming error should fail loudly and immediately during development, not be swallowed
into a warning that lets a broken Bean silently limp along.

Full boundary-by-boundary writeup (what each guard replaced, and the bugs found while adding them):
`DESIGN_HISTORY.md`.

## Change Log

- 2026-08-15 00:31 — **Correction to the entry below: run #18's "Install Unity" was misreported as
  2m22s** (an intermediate tool-summarization error, not re-verified against the raw API response
  at the time) — it actually took **13m22s**, essentially the same as this project's historical
  default-module install. The two genuinely fast (~4 min) installs only happened on the cache hits
  that then failed to run. **This round's original goal — skip the ~13-min install on repeat
  runs — was not met**; CI is reliable again, not fast. Caught by re-verifying against the raw
  job-step timestamps after a direct question about whether the original goal had actually been
  achieved. `modules: None` still stands on its own (smaller download regardless of timing), just
  not as the "genuine speed win" this doc previously claimed.
- 2026-08-15 00:17 — **CI cache saga, round two: re-enabled, broke twice, disabled again with a
  real root cause this time.** Re-enabling `cache-installation` (with the genuine `modules: None`
  IL2CPP opt-out) produced a cache-restored Unity install that failed to actually run in two
  different ways across two runs (a 55-minute silent hang, then a `Unity.dll failed to load` crash)
  from what should've been identical cached bytes — pointing at non-deterministic cache-restore
  corruption, not the module change. Disabled caching again; see the correction above for the real
  install-time figure. Full investigation: `DESIGN_HISTORY.md` §12.
- 2026-08-11 09:41 — **Restructured for a full docs condense pass**: split the multi-round design
  narrative and the full historical Change Log out into `DESIGN_HISTORY.md`, leaving this file as
  current-state architecture reference only. See `docs/PROJECT_OVERVIEW_HISTORY.md` for the full
  restructure writeup covering all affected docs.
- 2026-08-09 22:33 — T05 (`BeanVisualizer`'s live Scene-view gizmo) confirmed Pass for real, after
  eight attempts across three projects — see `TESTS/TestTracker.md`'s T05 row.
- 2026-08-09 15:34 — `BeanVisualizer.DrawPath()` given an injectable `IGizmoDrawer` seam (§8.3),
  closing a real test-coverage gap — proves the draw-call logic, not live pixel rendering.
- 2026-08-09 13:17 — CI's `cache-installation` reverted after a live 35-minute timeout (§12) — the
  first of the two cache incidents; see the 2026-08-15 entry above for how this eventually resolved.

Full Change Log since day one: `DESIGN_HISTORY.md`.
