# CASE-027 checklist

- [x] Take the ticket on `task/case-027-case-detail-views` /
      `../pegasus-worktrees/case-027-case-detail-views`.
- [x] `_CaseVehicle.cshtml`: facts, Vehicle checks (DVLA + DVSA/MOT posting
      `RequestVehicleLookup`), Experian D7 seam with `data-condition`, the
      observation state list, and the Accept/Correct suggestion forms posting
      `AcceptVehicleSuggestion`.
- [x] `_CaseDataHiddenFields.cshtml`: all twenty `CaseEditableData` values.
- [x] `_CaseInspectionAddress.cshtml`: recorded value, provider default,
      inspection mode, and the edit form posting `Details?handler=Save`.
- [x] `_CaseFiles.cshtml`: documents + the two galleries moved out of
      `Details.cshtml`.
- [x] `_CaseDocuments.cshtml`: design-system restyle, custody chip,
      Preview / Save as, Add evidence, Open Operations, explanatory sentence
      deleted.
- [x] `Details.cshtml`: four section branches replaced by four partials
      (lane E1's file — reported), plus the empty-gate-pill fix the new
      page-wide assertion caught.
- [x] `_CaseWorkflow.cshtml`: two missing hidden inputs added (lane E1's
      file — reported).
- [x] `OperatorLabels.cs`: one appended nested class, nothing reordered.
- [x] `catalogue.json`: Vehicle/Custody/Tasks → `protocol`. No snapshot
      regeneration.
- [x] Tests added to the three owned test files; no existing assertion
      weakened, skipped or deleted (53 pass, up from 42 by 11 new results).
- [x] `dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false`
      green, zero `CS####` diagnostics.
- [x] Focused test filters run and their real counts recorded
      (`CaseDetailsWebTests` 53/53, `ImageViewing|ImageIntake` 5/5,
      `Pegasus.ArchitectureTests` 100/100).
- [x] Simplification pass run over the branch diff; thirteen findings and
      dispositions written into the plan.
- [x] Commits pushed, post-implementation report written, PR #631 opened
      against `dev` and recorded, ticket moved to `review`.
