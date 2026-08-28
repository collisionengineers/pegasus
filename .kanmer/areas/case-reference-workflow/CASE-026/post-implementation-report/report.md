# Post-implementation report — CASE-026

PR: https://github.com/collisionengineers/pegasus/pull/606 (targets `dev`,
stops at the open PR per the lane brief).

## Delivered

- `src/Pegasus.Web/Pages/Search/Index.cshtml(.cs)` rewritten to §1.7:
  page-header with freshness + Create Case, advanced-search-grid with the
  ten UI-07 fields (Search dark, Clear), Vehicle images section, two-pane
  `case-search-layout` with selectable rows (`tr[data-select-href]`,
  per-row preview `<template>`, `aria-selected`, roving arrows) and the
  server-rendered Selected Case pane for `?selected=`.
- `src/Pegasus.Web/Pages/Search/_CasePreview.cshtml` new partial (facts +
  Outstanding (n) + Open Case; Copy Case/PO rendered by the page outside
  the swapped region — plan P7).
- Named Core extension: `CaseSearchItem` + `VehicleMake`/`VehicleModel`/
  `AccidentCircumstances`, projected by `EfCaseQueryStore` from the joined
  instruction draft (display facts only; no filter or migration change).
- `tests/Pegasus.IntegrationTests/CasesIndexWebTests.cs`: three existing
  contracts kept verbatim; new `SelectedRowPreviewsItsFactsServerSide` and
  D3 closed-outcome assertions on the recording fake.

## Verification status

- `dotnet restore --locked-mode` + `dotnet build -c Release`: PASS
  (0 warnings, 0 errors) on the task branch.
- Tests NOT run in this lane (orchestrator owns the wave loop); the test
  file compiles against the new shapes.
- Orchestrator items outstanding: full test loop, snapshot regen
  (catalogue states unchanged: default/empty/unavailable), browser walk
  1580/1100/760, live `/Cases?query=` 301 re-check.

## Deviations / findings

1. Prototype's Principal/State/Engineer/Origin selects were fixture-driven;
   control types follow the ticket's "1:1 to the existing UI-07 inputs"
   (State stays the enum select; the others stay text inputs) — plan P3.
2. site.js binds `[data-copy-target]` once at load, so a Copy button
   inside the script-swapped preview would never render; it is placed in
   the stable pane footer bound to the server-selected row. After a
   script swap the copyable reference lags the previewed facts until
   re-request. Suggested follow-up ticket (site.js is PLAT-029's file):
   bind copy by delegation.
3. `Next action` falls back to "Not recorded" when nothing is outstanding
   (the row projection carries no due-work state) — plan P5.
