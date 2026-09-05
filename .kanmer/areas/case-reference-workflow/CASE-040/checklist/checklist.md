# Checklist — CASE-040 (2026-09-02 terra xhigh; revised 2026-09-03)

Execution order follows the plan's steps; Step 7 of the plan is the snapshot
capture and verification lines below.

- [x] Step 1 (merge and lock boundary): confirm PLAT-068, PLAT-070, CASE-038, DOCS-017, shared locks, and the free migration lane.
- [x] Step 1 (CASE-038 transfer): the owned `Details.cshtml(.cs)` regions are recorded in writing on the ticket before the first commit.
- [x] Step 1 (D44): after the refresh, `git grep -i "ReviewedByStaff\|RequireStaffImageReview"` returns nothing.
- [x] Step 2 (Core): add the Core Sign-off Engineer contracts, resolver, lifecycle action, and production registration.
- [x] Step 3a (persistence and EVA policy): persist/project SignOffEngineerId and centralize manual versus automatic EVA state policy plus the eligible-sign-off precondition, consumed by both EVA stores.
- [x] Step 3b (one-delivery rule removed): remove the delivered-submission uniqueness path while preserving replay, automatic once-only submission, and Unknown-only retries.
- [x] Step 3c (state refusal): the two EVA routes return the new EVA state refusal instead of `CaseNotInReviewException`; the document-export use is untouched.
- [x] Step 4a (shared handoff partial): create the shared EVA handoff partial and wire the transferred Details dialog and script-off Send page.
- [x] Step 4b (ribbon slot and Overview): render the Sign-off Engineer in the ribbon/current-position slot and Overview using `OperatorLabels.CaseWorkspace`; `EvaSubmissionPolicy.NotEnabledReason` is deleted and its caller points at the label.
- [x] Step 5 (migration): generate the one CASE-040 migration and confirm no grants are required.
- [x] Step 6 (tests): extend Core, persistence, Web and `CustodyOutboxIntegrationTests` / `CaseWorkflowWebTests` coverage for defaults, selection, With Engineer re-send through `ISubmitCaseToEva`, and port-level refusal without an eligible sign-off.
- [x] `dotnet restore ./Pegasus.slnx --locked-mode` exits 0.
- [x] `dotnet build ./Pegasus.slnx --configuration Release --no-restore` exits 0.
- [x] `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"` exits 0 — run as the equivalent focused set per the EPIC-012 build policy (Core + Architecture + changed integration classes); full unfiltered run is CI's job.
- [x] `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Browser&Category!=Corpus" -- xUnit.MaxParallelThreads=2` exits 0 — covered via the scoped Test UI snapshot capture/verify (browser-rendering classes) rather than the whole Browser category, per EPIC-012's no-local-duplication-of-CI policy.
- [x] `./scripts/Test-MigrationGrants.ps1` exits 0.
- [x] `./scripts/Update-TestUiSnapshots.ps1` captures the expected two snapshot changes.
- [x] `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture` exits 0.
- [x] `./scripts/Test-UiCatalogue.ps1` exits 0.
- [x] post-implementation report written
- [x] PR opened with Kanmer: CASE-040 (#666)

- [x] Step 3a (2026-09-04): `EfAssessmentReportProjectionSource` passes the resolved complete `ReportSignatory` (null when none resolves) through the Core resolver; no second resolver.
- [x] Integration test through the real projection source: a draft generates end to end when the sign-off resolves; the Sign-off readiness item is returned when it does not. Test name recorded in the report.

## Review round fixes (2026-09-04)

- [x] Blocker 1: EVA routes apply StartCaseWork's assignment/eligibility preconditions inside the locked transition section.
- [x] Blocker 2: a successful EVA submission durably records its EvaSubmissions/history rows even when the local re-check fails afterward.
- [x] Blocker 3: archived-case projection carries the persisted SignOffEngineerId.
- [x] Should-fix 4: re-send test asserts the second action-history row and its identity payload for both routes.
- [x] Should-fix 5: post-implementation report refreshed at the fixes' head.
- [x] Fixes committed (`bfd089394`) and pushed to `task/case-040-sign-off-engineer-eva`.

## Review round fixes (2026-09-05) — second re-review

- [x] Blocker A: OperatorJourneyTests.ExportByKeyboardAsync updated for the "Download ZIP" rename; an eligible Engineer is assigned before the export so the D47 precondition is met; the second export's comment now describes it as the D36 re-send it is. Verified locally (5 passed).
- [x] Blocker B: ProcessQueuedEvaSubmission.ExecuteAsync treats a post-delivery CaseVersionConflictException as terminal (Completed), matching the state/signatory refusal branch; new automatic-path version-race test proves one transport call, one submission row, one history row, and a terminal (non-re-claimable) work row. Verified locally (CustodyOutboxIntegrationTests: 23 passed, 1 pre-existing skip).
- [x] Should-fix C: post-implementation report rewritten in place at head `f96af2435` with the real migration name (`20260905010654_CaseSignOffEngineer`), the three actually-changed snapshot sizes (66,771 / 25,888 / 40,383 bytes), and the corrected claim that `case-details--conflict.html` is an owned change in the diff (it is not reverted).
- [x] Fixes committed (`f96af2435`) and pushed to `task/case-040-sign-off-engineer-eva`.

## Review round fixes (2026-09-05) — round 3

- [x] Blocker 1: `EvaSubmissionPolicy.RequireOnceOnlyAutomaticSubmission` restores a delivered-submission refusal for `EvaSubmissionTrigger.Automatic` only, checked before the transport call in `EvaSubmissionStore.ExecuteAsync`; manual re-sends unaffected. New/extended test proves a delivered `EvaSubmissions` row plus an expired-lease `Processing` work item, re-claimed, makes no second transport call and completes the work row (`AutomaticEvaSubmissionCompletesAfterDeliveredVersionConflictWithoutRetrying`), plus two focused `EvaSubmissionPolicyTests` unit tests.
- [x] Blocker 2: Current position card renders the Sign-off Engineer row beside Engineer; Case details snapshots regenerated (scoped capture), verified, and catalogue re-checked — all green.
- [x] Should-fix 3: dead `OperatorLabels.CaseWorkspace.RibbonSignOff` deleted.
- [x] Should-fix 4: `Eva/Send.cshtml` title/h1 point at `OperatorLabels.CaseWorkspace.EvaHandoff`.
- [x] Fixes committed (`64889c424`) and pushed to `task/case-040-sign-off-engineer-eva`.

## Review round fixes (2026-09-05) — round 4

- [x] Blocker 1: `EvaSubmissionPolicy.StateAfterSend`/`EvaHandoffPolicy.StateAfterManualSend` gain an `isDelivered` parameter (default true, preserving the export route and the pre-transport preflight check); `EvaSubmissionStore.RecordSubmissionAsync` passes `result.IsDelivered` explicitly, so a Rejected or Unknown manual send from Review no longer moves the case, bumps its version, or clears its edit lease, while Partial still transitions. New Core unit tests (`UndeliveredManualSendFromReviewDoesNotMoveTheCase`, `PartialManualSendFromReviewStillMovesTheCase`) and a new integration block in `EvaRoutesTransitionFirstSendAtomicallyAndResendWithoutStateChange` (via the new `FixedOutcomeEvaTransport` fake) proving both outcomes leave state/version/edit-lease untouched while still recording the `EvaSubmissions` row and the `eva_api_submitted` history row.
- [x] Should-fix 2: FRD-07's API-submission paragraph corrected to distinguish a pre-transport failure (nothing recorded, Review unchanged) from a post-delivery failure (submission and action history recorded, Review unchanged) and to state the Rejected/Unknown-is-not-a-handoff rule explicitly.
- [x] Should-fix 3: post-implementation report corrected at the actual current head `3d82259f5`, with the real committed byte sizes for `case-details--default.html`/`case-details--conflict.html` and `queues--empty.html` named as an owned change from the `origin/dev` merge; authoritative sizes deferred to `proof.md` on merged `main`.
- [x] Should-fix 4: raised as follow-up ticket [[CASE-046]] (retyping the shared partial touches `DetailsModel.cs` outside CASE-040's narrow owned regions) rather than applied in this ticket.
- [x] Nit 5: `EvaSubmissionStore.RecordSubmissionAsync`'s catch filter narrowed to `InvalidOperationException` alone, with a comment naming why it stays deliberately wide.
- [x] Verified locally: Core.Tests 1252 passed; ArchitectureTests 100 passed; `CustodyOutboxIntegrationTests` 23 passed/1 pre-existing skip; `EvaSubmissionPersistenceTests`+`CaseWorkflowPersistenceTests` 44 passed. No migration or routed page changed, so `Test-MigrationGrants.ps1` and the snapshot procedure do not apply.
