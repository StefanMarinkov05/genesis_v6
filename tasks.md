# Tasks — DPS-NN

Live task queue. Full history of completed work lives in `changelogs/*` + `plan/state.md` — this
file only tracks what's still open.

---

## Now working

**Idle.** No task currently in progress. Also completed since the last cleanup: changelog 091
(product_details.sbn always shows the star row) + changelog 092 (card grid bulk average rating).

---

## Open — Category 3 backlog

**Group A2 — Identity/Manage English leaks** · **Sonnet** · effort: high · thinking: on
- [ ] Validation messages, watermarks, link buttons still English on `Identity/Account/Manage`
      (confirmed: the Manage pages DO exist in real generated output — ChangePassword, Email,
      Index, ManageNavPages, etc.). This area broke twice before (BG residue-map identifier
      corruption, dual `_ViewImports` layers) — `docs/explanation/techniques.md` has the traps.
      Do NOT attempt as a short task; do NOT reuse the blind residue-map approach on this subtree.

**Group (new) — `images.mode: "single"` UI is unexercised** · effort: medium · thinking: on
- [ ] `ImageService`'s `Save{Model}ImageAsync` correctly implements single-mode semantics
      server-side (delete existing before saving new), but `admin_product_images.sbn` — the only
      admin image-mgmt view, emitted for every image-bearing model regardless of `mode` — is
      hardcoded to a multi-file-upload gallery UI even for `mode: "single"` models. The
      client-facing carousel (`product_details.sbn`) is similarly hardcoded to slideshow style,
      never branching on mode. In practice only `Product` uses the `images` block today, always
      `mode: "multiple"` — so `"single"` mode is exactly as untested end-to-end as DTOs. Fix:
      branch `admin_product_images.sbn` on `model.images.mode` (single mode → simple "current
      photo, replace it" UI, no gallery grid) and branch the client display similarly. Verify with
      a real regenerate using a test model pinned to `mode: "single"`.

**Manual gate (Stefan, whenever convenient — not blocking anything else)**
- [ ] Eyeball `npm run dev` — confirm the NOVA theme renders correctly and window controls behave
- [ ] Run a real end-to-end generation from the GUI (LocalDB + full pipeline run)
- [ ] Fix anything that run surfaces (progress-tail timing, banner parse on real output)

**Seeding workflow (changed — now manual, not a task, just context)**
Stefan generates seed data by pasting/attaching the spec into a Claude chat using the
`dps-nn-seed-authoring` skill, then uploads the result via the GUI's "Upload seed" control or
drops it under `seeds/` for the CLI picker. The in-app "Claude" seed button is deprioritized, not
cancelled.

**Phase 2 (after the manual gate is confirmed)**
- [ ] Pause/Resume wired to the UI (main-side logic already present, UI-disabled)

**Phase 3**
- [ ] The other 4 themes (IMPERATOR/VEIL/FORGE/ENCHANT) as descriptor fills · **Sonnet** ·
      (ENCHANT owns the actual SGA glyph font — the `.lang-sga` swap seam is already wired,
      changelog 093, and just needs the font asset)
- [x] The 5 additional languages (BG/DE/RU/Romani/SGA) + flag picker — **DONE** (changelog 093,
      Opus). GUI-chrome only; SGA is a font-swap seam; flags are inline SVG. Romani is best-effort,
      flagged for a native-speaker review. NOT yet eyeballed on screen (manual `npm run dev` gate).
- [ ] Per-project wallpapers + loading animation on the generation screen · **Sonnet**
- [ ] Recent-projects sheet off `logs/registry.json` · **Sonnet**

---

## Done (see changelogs/ + plan/state.md for full detail)

- Category 1 short-task batch (5 items) — changelog 089
- Group A4 — image pipeline implementation — changelog 087
- Group A5 — Спецификации consolidation implementation — changelog 088
- product_details.sbn zero-star row fix — changelog 091
- Card grid bulk average rating — changelog 092
- 2026-07-12 evening batch (GUI credential scoping, Kill-app, Cyrillic role fix, etc.) — changelog 086
