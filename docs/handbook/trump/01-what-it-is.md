# The problem, and the promise — a very big promise

**Genesis** generates complete ASP.NET Core 10 MVC applications from two JSON files. Complete. Not
a skeleton. I hate skeletons, folks — a skeleton is an app that gave up. What comes out of Genesis
has everything:

- **accounts**: registration, login, logout, password change, email change, profile management;
- **authorization**: roles, a role-scoped admin area and client area, per-record ownership rules, and
  a server-side guard that stops an administrator deleting themselves — and believe me, people try;
- **content**: models with validation and Bulgarian labels — posts with comments and votes,
  products, categories, catalogues, reviews, follows, and whatever else the spec declares;
- **commerce**, when enabled: a shopping cart, checkout, orders with items, and order history;
- **media**: multi-image galleries with upload, replacement, deletion, a local cache of downloaded
  images, and shipped fallbacks so a failed download never breaks a page;
- **pages**: a landing page, a static contact page, card grids with search and sorting, paginated
  admin tables, and the consolidated Specifications page for small lookup models;
- **presentation**: responsive Bootstrap layout, a per-project theme and design preset;
- **data**: a seeded database that makes every screen look used on first boot — owners, prices,
  dates, comments, orders, images.

And people ask me, they come up and they say, "Sir, how complete is complete?" I tell them: it builds,
it migrates, it boots, it logs you in, it speaks Bulgarian. All of it. Every time. Nobody knows
complete like Genesis knows complete, believe me.

And then — this is the part people love — it *checks* it. It builds the app, migrates its database,
boots it, logs in through the real form, fetches the routes the spec declares, and scans the pages
it fetched for leaked English. Other generators? They generate and they pray. We verify.

## The problem it was built for

A diploma or a course project is judged on *coherence*. The author has to explain every table, every
screen, every authorization rule — and the thing has to actually run on the examiner's machine. Not
on the student's machine. The examiner's. Very tough examiners, some of them. Building that by
hand takes weeks, and the result usually has gaps: one forgotten `[Authorize]`, one English label in
a Bulgarian UI, an empty database that makes the app look broken, a login page that fails on a
fresh clone. Terrible. We've all seen it. Embarrassing.

Genesis turns that into a **declarative** input plus a **mechanical** guarantee:

- You describe the app: models, properties, their Bulgarian labels, validations, roles, areas,
  controllers, pages, features, and how many seed rows each model gets.
- You describe the data: realistic rows, cross-referenced symbolically.
- The generator produces the app, then **checks** it: builds it, migrates its database, boots it,
  logs in through the real form, fetches the routes the spec declares, and scans the pages it
  fetched for leaked English.

The output is not a scaffold waiting for you to finish it. It's an ordinary MVC app — the kind a
competent student would write, a *very* competent student — complete enough to run and to defend.

### The same seller, many projects — and that's a beautiful thing

A project generator is almost never used once by one person. Somebody builds a project for a
friend. Then the friend's classmates come — "can you do mine?" — and inside one term the same
developer is delivering eight, ten projects to the same school. Sometimes to the same class, where
every submission is read by the same examiner in the same afternoon. Same afternoon, folks!

That decides a requirement. If every delivered app shares its markup, its CSS, its variable names
and its seed rows, the whole thing collapses. Two submissions side by side are obviously one
source, and both students are exposed. A disaster. So variety is part of the product. It's not
decoration. It's the product.

Each project resolves its own profile from its own seed, and that profile reaches every emitted
file:

| What varies | Examples |
|---|---|
| code style | collection initialisation, namespace style, DI registration, string building, comment density |
| naming | admin and user role names, and every label the spec supplies (page titles, column headers, button text, validation messages) |
| presentation | Bootstrap theme, navbar convention, button shape, plus a design preset that sets accent colour, neutrals, fonts and depth |
| content | seed rows, owners, prices, dates, review text, and the derived passwords |
| composition | which features are enabled, therefore which pages and tables exist at all |

The variation is deterministic, not random. Same seed, same app. Different seed, visibly different
app. The point isn't unpredictability — anybody can be unpredictable, it's easy — the point is that
two apps from the same hand cannot be shown to be copies of each other.

Now, I'm going to be honest with you, because this book is honest, very honest: page *structure*
still comes from one template set. The apps differ in style, naming, content and which features are
composed — not in the shape of a CRUD table or the layout of a card grid. Structurally different
apps would need layout variants, and those don't exist yet ([Chapter 22](22-limits.md)). Someday.
Maybe soon. We'll see.

## The two inputs, and why the split matters

| | `project-spec.json` | the seed JSON |
|---|---|---|
| Answers | *What is the app?* | *What is in it?* |
| Contains | models, properties + `displayName`s, relations, validations, DTOs, identity (roles, seed users, pages, error text), areas, controllers + provenance, views + page kinds, features, row counts | one array of rows per model, `refKey` / `parentRef_*` links, `$seedUser:N` owners, image search phrases |
| Changes when | the domain changes | the demo data changes |
| Validated by | `spec.schema.json` + semantic rules | a schema **derived from that spec** |
| Detail | [Chapter 11](11-contracts.md) | [Chapter 11](11-contracts.md), worked example in [Chapter 6](06-spec-to-seed.md) |

Two files. Shape in one, contents in the other. People ask me, "why two, why not one big file?" I
tell them: the split does real work. A seed is valid only for the models it was written against —
that's why a spec change silently invalidates an old seed, and the stage regenerates it. Keeping
*shape* and *contents* apart makes them independently reviewable, lets a human write either one,
and lets the whole pipeline run offline: a hand-written seed is indistinguishable from an LLM's.
Indistinguishable! The machine can't tell. Nobody can tell.

## The promise, stated precisely

"In scope" below means a stage actually checks it — not that somebody hoped it would work. Hope is
not a strategy, folks:

| Property | How it is enforced |
|---|---|
| It compiles | every code-emitting stage ends in `dotnet build` |
| It migrates | `ef migrations add` + `ef database update` in `seed`, again in `verify` |
| It boots | `verify` starts Kestrel on a free port and waits for a response |
| The landing page works | `GET /` is hard-required, never skipped |
| The admin can log in | real form login as the seeded admin; a failed login fails `verify` |
| Declared pages are reached | every `cardGrid` page anonymously, then every `adminTable` and feature route as the admin; any declared page that does not answer 200 fails `verify` |
| Protected pages are protected | every admin page and every login-only feature page is requested anonymously and must answer 401/403 or redirect to the login page |
| The UI is Bulgarian | visible-text scan over every page that answered 200: 2+ Latin words where at least one is a common English word (bare brand names do not trip it) |
| The files the spec promised exist | a manifest check before the app is even booted |

And what does it **not** promise? I'll tell you, because we're straight shooters here: that a
logged-in user is kept out of *another user's* records (ownership rules come from the spec's
deviations, not from a check), runtime behavior beyond the declared routes, visual design review,
performance, data correctness beyond DB constraints. That's all in [Chapter 22](22-limits.md). Read
it before you promise the tool to somebody else.

## Signature choices — the greatest hits

These are the decisions you can actually *see* in how the product behaves. Each one has a full
record — context, alternatives, consequences — in the ADRs indexed in [Chapter 25](25-decisions.md).
Every one of them a winner. Well — almost every one. You'll see.

| What you can observe | The choice behind it | Why |
|---|---|---|
| Your spec file is never modified by a run | three files: your spec, a generated lock file, and run state | a single file cannot be your input, the tool's working copy and the run's progress log at once |
| The tool works with no API key | every LLM exchange is a request file and a response file | the manual and automatic paths are the same contract, and a canned reply becomes a test fixture |
| Re-runs keep the demo data | seed rows reference each other by symbolic name | the database assigns keys at insert time, so no ID can be written in advance |
| The login and account pages look like the rest of the app | core identity pages are emitted, not translated at runtime | translating markup can corrupt Razor; emitting it can also restyle it |
| Identity error messages are Bulgarian | a dedicated error describer | it is a resource-string problem, solved where the string is produced |
| An SDK upgrade breaks the build immediately | scaffolder output is patched by anchored regex that fails loud | a silent miss would ship subtly wrong code |
| Deleted rows disappear from every query | soft delete is a global query filter on the model | no query, present or future, can forget it |
| Projects look different without per-template styling | a design-token overlay picked per project | visual variety without maintaining N sets of templates |
| The same spec yields the same code | variation is a hash of the project's seed | reproducible for the owner, distinct across owners — the design preset included (Chapter 12) |
| The desktop app can never drift from the CLI | the GUI spawns the CLI and reads its log | one pipeline implementation |
| Images are served from the app itself | downloaded once, cached, with shipped fallbacks | no hot-linking, no broken pages, no repeat requests |
| The admin menu stays readable as models grow | small models consolidate on one Specifications page | fourteen near-identical nav entries is not a menu |
| A failed agent round cannot leave wrong code behind | the model may only change the spec | every delivered app stays pure template output |
| Uninstalling keeps your projects | the installed program is read-only; writes go to a workspace folder | updates replace the program, never the work |

Every one a winner — and I mean it now. There used to be an exception in that table, a little
footnote on the design preset. Low-Energy `GetHashCode()`, I called him. Very weak hash. Randomised
every process. He's gone. Fired. Changelog 141 replaced him with the same SHA-256 the axes use, and a
golden-value test stands guard so he never comes back. We'll tell the whole story in
[Chapter 21](21-techniques.md). Great story.

## Names — and it has some very good names

The system is called **Genesis**. Beautiful name. Biblical, almost. It's the same product under
three spellings, and they're not interchangeable everywhere:

| Where | Name | Note |
|---|---|---|
| this book, and the English edition of the product | **Genesis** | the name used from here on |
| Bulgarian edition of the product | a Bulgarian title, settled with that edition | same system; its naming is handled separately |
| the executable, spec fields, environment variables | `dps-nn` | `dps-nn run`, `DPS_NN_MODEL_SEED` |

The source folders are `src/Genesis.*` and the assemblies and namespaces are `GenesisV6.*`, and both
stay exactly that way. Renaming them would break every generated file, fixture and historical log
for no user-visible gain. Zero gain! Why would you do it? You wouldn't. The boundary between the
display names and the internal ones is recorded in ADR-009. Smart decision. Very smart.
