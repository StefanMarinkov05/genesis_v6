# Tasks — GUI MVP (spec B.0 phase 1: NOVA + EN)

Electron shell built (changelog 073), Task A docs done (074/075), `npm run dev` launch bugs fixed
and verified for real (076 — a real window opened, `MainWindowTitle: DPS-NN — Genesis` confirmed
via `Get-Process`). Two portable seed/spec-authoring Claude Skills written (077,
`utils/claude-skills/`) supporting the manual seeding workflow below.

## Remaining before phase 2 (Stefan's manual gate)
[ ] Eyeball `npm run dev` yourself — confirm the NOVA theme actually renders correctly and the
    window controls (min/max/close, debug toggle, settings) behave as designed
[ ] Run a real end-to-end generation from the GUI (needs LocalDB + a full pipeline run): pick a
    spec + seed → Start → watch progress/terminal → completion readout with URL/creds/DB
[ ] Fix anything the live run surfaces (esp. progress-tail timing, banner parse on real output)

## Seeding workflow (changed — now manual)
Stefan generates seed data by pasting/attaching the spec into a Claude chat using the
`dps-nn-seed-authoring` skill, then uploads the result via the GUI's "Upload seed" control or
drops it under `seeds/` for the CLI picker. The in-app "Claude" seed button (main-process
Anthropic API call) is deprioritized, not cancelled — revisit only if the manual workflow proves
too much friction.

## Phase 2 (after the manual gate above is confirmed)
[ ] Pause/Resume wired to the UI (main-side logic already present, UI-disabled)

## Phase 3
[ ] The other 4 themes (IMPERATOR/VEIL/FORGE/ENCHANT) as descriptor fills
[ ] The 5 additional languages (BG/DE/RU/Romani/SGA) + flag picker
[ ] Per-project wallpapers + loading animation on the generation screen
[ ] Recent-projects sheet off `logs/registry.json`
