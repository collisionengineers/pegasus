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
  Notable: EVA sums its Specialist hours into Labour (22.00 h × £83.28) —
  see § 10 for what that means in Pegasus;
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
| Hours | New … Check | `EstimateHours.Of(estimate)` (§ 10, fix 3): priced hours per operation — `WorkUnits` on Replace / Repair / R & R / Check / `specialist_wu`, `PaintWorkUnits` on Paint / Blend | one classification shared with `EstimateTotals.Compute` |
| Hours | Total | `EstimateHours.PricedTotal` | **Total × rate = Labour £** on every page, as on EVA's |
| Hours | Specialist (not priced) | `EstimateHours.UnpricedSpecialist` — `WorkUnits` on `specialist_fixed` lines | printed only when non-zero, outside the Total, so a fixed-price sublet's informational hours never look like labour |
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
  an approval. In the *Estimate VAT on the rendered report* table the Labour
  row becomes "Panel, paint and Specialist work-unit hours × the selected
  labour-rate-card rate; hours on a fixed-price Specialist line are
  retained, shown and not priced" (§ 10).
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
- **Golden fixture, two typings of the same 22 EVA lines** at £83.28,
  repairer VAT Registered:
  - *typed as Pegasus models them* — tyre and alignment `specialist_fixed`,
    the five 1.00 h operations `check_labour`: the document prints Labour
    £1,832.16 (22.00 h), Materials £510.58, Parts £160.63, Specialist
    £531.02, Net £3,034.39, VAT £606.88, Gross £3,641.27 — **EVA's figures
    exactly**, and no anomaly;
  - *typed as EVA labels them* — all eleven as `specialist_fixed`: Labour
    £1,415.76, priced Total 17.00 h, `Specialist (not priced)` 5.00 h, five
    `hours` anomalies (§ 10, fix 2), Net £2,618.23. The test pins that the
    shortfall is visible on the page, never silent.
- **Calculation-fix tests** (`EstimateTests.cs`, `RepairSpecificationPolicyTests.cs`,
  `AssessmentReportRenderingTests.cs`): `specialist_wu` hours price at the
  rate into panel labour and count in the Specialist hours column;
  `specialist_fixed` hours raise the anomaly and stay out of every money
  figure; `EachOperationLandsInExactlyOneCostBucket` gains the
  `specialist_wu` row and its Net; `ReportRepairCosts.For` reads
  `EstimateHours` so the report's Labour Hours × rate equals its printed
  labour; an accepted v3 estimate still projects its frozen
  `RecordedTotals` untouched (`ForProjection`), and its hours come from
  `EstimateHours.Of` over the same lines.
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

## 10. Calculation fixes carried with this work

Operator direction, 16 September: fix the three specialist-hours defects
rather than record them. They are one small Core change to the one money
owner, delivered in the same PR as the document so the first estimate
document ever printed is already right. Each is behaviour the EVA sample
exposed; none changes an accepted estimate.

**The rule, settled.** EVA's figure (22.00 h × £83.28 = £1,832.16) is the
commercially right total for that job and the B04 rule is also right — EVA's
"Specialist" type conflates a fixed-price sublet (tyre £180.00, wheel
alignment £112.42: a £ amount, hours informational) with hours-based
operations (QC & road test, standard shutdown, the two diagnostic checks,
wash/clean: 1.00 h each, charged at the rate by every repairer). Pegasus
already separates the two — `specialist_fixed` and `check_labour` — and,
typed that way, reaches the same £1,832.16 / £3,034.39. So the totals rule
stays: **a fixed-price Specialist line's £ is the whole of its money.** The
defects are that hours can vanish from the money silently, and that hours
are counted where they are not priced.

### Fix 1 — `specialist_wu` prices its work units

`Estimates.cs`, `EstimateTotals.Compute`: a `specialist_wu` line's
`WorkUnits` join **panel hours** and are priced at the estimate's one rate
(FRD-11 Labour row); its `Price`, if any, is off-pattern like a unit £ on a
Repair line (existing `OffPatternAmount`). `specialist_fixed` is unchanged.
Rationale: the type's name and label ("Specialist, by work units") promise
hours-based pricing; it sits in `EstimateLineCodes.Types` behind the SQL
check constraint `CK_CaseEstimateLines_LineType` and is accepted by the JSON
route and the AI toolset, so removing it would be a migration for no gain,
while pricing it costs one `case` arm. Landing the money in Labour (not the
Specialist category) keeps *Labour £ = priced hours × rate* true on the
document, the report and the Settlement strip, which is also how EVA sums
its Specialist column into Labour.

`RepairSpecificationPolicy.PolicyVersion` → **4** with its one-line history
note. Only editable versions are recomputed; Accepted and Superseded
versions keep their frozen `RecordedTotals` (v3) through `ForProjection`,
so no migration, no re-statement of an accepted figure.

### Fix 2 — hours on a fixed-price Specialist line are an anomaly

`EstimateTotals.Compute`: a `specialist_fixed` line with non-zero
`WorkUnits` adds an `EstimateAnomaly(Position, "hours", value, "Hours on a
fixed-price Specialist line are retained but not priced.")`, the same
mechanism as stray paint hours today. Nothing is dropped, nothing is
re-bucketed: the value stays on the line and in `OffPattern`, and now has a
name. Surfacing:

- the estimate document prints those hours in the line's Labour column and
  in the `Specialist (not priced)` cell of the Hours row (§ 4.4), outside the
  priced Total;
- the grid's amber cell with a tooltip is the v27 `offpattern` proposal
  (shot 79) and lands with its letter; until then the anomaly is visible on
  the document and in `EstimateTotals.OffPattern`.

Where such lines come from: the editor's **Specialist** operation maps to
`specialist_fixed`, so an operator who types hours on it hits this; the JSON
and AI routes can send either type. Whether the editor should offer a
second Specialist operation that lands as `specialist_wu` is decision **J**
— the fix does not need it.

### Fix 3 — hours printed anywhere are priced hours

New `EstimateHours` in `Estimates.cs`:

```csharp
public sealed record EstimateHours(
    decimal Replace, decimal Repair, decimal RemoveAndRefit, decimal Check,
    decimal SpecialistPriced,        // specialist_wu WorkUnits
    decimal Paint, decimal Blend,    // PaintWorkUnits on Paint / Blend lines
    decimal UnpricedSpecialist)      // specialist_fixed WorkUnits
{
    public decimal PricedPanel => Replace + Repair + RemoveAndRefit + Check + SpecialistPriced;
    public decimal PricedPaint => Paint + Blend;
    public decimal PricedTotal => PricedPanel + PricedPaint;
    public static EstimateHours Of(RepairSpecificationVersion estimate);   // pure, over Lines
}
```

`EstimateTotals.Compute` takes its `panelHours` / `paintHours` from
`EstimateHours.Of` so there is exactly one classification of a line's hours.
`EstimateHours` is **not** added to the persisted `EstimateRawTotals` /
`RecordedTotals` shape: it is a pure function over lines, so an accepted v3
estimate reports its hours the same way as a v4 one without touching its
frozen money. Consumers switch to it:

- `ReportRepairCosts.For` → `LabourHours = hours.PricedPanel`, `PaintHours =
  hours.PricedPaint` (the report's cost table and the repairable
  outcome's *Labour hours* tile then satisfy hours × rate = printed labour);
- `Details.Report.cs` `LabourHours` (the Settlement figures strip) — the
  same two;
- the estimate document's Hours row (§ 4.4);
- a Core test (not a `Validate` rule — accepted versions are never
  re-checked) pins the invariant on the raw figures, where it is exact:
  `Raw.PanelLabour + Raw.PaintLabour == PricedTotal × HourlyRate × (1 −
  Overall discount)`. The printed labour figures are rounded independently,
  so the page-level check is the one the fixture already makes: printed
  Labour equals the two printed components, and hours × rate reproduces
  EVA's £1,832.16 on the correctly typed fixture.

### Remaining observations (unchanged, not in scope)

1. **EVA's leading `.`** on standard charges is imported verbatim and will
   print verbatim. Fine for parity; stripping it is a parser change.
2. Once the document exists, the Report's cost table and the document must
   keep reading the same `ReportRepairCosts` / `EstimateTotals` /
   `EstimateHours` so they never disagree — the § 9 tests pin that.

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
- **J. Reaching `specialist_wu` from the editor.** Leave it to the JSON and
  AI routes (recommended for now — the three fixes need no editor change),
  or add a second Specialist operation ("Specialist, by hours") to the
  editor's operation list so an operator can type an hours-based specialist
  line without calling it Check.
