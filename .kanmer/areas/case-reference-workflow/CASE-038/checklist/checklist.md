# Checklist — CASE-038 (2026-09-02)

- [ ] Step 1: replace the section contract with the one canonical ordered list and add the authorized fragment handler.
- [ ] Step 2: render the sticky eleven-host Case frame, server-side addressed host, ribbon slots, jump-nav, and the four heading-only shells `_CaseDamage`/`_CaseEstimate`/`_CaseSettlement`/`_CaseReport` composed with `model="Model"`.
- [ ] Step 2a: rename the inspection form to `case-inspection-address-form` without `data-edit-save` in `_CaseInspectionAddress.cshtml` (declared `Pages/Cases/Shared/*` lock exception; id, attribute and comment lines only).
- [ ] Step 3: add measured sticky geometry, lazy fragment mounting, dirty-form rebinding, query jump, and scroll-spy.
- [ ] Step 4: update Case Details and seeded three-width Browser proof.
- [ ] Step 5: apply only the declared mechanical query-key retargets in the six direct test consumers.
- [ ] Step 6: regenerate default/conflict snapshots, preserve unavailable, and conditionally correct Details catalogue wording.
- [ ] Complete the dated Simplification pass with findings and dispositions.
- [ ] Run `dotnet restore ./Pegasus.slnx --locked-mode`.
- [ ] Run `dotnet build ./Pegasus.slnx --configuration Release --no-restore`.
- [ ] Run `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"`.
- [ ] Run `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Browser&Category!=Corpus" -- xUnit.MaxParallelThreads=2`.
- [ ] Run `./scripts/Update-TestUiSnapshots.ps1`.
- [ ] Run `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture`.
- [ ] Run `./scripts/Test-UiCatalogue.ps1`.
- [ ] post-implementation report written
- [ ] PR opened with Kanmer: CASE-038
