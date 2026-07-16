# Tasks — DPS-NN

Live task queue. Full history of completed work lives in `changelogs/*` + `plan/state.md` — this
file only tracks what's still open.

---

## Now working

**Idle.** No task currently in progress. Last completed: Category 3 batch (changelog 097) —
`images.mode:"single"` UI gap + Group A2 (Identity/Manage English leaks), both DONE and verified
via real regenerates. See "Done" below for summary; full detail in changelog 097.

---

## Open — Category 3 backlog

**Group (new) — outgoing Identity email body/subject still English** · effort: low · thinking: on
- [ ] Found while verifying A2 (changelog 097): `Email.cshtml.cs`'s `OnPostChangeEmailAsync`/
      `OnPostSendVerificationEmailAsync` send real outgoing emails via `_emailSender.SendEmailAsync`
      with English subject ("Confirm your email") and body ("Please confirm your account by
      clicking here.") — genuinely different from the on-screen Manage pages (mail content, not a
      rendered browser page), so deliberately NOT folded into A2's fix. Small, contained: 2 subject
      strings + 2 body strings in one file, same "exact known literal" replacement discipline as
      `InjectBgStatusMessages` already uses. Confirm scope with Stefan before starting (is a
      Bulgarian-language project expected to send Bulgarian confirmation emails? — very likely
      yes, but wasn't explicitly asked for).

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

- Category 3 batch: `images.mode:"single"` UI gap + Group A2 (Identity/Manage English leaks) —
  changelog 097. Also fixed a real pre-existing bug found along the way: a too-narrow lookahead
  window in `InjectBgValidationMessages` produced a blank display name in `Register.cshtml.cs`
  (shipping bug, not introduced this session) and would have in `ChangePassword.cshtml.cs` too.
- Category 1 short-task batch (5 items) — changelog 089
- Group A4 — image pipeline implementation — changelog 087
- Group A5 — Спецификации consolidation implementation — changelog 088
- product_details.sbn zero-star row fix — changelog 091
- Card grid bulk average rating — changelog 092
- 2026-07-12 evening batch (GUI credential scoping, Kill-app, Cyrillic role fix, etc.) — changelog 086
