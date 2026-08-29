# CASE-027 checklist

- [ ] Take the ticket on `task/case-027-case-detail-views` /
      `../pegasus-worktrees/case-027-case-detail-views`.
- [ ] `_CaseVehicle.cshtml`: facts, Vehicle checks (DVLA + DVSA/MOT posting
      `RequestVehicleLookup`), Experian D7 seam with `data-condition`, the
      observation state list, and the Accept/Correct suggestion forms posting
      `AcceptVehicleSuggestion`.
- [ ] `_CaseDataHiddenFields.cshtml`: all twenty `CaseEditableData` values.
- [ ] `_CaseInspectionAddress.cshtml`: recorded value, provider default,
      inspection mode, and the edit form posting `Details?handler=Save`.
- [ ] `_CaseFiles.cshtml`: documents + the two galleries moved out of
      `Details.cshtml`.
- [ ] `_CaseDocuments.cshtml`: design-system restyle, custody chip,
      Preview / Save as, Add evidence, Open Operations, explanatory sentence
      deleted.
- [ ] `Details.cshtml`: four section branches replaced by four partials
      (lane E1's file — reported).
- [ ] `_CaseWorkflow.cshtml`: two missing hidden inputs added (lane E1's
      file — reported).
- [ ] `OperatorLabels.cs`: one appended nested class, nothing reordered.
- [ ] `catalogue.json`: Vehicle/Custody/Tasks → `protocol`. No snapshot
      regeneration.
- [ ] Tests added to the three owned test files; no existing assertion
      weakened, skipped or deleted.
- [ ] `dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false`
      green, zero `CS####` diagnostics.
- [ ] Focused test filters run and their real counts recorded.
- [ ] Simplification pass run over the branch diff; findings and
      dispositions written into the plan.
- [ ] Commits pushed, post-implementation report written, PR opened against
      `dev`, PR number recorded, ticket moved to `review`.
