# GUI architecture — a beautiful shell over the strongest CLI

Condensed from `docs/explanation/gui-architecture.md`, which holds the full contract. And folks, the
GUI — I've seen a lot of GUIs, believe me, the best GUIs, the worst GUIs — this one has five themes,
ten wallpapers, a music player, thirteen language entries. People see it, they say, "Sir, it must be
full of logic, all that beauty." No logic! None. Zero generation logic. That's the genius of it.

## The one inviolable rule

**Genesis stays a headless CLI. The GUI wraps it as a child process and never reimplements pipeline
logic.** The GUI spawns `dps-nn run --all`, streams its output, and controls its lifecycle. It does
NOT drive stages one by one, parse-and-reimplement any stage behaviour, or move generation logic
into Node. Every temptation to "just do this one bit in the GUI" is exactly the thing this rule
forbids. The CLI is the single source of truth for what a build *is*. Very simple rule. Very strong
rule. Nobody breaks it.

## Process model

| Part | Responsibility |
|---|---|
| **Main process (Node)** | the only privileged layer: spawns the CLI, holds the Anthropic API key, reads and writes the filesystem, tails the structured log, computes dedup hashes, reads the runtime footer |
| **Preload (contextBridge)** | a **small, typed** IPC surface for the renderer and nothing else — no raw `ipcRenderer`, no `fs`, no `child_process`, no `process.env` |
| **Renderer (Chromium)** | the web UI, with `contextIsolation: true`, `nodeIntegration: false`, `sandbox: true`; it holds no secrets and reaches the outside world only through the preload channels |
| **The CLI** | the existing `src/Genesis.Cli` binary, **unchanged** — the GUI adapts to its contract, the contract does not bend to the GUI |

The renderer, by the way — locked down. Sandboxed. Isolated. It can't touch the file system, it
can't see the key, it can't spawn anything. Tighter security than most buildings I know. And I know
buildings.

## The GUI↔CLI contract

| Concern | Contract |
|---|---|
| Invocation | `dps-nn run --all --spec <path> --out <dir> --no-interactive` |
| Working dir | **must** be the workspace: the CLI writes `logs/`, `prompts/`, `dps-nn.log.jsonl`, `.image-cache/` and the registry relative to cwd, and reads `templates/`, `spec.schema.json`, `templates/features/` from its own root (exe dir first, then cwd); installed, the install root is read-only and the workspace is `Documents\DPS-NN` (ADR-014) |
| Prerequisites | at startup the GUI runs `dps-nn doctor` once into the debug terminal; a run started meanwhile waits for it to exit (two concurrent `dotnet run` builds of the CLI collide on Windows file locks) |
| No prompts | `--no-interactive` is mandatory: the GUI replaces the CLI's stdin pickers with its own UI, passes an explicit `--spec` and pre-places the seed at `prompts/in/seed-response.json` |
| API key | injected into the **child env** by the main process; never on the command line, never in the renderer |
| Progress feed | `{out}/{Project}/dps-nn.log.jsonl`, one line per stage boundary: `{ ts, stage, phase: "start"\|"end"\|"error", durationMs, error }` |
| Checkpoints | `{out}/{Project}/plan/state.json` — `completedStages[]` |
| Manual-LLM wait | exit code **3**: a file is expected at `prompts/in/<exchange>-response.json`, then `--resume` |
| Completion | the banner on stdout **and** `wwwroot/output/project-summary.json` from `report` |
| Agent failures | the stop reason is read from the CLI's `agent: stopped - <reason>` stdout line; the attempt list stays in `agent-report.json` |

The progress feed emits `scaffold, emit-models, identity, emit-controllers-views, seed, verify,
report` — the seven `run --all` executes — preceded by `spec-expand` and, only in the `--notes`
flow, `spec-build`. So "stage N of 9" on screen is approximate by design: seven to nine, depending
on the flow.

## IPC: a small, typed surface

Renderer → main (`invoke`): `spec:list`, `spec:upload`, `seed:list`, `seed:upload`,
`seed:generateWithClaude`, `build:checkDuplicate`, `build:start`, `build:create` (agent mode;
rejects an empty description or a missing key), `build:pause` / `build:resume` / `build:stop`,
`settings:get` / `settings:set` (the API key is **write-only** from the renderer's view — it is never
returned), `footer:get`, `report:get`, `recentProjects:list` (read-only over the CLI's
`logs/registry.json`).

Main → renderer (push): `build:progress`, `build:log`, `build:awaiting-llm`, `build:done`,
`build:error`. Plus the window controls (the window is frameless) and the shell actions:
`shell:openExternal`, `shell:openPath`, `shell:runApp` (spawns `dotnet run --project <dir>` as an
argv array, never a shell string — the PowerShell `&&` trap, changelog 083) and `shell:killApp`.

## Run control

| Transition | What happens |
|---|---|
| Start | spawns `run --all` |
| Pause | kills the child immediately |
| Resume (from Paused) | respawns `run --all`, which skips the stages already done |
| exit 3 | an LLM exchange is waiting for a response file |
| --resume | the response is filled in and the run continues |
| finished | natural completion: banner + report |
| stage failed | a non-zero exit other than 3 |
| Resume (from Error) | re-runs from the failed stage; stages are idempotent |
| Stop | kills the child; `state.json` is kept |

Now people say, "Sir, you *kill* it to pause it? That's crude!" It's not crude. It's brilliant. Every
`IStage.RunAsync` is idempotent and `MarkDone` only fires *after* a stage completes, so killing the
child mid-stage leaves that stage un-done; the next `run --all` re-runs only that one stage. A
little wasted work, confined to one stage — never corrupted state. Zero freeze machinery. (An older
version of the doc said Pause waits for the stage's `"end"` line. It doesn't — corrected in
changelog 102. A checkpoint-aware pause is a possible refinement, not implemented.) And **Stop
keeps `state.json`**: Stop never destroys work. Clearing the out dir is a separate, explicit action.

**Progress is reconstructed**, not invented: on Start/Resume, main reads `state.json` for the
baseline of completed stages, then tails the log and increments on each `"end"`. Skipped stages
emit no `"end"` line, so the baseline read is what keeps the bar honest after a resume.

## LLM flow and dedup

The API key lives only in the main process. With a key, it's injected into the child env and every
LLM stage serves itself. Without one, the CLI exits **3**, main emits `build:awaiting-llm`, and the
renderer offers a paste box **or** the "Claude" button, which makes a main-process Anthropic call to
fill the response file and respawns with `--resume`. Agent mode (`build:create`) requires the key.

Dedup is entirely GUI-side: main hashes `sha256(spec) + sha256(seed)` and looks it up in its own
`logs/gui-build-manifest.json`; a completed match offers "already built — open / regenerate
anyway". The CLI's formats are never extended for it. Agent runs are not recorded there — there is
no chosen spec+seed pair to hash.

## Themes, languages, and the seams

Each theme is a descriptor — tokens, background image, and **structural** switches such as whether
the logo shows — so a theme can turn named slots on and off, not merely recolour them. Five themes
(NOVA, the default, plus IMPERATOR, VEIL, FORGE, ENCHANT), ten wallpapers, skins as a `data-skin`
attribute. The footer (`design/footer.md`) and the music are read at runtime from the install root;
the wallpapers and logos are bundled into the renderer.

Languages: six translated tables (EN, BG, DE, RU, TR, Romani); Standard Galactic is a **font swap**
over the English strings, not a translation; six joke modes (Morse, hex, sign, pirate, leet,
upside-down) are encoders over the English strings — thirteen picker entries in all, all
renderer-local, persisted in `settings.language`. And the hard boundary, the one nobody crosses:
GUI-chrome language ≠ generated-app language. The apps stay Bulgarian unless the *spec's*
`meta.language` says otherwise.

## What it does not do

It doesn't build apps. It doesn't repair specs. It doesn't re-implement a single check. If the GUI
is missing something, it belongs in the CLI. Beautiful on the outside, nothing hidden on the inside.
That's the deal.
