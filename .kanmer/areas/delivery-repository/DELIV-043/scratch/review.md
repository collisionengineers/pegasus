# Review record — DELIV-043 (PR #653)

Reviewer: independent agent (Claude Code); did not implement this ticket.
Head SHA reviewed: `8cdbb3062913f8be335c46b72f75c07bee803090`
Base: `dev` (branch cut at `659cec77`; `dev` head at review time `3f0cb45ed`).
Diff reviewed: `git diff origin/dev...origin/task/deliv-043-ci-concurrency-preflight` — `.github/workflows/ci.yml` only, 20 insertions, 7 deletions, one commit.

## The three workflow questions

### 1. Did the plan miss anything implied by the ticket?

No. The ticket's Approach lists three changes (workflow-level `concurrency`; the
three cheap invariant jobs added to `needs` on the five heavy lanes with their
`if` unchanged; `sql-integration-coverage` also skipping when the shards were
skipped) and the plan's "Required changes" reproduces all three literally, with
the same out-of-scope list (`push: dev` CI, the duplicate Azure-plan invocation,
lane-content work). The plan's acceptance checks mirror the ticket's three
verification bullets. The plan named the governing doc (`docs/engineering.md`
"Branches and delivery") and the local YAML parse as the pre-push check, which
is the only executable check available for a workflow file outside GitHub.

One thing the plan could have stated and did not: the second-order effect of
`concurrency` on `sql-integration-coverage`, whose `always()` makes it eligible
to run even when the run is cancelled (finding F1). It is a consequence of the
new concurrency block, not a gap in the ticket's stated scope, and it is not
reachable on a run that gates anything.

### 2. Did the implementation miss anything in the plan?

No. Every planned edit is present and nothing else is:

- `concurrency.group: ${{ github.workflow }}-${{ github.event.pull_request.number || github.ref }}`, `cancel-in-progress: ${{ github.event_name == 'pull_request' }}` at workflow level (`ci.yml:15-17`), exactly as planned.
- `needs: [changes, documentation, local-development-scripts, reference-data]` on `infrastructure`, `unit`, `sql-integration`, `browser`, `test-ui`; each lane's `if` is byte-identical to `dev` (`needs.changes.outputs.infrastructure == 'true'` / `...build == 'true'`).
- `sql-integration-coverage`: `if: always() && needs.changes.outputs.build == 'true' && needs.sql-integration.result != 'skipped'`.
- No job renamed, added, merged or removed; no step, `runs-on`, `timeout-minutes`, shard count, cache key or script call changed. The "Do not modify" list is respected.
- The commits/PR/branch recorded on the ticket match the reviewed head.

### 3. Did the simplification pass run with honest dispositions?

Substantively yes, with one labelling caveat (finding F5). The plan and the
post-implementation report both record "n/a — workflow-only", and AGENTS.md
step 4 only defines the "n/a" wording for a docs-only task, so the category is
invented rather than quoted. I therefore ran the simplification lenses over the
diff myself:

- **Reuse / duplication.** The same four-element `needs` list appears five
  times. GitHub Actions does not support YAML anchors or aliases in workflow
  files, so the only dedup is a new aggregator `preflight` job that the heavy
  lanes depend on. That adds a job and a scheduling round trip and is excluded
  by the ticket's own explicit constraint ("no job is renamed, merged or
  removed", because Kanmer's generated workflow will replace this file). The
  duplication is the correct call at this altitude.
- **Efficiency.** The change is net-negative on runner minutes; the only cost is
  added latency, measured below (F4).
- **Altitude.** Three additive edits in one file with comments that state the
  measurement behind each. No scope creep.

Nothing behaviour-preserving was left unapplied, so the disposition is honest
even though the heading should have read "considered and rejected: a single
aggregator `preflight` job, for the ticket's no-new-job constraint" rather than
"n/a".

## Correctness scrutiny

**`needs` + `if` on the dependants really gates.** A dependant's `if` carries an
implicit `success()` over its `needs` unless the expression uses `always()`,
`!cancelled()` or `failure()`. The five heavy lanes keep plain
`needs.changes.outputs.* == 'true'` conditions, so a failed `documentation`,
`local-development-scripts` or `reference-data` skips all of them. Verified in
the file that the three cheap jobs are unconditional — they declare no `needs`
and no `if` (`ci.yml:79-125`) — so they can never themselves be *skipped*, which
rules out the opposite hazard (a skipped `needs` job silently skipping its
dependants and hiding the heavy lanes on a legitimate code change). This is the
single most load-bearing fact in the diff and it holds.

**`sql-integration-coverage`.** `always()` is still needed (a failing shard must
still get its coverage reported). With `build == 'true'` and a cheap job failing,
`sql-integration` is `skipped`, so the new `needs.sql-integration.result !=
'skipped'` clause is what stops the coverage job from running and failing on a
missing artifact download. The path-skip case (`build == 'false'`) is still
caught by the pre-existing `build == 'true'` clause, so the new clause is
additive, not a replacement. Correct.

**Can `cancel-in-progress` cancel anything on `main`?** No. The workflow triggers
are `pull_request` and `push: branches: [main]`. A `pull_request` run's group is
`repository-check-<PR number>`; a `main` push run's group is
`repository-check-refs/heads/main`. The two groups can never collide, and a
`main` push run evaluates `cancel-in-progress` to `false`, so it cancels nothing
and no PR run is a member of its group. See F2 for the one residual nuance
(pending, not in-progress, `main` runs).

**Is the group stable for `push`?** Yes. `github.event.pull_request.number` is
empty on a push, so the fallback `github.ref` (`refs/heads/main`) is used; it is
constant for every push to `main`. On `pull_request` the number is stable across
`opened`/`synchronize`/`reopened`, which is exactly the set that needs to
collapse. Expressions are permitted in both `concurrency.group` and
`cancel-in-progress`, and the `github` context is available there; the file
parsed and dispatched on GitHub (run 33796752315 exists), which is the executable
proof that the block is valid.

**Could this change have caused the `test-ui` timeout on attempt 1?** No, and the
argument is from the diff. The diff touches `test-ui` in exactly one line — its
`needs:` — and leaves the job's `runs-on`, `timeout-minutes: 40`, the step-level
`timeout-minutes: 35`, and every step (`checkout`, `dotnet-build`, the Playwright
cache and install, `Update-TestUiSnapshots.ps1 -Verify`) byte-identical. The only
observable effect on that job is that it started 3 seconds later
(19:30:46 against a `changes` completion of 19:30:42). A 3-second start shift
cannot consume a 35-minute step budget. The job's own comment records a
historical 40m23s capture (run 33310451221) against "recent 24-27 minute" ones,
and attempt 2 of this very run took 28m47s — the capture step is chronically
close to its budget, which is a pre-existing variance owned by UIIMP-013, not a
regression introduced here. The job's own `if: failure()` step ran and succeeded,
stating the failure is not a stale-catalogue verdict.

**`docs/engineering.md` wording.** "Green means every `repository-check` job for
the PR's head revision succeeded or was path-skipped." A lane skipped because an
upstream cheap job failed is neither "succeeded" nor "path-skipped", so the
existing sentence already returns *not green* for the new state — the
implementer's reading is the literal one, not an interpretation. Additionally,
the only way to reach that state is for a cheap job to be *failed*, so the run
is red under any reading and no false green is constructible. This PR does not
need to carry a docs change; a clarifying half-sentence is an optional
follow-up, recorded as F3 rather than a required change.

## Findings and dispositions

| # | Severity | Finding | Disposition |
|---|---|---|---|
| F1 | Low | `sql-integration-coverage` keeps `always()`, which now becomes reachable under cancellation: when concurrency cancels a superseded run, the shards are `cancelled` (not `skipped`), so the new clause does not exclude them and the coverage job will still start and fail downloading artifacts. `!cancelled()` in place of `always()`, or an added `!= 'cancelled'`, would be tighter. | Accept, non-blocking. It can only occur on a run that concurrency has already superseded, whose checks belong to a stale head SHA and gate nothing; it costs under a minute of ubuntu time. Recorded here as a follow-up candidate for the next CI ticket; not worth a second ~90-runner-minute CI cycle on this PR. |
| F2 | Low | The comment says "A push to `main` is never cancelled". Precisely, `cancel-in-progress: false` never cancels an *in-progress* run, but GitHub does cancel *pending* runs in a group when a newer run queues behind the running one. Two `main` pushes landing while a third run is in flight would leave the middle commit without a completed `repository-check`. | Accept, non-blocking. `main` is only ever updated by a single exact-SHA promotion with explicit `MERGE AUTH GRANTED` (`docs/engineering.md` "Branches and delivery"), so two queued `main` runs is not a reachable release route. The comment is accurate for the route the repository actually uses. |
| F3 | Low | `docs/engineering.md` "Green means … succeeded or was path-skipped" does not name the new "skipped because an upstream invariant job failed" state explicitly. | No change required in this PR. The literal sentence already excludes such a lane, and the state is only reachable with a failed job in the same run, so it cannot produce a false green. Optional wording follow-up. |
| F4 | Informational | Added latency on green runs. | Measured, not modelled: on run 33796752315 the heavy lanes started at 19:30:45-19:31:06 against `changes` completing at 19:30:42 — about 3 seconds, because `changes` (53s) was already the slowest of the four preflight jobs (`local-development-scripts` 13s, `reference-data` 23s, `documentation` 29s). Worst case is bounded by `documentation`'s own 10-minute timeout and is dominated by the ~85 runner-minutes the change avoids. Accepted. |
| F5 | Low | The simplification heading reads "n/a — workflow-only", a category AGENTS.md does not define (it defines "n/a — docs-only"). | Accept. I ran the lenses independently (see question 3) and found only the aggregator-`preflight` alternative, which the ticket consciously excludes; no unapplied behaviour-preserving finding is being hidden. Recommend the wording "considered and rejected" for the next workflow-only ticket. |
| F6 | Informational | The ticket's verification bullets 1 (a failing `documentation` starts zero heavy lanes) and 2 (a second push cancels the older run) are not empirically demonstrated: only one run exists on this branch, and it was green. | Accept. Both are standard, documented GitHub Actions semantics, verified above against the actual job definitions; forcing a deliberate red run or a throwaway double push to prove them would burn the runner minutes this ticket exists to save. Bullet 3 (a green PR shows the same job set with every heavy lane `success`) is fully demonstrated by run 33796752315. |
| F7 | Informational | The follow-ups (`push: dev` CI, the duplicate Azure-plan invocation in `infrastructure`) are recorded only in the ticket's prose, not as board tickets. | Accept for this PR; noted so they are carried into the next CI grooming pass. `DELIV-025` already covers path-relevant lane selection. |

No finding blocks the merge.

## CI evidence

Run **33796752315** (`repository-check`, event `pull_request`, head
`8cdbb3062913f8be335c46b72f75c07bee803090`), 2 attempts, overall conclusion
`success`.

Attempt 1 — verified from `gh api .../attempts/1/jobs`:

| Job | Conclusion | Started | Completed |
|---|---|---|---|
| local-development-scripts | success | 19:29:49 | 19:30:02 |
| changes | success | 19:29:49 | 19:30:42 |
| reference-data | success | 19:29:51 | 19:30:14 |
| documentation | success | 19:29:53 | 19:30:22 |
| infrastructure | success | 19:30:45 | 19:31:16 |
| unit | success | 19:30:45 | 19:34:13 |
| test-ui | **failure** | 19:30:46 | 20:10:35 |
| sql-integration (3) | success | 19:30:47 | 19:41:32 |
| sql-integration (2) | success | 19:30:54 | 19:43:01 |
| browser | success | 19:31:05 | 19:44:08 |
| sql-integration (1) | success | 19:31:06 | 19:44:31 |
| sql-integration-coverage | success | 19:44:34 | 19:44:52 |

Ordering: every one of the four preflight jobs completed (19:30:02-19:30:42)
before any heavy lane started (earliest 19:30:45). The gate is demonstrated, not
asserted. `sql-integration-coverage` ran after all three shards and passed, so
the rewritten `if` still admits the normal case.

Attempt 1's `test-ui` failure is a step timeout: its step list shows `Capture and
verify the Test UI snapshots` as the only `failure` (35-minute step budget) with
the following `Explain an incomplete Test UI result` step succeeding — the
workflow's own statement that this is not a stale-catalogue verdict. Classified
as infrastructure/timeout, not an assertion; see the diff argument above.

Attempt 2: `gh run rerun --failed` re-ran only `test-ui`, which passed in 28m47s
(23:16:18-23:45:05). Every retained job stays `success`. Run conclusion
`success`.

`gh pr checks 653`: 12 checks, all `pass` (browser, changes, documentation,
infrastructure, local-development-scripts, reference-data, sql-integration 1-3,
sql-integration-coverage, test-ui, unit).

`gh pr view 653`: `state OPEN`, `isDraft false`, `mergeable MERGEABLE`,
`mergeStateStatus CLEAN`, `headRefOid 8cdbb306…`, `baseRefName dev`.

Base drift: `dev` advanced from `659cec77` to `3f0cb45ed` (PLAT-068 and
predecessors) after the branch was cut, but
`git diff 659cec77 origin/dev -- .github/workflows/ci.yml` is empty, so the
merged `ci.yml` is byte-identical to the reviewed file and the run's evidence
survives the merge unchanged. `dev` has no branch protection
(`repos/.../branches/dev/protection` returns 404), consistent with
`docs/engineering.md` recording server-side rulesets as intentionally out of
scope, so no required-check contexts depend on the job names — and none were
renamed anyway.

## Verdict

**Approved.** The plan covers everything the ticket implies, the implementation
covers everything the plan requires and nothing more, and the simplification
disposition is honest (F5 is a labelling nit, not a hidden finding). The gating
semantics are correct against the actual job definitions, the concurrency group
cannot touch `main`, and CI is green at the reviewed head with the preflight
ordering demonstrated on the wire. Merging with a merge commit.

## Merge

Merged into `dev` by `gh pr merge 653 --merge` at 2026-09-03T23:51:04Z.
Merge commit SHA: `f479a94849bb3a1208f06d5823687d7bff30566f` (PR #653, head `8cdbb3062913f8be335c46b72f75c07bee803090`).
Branch and worktree left in place; proof, closeout and `main` untouched.
