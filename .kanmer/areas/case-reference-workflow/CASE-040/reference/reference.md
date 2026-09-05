# Review record — CASE-040 (PR https://github.com/collisionengineers/pegasus/pull/666)

Reviewed head: `51e7fe5c78f4643d549b86ce30051d7d3f01edcc`
(branch `task/case-040-sign-off-engineer-eva`; the branch moved past the
post-implementation report's recorded head `d061171c8` through the
`origin/dev` merge, the migration regeneration to
`20260905010654_CaseSignOffEngineer` and a snapshot regeneration).

Reviewers: gpt-5.6-terra xhigh (independent read, detached review worktree
`.worktrees/case-040-review`); Claude Opus (dispositions, independent
verification). Built by gpt-5.6-sol.

**Verdict: REQUEST CHANGES.** Two confirmed defects contradict the ticket's
own acceptance conditions, plus one concurrency regression and one
overstated coverage claim. Not merged; the ticket returns to the
implementer.

## Findings and dispositions

| # | Severity | File:line | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker | `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs:224`, `src/Pegasus.Infrastructure/Persistence/EvaSubmissionStore.cs:330` | The D47 transition writes `State`/`Version` and clears the edit lease directly, checking only the EVA state policy and the resolved Sign-off Engineer. It does not apply `StartCaseWork`'s preconditions (`CaseLifecycle.cs:185-197`): state Review **and** `AssignedEngineerId is not null`, plus `CaseEngineerEligibilityPolicy.RequireEligibleAsync`. Because the default sign-off designation resolves independently of assignment, a Review case with **no assigned Engineer** advances to `ReportPreparation` (With Engineer) on Download ZIP or Send via API — reachable by a direct POST to the Export `Bundle` / Eva/Send `Submit` handlers. A case whose assigned Engineer was since disabled likewise advances without the eligibility re-check. D47 says the send "performs the existing `StartCaseWork` transition"; this is a second, weaker implementation of that transition's rule. | **Fix.** Hold the start-work precondition once in Core (extend the EVA state policy or extract the `StartCaseWork` guard) and apply it inside the same locked section as the transition, with a port-level test that an unassigned Review case is refused by both routes. |
| 2 | blocker | `src/Pegasus.Infrastructure/Persistence/EvaSubmissionStore.cs:248-271` | `RecordSubmissionAsync` runs **after** `transport.SubmitInstructionAsync`, and this PR adds three new throw paths to it: `EvaSubmissionPolicy.StateAfterSend`, `ResolveRequiredSignOffEngineer`, and a new `CaseVersionConflictException` when the locked workflow version has moved. On `dev` that method had no version check and no state re-check. EVA has no idempotency: if any of the three fires, EVA holds the instruction while Pegasus records **no** `EvaSubmissions` row and **no** action-history row, so the operation key stays replayable into a second claim. The window spans an HTTP round trip carrying every case photograph. Conduct rule 11 — a concurrency result is never discarded. | **Fix.** Record the attempt durably across the transport boundary (reserve the row before the call, or persist the outcome and surface the conflict as a recorded failure) so a concurrent version change never loses a completed EVA submission. |
| 3 | blocker | `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs:524-543` | `SignOffEngineerId` is set only on the non-archived return path; the archived branch returns `workflow with { Archive = … }` and silently drops it. The plan (Step 3) requires the field in **both** workflow projections. An archived case therefore reads back `SignOffEngineerId = null`, and every consumer — the ribbon, Overview, the handoff view model, `EfAssessmentReportProjectionSource` and `AssignCaseEngineer`'s `current.SignOffEngineerId` — silently falls back to the assigned Engineer or the default designation instead of the persisted selection. | **Fix.** Set `SignOffEngineerId` once on the initial record initializer so both branches carry it, and add a projection test over an archived workflow. |
| 4 | should-fix | `post-implementation-report/post-implementation-report.md` | The report is bound to head `d061171c8` and is now inaccurate at `51e7fe5c7`: the migration is `20260905010654_CaseSignOffEngineer` (not `20260904185256_`); the recorded snapshot byte sizes (64,565 / 25,888) are stale (actual 67,885 / 26,237); and the claim that no unowned snapshot changed is false — `case-details--conflict.html` is in the diff. | **Fix (report only).** Refresh the report at the final head. **The reviewer's proposed remedy of reverting `case-details--conflict.html` is rejected:** CI runs the *unscoped* `Update-TestUiSnapshots.ps1 -Verify` (`.github/workflows/ci.yml:281`), so a stale conflict snapshot would fail the gate; the EPIC-012 Build policy explicitly permits a lane to edit `docs/design/test-ui/**` that its plan names. Regenerating it is correct — only the report's claim is wrong. |
| 5 | should-fix | `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs:1498-1512` | The re-send test asserts state, version, transport call count and `EvaSubmissions` row count, but never asserts a second `eva_api_submitted` action-history row nor the `AssignedEngineerId` / `SignOffEngineerId` payload. `grep -n "eva_api_submitted\|SignOffEngineerId\|AssignedEngineerId"` over that file returns nothing. The plan's acceptance condition ("each successful export and API attempt records the selected Engineer and Sign-off Engineer identities in its route-specific history payload") and the report's matching claim are therefore untested. | **Fix.** Assert the second `eva_api_submitted` row and both identity values in its `afterJson` for both routes. |
| 6 | nit (reviewer: should-fix) | `src/Pegasus.Web/Pages/Cases/Workflow.cshtml.cs:148` | `"The Sign-off Engineer was set."` is an inline literal rather than an `OperatorLabels` member. | **Rejected, with reason.** Every sibling handler in the same file passes an inline confirmation literal (lines 41, 63, 93, 124, 195). Moving only the new one gives one concept two homes, against the one-list rule; moving all six is a separate change to CASE-038/PLAT-069-shared code and is not this ticket's scope. |
| 7 | nit (wrapper) | `src/Pegasus.Core/Lifecycle/CaseLifecycle.cs:159` | `CaseSignOffEngineerResolver.Resolve` ends in `eligibleProfiles.SingleOrDefault(p => p.IsDefault)`, which throws an opaque "Sequence contains more than one matching element" if two accounts ever carry the default designation. | **Accept risk.** PLAT-068 owns single-designation enforcement; the throw fails closed rather than picking arbitrarily. |

Reviewer questions answered clear (independently re-checked): the shared
`_EvaHandoff` partial has exactly two Razor callers and every form targets a
real named handler (`AssignEngineer`, `SetSignOffEngineer` on
`/Cases/Workflow`, `Bundle` on `/Cases/Documents/Export`, `Submit` on
`/Cases/Eva/Send`); no explanatory copy is introduced (the disabled API route
uses the existing `.gated` + `data-condition` convention, and the only new
prose is a Razor server-side `@* *@` comment); `Download EVA package` survives
only as a negative assertion (`CaseDetailsWebTests.cs:743`); no
`ReviewedByStaff` / `RequireStaffImageReview` member survives outside frozen
historical migrations and two D44 negative assertions; the migration adds one
nullable column and drops one filtered index, needing no new grant; the eleven
`id="section-"` hosts and `class="case-sticky"` are present in the regenerated
Case page.

## Commands run in the review checkout (`.worktrees/case-040-review`)

```
git worktree add --detach … origin/task/case-040-sign-off-engineer-eva   — exit 0; HEAD = 51e7fe5c78f4643d549b86ce30051d7d3f01edcc
dotnet restore ./Pegasus.slnx --locked-mode                              — RESTORE_EXIT=0
dotnet build ./Pegasus.slnx --configuration Release --no-restore         — BUILD_EXIT=0, 0 warnings, 0 errors
dotnet test tests/Pegasus.Core.Tests --configuration Release --no-build  — CORE_EXIT=0, 1245 passed
dotnet test tests/Pegasus.ArchitectureTests --configuration Release --no-build — ARCH_EXIT=0, 100 passed
dotnet test tests/Pegasus.IntegrationTests --configuration Release --no-build
  --filter "FullyQualifiedName~CaseWorkflowPersistenceTests|~EvaSubmissionPersistenceTests|
            ~CustodyOutboxIntegrationTests|~CaseDetailsWebTests|~IntakePersistenceIntegrationTests|
            ~AssessmentPersistenceIntegrationTests|~CaseWorkflowWebTests"
  -- xUnit.MaxParallelThreads=2                                          — INT_EXIT=0, 173 passed, 1 skipped
pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1                 — GRANTS_EXIT=0, 94 migrations checked
```

That scope covers the change: the filter names every test class in
`git diff --name-only origin/dev...HEAD` under `tests/`, the two Core suites
cover the new resolver and EVA state policy, and the grant script covers the
new migration. It does not cover findings 1, 2, 3 or 5 — those are gaps the
existing tests do not exercise, which is why they were found by reading.

Snapshot artifacts opened at this head:
`docs/design/test-ui/pages/case-details--default.html` 67,885 bytes, begins
`<!DOCTYPE html>`, one `class="case-sticky"`, eleven distinct `id="section-…"`
hosts, no `<img src="#">`; `case-eva-send--default.html` 26,237 bytes, begins
`<!DOCTYPE html>`, carries the Sign-off Engineer field;
`case-details--conflict.html` 41,054 bytes, same markers.

CI was not gated: the PR was not approved, so no merge was attempted. The
run for this head was still in progress (SQL integration, browser and Test UI
checks) at review time.

---

# Review record — CASE-040 (PR https://github.com/collisionengineers/pegasus/pull/666) — re-review

Reviewed head: `bfd0893943f2b81c4446b7756860c02f415a17b7`
(branch `task/case-040-sign-off-engineer-eva`; the branch had not moved —
`git rev-parse HEAD` in the detached review worktree
`.worktrees/case-040-review` equals the head named in the packet).

Reviewers: gpt-5.6-terra xhigh (independent read, reading-only, same detached
worktree); Claude Opus (dispositions, independent verification, CI gate).
Built by gpt-5.6-sol. Round 2, after the fix commit `bfd089394` on top of the
first review's head `51e7fe5c7`.

**Verdict: REQUEST CHANGES.** Three of the five earlier findings are closed and
verified. One earlier finding (5, report accuracy) was claimed fixed but is
not. Two blockers remain: a CI-red browser test this PR's own label rename
broke, which no local lane filter covered, and a new double-claim path the
blocker-2 fix opened on the automatic worker. Not merged.

## Re-review status of the first round's findings

| # | Earlier finding | Status at `bfd089394` | Evidence |
| --- | --- | --- | --- |
| 1 | D47 transition skipped `StartCaseWork`'s preconditions | **CLOSED** | `CaseEngineerEligibilityPolicy.RequireStartCaseWorkAsync` (`src/Pegasus.Core/Lifecycle/CaseLifecycle.cs:200-212`) holds both preconditions in one Core place; `StartCaseWork` now calls it instead of its own inline check (`:182-195`), and both stores call the same method, guarded by `resultingState != currentState` so it never runs on a re-send (`EvaHandoffStore.cs:159-168`, `EvaSubmissionStore.cs:275-282`). `EfCaseEngineerEligibility` reads only `Users`/`UserRoles`/`Roles`, so the extra context inside the `Serializable` `CaseWorkflows` row lock cannot self-deadlock. Port-level refusals for both routes, with the exact `StartCaseWork` and eligibility messages and `evaTransport.CallCount == 0`, at `CustodyOutboxIntegrationTests.cs:1324, 1332, 1369, 1377`. |
| 2 | A completed EVA submission could be lost on a post-transport re-check failure | **PARTLY CLOSED — see new blocker B** | The manual, same-operation-key case is genuinely fixed: the four checks run in a local `try`, the failure is captured, the `EvaSubmissions` row and the `eva_api_submitted` history row are added unconditionally, `SaveChangesAsync`/`CommitAsync` run, the workflow write is skipped only when a failure was captured, and the exception is re-thrown after the commit via `ExceptionDispatchInfo` — so nothing is swallowed (`EvaSubmissionStore.cs:266-379`). The version-race test drives the exact window through a transport that mutates `CaseWorkflows.Version` by raw SQL from inside the call, and asserts the thrown conflict, one submission row with the real EVA identifiers, one history row, one transport call, and an exact-key replay that does not call the transport again (`CustodyOutboxIntegrationTests.cs:1639-1690`). What it does **not** close is the automatic worker's own retry, which uses a different attempt key — blocker B below. |
| 3 | `SignOffEngineerId` dropped on the archived read path | **CLOSED** | `EfCaseQueryStore.MapWorkflow` sets it once in the initial object initializer, before the archived/non-archived branch (`EfCaseQueryStore.cs:510-516`); the archived branch inherits it. `CaseWorkflowPersistenceTests.ArchivedCaseProjectionRetainsPersistedSignOffEngineer` asserts the archived projection carries the persisted value, distinct from `AssignedEngineerId`. |
| 4 | Re-send test never asserted the second history row or the identity payload | **CLOSED** | Export route: second `eva_bundle_exported` row keyed on the re-send's own operation key, with the exact `assignedEngineerId`/`signOffEngineerId` GUIDs from its `afterJson` (`CustodyOutboxIntegrationTests.cs:1538-1555`). API route: `eva_api_submitted` count 2 and the same two GUIDs on the second row (`:1607-1626`). No earlier assertion was weakened to make room. |
| 5 | Post-implementation report stale at the final head | **OPEN — see finding C** | |

## Findings and dispositions (this round)

| # | Severity | File:line | Finding | Disposition |
| --- | --- | --- | --- | --- |
| A | blocker | `tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs:467` | This PR renames the Send page's export button from `Download export` to `Download ZIP` (`Pages/Cases/Eva/Send.cshtml` loses its own cluster and now composes `_EvaHandoff`, which prints `OperatorLabels.CaseWorkspace.DownloadZip`). `git grep "Download export"` at this head returns exactly one hit — that test line — so the only assertion of the old name was never updated. `ExportByKeyboardAsync` therefore cannot find the button and the journey fails. This is **CI-red at the reviewed head**: run `33939704373`, job `browser` (`101234699951`), `OperatorJourneyTests.CustodyRecoveryAndExportAreKeyboardUsableWithoutInternalIdentifiersOrExternalClaims [FAIL]` at `OperatorJourneyTests.cs:468`, `Failed: 1, Passed: 124`; the same failure ended runs `33936115163` (`51e7fe5c7`) and `33916215696` (`d061171c8`) in both the `browser` and `test-ui` jobs. The class is outside the ticket's `files` document, which is why no local filter ran it — but the breakage is this ticket's. Two further breaks sit behind the rename in the same journey: the case it drives is in `Review` with **no assigned Engineer** (the failure text prints `Engineer / Unassigned`), which finding 1's own fix now refuses on a first send; and it exports twice and asserts the second archive is byte-identical, which D47 changes by moving the case to `ReportPreparation` on the first export. | **Fix.** Update `OperatorJourneyTests` for the new label, assign an eligible Engineer before the export so the D47 transition's preconditions are met, and re-express the second export as the D36 re-send it now is. The controller should widen the ticket's owned paths to include `tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs` — it is a browser test, not tooling, so it is not covered by the tooling no-touch rule. Re-run it locally (`--filter "FullyQualifiedName~OperatorJourneyTests" -- xUnit.MaxParallelThreads=2`) before pushing. |
| B | blocker | `src/Pegasus.Core/Eva/EvaSubmissionWorkItem.cs:165-180`, `src/Pegasus.Infrastructure/Persistence/EvaSubmissionStore.cs:296-379` | Blocker 2's fix makes the post-transport `CaseVersionConflictException` reach the caller **after** the commit — correct for the manual routes, but the automatic worker has no branch for it. Its terminal "no longer applicable" filter names only `EvaHandoffStateException`, `EvaSubmissionNotEnabledException` and `EvaSignOffEngineerRequiredException`, and its retryable filter names only `IOException`, `InvalidDataException`, `TimeoutException` and `UnauthorizedAccessException`, so the conflict escapes `EvaSubmissionWorkItem.ExecuteAsync` uncaught and the `ExternalWorkItems` row stays `Processing`. `EfEvaSubmissionWorkStore.ClaimProcessingAsync` re-claims a `Processing` row once its lease expires and increments `AttemptCount` (`EfEvaSubmissionWorkStore.cs:61-104`), and the next attempt runs under `EvaSubmissionPolicy.AttemptOperationKey(operationKey, attemptCount)` — a **different** key, which the exact-key replay guard (`EvaSubmissionStore.FindReplayAsync`) does not match. The delivered-submission uniqueness path that used to catch this was deliberately removed in Step 3b. EVA has no idempotency, so the case is submitted a second time and a second claim is created, even though the first delivery is now durably recorded. This is new in this PR: `dev`'s `RecordSubmissionAsync` had no post-transport version check, so no such exception existed. `StateAfterSend` returns `Review` unchanged for the automatic trigger, so `RequireStartCaseWorkAsync` is never reached on that path and the conflict is the only escaping case — which is precisely why it needs its own answer rather than a wider catch. | **Fix.** Make a post-delivery local-transition failure terminal for the automatic work item while still surfacing it — either add `CaseVersionConflictException` to the work item's terminal branch (a delivery that reached EVA is an answer, not a fault, exactly as the state and signatory refusals already are), or refuse a fresh automatic transport call for a case that already carries a delivered `EvaSubmissions` row. Prove it with an automatic-path version-race test: one transport call, one submission and one history row, and a terminal (not re-claimable) work row after the lease expires. |
| C | should-fix | `post-implementation-report/post-implementation-report.md`, `checklist/checklist.md:37` | Finding 5 is not closed. The report's appendix asserts "This report is now written at the fixes' own head", but the body was not rewritten: it still names `Head SHA: d061171c8e2cab6066102af4d8a96f010b215e55`; still names the migration `20260904185256_CaseSignOffEngineer` in both "Files changed" and "Behaviour delivered" (the file at this head is `20260905010654_CaseSignOffEngineer`); still records the snapshots as 64,565 and 25,888 bytes (measured at this head: `case-details--default.html` **67,885**, `case-eva-send--default.html` **26,237**, `case-details--conflict.html` **41,054**); and still claims "No `case-details--conflict.html` … change is in the diff — those three non-owned snapshots … were reverted", which `git diff --stat origin/dev...HEAD` contradicts. The checklist item "Should-fix 5: post-implementation report refreshed at the fixes' head" is ticked on that false basis. The first round's disposition stands: regenerating the conflict snapshot is *correct* (CI runs the unscoped verify), so only the report's claim is wrong. | **Fix (report only).** Rewrite the report body against `bfd089394` — or the head of the next fix round — with the real migration name, the three changed snapshots and their measured sizes, and re-tick the checklist item only once it is true. |

The two findings dispositioned in round 1 as **rejected** (the inline
`"The Sign-off Engineer was set."` literal, matching every sibling handler in
`Workflow.cshtml.cs`) and **accepted risk** (`SingleOrDefault(IsDefault)`
failing closed on a double default, PLAT-068's rule to enforce) were re-checked
and are unchanged; the reviewer did not re-raise them.

## Checks that passed

Every drawn control has a named, existing handler: `AssignEngineer` and
`SetSignOffEngineer` on `/Cases/Workflow` (`Workflow.cshtml.cs:95, 126`),
`Bundle` on `/Cases/Documents/Export` (`Export.cshtml.cs:46`), `Submit` on
`/Cases/Eva/Send` (`Send.cshtml.cs:117`). `_EvaHandoff` has exactly two Razor
callers (`Details.cshtml:607`, `Eva/Send.cshtml:63`). No explanatory copy is
added — the disabled API route uses the existing `.gated` + `data-condition`
convention and the removed Razor comments are server-side. All eleven new
operator labels sit in the delimited `// CASE-040` block in
`OperatorLabels.CaseWorkspace` (`OperatorLabels.cs:1481-1493`). Every one of
the 39 changed paths is an owned path. Core owns the sign-off resolver, the
EVA state policy and the start-work precondition, with no second
implementation in Infrastructure or Web. D44 holds: `git grep` for
`ReviewedByStaff` / `RequireStaffImageReview` outside frozen migrations returns
only two negative assertions. `Download EVA package` survives only as a
negative assertion (`CaseDetailsWebTests.cs:743`). The migration adds one
nullable column and drops one filtered index — no new table, so no grant and
no bootstrap census row is due. The regenerated Case page begins
`<!DOCTYPE html>`, carries one `class="case-sticky"` and eleven distinct
`id="section-…"` hosts (overview, engineer-notes, inspection, vehicle, damage,
valuation, estimate, settlement, report, files, notes) and no `<img src="#">`;
the EVA send page carries the Sign-off Engineer field.

## Commands run in the review checkout (`.worktrees/case-040-review`)

```
git worktree add --detach … origin/task/case-040-sign-off-engineer-eva  — exit 0; HEAD = bfd0893943f2b81c4446b7756860c02f415a17b7
dotnet restore ./Pegasus.slnx --locked-mode                             — RESTORE_EXIT=0
dotnet build ./Pegasus.slnx --configuration Release --no-restore        — BUILD_EXIT=0, 0 warnings, 0 errors
dotnet test ./tests/Pegasus.Core.Tests --configuration Release --no-build        — CORE_EXIT=0, 1245 passed
dotnet test ./tests/Pegasus.ArchitectureTests --configuration Release --no-build — ARCH_EXIT=0, 100 passed
pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1                — GRANTS_EXIT=0, 94 migrations checked
dotnet test ./tests/Pegasus.IntegrationTests --configuration Release --no-build
  --filter "FullyQualifiedName~CaseWorkflowPersistenceTests|~EvaSubmissionPersistenceTests|
            ~CustodyOutboxIntegrationTests|~CaseDetailsWebTests|~IntakePersistenceIntegrationTests|
            ~AssessmentPersistenceIntegrationTests|~CaseWorkflowWebTests"
  -- xUnit.MaxParallelThreads=2                                         — INT_EXIT=0, 174 passed, 1 pre-existing skip
```

That scope covers the change: the filter names every test class under `tests/`
in `git diff --name-only origin/dev...HEAD`, the two Core suites cover the new
resolver, the EVA state policy and the new `RequireStartCaseWorkAsync`, and the
grant script covers the new migration. It does **not** cover finding A — the
class that fails is `Browser/OperatorJourneyTests`, which this ticket changed
the behaviour of without changing the file, so no `--filter` derived from the
diff would ever have reached it. That is the gap CI closed and the lane's local
scope could not.

## CI gate

Not passed. `gh run list --branch task/case-040-sign-off-engineer-eva` names
run `33939704373` on `headSha bfd0893943f2b81c4446b7756860c02f415a17b7`; its
`browser` job completed with conclusion `failure` (`Failed: 1, Passed: 124`)
for finding A, with `test-ui` and `sql-integration (1)` still in progress at
the time of review. The two earlier heads failed the same way. No merge was
attempted. A rerun would not help — the failure is deterministic and its cause
is in the diff.
