<!-- last-verified: 5821ff6f31c37384b3bf306ddf9ee24276324030 -->
# GUI architecture — Electron shell over the headless Genesis CLI

Design record for Task B of `plan/dps-nn-6.5.0-spec.md`. This is the **architecture** every GUI
subtask (theming, i18n, process wiring, dedup, Claude-seed) builds against — it fixes the process
model, the GUI↔CLI contract, and the IPC surface. It does **not** cover visual design (theme tokens,
the 5 themes) — that is Task 2, `design/design-specs.md`.

Everything here is grounded in the *real* CLI contract, verified against `src/Genesis.Cli/Program.cs`
and `src/Genesis.Core/Pipeline/Pipeline.cs` at the stamped commit — not guessed.

## The one inviolable rule

**Genesis stays a headless CLI. The GUI wraps it as a child process and never reimplements pipeline
logic.** The GUI spawns `dps-nn run --all`, streams its output, and controls its lifecycle. It does
NOT drive stages one-by-one, parse-and-reimplement any stage behaviour, or move generation logic
into Node. Every temptation to "just do this one bit in the GUI" is the thing this rule forbids —
the CLI is the single source of truth for what a build *is*.

## Process model

```mermaid
flowchart LR
  subgraph Electron["Electron app (gui/)"]
    R["Renderer (Chromium)\nsandboxed web UI\nno Node, no fs, no key"]
    P["Preload (contextBridge)\ntyped IPC surface only"]
    M["Main (Node)\nspawns CLI, holds API key,\ntails logs, owns all fs"]
    R <-->|"ipcRenderer ↔ contextBridge"| P
    P <-->|"ipcMain handlers + webContents.send"| M
  end
  M -->|"child_process.spawn\ncwd = Genesis root\nenv: ANTHROPIC_API_KEY?\n--no-interactive"| CLI["dps-nn CLI\n(existing .NET, unchanged)"]
  CLI -->|"stdout/stderr (pipe)"| M
  CLI -->|"writes"| LOG["{out}/{Project}/dps-nn.log.jsonl\nstate.json · logs/registry.json\nwwwroot/output/project-summary.json"]
  M -->|"tails / reads"| LOG
```

- **Main process (Node)** — the only privileged layer. Spawns the CLI, holds the Anthropic API key,
  reads/writes the filesystem, tails the structured log, computes dedup hashes, reads the runtime
  footer. Nothing privileged happens anywhere else.
- **Preload (contextBridge)** — exposes a **small, typed** IPC surface to the renderer and nothing
  else. No raw `ipcRenderer`, no `fs`, no `child_process`, no `process.env`.
- **Renderer (Chromium)** — the web UI. `contextIsolation: true`, `nodeIntegration: false`,
  `sandbox: true`. It renders the screens, holds no secrets, and reaches the outside world only
  through the preload channels.
- **The CLI** — the existing `src/Genesis.Cli` binary, **unchanged**. The GUI adapts to its contract;
  the contract does not bend to the GUI.

## The GUI↔CLI contract (all verified against code)

| Concern | Contract | Source of truth |
|---|---|---|
| Invocation | `dps-nn run --all --spec <path> --out <dir> --no-interactive` | `Program.cs` run command |
| Working dir | **Must** be the Genesis root — CLI resolves `templates/`, `spec.schema.json`, `feature-templates/` and writes `logs/`, `prompts/`, `dps-nn.log.jsonl`, the registry all relative to cwd | `GenesisRoot()`, `Llm()` |
| No prompts | `--no-interactive` is **mandatory** — the GUI can't answer the CLI's stdin spec/seed pickers, so it replaces them with its own file/list UI and passes an explicit `--spec` + pre-places the seed at `prompts/in/seed-response.json` | `ResolveSpecInteractively`, `ResolveSeedInteractively` |
| API key | Injected into the **child env** by the main process when configured; never on the command line, never in the renderer | `Llm()` reads `ANTHROPIC_API_KEY` |
| Progress feed | `{out}/{Project}/dps-nn.log.jsonl` — one line per stage boundary: `{ ts, stage, phase: "start"\|"end"\|"error", durationMs, error }` | `PipelineRunner.AppendLog` |
| Checkpoints | `{out}/{Project}/plan/state.json` — `completedStages[]`; `run --all` skips done stages | `StateStore`, `RunAllAsync` |
| Resume | Re-run `run --all` (skips done via `IsDone`) or force a stage with `--from <stage>` | `RunAllAsync(ctx, fromStage)` |
| Manual-LLM wait | Exit code **3** = "waiting for a manual LLM response"; the run stops and expects a file at `prompts/in/<exchange>-response.json`, then `--resume` | `RunStage` catch, `LlmPendingException` |
| Completion | Banner on stdout (URL, seeded creds, DB, start cmd) **and** the machine-readable `wwwroot/output/project-summary.json` from the `report` stage | `PrintCompletionBanner`, `ReportStage` |
| Dedup input | `logs/registry.json` maps project → { specPath, seedResponsePath, outDir, ts } | `RecordProjectRegistry` |

Stage names the progress feed will emit, in order: `scaffold, emit-models, identity,
emit-controllers-views, seed, verify, report` (the 7 that `run --all` executes), preceded by
`spec-expand` (run before the pipeline loop) and, only in the `--notes` flow, `spec-build`. The
mockup's "stage N of 9" is therefore **approximate by design** (7–9 depending on flow) — which the
spec explicitly accepts.

## IPC channel surface (the typed contract)

The preload exposes exactly these, and nothing more. Names are the contract the renderer and main
both implement.

**Renderer → Main (request/response, `ipcRenderer.invoke`):**
- `spec:list` → available specs (fixtures/, prompts/out/schemas/, cwd)
- `spec:upload(fileBytes, name)` → copies into place, returns a handle
- `seed:list` → available seeds (seeds/, prompts/out/seeds/)
- `seed:upload(fileBytes, name)`
- `seed:generateWithClaude(specHandle)` → **main-process** Anthropic call, writes `prompts/in/seed-response.json`
- `build:checkDuplicate(specHandle, seedHandle)` → prior completed build for this (spec+seed) hash, or null
- `build:start(specHandle, seedHandle, outDir)` → spawns the CLI, returns a `runId`
- `build:pause(runId)` / `build:resume(runId)` / `build:stop(runId)`
- `settings:get()` / `settings:set(patch)` → theme, language, genesis-command, api-key
  (**write-only** from the renderer's view — the key is never returned)
- `footer:get()` → contents of `design/footer.md`, read at runtime
- `report:get(runId)` → parsed `project-summary.json` + parsed completion banner

**Main → Renderer (push events, `webContents.send`):**
- `build:progress` → `{ stage, phase, index, total }` (index = completed-stage count, see below)
- `build:log` → a raw stdout/stderr line (feeds the xterm.js debug terminal)
- `build:awaiting-llm` → `{ stage, requestPath }` (exit 3 detected)
- `build:done` → `{ url, credentials, dbName, startCommand, outDir, summaryPath }`
- `build:error` → `{ stage, message }`

**Window controls** (because the window is frameless — see Theming): `window:minimize`,
`window:maximize`, `window:close`.

**Shell** (main-process `shell.*`, for the report-screen actions): `shell:openExternal(url)`
launches the generated app's URL in the OS browser; `shell:openPath(dir)` opens an output
folder. Both run in main — the renderer never touches `shell` directly.

**Run app** (`shell:runApp(dir)`): spawns `dotnet run --project <dir>` as argv, `shell: false`
— never a shell command string, so it can't hit the PowerShell-5.1 `&&`-parses-as-invalid
class of bug the CLI's own completion banner used to (changelog 083). Detached + unref'd so
the dev server outlives the GUI.

**Kill app** (`shell:killApp(dir)`): kills the process most recently launched by `shell:runApp`
for that project dir (tracked in a small main-process side-map keyed by dir, since `runApp`
detaches/unrefs it and it isn't part of the tracked pipeline `RunState`). Distinct from
`build:stop`, which cancels the CLI pipeline itself, not the generated app.

## Run-control state machine

```mermaid
stateDiagram-v2
  [*] --> Idle
  Idle --> Running: Start (spawn run --all)
  Running --> Paused: Pause (kill child after/at checkpoint)
  Paused --> Running: Resume (respawn run --all; skips done stages)
  Running --> AwaitingLLM: exit 3
  AwaitingLLM --> Running: seed/response filled + --resume
  Running --> Done: natural completion (banner + report)
  Running --> Error: stage error (non-zero, ≠3)
  Running --> Idle: Stop (kill; state.json kept)
  Paused --> Idle: Stop
  Error --> Running: Resume (idempotent re-run)
  Done --> [*]
```

**Why kill-to-pause is correct, not crude:** every `IStage.RunAsync` is contractually idempotent
and `MarkDone` only fires *after* a stage completes. So killing the child mid-stage leaves that
stage un-done in `state.json`; the next `run --all` re-runs only that stage (and its own internal
idempotency detects any partial work). Combined with checkpoints, this gives correct pause/resume
with **zero** mid-stage-freeze machinery — exactly decision #6 in the 6.5.0 spec.

**Pause waits for the checkpoint (resolved).** On `Pause`, main lets the current stage finish —
waits for its `"end"` line in `dps-nn.log.jsonl` — then kills the child. No wasted re-run. An
immediate kill is kept only as a fallback for a stage that appears hung (a manual "force stop").

**Stop keeps state (resolved).** On `Stop`, main kills the child but **preserves `state.json`**, so
the same (spec, out dir) can be resumed later via a fresh `run --all`. A separate explicit
"discard build" action is what clears the out dir — Stop alone never destroys work.

**One process per Start/Resume.** The GUI spawns a single `run --all` and controls its lifecycle.
It never orchestrates stage-by-stage CLI calls — that would be reimplementing `RunAllAsync` in Node,
which the inviolable rule forbids.

## Progress reconstruction (accurate across pause/resume)

On `Start`/`Resume`, main reads `state.json` first to get the baseline of already-completed stages,
then tails `dps-nn.log.jsonl` and increments on each `"end"` phase. `total` = the number of stages
that *will* run for this invocation (known up front). This keeps the bar correct even when a resume
skips half the pipeline (skipped stages emit no `"end"` line in the JSONL — they only log to stdout —
so the baseline read is what makes resume progress accurate). The debug terminal is fed separately
from raw stdout/stderr and is independent of this reconstruction.

## Claude-seed & LLM flow

The main process owns the API key (from env or local config); it never crosses into the renderer
(decision #4). Two modes, same underlying ADR-005 file contract:

- **Automatic** — key present → injected into the child env → every LLM-needing stage (`spec-build`
  if used, `seed`) self-serves. No exit-3 handling needed. This is the default when a key is set.
- **Manual / paste** — no key → the CLI exits **3** at the first LLM-needing stage, having written a
  request under `prompts/`. Main emits `build:awaiting-llm`; the renderer offers either a paste box
  **or** the **"Claude" button**, which triggers a main-process Anthropic call to fill
  `prompts/in/<exchange>-response.json`; main then respawns with `--resume`.

The "Claude" *seed* button specifically is the same mechanism scoped to the seed exchange: generate
`prompts/in/seed-response.json` from the chosen spec, in the main process, then proceed. Its detailed
design (model choice, prompt, cost) is Opus Task 3 — this doc only fixes *where* it runs (main) and
*what* it writes (the seed file the CLI already reads).

## Dedup (entirely GUI-side, CLI untouched)

The 6.5.0 spec wants "(spec, seed) already built → skip". This lives wholly in the main process and
does **not** modify the headless CLI:

- On `build:checkDuplicate`, main computes `sha256(specBytes) + sha256(seedBytes)` and looks it up in
  a GUI-owned manifest `logs/gui-build-manifest.json` mapping the pair-hash → `{ outDir, ts, status }`.
- A completed match → the renderer offers "already built — open / regenerate anyway".
- On a completed build, main appends the pair-hash to the manifest.

This reuses the *idea* of the CLI's registry without depending on it or extending the CLI's own
formats. (The CLI's `StateStore.specHash` and `logs/registry.json` remain the CLI's business.)

## Theming & i18n seams (structure only; visuals are Task 2)

Task 1 must leave the right seams; it does not choose colours.

- **Frameless window + DOM chrome.** The window is `frame: false`. The title bar and the
  minimize/maximize/close controls are **real renderer DOM** drawn on top, wired to
  `window:minimize/maximize/close`. The painted min/max/close in the theme background art are purely
  **decorative** and are never interactive — a hard constraint from the reference designs.
- **Theme descriptors carry structural overrides, not just colour tokens.** Each theme is a
  descriptor `{ id, backgroundImage, tokens, showLogo, logoVariant, chromeStyle, ... }`. This is
  required because the **Veselin Marinov theme suppresses the ДПС-НН logo** (its art carries its own
  branding) — so a theme can turn *structural* elements on/off, not merely recolour them. The
  renderer's theme model must support per-theme show/hide of named slots (logo, frame ornaments),
  not a flat token map.
- **Runtime footer.** `design/footer.md` is read at runtime via `footer:get` (never bundled), so it
  can change without a rebuild. Empty/placeholder = render nothing, never error.
- **i18n.** String tables per language (EN, BG, DE, RU, Romani) resolved in the renderer; the
  Standard Galactic Alphabet "language" is a **font swap over the English strings**, not a
  translation. All of this is renderer-local; the main process is language-agnostic. The chosen
  language persists through the existing `settings.language` field — **no new IPC channel**.
  **Built in phase 3b** (changelog 093): the six tables + a data-driven flag picker live under
  `gui/src/renderer/i18n/`; flags are **inline SVG**, not PNG (CSP-safe and self-contained — the
  renderer loads no external assets). SGA's glyph font itself is owned by the ENCHANT theme
  (`design/design-specs.md` §176); until it lands, the `.lang-sga` font-swap seam falls back to the
  normal faces. **Hard boundary:** GUI-chrome language ≠ generated-app language — the latter stays
  Bulgarian-only (CLAUDE.md) unless the *spec's* `meta.language` says otherwise (changelog 054).

## Security model

- Renderer: `contextIsolation: true`, `nodeIntegration: false`, `sandbox: true`.
- The API key lives only in the main process (env/local config); `settings:set` accepts it
  write-only, `settings:get` never returns it, and it is passed to the child **only** via env.
- The preload surface is the *entire* attack surface — keep it minimal and typed; no passthrough of
  `fs`/`child_process`/`ipcRenderer`.
- Uploaded spec/seed files are validated (JSON parse +, for specs, the existing `spec.schema.json`)
  in the main process before being written into place.

## Folder layout

```
gui/                      # the Electron app — sibling to src/, never contains pipeline logic
  package.json
  electron/
    main.ts               # spawns CLI, owns fs + key, tails log, dedup, IPC handlers
    preload.ts            # the typed contextBridge surface (the IPC contract above)
    genesis-runner.ts     # child-process lifecycle: start/pause/resume/stop, progress, exit-3
  src/                    # renderer web UI (framework TBD by the impl task)
    screens/  themes/  i18n/  components/
  ...
```

`images/` (logo, flags, wallpapers) and `design/` (`design-specs.md`, `footer.md`) already exist at
the repo root (Task 0) and are read at runtime.

## What this doc fixes vs. what later tasks own

**Fixed here:** process model; the CLI-child contract; cwd/`--no-interactive`/env-key constraints;
the IPC channel names; kill-to-pause + respawn-to-resume via idempotent checkpoints; progress
reconstruction; GUI-side dedup; the theming/i18n/security *seams*.

**Owned by later tasks:** the visual design system + the 5 themes (Task 2, `design/design-specs.md`);
the renderer framework choice and screen implementation; the Claude-seed prompt/model/cost design
(Opus Task 3); per-theme wallpapers and the loading animation. Build order follows the spec's B.0
phasing: **MVP first** — one theme, EN only, spec/seed input + dedup + Start/Stop + progress + debug
terminal + generation screen, generating end-to-end — before themes/languages expand.

## Resolved decisions

1. **The GUI is the primary way to run Genesis; the CLI stays a first-class parallel surface** for
   debugging and fast batch runs (the `1-build-and-check.bat` / `2-generate.bat` workflow). This
   *reinforces* the inviolable rule: the CLI must remain independently usable and testable, because
   the batch/debug workflow depends on it — the GUI is one caller among two, never the sole entry
   point. Concretely: main spawns via a configurable `settings.genesisCommand`, default to the dev
   form `dotnet run --project <root>/src/Genesis.Cli --` (same invocation the batch scripts use),
   overridable to a published `dps-nn` exe later. Nothing about the GUI may assume it is the only
   caller.
2. **Stop keeps `state.json`.** A later `run --all` can resume the same (spec, out dir). Clearing
   the out dir is a separate, explicit "discard build" action — Stop never destroys work.
3. **Pause waits for the current stage's checkpoint** (its `"end"` line) then kills — no wasted
   re-run. Immediate kill is retained only as a manual "force stop" fallback for a hung stage.
