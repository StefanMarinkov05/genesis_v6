# Tasks — GUI MVP (spec B.0 phase 1: NOVA + EN) + FaceNote polish batch

Electron shell built (073), Task A docs done (074/075), `npm run dev` launch bugs fixed (076),
two portable seed/spec-authoring Claude Skills written (077, `utils/claude-skills/`), stale
archive cleanup + GUI file sizes (079), scaffold MSB1011 collision diagnosed + clean `out/` wipe
(080), FaceNote order validation / status display / images hint / follow button (081), GUI G1
split copy buttons + close buttons (082).

## Remaining, grouped by model (per the standing task-batch convention)

**Group G2 — Shell diagnosis, then run button** · split
- [ ] **Opus** · effort: medium · thinking: on — diagnose why `дпс-нн`/`&&` breaks in Stefan's
      terminal (confirm shell first: PowerShell 5.1 vs 7+ vs cmd.exe)
- [ ] **Sonnet** · effort: low · thinking: off — add a "Run app" button to the GUI's completion
      screen once diagnosed

**Group A2 — Identity/Manage English leaks** · **Sonnet** · effort: high · thinking: on
- [ ] Validation messages, watermarks, link buttons still English on `Identity/Account/Manage`
      (this exact area broke twice before — `docs/explanation/techniques.md` has the traps)

**Group A4 — Image pipeline (design first)** · split
- [ ] **Opus** · effort: high · thinking: on — design: generic PFP/product fallback, download-once
      caching, local `<img>` serving instead of raw URLs, spec/seed impact
- [ ] **Sonnet** · effort: medium · thinking: on — implement once designed

**Group A5 — Спецификации consolidation (design first, new convention)** · split
- [ ] **Opus** · effort: high · thinking: on — design the lookup-model-consolidation rule
      (ADR-worthy), the Feedbacks+filter pattern
- [ ] **Sonnet** · effort: medium-high · thinking: on — implement in sequence: consolidation →
      filters → apply elsewhere → rename last

## Manual gate (Stefan, whenever convenient — not blocking the above)
[ ] Eyeball `npm run dev` — confirm the NOVA theme renders correctly and window controls behave
[ ] Run a real end-to-end generation from the GUI (LocalDB + full pipeline run)
[ ] Fix anything that run surfaces (progress-tail timing, banner parse on real output)

## Seeding workflow (changed — now manual)
Stefan generates seed data by pasting/attaching the spec into a Claude chat using the
`dps-nn-seed-authoring` skill, then uploads the result via the GUI's "Upload seed" control or
drops it under `seeds/` for the CLI picker. The in-app "Claude" seed button is deprioritized, not
cancelled.

## Phase 2 (after the manual gate is confirmed)
[ ] Pause/Resume wired to the UI (main-side logic already present, UI-disabled)

## Phase 3
[ ] The other 4 themes (IMPERATOR/VEIL/FORGE/ENCHANT) as descriptor fills
[ ] The 5 additional languages (BG/DE/RU/Romani/SGA) + flag picker
[ ] Per-project wallpapers + loading animation on the generation screen
[ ] Recent-projects sheet off `logs/registry.json`
