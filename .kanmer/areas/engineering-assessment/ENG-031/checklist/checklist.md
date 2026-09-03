# Checklist — ENG-031 (2026-09-02, gpt-5.6-terra xhigh)

Execution order follows the plan's steps; the conditional approval-linkage
work in Steps 2–3 depends on the Q1 answer recorded in open-questions.

- [ ] Open questions Q1 (snapshot boundary) and Q2 (reflection disposition) answered or parked by the operator; the conditional approval-linkage rows are built only if Q1 names approval.
- [ ] Step 1: confirm the ENG-034/CASE-038 Report host, handler, script, CSS, and lock hand-offs.
- [ ] Step 2: add Core report-image curation, projection, rendering, and approval contracts.
- [ ] Step 3: add guarded persistence, approved snapshot linkage, projection filtering, and rendition rendering.
- [ ] Step 4: add the serialized migration, grants, bootstrap census, and grant assertions.
- [ ] Step 5: add Report-image labels, partial, and external cropper behaviour.
- [ ] Step 6: complete focused Core, persistence, renderer, draft-Web, and approval-Web tests.
- [ ] simplification pass recorded with dated four-lens dispositions
- [ ] `dotnet restore ./Pegasus.slnx --locked-mode`
- [ ] `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- [ ] `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"`
- [ ] `./scripts/Update-TestUiSnapshots.ps1`
- [ ] `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture`
- [ ] `./scripts/Test-UiCatalogue.ps1`
- [ ] `./scripts/Test-MigrationGrants.ps1`
- [ ] post-implementation report written
- [ ] PR opened with Kanmer: ENG-031
