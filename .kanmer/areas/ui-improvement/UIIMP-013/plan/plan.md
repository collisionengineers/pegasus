# Plan — UIIMP-013: The test-ui gate costs 50 minutes of every build-affecting PR

## Diff estimate

About **+58 / −24 lines across three files**, one commit, no new files, no deleted files:
`scripts/Update-TestUiSnapshots.ps1` ≈ +20/−9, `.github/workflows/ci.yml` ≈ +30/−13,
`docs/runbook.md` ≈ +8/−2. `AGENTS.md` is expected at +0/−0 and is authorized only under
the condition in Constraints. Nothing under `docs/design/test-ui/` changes.

## Objective

Cut the `test-ui` job's wall clock by removing a parallelism cap that is applied to the
wrong half of the capture suite, and make a budget failure say so — without changing which
tests run, which pages are generated, or what the gate detects.

## Starting state

Verified read-only from `origin/dev` at `fbf8ee40983ee30030b296d9e61274b238c80b04`
(`git show origin/dev:<path>`; the run's recorded `9b8f78a3…` has advanced, and every file
below is identical on `origin/dev` and `origin/main`).

Evidence: `files`@this ticket's `files` document; repository at
`fbf8ee40983ee30030b296d9e61274b238c80b04`; measurement source run `33310451221` on PR #609
as quoted in the ticket body and in the `ci.yml` `test-ui` comment.

**How the gate works today.** `.github/workflows/ci.yml` § `test-ui` runs on
`windows-latest` with `timeout-minutes: 75`, gated on `needs.changes.outputs.build`. It
checks out, runs the `dotnet-build` composite action (locked restore + Release build),
restores the cached pinned Playwright browsers, installs Chromium, then runs one step:
`./scripts/Update-TestUiSnapshots.ps1 -Verify`.

That script wipes `artifacts/test-ui-capture`, exports `PEGASUS_TEST_UI_CAPTURE_DIR`, and
runs **two** `dotnet test` invocations:

1. **Capture** (script l.29-33) — filter
   `(FullyQualifiedName~WebTests|Category=Browser|FullyQualifiedName~StaffSignInSecurityTests|FullyQualifiedName~TestUiFocusedRenderTests|FullyQualifiedName~QdosCustodialWebTests|FullyQualifiedName~AutomationConnectorAuthorizationTests|FullyQualifiedName~ImageViewingWebTests)&Category!=Corpus`,
   with `PEGASUS_TEST_UI_MODE` unset and `-- xUnit.MaxParallelThreads=2`. 414 tests. The
   capture middleware (`TestUiResponseCapture.cs`) writes every `text/html` and `image/*`
   response into the capture directory, content-addressed and write-once.
2. **Verify** (script l.40-43) — filter `FullyQualifiedName~TestUiSnapshotTests` with
   `PEGASUS_TEST_UI_MODE=verify`.

**Two claims in the ticket body and in the `ci.yml` comment are false, and correcting them
is what identifies the cost driver.**

- *"the capture runs at processor-count parallelism"* — it does not. Script line 33 pins
  `-- xUnit.MaxParallelThreads=2` across the **entire** capture filter. The project's own
  `tests/Pegasus.IntegrationTests/xunit.runner.json` sets `maxParallelThreads: 4`.
- *"Verify then runs a second pass on top"* / *"a second pass on the committed corpus"* —
  it does not. The verify invocation selects `TestUiSnapshotTests`, which is a **single
  `[Fact]`**. It reads the retained capture, regenerates in memory, compares against the
  committed files, checks for orphans, and renders each committed page once in one
  Chromium. It re-runs none of the 414 tests. Removing a "second pass" therefore saves
  nothing; only the build it repeats is free to remove.

**Where the 40m23s actually goes.** `docs/runbook.md` § Locked restore, build, and test
(l.326-334) is the repository's owner of this model and states the rule the script breaks:
the project caps concurrency at four, and *"the browser selection halves it again on the
command line, because each of its tests starts a Chromium and a loopback host beside its
own database."* The capture applies that halving to browser **and** non-browser tests
alike. Two measurements bound each half exactly:

| Half of the capture | Selection | Measured evidence | At |
| --- | --- | --- | --- |
| Browser | Intersecting the capture filter with `Category=Browser` is **exactly** `Category=Browser&Category!=Corpus` — the `browser` lane's own filter | 11-15 min (ticket body; lane budget 25 min) | `MaxParallelThreads=2` |
| Non-browser | A strict **subset** of `Category!=Corpus&Category!=Browser` — the `sql-integration` lane's filter | ≤ 11m55s for the whole superset on one runner (`ci.yml` § `sql-integration` comment) | project default, 4 |

So the browser half is already at its correct, proven cap and cannot get cheaper here; the
non-browser half is running at half the parallelism the same tests are proven green at in
`sql-integration`. That is the cost driver, and it is a one-line origin.

**The guarantee the gate must keep.** `TestUiSnapshotTests` asserts that every `visual`
state in `catalogue.json` matched a captured Razor response (`Generate`, l.115-169), that
every committed page under `pages/` is one a state generates (orphan check, l.89-93), that
each generated file is byte-equal to the committed one after newline normalization
(l.79-88), and that every committed page renders offline in Chromium with every visible
image loading (l.97-113). Committed snapshots regenerated from the Razor pages, and hand
edits detected, are exactly those four assertions.

**The failure mode being fixed.** The job budget is enforced only at job level, so
exceeding it cancels the job mid-step; the last thing in the log is a partial run of the
gate that exists to detect a stale catalogue, and both prior failures were read as stale
corpora. Nothing in the job ever says "this ran out of time".

## Governing docs

- **`docs/engineering.md`** (the ticket's `refs`) — **Meets.** § Plan sizing: the diff
  estimate is stated first and the plan has four real steps. § Test support: no new fake,
  helper or knob is introduced; the change removes an override so the non-browser half
  inherits the project's existing `xunit.runner.json` cap, and reuses the existing
  `-SkipCapture` switch for the perturbation checks. § Simplicity and § Skip rules: the
  change is behaviour-preserving and adds no abstraction.
- **`docs/design/README.md` § Test UI** — **Meets, not modified.** Every sentence there
  stays true: the catalogue remains captured from the current Razor pages, hand editing
  remains forbidden and detected, the catalogue remains design-evidence-only and never a
  publish input, and the three documented local commands
  (`scripts/Update-TestUiSnapshots.ps1`, the same with `-Verify`, `scripts/Test-UiCatalogue.ps1`)
  keep their names and switches. No file under `docs/design/test-ui/` is touched.
- **`docs/runbook.md` § Locked restore, build, and test** — **Meets its rule, extends its
  text.** The change makes the capture conform to the parallelism model this section
  already states (project cap of four; the browser selection halves it). One sentence is
  added recording that the capture applies the same split. That is a documentation
  extension of an existing rule, not a change to it, and needs no authorization beyond
  this ticket.
- **`AGENTS.md` rule 24** — **does not fire.** No command name, switch, or convention
  changes: `./scripts/Update-TestUiSnapshots.ps1`, `-Verify`, `-SkipCapture` and
  `./scripts/Test-UiCatalogue.ps1` are all unchanged, so `AGENTS.md` lines 168-174 stay
  correct as written. If implementation nonetheless changes a documented command or
  switch, those lines must be updated in the same PR — see Constraints.
- **No new ADR.** This changes CI lane cost and a script's invocation of an existing test
  selection. It settles no architectural question, introduces no port, adapter or
  dependency, and alters no Core contract. Recording it as an ADR would be ceremony.

## Required changes

1. `scripts/Update-TestUiSnapshots.ps1` runs the capture as **two sequential `dotnet test`
   invocations against the same, once-wiped capture directory**: the existing capture
   filter intersected with `Category=Browser` at `-- xUnit.MaxParallelThreads=2`, then the
   same filter intersected with `Category!=Browser` at the project default (no override).
   The union of the two selections is byte-for-byte the current selection and their
   intersection is empty, so the set of tests that run and the set of captured responses
   are unchanged.
2. The second and third `dotnet test` invocations pass `--no-build`. The first keeps
   `--no-restore` alone and remains the one that builds, so the script's local contract is
   unchanged.
3. Each phase prints a delimited banner naming the phase and its elapsed time, so a log cut
   short names the phase it died in.
4. `.github/workflows/ci.yml` § `test-ui`: the false comment statements are corrected, the
   snapshot step gains its own `timeout-minutes` strictly below the job's, and a following
   `if: failure()` step prints an unambiguous budget message. A step-level timeout **fails**
   the step rather than cancelling the job, so the diagnostic step runs.
5. `.github/workflows/ci.yml` § `test-ui`: `timeout-minutes` drops from 75 to a value
   derived from the PR's own measured run (Step 4), never from an estimate.
6. `docs/runbook.md` § Locked restore, build, and test records that the capture applies the
   same browser/non-browser split.

## Expected files

| Action | Repo-root-relative path | Responsibility |
|---|---|---|
| Modify | `scripts/Update-TestUiSnapshots.ps1` | The capture split, `--no-build` on the invocations that follow the build, and the phase banners. Not a generated artifact. |
| Modify | `.github/workflows/ci.yml` | The `test-ui` comment correction, the step budget, the failure diagnostic, and the measured job `timeout-minutes`. Not a generated artifact. |
| Modify | `docs/runbook.md` | One sentence in § Locked restore, build, and test naming the capture's split. Not a generated artifact. |
| Modify | `AGENTS.md` | Lines 168-174 only, and only under the rule-24 condition in Constraints. Expected diff is zero. Not a generated artifact. |
| Inspect | `tests/Pegasus.IntegrationTests/xunit.runner.json` | Read for the `maxParallelThreads: 4` value the non-browser half inherits. Read-only. |
| Inspect | `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs` | Read for the four assertions that constitute the gate's guarantee. Read-only. |
| Inspect | `tests/Pegasus.IntegrationTests/TestUiResponseCapture.cs` | Read for the write-once, content-addressed capture semantics that make two sequential writers safe. Read-only. |

## Do not modify

- `docs/design/test-ui/**` — the `test_ui_catalogue` lock's files. This ticket changes no
  Razor page, so the committed catalogue must be byte-identical before and after.
- `src/**` — no product-page change of any kind.
- `tests/**` — including `tests/Pegasus.IntegrationTests/xunit.runner.json`. The saving
  comes from removing an override, not from adding or changing a knob.
- `scripts/Test-UiCatalogue.ps1`, `scripts/Invoke-TestShard.ps1`,
  `.github/actions/dotnet-build/action.yml`.
- `docs/operator-notes.md` — never edited by an agent.
- Every job in `.github/workflows/ci.yml` other than `test-ui`. `sql-integration` and
  `browser` supply this plan's measurements; changing them invalidates the evidence.

## Constraints

- **The guarantee is fixed.** The union of the two capture selections must equal the
  current selection exactly, and their intersection must be empty. No test may be dropped,
  no filter alternative removed, no assertion in `TestUiSnapshotTests` weakened or skipped.
- **The browser cap stays at 2.** `ci.yml` § `browser` records why: a Chromium, a loopback
  Kestrel host and a restored database per test, four at once on four vCPUs is what makes
  navigation waits time out. Raising it is out of scope.
- **The non-browser half takes no explicit override.** It inherits
  `xunit.runner.json`'s `maxParallelThreads: 4` — the value `sql-integration` is proven
  green at for a strict superset of the same tests. Do not write `4` on the command line.
- **The capture directory is wiped once**, before the first half, and never between the
  halves. The middleware's write-once semantics make the two writers safe only if both
  write into the same surviving directory.
- **`Category!=X` complement semantics are already proven in this repository**:
  `ci.yml` § `sql-integration` calls its filter and `browser`'s "a complement pair, so this
  lane and `browser` together select exactly what the single validate job used to select".
  This plan relies on that same property and on nothing new.
- **No timeout is raised.** The ticket forbids it. `timeout-minutes` moves downward only,
  and its final value comes from Step 4's measurement, not from this plan's arithmetic.
- **Rule 24 condition.** `AGENTS.md` may be edited only if implementation changes a
  documented command name or switch. It should not. If it does, lines 168-174 are updated in
  the same PR and the deviation is reported.
- **This agent's role runs no tests.** Build only, for compiler feedback. The capture, the
  verify, the perturbation injections and the catalogue check belong to the test runner.

## Ordered steps

### Step 1 — Split the capture by browser-ness and stop rebuilding

- Preconditions: worktree on `task/<slug>` cut from `origin/dev`; capture directory logic at
  script l.14-24 unchanged.
- Files: `scripts/Update-TestUiSnapshots.ps1`
- Symbols: the `if (-not $SkipCapture)` capture block (l.25-37) and the verify invocation
  (l.40-46).
- Change: replace the single capture `dotnet test` with two sequential invocations over the
  same capture filter, appending `&Category=Browser` to the first (keeping
  `-- xUnit.MaxParallelThreads=2`) and `&Category!=Browser` to the second (with **no**
  `--` thread override). Hold the shared filter in one variable so the two forms cannot
  drift apart. Give the second capture invocation and the verify invocation `--no-build`;
  leave `--no-restore` on all three and leave the first without `--no-build` so it remains
  the build. Wrap each phase in a banner printing the phase name on entry and its elapsed
  time on exit, and keep each invocation's existing non-zero exit-code throw, naming the
  phase in the message.
- Preserved behaviour: the capture directory is wiped exactly once and only when
  `-SkipCapture` is absent; `-SkipCapture` still skips both capture invocations and still
  throws when no retained capture exists; `PEGASUS_TEST_UI_MODE` is unset during capture,
  set to `update` or `verify` for the third invocation, and restored in `finally`.
- Forbidden: changing the filter's alternatives; adding `4` or any explicit thread count to
  the non-browser invocation; adding a new switch or parameter to the script; wiping the
  capture directory between the halves; `--no-build` on the first invocation.
- Negative cases: a capture invocation exiting non-zero must still throw and must name
  which half failed; `-SkipCapture` with no retained capture must still throw.
- Tests: `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs` proves the result, run by
  the test runner via the script; the implementer runs no tests.
- Commands: `dotnet build ./Pegasus.slnx --configuration Release --no-restore` (compiler
  feedback only; the script itself is not compiled).
- Expected output: build succeeds; `git diff` touches one file and shows the union of the
  two filters is textually the original filter plus the two appended clauses.
- Done when: the script contains exactly three `dotnet test` invocations, the shared filter
  appears once, and only the browser invocation carries a thread override.
- Deviation stop: if the shared filter cannot be intersected without editing an alternative,
  or if `Category!=Browser` proves not to be the exact complement of `Category=Browser` for
  this project, stop and report — the guarantee is at stake.

### Step 2 — Correct the job comment, budget the step, name a timeout as a timeout

- Preconditions: Step 1 complete.
- Files: `.github/workflows/ci.yml`
- Symbols: the `test-ui` job — its comment block, `timeout-minutes`, and the
  "Capture and verify the Test UI snapshots" step.
- Change: (a) rewrite the comment block so it states what is true — 40m23s for the capture
  in run `33310451221` against 18m52s locally; the capture was pinned to two parallel
  threads across browser and non-browser tests alike, while the project cap is four and only
  the browser selection warrants halving; verify is one test over the retained capture, not
  a second pass; UIIMP-013 split the capture accordingly. (b) Give the snapshot step its own
  `timeout-minutes`, provisionally 45, strictly below the job's. (c) Add a following step
  `if: failure()` that prints, unambiguously, that the `test-ui` step exceeded its time
  budget and that this is a budget failure and not a stale catalogue, naming the step budget
  and the job budget. Leave the job's `timeout-minutes: 75` for this step; Step 4 sets it.
- Preserved behaviour: the job's `needs`, `if`, runner, checkout, `dotnet-build`, Playwright
  cache and Chromium install steps are untouched; the snapshot step's command is unchanged.
- Forbidden: raising any `timeout-minutes`; `continue-on-error`; touching any other job;
  making the diagnostic step mask a genuine gate failure — it must print, not swallow, and
  must not change the job's conclusion.
- Negative cases: a genuine stale-catalogue failure must still fail the job and must not be
  reported as a timeout; the diagnostic text must therefore be conditional on the step's own
  timeout, not printed for every failure, or must be worded so it cannot be mistaken for a
  verdict.
- Tests: none in this repository — the workflow is proven by the PR's own run.
- Commands: none beyond `git diff`.
- Expected output: the `test-ui` job in `git diff` shows a corrected comment, a step budget,
  and one added diagnostic step.
- Done when: the workflow parses on the PR (GitHub reports no workflow syntax error) and the
  `test-ui` job starts.
- Deviation stop: if a step-level `timeout-minutes` turns out to cancel the job rather than
  fail the step, so the diagnostic never runs, stop and report rather than inventing a
  wrapper.

### Step 3 — Record the split where the parallelism model is owned

- Preconditions: Step 1 complete.
- Files: `docs/runbook.md`
- Symbols: § Locked restore, build, and test, the paragraph beginning "Test classes run in
  parallel."
- Change: add one sentence stating that the Test UI capture runs the same selection in two
  passes for this reason — its browser tests under the halved cap, the rest at the project
  cap.
- Preserved behaviour: every existing sentence and both existing command blocks stay
  exactly as they are; the four canonical command forms are unchanged.
- Forbidden: adding a new command block; restating the commands; editing any other section
  or document.
- Negative cases: none.
- Tests: none; `./scripts/Test-DocumentationLinks.ps1` runs in the `documentation` lane.
- Commands: none beyond `git diff`.
- Done when: the paragraph names the capture's split in one sentence and the section's
  commands are untouched.
- Deviation stop: if the sentence cannot be added without restating a command, stop — that
  would put a second owner beside `AGENTS.md`.

### Step 4 — Set the budget from the PR's own measurement

- Preconditions: Steps 1-3 pushed; the PR open against `dev`; the `test-ui` job has
  completed green on the PR head.
- Files: `.github/workflows/ci.yml`
- Symbols: the `test-ui` job's `timeout-minutes` and the snapshot step's `timeout-minutes`.
- Change: read the job's actual duration from its own run, then set the job's
  `timeout-minutes` to that duration multiplied by 1.5 and rounded up to the next multiple
  of 5, and the step's to the job's value minus 5. Record the measured duration, the
  40m23s capture baseline and the resulting values in the PR description and in the
  ticket's checklist progress notes. Push the amendment and let CI re-run.
- Preserved behaviour: everything from Steps 1-3.
- Forbidden: setting a value above 75; setting a value from an estimate rather than the
  measurement; setting the step budget at or above the job's.
- Negative cases: if the measured duration is not materially below the 40m23s capture
  baseline plus build and verify, the change has not delivered — stop and report rather than
  adjusting the number to look like a win.
- Tests: the PR's own `test-ui` job is the evidence.
- Commands: `gh run view <run-id> --json jobs` (or the run's web view) to read the job's
  duration; no test command.
- Expected output: a recorded duration and two `timeout-minutes` values derived from it.
- Done when: both budgets are set from the measurement and the re-run is green with the
  recorded headroom.
- Deviation stop: if the job is not materially cheaper, or is cheaper only on a retry, stop
  and report the measurements. Do not re-run until a fast one appears.

## Acceptance checks

- **Measured, on the PR's own run.** The `test-ui` job's duration on the PR head, compared
  with the 40m23s capture baseline from run `33310451221`. The capture phase alone should
  land near the sum of its two proven halves — 11-15 minutes of browser tests plus at most
  the 11m55s ceiling the same runner is measured at for a strict superset of the non-browser
  half. A duration not materially below the baseline fails this check.
- **The guarantee still holds.** The test runner re-runs the two UIIMP-005 injections
  against a retained capture and both exit non-zero: (a) perturbation — hand-edit one
  committed file under `docs/design/test-ui/pages/`, run the verify with `-SkipCapture`,
  expect a non-zero exit naming that file as stale, then restore the file; (b) orphan — add
  an unreferenced `pages/<key>--<state>.html`, run the verify with `-SkipCapture`, expect a
  non-zero exit naming it as a committed page no state generates, then remove it. Both are
  run by the test runner, not by the implementer.
- **The catalogue is untouched.** `git diff origin/dev...HEAD -- docs/design/test-ui/`
  is empty. No Razor page changed, so no snapshot may change.
- **The same tests still run.** The two capture filters' union is textually the original
  filter and their intersection is empty; the capture step's reported test count on the PR
  run equals 414 across the two halves.
- **A timeout reads as a timeout.** The diagnostic step's text is present in the workflow
  and is unambiguous about being a budget failure rather than a stale catalogue.
- **No production caller, registration, route, migration or runtime dependency is
  involved** — this change ships no application code, so those acceptance rails are
  not applicable and are recorded as such.

## Commands

Repository root is `<worktree>` for every command.

Implementer (this role runs no tests — M6; the shell guard denies them):

```powershell
dotnet build ./Pegasus.slnx --configuration Release --no-restore
git diff origin/dev...HEAD --stat
git diff origin/dev...HEAD -- docs/design/test-ui/
```

Test runner only, locally:

```powershell
pwsh ./scripts/Test-UiCatalogue.ps1
pwsh ./scripts/Update-TestUiSnapshots.ps1 -Verify
pwsh ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
```

The first is the cheap catalogue check (no build, no tests). The second is the full local
capture and verify, whose local baseline is 18m52s for the capture. The third reuses the
retained `artifacts/test-ui-capture` from the second and is the cheap way to run both
perturbation injections; it throws if no capture has been retained, so it must follow a
full run.

CI, as the timing evidence: the `test-ui` job of `repository-check` at the PR head. Read
its duration with `gh run view <run-id> --json jobs`. The acceptance is that measured job
duration compared with the 40m23s capture baseline — not a local timing and not an estimate.

## Options considered

| Option | Verdict | Reason, against the code |
| --- | --- | --- |
| **Split the capture by browser-ness; browser half stays at 2, the rest inherits the project cap of 4** | **Chosen** | The cost driver is a single command-line override applied to the wrong half. The non-browser half is a strict subset of a selection `sql-integration` runs green at the project cap in ≤11m55s; the browser half's selection is *identical* to the `browser` lane's, which is capped at 2 for a documented reason. The guarantee is untouched because the union of selections is unchanged. Smallest diff of every option that moves the number. |
| Run the capture once and verify from that capture instead of a second pass | Already true | Verify is a single `[Fact]` reading the retained capture (`TestUiSnapshotTests.cs` l.54-95), not a second suite pass. The only residual is the repeated build, which this plan removes with `--no-build`. |
| Scope the capture to pages whose Razor/CSS/JS inputs changed, using the `changes` job's outputs, with a full run on `dev` | Rejected | Architecturally incompatible. `Generate` asserts that **every** `visual` state in `catalogue.json` matched a captured response, and the orphan check then fails every committed page the partial run did not generate. Making it work needs the manifest scoped in lockstep with the diff, and even then a hand-edited snapshot on an unchanged page stops being detected on the PR — which is the defect the gate exists to catch. Large redesign, weaker guarantee. |
| Shard the capture across a runner matrix with `Invoke-TestShard.ps1` and merge the captures | Rejected | Verify needs the union of the capture on one filesystem, so every shard must upload its capture and a verify job download and merge them. That is real plumbing (the `sql-integration-coverage` pattern), triples runner minutes, and adds an artifact of every HTML response from 414 tests plus base64 image bytes. The chosen option reaches a comparable wall clock with no plumbing. Reconsider only if the measured result in Step 4 is disappointing. |
| Reuse the `browser` job's Playwright run instead of capturing browser pages again | Rejected, with arithmetic | The capture's browser half *is* the `browser` lane's selection, so this would save 11-15 minutes inside `test-ui` — but `test-ui` would then need `needs: browser` and start only after that lane finishes (~11-15 min), then run ~12 minutes of non-browser capture plus verify. Same wall clock, plus a capture artifact and a new critical-path dependency on a lane whose comment says it is deliberately off the critical path. |
| Share one host or database across captures | Rejected | A test-infrastructure redesign across `tests/**`, which this ticket does not own, for a saving the parallelism split already gets from files the ticket does own. |
| Cache the Playwright browsers | Already done | `ci.yml` § `test-ui` already has `actions/cache@v4` on `~\AppData\Local\ms-playwright`, keyed on the lock file, with a deliberately ungated idempotent install after it. |
| Raise the timeout again | Forbidden | The ticket forbids it. This plan lowers it, from a measurement. |
| Stop running `test-ui` on every PR | Not taken | The ticket names it the honest alternative *if* the job cannot be made cheaper. It can be, so this would need its own argument and would undo what UIIMP-005 delivered. |

## Failure and deviation rules

Stop and report, rather than improvising, on any of: a capture filter that cannot be
intersected without editing one of its alternatives; `Category!=Browser` not behaving as the
exact complement of `Category=Browser` for this project; the non-browser half proving
unstable at the project cap of four (report the failure and its output — do not reintroduce
an override); a step-level `timeout-minutes` cancelling the job instead of failing the step;
any diff appearing under `docs/design/test-ui/`; a measured duration not materially below
the baseline; or a need to touch any path outside Expected files, including an obvious
neighbouring fix. Dependency additions, new script switches and changes to any other CI job
are scope expansion and stop the work. A deviation is reported in the post-implementation
report with the observed values; it is never a silent redesign.

## Simplification pass

Not yet run. Before opening the PR the implementer runs the pass over this branch's own diff
(reuse, simplification, efficiency, altitude) and records findings and dispositions here
under a dated `### YYYY-MM-DD` heading, naming any unapplied finding with a reason or a
ticket. It is part of the work, not a review stage (AGENTS.md, Repository task workflow
step 4).

## Stop condition

PR_OPEN: a pull request to `dev` titled
"The test-ui gate costs 50 minutes of every build-affecting PR (UIIMP-013)", with the footer
`Kanmer: UIIMP-013`, and the ticket moved implementing → review. Do not merge the PR, do not
cross more than that one gated boundary, and do not start or take another ticket.
