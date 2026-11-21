# Quality architecture — the highest quality, nobody has higher

Quality here is not a phase at the end. A phase at the end — that's what you do when you've already
lost. It's a set of gates spread through the pipeline, plus a test policy that decides what a fixed
bug has to leave behind.

People come up to me — testers, the toughest people in software, very hard to impress — and they
say, "Sir, how do you know it works?" Gates. Tests. A policy. Red, green, revert, green. Nobody
tests a generator like this, believe me. Some tools test by hoping. We test by testing.

## Four kinds of gates

| Gate | Where | What it refuses |
|---|---|---|
| Schema + semantic validation | `spec-expand` (and every `SpecLoader.Load`) | a spec that cannot mean anything |
| Build gate | end of `scaffold`, `emit-models`, `identity`, `emit-controllers-views`; inside `seed` | code that was emitted but never compiled |
| Migration gate | inside `seed`, again in `verify` | a model change that does not reach a database |
| The verify gate | stage 8 | an app that builds but does not boot, log in, render, or speak Bulgarian |

The ordering inside these gates is cheap-to-expensive, on purpose: schema before generation,
manifest before boot, `GET /` before the declared routes, the routes before the language scan. A
failure at step *n* means steps *n+1…* were never wasted. Never wasted! You don't boot an app to
find out a file is missing. You check the file. Common sense. Not so common, frankly.

## What unit tests cover, and what they leave out

The .NET suite (`tests/Genesis.Tests/`) tests **decisions**, not generated artifacts:

| Area | Examples |
|---|---|
| Agent loop | green run, one repair, environment stop, exhausted budget, draft failure, pending propagation — all with a fake author and a fake pipeline |
| Validation | semantic rules per cross-reference; the `displayName` gate; sorting fields on the audit base |
| Variation | the same seed resolves identically; different seeds differ; every password pattern satisfies Identity's policy |
| Pipeline | resume/skip semantics, stage-failure bookkeeping, the log format |
| Seeding | schema derivation from a spec; ref resolution; image filename pattern |
| Doctor | each missing prerequisite produces its fix line |
| Language gate | the English heuristic on real markup samples (proper nouns pass, "Add to cart" fails) |

The GUI has its own `node --test` suite for the pure logic it owns: theme resolution, i18n table
completeness, completion-banner parsing, the run-control state machine, recent projects, agent
helper functions and the music-folder parser.

What is **not** unit-tested is the actual generation of an app — that's what the pipeline run and
`verify` are for. Mocking `dotnet build` would test the mock. You'd be testing a fake! Very fake.
We don't test fakes. We test the real thing.

## The rule for every bug: red, green, revert, green

A bug fix is not done when the symptom disappears. Symptoms disappear all the time — they come
back, folks, they always come back. It's done when a test *proves* the bug existed:

1. **Red:** write a test that exercises the bug; watch it fail *for the stated reason*.
2. **Green:** fix the code until it passes; change nothing else.
3. **Revert:** undo the fix, keep the test, watch it fail again. A test that still passes without
   the fix was never testing the bug.
4. **Green:** restore the fix; commit test and fix together.

The revert step is the one that matters, because it's the only evidence that the test is connected
to the failure. It costs one command. One! Skip it often enough and the suite stops meaning
anything. Red, green, revert, green. Say it with me. Red, green, revert, green. Beautiful.

## New behavior: test first

New features follow the same order. The test that describes the behavior is written first and seen
to fail, the code makes it pass, and the code is then cleaned up with the test as the safety net. A
test written after the code tends to describe what the code *does*, not what it *should* do. That's
not a test. That's a press release.

## Regression: the suite only grows

- Every fixed bug leaves its test behind. A regression test is never deleted, skipped or weakened to
  make a build pass — if it fails, the bug is back.
- Both suites run on every push and pull request; a change with red CI is not merged.
- **A flaky test is a bug**, in the test or in the code, and gets the same red-green-revert
  treatment. The parallel-emit test that failed only on a single CPU was reproduced on purpose
  (`taskset -c 0`, 30 failures in 30 runs), fixed by making the test items wait until a second
  worker lane has started, and re-run (0 in 30). Thirty for thirty, then zero for thirty. What a
  turnaround. One of the great comebacks.
- For anything that changes generated apps, the end-to-end test is the pipeline itself: a full run
  on the FaceNote fixture must pass `verify`.

## Mutants: testing the tests

Passing tests show the code works for the cases tested. They don't show that the tests would
*notice* a mistake. **Mutation testing** does. It makes small deliberate changes to the code — flips
a condition, removes a call, changes a constant — and runs the suite against each changed copy, a
*mutant*. A mutant the suite fails on is *killed*; one that passes every test *survives* and marks
behavior that no test actually checks. Mutants. Very dangerous. We find them.

- Surviving mutants in code that matters (stage logic, spec validation, the agent's repair loop,
  path handling) get a test that kills them.
- The revert step above is a hand-made, targeted mutant: removing the fix is the most important
  mutation of all.
- The tools are **Stryker.NET** for the .NET suite and **StrykerJS** for the GUI. They are **not yet
  wired into the repository or CI** — a full run is slow — so mutation runs are done on demand, per
  module, before a release or after a large change to that module.

The full policy, including what "done" means for a change, is `docs/explanation/testing-policy.md`.

## Continuous integration

| Job | Runs on | What it proves |
|---|---|---|
| `ci.yml` → build-and-test | Windows, every push/PR to master | `dotnet build` + `dotnet test` on the solution |
| `ci.yml` → gui-test | Windows | typecheck, electron-vite build (all three targets), `node --test` |
| `handbook.yml` | docs changes, releases, manual | the first edition of this book (assembled from `docs/`) builds; on a release it is attached. This edition is built locally with `scripts/build-handbook2.py` |
| `release.yml` | version tags | the Windows installer is produced |
| `lite-mirror.yml` | master pushes and tags | the music-free edition is rebuilt and pushed |

Windows is not incidental. The installer is NSIS and the release job builds it on the same runner
image, so CI runs where the product ships. CI does **not** run a full generation — that needs SQL
Server LocalDB and several minutes of builds, and it's run locally with the commands below. The
generator itself also builds and unit-tests on Linux; only generation needs Windows.

## Why "done" is a pipeline run

The end-to-end proof:

```bash
dotnet build
dotnet test
cp seeds/facenote-rich-seed.json prompts/in/seed-response.json
dotnet run --project src/Genesis.Cli -- run --all --spec fixtures/project-spec.facenote.json --out out
```

If `verify` passes, the change is proven against a real database, a real web server and the real
routes. Real, real, real. If it fails, the failure is attributable to a stage, and the stage is the
thing to fix.

## The knowledge that is not in the code

Three running records make the quality system survive across sessions and agents:

- `changelogs/` — one numbered entry per change, with the mechanism and the fix;
- `plan/decisions.md` — every bug and **every theory already tried and rejected**;
- `plan/state.md` — the session log.

The rule that pays for them: **before proposing a fix for a recurring symptom, read the record.** A
theory recorded as failed must not be proposed again, and after two or three failures the next
step is instrumentation, not a fourth guess. A fourth guess! Can you imagine? Some people would
guess forever. Not here. This is the cheapest quality mechanism in the project, and the easiest to
skip under pressure. Don't skip it. Winners read the record.
