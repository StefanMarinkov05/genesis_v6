<!-- front-matter -->
# Preface — the greatest preface, maybe ever

Folks, this is a book about one system. One. And what a system it is. It's called **Genesis**, and
it's a generator — a tremendous generator — that turns two JSON files into a complete, seeded,
verified ASP.NET Core application. Two files go in. A whole app comes out. Built, migrated, booted,
logged into. Nobody had ever seen anything like it. People said it couldn't be done. It was done.

A lot of people come up to me — students, developers, very smart people, some of the smartest — and
they say, "Sir, what is this book?" Great question. Tremendous question. This book is the whole
system, front to back, in one place. Nobody has ever documented a generator like this. Nobody. People
said documentation had to be boring. Long, grey, sad. We said no. Believe me, you're going to read
every page, and you're going to know this system better than people who've worked on systems for
twenty years.

We go from breadth to depth. Very smart. You need the shape of a system before any of its parts make
sense — everybody knows this, the best engineers know this — and every part assumes only the ones
that came before it. No surprises. We don't like surprises. Surprises are for bugs.

## The ladder — and it's a beautiful ladder

| Part | Chapters | You learn |
|---|---|---|
| **I. Overview** | 1–3 | the problem it solves, one generation told as a story, and what a generated app looks like to its user. No internals. |
| **II. Using it** | 4–7 | run it, write a spec from a requirements paper, write seed data, let the agent do both. |
| **III. Inside** | 8–12 | the architecture in one picture — style, patterns, coding conventions, tools; then the pipeline as a machine, the output, the two contracts and the design principles. |
| **IV. Architecture in depth** | 13–16 | the components and their dependency rules; how quality is enforced and tested; how the project itself is built; how it runs and ships. |
| **V. Deep dives** | 17–21 | the long-form chapters the narrative only summarises: the CLI, the GUI, the image pipeline, the Specifications page, and the traps that cost real time. |
| **VI. Reference** | 22–25 | what it cannot do, what the buyer receives (including ownership marks), a glossary, and the decision record. |

Even if you only want to *use* the tool — and a lot of people do, believe me — read Part I. The
vocabulary comes from there. And read Part VI before you promise this tool to anybody else. Very
important. We tell the truth about the limits in this book. Other books don't. Sad!

## Who each part is for

| Reader | Read |
|---|---|
| Reviewer, professor, recruiter | Parts I and VI, then Chapter 8 for the architecture and Chapter 9 for the machinery |
| Student building a project | Parts I–II, with Chapter 22 open |
| Contributor or coding agent | Parts I, III, IV, then the file map and the ADRs |

Coding agents, by the way — fantastic readers. They read everything. They never skim. Some humans
should learn from them.

## Conventions — very strong conventions

- Code blocks carry a language tag (`SHELL`, `JSON`, `C#`, …). An untagged block is illustrative
  output, not a command to run. Don't run it. It's a picture of output. A beautiful picture.
- Paths are repo-relative unless they start with `/` or a drive letter.
- **Generated** means "produced into `out/`"; **verified** means the `verify` stage passed — never
  "it compiled". "It compiled" is what losers say. Compiling is the minimum, folks.
- Diagrams are Mermaid source, rendered as vector figures here. Crisp. Sharp. You can zoom forever.
- Counts and names were checked against the tree at the version on the cover; where a claim could
  drift, the command that re-checks it is given.
- ADRs are referenced by number (`ADR-004`) and indexed in [Chapter 25](25-decisions.md).

## Status of this edition

**The Trump Edition.** This is edition 2 of the handbook — the same audited text, the same facts, the
same tables, the same diagrams — told in the rhetorical style of a certain very famous speaker. It
is a parody of a speaking style, nothing more: no endorsement, no politics, no campaign. The only
things that get insulted in here are bugs, boring documentation and lesser tools. They deserve it.

Every technical claim in this edition is the claim in the clean English edition 2, which was
audited twice against the code — the corrections are recorded in the repository's changelog
(entries 137–139), and the gaps the audit found are in [Chapter 22](22-limits.md). If a superlative
and a table ever seem to disagree, the table wins. The table is always right. That's the deal.

That edition has been adopted, so the documents below should be retired rather than kept in
parallel. Two descriptions of one system drift apart, and the copy nobody reads is the one that
turns out wrong. Total disaster, every time. We've seen it.

| Chapters | Replaces |
|---|---|
| 1–3, 22 | `docs/explanation/project-overview.md` |
| 4–7 | `docs/tutorial/genesis.md` and the two authoring skills' prose |
| 9 | `docs/reference/control-flow.md` |
| 10 | `docs/explanation/generated-app-anatomy.md` |
| 11 | `docs/reference/spec-format.md` + `docs/reference/seed-format.md` |
| 12 | the variation half of `docs/explanation/architecture-decisions.md` |
| 13 | `docs/reference/components.md` + `docs/reference/file-map.md` (summary) |
| 14 | `docs/explanation/testing-policy.md` |
| 15 | (nothing today — the AI-assisted process, the skills and the cost figures were never written down) |
| 16 | `docs/how-to/build-the-installer.md` + `mirror-the-lite-edition.md` (summarised) |
| 17–21 | nothing — these chapters summarise those docs, as the book's deep dives |
| 23–25 | (nothing today — delivery marks, the glossary and the decision index were not documented) |

Not replaced, deliberately: `docs/explanation/gui-architecture.md`, `image-pipeline.md`,
`specifications-consolidation.md`, `techniques.md`, and the ADRs. They're deep. Very deep. Deep
enough that this book summarises them in Part V and sends you to them for the rest, instead of
copying them word for word. In this edition, Chapters 17–21 are condensed retellings — in the
voice, with the facts kept — and the full long-form originals stay in `docs/`.
