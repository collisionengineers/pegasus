# Plan — PLAT-010: strip UI narration to the design rule

## Approach
Per page: classify every prose block as (a) lede/subtitle/obvious-action narration → delete,
(b) multi-sentence guidance → compress to one sentence beside the control it governs, (c)
operational knowledge worth keeping → move to docs (none found — every retained block was
either approved necessary copy already scoped narrowly, or fit in one sentence beside its
control), (d) approved necessary copy (validation, empty-state, honest status) → keep as-is.
Reused the existing `Shared/_StatusChip` partial and `Intake.DetailsModel.SourceChannelLabel`
static label map rather than inventing new display helpers (existing convention wins).

## Scope carve-outs received mid-flight (operator, via coordinator)
1. `Pages/Unidentified/Index.cshtml` and `Pages/Unidentified/Details.cshtml` excluded — a
   structural rebuild (Queues tab, image/e-mail filters) owns them under a new ticket. One
   accidental edit (lede removal on `Details.cshtml`) was made before the message landed and
   was reverted with `git checkout --`.
2. `Pages/Upload.cshtml`, `Pages/UploadGroupStatus.cshtml` (does not exist in this checkout —
   likely `UploadStatus.cshtml`, treated as excluded to be safe), `Pages/UploadStatus.cshtml`,
   and `wwwroot/js/site.js` excluded — a structural rebuild of the upload flow owns them.
   Edits already made to `UploadStatus.cshtml` (lede→status-card, raw-GUID `Receipt` row
   removed) were reverted with `git checkout --`. `Upload.cshtml` and `site.js` were never
   touched.

## Per-page disposition

| Page | Deleted | Compressed | Moved to docs | Kept | Banned-term / identifier-leak fixes |
|---|---|---|---|---|---|
| `Cases/Assessment/Index.cshtml` | 9 (6 section ledes, 3 narration) | 8 | 0 | 16 | none found |
| `Cases/Shared/_CaseWorkflow.cshtml` | 0 | 11 | 0 | 13 | `artifact`×3 (labels/heading rewritten to "report"), `bounded`×1 |
| `Intake/Details.cshtml` (+`.cshtml.cs`) | 3 (page ledes → 2 became compact status-cards, 1 dropped) | 8 | 0 | 6 | `intake` sweep: eyebrow/title "Received review", h2 "Intake resolution"→"Resolution", "Reason for blocking intake"→"Reason for blocking", "Block intake"→"Block", "this intake receipt"→"this received item"; `.cshtml.cs` TempData strings "the intake receipt was..."→"the received item was..." (2) |
| `Triage/Details.cshtml` | 0 | 2 | 0 | 9 | none found |
| `Cases/Create.cshtml` | 0 | 2 | 0 | 6 | none found (page already avoided "intake" by design, per its own code comment) |
| `Administration/Index.cshtml` | 2 (tile blurbs made redundant with the link name) | 6 (tiles cut to short clause) | 0 | 0 | none found |
| `Administration/Automation/Index.cshtml` (+`.cshtml.cs`) | 0 | 5 | 0 | 2 | `ingress`×2→removed, `composed`×2→"is not part of this deployment", `correlation identifier`×1→removed |
| `Cases/Assessment/Suggestions.cshtml` | 1 (`ViewData["Lede"]`) | 2 | 0 | 4 | none found |
| `ImageIntake/Details.cshtml` | 4 dt/dd rows (raw GUID/token/hash — design :168) | 2 | 0 | 1 lede→`_StatusChip` | raw-GUID "Origin receipt" removed, raw "Source receipt token"/"Source hash"/"Evaluation revision" GUID/hash removed, "Source channel" now goes through `SourceChannelLabel` instead of raw enum |
| `Administration/Principals/Index.cshtml` | 0 | 2 | 0 | 1 | `projection`+`bounded`→"Not every principal is shown; more exist for this organization." |
| `Operations/Index.cshtml` | 0 | 1 | 0 | 4 | `bounded`→"Showing recent operational results" |
| `Mail/Message.cshtml` | 0 | 0 | 0 | (unchanged) | reviewed, none found |
| `Administration/Automation/Activity.cshtml` | 0 | 1 | 0 | 1 | `correlation identifier`→"activity reference" (label, column header, empty-state, comment) |
| `Administration/Organizations/Edit.cshtml` | 0 | 0 | 0 | 1 rewritten | `projection`+`bounded`→"Not every principal is shown; more exist for this organization." |
| `Administration/Organizations/Index.cshtml` | 0 | 0 | 0 | 1 rewritten | `bounded`→"More exist" |
| `Administration/Principals/Create.cshtml` | 1 (`ViewData["Lede"]`, redundant with surviving field hint) | 0 | 0 | 1 rewritten | `bounded`→"Not every organization is listed; use the Organizations workspace to find one that isn't shown." |
| `Administration/Principals/Replace.cshtml` | 0 | 0 | 0 | 1 rewritten | `bounded`→"Not every organization is listed; confirm one that isn't shown in the Organizations workspace." |
| `Cases/Details.cshtml` | 0 | 1 | 0 | 0 | `projection`→"The case could not be loaded; reload the page to try again." |
| `Cases/Shared/_CaseSummary.cshtml` | 0 | 1 | 0 | 0 | `artifact`×2 (heading "Approved report artifact"→"Approved report", dt "Artifact"→"Report") |
| `ImageIntake/Index.cshtml` | 1 (page lede) | 0 | 0 | 1 | none found |
| `Administration/Access/Index.cshtml` | 0 | 1 | 0 | 0 | none found |
| `Administration/Accounts/Edit.cshtml` | 0 | 1 | 0 | 0 | none found |
| `Administration/Configuration.cshtml` | 1 sentence (safety reassurance, redundant — page genuinely has no such controls) | 1 | 0 | 0 | none found |
| `Administration/Roles/Index.cshtml` | 0 | 2 (duplicate instances of the same 3-sentence block) | 0 | 0 | none found |
| `Cases/Index.cshtml` | 0 | 1 | 0 | 2 | `projection`→"The case query could not be completed; try again." |
| `Connect/Authorize.cshtml` | 1 sentence (redundant with H1 + Connector row) | 1 | 0 | 0 | none found (MCP/connector framing kept — this is a technical OAuth-consent screen for administrators, not general operator copy) |
| `Uploads/Request.cshtml` | 0 | 1 | 0 | 2 | none found |
| `Shared/_PageHeader.cshtml` | 1 (the lede slot itself — no caller passes one any more) | — | — | — | — |

Pages swept with zero violations found (no diff): `Account/*`, `Administration/Accounts/Index.cshtml`,
`Cases/Custody.cshtml`, `Cases/Documents/*`, `Cases/Eva/Download.cshtml`, `Cases/Tasks.cshtml`,
`Cases/Vehicle.cshtml`, `Cases/Workflow.cshtml`, `Cases/Shared/_CaseDocuments.cshtml`,
`Cases/Shared/_CaseHistory.cshtml`, `Search/Index.cshtml`, `Intake/Source.cshtml`, `Mail/Index.cshtml`,
`Mail/Message.cshtml`, `Triage/Index.cshtml`, all remaining `Shared/_*.cshtml` partials,
`Uploads/_ViewStart.cshtml`, `Index.cshtml`.

## Deliberate non-changes (reasoned, not oversights)
- **`custody` terminology** (`Cases/Custody.cshtml`, "Document custody"/"Case custody" section
  headers, `_ProvenancePanel.cshtml`'s "Custody Hash" dt) is NOT on the design README's literal
  banned-word list and is established, extensively-used, correct domain language (chain of
  custody), distinct from development jargon like "intake". Left unchanged. The one place the
  operator's report named "custody detail" as a leak (`Unidentified/Details.cshtml:56`) is on an
  excluded page.
- **"AI" / "Send to AI" / "Send to Claude"** kept — these are the settled, already-approved
  control names (design README's own "Send to Claude" divergence section, and the existing
  "Send to AI switch" on Automation), not the "AI mechanics vocabulary" the banned-terms rule
  targets.
- **`Administration/Automation/Index.cshtml.cs` `IngressComposed` / `SendComposed` C# identifiers**
  kept — internal code identifiers are explicitly exempted by the design rule ("the ban is on
  what an operator reads").
- **Raw GUID leaks not fixed — no better handle exists without a model/handler change (reported,
  not invented):**
  - `Administration/Automation/Activity.cshtml:65` — `@record.SubjectId` (a raw staff GUID) in
    the "Subject" column. `AutomationActivityRecord` carries no display name, only the GUID
    string; resolving it to a name needs a genuine query/handler change, out of this ticket's
    copy-only scope.
  - `Cases/Shared/_CaseSummary.cshtml:208` — `@approval.ApprovedBy.SubjectId` in the "Actor" row.
    Same shape, same reason.
  - Both are legitimate follow-up findings for a ticket that can touch handlers/queries.

## Simplification pass (dated 2026-08-20)
n/a — copy-only diff (33 files, +106/-139 lines, all string/markup substitution or deletion).
No new abstractions were introduced; the two constructs reused are pre-existing conventions
(`_StatusChip` partial, `Intake.DetailsModel.SourceChannelLabel`). No duplication, dead code, or
efficiency issue found on review of the diff.

## Tests
- `dotnet build ./Pegasus.slnx -c Release` — 0 warnings, 0 errors.
- `dotnet test tests/Pegasus.IntegrationTests -c Release --filter "Category=Browser"` — 37/37 passed
  (includes Playwright/AccessibilityTests; chromium installed via `playwright.ps1 install chromium`).
- `dotnet test tests/Pegasus.IntegrationTests -c Release --filter "FullyQualifiedName~WebTests"` —
  132/132 passed, 11 skipped (pre-existing skips, unrelated). 3 pre-existing failures fixed by
  updating assertions to the new honest copy (all three were asserting text this ticket
  deliberately removed/changed, per the ticket's own instruction to update tests only where the
  removed text was the thing asserted):
  - `CaseReportApprovalWebTests.ReportApprovalPostUsesServerActorStableArtifactIdentityAndNoCallerTime`
    — asserted the old button label `"Approve immutable report artifact"`; updated to
    `"Approve immutable report"` (the `artifact` banned-term fix).
  - `CaseDetailsWebTests.ARetainedValueTooLongToKeepIsReportedRatherThanTrimmedQuietly` —
    asserted `"Re-enter those in full"` (capital R, separate sentence); the two-sentence notice
    was compressed to one via semicolon, lower-casing the join; updated assertion to
    `"re-enter those in full"`.
  - `OrganizationAdministrationWebTests.AdministratorRoutesAreDiscoverableAndPostThroughCoreEfCallers`
    — asserted `"immutable identity"` from the removed `ViewData["Lede"]` on
    `Principals/Create.cshtml`; updated to assert `"cannot be edited"`, the surviving field-level
    hint beside the Code input that already carries the same fact.
- `dotnet test tests/Pegasus.Core.Tests -c Release` — 684/684 passed.

All runs above executed to completion; nothing could not be run.

## Review fix (2026-08-20)
PR #431 review flagged three lines still violating the rule after the initial sweep:
- `Cases/Shared/_CaseWorkflow.cshtml:583` — "custody-confirmed" (operator-facing "custody") and
  "deterministic order" (mechanics vocabulary) → "The hand-off includes every eligible confirmed
  vehicle image, in a fixed order; EVA owns selection from there."
- `ImageIntake/Details.cshtml:91` — "Image intake" (superseded term; settled operator term per
  INTK-008 is "Image-initiated Case") and "origin receipt" (mechanics) →
  "Pre-report instructed cases whose confirmed registration matches this Image-initiated Case;
  linking is a reasoned staff action."
- `Intake/Details.cshtml:172` — same superseded term, and "pre-Case Image intake" contradicted
  the settled model outright → "Image-only material with a usable registration becomes an
  Image-initiated Case with a permanent reference; it never becomes a formal Case by itself."

The two remaining grep hits the reviewer checked (`Pages.Intake.DetailsModel` fully-qualified
namespace reference, `id="intake-resolution-title"` attribute) are not operator-visible copy —
confirmed not to need changing.

Re-ran after the fix: build 0 warnings/0 errors; WebTests 132/132 passed (0 failed, 11
pre-existing skips); Browser category 37/37 passed. No test asserted the old text, so no test
files needed updating this round. Commit `55fef8ef`, pushed to `task/plat-010-copy-strip`.
