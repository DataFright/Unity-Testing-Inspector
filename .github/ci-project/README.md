This is **CI scaffolding only** — not a game, not a demo, not a sample. It exists purely so
GitHub Actions can point Unity's headless Test Runner at a real project and run UTI's EditMode
suite (`TESTS/EditMode/`) on push/PR. See `.github/workflows/tests.yml`.

There is no `Assets/` folder here and none should be added — no scenes, no GameObjects, no
gameplay code. UTI's own standing rule against building demo/sample Unity projects/scenes still
applies; this folder is infrastructure to run the tests that already exist, not a place to test
or showcase a feature by hand.

`ProjectSettings/` started as a copy of a real consuming test project's settings (known to work
with UTI on the pinned Unity version — see `ProjectSettings/ProjectVersion.txt`), then had every
identifying field scrubbed (company/product/project name, Unity Cloud project ID, organization ID)
before being committed here. If you ever need to refresh these settings from a newer Unity
version, re-check `ProjectSettings.asset` and `ProjectSettings/Packages/` (if present) for the
same kind of identifying data before committing again.

`Packages/manifest.json` references UTI itself via a relative `file:` path (`../../..`, back up
to the repo root) plus the minimum package set needed to compile and run the tests — not the
full dependency list of whatever real project this was sourced from.
