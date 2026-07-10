# dps-nn (ДПС-НН) — project instructions for Claude Code

Read `plan/state.md` and `plan/decisions.md` in full before doing anything else in a new session -
they're the running, session-by-session history of every decision and bug fix so far. Do not
re-propose a fix already tried and recorded as failed there.

## What this is
A C# ASP.NET Core 10 MVC project generator (`dotnet run --project src/Genesis.Cli -- ...`). It turns
`fixtures/project-spec.facenote.json` into a complete, seeded, Bulgarian-language web app (FaceNote)
via a 9-stage pipeline. See `README.md` for the full command surface.

## Mandatory discipline (already a standing skill - `changelog-discipline`)
After ANY change to this project: write one entry in `changelogs/NNN-slug.md` (numbered sequentially,
error template or feature template per `changelogs/README.md`). Before proposing any fix for a
recurring/reported bug: read existing changelog entries + `plan/decisions.md` first. Never re-propose
a theory already tried and failed - if 2-3 fixes have already failed for the same symptom, stop
guessing and add diagnostics/ask for more data instead of a fresh confident guess.

## Current priority: v6 task list
`plan/v6-task-breakdown.md` has the full breakdown of 25 pending requests, grouped into work packages
A-K with model/effort recommendations. Work through packages in the dependency order listed there
(K+A foundation → H followers/following → E upload plumbing → everything else).

## House conventions
- Scripts: `1-build-and-check.bat` (SDK check, sln bootstrap, build+test) and `2-generate.bat` (runs
  the pipeline into a fresh `out/run_<timestamp>/` folder each time).
- `.NET 10 SDK creates .slnx by default`, not `.sln` - checks in both scripts already handle this.
- Bracket/Scriban-balance checks were done by hand throughout prior sessions since no compiler was
  available in that environment - you have a real one now, so just build/test normally instead.
- Bulgarian-only UI is a hard requirement - every non-key/FK model property needs a `displayName` in
  the spec JSON (`feature-templates/*.json`, `fixtures/project-spec.facenote.json`).
- `logs/registry.json` / `logs/<Project>/actions.log.jsonl` track every generation run automatically -
  check there for a project's history (which spec/seed it used) before assuming.
- `prompts/out/schemas/` + `prompts/out/seeds/` hold the permanent archive of every completed
  generation's spec+seed, named `<project>-schema.json` / `<project>-seed.json`.

## Do NOT
- Touch AutoPazar - it was fully removed (see changelog 012); FaceNote is the sole fixture.
- Assume a fix works without actually running `dotnet build`/`dotnet test`/the pipeline for real - you
  have that ability now, use it, don't reason blind the way prior sessions had to.
