# Checklist — DOCS-018 (2026-09-02)

- [ ] Step 1: Confirm [[CASE-038]], [[ENG-034]], and [[ENG-029]] are merged to `dev`, refresh the task worktree with `git merge --no-edit origin/dev`, and take the sequential leases on `Details.cshtml.cs`, `_CaseReport.cshtml`, `OperatorLabels.cs` and `AssessmentReportDraftWebTests.cs`.
- [ ] Step 2: Add `OnGetPreviewFeeNoteAsync` to `DetailsModel`, reusing the existing report-draft preview flow (`TryGetActor`, `GenerateCaseAssessmentReportDraft`, `NotFound`/`NotReady` mapping) and returning `Draft.FeeNote.Pdf` inline as `application/pdf`.
- [ ] Step 3: Add the single `PreviewFeeNote` key to `OperatorLabels.CaseWorkspace.EngineerSections` and the adjacent `Preview fee note` anchor (`target="_blank"`, `rel="noopener"`, `?handler=PreviewFeeNote`) beside `Preview report draft` in `_CaseReport.cshtml`; no partial, dialog or copy.
- [ ] Step 4: Add the direct fee-note Web endpoint test in `AssessmentReportDraftWebTests` using `Compose`, `FakeRenderer` and `ThrowingDocumentContentStore`; assert OK, `application/pdf` and the fake fee-note bytes.
- [ ] Run the simplification pass over the branch diff and record findings and dispositions under a dated "Simplification pass" heading in this ticket's plan.
- [ ] Run `dotnet restore ./Pegasus.slnx --locked-mode`.
- [ ] Run `dotnet build ./Pegasus.slnx --configuration Release --no-restore`.
- [ ] Run `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"`.
- [ ] Run `./scripts/Update-TestUiSnapshots.ps1`, then `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture` and `./scripts/Test-UiCatalogue.ps1`; commit a changed `docs/design/test-ui/pages/case-details--*.html` capture in the same PR, revert and report any other test-ui change.
- [ ] post-implementation report written
- [ ] PR opened with Kanmer: DOCS-018
