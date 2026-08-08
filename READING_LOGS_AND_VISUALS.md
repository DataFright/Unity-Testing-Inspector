# UTI — Reading the Logs & Visuals

*This is an end-user doc — meant to be copied into your game project's `<project root>/UTI/`
folder (the same one your CSVs/PNGs land in), not just read from the UTI package repo.*

Once UTI is actually running (see [USAGE.md](./USAGE.md) for setup), this is the doc for "okay,
I'm looking at a console line / a CSV / a PNG / a Scene-view trail — what am I actually looking
at, and what does it tell me." Written for a developer debugging their game, but the formats below
are plain text on purpose, so an AI assistant reading the file directly can use them the same way.

## Where everything lives

All of UTI's generated output lands under one folder at your project's root — `<project root>/UTI/`
— sitting alongside Unity's own `Library/`, `Logs/`, `Temp/`. Two subfolders:

- **`UTI/BeanLogs/`** — CSVs from `BeanLogger`.
- **`UTI/BeanSnapshots/`** — PNGs from `BeanSnapshotExporter`.

Every file is named `{timestamp}_{objectName}_{uniqueToken}_...`, timestamp first — so sorting
the folder by name (or by date modified) gives you every run in chronological order, across every
Bean you've ever run in that project. Nothing gets overwritten; if you don't need old runs,
they're safe to delete manually (UTI doesn't clean up after itself).

**Multi-angle snapshots** (see `USAGE.md` §6) name slightly differently: `{timestamp}.{n}_
{objectName}_{angleName}_{uniqueToken}_bean_snapshot.png` — the `.{n}` and the angle name (`Above`,
`Side`, `Behind`, `Auto`) tell you which angle each file is, and every file from the same capture
call shares the same `{timestamp}`, so they still sort/group together.

## Console output

One line per captured sample, from `ConsoleBeanOutput`:

```
[Bean] tick=42 t=3.14 pos=(12.5, 0.0, 8.2) rot=(0.0, 45.0, 0.0)
```

- **`tick`** — the sample's index within *this Bean's* buffer, starting at 0. Not a global frame
  counter — two Beans started at different times will have different tick 0 moments.
- **`t`** — `Time.time` (seconds since the scene started) at the moment of capture. Use this to
  correlate a Bean's samples against wall-clock/gameplay events, or against another Bean's samples
  from the same session.
- **`pos`** — world-space position.
- **`rot`** — rotation as Euler angles (degrees), not the raw quaternion — chosen for console
  output specifically because Euler is what a human skimming the log can actually parse at a
  glance ("facing roughly north-east") without doing quaternion math in their head.

Meant for low-frequency/spot-checking use. At a high capture rate this floods the console fast —
that's expected, not a bug; switch to CSV output for anything you need to actually analyze rather
than eyeball live.

## CSV output

Header row, then one row per sample:

```
tick,timestamp,x,y,z,qx,qy,qz,qw,extras
0,0.02,12.5,0.0,8.2,0.0,0.383,0.0,0.924,
1,0.05,12.6,0.0,8.3,0.0,0.383,0.0,0.924,velocity=15.2;health=80.0
```

- **`tick`/`timestamp`** — same meaning as console's `tick`/`t`.
- **`x,y,z`** — world-space position.
- **`qx,qy,qz,qw`** — rotation as a raw quaternion, not Euler. CSV is the format meant for actual
  analysis (spreadsheets, scripts, an AI reading the file directly), where the raw component
  values matter more than at-a-glance human readability — Euler angles have gimbal-lock and
  wraparound issues that make them awkward to do real math on, which is exactly why console
  output and CSV output deliberately use different rotation representations for their different
  audiences.
- **`extras`** — empty unless a `CustomCapture` delegate is assigned on the `BeanTracker`. When
  present, packed as `key=value;key=value` in one trailing column (not one column per key) — the
  set of keys can vary sample to sample, so a fixed column layout wouldn't work.

**Reading it for debugging, concretely:**
- A sudden large jump in `x/y/z` between consecutive rows (rather than a smooth progression) means
  the object teleported, was reset, or a physics resolve punched it somewhere — not a smooth
  movement bug.
- `tick` incrementing by more than 1 between rows you'd expect to be adjacent usually means
  `EveryNSeconds` skipped ticks by design (see `USAGE.md` §3) — check `BeanTracker.CaptureMode`
  before assuming something was missed.
- If you're comparing two Beans (e.g. a mouse-aim target and the actual weapon reticle, per
  `USAGE.md`'s recipe), correlate rows by `timestamp`, not `tick` — two Beans' tick counters are
  independent and won't line up even if they started at the same moment.
- A position that stops changing entirely for many consecutive rows, at a value that lines up with
  known level geometry (e.g. `z` frozen exactly at a wall's face minus your character's collision
  radius), usually means the object is correctly, physically stuck against something — not a
  tracking bug. Worth computing the exact expected stop distance by hand before assuming the game
  logic is broken; this pattern has already found a real off-by-a-fraction trigger-distance bug in
  practice (see `TESTS/TestTracker.md`'s `project 2` closing report).

## `BeanVisualizer` — the live Scene-view trail

A line drawn through the tracked object's recorded positions, visible in the **Scene view only**,
only while the data still exists in memory (during Play Mode, or briefly after exiting it before
the scene reloads). Not present in a build, not present after the Editor session ends — for a
persisted, after-the-fact artifact, see `BeanSnapshotExporter` below instead.

- **`None` color mode** — the whole path is one flat color (`Path Color`). Good for "does the
  shape look right," not for "when/how fast was it moving."
- **`BySpeed`** — blue (slow) → red (fast), normalized against the *fastest and slowest segment in
  the whole buffer* — so "red" means "the fastest this particular run got," not any absolute
  speed. A mostly-blue path with one short red burst is a legible signal (a sudden speed spike);
  a path that's uniformly one color just means the object moved at roughly constant speed
  throughout.
- **`ByTime`** — blue (start) → red (end) — good for seeing *direction of travel* on a path that
  crosses itself or backtracks, which a flat-color line can't show.
- If the path looks like it's missing points or has visibly straight long segments where you'd
  expect curves, check whether the sample count exceeds `Max Points To Draw` (default 200) — past
  that, the line is decimated (evenly stepped through) for perf, not drawn from every sample.
- **If the trail has quietly shrunk to just a small stationary cluster over a long session**, that's
  not decimation — it means the real movement has aged out of `BeanTracker`'s own buffer (`Max
  Samples`, default 1000 entries). The live trail only ever reflects what's *currently* buffered;
  see "Best practice" in `USAGE.md`'s "Known constraints" section.

## `BeanSnapshotExporter` — reading a PNG

A real Camera render (not a gizmo) with the recorded path drawn in as a colored line, so real
scene geometry (floor, walls, props) is visible around the path — that's the whole point versus
the live-only gizmo trail.

- **The line's color** is `Path Color` (yellow by default). If it renders pink/magenta instead,
  that's a render-pipeline shader mismatch (the line material is built off `Sprites/Default`,
  which can behave oddly under URP/HDRP in some project configurations) — not a logic bug, but
  worth fixing the material setup if it happens.
- **Framing**: with `Auto Frame Camera` on (the default), the shot is composed to fit the whole
  path with margin — an orthographic front-on view for a flat/2D path, or a perspective view from
  a broadside angle (perpendicular to the path's own direction of travel) for a real 3D one. If a
  shot looks emptier/farther-away than you expected, that's the auto-framing pulling back to fit a
  longer path than a manually-placed camera would have shown — not a bug, just a wider shot.
- **A tight, unhelpful close-up on a path that barely moved** (e.g. the object walked up to
  something and stopped for most of the run) means the recorded bounds are near a single point —
  raise `Min Framing Radius` (per-Bean, or project-wide via `BeanConfig`) so a near-stationary path
  still frames with real margin instead of zooming in on almost nothing.
- **A tight close-up or invisible path line despite real, substantial movement** is a different
  problem from the one above: check the console for a `[Bean] ... tracker buffer is full ...`
  warning. `BeanSnapshotExporter` frames/draws from the *live* sample buffer, not the CSV — if
  tracking ran long past the interesting part (a long idle tail at the end), the real movement can
  get silently evicted from that fixed-capacity buffer before the snapshot ever happens, leaving
  only near-identical stationary samples behind. The CSV still has the full history either way; the
  fix is calling `StopTracking()` promptly, or raising `Max Samples` for a long session.
- **A visible line but no obvious context** (blank-looking background) can mean the path is framed
  correctly but there's genuinely little geometry near it (e.g. an open sky level) — check the
  actual scene, not just the PNG, before assuming something's wrong with the capture.
- **Multiple files from one run** (`{timestamp}.1_...`, `.2_...`, ...) mean `Capture Angles` was
  set to more than one angle — each numbered file is a different named angle (`Above`, `Side`,
  `Behind`, `Auto`) of the *same* recorded path, not a different run. Compare them side by side if
  one angle alone doesn't show what you need.
- `LastLineWidth` (readable on the component after a capture) tells you the actual world-unit
  width used for that specific shot — useful for confirming "is the line thin because of a real
  scaling issue" versus "is it just genuinely there and I'm not seeing it." `LastSnapshotPaths`
  lists every file a multi-angle capture wrote, in order.

## Change Log

- 2026-08-08 — Documented a second, distinct close-up/invisible-line symptom (T28,
  `TESTS/TestTracker.md`): real movement followed by a long idle tail can silently evict the whole
  path from the live sample buffer before a snapshot happens — different mechanism from the
  near-stationary-path case below, now both covered. Also noted `BeanVisualizer`'s live trail
  shares the same underlying buffer, so a long session can shrink it the same way.
- 2026-08-08 — Documented multi-angle snapshot naming/reading, the near-stationary-path close-up
  symptom and its fix (`Min Framing Radius`), and added a CSV-reading note about "frozen position
  at a value matching known geometry usually means physically stuck, not a tracking bug" — a
  pattern that already found a real bug in practice.
- 2026-08-07 — Removed a stale reference to `EveryNTicks` (added and reverted same day elsewhere).
- 2026-08-07 — Initial READING_LOGS_AND_VISUALS.md: console/CSV column reference, BeanVisualizer
  color-mode guide, BeanSnapshotExporter interpretation notes.
