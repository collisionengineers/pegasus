# Checklist — CASE-040 (2026-09-02 terra xhigh; revised 2026-09-03)

Execution order follows the plan's steps; Step 7 of the plan is the snapshot
capture and verification lines below.

- [ ] Step 1 (merge and lock boundary): confirm PLAT-068, PLAT-070, CASE-038, DOCS-017, shared locks, and the free migration lane.
- [ ] Step 1 (CASE-038 transfer): the owned `Details.cshtml(.cs)` regions are recorded in writing on the ticket before the first commit.
- [ ] Step 1 (D44): after the refresh, `git grep -i "ReviewedByStaff\|RequireStaffImageReview"` returns nothing.
- [ ] Step 2 (Core): add the Core Sign-off Engineer contracts, resolver, lifecycle action, and production registration.
- [ ] Step 3a (persistence and EVA policy): persist/project SignOffEngineerId and centralize manual versus automatic EVA state policy plus the eligible-sign-off precondition, consumed by both EVA stores.
- [ ] Step 3b (one-delivery rule removed): remove the delivered-submission uniqueness path while preserving replay, automatic once-only submission, and Unknown-only retries.
- [ ] Step 3c (state refusal): the two EVA routes return the new EVA state refusal instead of `CaseNotInReviewException`; the document-export use is untouched.
- [ ] Step 4a (shared handoff partial): create the shared EVA handoff partial and wire the transferred Details dialog and script-off Send page.
- [ ] Step 4b (ribbon slot and Overview): render the Sign-off Engineer in the ribbon/current-position slot and Overview using `OperatorLabels.CaseWorkspace`; `EvaSubmissionPolicy.NotEnabledReason` is deleted and its caller points at the label.
- [ ] Step 5 (migration): generate the one CASE-040 migration and confirm no grants are required.
- [ ] Step 6 (tests): extend Core, persistence, Web and `CustodyOutboxIntegrationTests` / `CaseWorkflowWebTests` coverage for defaults, selection, With Engineer re-send through `ISubmitCaseToEva`, and port-level refusal without an eligible sign-off.
- [ ] `dotnet restore ./Pegasus.slnx --locked-mode` exits 0.
- [ ] `dotnet build ./Pegasus.slnx --configuration Release --no-restore` exits 0.
- [ ] `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"` exits 0.
- [ ] `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Browser&Category!=Corpus" -- xUnit.MaxParallelThreads=2` exits 0.
- [ ] `./scripts/Test-MigrationGrants.ps1` exits 0.
- [ ] `./scripts/Update-TestUiSnapshots.ps1` captures the expected two snapshot changes.
- [ ] `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture` exits 0.
- [ ] `./scripts/Test-UiCatalogue.ps1` exits 0.
- [ ] post-implementation report written
- [ ] PR opened with Kanmer: CASE-040
