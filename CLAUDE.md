<!-- last-verified: 1b5fea4319805d9a0571619a0318462c896ee4ba -->
# DPS-NN (ДПС-НН)

DPS-NN (ASCII `dps-nn`, display `ДПС-НН`) is a C# ASP.NET Core 10 MVC + EF Core application
generator. Its core module, **Genesis** (`src/Genesis.*` — internal namespaces stay `Genesis.*`
by design, see ADR-009), is a headless .NET CLI that turns a `project-spec.json` into a complete,
seeded, Bulgarian-UI web app through a resumable 9-stage pipeline. A GUI is planned (see
`plan/dps-nn-6.5.0-spec.md`) but does not exist yet — Genesis is CLI-only today.

> This file is a router. It holds almost no content — it points to where the content lives.

## Read first, every new session
`plan/state.md` and `plan/decisions.md` in full — the running, session-by-session history of every
decision and bug fix so far. Do not re-propose a fix already tried and recorded as failed there.
Older sessions (1-21) live in `plan/state-archive.md`, split out to keep the auto-read file smaller
(changelog 094-era cleanup) — check it too if a symptom might trace back to the early v5/v6 era.

## Routing — read the right doc for the task

| If you are... | Read |
|---|---|
| Orienting for the first time | `README.md` → `docs/01-tutorial-genesis.md` for the full walkthrough |
| Working on pipeline order / stages | `docs/reference/control-flow.md` (Mermaid diagram + stage table + verify-gate ordering) |
| Asking *why* it is built this way | `docs/explanation/architecture-decisions.md` (synthesis, links each of the 13 `docs/adr/adr-*.md`) |
| Touching `project-spec.json` shape | `spec.schema.json` (authoritative) + `docs/reference/spec-format.md` (concepts) |
| Looking for the current priority / work queue | `plan/v6-task-breakdown.md` |
| Tracking a specific bug/decision's history | `plan/decisions.md` |
| About to write a changelog entry | `changelogs/README.md` (numbering + template convention) |
| Wondering what a source file does | `docs/reference/file-map.md` (generated — regenerate via `scripts/generate-file-map.ps1`) |
| Fighting Scriban / Razor / EF migrations, or a recurring build trap | `docs/explanation/techniques.md` |
| Adding a stage / a template | `docs/how-to/add-a-stage.md` / `docs/how-to/add-a-template.md` |
| Planning the 6.5.0 modularization + GUI | `plan/dps-nn-6.5.0-spec.md` |
| Restructuring the docs (Task A) | `plan/task-a-doc-architecture.md` (target tree + per-doc contract; `docs/reference/`, `docs/how-to/` are stubs until filled) |
| Building the GUI (Electron shell over the CLI) | `docs/explanation/gui-architecture.md` (+ ADR-010) |
| Styling/theming the GUI (tokens, the 5 themes) | `design/design-specs.md` |
| Reworking image handling (cache, fallbacks, PFP) | `docs/explanation/image-pipeline.md` (+ ADR-011) |
| Consolidating small admin models / adding filters | `docs/explanation/specifications-consolidation.md` (+ ADR-012) |

## Critical rules (DO NOT)

- **Changelog discipline is mandatory** (standing skill: `changelog-discipline`). After ANY change:
  write one entry in `changelogs/NNN-slug.md`. Before proposing a fix for a recurring/reported bug:
  read existing changelog entries + `plan/decisions.md` first — never re-propose a theory already
  tried and failed. After 2-3 failed theories for the same symptom, stop guessing and add
  diagnostics instead of a fresh confident guess.
- **Never assume a fix works without running it for real** — `dotnet build`/`dotnet test`/the
  actual pipeline. A real compiler and a real terminal are both available; don't reason blind.
- **Bulgarian-only UI is a hard requirement** for generated apps — every non-key/FK model property
  needs a `displayName` in the spec JSON (`feature-templates/*.json`, `fixtures/project-spec.facenote.json`).
- **Never touch AutoPazar** — fully removed (changelog 012); FaceNote is the sole fixture.
- **Never edit generated output** under `out/`. Fix the template or the stage, not the artifact.
- **.NET 10 SDK creates `.slnx` by default, not `.sln`** — `dps-nn.slnx` is already checked in;
  don't recreate a `.sln`.

## Pipeline (9 stages, verified against `Pipeline.CanonicalOrder`)

1. `spec-build` — LLM turns freeform notes into `project-spec.json`
2. `spec-expand` — resolves `$variation:`/feature placeholders into `project-spec.lock.json`
3. `scaffold` — `dotnet new mvc` + connection string + initial build
4. `emit-models` — models, DbContext, validation attributes
5. `identity` — ASP.NET Identity scaffold, BG error describer, BG page map, seed users
6. `emit-controllers-views` — scaffolded/templated/hand-written controllers + views, deviations
7. `seed` — seed data (LLM or manual), images, migration apply
8. `verify` — build → EF migrate → boot → HTTP smoke checks → English-leak scan
9. `report` — machine-readable `wwwroot/output/project-summary.json`

Every code-emitting stage ends in a `dotnet build` gate. Full data-flow diagram + verify-gate
ordering: `docs/reference/control-flow.md`. Stage implementations live in `src/Genesis.Core/Stages/*.cs`
(one class per stage; several stages share a file — e.g. `IdentityStage` lives in `BuildStages.cs`).

## Layout (verified against the real tree, not the aspirational 6.5.0 one)

- `src/Genesis.Core/` — the pipeline engine (Spec, Variation, Pipeline, Llm, Stages, Util)
- `src/Genesis.Cli/` — CLI entry point (`dotnet run --project src/Genesis.Cli -- ...`)
- `tests/Genesis.Tests/` — xUnit test suite
- `templates/` — Scriban templates the pipeline renders
- `feature-templates/` — reusable feature fragments (cart, posts, catalogues, orders, rating, follows...)
- `fixtures/` — the canonical dev spec (`project-spec.facenote.json` + its `-en.json` counterpart)
- `spec.schema.json` — the spec's JSON Schema (source of truth for shape/validation)
- `changelogs/` — one file per change, see `changelogs/README.md` for the convention
- `plan/` — `state.md` (session log), `decisions.md` (bug/decision history), `v6-task-breakdown.md`
  (current priority), `dps-nn-6.5.0-spec.md` (modularization + GUI plan, not yet started)
- `docs/` — `architecture.md`, `adr/` (13 ADRs), `explanation/gui-architecture.md`
- `logs/` — `registry.json` + per-project `actions.log.jsonl`, tracks every generation run
- `prompts/out/schemas/` + `prompts/out/seeds/` — permanent archive of every completed
  generation's spec+seed (gitignored, local-only)
- `out/` — generated output, disposable, gitignored — **never hand-edit anything under here**
- `1-build-and-check.bat` / `2-generate.bat` — SDK check+build+test / fresh pipeline run
- `images/` — GUI asset drop folder (logo, flags, wallpapers), empty until Task B of the 6.5.0 spec
- `design/design-specs.md` / `design/footer.md` — GUI theme spec + runtime footer content,
  both placeholders until Task B/Stefan fill them in

**Not yet built** (planned in `plan/dps-nn-6.5.0-spec.md`): the Electron GUI itself (Task B) and
its supporting content. Task 0's literal "move everything under `./DPS-NN/`" was deliberately
**not** done — this repo root already *is* ДПС-НН/DPS-NN, and a full physical repositioning would
touch every path reference in the build scripts, this file, and IDE configs for no benefit until a
`gui/` sibling actually needs the separation (see ADR-009 for the same reasoning applied to a
different rename). Only the `images/`/`design/` folders were created at the current root.
