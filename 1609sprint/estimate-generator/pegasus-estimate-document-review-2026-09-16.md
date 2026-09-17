# Pegasus estimate document: review and plan amendments

**Review date:** 16 September 2026  
**Subject:** attached `PLAN(1).md`, `evaestimate.pdf`, `evaestimate-extracted.md`, and the supplied repairable-report house-style image.  
**Status:** review addendum, not implementation or operator approval. No repository changes or application build/test execution were performed.

## Verdict

Keep the proposed feature and its general architecture: an estimate-specific document, the existing Collision Engineers house style, QuestPDF behind a Core contract, and one authoritative estimate calculation. Revise the plan before implementation. Its largest gaps concern monetary disclosure, access, saved-versus-unsaved data, historical identity and preview/download consistency, rather than the choice of PDF engine.

## Evidence baseline and limitations

GitHub `main` was inspected at `8b9d358f71ac02363a3e9fa50549d4fef0fcdac8`. GitHub `dev` was inspected at `9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396`; the comparison showed two additional commits, principally planning/design/documentation changes. The estimate calculation, report renderer and generation files discussed here are unchanged between those revisions.

The Pegasus MCP source checkout was clean but at `32f8679d3695e0dcab8f310a1c20f8b129d20190`, substantially behind GitHub main. Its source-freshness response explicitly reported the difference. An actual source read confirmed an older estimate model. Consequently, the code findings below use the pinned GitHub revision, not the MCP checkout as an alleged current implementation.

The EVA PDF was rendered and visually inspected. Its extracted figures were checked by independent decimal arithmetic. Arithmetic below reproduces the observed calculation policy for the supplied mapped rows; it is not evidence of executing Pegasus's .NET tests or generating a new Pegasus PDF.

## Priority summary

| ID | Priority | Required amendment |
|---|---|---|
| E01 | Resolve before operational release | Decide specialist-hours charging; the supplied example differs by £499.68 gross under current Pegasus policy. |
| E02 | Before implementation | Include estimate-level additional materials and other costs; define quantity, price, hours and rounding semantics. |
| E03 | Before implementation | Separate Case-read authorization from the report/assessment lifecycle gate. |
| E04 | Before implementation | Define saved-state preview behaviour and ensure Download retrieves the document actually previewed. |
| E05 | Before implementation | Distinguish workflow state from incomplete pricing and unresolved VAT. |
| E06 | Before historical output is trusted | Preserve accepted calculations across all supported historical states, including the permitted Superseded-to-Discarded transition. |
| E07 | Correct Phase 2 design now | Replace estimate/version-only artifact reuse with complete frozen-document identity; attach the estimate pinned by the report. |
| E08 | During layout design | Include optional part numbers, complete status handling and repeated estimate identity; preserve the house style. |
| E09 | During backend design | Add a bounded consistent projection, distinct schema/template versions and explicit error contracts. |
| E10 | During renderer implementation | Bound queue waits and render inputs; use page-flow constraints selectively. |
| E11 | Before merge | Expand monetary, state, browser and visual regression tests. |
| E12 | Before declaring capability complete | Document preview-only scope and keep outward delivery distinct from preview/download. |

## E01 — Specialist hours must become an explicit release decision

**Plan:** §§9–10 already recognize that Pegasus excludes specialist hours from labour pricing, but classify this as outside the PDF feature's scope. The layout sketch nevertheless prints EVA's monetary figures. [U1–U3]

**Verified code:** `EstimateTotals.Compute` maps both `specialist_fixed` and `specialist_wu` to Specialist. It adds the quantity-adjusted Price to that category and explicitly does not multiply Specialist WorkUnits by the labour rate. This is current policy, not something the renderer should repair locally. [S1]

For the 22 supplied rows, with no added header costs or discounts, repairer VAT Registered, and VAT percentage 20:

| Figure | EVA reference | Current Pegasus policy |
|---|---:|---:|
| Recorded non-paint WorkUnits | 17.30 h | 17.30 h |
| Recorded PaintWorkUnits | 4.70 h | 4.70 h |
| Recorded hours combined | 22.00 h | 22.00 h |
| Hours priced at the labour rate | 22.00 h | 17.00 h |
| Labour | £1,832.16 | £1,415.76 |
| Materials | £510.58 | £510.58 |
| Parts | £160.63 | £160.63 |
| Specialist / Other fixed amounts | £531.02 | £531.02 |
| Net | £3,034.39 | £2,617.99 |
| VAT | £606.88 | £523.60 |
| Gross | £3,641.27 | £3,141.59 |

The gross difference is **£499.68**. This sample establishes EVA's treatment for these rows; it does not establish every possible EVA pricing rule. [U2, U3, S1]

**Correction to the plan:** `ReportRepairCosts.For` sums WorkUnits and PaintWorkUnits separately. For this fixture its LabourHours would be **17.30**, not 22.00; PaintHours would be **4.70**. Priced panel hours are 12.30, priced paint hours 4.70. Their separately rounded monetary components are £1,024.34 and £391.42. [S2]

**Amendment:** decide whether specialist work-unit entries should be charged and how they interact with fixed charges, discounts and VAT categories. Make any change in the one calculation owner, with its policy version and tests. Do not silently reprice accepted historical estimates. A policy-version bump affects a draft's computation; `ForProjection` intentionally keeps an accepted estimate's recorded calculation.

The visual fixture must use the chosen policy consistently. A mockup showing EVA totals and a test fixture expecting different Pegasus totals must be identified as different examples, or replaced by one coherent approved example.

## E02 — Print all inputs that contribute to the money

**Verified omission:** the calculation starts Materials with `EstimateDetails.PaintMaterials` and Specialist with `EstimateDetails.OtherCosts`. These are not necessarily line-table values. The proposed document snapshot does not carry those two inputs separately. A document following the proposed mapping could therefore have valid totals but no visible explanation of part of its charges. [U1, S1]

Add a small, conditional adjustment section containing **Additional materials** and **Additional costs** when recorded. Keep them distinguishable from imported source rows; do not manufacture source provenance or silently relabel them as parts or labour. Printed net categories still come from `EstimateTotals.Printed`.

Define the following semantics explicitly:

| Field | Current calculation meaning |
|---|---|
| Quantity | Positive supplied quantity; otherwise the calculator falls back to 1. Input validation and historical-data handling must agree with the displayed value. |
| Price | A unit amount; the calculator multiplies it by effective quantity. |
| WorkUnits | Row hours, not automatically hours per item multiplied by quantity. |
| PaintWorkUnits | Row paint hours; charged only for Paint/Blend operations under current policy. |
| Materials | Row materials amount, added once rather than multiplied by quantity. |
| Category totals | Already reduced by applicable category discounts and the overall discount. |
| Printed VAT | Rounded from the selected discounted raw categories, not recomputed from displayed rounded Net. |

Do not add a renderer-owned line-total calculator. A charged line breakdown or monetary discount explanation, if needed, should be supplied by the canonical calculation owner. Displaying quantity, unit price and rate does not justify implementing a second arithmetic policy.

Off-pattern amounts require the same care: a unit amount on a labour line is retained in Specialist treatment by the calculator, while stray paint hours on a non-Paint/Blend line are retained but not charged. Printing the values in their original columns is correct, but the document must not imply that every printed number contributes to the apparent column total in the same way. [S1]

**Precision:** current hour inputs support six decimals. Decide whether line display uses two to six decimals, or explicitly treats its hours as rounded display values. Do not silently round six-decimal source hours before costing, and do not test sums of rounded display hours as if they were the original precise values. [S3]

## E03 — The proposed access gate is not general Case viewing

`AssessmentAccessPolicy.CanOpen` allows only `ReportPreparation`, `PostReport` and `PostReportComplete`. It is a lifecycle gate, not a generic answer to whether an authenticated staff member may view a Case. FRD-11 separately says Engineer sections remain viewable in other states. [S4, S11]

The plan promises an estimate document for a Case the actor may open, including working and historical estimates. Reusing this lifecycle check without a decision narrows that promise.

Use the established staff/Case-read authorization and verify the selected estimate belongs to the requested Case. Define document availability by lifecycle state explicitly. A print/read action should not acquire an edit lease, make an estimate Current, or require report signatory, valuation or photographs.

Test all intended readable Case states, not just a single successful report-preparation Case. Keep foreign Case/estimate combinations non-disclosing, as the plan already intends.

## E04 — Saved-state preview and exact-byte download

The plan offers Estimate PDF in edit mode but does not specify what happens to unsaved edits. Its GET reads persisted server data; the visible grid may contain changes not represented by that data. [U1]

**Recommended contract:** the PDF is generated from the saved selected estimate. An unsaved editor either requires the existing Save first or makes the saved-copy meaning explicit. Printing must not silently save, discard, accept, or switch the Current estimate. Do not create a separate approval flow.

The plan also proposes previewing and downloading by requesting the same dynamic render URL. Two requests are two opportunities to read and render different state. A draft may change, its Case header may be corrected, or the London calendar date may change between the requests.

With JavaScript, prefer one authorized render response whose bytes are used both for the viewer and for Download. A browser Blob is one implementation option, subject to the existing viewer's integration and content-security constraints. Revoke temporary object URLs when the viewer closes. This is not durable document retention.

Without JavaScript, open the PDF response in the browser and let the browser download that opened document. State precisely what the no-script flow guarantees. Do not require permanent storage merely to support a preview.

Define response behaviour as part of the contract: PDF media type, a safe suggested filename supplied by the server, private/no-store caching policy, authentication, typed failure outcomes, and no redirects that accidentally load an entire Case page inside the PDF viewer. An enhanced client should display a useful render failure while leaving unsaved edits intact. The existing plain-navigation fallback may keep its normal redirect pattern.

Preview/download equality means equality of the bytes the user actually saw. It should not depend on asserting that independent PDF render executions always have identical metadata or byte streams.

## E05 — Workflow state and calculation completeness are separate

A DRAFT badge identifies workflow state; it does not explain whether the total excludes unpriced work or whether VAT treatment remains undecided. The current policy defaults Unknown repairer VAT status to no charged categories unless an override is supplied. `BlocksAcceptance` is true only when status is Unknown **and categories have not been overridden**. [S1, S12]

Amend §4.5 so it does not state that every Unknown VAT status blocks acceptance. A manual category override is an existing valid path.

For working PDFs, display concise facts such as **VAT treatment pending** when `BlocksAcceptance` is true and **Unpriced items: n** when applicable. These are proposed labels requiring the normal design decision, not new financial rules or automatic prose. Keep known amounts from the canonical owner; do not invent a completed total for missing prices.

Distinguish null, zero and explicitly Unpriced. An unpriced marker and a stored non-null Price must be treated according to canonical input validation; the renderer must not independently choose one meaning and conceal another.

Accepted estimates without a valid RecordedTotals record should return a specific not-renderable outcome. Do not catch that error and fall back to current-policy Compute, because doing so rewrites the historical monetary meaning. [S1]

The unconditional positive labour-rate requirement also deserves a small policy decision: should a parts-only or fixed-cost-only estimate be viewable without a rate? Do not inherit report-readiness constraints merely because the report already has them.

## E06 — Historical states need two corrections

**Accepted but not Current exists.** The Current flag is separate from State. Selecting another accepted estimate clears the earlier IsCurrent flag without necessarily changing it to Superseded. The document's status mapping must handle Accepted/non-current explicitly, not only Draft, Current, Superseded and Discarded. [S5, S6]

**Discarded historical calculations can be recomputed.** `ForProjection` uses RecordedTotals only for Accepted or Superseded. All other states use Compute. `ValidateDiscard` rejects Accepted and Current but permits a non-current Superseded record. The store then sets State to Discarded. Thus the supported path Accepted → Superseded → Discarded can cause a once-accepted calculation to be projected under current policy instead of its frozen record. [S1, S6, S12]

Resolve this in the canonical domain policy before claiming reliable historical output for every state. Possible decisions are to preserve an accepted calculation based on acceptance provenance even after discard, or prohibit that transition. Do not make the PDF's monetary rules disagree with the screen to work around it.

## E07 — Phase 2 identity and delivery must be revised

The planned identity `estimate-document:{estimateId}:{version}` is too coarse for every possible document the proposed snapshot can produce. The PDF includes live Case header facts, a print date, a Current/status presentation, and renderer/template choices. Accepted estimate money being frozen does not freeze all of those inputs. IsCurrent can change without the estimate's Version changing. [U1, S5, S6]

Choose a clear historical contract: a retained generation is the exact document made from a frozen snapshot, including all printed header values and status/date at generation. Later corrections or layout changes create another generation rather than silently replacing it. A historical retrieval returns the retained bytes, not a fresh rendering with today's header.

Use a generation identity plus a canonical snapshot fingerprint for reuse decisions. Include all material document inputs and the relevant schema/template/calculation versions; pin the renderer/assets when their revision affects reproduction. Store the actual artifact SHA-256 separately from the input fingerprint. Equal inputs and equal output bytes are different concepts.

Do not claim that a printed CURRENT label remains current forever. Either make it explicitly status-at-generation, or limit the meaning of the immutable output and present today's selection separately in Pegasus.

**Delivery binding:** `CaseReportGenerationSnapshot` already freezes `CurrentEstimateId` and `CurrentEstimateVersion`. Attach the document for the estimate pinned by the report being sent, not whichever estimate happens to be Current when attachment preparation runs. Verify the calculation identity and exact retained artifact hash/length; refuse a mismatch rather than silently assembling conflicting documents. [S7]

Keep Phase 2 gated on the intended delivery decision. Reuse the freeze → render → retain → confirm pattern and recovery semantics, but do not implement a broad new document platform in Phase 1.

## E08 — Document and UI design changes

Retain the small Collision Engineers letterhead, restrained red title/rules, grey label cells and consistent typography. The EVA page is valuable for content structure but its very large logo, empty header space, clipped descriptions and unused columns should not be reproduced. The supplied house-style image should remain a visual acceptance reference. [U2–U4]

| Element | Recommendation |
|---|---|
| Title and action | Keep the plan's ESTIMATE title and Estimate PDF action unless the operator chooses a different glossary term. Use one consistent user-facing term in later delivery controls. |
| Company block | Preserve the supplied report's company contact block; the ASCII sketch should not accidentally become permission to remove it. |
| Part number | Include when recorded, preferably as a smaller secondary line beneath Description instead of a permanently empty extra column. |
| Labour costs | Prefer separate Panel labour and Paint labour values to match the existing report's five-component cost projection. A combined subtotal may be secondary. |
| Quantity and price | Label unit amounts unmistakably; do not imply row materials or row hours are also per-unit values. |
| Status | Cover Accepted/non-current. Repeat estimate identity/version and status on continuation pages, not only on page 1. |
| Source | A concise route label can distinguish Manual, Audatex or Glass's. Do not imply the provider issued Pegasus's amended document. Keep detailed provenance in the snapshot. |
| Additional fields | Consider recorded repair days. Do not automatically print internal notes; first define which notes are intended for an external repair specification. |
| Source descriptions | Preserve recorded wording. The extraction's bracketed restorations are editorial annotations, not evidence of missing full source text. Do not strip EVA punctuation at render time. |
| Case reference | Consume the authoritative stored reference. Never reconstruct an a./ap. prefix from outcome inside this renderer. |
| Filename | Include enough selected-estimate identity to distinguish downloads: reference, registration where appropriate, estimate name and version. Sanitize and length-bound it. |
| Action placement | Keep one selected-estimate action in a stable read/edit location. Printing must not require opening an edit session. |

Use one document of as many pages as its content needs. Keeping the ordinary sample compact is desirable; forcing every estimate to one page by shrinking text is not.

## E09 — Backend contract refinements

Keep Core responsible for the document snapshot and semantic validation, Infrastructure responsible for QuestPDF and print styling, and Web responsible for HTTP/viewer behaviour. Reuse `RenderedReportArtifact` unless a concrete incompatibility appears.

Add the missing recorded adjustments, optional part number, explicit completeness facts and complete state mapping to the snapshot. Separate **payload schema version** from **template/layout version**: a data contract change is not the same as moving a border. Keep calculation policy version available from the canonical totals.

Use a selected-estimate projection that scopes by Case and estimate and captures the required Case header consistently. Loading a full workspace and then an estimate separately is not automatically a coherent snapshot. A bounded transaction or before/after version check is reasonable. No long database transaction should remain open while QuestPDF renders. This read consistency check is not an edit lease and need not become another visible approval step. FRD-11 already describes before/after Case-version checking for report snapshot assembly. [S4, S6, S11]

Validate and copy the snapshot into immutable-in-practice values before handing it to the renderer. Bound mutable collections and strings; define deterministic ordering by persisted line position. Catch known data/validation failures into NotRenderable, not a generic server exception.

The daily preview event is an activity record, not proof of every document version viewed. Include estimate identity/version where permitted. Keep the agreed daily deduplication contract unless deliberately revised, and do not imply that one daily history row establishes exact-byte delivery or receipt.

## E10 — Renderer limits and pagination

A shared print-styling helper and shared render gate are appropriate. Extract only what both document families need. Share font/logo resource initialization as well as style constants; do not create another generic document framework.

The existing renderer waits for its semaphore before creating the two-minute timeout budget. Its timeout cancels the caller's wait, not the underlying synchronous render. A slow render continues and releases the gate on completion. [S8]

Define a total request budget that includes queue waiting, bounded admission/backlog and typed busy/timeout outcomes. Keep the gate held until the actual render finishes; releasing it merely because a caller timed out would defeat concurrency protection. Add limits on line count, aggregate characters, pathological unbroken strings, output pages and output bytes. State proposed limits as configurable operational choices, not unexplained business caps on estimate size.

QuestPDF supports repeating table headers. Use `ShowEntire` for small bounded summary blocks, not arbitrary descriptions or the entire estimate. Its documentation explicitly warns that content too large for a page can cause DocumentLayoutException. `EnsureSpace` is the less restrictive option when the aim is to avoid a stranded heading or tiny fragment at the bottom of a page. [W1–W3]

## E11 — Acceptance tests to add

| Area | Minimum additional evidence |
|---|---|
| Independent monetary fixture | Explicit expected values for the supplied sample under the approved policy; no test that merely compares a value to itself through the same helper. |
| Monetary completeness | Nonzero header Additional materials/Other costs, quantity 2 or 3, row materials, fixed specialist charges, specialist hours, mixed panel/paint values and off-pattern fields. |
| Discounts and VAT | Category plus overall discounts, fractional-penny cases, selected VAT categories, nonregistered defaults, explicit category overrides and non-default VAT percentages. |
| Completeness | Null versus zero versus Unpriced; Unknown without override versus Unknown with explicit override; missing accepted RecordedTotals. |
| Precision | Imported six-decimal hours retained in calculation; displayed hour summaries follow the declared rounding policy. |
| Historical states | Draft, Current, Accepted/non-current, Superseded, Discarded; old policy snapshots; switching Current without changing recorded estimate money. |
| Access | Relevant staff rights, all intended readable Case states, unauthorized Case, estimate belonging to another Case, invalid/empty IDs. |
| Consistent reads | Header or estimate changing during snapshot assembly; bounded retry/refusal rather than mixed inputs. |
| Browser workflow | Dirty form, saved form, unavailable PDF, no JavaScript, full-screen estimate, repeat click and no lost unsaved edits. |
| Preview/download | Same bytes/hash for the viewed and downloaded document; safe server filename; no stale shared-cache output. |
| Pagination | Ordinary fixture plus 120 long lines, repeated headers, each row exactly once, no clipped descriptions, final totals once and complete identity on continuation pages. |
| Renderer regression | Rendered-image comparison of existing report outcomes and fee note before/after shared-style extraction, not just text extraction or a green test exit. |
| Runtime | Queue wait included in budget, caller cancellation, overrun with gate retained until completion, input/output bounds and a subsequent successful request. |
| Future delivery | Report frozen to estimate A cannot silently be paired with Current estimate B; retained retry reuses the exact frozen generation. |

Render-image checks and text checks serve different purposes: extracted text can confirm content while still failing to show whether the layout is clipped, overlapping or visually changed.

## E12 — Documentation and operator decisions

Update the existing canonical documents as proposed. The capability should say **preview/download of a saved estimate**, not suggest that Phase 1 has retained issuance, mailbox sending or proven delivery. FRD-11 already distinguishes preview, artifact download and sent evidence. [S11]

Recommended defaults for the plan's decision letters:

| Decision | Default |
|---|---|
| A — Name | Estimate PDF action; Estimate document in the specification; consistent estimate wording in delivery. |
| B — Signature | None; do not imply assessment-report sign-off. |
| C — Part number | Include when present, within the Description cell. |
| D — Labour money | Show panel and paint separately, matching the report. |
| E — Cash discount amounts | Stage them only through the one calculation owner; percentages are an acceptable first release if clearly applied and amounts remain transparent. |
| F — Read-mode placement | Same selected-estimate action location in read and edit mode. |
| G — Capability ID | Separate capability is reasonable; allocate an unused ID rather than assuming RPT-07 is free. |
| H — Phase 2 | Keep delivery/retention gated, but correct its identity design now. |
| I — Your Ref | Use the authoritative recorded claim reference, not an inferred or provider-specific substitute. |

Two additional decisions matter more than most cosmetic ones: the approved treatment of specialist hours, and whether the initial release is strictly a saved working preview or must support controlled outward issue.

## Suggested delivery sequence

**First: policy and examples.** Resolve specialist-hours treatment and the historical discarded-state projection. Agree saved-preview and incomplete-pricing semantics. Produce one consistent expected-money fixture.

**Second: shared rendering extraction.** Extract only common style/resources/runtime support and prove existing report and fee-note output unchanged with rendered comparison evidence.

**Third: estimate preview feature.** Implement the bounded authorized projection, snapshot, renderer and stable UI action. Include the monetary, access, browser and pagination tests above. No separate editing or approval workflow.

**Fourth: retention and delivery, when approved.** Freeze complete snapshots, retain exact bytes and bind the selected estimate artifact to the report's frozen estimate identity. Add recovery and delivery-mismatch tests before enabling sending.

## Source register

### Uploaded evidence

- **U1:** `PLAN(1).md`, especially §§3–7 and §§9–11.
- **U2:** `evaestimate.pdf`, page 1; original source for the sample's printed rows, hours and totals.
- **U3:** `evaestimate-extracted.md`; extraction record and arithmetic reconciliation. Bracketed description completions are editorial annotations.
- **U4:** `reference-repairable-report-p1.png`; supplied accepted house-style comparison image.

### Repository evidence

All paths below are pinned to main commit `8b9d358f71ac02363a3e9fa50549d4fef0fcdac8`. Open them under the repository's `blob/<commit>/` path; branch-head changes after the observation are outside this review.

| ID | Path and reviewed areas |
|---|---|
| S1 | `src/Pegasus.Core/Assessment/Estimates.cs`: VAT policy, EstimateDetails, EstimateOperations, EstimateTotals.ForProjection/Compute. |
| S2 | `src/Pegasus.Core/Reports/AssessmentReportRendering.cs`: ReportRepairCosts.For, render policy and contract versions. |
| S3 | `src/Pegasus.Core/Assessment/Estimates.cs`: WorkUnitDecimals, ValidateDetails and line-amount validation. |
| S4 | `src/Pegasus.Core/Assessment/AssessmentWorkspace.cs`: AssessmentAccessPolicy.CanOpen and workspace/query contracts. |
| S5 | `src/Pegasus.Core/Assessment/RepairSpecifications.cs`: states, version/source model, calculation policy and store contract. |
| S6 | `src/Pegasus.Infrastructure/Persistence/EfRepairSpecificationStore.cs`: AcceptAsync, DiscardEstimateAsync, SetCurrentEstimateAsync, GetVersionAsync. |
| S7 | `src/Pegasus.Core/Reports/CaseReportGeneration.cs`: CaseReportGenerationSnapshot and artifact/generation states. |
| S8 | `src/Pegasus.Infrastructure/Reports/QuestPdfAssessmentReportRenderer.cs`: semaphore, timeout placement, ongoing render and artifact hashing. |
| S9 | `src/Pegasus.Infrastructure/Reports/AssessmentReportLayout.cs`: margins/fonts/colors, footer and CostRows. |
| S10 | `src/Pegasus.Web/Pages/Cases/Shared/_CaseEstimate.cshtml`: selected-estimate read/edit mapping; `wwwroot/js/case-workspace.js` report-preview binder search excerpts. |
| S11 | `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`: renderer boundary, accepted wording, lifecycle viewing, snapshots, preview/download and delivery distinctions. |
| S12 | `src/Pegasus.Core/Assessment/Estimates.cs`: ValidateDiscard and ValidateSetCurrent. |

Repository: `https://github.com/collisionengineers/pegasus`

### External primary documentation checked on review date

- **W1:** QuestPDF, Show entire: `https://www.questpdf.com/api-reference/show-entire.html`
- **W2:** QuestPDF, Ensure space: `https://www.questpdf.com/api-reference/ensure-space.html`
- **W3:** QuestPDF, Table header and footer: `https://www.questpdf.com/api-reference/table/header-and-footer.html`
