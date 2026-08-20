## What shipped

1. **Drag-and-drop fix (coordinator-assigned mid-task).** Investigated honestly first: two faithful reproductions (page-level synthetic `DragEvent`, and CDP-level native drop with real file paths and hit-tested coordinates) both succeeded against the dashed dropzone itself on current `dev` HEAD — the reported failure did not reproduce there. The coordinator then supplied the real cause: drag/drop was bound only to the small dashed rectangle, so a drop landing anywhere else in the window (a heading, a panel border) fell through to the browser's default navigate-to-file action. Fixed with a document-level `dragover`/`drop` safety net plus widening the effective drop target to the containing `.panel`, reusing the existing `.is-dragover` styling and the product's one spin animation (`.is-refreshing .icon--spin`). Proved red-then-green: `NativeCdpDropOnThePanelOutsideTheDashedZoneStillPopulatesTheInput` failed pre-fix (0 files landed) and passes now.
2. **Per-file rows.** The crammed single-line readout ("the files leaking") is replaced with one row per file (name, size, state). `site.js` fetch-submits the form (opt-in via `data-upload-progress`, so the document-request upload page is untouched); every row enters "uploading" together — the honest bound, since one POST stores the whole batch and no finer per-file signal exists client-side during it — and ticks on a successful response, which is proven by the response rather than assumed. A validation failure falls back to a native re-submit (safe: nothing is ever stored on that path), showing the existing, already-correct error page rather than guessing which row to blame.
3. **Mechanics copy removed.** `UploadGroupStatus.cshtml:16`'s exact sentence is gone, plus the remaining "receipt" wording on `Upload.cshtml`'s own "What happens next" panel (the ticket's vocabulary list is stricter than design README's on this point, so this surface follows the ticket).
4. **Post-processing confirmation step.** New `UploadOutcomeQueries` (Pegasus.Web.Presentation) builds one decision per file once it's terminal: a report of what automation already did (a case already associated, or a new Image-initiated Case registered — both always automatic per the verified `AssociateCaseIfUnambiguousAsync` code path, never re-offered), routed-to-Unidentified reported with a link to its resolution surface, or the genuine staff decision (possible matching cases to review-and-attach with a free choice of destination, or no match at all with an offer to create a case). Every action routes to an existing page (`Cases/Details`, `Received/{id}` i.e. Intake/Details, `Cases/Create`, `VehicleImages/{id}`, `Unidentified/{id}`) — this surface never mutates anything. Evaluated per file, not per group, so a grouped upload's members can resolve independently (INTK-011 awareness, not fixed here).
5. **CASE-003 fixed.** `GET /Cases/Create` with no `receiptId` now returns 404 (guarded before `LoadAsync` runs) instead of throwing. In scope because the confirmation step's own "Create a case" offer always carries a real `receiptId`, but the guard is cheap, correct, and exactly CASE-003's own specified approach — a defensive backstop, not a redesign.
6. FRD-02 gained the full decision table; FRD-12 gained the operator-facing description, cross-linking rather than restating.

## The confirmation decision table (exact)

| State | What the operator sees | Action |
|---|---|---|
| Case already associated | Report: "automatically associated with case X" | Open case; quiet "Not the right case?" → Received/Intake-Details (existing reversal) |
| Registered as Image-initiated Case | Report: "registered as a new vehicle-image case X-01" | View → VehicleImages |
| Routed to Unidentified | Report: "needs a staff decision" | Review → Unidentified |
| Ambiguous candidate match | Staff offer | Review and attach → Received/Intake-Details (existing candidate UI, free choice = override) |
| No match, eligible | Staff offer | Create a case → Cases/Create?receiptId= |
| Cannot become a case / failed | Report, no offer | — |

## Honest limitations

- The "drop off the panel entirely doesn't navigate the tab away" claim could not be red/green proven via CDP simulation in this test harness (Chromium's real default-navigate action isn't triggered by CDP-injected drops either way) — the document-level `preventDefault()` is sound defensive practice, kept, but that specific test only proves the drop is swallowed and doesn't misroute files, not that it stops a real navigation.
- No visual pass at 1920 was run — no interactive browser/screenshot tool was available to this agent in this environment; stated honestly rather than claimed.
- Split-group independence (INTK-011 awareness) is covered by construction (the builder is a stateless per-call function, called once per member in its own loop) and by nine independent branch tests, not by one consolidated "two members, two different outcomes on one page" end-to-end test.

## Test results (verbatim)

- `dotnet build Pegasus.slnx --configuration Release` → **Build succeeded. 0 Warnings, 0 Errors.**
- `dotnet test tests/Pegasus.Core.Tests` → **Passed! Failed: 0, Passed: 684, Skipped: 0.**
- `dotnet test tests/Pegasus.ArchitectureTests` → **Passed! Failed: 0, Passed: 97, Skipped: 0.**
- `dotnet test tests/Pegasus.IntegrationTests --filter "FullyQualifiedName~Upload|FullyQualifiedName~GroupedIntake|FullyQualifiedName~IntakeWeb|FullyQualifiedName~Cases"` → **Passed! Failed: 0, Passed: 93, Skipped: 6** (the 6 skips are pre-existing, corpus-data-gated, unrelated to this change — verified unchanged before/after).
- `dotnet test tests/Pegasus.IntegrationTests --filter "Category=Browser"` → **Passed! Failed: 0, Passed: 43, Skipped: 0** (includes the two new INTK-010 Browser test files, run twice — once pre-fix to confirm red, once post-fix).

## Simplification pass

Four parallel lenses (reuse, simplification, efficiency, altitude) over the full diff, dated 2026-08-20 in plan.md:
- **Reuse**: no findings.
- **Efficiency**: one real finding, applied — `UploadGroupStatus.cshtml.cs`'s per-member reads were sequential despite each store using its own `DbContext`; parallelised with `Task.WhenAll`. Verified: full re-test green.
- **Simplification**: four minor test-JS/cosmetic-JS duplication findings, skipped (fix risk to already-green tests outweighed a 2-5 line duplication); recorded with reasons.
- **Altitude**: no findings requiring a change; confirmed the drag-fix layer, the `data-upload-progress` convention, and the decision-eligibility check are each the right depth, not bandaids. One pre-existing (not introduced here) duplicate `IntakeDecision` label switch in `Intake/Details.cshtml.cs` noted for a future ticket, not actioned.

## Files changed

`src/Pegasus.Web/Pages/Upload.cshtml`, `UploadStatus.cshtml(.cs)`, `UploadGroupStatus.cshtml(.cs)`, `Cases/Create.cshtml.cs`, `Shared/_UploadOutcome.cshtml` (new), `Presentation/UploadOutcome.cs` (new), `Presentation/OperatorLabels.cs`, `Program.cs`, `wwwroot/js/site.js`, `wwwroot/css/site.css`; `tests/Pegasus.IntegrationTests/Browser/UploadDropzoneBrowserTests.cs` (new), `Browser/UploadRowsBrowserTests.cs` (new), `UploadOutcomeQueriesTests.cs` (new), `CaseCreateWebTests.cs`, `QdosIntakeWebTests.cs`; `docs/frd/frd-02-intake-and-source-identity.md`, `docs/frd/frd-12-operator-experience.md`.

## PR

Branch `task/intk-010-upload-flow-v2`, 4 commits, pushed to `origin`. PR opened to `dev`, not merged.
