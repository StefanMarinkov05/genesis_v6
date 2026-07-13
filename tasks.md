# Tasks — DPS-NN

Three categories: **older GUI-MVP/phase backlog**, **A4/A5 design→implementation pair**, and
**tonight's short-task batch** (7 items from the 2026-07-12 evening session, changelog 086).
"Now working" tracks live progress within a session so you can estimate remaining runtime.

---

## Now working
_(updated live — check here to see exactly what's in flight and what's next in the queue)_

**Idle.** No task currently in progress.

**Last completed: Category 1 — short-task batch** · **Sonnet, low, thinking off** · **DONE**
(changelog 089). 5 items (item 4 dropped as stale — its target file was deleted in A5). Two real
bugs found and fixed: (a) `logs/<Project>/` was never gitignored — a real gap, since it includes
plaintext seed-user passwords; (b) the Admin Products table's Brand/Category FK links pointed at a
dead `Details` route (404) after changelog 086 removed that action — now falls back to `Edit`.
Also generalized the "no Details for single-property models" rule to the generator level (not just
Brand/Category by hand), catching auto-injected `CatalogueItem` too. GUI: Run-app folder-exists
guard + Kill-app PID persistence across GUI restarts (file-backed, not just in-memory). Verified via
a real regenerate + 116/116 tests (2 new).

**Before that: Group A5 — Спецификации consolidation implementation** · **Sonnet** · **DONE**
(changelog 088). Brand/Category CRUD folded into one page alongside read-only isLookup tabs +
a Feedbacks tab, generic auto-derived filters, Характеристики fully retired. Verified end-to-end
across TWO real regenerates (first surfaced a real spec-validation bug, fixed and reconfirmed).
Two scope cuts documented below — Client/Product/Details filter and scaffolded-controller (non-
Спецификации) filter surgery were NOT done this pass.

<details><summary>Full 9-step log (following the design doc's 5-step sequence, rename LAST)</summary>

1. [x] `isSpecification` flag: schema + POCO (`ModelSpec`/`ControllerSpec`) + `InjectAdminSupervisorSurface`
       plumbing; marked Brand/Category isSpecification in both fixtures (model + controller entries)
2. [x] `SpecificationsController` + `specifications_index.sbn` — built as the FULL final page in one
       pass (not staged alongside old Характеристики — single session, no multi-day continuity risk):
       CRUD tabs (Brand/Category, reusing scaffolded sub-controllers with returnUrl), read-only tabs
       (isLookup models), Feedbacks tab (Review, gated on features.rating)
3. [x] Generic filter convention implemented DIRECTLY inside SpecificationsController/Index (not via
       scaffolded-controller surgery — deliberate scope narrowing, see notes below): auto-derived from
       manyToOne relations + UserId-suffixed props + Stars; tab persistence via `?tab=` + hidden-field
       echo of Context.Request.Query so switching one tab's filter never drops another tab's state
4. [x] Applied to isLookup tabs (CatalogueItem/CartItem/OrderItem/PostComment/PostVote) + Feedbacks —
       these ARE "Admin catalogues/cart-items/orders/comments/votes" (the isLookup model set). Client/
       Product/Details filter NOT done this pass (scope cut, see notes)
5. [x] Feedbacks tab done as part of step 2/3 above (product/user/rating selects)
6. [x] Old Характеристики retired: templates deleted, emission swapped, nav updated to Спецификации
       (isLookup OR isSpecification OR rating-feedback gates the single nav entry)
7. [x] Build + `dotnet test` green — 114/114
8. [x] Full clean `run --all` regenerate — DONE, twice. First run surfaced a real bug (below);
       second run (post-fix) verify PASSED: build/db/boot/http/manifest/language, 6m48s total
9. [x] Changelog 088 + tasks.md updated

**Real bug found by verification, not guessed:** `SpecStages.SemanticCheck` requires every
`redirects[].to` target to reference a DECLARED controller — `SpecificationsController` was purely
template-emitted, never declared in `spec.controllers`, so the FIRST real regenerate's spec
validation failed (56 cascading test failures too, since `Arrange()` loads the fixture). Fixed:
added a `SpecificationsController` entry (`generatedBy: "writtenFromScratch"`, confirmed a no-op in
the generic emission switch, no double-emission risk) to both fixtures; excluded it from
`layout.sbn`'s generic admin-nav loop (`c.name != "SpecificationsController"`) to avoid a duplicate
nav entry. Also added `redirects` (Create/Edit/DeleteConfirmed → Specifications.Index) to
Brand/Category so their reused scaffolded CRUD lands the user back on Спецификации instead of the
orphaned Brands/Index — verified against a REAL scaffolded BrandsController.cs shape (not synthetic)
via `Real_ApplyDeviations_redirects_brand_crud_back_to_specifications_index`, THEN confirmed in the
second full regenerate's actual output (`RedirectToAction("Index", "Specifications")` × 3 in both
BrandsController.cs and CategoriesController.cs, exactly one Спецификации nav link, no duplicates).

**Scope notes (read before assuming 100% of the design doc is done):**
- Did NOT implement the scaffolded-controller filter surgery (adding filters to Products/Orders/etc.'s
  OWN admin_table.sbn pages via a new "filtering" deviation type, mirroring the sorting deviation).
  That's real scaffolded-code regex risk the design flagged as the hardest part. Filters landed instead
  on the read-only isLookup+Feedback tabs I fully control (zero scaffold risk) — interpreted as
  satisfying "apply filters to catalogues/cart-items/orders/comments/votes" since those ARE the
  isLookup models, displayed nowhere else meaningfully in the admin.
- Client/Product/Details review filter (explicitly named in the design) NOT done — real remaining gap.
- No shared `_admin_filter_bar.sbn` partial extracted — filter markup is inline per-tab in
  specifications_index.sbn (3 near-identical blocks: isLookup relation+user, isLookup user-only,
  feedback product+user+rating). Works, but the "generic reusable partial" the design named is not
  literally a separate file — deliberate simplification given effort budget, revisit if reused again.

Full spec: `docs/explanation/specifications-consolidation.md` (+ ADR-012).
</details>

---

## Category 1 — Short-task queue · **DONE** (changelog 089)

1. [x] **GUI: disable "Run app"/"Kill app" after project folder is gone** — `runGeneratedApp`
   throws on a missing folder; renderer only flips Kill-app-enabled state on confirmed success.
2. [x] **CLI: `seed-credentials.txt` gitignore coverage** — REAL GAP found (not a no-op):
   `logs/<Project>/` was never covered by any rule. Added `logs/*/`.
3. [x] **Website: generic single-property "no Details" rule** — found `CatalogueItem` missed by
   the hand-authored Brand/Category-only fix. Moved the rule into
   `InjectAdminSupervisorSurface` itself (generic, permanent, covers future cases too).
4. ~~Website: admin FK header sweep for `characteristics_index.sbn`~~ — dropped, stale
   (file deleted when A5 retired Характеристики).
5. [x] **GUI: "Kill app" PID survives a GUI restart** — replaced the in-memory Map with a
   file-backed `logs/launched-apps.json` store. Verified via a Node restart simulation.
6. [x] **Website: verify no dangling `Details` links** — found a REAL one: Admin Products'
   Brand/Category FK link cells pointed at the now-removed `Details` action (404). Fixed:
   `admin_table.sbn` falls back to `Edit` when the target's `Details` was removed.

Verified via a real `run --all` regenerate (verify PASSED) + 116/116 tests (2 new).

_Pick any subset — they don't depend on each other. I'd suggest 1 → 4 → 3 → 6 → 2 → 5 in that
order (rising cost, #2 and #5 are more "confirm or shrug" than "build")._

---

## Category 2 — A4 / A5 (design → implementation pair)

Both had full Opus design docs + ADRs (changelog 085). **Both A4 and A5 are now IMPLEMENTED +
verified** (changelogs 087, 088). This category is fully done.

**Group A4 — Image pipeline implementation** · **DONE** (changelog 087)
- [x] Persistent `.image-cache/`, shipped SVG fallbacks copied to wwwroot, local-only PFP, and
      product-default fallbacks — all verified against a real `run --all` (verify PASSED, cache
      copy-from proven byte-identical, 112/112 tests).

**Group A5 — Спецификации consolidation implementation** · **DONE** (changelog 088)
- [x] `isSpecification` flag, `SpecificationsController` + `specifications_index.sbn` (CRUD +
      read-only + Feedback tabs), generic auto-derived filters, Характеристики retired. Verified
      across two real regenerates (first caught a real spec-validation bug). See the collapsed
      log above under "Now working" for the full 9-step account + honest scope-cut notes
      (Client/Product/Details filter and scaffolded non-Спецификации filter surgery not done).

---

## Category 3 — Older backlog (GUI MVP phases, from before tonight)

**Group A2 — Identity/Manage English leaks** · **Sonnet** · effort: high · thinking: on
- [ ] Validation messages, watermarks, link buttons still English on `Identity/Account/Manage`
      (confirmed tonight: the Manage pages DO exist in real generated output — ChangePassword,
      Email, Index, ManageNavPages, etc. — this is a real multi-file job, not short). This exact
      area broke twice before (BG residue-map identifier corruption, dual `_ViewImports` layers)
      — `docs/explanation/techniques.md` has the traps. Do NOT attempt as a short task.

**Group (new) — `images.mode: "single"` UI is unexercised** · effort: medium · thinking: on
- [ ] Found while designing the spec-authoring Claude Skill (2026-07-13): `ImageService`'s
      `Save{Model}ImageAsync` correctly implements single-mode semantics server-side (delete
      existing before saving new), but `admin_product_images.sbn` — the ONLY admin image-mgmt
      view, emitted for every image-bearing model regardless of `mode` — is hardcoded to a
      multi-file-upload gallery UI even for `mode: "single"` models. The client-facing carousel
      (`product_details.sbn`) is similarly hardcoded to slideshow style, never branching on mode.
      In practice only `Product` uses the `images` block today, always `mode: "multiple"` — so
      `"single"` mode is exactly as untested end-to-end as DTOs (implemented, never exercised).
      Fix: branch `admin_product_images.sbn` on `model.images.mode` — single mode should show a
      simple "current photo, replace it" UI (single file input, no `multiple` attr, no gallery
      grid), not the multi-upload gallery. Branch the client display similarly (single `<img>`,
      no carousel chrome) when only one image can ever exist. Verify with a real regenerate using
      a test model pinned to `mode: "single"`.

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
- [ ] The other 4 themes (IMPERATOR/VEIL/FORGE/ENCHANT) as descriptor fills
- [ ] The 5 additional languages (BG/DE/RU/Romani/SGA) + flag picker
- [ ] Per-project wallpapers + loading animation on the generation screen
- [ ] Recent-projects sheet off `logs/registry.json`

---

## Done tonight (2026-07-12 evening, changelog 086)
GUI: Start-command display truncation, 1-admin/1-non-admin credential scoping, "Last result"
reopen control, "Kill app" button + PID tracking. Website: Cyrillic `userRoleName` fix (was the
literal typo "Potrebitel"), seed-credentials.txt file, Brand/Category Details removal, Admin FK
column headers in Bulgarian. All verified against a real `run --all` regenerate + 112/112 tests.
