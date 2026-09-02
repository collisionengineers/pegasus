# Plan — UIIMP-013: Reduce the Test UI snapshot gate critical path

## Objective

Reduce the full Test UI capture-and-verify critical path without changing the
415-test selection, generated corpus, stale/orphan guarantees, or when the gate
runs.

## Starting state

- Evidence: `files/files.md`@`7c8eb9f83852e8ef`.
- Recorded worktree: `../pegasus-worktrees/uiimp-013-test-ui-cost`.
- Recorded branch: `task/uiimp-013-test-ui-cost`.
- The recovered worktree was clean and fast-forwarded to `origin/dev` at
  `0f0e90ae44ffda7339ca2a460310deeb98121afa`.
- Recent successful snapshot steps take approximately 24–27 minutes.
- The capture applies `xUnit.MaxParallelThreads=2` to browser and non-browser
  tests alike; the repository default is four and only browser tests require
  the lower cap.
- Verify is one fact reading the retained capture, not a second capture-suite
  pass.

## Governing docs

- `docs/engineering.md`: meets the simplicity, test-support, and
  proportional-plan rules by removing one misplaced override and adding no
  dependency or abstraction.
- `docs/design/README.md`: remains unchanged; the catalogue is still generated
  from current Razor responses and hand edits remain invalid.
- `docs/runbook.md`: receives the one concurrency statement it owns.
- No ADR is needed because no architecture, runtime, dependency, or product
  contract changes.

## Required changes

1. Partition the existing capture filter into disjoint browser and non-browser
   invocations sharing one once-wiped capture directory.
2. Keep the browser cap at two; let non-browser capture inherit the existing
   project cap of four.
3. Avoid rebuilding after the first capture phase and report each phase's
   elapsed time and failure.
4. Keep the full gate on every build-affecting pull request. Correct the CI
   commentary and use a 40-minute step budget within a 45-minute job budget.
5. Make failure language honest: only an explicit stale-file assertion is a
   stale-corpus verdict; cancellation or an incomplete phase produces no
   snapshot verdict.
6. Record the split in the runbook and measure the result on three executions
   of the same PR SHA.

## Expected files

| Action | Path |
| --- | --- |
| Modify | `scripts/Update-TestUiSnapshots.ps1` |
| Modify | `.github/workflows/ci.yml` |
| Modify | `docs/runbook.md` |

## Do not modify

- `docs/design/test-ui/**`
- `src/**`
- `tests/**`
- `AGENTS.md`
- Any other CI job or script

## Constraints

- The two capture filters must be disjoint and their union must equal the
  original selection.
- The capture directory is removed and recreated once, before both phases.
- The first capture invocation remains build-capable for local use. The second
  capture and verify invocations use `--no-build`; all retain
  `--no-restore`.
- No explicit thread count is added to the non-browser phase.
- No command name, switch, dependency, test assertion, or catalogue file
  changes.
- A later pass never erases an earlier failed verification attempt.

## Ordered steps

### Step 1 — Refresh the recovered workspace

- Files: none.
- Change: fast-forward the clean ticket branch to `origin/dev` and confirm the
  worktree stays clean.
- Tests: none.
- Done when: branch and `origin/dev` resolve to the same commit before edits.
- Deviation stop: any local change, non-fast-forward history, or workspace
  mismatch.

### Step 2 — Split and time the capture

- Files: `scripts/Update-TestUiSnapshots.ps1`.
- Change: store the current base filter once; run browser capture with the
  existing two-thread override, then non-browser capture with no override;
  reuse one capture directory; add phase banners and elapsed time; add
  `--no-build` to the second capture and verify.
- Tests: fresh snapshot verify plus retained-capture negative checks.
- Done when: exactly three test invocations remain and only browser capture has
  a thread override.
- Deviation stop: any test alternative changes or the capture directory must be
  reset between phases.

### Step 3 — Correct and bound the CI job

- Files: `.github/workflows/ci.yml`.
- Change: preserve the existing trigger; correct the obsolete cost/parallelism
  commentary; set snapshot step timeout to 40 minutes and job timeout to 45;
  add failure text that never labels a generic failure as snapshot drift.
- Tests: workflow starts and completes on the PR.
- Done when: no other job changes and the failure text distinguishes an
  incomplete run from an explicit stale assertion.
- Deviation stop: the diagnostic would mask failure or path scheduling would
  weaken coverage.

### Step 4 — Record the concurrency rule

- Files: `docs/runbook.md`.
- Change: add one sentence beside the existing concurrency owner explaining the
  Test UI browser/non-browser split.
- Tests: documentation checks.
- Done when: existing command blocks remain byte-identical.
- Deviation stop: another command or convention needs changing.

### Step 5 — Verify, simplify, and open the PR

- Files: the three expected files only.
- Change: run the canonical gates, fresh Test UI verify, catalogue check,
  stale/orphan negative checks, and simplification pass; commit, push, and open
  the PR to `dev`.
- Tests: commands in Acceptance checks.
- Done when: the PR is open, its first Test UI run passes, and no catalogue
  bytes changed.
- Deviation stop: any required check fails or a file outside scope changes.

### Step 6 — Measure stable performance

- Files: `.github/workflows/ci.yml` only if the formula changes the budgets.
- Change: obtain three runs of the same PR SHA. Set the final step budget to
  1.5 times the slowest snapshot-step duration, rounded up to five minutes, and
  the job budget five minutes higher, capped at 40/45.
- Tests: all three exact-SHA runs.
- Done when: all pass, median snapshot duration is at most 22 minutes, no run
  exceeds 25 minutes, and any budget amendment is rechecked by CI.
- Deviation stop: the target is missed or the formula exceeds the cap.

## Acceptance checks

- `dotnet restore ./Pegasus.slnx --locked-mode`
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"`
- `pwsh ./scripts/Update-TestUiSnapshots.ps1 -Verify`
- `pwsh ./scripts/Test-UiCatalogue.ps1`
- Combined capture count remains 415 and the filters are a disjoint partition.
- Perturbing one committed page makes `-Verify -SkipCapture` fail naming it as
  stale; restoring it returns the tree to clean.
- Adding one orphan page makes the same command fail naming it as ungenerated;
  removing it returns the tree to clean.
- `git diff origin/dev...HEAD -- docs/design/test-ui/` is empty.
- Three exact-SHA CI runs meet the performance target.

## Commands

Use the commands listed in Acceptance checks from the recorded worktree. CI
measurement uses `gh run view <run-id> --json jobs` and exact-SHA reruns.

## Failure and deviation rules

Stop rather than weaken coverage, raise a timeout, discard a failed run, change
another CI lane, or touch a path outside Expected files. Record every command
with cwd and exit code in the post-implementation report.

## Simplification pass

### 2026-09-02

- Reuse: retained the existing capture filter, project concurrency cap, browser
  cap, capture directory, and CI trigger.
- Simplification: one local helper owns the three otherwise duplicated
  `dotnet test` invocations; no public switch, dependency, or second path
  taxonomy was introduced.
- Efficiency: only the first phase may build; non-browser capture inherits the
  proven higher cap.
- Altitude: no application/test policy or neighbouring CI lane changed.
- Disposition: no further behaviour-preserving simplification found.

## Stop condition

PR_OPEN: a pull request to `dev` titled
"Reduce the Test UI snapshot gate critical path (UIIMP-013)", with footer
`Kanmer: UIIMP-013`, and the ticket moved implementing → review. Do not merge
the PR or start another ticket.
