# UTI — Project Config

*This is an end-user doc — meant to be copied into your game project's `<project root>/UTI/`
folder (the same one your CSVs/PNGs land in), not just read from the UTI package repo.*

This doc explains `BeanConfig` — a single plain-text file that lets you set this *specific game's*
preferred UTI defaults in one place, instead of configuring every Bean you add by hand.

## Set it up (once per game)

Easiest way: in the Unity Editor menu bar, **UTI > Create Bean Config** (or **UTI > Setup Project
(Config + Docs)**, which does this *and* copies `USAGE.md`/`READING_LOGS_AND_VISUALS.md`/
`CONFIG.md` into this same folder — the standard first step for any new project, see `USAGE.md`
§1). This writes a commented template to `<project root>/UTI/BeanConfig.txt` — the same `UTI/`
folder your logs and snapshots already land in — with the compiled-in defaults spelled out, ready
to edit. (Won't overwrite one that already exists.)

Or just create the file yourself: `<project root>/UTI/BeanConfig.txt`, plain text, one
`Key=Value` per line.

From then on, any *new* `BeanTracker`, `BeanLogger`, or `BeanSnapshotExporter` you add to a
GameObject in this project starts pre-filled with whatever's in that file.

## What each key actually does

| Key | Affects | Meaning |
|---|---|---|
| `DefaultCaptureMode` | new `BeanTracker`s | Which of the existing capture modes (`EveryUpdate`, `EveryFixedUpdate`, `EveryNSeconds`) a fresh `BeanTracker` starts set to. |
| `DefaultCaptureInterval` | new `BeanTracker`s | The `Capture Interval` (seconds) a fresh `BeanTracker` starts with — only matters if `DefaultCaptureMode` is `EveryNSeconds`. |
| `DefaultOutputTargets` | new `BeanLogger`s | Which output(s) (`Console`, `Csv`, `Json`) a fresh `BeanLogger` starts with — `[Flags]`, so a comma-separated combination like `Console,Json` is valid too. |
| `DefaultDimensionMode` | new `BeanSnapshotExporter`s | Whether a fresh `BeanSnapshotExporter` starts on `Auto` (guess flat/2D vs. real 3D from the recorded path), or forced to `Force2D`/`Force3D`. Set this to whichever your game actually is if `Auto` ever guesses wrong for your scenes — see `READING_LOGS_AND_VISUALS.md` for what "guessing wrong" looks like. |
| `DefaultMinFramingRadius` | new `BeanSnapshotExporter`s | The floor (world units) on how close the auto-frame camera is allowed to sit, for a fresh `BeanSnapshotExporter`. The compiled-in default (`2`) can be too tight for a larger-scale game, producing an unhelpful close-up on a near-stationary path — raise this to whatever margin makes sense at your game's actual scale, once, instead of tuning `Min Framing Radius` on every Bean by hand. |

Example file:

```
# UTI project config - see CONFIG.md for what each setting does.
DefaultCaptureMode=EveryFixedUpdate
DefaultCaptureInterval=0.5
DefaultOutputTargets=Console,Json
DefaultDimensionMode=Force3D
DefaultMinFramingRadius=5
```

Lines starting with `#` are comments. Unrecognized keys, malformed lines, and bad enum/number
values are silently ignored (not an error) — a stray typo just means that one setting falls back
to its compiled-in default rather than breaking the whole file.

## Important: this only affects *new* Beans, not live behavior

`BeanConfig.txt` is read once, at the moment you add `BeanTracker`/`BeanSnapshotExporter` to a
GameObject (or hit "Reset" on the component's context menu) — not read continuously while the
game runs, and not something that silently changes an *existing*, already-configured Bean out
from under you. Whatever you see in a Bean's Inspector is always exactly what that Bean actually
does; `BeanConfig.txt` only ever pre-fills the values a brand-new one starts with. If you edit the
file later, existing Beans in your scenes keep whatever they already had — only Beans you add
*afterward* pick up the new values. Per-Bean fields are always still fully overridable one-off,
same as before `BeanConfig` existed at all.

## Why plain text, not a Unity asset (ScriptableObject)?

Considered first, reverted: a `ScriptableObject`-based config asset would need to live inside
`Assets/` to actually work (that's a hard Unity constraint, not a choice), which would put it in a
*different* folder than everything else UTI generates. A plain text file has no such constraint —
it lives in the exact same `<project root>/UTI/` folder as your logs, snapshots, and the other
UTI docs, so there's one place to look for everything UTI-related in this project, not two. The
tradeoff is editing it by hand instead of clicking dropdowns in an Inspector, which is a fine
trade for how few settings this actually has.

## Change Log

- 2026-08-08 — Added `DefaultOutputTargets` (→ `BeanLogger`) alongside the new JSON Lines output
  format — the first `BeanConfig` key to affect `BeanLogger` rather than `BeanTracker`/
  `BeanSnapshotExporter`. `[Flags]`, so `Console,Json` (comma-separated) picks more than one output
  at once. See `READING_LOGS_AND_VISUALS.md` for the JSON Lines format itself.
- 2026-08-08 — Added `DefaultMinFramingRadius` (T23 fix, see `TESTS/TestTracker.md`) and mentioned
  the new **UTI > Setup Project (Config + Docs)** menu item, which bootstraps this file and copies
  all three end-user docs in one step — the standard first-time-setup path going forward.
- 2026-08-07 — Switched from a `ScriptableObject` asset (`Assets/UTI/BeanConfig.asset`) to a
  plain text file (`<project root>/UTI/BeanConfig.txt`), per feedback that config should live in
  the same place as everything else UTI generates for this project, not split into `Assets/`.
- 2026-08-07 — Initial CONFIG.md: setup steps, field reference, and the "new Beans only, not live
  behavior" clarification.
