# UTI — Unity Testing Isolator

*(working title — funny on purpose, renameable at launch)*

**Drop a Bean on any GameObject and watch where it actually goes.** UTI is a lightweight, drop-in
Unity toolkit for tracking, logging, and visualizing a GameObject's movement over time — a player,
a car, an NPC, an AI ally, a projectile, a plane, a physics prop. No test framework to learn, no
assertions to write. Just attach it, hit Play, and look at the trail.

Free and open source (MIT — see [LICENSE](./LICENSE)). Built to eventually land on the Unity Asset
Store; usable as a local/git package today.

## The problem

Unity Test Framework is great for code-level assertions — did this function return the right
value. It's clunky for verifying *behavior over time*: "did this thing end up roughly where it
should have, following roughly the path it should have, in roughly the time it should have." That
usually gets checked by eyeballing the Game view once and hoping, because building custom
logging/visualization for it is annoying enough that most people skip it.

UTI closes that gap by making behavior visible: attach a Bean, hit Play, see the trail.

## What's in the box

Four small, independent pieces — use whichever ones you actually need:

- **`BeanTracker`** — attach to any GameObject. Captures position, rotation, and (optionally)
  custom fields on an interval you choose (every frame / every fixed update / every N seconds).
- **`BeanLogger`** — writes what got captured to the Console and/or a CSV file.
- **`BeanVisualizer`** — draws the captured path live as a line in the Scene view.
- **`BeanSnapshotExporter`** — renders the recorded path through a real Camera into a saved PNG
  (with the real scene geometry around it), so there's a durable artifact to review *after* the
  run ends — including multiple angles in one call if one view doesn't tell the whole story.

Plus `BeanMouseTracker` (track raw cursor movement through the same pipeline) and `BeanConfig`
(set your project's own preferred defaults once, instead of configuring every Bean by hand).

Nothing here asserts, fails a build, or fixes anything for you — UTI's job is to make behavior
visible so you can judge it yourself, faster than reading logs or guessing from a red X.

## Install

**Recommended — directly from GitHub**, via Unity Package Manager: Window > Package Manager >
`+` > "Add package from git URL", then paste:

```
https://github.com/DataFright/Unity-Testing-Inspector.git
```

Or edit your project's `Packages/manifest.json` by hand:

```json
{
  "dependencies": {
    "com.uti.core": "https://github.com/DataFright/Unity-Testing-Inspector.git",
    ...
  },
  "testables": [
    "com.uti.core"
  ]
}
```

The `testables` entry isn't optional if you want UTI's own tests to show up in your Test Runner —
without it, Unity silently reports 0 tests even though the package compiled fine.

Once it resolves, run **UTI > Setup Project (Config + Docs)** from the Editor menu bar — one click
bootstraps a `BeanConfig.txt` and copies the full end-user docs into your own project's `UTI/`
folder, right next to the CSVs/PNGs it'll generate.

**Contributing, or want a local/pinned copy instead of tracking a branch?** Clone this repo
somewhere on disk and reference that folder with a `"file:"` URL instead — local edits then show up
in your test project immediately, no commit/push round-trip needed:

```json
"com.uti.core": "file:C:/wherever/you/cloned/Unity-Testing-Inspector"
```

## Five-second usage

Add `BeanTracker` to any GameObject with a Transform you want to watch. Hit Play — it's already
capturing. Add `BeanLogger`, `BeanVisualizer`, and/or `BeanSnapshotExporter` alongside it for
console/CSV output, a live Scene-view trail, and a persisted PNG, respectively. Any combination,
none required beyond the tracker itself.

## Docs

- **[docs/USAGE.md](./docs/USAGE.md)** — full setup and field-by-field reference for every component.
- **[docs/READING_LOGS_AND_VISUALS.md](./docs/READING_LOGS_AND_VISUALS.md)** — how to actually
  interpret what UTI produces (console lines, CSV columns, the live trail, a snapshot PNG).
- **[docs/CONFIG.md](./docs/CONFIG.md)** — setting your project's own preferred defaults in one place.
- **[docs/DESIGN.md](./docs/DESIGN.md)** — architecture: how the pieces fit together, why they're
  built the way they are.
- **[docs/PROJECT_OVERVIEW.md](./docs/PROJECT_OVERVIEW.md)** — the full pitch, the complete
  Roadmap, and the entire project Change Log since day one.
- **[TESTS/TestTracker.md](./TESTS/TestTracker.md)** — feature verification status.
- **[TESTS/ErrorHandlingTracker.md](./TESTS/ErrorHandlingTracker.md)** — every guarded failure mode,
  tracked with the same rigor as feature tests.

## Example use cases

Player routes, vehicle/AI-nav paths, projectile trajectories, physics props after a tuning pass,
general "attach it and see what's actually happening" QA — or sitting alongside a failing automated
test to show whether a click/movement genuinely fired before you go debugging the wrong layer.
Anything with a Transform is a valid target for a Bean.

## License

[MIT](./LICENSE) — free to use, modify, and distribute.
