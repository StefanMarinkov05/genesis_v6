<!-- last-verified: f624752 -->
# CLI reference — `dps-nn`

**Diataxis type:** reference. **Source of truth:** the command tree in `src/Genesis.Cli/Program.cs`
(`dotnet run --project src/Genesis.Cli -- <command> --help` prints the same options).
Run from the repo root — `templates/` and `spec.schema.json` resolve relative to it.

## Commands

| Command | What it does |
|---|---|
| `run --all` | The whole pipeline for one spec: `spec-expand` → `scaffold` → … → `report`. Skips stages already done (resume). |
| `run --all --from <stage>` | Force `<stage>` and everything after it to re-run. |
| `create` | Agent mode (ADR-013): description → drafted spec → `run --all` → repair the spec on failure. See `docs/explanation/agent-mode.md`. |
| `spec build --notes <file>` | Draft a `project-spec.json` from free-form notes via the LLM (manual paste flow without a key). |
| `spec expand` | Feature expansion + `$variation:` resolution → `project-spec.lock.json`. |
| `scaffold`, `emit-models`, `identity`, `emit-controllers-views`, `seed`, `verify`, `report` | Run one stage on its own (debugging). |

## Options

Shared by every command:

| Option | Default | Meaning |
|---|---|---|
| `--spec <path>` | `project-spec.json` (with a picker, see below) | The spec to generate from. `create`: where the drafted spec is written (default `<out>/project-spec.json`). |
| `--out <dir>` | `out` (`create`: `out/agent_<timestamp>`) | Output root; the project lands in `<out>/<ProjectName>/`. |
| `--no-interactive` | off | Never prompt (spec/seed pickers are skipped); fail instead. |
| `--resume` | off | Continue after a manually filled `prompts/in/<stage>-response.json`. |
| `--version` | — | `6.6.6+<commit>` (from `VERSION`). |

`run` and `create` also take `--image-provider pexels|pixabay|picsum` (default `pexels`, falls back
to picsum without a key) and `--image-pack <dir>` (local images instead of downloads).
`create` adds `--describe "<text>"`, `--describe-file <file>` (or stdin) and `--max-repairs <n>`
(default 3).

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Success. |
| `1` | A stage failed / the agent stopped (reason printed; agent writes `agent-report.json`). |
| `2` | Bad usage (`create` without a description or without `ANTHROPIC_API_KEY`). |
| `3` | An LLM exchange is waiting for a manual response (ADR-005): paste into the printed `prompts/in/<stage>-response.json`, re-run with `--resume`. |

## Environment variables

| Variable | Effect |
|---|---|
| `ANTHROPIC_API_KEY` | Automatic LLM exchanges (else: manual file contract, exit 3). Required by `create`. |
| `DPS_NN_MODEL_SPEC` / `DPS_NN_MODEL_SEED` | Override the models (defaults `claude-sonnet-5-5` / `claude-haiku-5-5`). |
| `ANTHROPIC_BASE_URL` | Alternative API endpoint (proxy, test stub). |
| `PEXELS_API_KEY` / `PIXABAY_API_KEY` | Real stock photos for seed images (else picsum). |
| `DPS_NN_LOG_LANGUAGE=en` | English parallel-worker names in the log instead of the Bulgarian ones. |
| `ConnectionStrings__DefaultConnection` | Standard ASP.NET override of the *generated app's* database (default: LocalDB). |

## Choosing among several specs / seeds

Omitting `--spec` with several `project-spec*.json` files around (cwd, `fixtures/`, the
`prompts/out/schemas/` archive) shows a numbered menu; exactly one candidate is used
automatically; an explicit `--spec` never prompts.

Seed candidates live in `seeds/` or `prompts/out/seeds/`. With 2+ of them, `seed`/`run --all` shows
a menu; the choice is copied to `prompts/in/seed-response.json` and any stale `Data/seed-data.json`
in the target project is cleared so the choice actually takes effect. A *single* candidate is
deliberately not auto-applied — that would force a reseed on every run and defeat the
"reuse existing valid seed data" path. Both menus are skipped under `--no-interactive`.

## Project history archive

When `run --all` succeeds, the spec (+ `.lock.json`) and the seed used are **copied** to
`prompts/out/schemas/<project>-schema.json` (+ `-schema.lock.json`) and
`prompts/out/seeds/<project>-seed.json` — a per-project record of exactly what produced that
generation. Both pickers search these folders, so archived specs/seeds stay selectable. The latest
run of a project name overwrites its entry (same convention as `logs/registry.json`). Gitignored.

## Logs

- `logs/registry.json` / `registry.jsonl` — every project → the spec and seed it came from
  (latest snapshot / full history). See `logs/README.md`.
- `logs/<Project>/actions.log.jsonl` — that project's leveled action log; a stage failure's full
  exception lands here, not just on screen.
- `logs/global-errors.jsonl` — failures before a project exists (spec resolution, missing `templates/`).
- `<out>/<Project>/dps-nn.log.jsonl` — the stage progress feed (the GUI tails it) and the
  hash-logged LLM exchanges.

## One-click scripts (Windows)

- **`1-build-and-check.bat`** — SDK check, `dotnet build` + `dotnet test`. Never generates
  anything; safe to re-run.
- **`2-generate.bat`** — generates exactly one project per click into its own
  `out/run_<yyyyMMdd_HHmmss>/`, so nothing is overwritten. If the run pends on a manual LLM paste
  (exit 3), the next click resumes in the same folder (tracked in `logs/.pending-outdir.txt`,
  removed when that run completes).
