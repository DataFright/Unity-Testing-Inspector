# CLAUDE.md

Claude should:

- Track project ideas, features, and concepts in a general `README.md` file for the project. This should be updated as needed.
- Keep tests in a `TESTS/` folder and maintain a tracking test document that should be updated every time a new test is added. Tests should be added when new features or bugs arise to help maintain quality and shorten the debug lifecycle.
- Keep a parallel tracking document for error handling / fault isolation (`TESTS/ErrorHandlingTracker.md`), updated every time a new system boundary gets guarded (caller-supplied code, file/disk I/O, an externally-edited config file, etc.). Not every guarded boundary has an automated test behind it (some need a live Camera/Play Mode) — track its verification status honestly either way, the same as the test tracker does for Planned/Pass/Partial rows.
- Track in-depth design choices in a `DESIGN.md` file, such as exact architecture, data structures, component responsibilities, and how pieces connect to each other.

Additionally, Claude should:

- When updating a document, leave a note at the bottom of the file with a short sentence describing what was updated, along with the date and timestamp. The log should contain no more than 24 log entries, and the newest entry should replace the oldest when the limit is reached.

## Project-specific notes

- UTI is a standalone Unity package/library, not part of any single game. It is developed here and referenced as a local package (`"file:"` dependency) from existing test projects — currently `little wings`, `project 2`, and `2d project 3` — rather than being embedded in or merged with any of them.
- A Unity MCP connection may be available — **check which project it's actually attached to and how much it can do at the start of each session; don't assume from a prior session's notes.** It has varied a lot: one round it was read-only against `little wings` only (`AssetDatabase.Refresh` + console logs, real test execution relayed through a separate agent); another round it was attached directly to `project 2` with real execution — `TestRunnerApi` worked (EditMode only, not Play Mode), Editor menu items could be invoked, GameObjects/components could be created and driven via **public-only** reflection (`Type.GetMethod`/`GetProperty` with no `BindingFlags`; `AppDomain.CurrentDomain.GetAssemblies()` + `Assembly.GetType(string)` instead of a direct `using UTI;`, which doesn't compile in the scratch context). Confirmed hard limits when execution is available: named `System.Reflection` types (`BindingFlags`, `MethodInfo`, etc.) are sandboxed off, and both entering Play Mode and `GameObject.SetActive()` are blocked as unsupported "user interaction." When real execution is available, prefer it over relaying — it can produce definitive evidence (e.g. a real captured PNG, viewed directly) that a relay round's logged claims can't match.
- **Before starting work, check for `HANDOFF.md`** in this folder — if it exists, read it first. It's the cross-session "what's happening / what's next" note, kept current whenever a session pauses mid-task. Update it before ending a session that isn't at a clean stopping point, and it's fine to delete/leave stale once its blocking item is resolved and superseded by the tracker docs.
