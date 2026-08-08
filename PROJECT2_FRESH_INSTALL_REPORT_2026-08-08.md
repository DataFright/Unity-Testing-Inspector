# UTI fresh-install verification report

**From:** `project 2` team
**Date:** 2026-08-08
**What was tested:** a clean uninstall/reinstall of `com.uti.core`, the `Setup Project` menu item, adding all four Beans from `USAGE.md` alone, and a real Play Mode traversal of the blue → yellow → red box jump path with all four Beans live.

## Part 1 — Formal test results (per the requested checklist)

### Install/reinstall: clean in both directions

- Removed `BeanTracker`/`BeanLogger`/`BeanSnapshotExporter` from `Player`, removed `com.uti.core` from `manifest.json` (dependencies + testables), deleted `<project root>/UTI/` entirely. Recompiled: zero errors, project runs fine without UTI.
- Re-added the dependency + testables entry, let it resolve. Zero errors on reinstall.
- Bonus regression check: the `CS0104` ambiguous-`Object` fix from last session's report was hotfixed directly in the shared package source, so it survived this full removal/reinstall cycle intact — confirms fixing it at the canonical source (not a project-local copy) was the right call.

### `UTI > Setup Project (Config + Docs)`: works exactly as documented

Ran via `EditorApplication.ExecuteMenuItem`. All four expected files landed in `<project root>/UTI/`: `BeanConfig.txt` (freshly generated, matches `TemplateContents` exactly) plus `USAGE.md`/`READING_LOGS_AND_VISUALS.md`/`CONFIG.md` (copied, correct content). One click, no manual steps needed — the doc-copy gap this menu item was built to close appears genuinely closed.

### Adding the four Beans from `USAGE.md` alone: no ambiguity, no gaps

Read `USAGE.md` fresh and added `BeanTracker`, `BeanLogger` (→ `Output Targets = Csv`), `BeanVisualizer`, `BeanSnapshotExporter` (→ `Capture Angles = [Auto, Above, Side]`) to `Player`. The doc's field tables were sufficient on their own — never needed to check source. One incidental, not-rigorously-proven-but-encouraging sign: the newly-added `BeanTracker`/`BeanSnapshotExporter` picked up `EveryUpdate`/`0.5`/`2` (`MinFramingRadius`), matching `BeanConfig.txt`'s defaults — consistent with `BeanConfig` being applied, though since those happen to equal the compiled-in defaults too, this isn't a rigorous confirmation either way.

### Live Play Mode traversal: succeeded, real jump physics confirmed via CSV

Drove the Player through the real zigzag path in actual Play Mode (not the automated NUnit suite). Note on methodology: this took three attempts because of two bugs in *our own* one-off test-driving script (wrong transform reference, then a steering bug that walked the player off an intermediate platform before it could jump) — not anything to do with UTI. Once fixed, the full Small→Medium→Tall climb completed in ~22 real seconds. `BeanTracker`'s own CSV became the tool we used to diagnose our own script's first bug (an obviously-wrong constant `dist=3.00` pointed straight at the mistake).

### CSV output: correct, and independently verified as physically accurate

Landed exactly where documented (`UTI/BeanLogs/`, correct filename scheme). Inspected the content directly:
- Header/columns exactly as documented: `tick,timestamp,x,y,z,qx,qy,qz,qw,extras`.
- 31,744 rows over the session (we left tracking running ~55s longer than needed after the traversal finished — our error, not UTI's, see the "one UX thought" note below).
- The recorded Y trajectory during the actual jump onto the first box rises smoothly (1.13 → 1.22 over ~50ms of samples) exactly matching real jump-arc physics, and peak Y across the run (3.664) is consistent with a real jump overshoot before settling onto the final box at Y=3.08 (matching that box's known 3.0m top height). The CSV is trustworthy ground truth.

### Multi-angle snapshot capture: files land correctly, but two things worth flagging

Set `Capture Angles = [Auto, Above, Side]` before the run, then triggered capture via `StopTracking()` (since `Capture On Stop Tracking` was on). Three PNGs landed exactly as documented: shared timestamp/token, correct `.1`/`.2`/`.3` ordering, correct angle names in each filename.

**Finding 1 — no visible path line in any of the three captures.** `Path Color` was left at its default (yellow). None of the three angle images show a distinguishable colored trail — the character and box geometry are visible, but nothing that reads as "a path line was drawn." This was consistent across all three angles, so it doesn't look like an angle-specific issue. We can't rule out that a very thin line is present but imperceptible at this render resolution/scene lighting; flagging it as "not visibly confirmed working" rather than "confirmed broken."

**Finding 2 — Auto Frame Camera still produced a tight close-up despite a path with real extent (looks like a T23-class recurrence).** All three angle captures center tightly on the *final resting position* (standing on the Tall box), not the full recorded path — despite that path spanning ~9m in Z with real movement and a real jump in the first second. Per UTI's own `HANDOFF.md`, T23 (`MinFramingRadius` now configurable) was believed to fix "the useless close-up on a near-stationary path" bug — but that fix targeted the case where the *whole* path is near-stationary. This run is a different shape: real movement for ~1s, then ~55s stationary at the end (our own script's fault for not calling `StopTracking()` promptly — see below). If the framing algorithm weights by sample density or last-position rather than the full recorded bounding box, a long stationary tail could be dominating the auto-frame calculation the same way a fully-stationary path did before T23. Worth testing specifically: *short real movement + long stationary tail*, as distinct from *entirely stationary*.

### Live Scene-view trail (`BeanVisualizer`): inconclusive, not a UTI finding

We tried to screenshot the Scene view mid-run to confirm the live gizmo trail, but couldn't get a satisfactory camera framing through our available tooling in this session (a tool/environment limitation on our end, not something we're attributing to UTI). We're not able to confirm or deny this part of the checklist — would need an actual human with an interactive Editor window to close this gap, since a "does the live line visibly track" check fundamentally needs a live interactive viewport.

## Part 2 — Direct answers to your other questions

**Did it install correctly?** Yes, cleanly, in both directions, zero errors throughout, including surviving a full uninstall/reinstall cycle without losing the earlier `CS0104` fix.

**New bugs?** The two Findings above (no visible path line in snapshot exports; Auto Frame Camera close-up recurrence in the "real movement + long stationary tail" shape).

**Anything weird or missing?** Nothing missing from the docs. One weird-but-documented-correctly behavior: `BeanTracker` has no built-in idle/auto-stop, so a session left running after "the interesting part" is over just keeps recording identical stationary rows indefinitely — matches the docs exactly (explicit `StartTracking`/`StopTracking` control), but it's an easy trap for exactly the kind of manual/exploratory session we just ran, and may be *part of* why Finding 2 happened (the stationary tail vastly outnumbering the real-movement samples). Not asking for an auto-stop feature necessarily, but a one-line callout in the docs ("call `StopTracking()` promptly once you have what you need — a long idle tail can skew auto-framing") might save someone else this exact detour.

**Was this helpful?** Genuinely yes. In an earlier session, a `BeanTracker` CSV was the thing that actually resolved a debugging deadlock we couldn't get out of by reading logs alone — it gave us an exact number to check against known geometry instead of guessing.

**Did it weigh me down?** Not UTI itself — the friction was almost entirely on our side: driving a live Play Mode session through our specific automation environment (no reflection allowed in our scripting sandbox, no direct references to custom project assemblies, occasional bridge reconnects) meant most of the time in this task went into building a way to *drive* the scenario at all, not into evaluating UTI. Once a real session was actually running, UTI's own pieces (install, setup menu, component config, CSV/PNG output) each worked in one shot or close to it.

**Anything unnecessary in UTI?** Nothing stood out as unnecessary. The four components stayed cleanly independent, matching the "add only what you need" pitch in the docs.

**Anything that could better support this kind of testing?** One idea: a menu-item-style or otherwise non-reflection way to trigger `CaptureSnapshot()`/`StopTracking()` on a specific tracked object from outside Play Mode's normal script lifecycle would have simplified our job — though on reflection, `CaptureSnapshot()` already exists as exactly that public hook, so this is really more a note about our own tooling constraints than a UTI gap.
