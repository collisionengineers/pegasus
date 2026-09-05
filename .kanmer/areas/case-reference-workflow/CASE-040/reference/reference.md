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

---

# Review record — CASE-040 (PR https://github.com/collisionengineers/pegasus/pull/666) — re-review

Reviewed head: `f96af24355bafd078bef7422e9837184cd36dcdc`
(branch `task/case-040-sign-off-engineer-eva`; `git rev-parse HEAD` in the
detached review worktree `.worktrees/case-040-review` equals the head named in
the packet — the branch had not moved). Round 3, after the fix commit
`f96af2435` on top of round 2's head `bfd089394`.

Reviewers: gpt-5.6-terra xhigh (independent read, reading-only, same detached
worktree); Claude Opus (dispositions, independent verification, CI gate).
Built by gpt-5.6-sol.

**Verdict: REQUEST CHANGES.** Both round-2 blockers (A, the CI-red keyboard
export journey, and B, the automatic worker's post-delivery version conflict)
are closed and verified, and should-fix C is closed — the report's byte sizes
were re-measured and are correct as committed-blob sizes. But closing B by
catching the symptom left the underlying guarantee unrestored: this PR removed
`dev`'s delivered-submission refusal, and the only remaining automatic
once-only guard is the D47 state transition, which is precisely what does not
happen in the version-conflict branch. A second, separate defect surfaced on
re-read: the ticket's third named surface, the Current position card, never
received the Sign-off Engineer field. Not merged.

## Re-review status of round-2's findings

| # | Round-2 finding | Status at `f96af2435` | Evidence |
| --- | --- | --- | --- |
| A | CI-red keyboard export journey after this PR's own `Download export` → `Download ZIP` rename | **CLOSED** | The only selector change is the button name (`OperatorJourneyTests.cs:542`), matching `OperatorLabels.CaseWorkspace.DownloadZip` which `_EvaHandoff.cshtml:89` renders. No assertion was weakened or deleted: the journey still asserts the keyboard-driven POST returns 200, the same suggested filename, and byte-identical archive content on the re-send (`:151-165`). The new `AssignEligibleEngineerAsync` helper (`:429-497`) seeds a `PegasusIdentityUser` in the Engineer role and assigns it through the real `IAssignCaseEngineer` under a claimed edit lease, which supplies D47's `RequireStartCaseWorkAsync` precondition rather than bypassing it; the comment on the second export was corrected to describe it as the D36 re-send it now is. Independently re-run: `--filter "FullyQualifiedName~OperatorJourneyTests"` — exit 0 as part of the scoped integration run below. |
| B | Automatic worker could double-submit after a post-delivery version conflict | **CLOSED as stated — but see finding 1** | `ProcessQueuedEvaSubmission.ExecuteAsync`'s terminal filter now names `CaseVersionConflictException` alongside the three existing refusals (`EvaSubmissionWorkItem.cs:171-175`). I verified the exception cannot be swallowed pre-delivery: in `EvaSubmissionStore.ExecuteAsync` the only throw site is inside `RecordSubmissionAsync` (`EvaSubmissionStore.cs:290-294`), which runs strictly after `transport.SubmitInstructionAsync` (`:150-157`); no version comparison exists anywhere earlier in the path (`:44-150`). `AutomaticEvaSubmissionCompletesAfterDeliveredVersionConflictWithoutRetrying` (`CustodyOutboxIntegrationTests.cs:1736-1865`) drives the real automatic path through `ReconcileAutomaticEvaSubmissions` → `EfEvaSubmissionWorkStore` → `ProcessQueuedEvaSubmission`, races `CaseWorkflows.Version` by raw SQL from inside the transport call, and asserts one transport call, one `EvaSubmissions` row carrying the real EVA identifiers, exactly one `eva_api_submitted` history row, and a `completed` work row with a null lease token. Independently re-run: `--filter "FullyQualifiedName~CustodyOutboxIntegrationTests"` — exit 0. |
| C | Post-implementation report stale at the final head | **CLOSED** | The report body now names head `f96af2435`, migration `20260905010654_CaseSignOffEngineer`, and lists `case-details--conflict.html` as an owned change in the diff, with the false "reverted" claim removed. The byte sizes it records — 66,771 / 25,888 / 40,383 — were challenged on first measurement (a working-tree `wc -c` returns 67,885 / 26,237 / 41,054) and then confirmed correct: `git cat-file -s` on the three blobs at HEAD returns exactly 66,771, 25,888 and 40,383. The difference is CRLF in the checkout across 1,114 / 349 / 671 lines. Round 2's record carried the working-tree numbers; the report's are the right ones. No finding. |

## Findings and dispositions (this round)

| # | Severity | File:line | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker | `src/Pegasus.Infrastructure/Persistence/EvaSubmissionStore.cs:44-150` | **Automatic once-only submission is no longer guaranteed, and finding B's fix closes only the in-process half of the window.** On `dev`, `ExecuteAsync` refused *before* the transport call for any case already carrying a delivered `EvaSubmissions` row — `FindDeliveredAsync` + `EvaAlreadySubmittedException` (`origin/dev:EvaSubmissionStore.cs:95-98, 172-178`), backed by the `UX_EvaSubmissions_CaseDelivered` index. Step 3b removed both, correctly, so D36 manual re-sends are possible. But the checklist and report claim the removal preserved "automatic once-only submission", and the only thing now preserving it is `EvaSubmissionPolicy.StateAfterSend(state, Automatic)`, which throws `EvaHandoffStateException` **only once the case has left `Review`** (`EvaSubmissionPolicy.cs:108-115`). In the exact scenario finding B describes — the D47 transition fails on a version conflict, so `transitionFailure` is set and the workflow write is skipped (`EvaSubmissionStore.cs:352-364`) — the case stays in `Review` while the delivery is durably recorded, and that guard is inert. Finding B's catch makes the work item terminal *when `ExecuteAsync` returns normally*. It does not cover a host crash, restart, or shutdown between `RecordSubmissionAsync`'s `CommitAsync` (`:366`) and the worker's `RecordAsync`: the `ExternalWorkItems` row stays `Processing`, `EfEvaSubmissionWorkStore.ClaimProcessingAsync` re-claims it on lease expiry with `AttemptCount + 1`, the next attempt derives a different `AttemptOperationKey`, the exact-key `FindReplayAsync` misses, and the case is submitted to EVA a second time. EVA has no idempotency, so that is a second live claim. This window is created by this PR: on `dev` the delivered-row refusal caught it regardless of state. | **Fix.** Restore a delivered-submission refusal for `EvaSubmissionTrigger.Automatic` only, before the transport call, so automatic once-only holds independently of whether the D47 transition succeeded — Core owns the rule (it is an EVA submission decision, `EvaSubmissionPolicy`'s stated remit), the store supplies the fact. Manual re-sends stay unaffected because the refusal is trigger-scoped. Prove it with a test that leaves a delivered `EvaSubmissions` row and a `Processing` work item whose lease has expired, re-claims it, and asserts the second attempt makes **no** transport call and completes the work row. |
| 2 | blocker | `src/Pegasus.Web/Pages/Cases/Details.cshtml:388` | The ticket's What names three surfaces: "A Sign-off Engineer field beside Engineer (ribbon, Overview, Current position)". Two are delivered — the ribbon (`Details.cshtml:130-136`) and Overview (`_CaseSummary.cshtml:31`). The third is not: the Current position context card still renders `<div class="decision-row"><span>Engineer</span>…</div>` with no Sign-off Engineer row, and `grep -n "Current position"` returns exactly one site in `src/`, this one. The mockup this epic implements is explicit — `Pegasus_UI_v2_src/src/20-case.js:113` builds the Current position panel as `['Engineer', …], ['Sign-off', …]`, adjacent. The plan's own acceptance line writes "ribbon/current-position slot" as a single item, which is how the implementation read it, but the ribbon is `aria-label="Case identity"` and the Current position card is a separate surface that already carries Engineer; collapsing the two loses a surface the ticket names. Confirmed in the committed artifact: `case-details--default.html` contains "Sign-off Engineer" three times (ribbon, Overview, dialog) and none inside the Current position card. | **Fix.** Add the Sign-off Engineer `decision-row` beside Engineer in the Current position card, using `OperatorLabels.CaseWorkspace.SignOffEngineer` and `Model.SignOffEngineerDisplayName` (both already on the model — no new plumbing). This changes a routed Razor page, so regenerate the Case details snapshots (scoped capture, `-Scope case-details`) and re-run `Test-UiCatalogue.ps1`. |
| 3 | should-fix | `src/Pegasus.Web/Presentation/OperatorLabels.cs:1475` | `RibbonSignOff = "Sign-off"` is now dead. CASE-040 replaced its only call site with the new `SignOffEngineer` label (`Details.cshtml:133`); `grep -rn "RibbonSignOff" src/ tests/ docs/` returns the declaration and nothing else. Conduct rule 6 — delete what you replace. | **Fix.** Delete the constant in the same diff that orphaned it. |
| 4 | should-fix | `src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml:5, 11` | This PR introduces `OperatorLabels.CaseWorkspace.EvaHandoff = "EVA handoff"` and uses it for the Details dialog title (`Details.cshtml:601`), but the Send page keeps the same words as two literals — `ViewData["Title"] = "EVA handoff"` and `<h1>EVA handoff</h1>`. The literals pre-date the PR; the duplication does not. One list per concept. | **Fix.** Point both at `OperatorLabels.CaseWorkspace.EvaHandoff`. The `<h1>` is inside the captured `case-eva-send--default` page, so regenerate that snapshot with finding 2's capture (the rendered text is unchanged, so the snapshot may not move). |
| 5 | nit (reviewer: blocker, restated) | `src/Pegasus.Core/Eva/EvaSubmissionWorkItem.cs:156` | The reviewer raised the general "crash after commit, before work completion" window as its own blocker and proposed durably associating submission recording with work completion. | **Merged into finding 1, and the proposed remedy rejected.** The reviewer is right about the window and wrong about the cheapest correct fix: coupling the `EvaSubmissions` write to the `ExternalWorkItems` write across two stores would put the outbox row inside the submission transaction, which is a larger design change than this ticket owns and duplicates a guarantee the delivered-row refusal already gives for free. Finding 1's trigger-scoped refusal closes the same window at the point of the second transport call, which is the only place a duplicate claim can actually be created. |

The two findings dispositioned in round 1 as **rejected** (the inline
`"The Sign-off Engineer was set."` literal, matching every sibling handler in
`Workflow.cshtml.cs`) and **accepted risk**
(`CaseSignOffEngineerResolver.Resolve`'s `SingleOrDefault(IsDefault)` failing
closed on a double default, PLAT-068's rule to enforce) were re-checked at this
head and are unchanged. The independent reviewer re-confirmed both.

## Checks that passed

Every drawn control has a named, existing handler (`AssignEngineer` and
`SetSignOffEngineer` on `/Cases/Workflow`, `Bundle` on
`/Cases/Documents/Export`, `Submit` on `/Cases/Eva/Send`). No explanatory copy,
field hint or empty-state panel is added; the disabled API route uses the
existing `.gated` + `data-condition` convention. All new operator labels sit in
the delimited `// CASE-040` block (`OperatorLabels.cs:1482-1494`) — findings 3
and 4 are about a stale sibling and two pre-existing literals, not about a
label written elsewhere. All 40 changed paths are inside the owned set. Core
owns the sign-off resolver, the EVA state policy and the start-work
precondition, with no second implementation in Infrastructure or Web. The
migration adds one nullable column and drops one filtered index — no new
securable, so no grant and no bootstrap census row is due. The committed
snapshots at this head begin `<!DOCTYPE html>`, `case-details--default.html`
carries one `class="case-sticky"` and no `<img src="#">`, and
`case-eva-send--default.html` carries the Sign-off Engineer field. Neither
round-3 commit touched a routed Razor page, a partial it composes, or
`catalogue.json`, so no snapshot procedure was due this round — correct.

## Commands run in the review checkout (`.worktrees/case-040-review`)

```
git worktree add --detach … ; git checkout --detach f96af2435   — CO_EXIT=0; HEAD = f96af24355bafd078bef7422e9837184cd36dcdc
dotnet restore ./Pegasus.slnx --locked-mode                     — RESTORE_EXIT=0
dotnet build ./Pegasus.slnx --configuration Release --no-restore — BUILD_EXIT=0, 0 warnings, 0 errors
dotnet test ./tests/Pegasus.Core.Tests --configuration Release --no-build        — CORE_EXIT=0, 1245 passed
dotnet test ./tests/Pegasus.ArchitectureTests --configuration Release --no-build — ARCH_EXIT=0, 100 passed
dotnet test ./tests/Pegasus.IntegrationTests --configuration Release --no-build
  --filter "FullyQualifiedName~CustodyOutboxIntegrationTests|~OperatorJourneyTests|
            ~CaseWorkflowPersistenceTests|~EvaSubmissionPersistenceTests|
            ~CaseDetailsWebTests|~CaseWorkflowWebTests|~AssessmentPersistenceIntegrationTests"
  -- xUnit.MaxParallelThreads=2                                 — INT_EXIT=0, 170 passed, 1 pre-existing skip
git cat-file -s <three snapshot blobs at HEAD>                  — 66,771 / 25,888 / 40,383 bytes
```

That scope covers the change: the filter names every test class under `tests/`
in `git diff --name-only origin/dev...HEAD`, and adds `OperatorJourneyTests` —
the class round 2's finding A broke — so the browser journey this round claims
to have fixed was actually executed here rather than taken on trust. The two
Core suites cover the sign-off resolver, the EVA state policy and
`RequireStartCaseWorkAsync`. No migration changed this round, so
`Test-MigrationGrants.ps1` was not re-run (it passed at `bfd089394` over the
same migration file, unmodified since). The green run does **not** clear
findings 1 or 2: finding 1 is a crash/lease-expiry window no in-process test
exercises, and finding 2 is a missing surface no assertion covers.

## CI gate

Not reached. The PR was not approved, so no merge was attempted. Run
`33942496397` on `headSha f96af24355bafd078bef7422e9837184cd36dcdc` was still
`in_progress` at review time; the previous two heads failed on finding A, which
is closed here, so this run is expected to go green — but a green run would not
change the verdict, since neither finding 1 nor finding 2 is covered by any
existing check.

---

# Review record — CASE-040 (PR https://github.com/collisionengineers/pegasus/pull/666) — re-review

Reviewed head: `3d82259f5fa1e37c5d4fcc7081f8c54f091115b4`
(branch `task/case-040-sign-off-engineer-eva`). **The branch moved past the
head named in the packet's premise and past the head the report records.**
Round 3's head `f96af2435` is three commits behind this one: `64889c424`
(round-3 fixes), `916177da7` (`origin/dev` merge, bringing ENG-034's Engineer
sections onto the Case page) and `3d82259f5` (Test UI regeneration after that
merge). The whole diff `origin/dev...3d82259f5` was read at this head.

Reviewers: **Claude Opus 5 alone** — gpt-5.6 (Codex) is unavailable until
2026-09-08 on a usage limit, so there was no second model this round. The
whole 41-file diff was read by hand as the independent review. Built by
Claude Sonnet (round-3 fixes by gpt-5.6-sol medium).

**Verdict: REQUEST CHANGES.** All four round-3 findings are closed and
verified. One **new blocker** was found by reading, in code untouched since
round 1 and never covered by any test: a manual Send via API that EVA
**rejects**, or that returns `Unknown` because EVA could not be reached, still
performs the D47 transition and moves the case out of `Review`. Three
should-fix findings and three accepted observations follow. Not merged.

## Re-review status of round-3's findings

| # | Round-3 finding | Status at `3d82259f5` | Evidence |
| --- | --- | --- | --- |
| 1 | Automatic once-only submission no longer guaranteed | **CLOSED** | `EvaSubmissionPolicy.RequireOnceOnlyAutomaticSubmission(trigger, hasDeliveredSubmission)` (`EvaSubmissionPolicy.cs:119-137`) throws the new `EvaAutomaticSubmissionAlreadyDeliveredException` for `Automatic` + delivered and is a no-op for `Manual` of either delivery state — Core owns the decision, the store supplies the fact (`EvaSubmissionStore.cs:112-119`, an `AnyAsync(… && item.IsDelivered)` placed after the exact-key replay check and before the transport call). Added to the worker's terminal filter (`EvaSubmissionWorkItem.cs:171-176`). Proved exactly as asked: `AutomaticEvaSubmissionCompletesAfterDeliveredVersionConflictWithoutRetrying` now expires the `Processing` lease, re-claims the work item (`AttemptCount` 2), and asserts `transport.CallCount` is **still 1** and the row ends `completed` with `eva_submission_no_longer_applicable`. Two focused unit tests added. |
| 2 | Current position card missing Sign-off Engineer | **CLOSED** | `Details.cshtml:389` renders the `decision-row` beside Engineer using `OperatorLabels.CaseWorkspace.SignOffEngineer` and `Model.SignOffEngineerDisplayName` — no new plumbing, as suggested. The committed `case-details--default.html` now contains "Sign-off Engineer" four times (ribbon, Overview, Current position, dialog), up from three. |
| 3 | Dead `RibbonSignOff` label | **CLOSED** | Deleted (`OperatorLabels.cs`). `grep -rn "RibbonSignOff" src/ tests/ docs/` returns nothing. |
| 4 | `Eva/Send.cshtml` duplicates the "EVA handoff" wording | **CLOSED** | Both `ViewData["Title"]` and the `<h1>` now read `OperatorLabels.CaseWorkspace.EvaHandoff`. The rendered text is unchanged and `case-eva-send--default.html` did not move (25,888 blob bytes, as before), confirming the round-3 expectation. |

## Findings and dispositions (this round)

| # | Severity | File:line | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | **blocker** | `src/Pegasus.Infrastructure/Persistence/EvaSubmissionStore.cs:369`, `src/Pegasus.Core/Eva/EvaSubmissionPolicy.cs:108-116` | **A rejected or unreachable EVA send still moves the case to With Engineer.** The D47 transition is decided by `EvaSubmissionPolicy.StateAfterSend(currentState, request.Trigger)`, which takes the case state and the trigger and **never sees the EVA outcome**. `RecordSubmissionAsync` is called unconditionally after `transport.SubmitInstructionAsync` (`EvaSubmissionStore.cs:159-166`), so every manual API send from `Review` — `Succeeded`, `Partial`, `Rejected` or `Unknown` alike — sets `workflow.State = ReportPreparation`, increments the version and clears the edit lease (`:369-380`). This contradicts three statements the ticket carries: FRD-07 as amended by *this PR* ("The first **successful** manual Send via API from `Review` atomically records the handoff and moves the Case", `frd-07:133`); D47 in `open-questions/` (the transition is "atomically with the handoff" — a rejection is not a handoff, as `EvaSubmissionResult.IsDelivered`, `EvaApiContracts.cs:133`, already states); and the plan's Resolutions §2 ("If either half fails the whole command fails: the case stays in `Review`"). **Failure scenario:** a `Review` case with an assigned Engineer; the operator presses Send via API; EVA refuses ("'Agent' field value couldn't be bound" is the FRD's own example); `Send.cshtml.cs:167` tells the operator to fix the cause and re-send — but the case is now `ReportPreparation`, `AssignCaseEngineer` refuses outside `Review` (`CaseLifecycle.cs:85-90`) and `ReturnCaseToReview` accepts only from `NotReady` (`CaseLifecycle.cs:53-57`), so the cause can no longer be fixed and the case reads `With Engineer` when nothing reached an Engineer. **Untested:** every store-level EVA test drives `RecordingEvaTransport`, which returns `Succeeded` unconditionally; no test asserts the case state after a `Rejected` or `Unknown` outcome. | **Fix.** Gate the transition on delivery, not on state and trigger alone: pass `result.IsDelivered` into the Core rule — the same property `RequireOnceOnlyAutomaticSubmission` already consumes, and `Partial` must keep transitioning because EVA did create a claim. Add integration assertions that a `Rejected` and an `Unknown` manual send from `Review` leave `State == Review`, the version unchanged and the edit lease intact, while still recording the `EvaSubmissions` row and the `eva_api_submitted` history row. |
| 2 | should-fix | `docs/frd/frd-07-eva-and-external-engineering-handoff.md:137` | **FRD-07 contradicts this PR's own durability fix.** The API paragraph states "If either part fails, the Case remains in `Review` and no handoff is recorded." The round-2 blocker-2 fix deliberately does the opposite in the post-delivery window: `RecordSubmissionAsync` (`EvaSubmissionStore.cs:290-386`) captures a failing state/sign-off/version check instead of aborting, still commits the `EvaSubmissions` and `eva_api_submitted` rows, and only then re-throws — proved by the two version-race tests. So "no handoff is recorded" is false exactly where it matters. The export paragraph (`:62-67`) *is* satisfied: `EvaHandoffStore.cs:174-180` throws the version conflict before any write. | **Fix, alongside finding 1** (the same paragraph is what finding 1 makes true again for the state half). Replace the API atomicity sentence with the implemented rule: a failure before the transport call leaves the Case in `Review` with no handoff; a failure discovered after EVA accepted the instruction still records the submission and its action history, and the Case stays in `Review`. |
| 3 | should-fix | `post-implementation-report/post-implementation-report.md`, `checklist/checklist.md` | **Report and checklist are stale at the reviewed head — the third round running.** The report still carries `Head SHA: f96af24355…`, three commits behind. Measured as committed blobs (`git cat-file -s`, the units round 3 established): `case-details--default.html` is **68,567** bytes, not the recorded 66,771/66,881; `case-details--conflict.html` is **42,179**, not 40,383/40,493. (`case-eva-send--default.html` at 25,888 is correct.) Both moved in the `origin/dev` merge that brought ENG-034's Engineer sections onto the Case page. `docs/design/test-ui/pages/queues--empty.html` is in `git diff --name-only origin/dev...HEAD` and is named nowhere in either document, and neither has an entry for `916177da7` or `3d82259f5`. | **Fix (records only), in the same round as finding 1.** The recurrence is structural — the report is rewritten before the final push, so the next commit re-stales it. Record the head as "branch tip at review time" rather than a pinned SHA, name `queues--empty.html` and why it moved, and leave the authoritative byte sizes to `proof.md` on merged `main`, which is written after the last commit exists. |
| 4 | should-fix | `src/Pegasus.Web/Pages/Cases/Shared/_EvaHandoff.cshtml:19-21` | The new partial re-spells the readiness envelope inline (`instructionsComplete`, `imagesComplete`, `evidenceReference="case-completeness-projection"`). The form it replaced used `<partial name="Cases/Shared/_ReadinessHiddenFields" …>`, whose own comment reads "One spelling, because the reopen, return and assignment forms must never drift apart." `Details.cshtml:549` still uses that partial, so one envelope now has three spellings (the partial, the JS data attribute at `Details.cshtml:588-592`, and this). Values are identical, so nothing is broken today. | **Fix if cheap, otherwise defer with a linked ticket.** The obstacle is real: `_ReadinessHiddenFields` is typed to `DetailsModel` and `_EvaHandoff` to `EvaHandoffViewModel`. Retyping the readiness partial to a two-bool model, or carrying the envelope on `EvaHandoffViewModel`, collapses the copies. If that reaches beyond what this round should touch, raise it as a follow-up and link it — do not leave it unrecorded. |
| 5 | nit | `src/Pegasus.Infrastructure/Persistence/EvaSubmissionStore.cs:290-296` | The catch filter reads `when (exception is EvaHandoffStateException or EvaSignOffEngineerRequiredException or CaseVersionConflictException or InvalidOperationException)`. All three named types derive from `InvalidOperationException` (`EvaBundleSchema.cs:145,152`, `CaseWorkflowContracts.cs:121`), so the trailing clause subsumes them and silently widens the capture to any `InvalidOperationException` in that block, EF's own included. Nothing is suppressed — the exception is re-thrown after the commit via `ExceptionDispatchInfo` — so rule 12 holds, but the list reads as closed when it is not. | **Accept risk, with the wording tightened when finding 1 is applied.** Keep only `InvalidOperationException` and say in a comment why the block is deliberately wide (a delivered submission must never be lost, whatever the local re-check throws). No behaviour change. |
| 6 | observation | `src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml.cs:186-196` | A post-delivery `CaseVersionConflictException` lands in the broad `InvalidOperationException` arm and shows "The case could not be sent to EVA." — when EVA did receive the instruction and the row was committed. | **Accept risk.** Rare race; the delivery is durable and the Send page's `LastSubmission` shows it on the next load. Distinguishing it would need a second message for a window the operator recovers from by reloading. |
| 7 | observation | `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs:174-180` vs `:215` | The version-conflict check precedes the replay check. Because the first export from `Review` now bumps the version, a genuinely concurrent second export under the same operation key sees a stale `caseData.Version` and throws `CaseVersionConflictException` where it previously replayed. The existing concurrency assertion (`concurrentOperationKey` "4444…") runs from `ReportPreparation`, so this path is uncovered. | **Accept risk.** The error surfaces, nothing is lost, and no EVA claim exists (the export is local). Worth a line in `proof.md` rather than a code change. |

The two findings dispositioned in round 1 as **rejected** (the inline
`"The Sign-off Engineer was set."` literal, matching every sibling handler in
`Workflow.cshtml.cs`) and **accepted risk**
(`CaseSignOffEngineerResolver.Resolve`'s `SingleOrDefault(IsDefault)` failing
closed on a double default, PLAT-068's rule to enforce) were re-checked at this
head and are unchanged.

## Checks that passed

**Every drawn control has a named, existing handler.** `_EvaHandoff.cshtml`
posts to `AssignEngineer` and the new `SetSignOffEngineer` on `/Cases/Workflow`
(`WorkflowModel.OnPostSetSignOffEngineerAsync`, registered at
`DependencyInjection.cs:404`, bound and proved by `CaseWorkflowWebTests`),
`Bundle` on `/Cases/Documents/Export`, and `Submit` on `/Cases/Eva/Send`. The
partial has exactly two Razor callers. The disabled Send via API uses the
existing `.gated` + `data-condition` convention; an uncomposed transport is
absent, not disabled.

**No explanatory copy.** Every new operator-visible string is a label or a
value.

**Labels only in `OperatorLabels`,** in one `// CASE-040 … // end CASE-040`
block. All eleven new members have callers; `RibbonSignOff` was deleted;
`EvaSubmissionPolicy.NotEnabledReason` and `AlreadySubmittedReason` were
deleted with their callers repointed — no second wording home survives.

**Owned paths only.** No `TestUiSnapshotTests.cs`, `ci.yml`, `scripts/*`,
`catalogue.json`, `Pegasus.slnx`, lock file, `operator-notes.md`, `corpus/` or
`.kanmer/` in the diff. No new package.

**Core owns policy.** The default rule lives once in
`CaseSignOffEngineerResolver.Resolve`; `EvaHandoffPolicy`/`EvaSubmissionPolicy`
hold the state and once-only rules; the D47 start-work precondition lives once
in `CaseEngineerEligibilityPolicy.RequireStartCaseWorkAsync`, called by
`StartCaseWork` and both EVA stores. `EfAssessmentReportProjectionSource` calls
the resolver rather than restating it. ArchitectureTests 100 passed.

**No test weakened or deleted.** Diffing every test file against `origin/dev`,
the only removed assertions are the four the D36/D47 decisions invert
(`CaseNotInReviewException` → `EvaHandoffStateException` ×2; two
`DbUpdateException` refusals → two-rows-retained) plus a "Download export"
string replaced by "Download ZIP".
`ReportProjectionReadsPhotographsAndFailsClosedWithoutSignatory` was renamed
and **extended** — both original assertions survive verbatim, and a real
end-to-end draft through the production `EfAssessmentReportProjectionSource`
was added on top.

**Replay is safe.** `EfCaseWorkflowStore.MutateAsync:831-838` returns before
calling `apply`, so `AssignCaseEngineer`'s replay-path `signOffEngineerId =
null` never reaches the column — asserted by
`EnabledEngineerCanBeAssignedAndExactReplayDoesNotRecheckEligibility`.

**No NRE from the new nullable view model.** `Model.EvaHandoff!` is
dereferenced only inside `@if (canSendToEva)`, and `Details.cshtml:29-32`
returns when `Model.Case is null` — the exact condition under which
`DescribeWorkspaceExtrasAsync` leaves `EvaHandoff` null. `RedrawEditorAsync`
routes through `OnGetAsync`.

**Migration.** `20260905010654_CaseSignOffEngineer` adds a nullable column and
drops `UX_EvaSubmissions_CaseDelivered` — no table, no grant, no census row.
`Test-MigrationGrants.ps1` exit 0.

**Simplification pass dispositions are honest.** Three findings, all applied
and visible in the diff.

## Commands run in the review checkout (`.worktrees/case-040-review`)

```
git checkout --detach 3d82259f5…                                 CO_EXIT=0; HEAD = 3d82259f5fa1e37c5d4fcc7081f8c54f091115b4
dotnet restore ./Pegasus.slnx --locked-mode                      RESTORE_EXIT=0
dotnet build ./Pegasus.slnx --configuration Release --no-restore  BUILD_EXIT=0, 0 warnings, 0 errors
dotnet test tests/Pegasus.Core.Tests --configuration Release --no-build           CORE_EXIT=0, 1249 passed
dotnet test tests/Pegasus.ArchitectureTests --configuration Release --no-build    ARCH_EXIT=0, 100 passed
dotnet test tests/Pegasus.IntegrationTests --configuration Release --no-build
  --filter "FullyQualifiedName~CaseWorkflowPersistenceTests|~EvaSubmissionPersistenceTests|
            ~CustodyOutboxIntegrationTests|~CaseDetailsWebTests|~IntakePersistenceIntegrationTests|
            ~AssessmentPersistenceIntegrationTests|~OperatorJourneyTests"
  -- xUnit.MaxParallelThreads=2                                  INT_EXIT=0, 180 passed, 1 pre-existing skip
dotnet test tests/Pegasus.IntegrationTests --configuration Release --no-build
  --filter "FullyQualifiedName~OperatorJourneyTests" -- xUnit.MaxParallelThreads=2   JOURNEY_EXIT=0, 5 passed
pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1          GRANTS_EXIT=0, 94 migrations checked
```

That scope covers the change: `git diff --name-only origin/dev...HEAD` touches
six Core files (covered by `Pegasus.Core.Tests` and `Pegasus.ArchitectureTests`),
nine Infrastructure persistence files plus the migration (covered by
`CaseWorkflowPersistenceTests`, `EvaSubmissionPersistenceTests`,
`CustodyOutboxIntegrationTests`, `AssessmentPersistenceIntegrationTests`,
`IntakePersistenceIntegrationTests` and `Test-MigrationGrants.ps1`), ten Web
files (covered by `CaseDetailsWebTests` with its `CaseWorkflowWebTests` partial,
and by the browser `OperatorJourneyTests` that renders the real Send page), and
four Test UI snapshots plus one FRD (covered by CI's unscoped
`Update-TestUiSnapshots.ps1 -Verify`). The browser class round 2 broke was run
separately rather than taken on trust. **The green run does not clear finding
1:** no test drives a non-`Succeeded` transport outcome through
`EvaSubmissionStore`, which is exactly why the defect survived four rounds.

## Snapshot artifacts opened at this head

| File | Blob bytes | Worktree bytes | Facts |
| --- | --- | --- | --- |
| `case-details--default.html` | 68,567 | 69,732 | begins `<!DOCTYPE html>`; one `class="case-sticky"`; 16 distinct `id="section-…"` (11 base hosts + 5 `-title`); "Sign-off Engineer" ×4 (ribbon, Overview, Current position, dialog); no `<img src="#">` |
| `case-details--conflict.html` | 42,179 | 42,901 | same markers; "Sign-off Engineer" ×3; no `<img src="#">` |
| `case-eva-send--default.html` | 25,888 | 26,237 | begins `<!DOCTYPE html>`; carries the Sign-off Engineer field |
| `queues--empty.html` | 29,761 | 30,229 | begins `<!DOCTYPE html>`; the case list is still empty (no `<tr>` rows) — only the ambient sidebar counts moved 0 → 2/1/1 |

`queues--empty.html` is the documented capture-pool limit: a state whose
matcher several captured responses satisfy is claimed by whichever candidate
the pool holds. CASE-040 touches no queue route, and CI's unscoped verify is
the authority.

## CI gate

Not reached — the PR was not approved, so no merge was attempted. Run
`33974413448` on `headSha 3d82259f5fa1e37c5d4fcc7081f8c54f091115b4` was
`in_progress` at review time. A green run would not change the verdict: finding
1 is behaviour no existing check exercises.

---

# Review record — CASE-040 (PR https://github.com/collisionengineers/pegasus/pull/666) — re-review

Reviewed head: `861391d9ad6bc3a42e83d107fc0d219d5346e347`
(branch `task/case-040-sign-off-engineer-eva`, round 4 fixes commit
"fix(eva): gate the D47 transition on delivery, not state and trigger alone").
Verified in the detached review worktree
`.worktrees/case-040-review`; `git rev-parse HEAD` equals the reviewed head and
`origin/dev` is an ancestor.

Reviewers: **Claude Opus 5** — sole independent reviewer this round, reading
the whole `origin/dev...HEAD` diff and independently re-running the gates.
Codex was unavailable (usage limit to 2026-09-08). Built by Claude Sonnet.

**Verdict: REQUEST CHANGES — blocked.** Every finding from rounds 2, 3 and 4 is
confirmed closed at this head and no regression was found in them. The
implementation itself is correct and well proved. But CI is **red on this exact
head** for a committed Test UI snapshot the branch should never have changed,
and that is a merge blocker. Not merged; the ticket returns to the implementer.

## Findings and dispositions

| # | Severity | File:line | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker | `docs/design/test-ui/pages/queues--empty.html:97,196,263` | The branch replaced dev's `queues--empty` capture with one whose nav-count and tab counts read 2/1/1 instead of 0/0/0. It still carries the state matcher (`class="muted">0 items</span>`, line 272, identical to dev), so a scoped local `-Verify` passes, but CI's full capture regenerates the 0/0/0 page: run `33976995699` (this head) and run `33974413448` (`3d82259f5`) both fail `test-ui` with `Generated Test UI file is stale: pages/queues--empty.html`. CASE-040 renders nothing on the `/Cases` queues route, and the plan's *Must not modify* list forbids any Test UI file other than the two capture-generated snapshots, so this file should not be in the diff. The report's "Files changed → Test UI" list also names only three snapshots while the diff carries four. | **Fix — returned to the implementer.** Restore dev's blob (`git checkout origin/dev -- docs/design/test-ui/pages/queues--empty.html`), or regenerate that route with the FULL capture (no `-Scope`) under the capture lock; then re-verify and correct the report's file list. `test-ui` is not the `changes` job, so a `--failed` rerun is not available. |
| 2 | should-fix | `src/Pegasus.Infrastructure/Persistence/EvaSubmissionEntities.cs:33` | "…the unique index below is what actually prevents the second send" — `UX_EvaSubmissions_CaseDelivered` is dropped by this PR's migration and its configuration removed. The sibling comments in `EvaSubmissionModelConfiguration.cs` were rewritten; this one was missed, so the entity documents a guarantee the schema no longer carries. | **Fix — returned to the implementer.** Reword to name `EvaSubmissionPolicy.RequireOnceOnlyAutomaticSubmission` and the durable `ExternalWorkItems` row as the automatic once-only owners. |
| 3 | should-fix | `src/Pegasus.Infrastructure/Persistence/EvaSubmissionStore.cs:110-119` | Dropping the filtered unique index is required by D36, but automatic once-only now rests on an `AnyAsync(IsDelivered)` read taken outside the serializable transaction and before the transport call. It is proved only at Core (`EvaSubmissionPolicyTests`); no store-level test drives a second automatic submission over an already-delivered case. Residual risk is low — the enqueue sweep's two durable markers and the work-row lease are the real guards. | **Fix or accept risk — returned to the implementer.** Either add one assertion to `AutomaticEvaSubmissionCompletesAfterDeliveredVersionConflictWithoutRetrying` (a fresh automatic work item over a delivered case makes no transport call), or record the acceptance and its reasoning in `proof.md`. |
| 4 | nit | `docs/current-architecture.md:587` | "…it does not take an edit lease or move the case version" is false after D47: the first export from `Review` moves the version and clears the lease. | **Defer.** It is the as-built/deployed snapshot and CASE-040 is not deployed; the refresh belongs to the release task, not this PR. |
| 5 | nit | post-implementation report, "Files changed" | The Test UI list names three snapshots; the diff carries four. | **Rolled into finding 1.** |

## Earlier rounds — confirmed closed at this head

| Round | Finding | Evidence |
| --- | --- | --- |
| R2 B1 | D47 transition skipped StartCaseWork's preconditions | `CaseEngineerEligibilityPolicy.RequireStartCaseWorkAsync` inside the locked section on both routes (`EvaHandoffStore.cs:161`, `EvaSubmissionStore.cs:290`), proved for export *and* API by the "after an Engineer is assigned" / "Engineer account is disabled" assertions. |
| R2 B2 | A delivered submission could be lost on a local re-check failure | `transitionFailure` captured, row and history committed, then `ExceptionDispatchInfo` rethrow (`EvaSubmissionStore.cs:378-396`); proved by the version-race block. |
| R2 B3 | `SignOffEngineerId` dropped on the archived read path | `EfCaseQueryStore.cs:513-516`; `ArchivedCaseProjectionRetainsPersistedSignOffEngineer`. |
| R3 BA | CI-red keyboard export journey | `OperatorJourneyTests.AssignEligibleEngineerAsync` + `Download ZIP`; CI `browser` job green on this head. |
| R3 BB | Automatic worker double-submit after a post-delivery conflict | `CaseVersionConflictException` in `ProcessQueuedEvaSubmission`'s terminal catch; `AutomaticEvaSubmissionCompletesAfterDeliveredVersionConflictWithoutRetrying`. |
| R4 B1 | Automatic once-only lost | `RequireOnceOnlyAutomaticSubmission` at `EvaSubmissionStore.cs:117` + unit tests (residual gap raised as finding 3). |
| R4 B2 | Current position card missing Sign-off Engineer | `Details.cshtml:389`. |
| R4 SF3 | Dead `RibbonSignOff` label | Deleted; 0-warning build proves no dangling reference. |
| R5 B1 | Rejected/unreachable send still moved the case | `isDelivered` gate; `FixedOutcomeEvaTransport` block asserts state, version and the seeded edit-lease token untouched while the row and history still commit. |

## Checked and clean

Every drawn control has a named, registered handler (`AssignEngineer`,
`SetSignOffEngineer`, Export `Bundle`, Eva/Send `Submit`;
`ISetCaseSignOffEngineer` registered at `DependencyInjection.cs:404`). No
dead-end control: all three new exception types derive from
`InvalidOperationException`, already caught by `Export.cshtml.cs:97` and
`Send.cshtml.cs:186`. `Model.EvaHandoff!` is unreachable while null. Labels are
all inside one delimited `// CASE-040 … // end CASE-040` block, and the
duplicate wording homes in `EvaSubmissionPolicy` were deleted rather than
copied. No explanatory copy: the disabled *Send via API* uses the existing
`class="gated" data-condition` convention. Core owns the policy — both stores
call `EvaSubmissionPolicy` / `EvaHandoffPolicy` /
`CaseEngineerEligibilityPolicy` / `CaseSignOffEngineerResolver` with no second
implementation. The migration adds a nullable column to an existing table and
drops an index, creating no table, so the bootstrap census is unchanged.

**No test was weakened or deleted.** The two `EvaSubmissionPersistenceTests`
were *inverted* (renamed, still asserting `Count == 2`) because the index they
pinned is deliberately dropped — that change is the ticket.
`ReportProjectionReadsPhotographsAndFailsClosedWithoutSignatory` was renamed
and extended, its fail-closed assertions surviving verbatim.
`SendToEvaRendersOnlyInReview` and `SendPageRendersItsChoiceForAReviewCase`
became theories over the newly permitted states. `CaseNotInReviewException`
assertions became `EvaHandoffStateException` (the type these routes now throw);
the exception still exists and is still asserted for the custody route.
Everything else is additive.

Every file in the diff is named by the plan's *Expected files* or by its own
binding Resolutions — `EfAssessmentReportProjectionSource.cs` by the 2026-09-04
override, `frd-07` by the D47 resolution, `OperatorJourneyTests.cs` by the
round-2 controller addition, `IntakePersistenceIntegrationTests.cs` by the
migration list — **except** `queues--empty.html` (finding 1).

## Commands and exit codes (review checkout, HEAD 861391d9a)

```
git rev-parse HEAD                                                                  861391d9ad6bc3a42e83d107fc0d219d5346e347
dotnet restore ./Pegasus.slnx --locked-mode                                          RESTORE_EXIT=0
dotnet build ./Pegasus.slnx --configuration Release --no-restore                     BUILD_EXIT=0   (0 warnings, 0 errors)
dotnet test ./tests/Pegasus.Core.Tests/... --configuration Release --no-build         CORE_EXIT=0    (1252 passed)
dotnet test ./tests/Pegasus.ArchitectureTests/... --configuration Release --no-build  ARCH_EXIT=0    (100 passed)
pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1                              GRANTS_EXIT=0  (94 migration files; every created table granted or exempted)
dotnet test ./tests/Pegasus.IntegrationTests/... --filter
  "FullyQualifiedName~CustodyOutboxIntegrationTests|~EvaSubmissionPersistenceTests
   |~CaseWorkflowPersistenceTests|~AssessmentPersistenceIntegrationTests
   |~CaseDetailsWebTests" -- xUnit.MaxParallelThreads=2                               INT_EXIT=0     (165 passed, 1 pre-existing skip)
gh run view 33976995699 (head 861391d9a)                                              conclusion=FAILURE — test-ui failed; every other job success
```

Scope rationale: the diff changes Core EVA and lifecycle policy (covered by
`Pegasus.Core.Tests` and the layering rules in `Pegasus.ArchitectureTests`), the
two EVA stores plus the workflow, query and report-projection stores (covered by
`CustodyOutboxIntegrationTests`, `EvaSubmissionPersistenceTests`,
`CaseWorkflowPersistenceTests`, `AssessmentPersistenceIntegrationTests`), the
Case details and Send pages plus the new shared partial (covered by
`CaseDetailsWebTests`, the partial class `CaseWorkflowWebTests` also extends),
and one migration (`Test-MigrationGrants.ps1`). The Playwright journey change
and the Test UI snapshots were left to CI's `browser` and `test-ui` jobs;
`browser` passed, `test-ui` did not.

Full findings file:
`scratchpad/build/CASE-040/review-out-2.md`.
