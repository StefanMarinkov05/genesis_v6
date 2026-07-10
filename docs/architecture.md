# dps-nn (ДПС-НН) architecture

## Data flow

```
notes.md --(spec build, LLM)--> project-spec.json --(spec expand)--> project-spec.lock.json
lock --> scaffold --> emit-models --> identity --> emit-controllers-views --> seed --> verify --> report
                                  every code-emitting stage ends in a `dotnet build` gate
```

## Components

- **Spec layer** (`Genesis.Core.Spec`): POCO mirror + two-layer validation (JSON Schema with additionalProperties:false, then semantic cross-checks). Unknown keys are errors, not silence.
- **Variation** (`Genesis.Core.Variation`): ADR-004 resolver; the only randomness source in the system, and it is deterministic.
- **Pipeline** (`Genesis.Core.Pipeline`): stage interface, resumable state store, process wrapper with timeouts.
- **LLM bridge** (`Genesis.Core.Llm`): ADR-005 file contract, dual mode, schema-gated.
- **TemplateEngine** (`Genesis.Core.Util`): single Scriban surface; helpers (`collection_init`, `ns_open/close`, `clr_type`, `subst`) are how variation reaches every emitted file.
- **Stages** (`Genesis.Core.Stages`): one class per pipeline stage; see ADRs for the non-obvious ones.

## The generated app (per spec)

MVC + EF Core + Identity (roles, BG error describer, BG page map), Areas per role, controllers from three provenances (scaffold+deviations / template / hand-written UsersController with server-side AdminCannotDeleteSelf), central ImageAsset table + ImageService, symbolic-ref seeding, Bootswatch theme, layout with role-aware nav and BG user widget, cardGrid pages with mandatory client-side JS search, output report with resolved code pointers (file + line ranges + snippets) for the documentation toolchain.

## Verify gate ordering (cheap -> expensive)

build -> ef database update -> spec manifest on disk -> boot on a free port -> GET / -> GET first cardGrid route -> real admin form login (antiforgery + cookies) -> GET first adminTable route -> English visible-text leak scan (report always written; leaks throw).
