# Checklist — CASE-032

- [x] Step 1: Add `ImageCustodyState` to Core and the nullable `Custody` member to `ImageIntakeSummary` (before the defaulted parameters) and to `ImageIntakeDetail`.
- [x] Step 2: Select `CustodyState` in the existing `ProjectAsync` select and in `ToDetailAsync`; add no query.
- [x] Step 3: Add the `OperatorLabels.ImageCustodyState` mapping in a CASE-032 block; render `files·custody` in `ImageRow` via `Join` (null custody renders the file count alone); pass `Custody` through `Pages/Search/Index.cshtml.cs`.
- [x] Step 4: Append `Reference` (`InstructionDraft.ClaimNumber`) and `Provider` (`InstructionDraft.SuggestedPrincipalCode`) to `TriageSummary`, both nullable, no defaults.
- [x] Step 5: Left-join `InstructionDrafts` on `OriginReceiptId` inside `EfTriageStore.ListAsync` **and** `GetByOriginReceiptAsync`; one statement each, no per-row lookup.
- [x] Step 6: Render `ref·reg` as the `TriageRow` title and `provider·assignee` as its meta; add both to the quick detail list; leave tabs, rail, filters and the loader bodies untouched for CASE-042.
- [x] Step 7: Extend the image-row test to assert reference, registration, file count and custody separately; add a Triage-row test seeded through `StoreMinimalReceiptAsync` (with an `InstructionDraft`), `ICreateTriageFromIntake` and `IAssignTriage`, asserting all four halves individually.
- [x] Update the two Core test helpers whose positional summary construction breaks (`ImageIntakeCasePairingTests.Summary`, `DashboardBoundaryTests.NewTriage`). A third site was also found and fixed: `ReconcileUnidentifiedDestinationsTests.cs` — see post-implementation report Deviations.
- [x] Run `dotnet restore ./Pegasus.slnx --locked-mode`.
- [x] Run `dotnet build ./Pegasus.slnx --configuration Release --no-restore`.
- [x] Run `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build`.
- [x] Run `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build`.
- [x] Run `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~TriageQueuesWebTests"`.
- [x] Step 8: Regenerate and verify the `/Cases` Test UI snapshots with UIIMP-015's scoped capture as merged, run `./scripts/Test-UiCatalogue.ps1`, and record the artifact's byte size and doctype (or that it was byte-identical). (Ran the full capture — the scoped switches are not present at this branch's base, as the plan anticipated.)
- [x] Run the simplification pass and record findings and dispositions in the plan.
- [x] Write the post-implementation report.
- [x] Open a PR with `Kanmer: CASE-032`. Do not merge it. (PR #659)
