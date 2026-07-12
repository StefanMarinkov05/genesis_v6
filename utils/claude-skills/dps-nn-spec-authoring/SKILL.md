---
name: dps-nn-spec-authoring
description: Draft or extend a project-spec.json for the dps-nn (ДПС-НН / Genesis) ASP.NET MVC generator - a declarative spec that turns into a full seeded app through dps-nn's 9-stage pipeline. Use whenever Stefan pastes raw project notes and asks for a project-spec.json, asks to add a model/controller/view/feature to an existing spec, or asks "does this spec look right" for the dps-nn tool. Mirrors the pipeline's own spec-build stage, so the output matches what the automatic LLM path would produce. Works with or without spec.schema.json attached - if it's available, treat it as authoritative over this skill's summary.
---

# dps-nn spec authoring

Drafts or extends `project-spec.json` for dps-nn/Genesis - the tool that turns one JSON spec into a
buildable, seeded, verified ASP.NET Core MVC + EF Core app. This mirrors the pipeline's own
`spec-build` stage prompt and the concept groups in the project's `docs/reference/spec-format.md`,
so a hand-drafted spec here matches what the pipeline's automatic LLM path would produce.

**If `spec.schema.json` is attached to the conversation, it is the authoritative source of truth for
exact field names/types/enums - this skill is a guide for structure and judgment, not a substitute
for the schema.** Where this skill and the attached schema disagree, the schema wins.

## Hard requirements (non-negotiable)

1. **Every non-key/non-FK model property needs a `displayName`.** This is dps-nn's hard
   Bulgarian-UI requirement - the generated app's `verify` stage scans every rendered page for
   leaked English text and fails the build if it finds any. A property with no `displayName`
   renders its bare C# name, which usually IS the English leak.
2. **Bulgarian by default, unless told otherwise.** If `meta.language` isn't specified as
   something else, default to `"bg"` and write ALL human-readable strings (names, titles,
   `displayName`s, `errorMessage`s, category names) in Bulgarian (Cyrillic). Internationally
   branded proper nouns (BMW, iPhone, Adidas) stay in their original script. Only write English
   strings if the person explicitly asks for an English-language project.
3. **`specVersion` must be `"1.0"` or `"1.1"`. `meta` and `models` are the only other
   top-level required keys** - everything else (`conventions`, `identity`, `controllers`, `views`,
   `features`, `seeding`, `outputReport`) is optional but almost always wanted for a real app.

## Top-level concept groups

- **`meta`** - `projectName`, `solutionName`, `rootNamespace`, `domainHint` (what the app is
  about, in English, used by the pipeline's own prompts), `domainHintBg` (Bulgarian version),
  `language` (`"bg"` or another code), `database`, `style` (Bootstrap theme + design preset),
  `routing`.
- **`conventions`** - `timestamps` (CreatedAt/UpdatedAt auditing, ask the user y/n per field rather
  than guessing - the pipeline itself always asks interactively) and `deleteBehavior` (`"soft"`
  wires an EF global query filter so admins can undo deletes without losing data).
- **`models`** - the domain entities. Each property: `name`, `type` (`string` / `int` / `decimal` /
  `DateTime` / `DateOnly` / `bool` / `double` / `enum`), `key`/`foreignKey`/`nullable` flags,
  `validations` (with Bulgarian `errorMessage`s), `enumValues`, and **`displayName`** (required on
  every non-key/FK property - see above). `relations` describe FK/navigation shape
  (`manyToOne`/etc. with `target`). `images` wires a model into the central image-upload system.
- **`identity`** - Identity scaffold config: `modules` (which Identity pages to generate),
  `roles`, and `seedUsers` (email + role **only** - never write a password field; the pipeline
  derives seed-user passwords deterministically from the project's own variation seed).
- **`controllers`** - `generatedBy` is one of exactly three values: `"scaffold"` (codegen +
  declarative deviations like `removedActions`/`redirects`/`sorting`), `"template"`
  (framework-emitted), or `"writtenFromScratch"` (fully custom, e.g. server-side ownership rules).
- **`views`** - `layout` (shared chrome) and `pages`, each with a `kind` of exactly one of:
  `"landing"`, `"contactFormStatic"`, `"cardGrid"`, or `"adminTable"`.
- **`features`** - reusable fragments (cart, posts, catalogues, orders, rating, follows...) toggled
  with `{"enabled": true}`. Only enable a feature that's actually relevant to the domain hint - it
  pulls in a full slice of models/controllers/views.
- **`seeding`** - how the `seed` stage will generate rows later (this is the OTHER skill,
  `dps-nn-seed-authoring` - don't try to write seed data here, just make sure `rowsPerModel`
  exists for every model that should get seed rows).
- **`outputReport`** - only needed if a downstream documentation/report step will consume
  `codePointer`s; skip it unless asked for.

## Workflow

1. Read the person's raw notes/description of the app they want.
2. Infer the domain's core models, their relations, and which controllers/views/features they
   imply. Don't over-scope - a diploma-work sample app is usually 5-10 models, not 30.
3. Write Bulgarian `displayName`s and `errorMessage`s for everything, unless told the project is
   English-language.
4. Ask (don't guess) about `conventions.timestamps` (CreatedAt/UpdatedAt/DeletedAt: y/n each) if
   it matters and wasn't specified - this mirrors the pipeline's own interactive prompt for it.
5. Output ONLY the JSON document - no markdown fences, no prose commentary, unless the person
   asked for an explanation alongside it.

## When something looks wrong

If asked to review an existing spec: check the hard requirements above first (displayName
coverage, no invented passwords in `seedUsers`, `generatedBy`/`kind` values are exactly the
allowed enum strings), then check internal consistency (every `manyToOne` relation's `target`
actually names a model that exists in the spec).
