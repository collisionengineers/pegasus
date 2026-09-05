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
