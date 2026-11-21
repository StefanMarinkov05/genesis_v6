# The Specifications page — one page to rule the small models

Condensed from `docs/explanation/specifications-consolidation.md` (the design record behind
ADR-012). In the generated apps' UI the page carries its Bulgarian label; here we call it
Specifications.

People ask me, "Sir, why does the admin menu have so few entries? Where did everything go?" It went
to one page. One beautiful page with tabs. And believe me, the admins love it. They come up to me,
they say, "Sir, I can find things now." That's what good design sounds like.

So picture this, folks. An admin menu. Brand. Category. CartItem. OrderItem. PostComment. PostVote.
CatalogueItem. On and on, entry after entry, all of them small, all of them looking the same. A
dropdown like a phone book. Nobody wants that. Sad! So we built one page. One beautiful page.

## Where it started

Before the consolidation, the admin surface had two ways of handling small models, and no filtering
at all:

- **Join / lookup models** — `CartItem`, `OrderItem`, `PostComment`, `PostVote`, `CatalogueItem` —
  carry `isLookup: true` in their feature fragments. They had already been gathered onto one
  read-only tabbed page (changelog 069), hidden from the nav, though their scaffolded admin CRUD
  controllers stayed reachable directly.
- **Atomic reference models** — `Brand`, `Category`: no outgoing relations, top-level reference
  data, each with its own full-CRUD admin controller *and its own nav entry*. Exactly the clutter
  the read-only page was invented to remove — but they needed real CRUD, so they were never folded
  in.
- **Reviews** (the rating feature) — product-scoped, with an author and 1–5 stars, shown on the
  product details page, with no admin page and no filtering anywhere.

## The model taxonomy: a second flag

| Flag | Meaning | Tab on Specifications |
|---|---|---|
| `isSpecification: true` | atomic reference model, no outgoing FK (Brand, Category) | **full CRUD** tab |
| `isLookup: true` | join/junction model (CartItem, …) | **read-only** tab, with filters |
| Review (rating feature) | feedback | **read-only** tab, with filters |

`isSpecification` is a bool on both the model and the controller spec, set in the spec on Brand and
Category. It plumbs through the admin-supervisor injection onto the auto-generated admin controller
entry, exactly the way `isLookup` already did. Apply it by the rule, not by taste — the rule is in
[Chapter 5](05-paper-to-spec.md).

## The controller architecture: an index-only page plus CRUD sub-controllers

- **`SpecificationsController`** (Admin area) has a single `Index()` that builds one combined view
  model — every `isSpecification` list for the CRUD tabs, every `isLookup` list for the read-only
  tabs, and the reviews list — and renders the tabbed page. It is the evolved form of the old
  read-only page's controller.
- **The scaffolded CRUD controllers are reused** as the write side. `BrandsController` and
  `CategoriesController` already have Create/Edit/Delete with FK dropdowns, soft delete and the
  Bulgarian residue handling — so they are **not** re-authored. They're marked `isSpecification`
  (hidden from the nav), and the CRUD tab's Create / Edit / Delete buttons point at their actions
  with a `returnUrl` back to the Specifications page, so the admin lands right back where they
  started.

The consolidated page owns the *index*; the scaffolded controllers own the *mutations*; the nav
shows only the consolidated page. A great division of labour. Everybody does what they're best at.

## The filters: derived, not hand-listed

The read-only tabs filter themselves, and the filters are **auto-derived** from the model — nobody
hand-writes a filter list per tab:

- every `manyToOne` relation → a `<select>` of the target's records;
- an owner field (`UserId`, or a review's author) → a user `<select>`;
- on the reviews tab, the rating → a 1–5 stars `<select>`, next to product and user.

The filter values travel as query-string parameters, and the page applies one `Where` per supplied
value. The integration trap the design called out, and it's a nasty one: active filters must be
carried through every other link on the page, or a click on a different control silently clears
them. Silently! The worst kind. Watch for it.

## The order of the work — the rename came LAST

Renaming the old read-only page first would have broken references halfway through the refactor.
So the sequence was:

1. add `isSpecification` (schema + POCO + injection plumbing); mark Brand/Category; hide them from
   the nav;
2. build the new controller and page **alongside** the old one, CRUD tabs first;
3. build the auto-derived filters;
4. add the reviews tab and its filters;
5. **last:** fold the read-only join tabs into the new page, rename the controller, view, nav label
   and identifiers, and delete the old page.

Build the new house next to the old one. Move in. *Then* knock the old one down. Common sense.

## Traps the implementer had to respect

- **`||` returns a boolean in Scriban**, not the left operand: `label = c.display_name || c.name`
  silently prints `"true"`. Bit the project twice (changelogs 057 and 069). Use an explicit
  `if/else`. A very deceptive operator. Looks friendly. It's not your friend.
- **The Bulgarian residue map corrupts C# identifiers** that contain words like `Email` or
  `Password` (changelog 070) — see [Chapter 21](21-techniques.md).
- **The `Create\([^)]*\)` regex can't backtrack past `[Bind("…")]`'s parenthesis** (changelog
  064) — one more reason the design reuses the scaffolded controllers instead of operating on them.
- **The FK target-controller lookup** must reuse the admin table's real-controller resolution —
  never naive pluralisation (`Category` → `Categories`, not `Categorys`).

## Spec and seed impact

A new `isSpecification` bool on `ModelSpec` and `ControllerSpec` (schema + POCO), set on Brand and
Category in the fixture. No change to the seed shape. The filters need no new spec fields — they
are derived.

No new fields, no new seed shape, one page instead of fourteen menu entries. That's efficiency,
folks. That's a winner.
