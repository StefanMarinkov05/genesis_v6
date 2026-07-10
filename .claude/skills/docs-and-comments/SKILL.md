---
name: docs-and-comments
description: Standards for code comments and project documentation written for learning and teaching. Use whenever writing code that should carry explanatory comments, creating READMEs, API docs, architecture docs, runbooks, or tutorials, or when the user asks to "document this", "add comments", "explain the code", or wants a codebase understandable for students/juniors/future maintainers. Apply automatically to all substantial code produced in learning-oriented projects.
---

# Documentation & Teaching Comments

Documentation is the interface to the humans. In learning-oriented codebases, comments teach the *reasoning*; docs teach the *system*. Both rot unless kept next to what they describe and reviewed with it.

## Comments: what to write

The code says *what*; comments earn their place by saying **why, why-not, and watch-out**:

- **Why**: the reason this approach was chosen. `// Keyset pagination instead of OFFSET: page 10,000 would scan-and-discard 200k rows.`
- **Why not**: the tempting alternative and why it loses. `// Not a Set: we need insertion order for the replay log.`
- **Watch out**: invariants, units, ranges, non-obvious contracts. `// amountMinor is in cents; callers must never pass floats.`
- **External context**: links to the spec, RFC, issue, or paper implemented. `// Implements RFC 6455 §5.2 frame parsing.`
- **Teaching notes** (learning codebases): brief explanations of the technique itself at first use - what an idea is and why it exists, one short paragraph, once, at the clearest example: `// Circuit breaker: after N consecutive failures we stop calling the`
  `// dependency for a cooldown window, converting slow failures into fast`
  `// ones so threads don't pile up waiting on a dead service.`

**Never write**: comments restating the line (`i++ // increment i`), change history (git owns that), commented-out code (delete it; git remembers), or lies - an outdated comment is worse than none, so comments are reviewed in every PR touching their code.

**Structure of a well-commented unit**: a block comment at the top of each non-trivial function/module answering *what role does this play in the system and what must callers know*, then inline comments only at the genuinely non-obvious lines. If a function needs a comment every 3 lines, refactor for clarity first - comments are not makeup for confusing code.

**Doc-comments** (docstrings/JSDoc/XML docs) on every public API: one-line summary in the imperative, params with units/ranges, return shape, exceptions/error cases, and one minimal usage example. Examples in doc-comments are the most-read documentation you'll ever write - keep them copy-paste runnable.

## Project documentation set

Keep docs in-repo (`README.md` + `docs/`), versioned with the code. The standard set, each with a distinct job:

- **README** - the front door, one screen ideally: what this is (2 sentences), who it's for, quickstart that works on a clean machine in <5 minutes (this is the most important doc in the repo - test it on a clean clone), links to the rest.
- **Architecture doc** (`docs/architecture.md`) - the system on one page: a diagram (Mermaid, lives in the repo, diffable), the main components and their responsibilities, how one representative request flows end to end, and the key invariants. Write it for the new team member on day 2.
- **ADRs** (`docs/adr/`) - one file per significant decision: context, decision, alternatives, consequences (see engineering-reasoning skill). ADRs are append-only history; supersede, don't edit.
- **API reference** - generated from doc-comments/OpenAPI where possible (generated docs can't drift), hand-written guides only for the concepts generation can't cover.
- **Runbook** (`docs/runbook.md`) - operations: how to deploy, roll back, read the dashboards, and a section per known failure mode with symptoms → diagnosis → fix. Written for someone paged at 3am who has never seen this service.
- **Contributing/setup** - environment setup, test running, conventions, PR process.

## The four kinds of docs (Diátaxis) - don't mix them

- **Tutorial**: learning by doing; a guaranteed-success guided path for beginners; assumes nothing.
- **How-to guide**: a recipe for a specific task, for someone who knows the basics ("how to add a new endpoint").
- **Reference**: complete, accurate lookup material; structured like the code, no narrative.
- **Explanation**: background and reasoning ("why we chose event sourcing"); deepens understanding, no steps.

Most bad docs mix these (a reference page that tries to teach; a tutorial that digresses into theory). Decide which one you're writing before writing.

## Writing rules

- Lead with the point; details after. Readers scan.
- Short sentences, active voice, concrete examples over abstractions - every claim about behavior gets a code snippet or command the reader can run.
- Define each term once at first use; consistent vocabulary thereafter (one name per concept - not "user/account/customer" interchangeably).
- State prerequisites and versions explicitly; docs that fail silently on version drift destroy trust.
- Every doc gets an owner and a "last verified" date; stale docs are deleted or fixed, never left to lie.

## Definition of done for any feature

Code + tests + doc-comments on public surface + README/architecture/runbook updated if the feature changed setup, flow, or ops. Documentation debt is tracked like tech debt - visibly.
