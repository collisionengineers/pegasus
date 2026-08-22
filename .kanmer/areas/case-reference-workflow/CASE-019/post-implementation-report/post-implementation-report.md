# Post-implementation report — CASE-019

Commits `94b6a9dd` and `b9743538` on `task/qdos26011-regressions`.

## What changed

| Layer | Change |
| --- | --- |
| `CaseEvaMapping` | `MapForOperatorExport(evidence, acceptance, today)` beside `MapForProduction`. `EvaEvidenceStatus` gained `Unrecorded`, appended so no existing member's ordinal moved. `ToReplayFields` and `NormalizedValue` own the ordered field set for both methods. |
| `EvaBundleSchema` | `ValidateSource` keeps every format rule and no longer asserts the hand-off's evidence bar. `WriteProvenance` maps all four statuses. `WriteOrderedJson` writes every key as a string, empty rather than null. `ExportCaseBundleRequest` / `Result` / `IExportCaseBundle`. |
| `EvaHandoffStore` | `BuildEvidence(caseData, vehicle, includeSuggestions)` extracted from `MapAcceptedCase`; `Fallback` reads the case's own vehicle field when the confirmed record is empty. `IExportCaseBundle.ExecuteAsync` and `LoadEligibleImagesAsync` added, both read-only. |
| `Export.cshtml.cs` | `OnGetAsync` returns the archive. `OnPostAsync` untouched. |
| `Details.cshtml` | `asp-route-id` → `asp-route-caseId`. |
| `_CaseDocuments.cshtml` | The selection column renders only under an edit lease. |

## The load-bearing decision

`ValidateSource` used to assert two things that are not format rules: every field non-empty, and every status accepted or corrected. Those looked like the gate. They were not. The hand-off reaches `CreateOfflineReplay` only through `mapping.Source`, which `MapForProduction` returns as null unless all thirteen fields already carry accepted, provenanced, non-empty evidence — so both rules were unreachable duplicates of a bar enforced upstream.

Removing them from the writer was therefore a de-duplication, not a loosening, and it is what let an operator export reuse the format without a `strict` flag threaded through — the shape `CLAUDE.md` names as a smell ("a flag added so one call site can carry something past a design constraint").

That the hand-off bar did not move is asserted, not assumed: `TheSameCaseIsStillRefusedAHandoff` and `ASuggestedMileageStillCannotReachAHandoff` both drive `MapForProduction` over exactly the data the export accepts.

## Why the control did nothing

`Details.cshtml` emitted `asp-route-id`; the page's route is `/Cases/{caseId:guid}/Documents/Export`. `caseId` was never supplied and is not an ambient value of the `/Cases/{id:guid}` route the operator was standing on, so link generation produced no `href` and the anchor was inert — which reads exactly as "the button goes nowhere".

## Departure from the plan

The plan had the control naming which fields are blank before download. **Dropped.** That is new operator-facing explanatory copy, which `docs/design/README.md` forbids, and it would have needed a second store round trip per page render to compute. The blanks are already visible: the case page shows "Not recorded", and the exported JSON carries the empty key. Recorded in the simplification pass on [[CASE-018]].

## Evidence

`CaseOperatorExportTests`, seven tests, all passing inside a green 923-test Core suite:

- a blank VAT status exports and is named `Unrecorded`, and the same case is still refused a hand-off;
- an absent inspection date becomes today and records `SystemDefault:Export date`;
- a suggested mileage travels as `suggested`, and still cannot reach a hand-off;
- an unaccepted mapping refuses the export;
- the archive carries all thirteen keys in order with the blank one an empty string, named `EVA-QDOS26011.zip`.

The pre-existing `EvaBundleContractTests` — byte-identical replay, exact entry order, manifest coverage — still pass unchanged, which is the check that the format did not move.

## Still gated on

[[PLAT-037]]. Until `Eva:AcceptedMapping:*` is set on the live Container App, `IsSwitchedOn` is false and every export returns the activation-gate reason. That is applied during the release, not by this commit.
