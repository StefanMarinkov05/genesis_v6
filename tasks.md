# Tasks — Opus Task 1: GUI app architecture (Electron shell + Genesis CLI child process)

[x] 1. Read the REAL CLI/pipeline integration surface (Program.cs + Pipeline.cs) — command list,
    JSONL progress schema, StateStore checkpoints, exit-code 3, registry, --no-interactive, cwd rule
[x] 2. Record design-image direction + hard constraints (VM theme → no DPS logo; art chrome is
    decorative → functional chrome is real DOM) — folded into the doc's theming-seams section
[x] 3. Design the Electron architecture: process model, main/renderer/preload split, IPC channel
    contract, kill-to-pause + respawn-to-resume via idempotent checkpoints, progress reconstruction,
    Claude-seed key-in-main flow, GUI-side dedup, frameless-window chrome, security model
[x] 4. Wrote docs/explanation/gui-architecture.md (full design, Mermaid process + state diagrams)
    + ADR-010 pointer + CLAUDE.md routing entry
[>] 5. Commit; report + flag the 3 open decisions, stop before implementation (this is a design task)
