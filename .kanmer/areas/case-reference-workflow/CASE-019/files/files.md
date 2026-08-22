# Files — CASE-019

| File | Change |
| --- | --- |
| `src/Pegasus.Core/Eva/CaseEvaMapping.cs` | Add `MapForOperatorExport(evidence, acceptance, today)` beside `MapForProduction`. Same ordered field set, same normalisation. Blocks only on an unaccepted mapping; defaults a missing inspection date to `today`; returns the names of fields left blank instead of refusing. Add `Unrecorded` to `EvaEvidenceStatus`. |
| `src/Pegasus.Core/Eva/EvaBundleSchema.cs` | `ValidateSource` keeps every **format** rule and stops asserting the **evidence bar**. Provenance writing maps all four statuses rather than assuming accepted-or-corrected. Add the export request/result contracts and `IExportCaseBundle`. |
| `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs` | Extract the `EvaAcceptedCaseEvidence` construction inside `MapAcceptedCase` into one builder that takes the evidence tier to read. Add `ExportAsync` — a read-only path that builds the bundle and returns it, writing no revision, no proxy and no case version. |
| `src/Pegasus.Web/Pages/Cases/Documents/Export.cshtml.cs` | Add `OnGetAsync`, returning the EVA-format archive. `OnPostAsync` (selective document versions) is untouched. |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml` | `asp-route-id` → `asp-route-caseId` on the Export control, which is what makes it a link at all. Name any blank field beside it. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseDocuments.cshtml` | Render the selection column only when an edit lease is held, instead of rendering permanently disabled tickboxes. |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | Surface the blank-field names for the control. |
| `tests/…` | Core tests for the export mapping and the relaxed writer; a Web test that the Export control resolves to a route and returns an archive. |

## Why `ValidateSource` may safely stop asserting the evidence bar

`EvaHandoffStore` reaches `CreateOfflineReplay` only via `mapping.Source`, which `CaseEvaMapping.MapForProduction` returns as `null` whenever any of the thirteen fields lacks accepted, non-empty, provenanced evidence. The writer's own copies of those two rules are therefore unreachable defence-in-depth on the hand-off path, not the gate. Removing them from the writer removes a duplicated rule; the hand-off bar stays exactly where it is enforced today.

This is the "fix the constraint" route rather than threading a `strict` flag through the writer, which `CLAUDE.md` names as a smell.

## The four faults, and which file closes each

| Fault | Closed by |
| --- | --- |
| Export control generates no `href` | `Details.cshtml` — `asp-route-caseId` |
| Tickboxes permanently disabled | `_CaseDocuments.cshtml` — column hidden without a lease |
| No photograph is eligible | [[DOCS-009]] |
| Bundle refuses without an accepted mapping | [[PLAT-037]] |

## Deliberately not changed

`ExportCaseDocumentsCommand` requires an `EditLeaseToken`, so the **selective** document export stays edit-gated. That is existing Core policy and widening it is not what was asked. The operator's "unclickable tickboxes" complaint is answered by the Export control working without a lease, and by not drawing dead controls.

## Read-only checks run

- Prod Container App `pegasus-prod-web-252ow37gij` declares no `Eva*` environment variable (2026-08-22) — hence [[PLAT-037]].
- `EvaHandoffRevisions` is empty and `EvaFirstHandoffProxies` is empty: no EVA bundle has ever been generated in production, so nothing existing depends on the writer's current strictness.
- QDOS26011 is in `Review` with custody `confirmed`, so the export precondition the button states is genuinely met.
