# Estimate document (PDF) — plan

Snapshot: 16 September 2026. Plan only — nothing is implemented. Sprint
handover file in `1609sprint/`, outside the Markdown placement allowlist; leave
it uncommitted unless separately requested.

Companion files: [evaestimate-extracted.md](evaestimate-extracted.md) (the
reference decoded, every figure reconciled), [evaestimate-page1.png](evaestimate-page1.png)
(the reference rendered), [reference-repairable-report-p1.png](reference-repairable-report-p1.png)
(page 1 of the accepted `rendererref1` house style, for comparison).

## 1. Aim

Produce a one-document PDF of any estimate on a Case — the itemised repair
specification with its hours, rate, discounts and money — equivalent in
content to the EVA estimate sheet (`evaestimate.pdf`), rendered in the accepted
Collision Engineers document style that the assessment report and fee note
already use, from Pegasus's own recorded facts and its one money owner.

Working name: **Estimate document**. Not "report", not "breakdown" in code:
`CONTEXT.md` reserves no term for it, RPT-02 already calls the content the
"itemised repair-specification breakdown", and the v27 `attach` proposal
calls the delivery attachment "Breakdown". The operator label is the one
decision the glossary needs (decision **A**).

## 2. What was examined

- `1609sprint/estimate-generator/evaestimate.pdf` — one page, EVA-produced,
  printed to PDF. Structure and arithmetic in the extraction record.
  Notable: EVA prices Specialist hours at the labour rate (22.00 h × £83.28);
  descriptions clip at the column edge; Litres / Anti-C / A/C columns are
  empty; leading `.` marks EVA's standard-charge items.
- `reference/rendererref1/` — `DESIGN_SPEC.md` (Design I, locked July 2026:
  logo top-left, company block top-right, centred red italic letterspaced
  title over a 1.5 pt red rule, red header-row tables, grey label cells,
  doc red `#C80A32`, charcoal `#2C2A27`, label grey `#F2F2F2`, grid grey
  `#BEBEBE`, Arial/Liberation Sans; footer `<REG> · <our ref> | Collision
  Engineers Ltd | <site>` + `Page n of N` on every page), the four sample
  reports, the JSON schema and sample jobs. The schema's `worklists` carry
  names only — the report deliberately prints no part numbers or per-line
  prices; the estimate document is exactly the document that does.
- The live renderer: `src/Pegasus.Core/Reports/AssessmentReportRendering.cs`
  (snapshot, `ReportRepairCosts`, `IAssessmentReportRenderer`,
  `RenderedReportArtifact`, fail-closed `Validate`),
  `src/Pegasus.Infrastructure/Reports/QuestPdfAssessmentReportRenderer.cs`
  (QuestPDF 2026.8.0, embedded Liberation Sans, embedded logo, one render at a
  time, 2-minute budget, page count via PdfPig) and
  `AssessmentReportLayout.cs` (the page frame, `Letterhead`, `Title`,
  `HeaderCell`, `BodyCell`, `DataTable`, `CostTable`, `Footer`, money/hours
  formatting). ADR-0050 fixes QuestPDF as the engine; ADR-0025 keeps the
  renderer behind a Core contract.
- The estimate model: `Estimates.cs` (`EstimateDetails`, `EstimateOperation`
  ↔ line types, `EstimateDiscounts`, `EstimateVatPolicy`, `EstimateTotals`
  — the single owner of estimate money, `ForProjection` for accepted
  versions, `Compute` for editable ones), `RepairSpecifications.cs`
  (`RepairSpecificationVersion`, states Draft / Accepted / Superseded /
  Discarded, `IsCurrent`, `RepairSpecificationPolicy.PolicyVersion = 3`),
  `AssessmentContracts.cs` (`CaseEstimateLineRecord`: Type, Description,
  GuideCode, PartNumber, Quantity, Price, Unpriced, WorkUnits,
  PaintWorkUnits, Materials, provenance).
- The generation pipeline: `CaseReportGeneration.cs` (freeze → render →
  custody → confirm; readiness needs signatory, Current estimate, valuation,
  Close-up and Overview images; artifact kinds `AssessmentReport` /
  `FeeNote`), `AssessmentReportProjection.cs` (`GenerateCaseAssessmentReportDraft`
  — the unretained working preview), `EfCaseReportGenerationStore.cs`
  (snapshot hash reuse, `case_report_draft_previewed` once per Case / kind /
  staff / London day).
- The Web surface: `Pages/Cases/Shared/_CaseEstimate.cshtml` (tabs per
  estimate, Import / Glass's / Send to AI / More / Expand head, Use estimate /
  Duplicate / Discard, the grid, discount and VAT bars, work-lists, rollup),
  `_CaseReport.cshtml` (Generate report, More → Preview draft / Generate fee
  note / Include fee note, artifact download links),
  `Details.cshtml.cs` handlers `PreviewReportDraft` (GET/POST → inline PDF,
  records the preview event), `GenerateReport` / `GenerateFeeNote`,
  `GeneratedArtifact`; `case-workspace.js` opens `[data-report-preview]` in
  the page's document viewer (`window.pegasusCaseViewer.openDocument`) with a
  download action, and falls back to a plain `target=_blank` link without
  script.
- Governing docs: FRD-11 (outcomes, `rendererref1` activation, generation /
  preview / download events, template rule: supplied templates are evidence,
  no invented wording), FRD-11 § Estimate VAT on the rendered report (the
  arithmetic), `docs/design/README.md` (design authority; report templates are
  Infrastructure assets, not Web shell assets; no explanatory copy), the v27
  round's proposals `compare` (print comparison sheet), `supp`, `attach`
  (Report / Fee note / **Breakdown** / Images on delivery) and `resend`
  (composed file names).

## 3. Where it fits

- **It is a Pegasus document, not an EVA facsimile.** FRD-11 and ADR-0050 put
  every rendered document behind the Core render contract in the accepted
  house style. The EVA sheet supplies the *information design* (what a
  repairer or handler expects to read, in what order); the *visual design* is
  Design I. No EVA badge, no EVA reference number, no clipped text.
- **It prints Pegasus's money, nothing re-derived.** Every figure comes from
  `EstimateTotals` (`ForProjection` for Accepted/Superseded — the frozen
  `RecordedTotals`; `Compute` for a Draft) exactly as the Estimate section's
  rollup and the report's cost table already do. The document adds no
  arithmetic beyond presentation sums that the totals owner already
  guarantees reconcile (see § 4.4).
- **It is per estimate, not per Case.** A Case may hold several named
  estimates (EPIC-011 §1.9); a Draft, the Current one and superseded ones can
  each be printed. The state is printed on the page.
- **It is lighter than the report.** Report generation requires the sign-off
  tuple, an applied valuation and prepared images. The estimate document
  needs only: a Case the actor may open, the estimate, its labour rate, and
  the Case header facts. No signature is printed (decision **B**), so no
  D31/DOCS-017 signatory content is involved.
- **Phase 1 is presentation; Phase 2 is retention.** Phase 1 renders on
  demand (view / download) like Preview draft — no generation row, no
  custody object, no Sent claim. Phase 2 (§ 7) retains a confirmed artifact
  through the existing custody path so it can be pinned to a report
  delivery as the `attach` proposal's "Breakdown". Phase 2 is gated on the
  v27 letter for `attach`.

## 4. Document design

### 4.1 Page

A4 portrait, the report's margins (8 mm top, 12 mm sides, 8 mm bottom plus
the 14 mm footer band), Liberation Sans, DATA register 8.8 pt for tables,
ink `#222222`, brand `#C80A32`, rule `#BEBEBE`, zebra `#F5F5F5`, shade
`#F2F2F2`. Multi-page: the line table repeats its header row on each page;
the summary blocks never split across a page (QuestPDF `ShowEntire`).

### 4.2 Layout, top to bottom

```
┌──────────────────────────────────────────────────────────────────────┐
│ [logo]                                       Date:      16/09/2026   │
│                                              Our Ref:   a.QDOS26214  │
│                                              Your Ref:  MFI/ND/46885 │
│                                              Estimate:  Audatex 1 ·  │
│                                                         Current (v3) │
│                                                                      │
│                     E S T I M A T E                                  │  ← red, italic, letterspaced,
│ ─────────────────────────────────────────────────────────────────── │    full-width 1.5 pt red rule
│                                                                      │
│ ┌ Claimant   │ Mr Dan Fuller      ┃ Registration │ YL57 KUF       ┐ │  ← grey-label grid (DataTable
│ │ Vehicle    │ CITROEN C2 CODE    ┃ Repairer VAT │ Registered     │ │    style, two label/value pairs
│ └────────────┴────────────────────┸──────────────┴────────────────┘ │    per row)
│                                                                      │
│ ▌Qty ▌Description                   ▌Type      ▌Hours▌Paint▌Mat £ ▌Unit £▌ │  ← red header row
│  1   Right Front Door Membrane        New         0.10              103.18 │
│  1   Rear Bumper Lining               R & R       0.80                     │  ← zebra body rows,
│  1   Right Rear Side Panel            Repair     10.00                     │    wrapped descriptions,
│  1   .QC & Road Test                  Specialist  1.00                     │    numbers right-aligned
│  1   Right Front Door, Complete       Paint              0.80  191.20      │
│  …                                                                        │
│                                                                      │
│ Hours                                                                │  ← red section heading
│ ┌ New │ Repair │ R & R │ Paint │ Blend │ Specialist │ Check │ Total ┐│    (Section style)
│ │0.20 │ 10.00  │ 2.10  │ 4.70  │  —    │   5.00     │  —    │ 22.00 ││  ← one bordered row of tiles
│ └─────┴────────┴───────┴───────┴───────┴────────────┴───────┴───────┘│
│                                                                      │
│ Rate and discounts                                                   │
│ ┌ Labour rate │ Parts disc │ Materials disc │ Specialist disc │ Overall ┐ │
│ │   £83.28    │   0.00 %   │     0.00 %     │     0.00 %      │  0.00 % │ │
│ └─────────────┴────────────┴────────────────┴─────────────────┴─────────┘ │
│                                                                      │
│ Totals                                                               │
│ ┌ Labour   │ Materials │ Parts   │ Specialist / Other ┐  ┌ Net      £3,034.39 ┐ │
│ │£1,832.16 │  £510.58  │ £160.63 │      £531.02       │  │ VAT (20 %) £606.88 │ │
│ └──────────┴───────────┴─────────┴────────────────────┘  │ Gross    £3,641.27 │ │  ← Gross row shaded,
│                                                          └────────────────────┘ │    red top rule (fee-note
│                                                                      │    TOTAL DUE treatment)
│ ─ footer: YL57 KUF · a.QDOS26214 | Collision Engineers Ltd | www…   Page 1 of 1 ─ │
└──────────────────────────────────────────────────────────────────────┘
```

A Draft prints `DRAFT` as a charcoal badge beside the title (the report's
`Badge` element); Superseded and Discarded print their state word the same
way; Current prints `CURRENT`. The state also appears in the header
"Estimate:" line so a monochrome print still carries it.

### 4.3 Column mapping (EVA → Pegasus)

| EVA column | Pegasus source | Printed |
| --- | --- | --- |
| Qty | `Quantity` (null → 1, matching `EstimateTotals.Compute`) | integer |
| Description | `Description`, else `GuideCode` (the report's own fallback in `RepairSpecificationPolicy.Names`) | wrapped, never clipped |
| Type | `EstimateOperations.FromLineType(Type)` → New / Repair / R & R / Paint / Blend / Specialist / Check | the seven operation words; "Check" is `EstimateOperation.Other` (`check_labour`), the label the live UI already uses (`OperatorLabels.EstimateLineType`) |
| Labour | `WorkUnits` | 0.00 h, blank when null |
| Paint | `PaintWorkUnits` | 0.00 h, blank when null |
| Litres | — no Pegasus fact | **omitted** |
| Mat Price | `Materials` | £, blank when null |
| Anti-C, A/C Price | — no Pegasus fact | **omitted** |
| Price | `Price`; `Unpriced` → "To be confirmed" (the live grid's word, `OperatorLabels…ToBeConfirmed`) | £ unit price, blank when null |
| (not in EVA) | `PartNumber` | **omitted** by default; decision **C** |

Off-pattern values (a unit £ on a Repair line, paint hours on a New line)
are printed where they sit — the totals owner already retains them in
`EstimateTotals.OffPattern`; the document never drops or re-buckets a value
(the v27 `offpattern` proposal's rule, and FRD-11's "retained and reported").

### 4.4 Summary blocks — every figure's owner

| Block | Cell | Source | Note |
| --- | --- | --- | --- |
| Hours | New … Check | Σ `WorkUnits` per operation; Paint and Blend also Σ `PaintWorkUnits` | descriptive, like `ReportRepairCosts.LabourHours` |
| Hours | Total | Σ of the seven | descriptive |
| Rate and discounts | Labour rate | `EstimateDetails.HourlyRate` | the one rate that prices panel and paint hours |
| Rate and discounts | Parts / Materials / Specialist / Overall disc | `EstimateDiscounts` as % | Pegasus's four; EVA's "Labour Disc" has no Pegasus fact and is omitted |
| Totals | Labour | `Printed.PanelLabour + Printed.PaintLabour` | a sum of two printed-pence components; `EstimatePrintedTotals` already defines Net as the sum of all five, so this reconciles by construction. Alternative: print the two separately (decision **D**) |
| Totals | Materials / Parts / Specialist / Other | `Printed.Materials` / `Printed.Parts` / `Printed.Specialist` | already net of discounts |
| Totals | Net / VAT / Gross | `Printed.Net` / `Printed.Vat` / `Printed.Gross` | VAT label from `VatPercent` and the charged categories, the same words the rollup uses ("VAT 20 % on labour, parts, materials, specialist") |

EVA's **£ discount amounts row** (Labour/Materials/Parts/Other Discount,
Total Disc) is **omitted**: the printed components are already net of
discounts and `EstimateTotals` carries no pre-discount category figures; the
live Estimate section prints percentages only for the same reason. Adding £
amounts means extending the one money owner (decision **E**).

### 4.5 Fail-closed rules (Core `Validate`)

Refuse to render, naming the reason, when:

- the estimate has no lines;
- `HourlyRate <= 0` (the document prints the rate and prices hours by it —
  the same `LabourRateRequirement` the report applies);
- any line has neither Description nor GuideCode;
- a line's Type is not one of `EstimateLineCodes.Types`;
- printed components do not reconcile (`Parts + PanelLabour + PaintLabour +
  Materials + Specialist ≠ Net` or `Net + Vat ≠ Gross`) — the
  `ReportRepairCosts.Validate` rule, reused;
- the Case header lacks a reference or registration.

Not a refusal: `RepairerVatStatus.Unknown` on a Draft. The totals owner then
charges VAT on nothing and the VAT row prints `VAT (20 %) on nothing — £0.00`
(the rollup's existing wording via `CaseWorkspaceLabels.EstimateVat.NoCategories`).
The Draft badge already says this is not a Current estimate; `Use estimate`
is what refuses an Unknown status, not the printout.

## 5. Backend feature

### 5.1 Core (`src/Pegasus.Core/Reports/EstimateDocumentRendering.cs`)

```csharp
public static class EstimateDocumentContract
{
    public const string TemplateVersion = "estimate-v1";   // bump on layout change, like AssessmentReportContract
}

public sealed record EstimateDocumentLine(
    int Position, int Quantity, string Description, EstimateOperation Operation,
    decimal? Hours, decimal? PaintHours, decimal? Materials, decimal? UnitPrice, bool Unpriced);

public sealed record EstimateDocumentHours(
    decimal New, decimal Repair, decimal RemoveAndRefit, decimal Paint, decimal Blend,
    decimal Specialist, decimal Check)
{ public decimal Total => …; }

public sealed record EstimateDocumentSnapshot(
    string OurReference, string? YourReference, DateOnly DocumentDate,
    string ClaimantName, string VehicleDescription, string Registration,
    Guid EstimateId, int EstimateVersion, string EstimateName,
    RepairSpecificationState State, bool IsCurrent,
    RepairSpecificationSourceRoute Route,
    IReadOnlyList<EstimateDocumentLine> Lines,
    EstimateDocumentHours Hours,
    decimal HourlyRate, EstimateDiscounts Discounts,
    EstimateTotals Totals,                 // the money, unchanged
    string PayloadVersion = EstimateDocumentContract.TemplateVersion)
{
    public static EstimateDocumentSnapshot For(RepairSpecificationVersion estimate, header facts…);
    public void Validate();                // § 4.5
}

public interface IEstimateDocumentRenderer
{
    string EngineVersion { get; }
    Task<RenderedReportArtifact> RenderAsync(EstimateDocumentSnapshot snapshot, CancellationToken ct);
}

public sealed class RenderCaseEstimateDocument(
    IGetAssessmentAccess access, IGetAssessmentWorkspace workspace,
    IRepairSpecificationStore estimates, IEstimateDocumentRenderer renderer, TimeProvider clock)
{
    Task<RenderCaseEstimateDocumentResult> ExecuteAsync(Guid caseId, Guid estimateId, ActionActor actor, CancellationToken ct);
    // NotFound (case not openable / estimate not on this case) | NotRenderable(reasons) | Rendered(artifact)
}
```

Design points:

- `EstimateDocumentSnapshot.For` is the **one mapping** from
  `RepairSpecificationVersion` to the document, next to
  `ReportRepairCosts.For`. It calls `EstimateTotals.ForProjection` — never
  `Compute` directly — so an accepted version prints its frozen
  `RecordedTotals` and a Draft prints live figures.
- `RenderedReportArtifact` is reused as the return type (bytes, page count,
  SHA-256, template + engine versions). The `GenerateAssessmentReportDraft`
  hash re-verification pattern is reused verbatim.
- Header facts come from `GetAssessmentWorkspace` (`Header.Reference`,
  `Header.Registration`, `Data.Claimant.Name`, `Data.Claim.Number`,
  `Data.Vehicle.Make/Model`) — the same reads the report projection source
  uses, so the two documents never disagree about the claimant or vehicle.
  Vehicle description = `Make + " " + Model` current values; a Case without
  a confirmed make/model prints the source-only combined description if the
  workspace exposes it, else "—".
- Access: `IGetAssessmentAccess.CanOpen` (the gate Preview draft uses). No
  edit lease, no expected version: this is a read.
- Document date: `LondonCalendar.DateAt(now)` — the day it was printed. No
  override (a Phase 2 retained artifact would freeze it).
- Presentation event: follow DOCS-014. Add
  `CaseReportPresentationEvents.EstimateDocumentPreviewed =
  "case_estimate_document_previewed"` and a store method
  `RecordEstimateDocumentPreviewedAsync(actor, caseId, estimateId, now)`,
  idempotent per Case, estimate, staff member and London day, implemented
  the same way as `RecordDraftPreviewedAsync` (one ActionHistory row). Not a
  Case mutation; no version bump.

### 5.2 Infrastructure (`src/Pegasus.Infrastructure/Reports/`)

- `QuestPdfEstimateDocumentRenderer : IEstimateDocumentRenderer` — same
  shape as the report renderer: font registration, embedded logo, one
  render at a time (share the process gate — extract it into a small
  `RenderGate` both renderers take from DI so a report render and an
  estimate render never overlap), 2-minute budget via
  `AssessmentReportRenderPolicy.RenderTimeout`, page count via PdfPig,
  `EngineVersion = "QuestPDF/<version>"`.
- `EstimateDocumentLayout` — pure composition from the snapshot. It must
  share the chrome with `AssessmentReportLayout` rather than copy it:
  extract `Letterhead`, `Reference`, `Title`, `Badge`, `Section`,
  `HeaderCell`, `BodyCell`, `DataTable`, `Footer`, the colour and register
  constants and the `Money` / `Hours` / `Date` / `Slug` formatters into an
  `internal static class ReportChrome` (one owner of Design I). The
  assessment layout keeps only what is report-specific. This refactor is
  behaviour-preserving and is verified by the existing renderer tests.
- New pieces the estimate needs and the report does not: a `TileRow`
  (the bordered label-over-value row for Hours / Rate / Totals — the report's
  `Tile` is close but is a highlight card, not a bordered grid cell) and the
  seven-column line table with a repeating header.
- DI: register the renderer and the use case in
  `DependencyInjection.cs` next to the report renderer. Architecture tests
  (`DependencyDirectionTests`) already assert QuestPDF is referenced only
  from Infrastructure; the new files land there.
- File name (Web decides, Core suggests): `{Slug(reference)}_{Slug(estimate
  name)}_estimate.pdf`, e.g. `A_QDOS26214_AUDATEX_1_estimate.pdf`, following
  `AssessmentReportLayout.Slug`. The v27 `resend` proposal's human file names
  (`QDOS26214 MA59BDY Total loss report.pdf`) are not yet lettered; adopt
  them for both documents together when they are.

### 5.3 Persistence

None in Phase 1. No migration, no new table: the render is unretained and
the presentation event is an ActionHistory row.

## 6. Frontend interaction

### 6.1 Placement — the Estimate section, per estimate

The action belongs to the estimate being looked at, so it sits in the
Estimate section's `est-actions` row (beside Use estimate / Duplicate /
Discard) for the **selected tab**, in both read and edit mode, for every
listed estimate (Draft, Current, Superseded, Discarded — the page prints its
state). It is a read, so it is offered without the edit session and without
the lease, exactly as Preview draft is offered on the Report section.

```
┌ Estimate ─────────────────────────── [Import] [Glass's] [Send to AI] [More ▾] [⤢] [Edit] ┐
│ [Audatex 1 · AX · Current] [Manual copy · Manual · Draft] [+ New estimate]                 │
│                                                                                            │
│ [✓ Use estimate] [⧉ Duplicate] [👁 Estimate PDF] [🗑 Discard]                              │  ← new: Estimate PDF
│ …grid, bars, work-lists, rollup…                                                            │
└────────────────────────────────────────────────────────────────────────────────────────────┘
```

Read mode today shows no `est-actions` row (it renders only while editing
and not on the "new" tab). Phase 1 therefore adds a read-mode actions row
that carries only Estimate PDF, or places the button on the tab strip's
right edge; the mockup round decides (decision **F**). Label: **Estimate PDF**
(an `eye` icon with the viewer; the same `download` icon when it downloads).
Labels and button text need no copy approval; no sentence is added.

### 6.2 Behaviour

- **With script:** the click opens the PDF in the page's document viewer
  (`window.pegasusCaseViewer.openDocument`) with `name` = the file name and a
  **Download** action — the exact `[data-report-preview]` binding in
  `case-workspace.js`, generalised to `[data-document-preview]` so both the
  report and the estimate use one binder. The More menu closes if the
  trigger lives in one.
- **Without script:** the anchor is a plain `GET` to
  `/Cases/Details?handler=EstimateDocument&id=…&estimateId=…` with
  `target=_blank` and `data-no-inplace`, returning `application/pdf` inline;
  the browser's own viewer offers download. This is how Preview draft works
  today.
- **Refusal:** when Core returns NotRenderable, the handler redirects back
  to the estimate tab with the reasons in `TempData["CaseError"]` joined as
  `Requirement: WhyOutstanding` — the report's existing pattern. A Draft
  with no lines simply does not offer the button (the reasons are known
  from the loaded estimate without a render).
- **No state change:** the Case version, lease and edit session are
  untouched; the presentation event is recorded once per day.
- **Full-screen estimate (`data-estimate-expand`)**: the button stays in
  the actions row and works the same.

### 6.3 Page-model handler (`Details.cshtml.cs`)

```csharp
public async Task<IActionResult> OnGetEstimateDocumentAsync(Guid id, Guid estimateId, CancellationToken ct)
{
    if (!TryGetActor(out var actor)) return Forbid();
    var result = await renderEstimateDocument.ExecuteAsync(id, estimateId, actor, ct);
    return result.Outcome switch
    {
        NotFound      => NotFound(),
        NotRenderable => RedirectToEstimate(id, estimateId, reasons…),
        _             => File(result.Artifact!.Pdf, "application/pdf")   // inline; the viewer names the download
    };
}
```

`OnGetEstimateDocumentDownloadAsync` is unnecessary: the viewer's Download
uses the same URL with the `download` attribute, and the no-script path
relies on the browser.

### 6.4 Design process

This is a routed Razor change to the Case record, so it follows the existing
Razor skills: a Stage 1 mockup item on the current v27 round (a new lettered
item under § 15 — "Estimate PDF on the actions row", one shot in read and one
in edit, plus the PDF page itself as an image beside the mockup), operator
sign-off, then Stage 2 conversion with the FRD-11 / FRD-12 sentences in the
same delivery. The PDF's own visual acceptance is a rendered sample from the
integration test fixture (§ 9), compared side by side with
`evaestimate-page1.png`.

## 7. Phase 2 — retained artifact and delivery attachment (outline, gated)

Only once the v27 `attach` proposal ("Report, Fee note, Breakdown, Images" on
delivery) has its letter:

- Add `CaseReportArtifactKind.EstimateDocument`? **No** — a report
  generation's snapshot hash covers signatory, images and valuation, none of
  which the estimate depends on, and readiness would wrongly gate it.
  Instead a small sibling family: `CaseEstimateDocumentGeneration`
  (estimate id + version + snapshot JSON + template/engine version + artifact
  row), frozen in one short transaction, rendered and retained through
  `ICaseArtifactCustody.RetainAsync` with occurrence identity
  `estimate-document:{estimateId:D}:{version}`, confirmed in a second
  transaction — the `GenerateCaseReport` sequence, not a new one.
- Accepted/Superseded versions are immutable, so one confirmed artifact per
  (estimate, version) is enough and a repeat request returns
  `AlreadyConfirmed`. A Draft is never retained (retain the estimate first
  by making it Current or accepting it).
- Delivery: `PrepareCaseReportDelivery` pins confirmed artifacts by exact
  document/version/hash/length; extend the preparation's `Artifacts` with
  the Current estimate's confirmed document when the operator ticks
  Breakdown. Requires `IReportSendReadiness` to re-check that artifact too.
- Stale rule: none needed — the artifact is bound to an immutable estimate
  version, not to the Case's moving facts.
- Documentation: an FRD-11 sentence under *Initial renderer activation*
  naming the estimate document as a separately addressable retained artifact
  and what delivery may attach; `docs/current-architecture.md` gains the
  family; capabilities gains an id.

## 8. Documentation impact (Phase 1)

- **FRD-11**: one sentence under *Report generation entry point* — the
  Estimate section renders an unretained estimate document of any estimate
  version from `EstimateTotals`, in the accepted house style, viewing
  recorded as `case_estimate_document_previewed`; it is neither a report nor
  an approval. One row in the *Estimate VAT on the rendered report* table is
  not needed (the rules are unchanged).
- **FRD-12 / `docs/design/README.md`**: the Estimate section's actions row
  gains Estimate PDF (placement sentence); the document itself is an
  Infrastructure asset (already stated).
- **`docs/capabilities.md`**: RPT-02 already claims "the fee note plus
  itemised repair-specification breakdown"; either point RPT-02's wording at
  this document or add `RPT-07 — Estimate document rendered per estimate
  version from the one totals owner`. Recommend the latter (decision **G**).
- **`docs/current-architecture.md`**: `Pegasus.Infrastructure/Reports` gains
  `ReportChrome`, `EstimateDocumentLayout`, `QuestPdfEstimateDocumentRenderer`;
  Core gains `EstimateDocumentRendering.cs`.
- **`CONTEXT.md`**: the interface term for the document (decision **A**).
- Markdown placement gate: these are edits to existing canonical files; no
  new doc file.

## 9. Verification plan

Application code changes, so build/test evidence is required (CLAUDE.md
§ Verification). One host-slot owner; CI on the PR for the full suite.

- **Core tests** (`tests/Pegasus.Core.Tests/Reports/EstimateDocumentRenderingTests.cs`):
  `For` maps every line column; hours by operation and the total; Labour =
  PanelLabour + PaintLabour and the five components reconcile to Net; an
  accepted version uses `RecordedTotals`, a Draft uses `Compute`; each § 4.5
  refusal by name; `Unpriced` → "To be confirmed"; Quantity null → 1;
  off-pattern values retained.
- **Golden fixture**: a Draft built from the 22 EVA lines at £83.28 with
  repairer VAT Registered. Assert the document prints Materials £510.58,
  Parts £160.63, Specialist £531.02 and VAT/Gross exactly as `EstimateTotals`
  computes them — and record in the test that Labour will read
  **£1,415.76 (17.00 h)**, not EVA's £1,832.16, because Pegasus does not price
  Specialist hours (§ 10). The fixture is evidence of the mapping, not of
  parity with EVA's arithmetic.
- **Renderer integration tests** (`tests/Pegasus.IntegrationTests/Reports/EstimateDocumentRendererTests.cs`,
  same shape as `AssessmentReportRendererTests`): renders one page for the
  fixture; PdfPig text contains the title, the five header facts, the
  `DRAFT`/`CURRENT` badge, every description, the hours row and the totals;
  a 120-line estimate paginates with the header row repeated and the summary
  blocks unsplit; SHA-256 and page count match; engine version published
  without rendering; invalid snapshot fails before rendering.
- **Existing renderer tests** stay green after the `ReportChrome` extraction
  (the behaviour-preservation check).
- **Web tests** (`CaseEstimateHeaderWebTests` or a new
  `CaseEstimateDocumentWebTests` class, `Category=SqlServer`): the button
  renders for each listed estimate in read and edit mode, absent for an
  empty Draft; `GET` returns `application/pdf`; the preview event is
  recorded once for two views on one day; a foreign estimate id → 404; a
  Case the actor cannot open → 404.
- **Visual**: run Pegasus.Web `DevelopmentOffline` against LocalDB
  (memory: local Web visual QA), open a Case with an Audatex import, view
  the PDF in the viewer and download it; capture the page beside
  `evaestimate-page1.png` and page 1 of the sample repairable report; check
  1580 px and a narrower desktop width for the actions row.
- **Docs**: link check and the base..head `scripts/Test-MarkdownPlacement.ps1`
  gate.

## 10. Observations to raise (not in scope to fix here)

1. **Specialist hours are unpriced in Pegasus but priced by EVA.**
   `EstimateTotals.Compute` adds Specialist lines' `Price` to the specialist
   category and ignores their `WorkUnits`; EVA charges them at the labour
   rate (its Total Hours 22.00 × £83.28). `ReportRepairCosts.For` meanwhile
   sums `WorkUnits` across all lines for the report's *Labour Hours* figure,
   so the report already prints 22.00 h beside £1,415.76 of labour. The
   estimate document will make this visible on every page that has
   Specialist hours. This is a calculation-policy question for the operator
   (FRD-11 § Estimate VAT table: "Labour — Panel and paint hours × rate"),
   not a rendering one; if the policy changes, `PolicyVersion` bumps and the
   document follows automatically.
2. **`specialist_wu` ("Specialist, by work units")** is treated identically
   to `specialist_fixed` by the totals owner; the name promises hours-based
   pricing it does not do. Same question as (1).
3. **EVA's leading `.`** on standard charges is imported verbatim by the
   parsers and will print verbatim. Fine for parity; the operator may prefer
   it stripped at import (a parser change, separate).
4. The report's `CostRows` prints *Labour Hours* and *Paint Hours* from all
   lines; once the estimate document exists the Report's cost table and this
   document must keep reading the same `ReportRepairCosts`/`EstimateTotals`
   so the two never disagree — a test in § 9 pins that.

## 11. Decisions for the operator

- **A. Name.** "Estimate PDF" as the button and "Estimate document" in
  FRD/code; or "Breakdown" throughout (the v27 `attach` word). Recommend
  Estimate.
- **B. Signature.** Print nothing (recommended — the estimate is a working
  document, and the sign-off tuple is DOCS-017's) or print the Sign-off
  Engineer's name and qualifications without the signature image.
- **C. Part number column.** Omit (EVA parity, recommended) or include after
  Description when any line carries one.
- **D. Labour money.** One `Labour` box (EVA parity, recommended) or two
  (`Panel labour`, `Paint labour`) matching the report's cost table.
- **E. £ discount amounts.** Omit (percentages only, as the live rollup;
  recommended) or extend `EstimateTotals` with pre-discount category figures
  so the four £ discounts and Total discount can print.
- **F. Button placement in read mode.** A read-mode actions row (Estimate PDF
  alone) or the right edge of the tab strip — settled by the mockup shot.
- **G. Capability id.** New `RPT-07` (recommended) or fold into RPT-02.
- **H. Phase 2.** Confirm it waits for the `attach` letter, or bring
  retention forward so the Current estimate's document can be sent through
  the approved mailbox now.
- **I. Header "Your Ref".** Print the claim number as the report does
  (recommended) or match EVA's five facts exactly.
