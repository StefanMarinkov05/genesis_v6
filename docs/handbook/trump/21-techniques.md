# Techniques — hard-won patterns, we fought for every one

Condensed from `docs/explanation/techniques.md` — the highest-value doc in the set, because it
holds knowledge you *cannot* recover from the code alone. Every pattern here cost real time. Hours.
Days, sometimes. Some of these bugs, folks, they fought dirty. Very dirty. But we won. We always win
in the end. Here's how.

People come up and say, "Sir, isn't all this in the code?" No! That's the point. The code tells you
*what*. It never tells you *why the other three ways failed*. Nobody remembers that unless it's
written down — and believe me, it's written down right here.

## The Scriban rendering surface

`Util/TemplateEngine.cs` is the **only** place any stage touches Scriban. Every template sees exactly
the same helper surface, and that's what makes consistency inside a project enforceable instead of
hoped-for:

- `collection_init "Car"` → `"new()"` / `"new List<Car>()"` / `"[]"`, switched on the
  `collectionInit` axis. A template never hard-codes a collection-init style.
- `ns_open ns` / `ns_close` → file-scoped (`namespace X;`) vs block-scoped namespace tokens,
  switched on the `namespaceStyle` axis.
- `clr_type prop` → the C# type including nullability (`PropertySpec.Nullable` appends `?`).
- `subst` → `variation.Substitute`, the same function that resolves `$variation:axis` placeholders
  anywhere in the spec.
- `pascal` / `camel` → simple casing helpers.
- `ordered_models` → a topological sort of the models by `manyToOne` relation, so seed emission
  inserts parents before children in one linear pass. It replaced a two-pass heuristic that broke
  the moment a model was both a child and a parent (`Product` depends on `Brand`/`Category` but is
  itself depended on by `CartItem`/`OrderItem`). Don't bring the two-pass approach back. It had its
  chance. It failed.

## The trap: never pick anything with `GetHashCode()`

And here he is, folks. Low-Energy `GetHashCode()`. I've been telling you about him the whole book.
`GetHashCode()` is process-randomised in .NET, so a pick made with it changes between two runs of the
same spec. `TemplateEngine.ResolveDesignPreset` did exactly that for the unpinned design preset —
`variation.Seed.GetHashCode() % names.Count` — right up until changelog 141. Every other axis was
`SHA256(seed:axis)` per ADR-004, and this one little method was out there freelancing.

Not anymore. It now uses `VariationResolver.DeterministicIndex(seed, "designPreset", count)`, the
same `SHA256(seed:axis)` pick as every ADR-004 axis, pinned by a golden-value test
(`Unpinned_design_preset_is_sha256_of_seed`). He's finished. And the rule that keeps him finished:
**any new seed-derived choice goes through `DeterministicIndex` too.** No exceptions. No comebacks.
Well — one comeback. Ours.

## Razor `<style>` blocks: escape `@` as `@@`

Razor does **not** treat an inline `<style>` block as raw text the way it does `<script>`. A bare
CSS at-rule like `@media (...)` inside a `.cshtml` `<style>` block is parsed as a Razor code
transition — `@` plus the identifier `media` — and fails to compile (`CS0103: The name 'media' does
not exist`). Any template that embeds CSS with an `@`-rule inside markup must escape it as
`@@media`. It has only ever bitten `templates/views/design_overlay.sbn`, but the rule applies
anywhere CSS is embedded in Razor. One little `@`. One! And the whole build goes down.

## The `dotnet ef` "deps.json does not exist" saga

You see `Build succeeded.` and right after it `The specified deps.json [...] does not exist.` from
an automated `dotnet ef` call. **This was already investigated at length — read this before
proposing a new theory.** Four rounds, folks. Four:

1. **dotnet-ef version mismatch** (changelog 020) — the textbook match, worth checking first
   (`dotnet ef --version` must be 10.0.*), but *not* the cause here: the version was already right.
2. **File-flush / antivirus race** (changelog 022) — an explicit `dotnet build` before any `ef`
   call. Reduced the failure; didn't eliminate it.
3. **MSBuild build-server node reuse** (changelog 023) — `DOTNET_CLI_DISABLE_BUILD_SERVERS=1` on
   every build and `ef` call, plus a widened retry (up to 3 attempts, increasing delays). Still
   didn't eliminate it.
4. **Diagnostics instead of a fourth guess** (changelog 024) — after three failed theories,
   `--verbose` was added to every automated `ef` call instead of another blind fix. And that's the
   lesson, the big one, now written into the project's rules: **after 2–3 failed theories for the
   same symptom, stop guessing and add instrumentation.**

The status, honestly: the symptom stopped recurring before the `--verbose` output was ever exercised
against a real failure, so **no theory was conclusively confirmed as the root cause**. All four
mitigations stay in `SeedStage.cs` as defence in depth. If it comes back, the verbose output is
already there — compare it line by line with a manual `dotnet ef migrations add Initial --verbose`
in the same folder (the "Using application base" / "Using working directory" lines are where a real
difference would show). The bug went quiet. We're watching it. Very closely.

In the same cluster of work, unrelated to the race: unique per-run database names
(`{Project}_{yyyyMMddHHmmss}`, computed once in `ScaffoldStage`), so a fresh generation can't
silently collide with a stale LocalDB database from an older run (changelog 023).

## The Bulgarian residue-map trap: never blind-replace inside C# identifiers

The identity stage's static EN→BG map (`templates/identity/identity-bg-map.json`, ADR-002) does a
blind, longest-key-first text replacement on scaffolded Razor *markup*. No syntax awareness. None.
So it will just as happily corrupt a **C# identifier that contains an English word** as translate
prose. Confirmed in changelog 070: the "Email" entry turned `EmailModel` into a broken identifier
and `ManageNavPages.Email` into a broken member reference — both compile errors. There's no
reliable text-only way to tell translatable prose from an identifier on the same line, so the fix
was to **exclude the whole `Manage/` subtree** from the map, not to make the map "smarter". If you
add an Identity page group to `identity.modules`, check its code for words the map would match, and
exclude the subtree.

## Two separate `_ViewImports.cshtml` layers

A `using` that a Razor page needs must go into **both** cascades, because they really are two:

- the per-area MVC ones, `Areas/{Admin,Client}/Views/_ViewImports.cshtml` — generated by dps-nn;
- the Identity Razor Pages one, `Areas/Identity/Pages/_ViewImports.cshtml` — scaffolder-generated,
  a separate convention that cascades down *its* folder tree only. A page under
  `Areas/Identity/Pages/Account/Manage/` needs that top-level Identity one.

Miss either and you get a `using`-not-found compile error scoped to exactly that cascade —
confirmed the hard way, changelog 070.

## Stage ordering: a helper must exist before the build check that needs it

Every code-emitting stage ends in a `dotnet build` gate. If stage N emits something that
**references** a type emitted by a *later* stage, stage N's own gate fails — the type doesn't exist
yet. This bit `ProfileHash.cs` (changelog 070): emitted in `EmitControllersViewsStage` (stage 6),
but `IdentityStage` (stage 5) scaffolds and build-checks `_ManageNav.cshtml`, which references it.
The fix moved the emission earlier, into `IdentityStage` before its own scaffold call — the build
gate was **not** weakened. Never weaken the gate. Emit a cross-cutting helper in (or before) the
earliest stage that references it, not wherever it feels topically related.

## "You must install .NET to run this application" — only when installed

`dotnet-aspnet-codegenerator` and `dotnet-ef` are framework-dependent apphosts: they find the
runtime through `DOTNET_ROOT`, else the registered or default install location. `dotnet run` sets
`DOTNET_ROOT` for what it launches, so from a checkout they always worked. The installed,
self-contained `dps-nn.exe` has no `dotnet` parent, and with a per-user .NET install the `identity`
stage failed with "App host version … .NET location: Not found". `DotnetHost.EnsureDotnetRoot`,
called first thing in `Program.cs`, now sets it from the `dotnet` on `PATH` when unset. Any later
child that is itself a .NET apphost inherits the fix (ADR-014, changelog 134).

## Where to look next

The *why* behind the staging and the verify gates — the reason these ordering traps exist at all —
is in the ADRs ([Chapter 25](25-decisions.md)). The dated blow-by-blow is `plan/decisions.md`. Read
it before proposing a fix for a recurring symptom. Every time. That's how you stay a winner.
