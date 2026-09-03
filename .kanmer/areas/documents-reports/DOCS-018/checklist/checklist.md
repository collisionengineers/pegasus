# Checklist — DOCS-018 (2026-09-02; revised 2026-09-03 after plan review)

- [ ] Step 1a: Record the ownership reallocation before touching any shared file — `append_scratch` on [[ENG-029]] naming the `OnGetPreviewFeeNoteAsync` handler, the `_CaseReport.cshtml` action anchor and the `AssessmentReportDraftWebTests` rows DOCS-018 takes; if a capture is committed in step 5, `append_scratch` on [[UIIMP-014]] naming it.
- [ ] Step 1b: Confirm [[CASE-038]], [[ENG-034]], and [[ENG-029]] are merged to `dev`, refresh the task worktree with `git merge --no-edit origin/dev`, and take the sequential leases on `Details.cshtml.cs`, `_CaseReport.cshtml`, `OperatorLabels.cs` and `AssessmentReportDraftWebTests.cs`. If any lease is held, stop and report rather than editing the file.
- [ ] Step 2: Add `OnGetPreviewFeeNoteAsync` to `DetailsModel`, reusing the existing report-draft preview flow (`TryGetActor`, `GenerateCaseAssessmentReportDraft`, `NotFound`/`NotReady` mapping) and returning `Draft.FeeNote.Pdf` inline as `application/pdf`.
- [ ] Step 3: Add the single `PreviewFeeNote` key to `OperatorLabels.CaseWorkspace.EngineerSections` and the adjacent `Preview fee note` anchor beside `Preview report draft` in `_CaseReport.cshtml`, built with the same `asp-page` / `asp-route-id` / `asp-page-handler` tag helpers the existing control uses (no hand-built `?handler=` URL), `target="_blank"`, `rel="noopener"`, and the same visibility condition; no partial, dialog or copy.
- [ ] Step 4a: Give `FakeRenderer` distinct bytes per artifact family before writing the new assertion — both artifacts share `pdfBytes` today, so a fee-note assertion cannot fail until they differ. Weaken no existing assertion.
- [ ] Step 4b: Add GET coverage of `PreviewFeeNote` in `AssessmentReportDraftWebTests` using `Compose`, `FakeRenderer` and `ThrowingDocumentContentStore`, for all three mapped outcomes: ready → 200 `application/pdf` with the fee-note bytes specifically; not-ready → the `NotReady` redirect; unopenable Case → 404.
- [ ] Run the simplification pass over the branch diff and record findings and dispositions under a dated "Simplification pass" heading in this ticket's plan.
- [ ] Run `dotnet restore ./Pegasus.slnx --locked-mode`.
- [ ] Run `dotnet build ./Pegasus.slnx --configuration Release --no-restore`.
- [ ] Run `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"`.
- [ ] Run `./scripts/Update-TestUiSnapshots.ps1`, then `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture` and `./scripts/Test-UiCatalogue.ps1`; commit a changed `docs/design/test-ui/pages/case-details--*.html` capture in the same PR, revert and report any other test-ui change.
- [ ] post-implementation report written
- [ ] PR opened with Kanmer: DOCS-018
