## Owned by this ticket (per the brief)

- `src/Pegasus.Web/Pages/Upload.cshtml` — per-file row markup (progressive-enhancement fallback), copy.
- `src/Pegasus.Web/Pages/Upload.cshtml.cs` — no behaviour change to the POST contract itself (single group submission, replay token unchanged); may add a JSON-friendly response shape if the fetch-submit needs one (see plan — decided against; reuses the existing redirect).
- `src/Pegasus.Web/Pages/UploadGroupStatus.cshtml` / `.cshtml.cs` — delete the mechanics copy; add the confirmation section (loops `Group.Members`, one outcome each).
- `src/Pegasus.Web/Pages/UploadStatus.cshtml` / `.cshtml.cs` — add the confirmation section (single member).
- `src/Pegasus.Web/wwwroot/js/site.js` — per-file row rendering + spinner/tick state; drag-and-drop fix (already applied, see research.md).
- `src/Pegasus.Web/wwwroot/css/site.css` — new rules for file rows only if the existing `.dropzone__file`/`.status-chip` primitives cannot express them without a new class (expect a small addition: a file-row list, reusing `.status-chip`/`.icon--spin` conventions).

## Read, not owned (reused, not modified)

- `src/Pegasus.Core/Intake/GroupedIntake.cs`, `IntakeContracts.cs`, `DurableIntake.cs`, `IntakeDecisionPolicy.cs`, `CaseMatching/CaseMatchContracts.cs` — existing Core contracts the confirmation view-model reads.
- `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs` (`IImageIntakeQueries.GetByOriginReceiptAsync`) and `src/Pegasus.Core/Intake/Unidentified/UnidentifiedContracts.cs` (`IUnidentifiedStore.GetByOriginAsync`) — read-only lookups for routing links.
- `src/Pegasus.Web/Pages/Intake/Details.cshtml(.cs)`, `src/Pegasus.Web/Pages/Cases/Create.cshtml(.cs)`, `src/Pegasus.Web/Pages/Cases/Details.cshtml`, `src/Pegasus.Web/Pages/ImageIntake/Details.cshtml`, `src/Pegasus.Web/Pages/Unidentified/Details.cshtml` — link targets only; not edited except CASE-003 below.
- `src/Pegasus.Web/Pages/Shared/_StatusChip.cshtml` — extended with new state keys (`uploading`, `stored`, per-file "failed") rather than duplicating its tone/icon switch; this is the existing "single place a state chooses its visual treatment" (its own doc comment) and the simplicity rule ("one list per concept") requires reusing it rather than a second table in `site.js`/CSS.
- `src/Pegasus.Web/Pages/Shared/_LucideSprite.cshtml`, `src/Pegasus.Web/wwwroot/images/lucide-sprite.svg` — existing `icon-check-circle`/`icon-alert-circle`/`icon-refresh-cw` glyphs reused for stored/failed/uploading; no new glyph needed (design README:337-347 forbids drawing a new one without the checksummed-asset process, and none is required).

## New

- A small server-side view-model builder (Core-adjacent presentation helper in `Pegasus.Web`, e.g. `Pegasus.Web/Presentation/UploadOutcome.cs`) that composes the decision table from `research.md` into one `UploadOutcomeView` per member, used by both `UploadStatusModel` and `UploadGroupStatusModel`. One place, not duplicated per page — required by the "one list per concept" rule since the same seven-branch decision applies on both surfaces.
- `src/Pegasus.Web/Pages/Shared/_UploadOutcome.cshtml` — the confirmation partial rendering one `UploadOutcomeView` (used by both status pages), following the existing partial convention (`_StatusChip`, `_FreshnessBanner`).
- `tests/Pegasus.IntegrationTests/Browser/UploadDropzoneBrowserTests.cs` — already added (drag-and-drop fix).
- New Browser/integration tests for per-file rows and the confirmation decision table (see plan/checklist).

## CASE-003 (in scope, small)

- `src/Pegasus.Web/Pages/Cases/Create.cshtml.cs` — guard `receiptId == Guid.Empty` in `OnGetAsync`, return `NotFound()` before `LoadAsync` runs, per CASE-003's own approach.

## Docs updated in the same PR

- `docs/frd/frd-02-intake-and-source-identity.md` — upload/identity behaviour: per-file row states, confirmation decision table.
- `docs/frd/frd-12-operator-experience.md` — the operator-facing upload/confirmation surface description.
- `docs/capabilities.md` — only if this surface has no existing canonical-owner row already covering it (checked in plan; INT-28's row already describes the automation, no new capability ID expected).

## Not touched (explicitly out of scope, owned elsewhere)

- `src/Pegasus.Web/Pages/Triage/Index.cshtml*`, `src/Pegasus.Web/Pages/Unidentified/Index.cshtml*` — INTK-009.
- Any Core routing/race behaviour for grouped image members (INTK-011).
- `src/Pegasus.Core/ImageIntake/*` write paths, `src/Pegasus.Core/Intake/Unidentified/*` write paths — read-only use only.
