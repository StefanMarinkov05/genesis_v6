# CLI reference — `dps-nn`, the most powerful command line

A lot of people come up to me, strong people, smart people, developers, and they say, "Sir, the GUI
is beautiful, the themes, the music, incredible — but what about the command line?" Great question.
Tremendous question, maybe the best question I've ever gotten, and I get a lot of questions,
believe me. The command line is where it all happens. The GUI calls it. The agent calls it. Scripts
call it. Everybody calls the command line. This chapter is the reference, condensed from
`docs/reference/cli.md` — and the source of truth, always, is the command tree in
`src/Genesis.Cli/Program.cs` (`dotnet run --project src/Genesis.Cli -- <command> --help` prints the
same options).

From a checkout, run from the repo root. The CLI reads `templates/` and `spec.schema.json` from its
own folder first, then the current one, and writes `logs/`, `prompts/`, `out/` and `.image-cache/`
to the current folder — so the installed `dps-nn.exe` works from any writable folder (the GUI uses
`Documents\DPS-NN`; ADR-014).

## Commands

| Command | What it does |
|---|---|
| `run --all` | The whole pipeline for one spec: `spec-expand` → `scaffold` → … → `report`. Skips stages already done (resume). |
| `run --all --from <stage>` | Force `<stage>` and everything after it to re-run. |
| `create` | Agent mode (ADR-013): description → drafted spec → `run --all` → repair the spec on failure. See [Chapter 7](07-agent-mode.md). |
| `doctor` | Check the prerequisites — .NET 10 SDK, `dotnet-ef` 10.x, `dotnet-aspnet-codegenerator`, LocalDB, the templates, a writable workspace, the API key (warning only) — and print the fix for each gap. The GUI runs it at startup into its debug terminal. |
| `spec build --notes <file>` | Draft a `project-spec.json` from free-form notes via the LLM (manual paste flow without a key). |
| `spec expand` | Feature expansion + `$variation:` resolution → `project-spec.lock.json`. |
| `scaffold`, `emit-models`, `identity`, `emit-controllers-views`, `seed`, `verify`, `report` | Run one stage on its own (debugging). |

One remark about `--from`, and then I'll let it go — no, I won't let it go: `--from spec-build` and
`--from spec-expand` run nothing at all. Sleepy No-Op, I told you about him in
[Chapter 9](09-workflow.md). Use the stage names the runner actually knows.

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
to picsum without a key) and `--image-pack <dir>` (local images instead of downloads). `create` adds
`--describe "<text>"`, `--describe-file <file>` (or stdin) and `--max-repairs <n>` (default 3).

Version 6.6.6, by the way. Some people say that's a scary number. I say it's a strong number. Very
memorable. You'll never forget it.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Success. |
| `1` | A stage failed / the agent stopped (reason printed; agent writes `agent-report.json`) / `doctor` found a missing prerequisite. |
| `2` | Bad usage (`create` without a description or without `ANTHROPIC_API_KEY`). |
| `3` | An LLM exchange is waiting for a manual response (ADR-005): paste into the printed `prompts/in/<stage>-response.json`, re-run with `--resume`. |

Four codes. Zero, one, two, three. A child could learn them. Some very expensive tools have forty
exit codes and nobody knows what any of them mean. Total disaster.

## Environment variables

| Variable | Effect |
|---|---|
| `DOTNET_ROOT` | Where .NET lives, for the global tools the pipeline launches. Inferred from the `dotnet` on `PATH` when unset (the self-contained exe has no `dotnet` parent to set it). |
| `ANTHROPIC_API_KEY` | Automatic LLM exchanges (else: manual file contract, exit 3). Required by `create`. |
| `DPS_NN_MODEL_SPEC` / `DPS_NN_MODEL_SEED` | Override the models (defaults `claude-sonnet-5-5` / `claude-haiku-5-5`). |
| `ANTHROPIC_BASE_URL` | Alternative API endpoint (proxy, test stub). |
| `PEXELS_API_KEY` / `PIXABAY_API_KEY` | Real stock photos for seed images (else picsum). |
| `DPS_NN_LOG_LANGUAGE=en` | English parallel-worker names in the log instead of the Bulgarian ones. |
| `ConnectionStrings__DefaultConnection` | Standard ASP.NET override of the *generated app's* database (default: LocalDB). |

## Choosing among several specs or seeds

Omitting `--spec` with several `project-spec*.json` files around (cwd, `fixtures/`, the
`prompts/out/schemas/` archive) shows a numbered menu; exactly one candidate is used automatically;
an explicit `--spec` never prompts. You tell it what you want, it doesn't ask. Respect.

Seed candidates live in `seeds/` or `prompts/out/seeds/`. With 2+ of them, `seed`/`run --all` shows
a menu; the choice is copied to `prompts/in/seed-response.json` and any stale `Data/seed-data.json`
in the target project is cleared so the choice actually takes effect. Now here's the clever part —
the smart people get it — a *single* candidate is deliberately **not** auto-applied. Why? Because
that would force a reseed on every run and defeat the "reuse existing valid seed data" path. Very
subtle. Very smart. Both menus are skipped under `--no-interactive`.

## Project history archive

When `run --all` succeeds, the spec (+ `.lock.json`) and the seed used are **copied** to
`prompts/out/schemas/<project>-schema.json` (+ `-schema.lock.json`) and
`prompts/out/seeds/<project>-seed.json` — a per-project record of exactly what produced that
generation. Both pickers search these folders, so archived specs and seeds stay selectable. The
latest run of a project name overwrites its entry (same convention as `logs/registry.json`).
Gitignored — local only.

## Logs

- `logs/registry.json` / `registry.jsonl` — every project → the spec and seed it came from
  (latest snapshot / full history).
- `logs/<Project>/actions.log.jsonl` — that project's leveled action log; a stage failure's full
  exception lands here, not just on screen.
- `logs/global-errors.jsonl` — failures before a project exists (spec resolution, missing
  `templates/`).
- `<out>/<Project>/dps-nn.log.jsonl` — the stage progress feed (the GUI tails it) and the
  hash-logged LLM exchanges.

## One-click scripts (Windows)

- **`1-build-and-check.bat`** — SDK check, `dotnet build` + `dotnet test`. Never generates
  anything; safe to re-run.
- **`2-generate.bat`** — generates exactly one project per click into its own
  `out/run_<yyyyMMdd_HHmmss>/`, so nothing is overwritten. If the run pends on a manual LLM paste
  (exit 3), the next click resumes in the same folder (tracked in `logs/.pending-outdir.txt`,
  removed when that run completes).

Two batch files. One click each. That's it. That's the whole thing. People ask me, "Sir, is it
really that easy?" It's that easy.
