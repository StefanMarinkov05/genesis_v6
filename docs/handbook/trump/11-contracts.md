# The two contracts — the best deals ever made

This chapter treats the spec and the seed as what they really are, architecturally: **the system's
public interface**. Everything inside the pipeline is an implementation detail that can change.
These two document shapes are what users — and the LLM — code against. They're contracts, folks,
and I know contracts. Field-by-field detail is owned by `spec.schema.json`; what follows is what
each part is responsible for.

People ask me, "Sir, what's the most important part of the system?" Easy. These two files. The
spec and the seed. Everything else can change — and it does change, all the time — but these two
are the deal. And when you make a deal, you keep it. Believe me.

## Contract 1: the spec

### Sections and their consumers

| Section | Consumed by | Guarantee |
|---|---|---|
| `meta` | every stage | identity of the project, UI language, database, style, routing, and `unsupportedRequests` for what was refused |
| `conventions` | `emit-models`, `identity` | timestamps base entity and soft-delete behavior are project-wide, not per model |
| `variationProfile` | `spec-expand` | optional pins over a deterministic profile ([Chapter 12](12-principles.md)) |
| `models` | `emit-models`, everything downstream | the entities, their columns, labels, validations and relations |
| `dtos` | `emit-models`, `emit-controllers-views` | composed read models for pages that should not expose an entity (wired, but unexercised — see [Chapter 22](22-limits.md)) |
| `customValidationAttributes` | `emit-models` | date-rule attributes (`DateNotInFuture`, `DateNotInPast`, `DateAfter`) emitted as real C# |
| `identity` | `identity` | roles, seed users, application-user extensions, its admin controller, Identity pages, error text |
| `areas` | `identity`, `emit-controllers-views` | the area→role mapping that turns "admin pages" into an authorization fact |
| `controllers` | `emit-controllers-views` | which controllers exist, how each is produced, and the declarative deviations on top |
| `views` | `emit-controllers-views`, `verify` | layout + the page kinds that drive route discovery |
| `features` | `spec-expand` | which reusable fragments merge in, and which feature-gated emissions turn on |
| `seeding` | `seed` | row counts per model, image source, output file |
| `outputReport` | `report` | which code pointers to resolve for downstream documentation |

### What expansion does to it

The spec you write is not the spec the stages read. A lot of people don't know this. `spec-expand`
performs four transformations and writes the result to `project-spec.lock.json`:

1. **Feature merge:** each enabled feature merges its fragment (models, DTOs, controllers) from
   `templates/features/<name>.json`; a fragment's `"$primaryModel"` token is resolved to the
   project's main model.
2. **Admin supervisor injection:** any model without an admin controller gets one, so the admin
   area is complete by construction.
3. **Variation resolution:** every `$variation:axis` placeholder is replaced with that project's
   resolved value; seed-user passwords are derived.
4. **Echo:** the resolved profile is written back as `variationProfileResolved`, so the lock file
   is self-describing.

Expansion is deterministic and idempotent: expanding the same spec twice produces the same lock.
Twice, ten times, a hundred times. Same lock. Rock solid.

One optional step sits beside it. When an API key is set or an `expand-review` response is already
in place, the LLM reviews the lock and suggests missed fields or relations. Each suggestion is put
to you as a `[y/n]` question, and an accepted one is only **logged** for you to merge by hand. The
lock itself is never patched by the model. The model advises. You decide. That's the chain of
command.

### Validation is two layers, on purpose

| Layer | Catches | Example |
|---|---|---|
| JSON Schema (`additionalProperties: false`) | shape, enums, types, unknown keys | `generatedBy: "scaffoldd"` fails loudly instead of being ignored |
| Semantic checks (`SpecLoader.SemanticCheck`) | what a schema cannot express | exactly one key per model; relation targets exist; controller model/DTO/redirect targets exist; sort fields exist; area roles exist; every visible property has a `displayName`; every template controller's `Index` has a page |

`"scaffoldd"`. Two d's. You'd be amazed how many typos try to sneak in. Very sneaky typos. The
schema catches every one.

The second layer is where the project's opinions live — strong opinions, very good opinions. The
failure messages are worth reading for the same reason: they name the model and the property, and
agent mode hands that text straight back to the model.

## Contract 2: the seed

The seed's shape is **derived from the spec**, so it has no independent schema file. Its job is
small and strict:

- one array per model that has a `rowsPerModel` entry;
- **identity by name, not by key:** `refKey` on parents, `parentRef_<Target>` on children, resolved
  to database keys at insert time (ADR-003);
- **users by position:** `$seedUser:N` indexes into the spec's `seedUsers`;
- **images by phrase:** `imageKeywords` is an array (1 for `single`, 2–4 for `multiple`), the only
  intentionally English field;
- closed objects, at least `rowsPerModel` rows, ISO dates, numeric decimals.

Three architecture consequences:

1. **The seed cannot outlive its spec.** Change a model and the old seed fails validation and is
   regenerated on purpose: silently translating rows across a model change would hide the change.
2. **The seed is data.** It's written to `Data/seed-data.json` and read at startup by
   `Data/SeedData.cs`, so changing demo content doesn't mean regenerating C#.
3. **The LLM and a human are interchangeable producers.** Both write the same file; the stage
   cannot tell which, and the archive records exactly which file was used. Equal opportunity,
   folks. Human or machine — same contract, same rules.

[Chapter 6](06-spec-to-seed.md) is the worked version with examples and failure modes.

## Why the contracts are files, not API calls

Every LLM exchange — spec draft, expansion review, seed, agent repair — is a request/response file
pair under `prompts/`. That one choice gives the project four properties it depends on. Four:

- **Works without a key** (paste into any chat), so the tool is usable in a locked-down
  environment.
- **Auditable:** the exact prompt and reply for a run are on disk, hash-logged.
- **Replayable:** a canned response is a test fixture; CI can exercise the LLM path with no network.
- **One validation path:** the schema check is identical for manual and automatic modes.

An API-first design would have none of them. Zero. And a week later nobody could answer "why did it
generate this app?". With files, you open the folder and there it is. On the record. Forever.
