# Tasks — GUI MVP build (spec B.0 phase 1: NOVA + EN)

Built and committed the Electron shell over the Genesis CLI (changelog 073). Contract- and
build-verified; the live-render + end-to-end generation are Stefan's manual `npm run dev` gate.

## Done this session
[x] Scaffold `gui/` (electron-vite + vanilla TS; electron, xterm, addon-fit)
[x] Shared typed IPC contract (`gui/src/shared/ipc-types.ts`)
[x] Main process: paths, settings (write-only key), dedup, footer, report, files, genesis-runner, ipc, frameless window
[x] Preload contextBridge surface (+ `shell:openExternal/openPath`)
[x] Renderer: NOVA styles ported, EN i18n, screens wired to IPC, xterm debug terminal, starfield
[x] Verify: typecheck clean · build 3 targets · banner-parser tests 3/3 · real CLI flags confirmed

## Remaining before phase 2 (Stefan's manual gate)
[ ] `cd gui && npm run dev` — confirm the window renders (NOVA) and controls behave
[ ] Run a real end-to-end generation from the GUI (needs LocalDB + a full pipeline run):
    pick a spec + seed → Start → watch progress/terminal → completion readout with URL/creds/DB
[ ] Fix anything the live run surfaces (esp. progress-tail timing, banner parse on real output)

## Phase 2 (after MVP is confirmed live)
[ ] Pause/Resume wired to the UI (main-side logic already present, UI-disabled)
[ ] "Claude" seed button — main-process Anthropic call (Opus Task 3 mini-design first)

## Phase 3
[ ] The other 4 themes (IMPERATOR/VEIL/FORGE/ENCHANT) as descriptor fills
[ ] The 5 additional languages (BG/DE/RU/Romani/SGA) + flag picker
[ ] Per-project wallpapers + loading animation on the generation screen
[ ] Recent-projects sheet off `logs/registry.json`

## Task A — documentation (independent of GUI)
Design done (Opus): `plan/task-a-doc-architecture.md` — target tree, Diataxis cut, per-doc
contract, reconciliation decisions D1–D4, target CLAUDE.md routing. Stubs created for all 9 new
docs. Remaining = the Sonnet fills (one commit each: fill doc → flip its CLAUDE.md routing row →
stamp `last-verified`), in this order:
[x] `docs/reference/control-flow.md` (Sonnet med) — Mermaid + stage table; `docs/architecture.md` deleted, CLAUDE.md rows flipped
[x] `docs/explanation/architecture-decisions.md` (Sonnet med) — thin synthesis indexing `docs/adr/`, CLAUDE.md row flipped
[x] `docs/reference/spec-format.md` (Sonnet low) — concepts companion to `spec.schema.json`, CLAUDE.md row flipped
[x] `docs/reference/file-map.md` (Sonnet med) — generator (`scripts/generate-file-map.ps1`) + per-file header pass, CLAUDE.md row added
[x] `docs/explanation/techniques.md` (Sonnet high) — Scriban helpers, @@media escaping, the 4-theory
    ef-migrations saga, BG residue-map trap, dual _ViewImports layers, stage-ordering trap; flagged
    (not fixed) a real ADR-004 deviation found in `ResolveDesignPreset` (uses GetHashCode, not SHA256)
[x] `docs/FAILED-ATTEMPTS.md` (D4) — searched decisions.md for structural dead-ends, found none
    (all rejections already live in ADR "Alternatives Considered" sections); dropped the stub
[x] `docs/how-to/add-a-stage.md`, `docs/how-to/add-a-template.md` (Sonnet med), CLAUDE.md row added
[x] `README.md` overview Mermaid + fix known drift (archive "moved"→"copied" fixed, ADR count fixed,
    sln quickstart no longer recreates a `.sln`, doc map updated); doc-map row for the new tree added
[x] `docs/01-tutorial-genesis.md` (Sonnet low) — end-to-end walkthrough, CLAUDE.md row flipped
[x] `Genesis.sln.notes.md` deleted — it instructed recreating a `.sln`, which CLAUDE.md now forbids
    (`dps-nn.slnx` is checked in); actively wrong, not just stale
