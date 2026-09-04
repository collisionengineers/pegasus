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
