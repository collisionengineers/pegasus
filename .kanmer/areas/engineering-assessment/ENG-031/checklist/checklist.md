# Checklist — ENG-031 (2026-09-02, gpt-5.6-terra xhigh; revised 2026-09-03 after plan review)

Execution order follows the plan's steps. Q1 and Q2 are resolved (2026-09-03),
so no row is conditional; the plan review of 2026-09-03 added rows 2, 6, 8, 10
and 11.

- [ ] Step 1: confirm the ENG-034/CASE-038 Report host, handler, script, CSS, and lock hand-offs.
- [ ] Step 1: confirm the PLAT-070 (`CaseWorkflowContracts.cs`), CASE-040 (`CaseLifecycle.cs`) and ENG-036/DOCS-018 (`PlaywrightAssessmentReportRenderer.cs`) releases before taking those files.
- [ ] Step 2: add Core report-image curation, projection, rendering, and approval contracts.
- [ ] Step 2: one Core eligibility owner called by both EVA and report curation; no copied predicate list.
- [ ] Step 3: add guarded persistence, approved snapshot linkage, projection filtering, and rendition rendering.
- [ ] Step 3: exactly one curation record per curated image, `Not used` included; both entry points update it.
- [ ] Step 4: add the serialized migration, grants, bootstrap census, and grant assertions.
- [ ] Step 5: add Report-image labels, partial, and external cropper behaviour.
- [ ] Step 5: Files-viewer Crop entry point — gallery case/document identity, trigger injected into the existing viewer footer, no `_EvidenceViewer.cshtml` edit, lease claimed on save (D46).
- [ ] Step 6: complete focused Core, persistence, renderer, draft-Web, and approval-Web tests.
- [ ] Step 6: `Browser/ReportImageCropBrowserTests.cs` proves both crop entry points and the D46 interactions.
- [ ] simplification pass recorded with dated four-lens dispositions
- [ ] `dotnet restore ./Pegasus.slnx --locked-mode`
- [ ] `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- [ ] `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"`
- [ ] `./scripts/Update-TestUiSnapshots.ps1`
- [ ] `./scripts/Update-TestUiSnapshots.ps1 -Verify`
- [ ] `./scripts/Test-UiCatalogue.ps1`
- [ ] `./scripts/Test-MigrationGrants.ps1`
- [ ] post-implementation report written
- [ ] PR opened with Kanmer: ENG-031
