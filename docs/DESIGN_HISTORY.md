# DESIGN.md — History

Full narrative behind `DESIGN.md`'s condensed reference — multi-round design revisions, the CI
setup saga, already-fixed limitations and the bugs that found them, and the complete Change Log
since day one. Read this when the short version in `DESIGN.md` isn't enough: understanding *why* a
design landed where it did, debugging something that resembles an old fixed bug, or auditing the
project's actual development trail. Public (real engineering value — several of these fixes, like
the CI/Unity-licensing findings, are useful to anyone else hitting the same third-party bugs).

## §4 history: `CI~/` moved from `.github/ci-project/`

The CI project shell originally lived at `.github/ci-project/`. Moved to `CI~/` after the first live
CI run failed: `buildalon/unity-setup`'s own version-detection glob (`**/ProjectVersion.txt`) skips
dot-prefixed directories by default, so it couldn't find the project living under `.github/` no
matter how correct everything else was. `CI~/` uses the same UPM "don't import this" convention
`Samples~/` already used, but — unlike a dot-prefixed folder — stays visible to a generic
(non-Unity-aware) glob.

## §8.3 history: why `BeanVisualizer` alone wasn't enough, and the `IGizmoDrawer` seam

**Originally logged as a "stretch goal, not v1" aside**, then re-flagged 2026-08-07 per user
feedback as a real gap worth an actual design pass: `BeanVisualizer` has no persisted artifact,
only a live Editor Gizmo draw — undercutting the point of visualizing a run at all if it can't be
reviewed after the fact (a developer can already watch the Game view live; the actual value of
visualization is reviewing *after*).

**Decided 2026-08-07 — the persisted artifact needs real scene context, not just an abstract
path.** Per user feedback: "say someone falls through the floor, well you have to see the floor." A
plotted line in empty space can't show that; a real camera render can, for free, because it's an
actual render of the scene. This is what led to `BeanSnapshotExporter` (§8.4) instead of e.g. a
CSV-replay viewer, which would have needed UTI to somehow also capture/reconstruct scene geometry —
a much bigger, more fragile undertaking than tracking a Transform.

Also worth naming: this design directly resolved T05's verification deadlock too. T05 had been
blocked repeatedly not on UTI's own logic but on *external* screenshot tooling reading the Editor
from outside (stale/cached captures). A component that renders and saves its own PNG from inside
Unity doesn't depend on that tooling at all — verifying became "open the file `BeanSnapshotExporter`
wrote and look at it," not "get some other tool to successfully screenshot Unity."

**Added 2026-08-09 — `DrawPath()` given an injectable `internal IGizmoDrawer`.** Found while
reviewing T05's long investigation history (`TESTS/TestTracker_HISTORY.md`) that `OnDrawGizmos`'s
actual draw-call logic (which segments, in what order, which color, decimation, point-spheres) had
never itself been unit-tested — only its two pure helper functions (`SelectIndicesToDraw`,
`ResolveColor`) were. The five live-capture attempts up to that point were all trying to visually
confirm code whose own correctness had never actually been proven. `IGizmoDrawer` (a real
`Gizmos.*` wrapper for the live path, zero behavior change; a recording fake in tests) closed that
gap — 5 new EditMode tests assert the exact draw-call sequence `DrawPath()` produces, run for real
via a local Unity install matching CI's version and mutation-checked (a deliberately broken
assertion was confirmed to actually fail before trusting a passing one).

## §8.4 history: `BeanSnapshotExporter`'s full iteration story

**Deferred to later, not v1 scope, at the time this was first designed:** multiple camera angles per
capture, periodic/interval snapshots instead of one at stop-tracking, true frame-by-frame replay.
One camera, one image, whole path baked in as a line, captured once at the natural "run just ended"
moment was the v1 cut — matching "drop-in simplicity, sane defaults" rather than building a full
scene-capture pipeline up front. (Multi-angle capture was later built anyway — see below.)

**Superseded 2026-08-07 (three real gaps found the first time a snapshot actually got looked at,
T17 in `little wings`, `PlayerPlane`):**
1. **No dedicated location.** The PNG landed as a loose file directly under
   `Application.persistentDataPath`, same folder as everything else — nothing marked it as "the
   generated artifacts." Fixed with a dedicated `BeanSnapshots/` subfolder (superseded again the
   same session — see §8.5 history, that subfolder itself moved out of `persistentDataPath`
   entirely once it turned out to be undiscoverable in practice).
2. **No way to keep more than one.** The old default filename was fixed per-GameObject-name, so
   capturing the same Bean five times to compare results would silently overwrite the same file
   four times over. Fixed: `ResolveSnapshotPath` prefixes the filename with a millisecond-precision
   UTC timestamp, timestamp first so a folder with many runs sorts chronologically.
3. **The first real capture was nearly useless as a "quickly understand this" artifact** — a tight
   follow-camera produced a close-up of mostly ground with the path as a tiny distant blob. Fixed:
   `autoFrameCamera` (default on) computes a bounding box of the recorded path and
   repositions/reorients a *copy* of the capture camera's transform (restored afterward) to frame
   the whole path with margin — orthographic front-on for a flat/2D path, elevated perspective for a
   real 3D one.

**Revised again 2026-08-07, same day — two more fixes from the very next live verification round
(T17/T18):**
- **The 3D branch's offset direction was a fixed diagonal** (`IsoDirection = (1, 0.75, 1)`), "a
  generic isometric-ish look" — that assumption turned out wrong in practice. A test path that
  happened to travel roughly parallel to that fixed direction rendered foreshortened into a thick
  vertical stripe instead of a wide diagonal line, since the camera was looking almost straight down
  the path's own length. A second path at a different angle framed correctly, confirming the
  underlying math was sound and the fixed direction was the actual problem. **Fixed:**
  `ComputeFraming` now derives the camera's horizontal offset from the path's own `travelDirection`
  (perpendicular to it, rotated 90° in the XZ plane), so the shot is always broadside regardless of
  which way the path runs. Falls back to a fixed forward broadside when there's no horizontal
  travel to derive a direction from.
- **Flat/2D-vs-3D was auto-guess-only.** Per user feedback ("can mod UTI for 3D vs 2D games"), added
  `dimensionMode` (`Auto`/`Force2D`/`Force3D`) so a dev can override the Z-depth heuristic outright.

**T23 fix and multi-angle capture (2026-08-08):**

`MinFramingRadius` was a fixed `2f` literal shared by both the orthographic half-size floor and the
perspective distance floor — found too tight against `project 2`'s actual scale on a near-stationary
path (leg geometry and a wall, no usable context; T23). Now a serialized instance field, threaded as
a parameter into `ComputeFraming`/`ComputeFramingForAngle`, defaultable project-wide via
`BeanConfig.DefaultMinFramingRadius`. Confirmed live: the exact same near-stationary path produced
the reported useless close-up at `MinFramingRadius=2` and a properly-framed shot at `=9`, both PNGs
viewed directly.

Multi-angle capture (`CaptureAngles`) was built the same day, directly motivated by real friction:
project 2's T23 report noted a single auto-framed angle could end up unhelpfully tight or miss
context a different angle would show. `Auto` stayed untouched original behavior. `Side` turned out
to be exactly the same broadside-3D placement `Auto` already used — factored out into
`ComputeSideFraming` so both share one implementation instead of duplicating the broadside-offset
math. Naming settled: a single-angle capture keeps the original `ResolveSnapshotPath` untouched
byte-for-byte; more than one angle uses a new `ResolveMultiAngleSnapshotPath`.

**Found live 2026-08-08 (T27/T28, `project 2` team's fresh-install round):** `CaptureSnapshot()`
reads the live `BeanTracker.Samples` ring buffer, not the CSV — a long idle tail after real
movement finished (tracking left running ~55s longer than needed) can silently evict the entire real
path from that fixed-capacity buffer before a snapshot happens, producing both an invisible path
line and a tight, context-free close-up on just the final resting position — superficially
resembling T23 but a genuinely different mechanism (T23 was a fully-stationary path; this is real
movement followed by a long stationary tail). Reproduced directly, not just theorized: 200 samples
of real 9m movement + 3000 stationary samples dropped the live buffer's recorded span to exactly
zero, first and last buffered positions identical. **Fixed** with a new pure
`IsBufferAtCapacity(sampleCount, maxSamples)` check and a `Debug.LogWarning` in `CaptureSnapshot()`
explaining the likely cause and the fix (raise `Max Samples`, call `StopTracking()` promptly).
Doesn't change framing/rendering — makes the failure mode visible instead of silent.
`BeanVisualizer.DrawPath()` reads the exact same live buffer and is subject to the identical
eviction, though no code fix was made there — a live trail only showing recent history is normal/
expected for a live view, unlike the exporter's whole purpose of summarizing the entire run after
the fact.

**Found live 2026-08-08 (T26):** `Object.Destroy()` on the temporary line/texture objects is a
documented no-op outside Play Mode — calling `CaptureSnapshot()` from an Editor context left 6
`BeanSnapshotPath` GameObjects permanently in a project's actual open scene (Unity logs "Destroy may
not be called from edit mode!" as an error but doesn't throw or actually destroy anything). Fixed
with `SafeDestroy()` (`Application.isPlaying ? Destroy() : DestroyImmediate()`), re-verified live
immediately after — an identical capture produced zero leaked objects.

## §8.5 history: moving off `Application.persistentDataPath`, and the `GetInstanceID()` dispute

**Decided 2026-08-07, correcting a real mistake.** Both `CsvBeanOutput` and `BeanSnapshotExporter`
originally defaulted to `Application.persistentDataPath` — the technically-correct Unity API for "a
writable, per-user location that survives app updates," but it resolves to a hidden, per-user
Windows folder (`AppData/LocalLow/{Company}/{Product}`) with nothing to do with the project folder a
developer actually has open. Confirmed the hard way: the person who designed the feature went
looking for their own generated snapshot in the project directory and genuinely couldn't find it.
For a tool whose entire purpose is "a developer looks at the results afterward," a
technically-correct-but-undiscoverable default defeated the point as thoroughly as the original
no-persisted-artifact gap did.

Fixed with `BeanArtifactPaths` (§8.5 in the main doc). First pass put `BeanLogs/`/`BeanSnapshots/`
as loose sibling folders directly at the project root; revised the same session, per feedback, to
nest one level further into a single `UTI/` folder so everything UTI generates lives in one
clearly-labeled place instead of scattered next to the project's own `Library`/`Logs`/`Temp`.

**The `GetInstanceID()`/CS0619 dispute:** the uniqueness token was originally keyed on
`GameObject.GetInstanceID()`, reverted after a relayed build reported it as a hard compile error
(`CS0619`, obsolete-as-error) against that project's Unity version — a claim that couldn't be
independently confirmed as a real Unity API change (`GetInstanceID()` is a foundational, extremely
widely-used API, and no record of a `GetEntityId()` replacement was found). Rather than gamble on an
unconfirmed API, switched to a random GUID-fragment token (`BeanArtifactPaths.NewUniqueToken()`)
instead — sidesteps the dispute entirely regardless of whether the claim was real, and is arguably
stronger anyway (true randomness, no dependency on Unity's own object-identity internals).

## §8.7 history: why `BeanConfig` isn't a `ScriptableObject`

Per direct user correction, what was actually wanted wasn't a new capture mode (an
`EveryNTicks` mode had briefly been added and was reverted the same session) but "give the dev the
ability to choose and change some stuff" — project-wide preferred settings, editable in one place.

**First built as a real Unity asset** (`ScriptableObject` at `Assets/UTI/BeanConfig.asset`), since a
`ScriptableObject` only works as an editable, discoverable asset if it lives inside a project's
`Assets/` folder. **User pushed back:** the whole point was one place for a project's UTI footprint,
and splitting config into `Assets/` while logs/snapshots/docs live in the plain `UTI/` folder
defeated that. **Fixed by dropping the Unity-asset requirement entirely** — `BeanConfig` became a
plain text file at `<project root>/UTI/BeanConfig.txt`, no `AssetDatabase`/`ScriptableObject`
involved. Switching away from a `ScriptableObject` incidentally made the parsing *more* testable
than the original design too — no `AssetDatabase`/Editor-only lookup to work around, just plain
string parsing.

Field additions after the initial build: `DefaultMinFramingRadius` (2026-08-08, the T23 fix) and
`DefaultOutputTargets` (2026-08-08, added alongside JSON Lines output — the first key to affect
`BeanLogger` rather than `BeanTracker`/`BeanSnapshotExporter`, which required giving `BeanLogger` its
own `Reset()`/`ApplyConfigDefaults()` pair for the first time).

**`UTI > Setup Project (Config + Docs)`, added 2026-08-08:** a second Editor menu item alongside
`UTI > Create Bean Config` — does that (bootstraps `BeanConfig.txt`) and also copies
`USAGE.md`/`READING_LOGS_AND_VISUALS.md`/`CONFIG.md` from the package's own `docs/` folder into
`<project root>/UTI/`, resolving the package's real root via
`UnityEditor.PackageManager.PackageInfo.FindForAssembly` (works regardless of how the `"file:"`
dependency resolves). Added because the doc-copy step kept being a real, recurring gap across every
new test project despite being simple in principle.

## §12 history: the CI setup saga

**`cache-installation: true` tried and reverted, 2026-08-09 — a real regression, not a speedup.**
Added to the "Install Unity" step right after CI first went green, expecting it to skip the
~12–13 min Editor download on repeat runs. Live result was the opposite: the very next run took 35
minutes and hit the job's 30-minute timeout, right in the middle of the cache-save step ("Saving
Unity installation cache..." — a `tar`+`zstd` compress of the whole install). Root-caused from
`buildalon/unity-setup`'s own source (`src/inputs.ts`), not guessed: leaving both `build-targets`
and `modules` unspecified silently installs the platform's IL2CPP module
(`windows-il2cpp`) by default — real, unwanted extra payload for a job that only runs EditMode tests
and never builds a Player. That extra size made the cache-save expensive enough to blow the timeout,
meaning the cache never finished saving, so the next run would have stayed cold regardless — the
project would have paid the cache-save cost every run without ever actually getting a cache hit. No
documented or source-confirmed way to suppress the default IL2CPP module was found (checked the
README and source directly rather than guessing one under time pressure), so `cache-installation`
was removed rather than risk another broken run.

**Corroborated afterward via the action's own issue tracker, not just this repo's own bad luck:**
[buildalon/unity-setup#55](https://github.com/buildalon/unity-setup/issues/55) is another user
hitting a real CI failure specifically when `cache-installation: true` was enabled (a different
symptom — a log-file-lock timeout during a Unity export — but the same trigger: worked with caching
off, broke with it on) — closed **"not planned,"** never fixed by the maintainer. The original
feature request, [#12](https://github.com/buildalon/unity-setup/issues/12), even has its own author
admitting they weren't sure exactly which directories needed caching to reliably preserve a working
Unity install. Reasonable read: `cache-installation` is a genuinely unreliable feature of this
action right now, not something specific to this repo's configuration.

**Real Unity license, activated live each run — not a portable file, and this took two attempts to
get right.** First attempt used `game-ci/unity-test-runner` with a `UNITY_LICENSE` secret holding an
exported license file (the classic, widely-documented approach). That failed a due-diligence check
before ever reaching CI: Unity's licensing backend changed since most of that documentation was
written — manual activation of a **Personal** license through `license.unity3d.com/manual` was
discontinued (that page is Pro/Plus-serial-only now), and the newer license format Unity 6000.x
issues locally (`UnityEntitlementLicense.xml`, replacing the old portable `Unity_lic.ulf`) has
machine-binding identifiers baked into the signed entitlement — a license exported from one machine
will not validate on a different one, and all GitHub-hosted runners present a shared HardwareId
different from any real machine. **Fixed** by switching to `buildalon/unity-setup` +
`buildalon/activate-unity-license` + `buildalon/unity-action` — actively maintained actions built for
the post-transition licensing client, which log in live each run (`UNITY_EMAIL`/`UNITY_PASSWORD`
secrets) so the resulting license is always correctly bound to whichever runner is actually
executing. The real tradeoff: an actual Unity account password now lives in this repo's secrets, not
just a license blob — the push-only trigger exists specifically to keep that exposure as narrow as
reasonably possible.

**First live run failed on a real, findable bug — not a licensing problem.** Once the
`UNITY_EMAIL`/`UNITY_PASSWORD` secrets were added, license activation actually worked (confirms the
approach above is sound). It failed one step later, in `buildalon/unity-setup`'s own
version-detection: `Error: No accessible file found for glob pattern: .../**/ProjectVersion.txt`.
Root cause: dot-prefixed directories get skipped by generic glob implementations by default — the
project shell lived at `.github/ci-project/` at the time. **Fixed** by moving to `CI~/` (§4).

**Second live run, same day: license and `CI~/` both confirmed working, found a third-party bug
next.** The run then failed inside `unity-setup`'s Unity Hub install step: `Error: ENOENT: no such
file or directory, access '/opt/unityhub/unityhub'`. Checked against the action's own issue tracker
before changing anything: a known, currently open, unfixed bug upstream (`buildalon/unity-setup`
issue #57) — newer Unity Hub `.deb` packages on Ubuntu install to `/usr/bin/unityhub`, but the
action's code still hardcodes the older `/opt/unityhub/unityhub` path. **Fixed** by switching the
workflow's `runs-on` from `ubuntu-latest` to `windows-latest` — Linux-path-specific bug, doesn't
exist on Windows, no cost tradeoff since public repos get unlimited free GitHub Actions minutes on
any OS.

**Third live run: `-logFile -` hangs on Windows.** The prior fixes all held — Install Unity (~13
min, expected first-run cost) and license activation both succeeded cleanly. The test step then sat
completely silent for 39+ minutes with zero further log output, never completing. Root cause,
confirmed via Unity's own issue tracker and community reports before touching anything: Windows
applications don't have a real stdout handle by default, so `-logFile -` (which streams cleanly to
stdout on Mac/Linux) can silently **hang the whole process** on Windows rather than just fail to
show output. **Fixed** by writing to a real file (`-logFile CI~/unity.log`) instead, plus a step
that prints that file's contents into the Actions log. Also added `timeout-minutes: 30` at the job
level so any future hang fails loudly within a bounded time.

**Fourth live run: `CI~/` needs an `Assets/` folder, even empty.** The `-logFile` fix worked — this
run failed fast with a clear Unity error instead: `Couldn't set project path to: D:/.../CI~`.
Root-caused via direct local reproduction rather than another slow CI round-trip: ruled out the `~`
character itself first (an identical tilde-free copy failed exactly the same way), then confirmed
Unity refuses to accept `-projectPath` for any directory lacking an `Assets/` folder, even a
completely empty one — something this project shell never had, since there's deliberately no game
content to put there. **Fixed** by adding `CI~/Assets/` (kept in git via `.gitkeep`, since git
doesn't track empty directories).

**Fifth live run: fully green, first confirmed pass end to end.** `Status: Success`, 12m 32s total,
one harmless warning (Node.js 20 deprecation notice from the actions themselves). Verified as a real
pass, not assumed: the log shows Unity finding `ProjectVersion.txt` and opening `CI~/` cleanly, real
compilation of UTI's own assemblies, and named UTI tests actually running and completing — including
two tests whose whole point is deliberately triggering a broken output and confirming UTI logs a
warning and keeps going instead of crashing, exactly as designed (their warning-looking log lines
are the test passing, not a problem). License activated and deactivated cleanly, results artifact
uploaded (38.4 KB). CI now runs the EditMode suite automatically on every push to `main`.

### Round two, 2026-08-11 to 2026-08-15: re-enabling the cache broke CI twice more, for a real reason this time

**Re-enabled `cache-installation`, correcting the round-one misdiagnosis.** The 2026-08-09 revert's
"no way to suppress IL2CPP" conclusion turned out to be wrong — `buildalon/unity-setup`'s own
`src/inputs.ts` explicitly handles `modules: None` (confirmed by reading the actual module-selection
logic: `getArrayInput('None')` returns `['None']`, length 1 not 0, which skips the
platform-default branch entirely; the loop then hits `continue` on the literal `'none'` and adds
nothing — the action even logs `> None` for this case, an anticipated state, not a hack). Also
re-diagnosed round one's real killer: `@actions/cache`'s `saveCache()` runs in the **post-job
phase**, which the job's `timeout-minutes` also covers — the old 30-minute cap was killing the
cache *save* mid-write on the very run that was supposed to seed it, not the actual test work.
Raised to 60 minutes and re-enabled caching with `modules: None` to keep the cache small and the
install fast.

**Run #16: a genuine cache hit (4m8s Install Unity), immediately followed by a 55-minute silent
hang with zero log output**, killed only by the job timeout. Traced as far as possible without
authenticated log access (GitHub's job-logs API 403s even for a public repo without auth, and the
Actions web UI requires sign-in past its summary page): `buildalon/unity-action`'s wrapper
(`@rage-against-the-pixel/unity-cli`) has no internal timeout or hang-detection of its own — it
spawns Unity with `stdio: ['ignore','ignore','ignore']` and just awaits process `close`, so a real
hang and a slow-but-working run look identical to it and it gives zero diagnostic signal either
way. Two new variables had landed in the same run (`modules: None`, and a cache-restored rather
than freshly-installed Editor), not yet isolated.

**Replaced the wrapper with a direct, polling PowerShell launcher for visibility**, rather than
guessing blind: launches Unity via `Start-Process`, polls every 20s logging process
alive/CPU/Windows "Responding" state, `unity.log`'s existence and growth, any `WerFault.exe`
(Windows' crash-dialog process — would explain a silent hang if Unity crashed natively and got
stuck on a dialog nothing in headless CI can dismiss), child processes, and a fallback check of
Unity's default `Editor.log` location. Syntax parse-checked locally (PowerShell AST parser, 0
errors) before pushing, since it couldn't be executed locally to test directly.

**Run #17: the diagnostic launcher worked exactly as intended.** Instead of another opaque hang,
Unity crashed in ~20 seconds with a specific, actionable error: `"Unity.dll failed to load. Make
sure you meet Unity's system requirements."`, exit code `-2147024770` = `0x8007007E` =
`ERROR_MOD_NOT_FOUND`. Ruled out the diagnostic script itself as the cause before trusting this:
read `unity-cli`'s actual spawn options (`baseEditorEnv` only adds `UNITY_THISISABUILDMACHINE` and
a build-pipeline logging flag on top of the inherited environment — nothing PATH- or
module-loading-related — and no `cwd` override anywhere), so the crash wasn't an artifact of using
`Start-Process` instead of the wrapper's `spawn()`. Confirmed via the Actions API that run #17's
own "Install Unity" step *also* completed in 4m12s — another cache hit, same as run #16.

**Reasoned to the real culprit rather than guessing between the two remaining variables.** Every
fresh (non-cached) install in this project's entire history has worked. Both cache-restored
installs failed, in two *different* ways, from what should have been identical cached bytes under
one unchanged key — a genuinely broken-without-that-module Editor should fail the *same* way every
time, not differently each run, whereas non-deterministic corruption during a multi-GB extraction
(a race, antivirus interference, an incomplete/interrupted original save — run #15, the actual
cache-seeding attempt, was itself cancelled mid-install at 29m57s, so what got cached may never
have represented a fully-complete install to begin with) fits the two-different-symptoms pattern
much better. There's also no real mechanism for IL2CPP's absence — a build-target/scripting-backend
module for Player builds — to affect the base Editor's own DLL loading.

**Disabled `cache-installation` again, kept everything else.** `modules: None` stayed (not
implicated by any of this reasoning). The diagnostic launcher stayed too, rather than reverting to
the plain wrapper immediately — it had just proven itself catching a real failure fast, and its
core exit-handling logic (`resolve(code === null ? 1 : code)` in the wrapper vs.
`exit $proc.ExitCode` here) is equivalent to what the wrapper already does, so there was no
meaningful correctness tradeoff to reverting for.

**Run #18: fully green, 14m33s total.** Confirmed cache-restore itself was the broken piece — a
fresh install (with or without the module) works cleanly. **Correction, found the same day after a
direct question about whether the original "stop reinstalling Unity every push" goal had actually
been met:** an earlier pass of this doc reported "Install Unity 2m22s" and framed `modules: None`
as a genuine standalone speed win. That number was wrong — a summarization error never re-verified
against the raw API timestamps at the time. The real figure, pulled directly from the job's raw
step timestamps (`04:58:22Z` → `05:11:44Z`): **13m22s**, essentially identical to this project's
historical ~13-minute default-module install. The two genuinely fast (~4 min) installs only
happened on the cache hits that then failed to actually run (runs #16/#17 above) — a *working*
install, cached or not, hasn't shown any real speedup in this round's actual data.
**This round's original goal is therefore not met**: CI is reliable again, not fast.
`modules: None` still stands on its own (smaller download/install regardless of timing, and this
job never needed IL2CPP), just not for the timing reason previously claimed. `cache-installation`
stays off pending a dedicated investigation into *why* the restore was non-deterministically
broken — that investigation, if it ever succeeds, is the actual remaining path to the original
~2-minute target, not anything shipped so far. Not urgent: a reliably-working ~14.5-minute CI beats
an unreliable one of any speed.

### Round three, 2026-08-15: the overlooked variable, a clean test of it, and the real answer

**The detail round two's own reasoning had already surfaced but not yet acted on:** round two's own
writeup above named "run #15, the actual cache-seeding attempt, was itself cancelled mid-install at
29m57s" as a real possibility for why what got cached might never have represented a complete
install. `@actions/cache` never overwrites an existing key — a save silently no-ops if one's
already there — so if that reasoning was right, every "re-enable caching" attempt since, including
both round-two runs (#16, #17), had been restoring that same original, possibly-incomplete entry,
never a genuinely clean one. That specific variable — a provably clean, uncancelled reseed under a
non-stale key — had never actually been tested. Round three tested it properly.

**Step one: confirmed the suspect entry directly, not just inferred it.** The GitHub Actions
Caches UI showed exactly two entries: `unity-setup-win32-6000.5.6f1` (4.7 GB, cached and last used
12 hours earlier — timeline matches round two exactly) and `unity-setup-win32-6000.5.6f1-windows-
il2cpp` (4.7 GB, a week old — an orphaned leftover from round one, before `modules: None` existed
and changed the cache key). Both deleted by hand before touching the workflow, so the next save
couldn't silently skip onto either one.

**Step two: re-enabled `cache-installation`, raised `timeout-minutes` 25→60 for headroom, pushed,
and let it run to completion without touching it.** Run #21: fully green, and — critically — not
cut off. `Install Unity` 14m59s (normal fresh-install cost, no cache existed yet), `Post Install
Unity` (the actual cache-save) 6m34s, both clean. This is the first unambiguously complete,
uncancelled seed this project has ever produced under this key.

**Step three: one trivial follow-up push to trigger a second, cache-hit run.** Run #22: `Install
Unity` 3m53s — a genuine cache hit, fast exactly like runs #16/#17 were. It then failed in 21
seconds with **the exact same error as run #17, confirmed via the full log (not just timing) once
GitHub sign-in was available to read it**: `"The code execution cannot proceed because Unity.dll
failed to load. Make sure you meet Unity's system requirements."`, exit code `-2147024770`
(`ERROR_MOD_NOT_FOUND`), `logExists=False logSizeBytes=0` — Unity never got far enough to create
its own log. Identical failure, from a cache entry now definitively known to have been seeded
cleanly. **This ruled out the poisoned-seed theory outright** — the corruption is reproducible from
a good seed, so it isn't about seed quality at all.

**Step four: found the actual reason, from the tool's own maintainer, not further guessing.**
Re-read `buildalon/unity-setup`'s issue tracker with the "reproducible from a clean seed" finding
in hand. [Issue #55](https://github.com/buildalon/unity-setup/issues/55) — a different user hitting
a cache-triggered failure on a GitHub-hosted runner — has the maintainer (StephenHodgson) stating
directly: **"cache-installation is only valid for self hosted runners."** Asked why it doesn't just
no-op harmlessly on GitHub-hosted ones instead of failing, the maintainer's own reply: "Hmm maybe
it is a bug then, it should short circuit so it doesn't become a problem on GitHub hosted runners."
Confirmed against their actual source (`src/index.ts`, read directly): the `cache-installation`
code path calls `@actions/cache`'s `restoreCache`/`saveCache` on the Unity Hub install path with no
runner-type branch anywhere — there is no self-hosted-only code, so the missing short-circuit the
maintainer describes is real, not fixed. This fully explains all three rounds' failures with one
first-party-confirmed cause instead of a guessed mechanism (tar/symlink issues, Windows Defender
interference, etc. — all plausible-sounding, none of it actually the reason).

**Considered and rejected: hand-rolling a custom `actions/cache` step instead of the built-in
flag.** The same source read that found the maintainer's explanation also closed this off — the
built-in flag's cache-installation code path is *just* `@actions/cache`'s own `restoreCache`/
`saveCache` calls on the same directory, nothing unity-setup-specific about the restore mechanics
itself. Wrapping the same directory in our own `actions/cache` step would invoke the identical
underlying calls and almost certainly hit the identical failure — there's no reason to expect a
different outcome from routing through our own step instead of the action's.

**Closed out: `cache-installation` set to `false` permanently** (not "currently off pending
investigation" — the investigation is done), `timeout-minutes` back to 25, workflow comments
rewritten to state the maintainer-confirmed reason directly so a future session doesn't re-attempt
this blind. **The only real remaining path to the original ~2-minute goal is a genuine self-hosted
runner** — a real infrastructure and public-repo-security tradeoff of its own, not attempted and
not currently planned; flagged as a real option for whoever revisits this, not a task in progress.

## §13 history: already-fixed limitations

- **CSV file path collisions on duplicate GameObject names.** Fixed 2026-08-07:
  `BeanLogger.ResolveFilePath()` defaults to `{timestamp}_{objectName}_{uniqueToken}_bean.csv` under
  `UTI/BeanLogs/` — two simultaneously-tracked clones get distinct files even opened in the same
  millisecond, since `uniqueToken` is a fresh random GUID fragment, not tied to any Unity
  object-identity API.
- **Object pooling wasn't accounted for.** Decided and built 2026-08-08 (T14): the default stays
  truncate-on-reopen (a fresh log per reuse) — now a documented decision, not an accident. New
  opt-in `BeanLogger.AppendAcrossReuse` (off by default) lets a pooled object keep one running CSV
  across `SetActive(false)`→`SetActive(true)` cycles instead. Needed caching the resolved path on
  first `Open()`, since `BuildActiveOutputs()` otherwise resolves a fresh, newly-timestamped path on
  every `Open()` — a bare append flag alone wouldn't have appended to the same file across reuse.
  `CsvBeanOutput` gained an `append` constructor parameter and only writes the header row when the
  target file doesn't already exist.
- **`EveryFixedUpdate` had no automated test coverage.** Fixed 2026-08-08 (T12):
  `BeanTracker.FixedUpdate()` now calls a new public `SimulateFixedFrame()` (mirroring
  `SimulateFrame(deltaTime)`), so `EveryFixedUpdate` capture is exercised deterministically in
  EditMode without a real physics tick. Live-verified the same day: a real Rigidbody-driven object
  produced 732 samples with `min=max=avg=0.02000` delta between every consecutive timestamp, exactly
  matching `Time.fixedDeltaTime`.
- **`CustomCapture`/`extras` was untested beyond "null by default."** Fixed 2026-08-08 (T11): new
  EditMode tests exercise the actual pipeline — a delegate assigned, `SimulateFrame()` invoking it,
  the result landing in `BeanSample.Extras`, and `CsvBeanOutput` serializing it into the `extras`
  column with real `key=value` data.
- **Multi-Bean scenes were entirely untested.** Partially closed 2026-08-08 (T15): a new EditMode
  test drives several `BeanTracker` instances independently via `SimulateFrame`, confirming each
  one's buffer reflects only its own object (true by construction, now actually checked). The
  gizmo-draw-cost-at-scale half remains open (§13 in the main doc).
- **`BeanSnapshotExporter`'s `MinFramingRadius` was a fixed `2f` literal, not scale-aware.** Fixed
  and verified live 2026-08-08 (T23) — see §8.4 history above.
- **`BeanSnapshotExporter` framed/drew from the live sample buffer, which could silently evict the
  real path.** Found live by the `project 2` team, root-caused and fixed 2026-08-08 (T28) — see
  §8.4 history above.

## §14 history: the boundary-by-boundary fault-isolation writeup

**`BeanTracker.Capture()` — `CustomCapture` is caller-supplied code.** A throwing delegate used to
propagate out of `Capture()` entirely, meaning nothing after the delegate call ran: no sample added,
`tickIndex` never advanced, `OnSample` never fired — every single frame the broken delegate was
invoked, silently corrupting the whole recording rather than just that one field. Now wrapped in
try/catch: a failure logs a warning naming the object and continues, capturing that sample with
`Extras = null` instead of losing the sample (and everything downstream of it) entirely.

**`BeanLogger` — every `IBeanOutput` call is file I/O or otherwise external.** `Open()`/`Write()`/
`Close()` on each active output are individually try/caught rather than a bare `foreach`. Before
this, one broken output (e.g. a CSV path with a permission problem) failing inside `Open()`'s loop
would skip `tracker.OnSample += HandleSample; isOpen = true;` entirely — silently disabling every
*other* output too (Console included), even ones that had already opened successfully, with no
signal beyond a single uncaught exception in the console. Now a failing output logs a warning naming
its type and is dropped from `activeOutputs` (`Open`/`Write` failures) or just logged past (`Close`
failures) — every other output keeps working uninterrupted. Verified with
`TESTS/EditMode/BeanLoggerTests.cs`'s `ThrowingBeanOutput` test double.

**`BeanSnapshotExporter.CaptureSnapshot()` — one angle's disk write shouldn't lose the others.** The
per-angle render/encode/write block is wrapped in its own try/catch/finally: a write failure on one
angle (disk full mid-capture, a permissions issue) logs a warning naming the angle and moves on to
the next one, instead of aborting the whole call and losing angles that already wrote successfully.
The `finally` also guarantees the per-angle `Texture2D` is always destroyed, even on failure.

**`BeanConfig.Load()` — `BeanConfig.txt` is an externally-edited file.** A locked or
permission-denied file used to throw straight out of `Reset()` (the Editor's "add component" hook)
with a raw stack trace. Now a read failure is caught and treated the same as "file doesn't exist" —
returns `null` (compiled-in defaults apply) with a warning, matching the method's own existing
"returns null if it doesn't exist yet" contract rather than adding a new failure mode alongside it.
`CreateTemplateIfMissing()`/`CopyEndUserDocsIfMissing()` are similarly wrapped.

## Full Change Log (since day one)

- 2026-08-15 17:50 — CI cache round three closed: found the real reason after a clean, uncancelled
  reseed (run #21) still produced a cache-hit run (#22) that crashed identically to round two's
  run #17. `buildalon/unity-setup`'s own maintainer confirmed `cache-installation` only works on
  self-hosted runners and admitted the missing GitHub-hosted short-circuit is a bug on their end.
  Set `cache-installation: false` permanently, not just currently. Full writeup above.
- 2026-08-15 00:31 — Corrected a wrong figure from the entry below: run #18's "Install Unity" is
  13m22s, not 2m22s (a summarization error, never re-verified against the raw API at the time) —
  meaning this round's original "stop reinstalling Unity every push" goal was **not** actually
  met. Caught after a direct question about whether the goal had been achieved. Full detail above.
- 2026-08-15 00:17 — CI cache round two closed: run #18 fully green (14m33s total).
  `cache-installation` disabled again after two cache-restored installs failed differently (a
  55-min hang, then a `Unity.dll` load crash) from what should've been identical cached bytes —
  non-deterministic restore corruption, not `modules: None` (see the 00:31 correction above for
  the real install-time figure). Full round-two writeup above.
- 2026-08-14 23:14 — CI cache re-enabled (`cache-installation: true` + genuine `modules: None`
  IL2CPP opt-out, job timeout 30→60min), correcting the 2026-08-09 misdiagnosis below — then hit a
  new problem the same day (see the 2026-08-15 entry above for how it resolved).
- 2026-08-09 22:33 — **T05 closed — `BeanVisualizer`'s live Scene-view gizmo confirmed rendering for
  real.** Full history in `TESTS/TestTracker_HISTORY.md`'s T05 Investigation Notes (eight attempts,
  one reverted premature Pass, then a first-hand user screenshot in `project 2` that finally cleared
  the verification bar).
- 2026-08-09 15:34 — **`BeanVisualizer.DrawPath()` given an injectable `IGizmoDrawer` seam,** closing
  a real test-coverage gap found while reviewing T05's history. Full reasoning and the local,
  mutation-verified 102/102 EditMode run: §8.3 history above and `TESTS/TestTracker_HISTORY.md`'s T05
  notes. Proves `BeanVisualizer`'s own draw-call logic; does not and cannot prove live pixel
  rendering, which was a separate, then-still-open question.
- 2026-08-09 13:17 — **CI's `cache-installation` reverted** — caused a real 35-minute timeout, not a
  speedup. Full writeup: §12 history above. Also: two commits landed back-to-back for T30's relay
  prompt (the flawed draft, then its fix), each triggering its own full CI run — a real process miss,
  not just a docs issue; the flawed draft should have been caught before pushing at all.
- 2026-08-09 11:50 — **T05's brief Pass marking reverted back to Planned, per direct user
  correction.** Earlier the same day, T05 was marked Pass based on screenshots the user provided.
  Corrected: this session did not produce those screenshots itself, could not independently verify
  their source, and its own separate live attempts (including real Play Mode) never reproduced a
  visible gizmo line. A result this session can't reproduce or verify doesn't meet this project's own
  "Pass only after a real, verified report" bar.
- 2026-08-09 10:05 — **CI speedup wired in: `cache-installation: true` added.** Not yet re-verified
  with a live run at the time (see 13:17 entry above for the outcome). Also fixed a naming
  inconsistency: the product name was "UTI (Unity Testing Isolator)" in several files but the actual
  live GitHub repo is `Unity-Testing-Inspector` — standardized on **Inspector** across all four files
  (matches the real, already-shared repo URL) rather than renaming the repo itself.
- 2026-08-08 22:50 — **CI's fifth live run: fully green, first confirmed pass end to end.** Full
  detail: §12 history above.
- 2026-08-08 22:25 — **CI's fourth live run found a fourth real bug: `CI~/` needs an `Assets/`
  folder, even empty.** Full detail: §12 history above. A local end-to-end verification (does it
  reach and pass the actual tests) was inconclusive for an unrelated reason: this repo lives inside a
  OneDrive-synced folder, and background sync activity appeared to cause a Unity build-system
  rebuild loop during the file-write-heavy first-time compile — a local testing-environment artifact,
  not evidence the fix itself was wrong. GitHub's runner has no such sync process.
- 2026-08-08 21:38 — **CI's third live run found a third real bug: `-logFile -` hangs on Windows.**
  Full detail: §12 history above.
- 2026-08-08 20:41 — **CI's second live run found a second real bug: switched runner OS from Ubuntu
  to Windows.** Full detail: §12 history above.
- 2026-08-08 20:35 — **CI's first live run found a real bug: `CI~/` moved from `.github/ci-project/`.**
  Full detail: §4 history and §12 history above.
- 2026-08-08 19:50 — **CI's license activation approach corrected before its first real run.** Full
  detail: §12 history above.
- 2026-08-08 19:05 — **Added CI and `docs/ONBOARDING.md`, following a project review.** New
  `.github/workflows/tests.yml` + `.github/ci-project/` (a minimal, scrubbed Unity project shell) run
  the EditMode suite headlessly on push/PR; not yet exercised live at this point (needed a Unity
  license secret added first). `ci-project/ProjectSettings` started as a copy of a real consuming
  project's settings, then had every identifying field (company/product/project name, Unity Cloud
  project ID, organization ID) scrubbed before being committed — confirmed clean via a full-folder
  string sweep before anything was written.
- 2026-08-08 16:40 — **JSON Lines export shipped and verified live, plus a duplication refactor.** New
  `JsonlBeanOutput` (`IBeanOutput`), wired into `BeanLogger` as `BeanOutputTargets.Json` alongside
  `Console`/`Csv`; `BeanConfig` gained `DefaultOutputTargets` and `BeanLogger` gained its first
  `Reset()`/`ApplyConfigDefaults()` pair to consume it. `ResolveFilePath` generalized with an
  `extension` parameter (default `"csv"`, every existing call site keeps compiling unchanged); an
  explicit `FilePath` is ignored for both formats when CSV and JSON are active together, same
  collision precedent as `BeanSnapshotExporter`'s multi-angle capture. Once both `CsvBeanOutput` and
  `JsonlBeanOutput` existed side by side, their `StreamWriter`-lifecycle code turned out to be
  identical except for per-line formatting and whether a header exists — extracted into a shared
  `BeanFileOutputBase`. Verified live in `project 2` via direct access, both before and after the
  refactor: clean compile, 0 console errors, 97/97 EditMode tests passing (10 new tests covering JSON
  output, `ApplyConfigDefaults`, and the CSV+JSON collision fallback). This closed the "unverified,
  suspected of spamming the console" open item from a prior session's `HANDOFF.md` — it wasn't the
  JSON code; both compiled clean the whole time.
- 2026-08-08 16:10 — **T08 descoped from "build demo scenes" to "use existing projects" — standing
  rule added.** Proposed building `Samples~/` car/NPC/player demo scenes; corrected directly by the
  user: building a new demo/sample project purely to test/showcase UTI is out of scope. Old
  demo-scene idea moved to `PROJECT_OVERVIEW.md`'s Dream To-Do section; rule written into `CLAUDE.md`.
- 2026-08-08 15:45 — **T12/T14 Play Mode gaps closed live** — the last two Play-Mode-only test gaps.
  This session's Unity MCP connection unexpectedly supported real Play Mode entry
  (`EditorApplication.EnterPlaymode()`) and `GameObject.SetActive`, both hard-blocked in every prior
  session. Full detail: §13 history above.
- 2026-08-08 15:20 — **Went public.** Repo created at github.com/DataFright/Unity-Testing-Inspector
  (MIT), first commit pushed. This file moved from the package root to `docs/DESIGN.md` as part of a
  cleanup for the public repo — root now holds only `README.md` (short pitch + fresh-clone install
  guide), `LICENSE`, `package.json`, `CLAUDE.md`, `Runtime/`, and `TESTS/`; everything else moved
  under `docs/`. `BeanConfig.CopyEndUserDocsIfMissing()`'s source path updated to match.
- 2026-08-08 14:50 — **T28: found live by the `project 2` team, root-caused live, fixed same day.**
  Full detail: §8.4 history above.
- 2026-08-08 13:15 — Rewrote the stale §12 (Verification Strategy) and formalized **the
  Bring-Your-Own-Test Protocol**. Written up directly from a real `project 2` T27 round hitting the
  failure mode it warns against.
- 2026-08-08 12:55 — New `TESTS/ErrorHandlingTracker.md` (EH01–EH09), tracking every guarded system
  boundary at-a-glance. Also a bug report from the `project 2` team flagged a second occurrence of an
  ambiguous-`Object`-reference `CS0104` compile error class — already fixed as a side effect earlier
  the same session, formally confirmed via a full-repo sweep.
- 2026-08-08 12:20 — **Live verification round via this session's own Unity MCP connection, attached
  directly to `project 2`.** Full EditMode suite: 84/84 passed. T22's `Reset()`-hook and the new
  `UTI > Setup Project` menu item confirmed live. T23 confirmed with real, viewed PNGs. T24 confirmed
  with 4 real, visually-distinct viewed PNGs. Found and fixed T26 (`Object.Destroy()` no-op outside
  Play Mode) via this live testing itself. Confirmed this connection's real limits by testing them
  directly: named `System.Reflection` types sandboxed off; both Play Mode entry and
  `GameObject.SetActive` toggling believed blocked at this point (corrected the same day — see the
  15:45 entry above).
- 2026-08-08 11:40 — Added §14, Error Handling & Fault Isolation. Full detail: §14 history above.
- 2026-08-08 10:55 — Closed most of the `project 2` round's punch list in one pass (T23 fix,
  multi-angle snapshots, T11/T12 closed with new EditMode tests, T15's EditMode half, T14 decided and
  built) — all code-complete and unit-tested, **none live-verified yet** at this point. New `UTI >
  Setup Project (Config + Docs)` Editor menu item. Also found: the three end-user docs
  (`USAGE.md`/`READING_LOGS_AND_VISUALS.md`/`CONFIG.md`) existed only as copies inside `little
  wings`, never actually saved back into this package repo — restored them, fixing a real staleness
  bug found in the process (`USAGE.md` §8 still described the old `ScriptableObject`-based
  `BeanConfig`).
- 2026-08-08 09:30 — First full closing report from `project 2`. Headline result: UTI's CSV pinned a
  real game bug (a jump-trigger distance that was geometrically unreachable given the player's own
  collision radius) to five decimal places, something the dev said they wouldn't have found from
  behavior alone.
- 2026-08-07 — `BeanConfig` (§8.7) rebuilt as a plain text file instead of a `ScriptableObject`
  asset. Full detail: §8.7 history above.
- 2026-08-07 — Corrected where `USAGE.md`/`READING_LOGS_AND_VISUALS.md` actually belong: end-user
  docs for a dev *using* UTI, not for developing UTI itself, so they need to live where that dev is
  actually looking — each game project's `<project root>/UTI/` folder, not just this package repo.
- 2026-08-07 — Reverted `BeanTracker.EveryNTicks` (added earlier the same session) after user
  clarification: the actual ask was "give the dev the ability to choose and change [existing]
  settings," not a new capture mode. Replaced with `BeanConfig` (§8.7).
- 2026-08-07 — T13/T16/T17 verified Pass live in `little wings` (25/25 tests across
  `BeanArtifactPathsTests`/`BeanLoggerTests`/`BeanSnapshotExporterTests`; real PNG+CSV confirmed on
  disk under the new `UTI/` folder, `LastLineWidth=3.47` confirming the width-scaling fix is live).
  Same round found the auto-frame camera's fixed offset direction problem (§8.4 history above),
  fixed the same day. Also added: `BeanSnapshotExporter.DimensionMode`, `BeanMouseTracker`, and two
  new root-level docs (`USAGE.md`, `READING_LOGS_AND_VISUALS.md`).
- 2026-08-07 — Fixed a real compile-blocking regression: `Object.GetInstanceID()` reported as
  `CS0619` (obsolete-as-error). Full detail: §8.5 history above.
- 2026-08-07 — Two more refinements to UTI's generated-file handling: nested `BeanLogs/`/
  `BeanSnapshots/` one level further into a shared `UTI/` folder at the project root; closed the CSV
  half of the filename-collision gap to match the PNG side.
- 2026-08-07 — Moved UTI's default output location off `Application.persistentDataPath` entirely.
  Full detail: §8.5 history above.
- 2026-08-07 — Found and fixed a real bug in the auto-framing fix: the folder and unique timestamped
  filenames were confirmed working, but the auto-framed capture itself came back showing ground/sky/
  horizon and a tiny distant plane with **no visible path line**. Root cause: `lineWidth`'s default
  (`0.1` world units) was tuned for a close manual shot and became sub-pixel/invisible once
  `autoFrameCamera` correctly pulled the camera back far enough to fit an entire path in frame. Fixed
  by scaling the line's actual render width to the computed framing distance/orthographic size.
- 2026-08-07 — T17's real capture surfaced three follow-on gaps in `BeanSnapshotExporter`, all fixed
  same-session: dedicated `BeanSnapshots/` subfolder, timestamp-prefixed filenames, and
  `autoFrameCamera`. Full detail: §8.4 history above.
- 2026-08-07 — `BeanSnapshotExporter` tuned for speed over fidelity per user feedback: default
  capture resolution dropped from 1920x1080 to 640x360.
- 2026-08-07 — Decided and built the persisted-visualization-artifact feature. Full detail: §8.3
  history above.
- 2026-08-07 — Re-flagged §8.3's live-only-visualization gotcha as a real, flagged design gap per
  user feedback. Also: a second T05/T06 live-Play verification round in `little wings` (a real
  ~103s run, not scripted) got strong objective evidence for editor responsiveness (steady
  ~330–370fps via Unity's own frame-timing, zero console errors, measured after the buffer hit its
  1000-sample cap) and a new cross-check (CSV logged 36,789 rows over ~103s, consistent with that
  frame rate) — but the actual gizmo *render* still hadn't been seen; screenshot tooling failed a
  third time.
