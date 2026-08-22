# UTI — Setup & Usage

*This is an end-user doc — meant to be copied into your game project's `<project root>/UTI/`
folder (the same one your CSVs/PNGs land in), not just read from the UTI package repo.*

The practical "how do I actually use this" doc. The [root README](../README.md) is the pitch and
quick install, [PROJECT_OVERVIEW.md](./PROJECT_OVERVIEW.md) is the full pitch/roadmap/history,
[DESIGN.md](./DESIGN.md) is the architecture, [TESTS/TestTracker.md](../TESTS/TestTracker.md) is
verification status. This one is for a developer who just wants to drop UTI into their game and
get useful output today. See [READING_LOGS_AND_VISUALS.md](./READING_LOGS_AND_VISUALS.md) for how
to actually interpret what comes out the other end, and [CONFIG.md](./CONFIG.md) for setting this
game's own preferred defaults in one place instead of configuring every Bean by hand.

## 1. Install it

UTI isn't embedded in your game project — it lives in its own repo and your project references it
as a package. **Always install from the GitHub URL, pinned to a specific release tag** — never a
bare branch URL, and never a local `"file:"` path. The tag pin is what makes "which version am I
on" a real, checkable answer instead of "whatever was on disk/`main` the day someone installed it."

Via Unity Package Manager: Window > Package Manager > `+` > "Add package from git URL", then paste
(swap in the current release tag — **`v0.2.1`** as of this writing):

```
https://github.com/DataFright/Unity-Testing-Inspector.git#v0.2.1
```

Or edit your project's `Packages/manifest.json` directly:

```json
{
  "dependencies": {
    "com.uti.core": "https://github.com/DataFright/Unity-Testing-Inspector.git#v0.2.1",
    ...
  },
  "testables": [
    "com.uti.core"
  ]
}
```

**To update later:** bump the `#v0.2.1` to whatever the new release tag is and let Package Manager
re-resolve.

**Actively developing UTI's own source, not just using it?** That's the one case where a local
`"file:"` reference makes sense (clone the repo, point at that folder — local edits show up
immediately, no commit/push/tag round-trip). If you're just consuming UTI in a game project, stay on
a tag-pinned GitHub URL instead — `file:` always reflects whatever's currently on disk, which means
there's no version to check it against.

The `testables` entry isn't optional if you want UTI's own tests to show up in your Test Runner —
without it, Unity silently reports 0 tests found even though the package compiled fine. Save,
switch to the Editor, let it recompile. "UTI (Unity Testing Inspector)" shows up under
Window > Package Manager > In Project.

**Then, one more one-click step:** in the Editor menu bar, run **UTI > Setup Project (Config +
Docs)**. This bootstraps `<project root>/UTI/BeanConfig.txt` (see [CONFIG.md](./CONFIG.md)) *and*
copies this doc plus `READING_LOGS_AND_VISUALS.md`/`CONFIG.md` into your own project's `UTI/`
folder, so they're sitting right next to the CSVs/PNGs they explain instead of only living in the
separate package repo. Safe to re-run any time — it never overwrites a file you already have.

## 2. The five-second version

Add `BeanTracker` to any GameObject with a Transform you want to watch — a player, a car, an
enemy, a bullet, the mouse cursor, anything. Hit Play. That's it: it's already capturing.

By itself `BeanTracker` just collects data into an in-memory buffer — nothing visible happens
until you add one of the other pieces:

- **`BeanLogger`** — prints/writes what got captured (Console, CSV, and/or JSON Lines).
- **`BeanVisualizer`** — draws the path live in the Scene view while Play Mode is running.
- **`BeanSnapshotExporter`** — saves a PNG of the path (with real scene geometry around it) that
  you can open after the run ends, unlike the live-only gizmo.

All three are independent — add whichever ones you actually want, in any combination, on the same
GameObject as the `BeanTracker`. None of them require each other beyond needing a `BeanTracker` to
read from (auto-found via `GetComponent` if you don't wire one in explicitly).

## 3. `BeanTracker` — what to actually configure

| Field | Default | What it does |
|---|---|---|
| `Capture Mode` | `EveryUpdate` | See the three modes below — this is the "tick-based vs. time-based" choice. |
| `Capture Interval` | `0.5` | Seconds between captures — only used by `EveryNSeconds`. |
| `Capture Rotation` | on | If off, every sample's rotation is `Quaternion.identity`. |
| `Max Samples` | `1000` | Ring buffer size — oldest samples get overwritten once full. **Matters for `BeanSnapshotExporter` too:** it frames/draws from this live buffer, not the CSV — if you leave tracking running well past the part you actually wanted (a long idle tail), the buffer can fill up entirely with stationary samples and silently push the real movement out before you ever capture a snapshot. If that happens you'll see a `[Bean] ... tracker buffer is full ...` warning in the console. Call `StopTracking()` promptly once you have what you need, or raise `Max Samples` for a long session, if you plan to capture a snapshot afterward. |
| `Start Tracking On Enable` | on | If off, call `StartTracking()` yourself (e.g. on a "race start" event) instead of capturing from the moment the object exists. |

**The three capture modes** — pick based on what you actually want to answer:

- **`EveryUpdate`** (tick-based) — one sample per rendered frame. The default; fine for most
  cases, but ties your sample density to framerate (a 30fps run and a 144fps run of the same play
  session produce very different sample counts).
- **`EveryFixedUpdate`** (tick-based) — one sample per physics step. Use this for anything
  Rigidbody-driven (vehicles, ragdolls, physics props) so samples line up with when physics
  actually moved the object, not when a frame happened to render.
- **`EveryNSeconds`** (time-based, via `Capture Interval`) — a fixed *time* cadence, independent
  of framerate. Good for long sessions where you want the CSV to stay a manageable size regardless
  of how fast the game is running.

Want a different project-wide default for `Capture Mode`/`Capture Interval` so you don't have to
set it on every Bean by hand? See [CONFIG.md](./CONFIG.md) — `BeanConfig` lets you set that once
per game.

Every captured `BeanSample` always has **both** `TickIndex` and `Timestamp` filled in, regardless
of which capture mode produced it — the mode only controls *when* a capture fires, not what gets
recorded once it does.

Custom fields beyond position/rotation: assign `CustomCapture` (a
`Func<GameObject, Dictionary<string,float>>`) to feed numeric extras (velocity, health, ammo,
whatever) into `BeanSample.Extras` without subclassing anything.

Tracking a *pooled* object (bullets/enemies reused via `SetActive` instead of
`Destroy`/`Instantiate`)? See `BeanLogger`'s `Append Across Reuse` field below — by default a
reused object's CSV starts fresh each time it's re-enabled.

## 4. `BeanLogger` — where output goes

| Field | Default | What it does |
|---|---|---|
| `Output Targets` | `Console` | `[Flags]` — any combination of Console, CSV, and JSON Lines. |
| `File Path` | *(empty)* | Explicit override; leave blank for the default location below. Only honored when just one file-based format (CSV or JSON) is active — see below. |
| `Append Across Reuse` | off | For pooled objects: if on, `SetActive(false)`→`SetActive(true)` reuse keeps writing to the *same* file instead of starting a fresh one each time. Off by default — most objects should get a clean log per real run. |

Default location (when `File Path` is blank): `<project root>/UTI/BeanLogs/`, filename
`{timestamp}_{objectName}_{uniqueToken}_bean.csv` (or `.jsonl` for JSON) — a fresh, uniquely-named
file every time tracking starts, so running the same Bean five times to compare results gives you
five files, not one overwritten file. See
[READING_LOGS_AND_VISUALS.md](./READING_LOGS_AND_VISUALS.md) for both formats' exact contents,
including why JSON exists alongside CSV (structured `extras`, not just "another format").

**Both CSV and JSON at once:** an explicit `File Path` can only safely back one file, so if both
`Output Targets` are active together, the explicit override is ignored for *both* and they each
fall back to their own default-named file instead of silently overwriting one another.

Want every *new* `BeanLogger` in this project to default to a different `Output Targets`? See
[CONFIG.md](./CONFIG.md)'s `DefaultOutputTargets`.

Own extra output sink? Add it to `CustomOutputs` (a `List<IBeanOutput>`) — anything implementing
`Open(BeanTracker)`/`Write(BeanSample)`/`Close()` slots in alongside Console/CSV/JSON.

## 5. `BeanVisualizer` — the live Scene-view trail

| Field | Default | What it does |
|---|---|---|
| `Path Color` | yellow | Line color in `None` mode. |
| `Color Mode` | `None` | `None`, `BySpeed` (blue=slow → red=fast), `ByTime` (blue=early → red=late). |
| `Draw Points` | off | Adds a small sphere at each drawn sample, not just the connecting lines. |
| `Max Points To Draw` | `200` | Perf cap — beyond this, the path is decimated (evenly stepped through, always keeping the first and last point) rather than drawing every sample. |

Only visible in the **Scene view**, only while the tracked object's data still exists in memory
(i.e. during Play Mode, or right after exiting it before the scene reloads). If you need to review
a path after the fact, that's what `BeanSnapshotExporter` is for.

**Also reads the same bounded buffer `BeanSnapshotExporter` does** (see `Max Samples` in §3) — the
trail you see is only ever what's *currently* in the buffer. For a live view this is usually fine
(seeing the recent path is normal for a live tool), but if a session runs long enough past the
part you actually care about, the trail can quietly become "just the last stretch of
standing still" instead of the interesting movement. Same fix as §3/§9: don't leave tracking
running much longer than you need once you have what you came for.

## 6. `BeanSnapshotExporter` — the reviewable-afterward artifact

| Field | Default | What it does |
|---|---|---|
| `Capture Camera` | *(empty → `Camera.main`)* | Which camera renders the shot. |
| `Path Color` | yellow | Line color drawn into the render. |
| `Line Width` | `0.1` | Floor width in world units — auto-scaled up if `Auto Frame Camera` pulls the camera back far enough that this would be sub-pixel. |
| `Capture Width` / `Capture Height` | `640` / `360` | Deliberately low-res — this is a fast sanity artifact, not a portfolio screenshot. Bump it if a specific case needs more detail. |
| `Auto Frame Camera` | on | Repositions a *copy* of the camera's transform (restored right after) to frame the whole recorded path with margin, instead of using whatever the gameplay camera happens to be pointed at. |
| `Dimension Mode` | `Auto` | `Auto` guesses flat/2D vs. real 3D from the path's own bounds (a path with near-zero depth is treated as 2D). `Force2D`/`Force3D` override that guess — use this if your 2D scene doesn't keep everything at Z=0, or a 3D path happens to come out flat and you don't want it treated as 2D. |
| `Min Framing Radius` | `2` | Floor on how close the auto-frame camera is allowed to sit, in world units. Raise this if a near-stationary path (e.g. an object that barely moved before the run ended) is framing as an unhelpful close-up with no scene context — 2 world units can be too tight for a larger-scale game. Want every *new* `BeanSnapshotExporter` in this project to default to a different value? See [CONFIG.md](./CONFIG.md). |
| `Capture Angles` | `[Auto]` | One entry (the default, `Auto`) behaves exactly like a single capture always has. List more than one (`Above`, `Side`, `Behind`, `Auto` in any combination) to capture several angles of the *same* run in one call — see "Multi-angle capture" below. |
| `Capture On Stop Tracking` | on | Auto-captures the moment the paired `BeanTracker` stops. Call `CaptureSnapshot()` yourself for a mid-run shot instead/also. |
| `File Path` | *(empty)* | Explicit override; leave blank for the default location below. Only honored when exactly one angle is configured — see "Multi-angle capture" below. |

Default PNG location (when `File Path` is blank, single angle): `<project root>/UTI/BeanSnapshots/`,
filename `{timestamp}_{objectName}_{uniqueToken}_bean_snapshot.png` — same uniqueness scheme as
`BeanLogger`'s CSVs, and both land under one shared `UTI/` folder at your project root (sitting
right alongside Unity's own `Library/`/`Logs/`/`Temp/`), not scattered loose subfolders and not
`Application.persistentDataPath` (that resolves to a hidden per-user AppData folder — deliberately
avoided, see `DESIGN.md` §8.5).

**Multi-angle capture:** set `Capture Angles` to more than one entry (e.g. `Above`, `Side`) and one
call to `CaptureSnapshot()` writes one PNG per angle, all sharing the same timestamp/run so they're
obviously related: `{timestamp}.{n}_{objectName}_{angleName}_{uniqueToken}_bean_snapshot.png` (1st
angle is `.1`, 2nd is `.2`, and so on). Useful when a single angle doesn't show enough — e.g. an
overhead shot to confirm the overall route, plus a side shot to see height/depth. `File Path`'s
explicit override is ignored in this case (one fixed path can't safely back more than one output
file); the default `UTI/BeanSnapshots/` location is always used. After a multi-angle capture,
`LastSnapshotPaths` lists every file written (in order); `LastSnapshotPath` still points at the
last one, for backward compatibility with single-angle usage.

After a capture, `LastSnapshotPath`/`LastSnapshotPaths` and `LastLineWidth` are populated with what
actually happened — useful for confirming what got written without having to guess from the image.

## 7. `BeanMouseTracker` — tracking the mouse itself

Not every Bean needs to track a GameObject that's actually moving in your game — sometimes the
thing worth debugging is the raw mouse input driving it (an aim reticle, a point-and-click target,
a drag gesture). Add `BeanMouseTracker` to any GameObject alongside a `BeanTracker`, and that
GameObject's Transform follows the mouse cursor every frame instead of anything else — the
`BeanTracker` (and everything downstream of it: `BeanLogger`, `BeanVisualizer`,
`BeanSnapshotExporter`) doesn't know or care that its source is a mouse rather than a real object;
it captures/logs/visualizes exactly the same way.

| Field | Default | What it does |
|---|---|---|
| `Tracking Space` | `Screen` | `Screen` = raw pixel coordinates (Z=0), works the same in any project. `World` = projects the mouse into 3D world space via `World Camera`, for tracking where a mouse-aimed reticle actually points in the game world. |
| `World Camera` | *(empty → `Camera.main`)* | Only used in `World` mode. |
| `World Distance From Camera` | `10` | Only used in `World` mode — how far in front of the camera to project. |

Uses the legacy Input Manager (`Input.mousePosition`), not the Input System package's
`Mouse.current`, specifically so UTI doesn't pick up a hard dependency on that package. **If your
project's Active Input Handling (Project Settings > Player) is set to "Input System Package
(New)" only, switch it to "Both"** for this component to actually receive mouse input.

## 8. `BeanConfig` — project-wide defaults, set once

Don't want to configure `Capture Mode`/`Output Targets`/`Dimension Mode`/`Min Framing Radius` by
hand on every single Bean you add to this game? `BeanConfig` is a plain text file — `<project
root>/UTI/BeanConfig.txt` — that lets you set your preferred defaults once; every *new*
`BeanTracker`/`BeanLogger`/`BeanSnapshotExporter` you add afterward starts pre-filled to match.

Bootstrap it via the Editor menu — **UTI > Create Bean Config** (or **UTI > Setup Project (Config
+ Docs)**, which also copies these three docs — see §1) — which writes a commented template with
the compiled-in defaults, ready to edit. Full field-by-field explanation is in
[CONFIG.md](./CONFIG.md).

## 9. Common recipes

- **"Did my player actually take the route I expected?"** — `BeanTracker` (`EveryUpdate`) +
  `BeanSnapshotExporter` on the player. Play through the level, check the PNG afterward.
- **"Compare 5 different runs"** — just re-enter Play Mode 5 times with the same setup; every run
  gets its own timestamped CSV/PNG automatically, nothing to configure.
- **"Debug exact mouse aim vs. what the reticle actually did"** — `BeanMouseTracker`
  (`World` mode, pointed at your gameplay camera) + `BeanTracker` + `BeanLogger` on one GameObject,
  a second `BeanTracker` on the actual reticle/aim target, compare the two CSVs.
- **"A Rigidbody-driven object's path looks wrong"** — `BeanTracker` set to `EveryFixedUpdate`, so
  samples land exactly on physics steps rather than potentially-uneven render frames.
- **"This 2D scene's snapshot looks wrong / this 3D scene got treated as 2D"** — set
  `BeanSnapshotExporter.Dimension Mode` to `Force2D`/`Force3D` explicitly rather than relying on
  the Z-depth auto-guess.
- **"The snapshot is a useless close-up on an object that barely moved"** — raise
  `Min Framing Radius` (per-Bean, or project-wide via `BeanConfig`'s `DefaultMinFramingRadius`).
- **"The snapshot is a tight close-up / the path line is invisible, even though the object
  definitely moved a lot"** — check the console for a `[Bean] ... tracker buffer is full ...`
  warning. If tracking was left running well past the interesting part, the real movement may have
  already been evicted from the buffer by the time you captured (see `Max Samples` in §3). Call
  `StopTracking()` promptly next time, or raise `Max Samples` for a long session.
- **"One angle doesn't show enough"** — set `Capture Angles` to more than one entry (e.g. `Auto`,
  `Above`) to get several angles of the same run in one capture.
- **"Bullets/enemies are pooled and their logs keep resetting"** — turn on `BeanLogger.Append
  Across Reuse` so a reused object's log accumulates across `SetActive` cycles instead of
  truncating each time.
- **"`extras` needs to stay structured, not a flat `key=value;key=value` string I have to
  re-parse"** — add `Json` to `BeanLogger.Output Targets` (alongside or instead of `Csv`). Each
  line is a standalone JSON object with `extras` as a real nested object — see
  `READING_LOGS_AND_VISUALS.md`'s JSON Lines section.

## Known constraints, not bugs

- **Wiring Beans into your own test code?** A test assembly (e.g. a `*.PlayMode.asmdef` in your own
  project) needs its own explicit `asmdef` reference to `UTI.Runtime` to compile against UTI's
  types — this is standard Unity assembly behavior, not something UTI does specially, but it's easy
  to miss since the only reference most people configure is the unrelated `testables` entry (which
  is for running *UTI's own* tests, not for using UTI *from* your tests). Add `UTI.Runtime` to your
  test assembly's References list the same way you'd add any other package assembly.
- **Scripting Bean setup instead of adding components in the Editor?** `OnEnable()` — which is what
  actually starts tracking/opens outputs — doesn't fire outside Play Mode unless a script has
  `[ExecuteAlways]`, which UTI's components deliberately don't use. So a script that does
  `gameObject.AddComponent<BeanTracker>()` etc. *before* pressing Play won't see anything actually
  start until Play Mode itself begins — any field values you set beforehand (like `OutputTargets`)
  are still respected once it does, but calling public methods like `Open()`/`StartTracking()`
  yourself in that Edit-Mode window works too, just be aware that state gets reset and `OnEnable()`
  fires fresh the moment Play Mode actually starts (same domain-reload behavior as any other script
  state that isn't serialized). If you're scripting Bean setup as part of something that runs *at
  runtime* (already in Play Mode), none of this applies — `OnEnable()` fires synchronously and
  immediately, same as any other runtime `AddComponent()` call.
- `BeanMouseTracker` needs the legacy Input Manager enabled (see §7).
- `BeanSnapshotExporter`'s output resolution is deliberately low (640×360 default) — it's a fast
  sanity check, not a polished screenshot.
- **Best practice: stop tracking soon after the interesting part is over.** Both
  `BeanVisualizer`'s live trail and `BeanSnapshotExporter`'s captured path read `BeanTracker`'s
  live sample buffer, which only holds `Max Samples` entries (default 1000) before it starts
  overwriting its own oldest data. This is fine for a normal-length run, but if tracking is left
  running well past the part you actually wanted — during manual exploration, debugging, or just
  forgetting to stop it — the real movement can get silently pushed out of the buffer before you
  look at either tool, leaving only whatever's left standing still at the end. The CSV is
  unaffected either way (it streams every sample immediately, unbounded), so this is really only a
  concern for the live trail and the snapshot. Two ways to avoid it: call `StopTracking()` promptly
  once you have what you need, or raise `Max Samples` up front if you know a session will run long.
  `BeanSnapshotExporter` will also warn in the console (`[Bean] ... tracker buffer is full ...`) if
  this may have already happened to your capture — see §6/§9.
- See `DESIGN.md` §13 for the current list of known robustness gaps that don't block normal usage
  but are worth knowing about.

## Change Log

- 2026-08-22 — Fixed BUG-08 and BUG-10 (`TESTS/BugTracker.md`): added "Known constraints" callouts
  for the test-assembly `asmdef` reference gap and Edit-Mode-vs-Play-Mode component-activation
  timing, both previously undocumented gaps that had actually tripped up consuming teams.
- 2026-08-08 — Documented the new JSON Lines output (`BeanLogger.Output Targets` gained `Json`
  alongside `Console`/`Csv`) and `BeanConfig`'s new `DefaultOutputTargets` key (§4, §8, §9).
- 2026-08-08 — Documented a real bug and its fix: `BeanSnapshotExporter` frames/draws from the live
  sample buffer, not the CSV, so a long idle tail after real movement can silently evict the whole
  path before a snapshot happens. Now warns in the console when this could be happening; added
  guidance to §3/§5/§9's "Known constraints" section (`BeanVisualizer`'s live trail shares the same
  underlying buffer, so a long session can shrink it the same way).
- 2026-08-08 — Documented `Min Framing Radius`, multi-angle `Capture Angles`, `BeanLogger.Append
  Across Reuse`, and the new `UTI > Setup Project (Config + Docs)` menu item. Also fixed §8, which
  had gone stale describing the old `Assets/UTI/BeanConfig.asset` `ScriptableObject` approach after
  `BeanConfig` was rebuilt as a plain text file — it now matches `CONFIG.md`.
- 2026-08-07 — Removed `EveryNTicks` (added and reverted same day — wasn't what was actually
  being asked for). Added `BeanConfig` (§8): project-wide default settings, set once per game
  instead of per-Bean. See `CONFIG.md` for the full explanation.
- 2026-08-07 — Initial USAGE.md: install steps, per-component field reference, common recipes.
