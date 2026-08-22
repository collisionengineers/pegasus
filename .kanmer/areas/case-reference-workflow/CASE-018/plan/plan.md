# Plan — CASE-018

Branch `task/qdos26011-regressions`, worktree `../pegasus-worktrees/qdos26011-regressions`, shared with [[ENG-013]], [[DOCS-009]], [[CASE-019]] — they touch the same case surfaces, so they run as one lane and one PR.

## Steps

1. **`_CaseWorkflow.cshtml` — "Case detail".** Delete the `rows` array, the `populated` filter and the `foreach` that renders them (`:96-152`). The `@if (data is not null)` wrapper and both edit forms stay. The section keeps its heading, because under an edit lease it is still the case-detail editor.

   *Reuses:* nothing new. This is deletion.

2. **`_CaseWorkflow.cshtml` — "Vehicle evidence".** Delete the `Confirmed …` and observation `<dl>` blocks. Keep `AcceptVehicleSuggestion` (both decisions) and `RequestVehicleLookup`. The section renders only when `mayEdit`, since with no values left there is nothing to read.

   *Reuses:* the existing forms, unchanged.

3. **`_CaseSummary.cshtml` — delete two blocks.** "Engineer queries" goes whole. "Where this case stands" goes, after moving its `Corrects` / `Corrected by` rows into "Case identity" — those are identity facts and have no other home. `State` is dropped: the header already renders `OperatorLabels.CaseStage` as a status chip, so it was the third place the same word appeared.

   *Reuses:* the existing `DataRow` / `PlainRow` local functions.

4. **`_CaseSummary.cshtml` — Vehicle block gains two rows.** Manufacture year and fuel type, via the same `DataRow` helper, so [[ENG-013]]'s lookup values land somewhere rather than being written and never shown.

5. **`site.css` — `.datarow` alignment.** The row is a grid; the provenance icon sits in `.datarow__end`. Rows without an icon currently collapse that track, so their value column starts at a different x than rows with one. Fix by declaring the track explicitly on `.datarow` rather than letting content size it.

   *Reuses:* the existing `.datarow` rule; no new class.

6. **Tests.** Assert on a rendered case page that `Registration` appears once, that `Where this case stands` and `Engineer queries` are absent, and that the edit form still renders under a lease.

## Acceptance

- Registration, make, model and mileage each appear exactly once on the Overview tab.
- Neither removed block appears in the markup at any lifecycle state.
- Under an edit lease, every field that could be edited before can still be edited.

## Simplification pass

Recorded after implementation, before the PR.

## Simplification pass — 2026-08-22

Run over the branch's own diff before the PR, four lenses (reuse, simplification, efficiency, altitude). The lane shares one branch, so the pass covers [[CASE-018]], [[ENG-013]], [[DOCS-009]] and [[CASE-019]] together and is recorded here once.

| Finding | Lens | Disposition |
| --- | --- | --- |
| `MapForProduction` and `MapForOperatorExport` each built the thirteen-value `EvaReplayFields` record and its provenance projection, so the field order existed twice in one file | reuse / one list per concept | **Applied** — `ToReplayFields` and `NormalizedValue` own both (`b9743538`) |
| `Text`, `ModeText` and `MileageText` in `_CaseWorkflow.cshtml` existed only to feed the removed read-only rows | simplification | **Applied** — deleted; the compiler caught them as unused |
| `.datarow__sug` had no remaining user once the read-only rows went | simplification | **Applied** — rule deleted |
| The "Case detail" section rendered a heading and nothing else outside edit mode | altitude | **Applied** — the section is now gated on `mayEdit`, since it is only an editor |
| `AddLookupSuggestionsAsync` reads existing field names once rather than probing per field | efficiency | **Applied** as written — one query, four adds, inside the caller's transaction |
| `IExportCaseBundle` has one implementation and one caller | abstraction | **Not a finding** — it is a Core port across the Core/Infrastructure boundary, which `CLAUDE.md` names as exactly the case where an abstraction is warranted |
| `EvaHandoffStore.LoadEligibleImagesAsync` repeats the candidate projection the generate path also builds | reuse | **Not applied, and named** — the two differ in what they do next (the generate path feeds `EvaHandoffPolicy.Evaluate` with its blocking reasons and needs the tracked workflow; the export needs neither). Merging them would mean threading the hand-off's policy authority into a read. Left as two readers of one selection rule, `EvaHandoffPolicy.SelectEligibleImages`, which is the part that must not diverge. |
| The Export control could name which fields are blank before download | scope | **Dropped deliberately** — new operator-facing explanatory copy, which `docs/design/README.md` forbids. The blanks are already visible on the case page and in the exported JSON. Recorded in [[CASE-019]]. |
