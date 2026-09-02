# Checklist — UIIMP-013

One tickable box per ordered plan step and acceptance check. Append progress notes; do not
rewrite ticked boxes.

- [ ] Step 1 — In `scripts/Update-TestUiSnapshots.ps1`, hold the capture filter in one variable and run it as two sequential invocations: `&Category=Browser` keeping `-- xUnit.MaxParallelThreads=2`, then `&Category!=Browser` with no thread override.
- [ ] Step 1 — Give the second capture invocation and the verify invocation `--no-build`; leave the first as the one that builds, and leave `--no-restore` on all three.
- [ ] Step 1 — Print a phase banner with elapsed time around each of the three phases, and name the failing phase in each non-zero exit throw.
- [ ] Step 1 — Confirm by reading the diff that the capture directory is still wiped exactly once, before the first half and never between the halves.
- [ ] Step 2 — Rewrite the `test-ui` comment block in `.github/workflows/ci.yml` so it states the truth: the capture was pinned to two threads across browser and non-browser tests alike, the project cap is four, and verify is one test over the retained capture rather than a second pass.
- [ ] Step 2 — Give the snapshot step its own `timeout-minutes` strictly below the job's.
- [ ] Step 2 — Add an `if: failure()` step that names a budget failure as a budget failure and not a stale catalogue, without changing the job's conclusion or masking a genuine gate failure.
- [ ] Step 3 — Add one sentence to `docs/runbook.md` § Locked restore, build, and test recording that the capture applies the same browser/non-browser split; leave both command blocks untouched.
- [ ] [pre-review] `dotnet build ./Pegasus.slnx --configuration Release --no-restore` succeeds; record the exact command, cwd and exit code.
- [ ] [pre-review] `git diff origin/dev...HEAD -- docs/design/test-ui/` is empty — no Razor page changed, so no snapshot may change.
- [ ] [pre-review] The two capture filters' union is textually the original filter and their intersection is empty; no alternative was edited or dropped.
- [ ] [pre-review] `AGENTS.md` is unchanged, because no documented command name or switch changed (rule 24); if either did, lines 168-174 are updated in this same PR and the deviation is reported.
- [ ] [pre-review] Run the simplification pass over this branch's diff and record the dated findings and dispositions under the plan's `## Simplification pass` heading.
- [ ] [pre-review] Open the PR to `dev` titled "The test-ui gate costs 50 minutes of every build-affecting PR (UIIMP-013)" with the footer `Kanmer: UIIMP-013`.
- [ ] Step 4 — Read the `test-ui` job's actual duration from the PR's own run, set the job's `timeout-minutes` to that duration × 1.5 rounded up to the next multiple of 5 and the step's to five below it, and record the measurement, the 40m23s baseline and both values in the PR description and in these progress notes.
- [ ] [pre-review] Acceptance — the measured `test-ui` duration on the PR head is materially below the 40m23s capture baseline; if it is not, stop and report the measurements rather than adjusting the number.
- [ ] [pre-review] Acceptance — the capture's reported test count across the two halves is 414, unchanged.
- [ ] [post-merge] Runner-only — `pwsh ./scripts/Test-UiCatalogue.ps1` passes, and `pwsh ./scripts/Update-TestUiSnapshots.ps1 -Verify` completes and retains a capture.
- [ ] [post-merge] Runner-only — perturbation injection: hand-edit one file under `docs/design/test-ui/pages/`, run the verify with `-SkipCapture`, confirm a non-zero exit naming that file as stale, then restore the file.
- [ ] [post-merge] Runner-only — orphan injection: add an unreferenced `pages/<key>--<state>.html`, run the verify with `-SkipCapture`, confirm a non-zero exit naming it as a committed page no state generates, then remove it.
- [ ] [pre-review] Stop at the plan's stop condition: PR open to `dev`, ticket moved implementing → review. Do not merge; do not start another ticket.

Not applicable, recorded as a decision: no production caller, registration, route or
composition entry (this change ships no application code); no packaged runtime dependency;
no schema change, migration, grant or rollback.

## Progress notes

Append with `set_ticket_doc(doc: "checklist", append: true)`.
