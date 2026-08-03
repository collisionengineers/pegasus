# Report blueprints and report templates — draft supporting plan

This is a **draft supporting plan** for the `report-renderer-integration`
task. It is a planning document only: it designs templates, validation, and
verification for the deferred `RPT-01`–`RPT-05` / `EXT-08` capabilities. It
does not activate a capability, does not change accepted requirements, and
does not decide any operator question. Nothing here authorises a Pegasus
caller for `workspaces/report-renderer/`.

## Superseded by operator decision, 2026-08-03

**The operator decided on 2026-08-03 that the C# `CollisionRenderer` is the
authoritative design and `DESIGN_SPEC.md` is superseded evidence.** This plan was
written before that decision and its central tension is resolved against
`DESIGN_SPEC`.

What that means for this document:

- **It is retained as analysis, not as a work plan.** Sections 1 and 6 — the gap
  analysis and the reconciliation against existing Pegasus owners — are the
  durable value: they record precisely what the C# renderer does *not* do, and
  which spec items collided with owners Pegasus already has. Keep reading those.
- **Sections 2, 3, 4, 5, 7 and 9 do not describe work that will be done.** No
  `AssessmentReportDocument`, no `assessment_report.scriban`, no four-preset
  family, no composed-narrative engine, no computed-once figure engine derived
  from `DESIGN_SPEC`, no new design tokens. Nothing is imported from
  `docs/reference/rendererref1/`, which keeps its registered role as evidence.
- **The RPT capability outcomes are unchanged, and the C# renderer does not
  satisfy them.** It performs no arithmetic beyond a fee-note sum in
  `HtmlComposer`; two template IDs cover three or four outcomes; there is no
  conservative/maximised pair for RPT-03 and no versioned amendment identity for
  RPT-05. The decision removes `DESIGN_SPEC` as the *specification* for that
  work, not the work itself. A specification must come later from accepted
  `CASE-31`/`ENG-01`/`ENG-02` data plus operator decisions.
- **Stage 1 touches no template.** The four `.scriban` bodies and `report.css`
  relocate unchanged.
- Open questions 1–12 below are **deferred, not answered**. They return when the
  RPT specification is written. One keeps a live edge:
  `docs/open-decisions.md:222` "Report wording" stays open in canonical
  documentation regardless of lineage, and the Core contract's wording gate still
  defaults closed.

The rest of this document is preserved unedited so the analysis and its evidence
remain readable.

## Terminology note: "blueprint"

The word **blueprint** appears nowhere in Pegasus canonical documentation
(`docs/`, `design/`, `NOW.md`, or the renderer workspace). This plan does not
introduce it as a Pegasus concept. Everything the task calls a "blueprint" is
mapped onto the existing renderer vocabulary:

- **template family** — one typed model record plus one `.scriban` body
  (today: `MarketValuationEvidenceDocument`, `AdvertEvidencePackDocument`,
  `FeeNoteDocument`, `ExpertReportDocument`).
- **template / preset** — one `TemplateDescriptor` entry in `TemplateCatalog`,
  identified by `Id`, carrying `Name`, `Description`, `ModelType`,
  `TemplateResource`, `DensityProfile`, and `FileNameSuffix`.

Several `TemplateDescriptor` entries may share one family; nine of the twelve
current descriptors already do.

## Source material and its standing

`docs/reference/rendererref1/` is registered in `docs/reference/README.md:22`
with an eight-word description and is referenced nowhere else in the
repository. Under `docs/reference/README.md:3-14` it is **evidence, not a
requirement, implementation proof, current directory, or authorization**, and
"any future import or directory use requires operator review, an accepted data
contract, and separately authorized target operations."

The directory contains:

| File | Standing |
| --- | --- |
| `DESIGN_SPEC.md` | Design record of a Python generator, `ce_report_generator.py`, that is not present in this repository |
| `report_data_schema.json` | JSON Schema for that generator's job file |
| `sample_job_PK12TMZ.json`, `sample_job_repairable.json`, `sample_job_cash_in_lieu.json`, `sample_job_contract_repair.json` | Four worked example jobs; all four carry the same claimant name, principal address block, registration, and VIN |
| `Sample - {Total Loss, Repairable, Cash in Lieu, Contract Repair} Report.pdf` | Binary rendered output; immutable visual acceptance evidence, unreadable as text |
| `logo_no_margin.png`, `andy_patterson.png`, `ed_mawdsley.png`, `neil_oreilly.png` | Duplicates of assets already governed under `design/brand/` |

The four sample jobs contain a personal name and a named principal's postal
address. `docs/reference/README.md:11-13` forbids copying personal names,
addresses, or contact rows into canonical prose, and the renderer workspace's
own `docs/adr/0009-reference-material-handling.md` git-ignores customer
reference material entirely. This plan therefore never lifts sample values into
templates, fixtures, starters, or committed baselines. See open question 9.

## The central tension: two renderer lineages

Two lineages exist and their data contracts do not match.

- `DESIGN_SPEC.md` is "Collision Engineers — Assessment Report Template
  (Design I, locked July 2026)", "Approved by Andrew, 21/07/2026". It describes
  a **Python** generator invoked as `python ce_report_generator.py job.json
  out.pdf`. That generator is absent from this repository; only its assets and
  schema were duplicated here.
- `design/assets/report-renderer/templates/report.css` (header comment, lines
  6–7) records that the DATA register was "Ported verbatim from the proven
  WeasyPrint renderer". WeasyPrint is Python.
  `workspaces/report-renderer/docs/adr/0003-unified-dotnet-8-stack.md:42-44`
  lists "Python (continuing the `report-renderer` lineage)" as a **rejected**
  alternative.

So the C# `CollisionRenderer` is the accepted successor stack, and it inherited
the **stylesheet** but not the **job schema**. The C# renderer's
`Models/Documents.cs` (245 lines, almost every field `string?`) is a generic
**content-block** model: an `ExpertReportDocument` is a title plus a list of
`ReportSection`s, each a list of `ContentBlock`s of seven types (`paragraph`,
`bullets`, `datatable`, `keyvalue`, `evidencetable`, `valuebox`, `mediarow`).
`report_data_schema.json` is a **domain model with computed figures**: it names
`assessment.outcome`, `costs.labour_hours`, `assessment.values.engineer`, and
forbids the derived values from being supplied at all.

The practical consequence: a `total-loss-report` today is whatever prose and
blocks the caller types into `ExpertReportDocument`. Under `DESIGN_SPEC` it is
a fixed page sequence whose every figure and most of whose sentences are
derived. These are not the same artefact, and the second cannot be reached by
configuring the first. This authority question is open question 1 and gates
everything below.

## 1. Gap analysis

Current equivalents are named by exact type and file. All renderer paths are
relative to `workspaces/report-renderer/`, except `.scriban`/`.css` which live
at `design/assets/report-renderer/templates/`.

| Schema section | Current C# renderer equivalent | Gap | Work required |
| --- | --- | --- | --- |
| `refs` (`our_ref`, `your_ref`, `date`, `claimant_name`, `incident_date`, `report_for[]`) | `DocumentMeta` (`Models/Documents.cs:9-15`) carries `OurRef`, `YourRef`, `Date`, `PreparedBy` only; the letterhead is built in `HtmlComposer.Letterhead` | No `claimant_name`, no `incident_date`, no `report_for[]` address block, no composed matter line; `DocumentMeta.PreparedBy` has no schema counterpart | Extend the new model with an addressee block and claimant/incident identity; compose the matter line; leave `DocumentMeta` untouched so the other eleven templates are unaffected |
| `vehicle` | `SubjectVehicle` (`Documents.cs:17-33`), all `string?`, 14 fields | Overlap on make/model/registration/vin/fuel/mileage only. **No** `vehicle_type` enum, **no** `mileage_source`, **no** `condition` enum, **no** `year` (it has `FirstRegistered`), `engine_cc` vs free-text `Engine`. `SubjectVehicle` adds `Derivative`, `BodyType`, `Transmission`, `Colour`, `VehicleHistory` with no schema counterpart | New typed vehicle record for the assessment family with the three enums; keep `SubjectVehicle` for the valuation/fee-note families; `Format.SubjectMileage`/`Format.Year` are reusable |
| `incident` (`date`, `instructions_received`, `assessed`) | None | Absent entirely | New record; note the schema gives `refs.date` a `dd/MM/yyyy` pattern but leaves all three `incident` dates unpatterned |
| `assessment` | None. Outcome is implied only by choosing template id `total-loss-report` vs `repairable-contract-repair-report` | Absent entirely; six enumerations with no current representation. `method` collides with ADR-0018 (see §6) | New record; enums as C# enums serialised camelCase by `CrJson.Options` |
| `assessment.values` (`engineer`, `retail`, `trade`) | `MarketValuationEvidenceDocument.AssessedRetailValue` / `GuideValue`, both `string` | Different shape and different purpose; the valuation template evidences one retail figure from adverts, the assessment report prints three chosen figures | New typed decimal triple; do **not** reuse the valuation model. `EXT-13`/`ENG-02` own where the three figures come from |
| `costs` | None for repair costs. The only arithmetic in the renderer is `HtmlComposer.FeeNote` (`Templating/HtmlComposer.cs:122-125`), which sums fee line items and applies `VatRate` | Absent entirely, and the one existing computation lives in the **renderer**, not Core — which contradicts `docs/adr/0009-adopt-pegasus-monorepo-workspaces.md:56-57` | New `decimal` cost record; move all derivation to `Pegasus.Core` (§3); treat the existing fee-note computation as a pre-existing single-owner conflict to resolve at integration |
| `worklists` (`new_parts[]`, `repairs[]`, `operations[]`) | `ContentBlock` type `bullets` (`items[]`) with an author-supplied `ReportSection.Heading` | Structurally expressible today but not typed, not named, and not enforced as exactly three sections; "paint items merge into `operations`" is unrepresented | Typed three-list record; `RPT-02`'s "itemised repair-specification breakdown"; source is `ENG-01`/`EXT-12`, not this plan |
| `narrative` plus the `not/anyOf` forbidding `settlement`, `introduction`, `desktop_assessment`, `salvage`, `pre_incident_condition` | `ExpertReportDocument.Intro[]` and free-form `ContentBlock` paragraphs — every sentence is typed by the caller | Inverted model. The current renderer's whole design is that prose is supplied; the spec's whole design is that prose is composed and supplying it is a validation error | Composition engine in `Pegasus.Core` (§4) plus negative validation (§5). This is the single largest behavioural change |
| `engineer` (`name`, `qualifications`, `signature` ∈ three keys) | `SignatureBlock` (`Documents.cs:65-82`); allowlist in `Design/BrandAssets.cs:81-82` is exactly `andy_patterson`, `ed_mawdsley`, `neil_oreilly` | Close match. Gaps: the schema has no `role`/`org`/`aqp_number`/`closing`; the schema lets `name` and `qualifications` vary freely against a fixed signature key, so a payload can pair the wrong name with a signature | Reuse `SignatureBlock`; bind name + qualifications + signature key as one Core-owned engineer record so the three cannot disagree. Qualifications for two of the three engineers are blocked |
| `fee` (`agreed_fee`, `description_lines[]`) | `FeeNoteDocument` (`Documents.cs:153-165`) | The standalone fee note is richer than the in-report fee page. The spec's fee page needs the same Subtotal/VAT/TOTAL DUE rows, the payment grid, terms, and the VAT number in header **and** footer, on that page only | Share one Core fee computation and one Scriban fragment between `fee-note` and the assessment report's fee page, so the two cannot disagree. The per-page VAT-number rule needs page-scoped furniture |
| `statement_of_truth` (optional `string[]` override) | None; the current renderer has no default statement | Absent; the spec says the generator "carries the standard CPR wording by default" but that wording is not in this repository, and the spec itself marks it "to be revised at finalisation" | Blocked by `docs/open-decisions.md` "Report wording". Model the field and the override; ship no default text |
| `photos` (`string[]`, `minItems: 1`) | `ContentBlock` type `mediarow` → `MediaItem { Caption, ImagePath, Note }`; path policy in `Validators.ValidateImagePath` | Spec requires a uniform 48 mm-crop grid, **six per page**, auto-continuation, heading "Vehicle Images", and **no captions anywhere**. Current CSS is a 2-column grid with a caption `h4` and a 46 mm placeholder height. Spec's "photos exist on disk" conflicts with the API path policy (`docs/TEMPLATES.md:384-389`) | New caption-free six-up grid CSS and a typed photo list; images must arrive as accepted Core-owned evidence references or `data:` URIs, never caller-local paths. Selection and ordering are **not** renderer concerns (§6) |
| `impact_diagram` (single `string`) | `mediarow` with one item | Expressible; the spec's "Future improvements" wants it generated from a location code instead | Model as one optional image; the code-driven overlay is out of scope and not planned |
| *(not in the schema)* report version / artifact hash | None | `docs/requirements.md:924-929` requires "deterministic template and payload versioning" and "immutable issued artifact identity and hash". The reference schema has neither | Add a Core-owned template-version and payload-version identity to the render contract before any issue path exists |
| *(not in the schema)* page furniture | `HtmlComposer.FooterTemplate` (`HtmlComposer.cs:264-275`) | Spec requires a registration/our-ref/company/site footer with "Page n of N" bottom-right, plus badges and four figure tiles on page 1, neither of which exists in `report.css` | Per-template footer, **not** a global change: the footer is shared by all twelve templates today and altering it silently changes eleven other documents |

**Capability coverage of the reference material.** `DESIGN_SPEC` and the schema
cover `RPT-01` and `RPT-02` only. They contain no conservative-versus-maximised
specification pair or uplift (`RPT-03`), no diminution percentage (`RPT-04`),
and no amendment or version identity (`RPT-05`). Those three capabilities gain
nothing from this material and must be specified separately.

## 2. Template inventory to build

### The four outcomes: one family, four presets

`DESIGN_SPEC` states plainly that "Cash in lieu and contract repair are
otherwise identical to repairable (per Andrew)". Comparing the four sample jobs
confirms it mechanically: the only differences across
`sample_job_repairable.json`, `sample_job_cash_in_lieu.json`, and
`sample_job_contract_repair.json` are `assessment.outcome` and
`assessment.legal_status`. Every other field — costs, worklists, narrative,
engineer, fee, photos — is identical. `sample_job_PK12TMZ.json` adds only
`category` and `salvage_value`.

The four outcomes differ in exactly four places: the title, the badge, the red
tile's label and source figure, and the settlement section's heading and
sentence. That is branching, not four documents.

**Recommendation: one template family, four `TemplateDescriptor` presets.**

- One new model record `AssessmentReportDocument`.
- One new body `assessment_report.scriban` in
  `design/assets/report-renderer/templates/`.
- One new `HtmlComposer` branch that switches on `assessment.outcome` for the
  four differing slots.
- Four `TemplateDescriptor` entries sharing that model and body, so each
  outcome keeps its own catalogue name, description, and `FileNameSuffix`.

This is exactly the pattern the catalogue already uses: nine of the twelve
descriptors bind `ExpertReportDocument` + `expert_report.scriban`. Four
separate `.scriban` bodies would create four copies of one page sequence, each
free to drift, which is the precise failure mode the computed-once rule exists
to prevent.

### Mapping onto the existing twelve template IDs

| Existing id | Model today | Verdict |
| --- | --- | --- |
| `total-loss-report` | `ExpertReportDocument` | **Superseded** by `assessment-total-loss` on the new family. Retire the id or repoint it; do not leave two total-loss paths |
| `repairable-contract-repair-report` | `ExpertReportDocument` | **Superseded and split** into `assessment-repairable` and `assessment-contract-repair`. The spec treats them as distinct titles, badges, and settlement sentences; one id cannot express both |
| *(none)* | — | **New**: `assessment-cash-in-lieu` |
| `fee-note` | `FeeNoteDocument` | **Reused** for the standalone note. The assessment report's fee page is a section of the new family sharing one Core fee computation and one Scriban fragment |
| `addendum-report` | `ExpertReportDocument` | **Reused unchanged** in this plan. `RPT-05` needs amendment/version identity that the reference material does not supply |
| `diminution-rebuttal` | `ExpertReportDocument` | **Reused unchanged**. `RPT-04` needs the Engineer-entered percentage; not in the reference material |
| `roadworthy-criminal-report` | `ExpertReportDocument` | **Reused unchanged**. Note the assessment family's `legal_status` is a different concept and must not be conflated with this template |
| `part-35-response`, `response-letter`, `blank-letterhead`, `expert-report` | `ExpertReportDocument` | **Reused unchanged** |
| `market-valuation-evidence`, `advert-evidence-pack` | own models | **Reused unchanged**. `EXT-13`/`ENG-02` own how the three chosen values reach the assessment report; the valuation template is not that route |
| *(none)* | — | **New, unspecified**: an `RPT-03` audit template. The reference material has no conservative/maximised pair. Out of scope here |

### Capability mapping

| Capability | Served by | Not served |
| --- | --- | --- |
| `RPT-01` | The typed model, the Core figure engine (§3), the validation gate (§5), and the fixed design (§9) | Nothing renders until `CASE-31`/`ENG-01`/`ENG-02` supply accepted data |
| `RPT-02` | Four presets + the shared fee page + the three typed worklists | Repair-specification source (`ENG-01`, `EXT-12`) |
| `RPT-03` | — | No reference material; needs its own specification |
| `RPT-04` | — | No reference material; needs its own specification |
| `RPT-05` | — | No reference material; needs version/amendment identity |
| `EXT-08` | — | Activation only; explicitly out of scope for this plan |

## 3. The computed-once figure engine

### Where it lives

`docs/adr/0009-adopt-pegasus-monorepo-workspaces.md:55-57` is unambiguous:
"Future rendering consumes a Core-owned render contract; **report policy does
not move into Infrastructure or the renderer**." Every derivation below is
business policy about money and settlement. They therefore belong in
`Pegasus.Core`, not in `HtmlComposer`, not in a `.scriban` body, and not in
Infrastructure.

The invariant `DESIGN_SPEC` states — "every figure on every page renders from
one variable" — is adopted. Its stated *location* (the generator derives them)
is rejected in favour of the accepted Pegasus ownership rule. The resolution:

1. Core computes every figure once from the raw components and emits them, with
   the raw components and a policy version, in the render contract.
2. The renderer **formats** figures (`Format.Money` already does this) and never
   derives one.
3. Validation rejects a payload that supplies a derived figure it should not,
   and rejects one that omits a required raw component.

Note the existing conflict: `HtmlComposer.FeeNote`
(`Templating/HtmlComposer.cs:122-125`) already computes subtotal, VAT, and total
inside the renderer. Integration must move that to Core or the repository will
have two owners of the same VAT rule.

### Formula table

All operands are `decimal`. VAT is always 20%.

| # | Figure | Formula | Rounding | Inputs |
| --- | --- | --- | --- | --- |
| F1 | Labour total | `labour_hours × hourly_rate` | 2 dp | `costs.labour_hours`, `costs.hourly_rate` |
| F2 | Subtotal | `F1 + parts + paint_materials + specialist_other` | 2 dp | F1 and the three cost components |
| F3a | VAT, repairer VAT-registered | `0.20 × F2` | 2 dp | F2 |
| F3b | VAT, repairer **not** registered | `0.20 × (parts + paint_materials)` | 2 dp | `costs.parts`, `costs.paint_materials` |
| F4 | Repair total | `F2 + F3` | exact (operands already 2 dp) | F2, F3 |
| F5 | Recommended settlement (`total_loss`) | `values.engineer − salvage_value` | 2 dp | `assessment.values.engineer`, `assessment.salvage_value` |
| F6 | Cash in lieu settlement | `= F4` | — | F4 |
| F7 | Agreed contract repair | `= F4` | — | F4 |
| F8 | Fee VAT | `0.20 × fee.agreed_fee` | 2 dp | `fee.agreed_fee` |
| F9 | Fee total due | `fee.agreed_fee + F8` | exact | `fee.agreed_fee`, F8 |

Ordering matters and must be fixed: **round each named figure to 2 dp at the
point it is defined, then derive later figures from the rounded value.**
Sum-of-rounded and rounded-of-sum differ, and pages that print F1 and F2 must
add up on the page.

### Rounding rule in .NET

`DESIGN_SPEC` specifies "Decimal arithmetic, ROUND_HALF_UP". Python's
`decimal.ROUND_HALF_UP` means *ties away from zero*, which is exactly .NET's
`MidpointRounding.AwayFromZero`. They agree for both signs, so:

```
decimal.Round(value, 2, MidpointRounding.AwayFromZero)
```

Two hard constraints:

- **`decimal`, never `double`.** `double` cannot represent `0.20` exactly and
  will produce off-by-a-penny results that differ by input. `Format.Money`
  already takes `decimal`; the new cost model must be `decimal` throughout.
- The one figure that can be negative is F5 (if salvage exceeds the engineer's
  value). Ties-away-from-zero is still correct there, but the *presentation* and
  *narrative* for a negative settlement are unspecified — see open question 6.

### Worked check from the reference sample

Derived from `sample_job_PK12TMZ.json` for use as an implementation vector.
**Not yet verified against `Sample - Total Loss Report.pdf`** — the PDFs are
binary and cannot be read here; confirming these numbers against the sample PDF
is an implementation step.

| Figure | Value |
| --- | --- |
| F1 labour | `25.9 × 83.28 = 2156.952` → `£2,156.95` |
| F2 subtotal | `2156.95 + 278.44 + 769.42 + 466.63` → `£3,671.44` |
| F3a VAT (registered) | `0.20 × 3671.44 = 734.288` → `£734.29` |
| F4 repair total | `£4,405.73` |
| F5 settlement | `4011.00 − 320.88` → `£3,690.12` |
| F8 fee VAT | `0.20 × 100.00` → `£20.00` |
| F9 fee total | `£120.00` |

### The two VAT modes

`costs.repairer_vat_registered` selects the calculation and the calc-row label:

| Mode | Calculation | Calc-row label |
| --- | --- | --- |
| `true` | 20% on the full subtotal (F3a) | Conventional VAT row |
| `false` | 20% on parts and paint only (F3b); **no** VAT on labour or additional operations | Exactly `VAT (20% — parts & paint only)` |

The exact label string is quoted verbatim from `DESIGN_SPEC` and must be treated
as fixed text, not paraphrased. The conventional-mode row label is **not**
specified in the reference material and is an operator question.

### Unresolved: recovery and storage charges

`recovery_charge` and `storage_charge` sit under `costs` in the schema, but
`DESIGN_SPEC`'s subtotal formula (`labour + parts + paint + specialist`)
excludes them, and the spec describes them only as driving a narrative
paragraph. Whether they enter F2, F4, F5, or nothing is undetermined by both
documents. See open question 4. Do not assume either way.

## 4. Composed-narrative inventory

`DESIGN_SPEC` reduces the free-text inputs for a whole report to four:
`history_check`, optional `engineers_comments`, optional `nature_of_incident`
override, and `unroadworthy_reason`. Everything else is generated. This is the
inversion of the current `ExpertReportDocument` model, where the caller types
every paragraph.

Composition is prose *about accepted case facts and figures*. Like the figure
engine, it is business policy and belongs in `Pegasus.Core`.

| # | Composed text | Composed from | Wording status |
| --- | --- | --- | --- |
| N1 | Matter line | `refs.claimant_name`, `refs.incident_date` | **Settled form**, but it hard-codes "Road Traffic Accident" for every case; Pegasus supports other case types — open question 7 |
| N2 | Intro paragraph | `incident.instructions_received`, `incident.assessed`, `assessment.method`, `assessment.location_address` | **Blocked**: the spec states it is "fully COMPOSED" but supplies no sentence text |
| N3 | Vehicle-location line | `assessment.method` → literal `Image Based Assessment`, or `location_address` | Literal is settled and owned by ADR-0018; the surrounding sentence is unsupplied |
| N4 | Mileage sentence | `vehicle.mileage_source` (six options), `vehicle.odometer_miles` | **Partly blocked**: one example given for `online_data`; the other five (`owner`, `repairer`, `principal`, `average`, `tbc`) are unsupplied |
| N5 | Unroadworthy sentence | `assessment.unroadworthy_reason` | **Settled** |
| N6 | Nature of Incident default | `assessment.impact_severity` × `assessment.impact_location` (5 × 14 = 70 combinations) | **Ambiguous**: the literal slash in "collision/impact" is unresolved, as is the display form of every enum value — open question 5 |
| N7 | Impact Magnitude line | Same two fields | Same ambiguity as N6; the spec says both are composed from severity + location but does not distinguish their forms |
| N8 | Pre-Incident Condition | `vehicle.condition` | **Settled**. Note the schema enum is `below_average` while the sentence needs `below average` |
| N9 | Settlement narrative | outcome + F4/F5/F6/F7 | **Four sentences given verbatim** in the `DESIGN_SPEC` outcomes table, one per outcome, each with a value box. Treat as settled *subject to operator confirmation* — they are a design record, not an accepted Pegasus wording |
| N10 | Salvage paragraph | `assessment.category`, `assessment.salvage_value` | **Blocked**. The open decision requires accepted wording for Categories N, A, B and N/A. The spec says Category S is "confirmed" but does **not** reproduce the S text anywhere, so no salvage wording at all exists in this repository |
| N11 | Recovery & Storage paragraph | `costs.recovery_charge`, `costs.storage_charge` | **Blocked**: explicitly a placeholder in both the spec and the open decision. Present only when a charge is supplied |
| N12 | Desktop Assessment section | `assessment.method == image_based` | **Blocked**: "fixed sentence" whose text is unsupplied. Omitted entirely when `method` is `physical` |
| N13 | Statement of truth | none (fixed default, optional override) | **Blocked**: text absent from this repository; the spec marks it "to be revised at finalisation" and notes it references Glass's Evaluator/Thatcham, which conflicts with "Glass's code REMOVED from all reports" |
| N14 | Badges | outcome, `category`, `legal_status` | **Settled** |
| N15 | Figure-tile labels | outcome | **Settled per the spec table** |

**Conclusion.** Of fifteen composed elements, six are settled, two are
ambiguous, and seven have no accepted text in this repository. A template can be
built; a report cannot be issued. This plan therefore delivers the composition
*mechanism* with every blocked string behind a single, obvious, review-gated
placeholder registry — never invented prose, and never a silent empty paragraph.

## 5. Validation rules before render

`DESIGN_SPEC` requires "Invalid jobs fail loudly — nothing renders." The
renderer already models this: `PayloadValidator.Validate` returns
`ValidationResult` and `RenderValidationException` (`Contracts.cs:129`) aborts
the render. A new `case AssessmentReportDocument d:` in `Validators.cs` is the
required extension point (`docs/TEMPLATES.md:488-512` makes a validator case
mandatory, not optional).

### Presence and enumeration

| Rule | Source | Severity |
| --- | --- | --- |
| All nine top-level sections present | schema `required` | Error |
| `refs`: six fields present; `report_for` has ≥ 1 line; `date` and `incident_date` match `dd/MM/yyyy` | schema | Error |
| `vehicle`: seven fields present | schema | Error |
| `incident`: `instructions_received`, `assessed` present | schema | Error |
| `assessment`: six fields present | schema | Error |
| `values`: `engineer`, `retail`, `trade` all present and `> 0` | schema `exclusiveMinimum: 0` | Error |
| `costs`: all six required components present; `hourly_rate > 0`; the rest `>= 0` | schema | Error |
| `narrative.history_check` present and non-empty | schema + spec | Error |
| `engineer.signature` ∈ the three allowlisted keys | schema enum; matches `BrandAssets.AvailableSignatures` exactly | Error |
| `fee.agreed_fee > 0` | schema | Error |
| `photos` has ≥ 1 item | schema `minItems: 1` | Error |
| Every enum value is in range | schema | Error |

### Conditional requirements

Only the first of these is expressed in the JSON Schema's `if`/`then`; the other
three exist **only as prose descriptions** and must be enforced in code or they
will not be enforced at all.

| Rule | Expressed in schema? |
| --- | --- |
| `outcome == total_loss` ⇒ `category` and `salvage_value` required | Yes |
| `method == physical` ⇒ `location_address` required | No — description only |
| `mileage_source != tbc` ⇒ `odometer_miles` required | No — description only |
| `legal_status == unroadworthy` ⇒ `unroadworthy_reason` required | No — description only |

### Negative constraints — fields that must NOT be supplied

| Rule | Expressed in schema? |
| --- | --- |
| `refs` must not contain `matter` | Yes |
| `narrative` must not contain `settlement`, `introduction`, `desktop_assessment`, `salvage`, or `pre_incident_condition` | Yes |
| `costs` must not contain a labour total, subtotal, VAT, repair total, or settlement | **No** — the description says computed values are "never supplied" but no `not` clause enforces it |

The third row is a real hole. Enforce it explicitly, otherwise a payload can
carry a stale hand-typed total that the schema accepts and the renderer ignores
— precisely the disagreement "computed once" exists to prevent.

### The VIN contradiction — flagged, not resolved

`DESIGN_SPEC`'s reliability rules list "17-char VIN" among the validations.
Its Section 3 and the schema's `vehicle.description` both say "VIN/engine/fuel/
odometer OPTIONAL, **no VIN format rule** (bicycles, trailers etc. may have
none)", and `vehicle.vin` is a plain optional `string` with no `pattern` and no
`minLength`. The two statements in one document contradict each other. The
later, more specific statement (and the schema, and the `vehicle_type` enum
containing `bicycle` and `trailer`) points to *no VIN rule*. This plan does
**not** choose: it is open question 3.

### Further schema inconsistencies to raise, not silently fix

- `assessment.category` enum includes `""` alongside `A`, `B`, `S`, `N`, `N/A`.
  All three non-total-loss sample jobs set `"category": ""`. `DESIGN_SPEC` lists
  the dashboard dropdown as `A | B | S | N | N/A` with no empty option. Decide
  whether empty means "absent" or is a distinct value.
- `refs.date` and `refs.incident_date` carry a `dd/MM/yyyy` pattern;
  `incident.date`, `incident.instructions_received`, and `incident.assessed`
  carry none, yet all five are dates printed on the same page.
- `vehicle.year` is a `string` and the sample carries a display label, not a
  year. `Format.Year` extracts a four-digit year from free text; whether the
  report prints the label or the year is unspecified.
- `refs.incident_date` and `incident.date` are two independent fields holding
  the same fact in every sample. One must be authoritative.

### Image path policy

`photos[]` in the reference schema are relative filesystem paths and the spec
validates that they "exist on disk". Under Pegasus that is not acceptable at the
API boundary: `Validators.ValidateImagePath` plus the API policy documented at
`docs/TEMPLATES.md:384-389` reject raw caller-local paths and HTTP(S) image URLs
in JSON requests. Assessment-report photos must arrive as accepted Core-owned
evidence references resolved by Core, or as `data:` image URIs, or through the
multipart upload route.

## 6. Reconciliation with existing Pegasus owners

Strict rule applied throughout: **no second policy owner.** Where Pegasus
already owns a concept, the spec item defers to it.

| `DESIGN_SPEC` item | Existing Pegasus owner | Verdict |
| --- | --- | --- |
| `assessment.method` → select `Image Based Assessment` or enter a vehicle location address | `docs/adr/0018-provider-inspection-mode-database-setting.md` | **Defer to ADR-0018.** The renderer receives a resolved mode and address; it never offers a "control" and never derives the mode. The spec's dashboard control is superseded by the Principal setting plus the case-level override |
| Vehicle History Check as pass-through from "e.g. Experian AutoCheck", Mandatory | `EXT-01`/`EXT-02` (DVLA/DVSA + MOT); `docs/open-decisions.md` leaves each global check's provider open | **Operator question.** Pegasus has no Experian adapter and selects no history provider. The report field stays a typed pass-through with a source label; do not name a provider. Open question 2 |
| "Open item: DVLA lookup on registration to auto-fill make/model/year/engine/fuel" | `EXT-01` | **Defer.** Already an accepted Pegasus capability; not a renderer concern |
| Valuation guide boxes; "Glass's code REMOVED from all reports" | `EXT-13` and `ENG-02` | **Defer to EXT-13/ENG-02.** Adopt only the *report-side* consequence: exactly three figures reach the report and no guide identity or Glass's code appears. The renderer never names a valuation source |
| Repair specification imported from an estimating system | `ENG-01` and `EXT-12` | **Defer.** Adopt only the report-side consequence: three named lists, names only, no part numbers and no per-line prices, paint merged into operations |
| "Include/exclude toggle, resize, crop and rotate are DASHBOARD features — the generator never manipulates images" | `design/product/ui-spec.md:44`; `design/product/requirements.md:47` | **Adopt the renderer half, defer the UI half.** The renderer never manipulates images. Selection and ordering belong to the future Engineers screen, not Case evidence and not the renderer |
| Statement of truth "keep current wording for now… to be revised at finalisation" | `docs/open-decisions.md` "Report wording" | **Blocked.** Model the field; ship no default text |
| Engineer list with signature images | `design/brand/signatures/**`; `docs/requirements.md:949`; `BrandAssets.AvailableSignatures` | **Adopt as-is, add nothing.** Exactly three engineers. Qualifications for two are blocked. Do not invent an engineer, a qualification, or a signature key |
| Impact diagram generated from a location code | none | **Not planned.** Keep the per-case image field |
| Stress-test suite | none | **Adopt.** Genuinely good verification; see §7 |
| API pipeline where an AI structures incoming case data into this JSON | `docs/requirements.md:940-942`; the `AI-*` capabilities; ADR-0009 | **Reject as described.** No AI-structured payload may reach a renderer without human acceptance through Core |
| VAT number "only on the fee note page" | `FeeNoteDocument.VatNumber` + `HtmlComposer.FeeNote` footer swap | **Adopt**, but note the footer is currently document-wide, not page-scoped |
| Payment details (named bank, sort code, account number) | `PaymentDetails` | **Adopt the structure; not the literal values.** Banking details are per-case/principal data from Core, not template constants, and are not copied into this plan |

## 7. Visual acceptance plan

### What exists today

`workspaces/report-renderer/scripts/visual-regression.ps1` already requires
Poppler's `pdftoppm`; has a default mode that renders a starter payload per
template, rasterises to PNG, and compares page-by-page SHA-256 against
`artifacts/visual-regression/approved/<id>/` with `-Approve` to seed; and has a
`-ReferenceMap` mode that compares a named template against a supplied
reference PDF.

`artifacts/` is git-ignored, so **no baseline is committed today** and a fresh
clone reports `MISSING APPROVAL` for every template.

### How the four sample PDFs are used

The sample PDFs are **immutable visual acceptance evidence for human review**,
not an automated gate. Reasons, in order:

1. They were produced by a renderer that is not in this repository, on a
   different rasteriser and font stack. Page-level SHA-256 equality against a
   Chromium-rendered PDF will never pass, so `-ReferenceMap` in its
   hash-comparison form cannot gate on them.
2. Their input data contains a personal name, a principal's address, a
   registration, and a VIN. Committing rasters of them would put case-like
   material into tracked history, against `docs/reference/README.md:11-13` and
   the renderer workspace's own reference-material ADR.

Recommended use: run `-ReferenceMap` once, manually, pointing at a synthetic
payload and the corresponding sample PDF, to produce side-by-side rasters under
the ignored `artifacts/` tree for an operator to review page by page. Record the
review outcome as a decision, not as a committed image. The four PDFs remain
untouched bytes in `docs/reference/rendererref1/`.

### Recommended committed baseline

| Option | Verdict |
| --- | --- |
| Commit approved PNGs under a tracked path | **Reject** — large binaries, and any baseline built from sample data is case-like |
| Commit a page-hash manifest (page count + per-page SHA-256) under a tracked path, generated from **synthetic** fixture data | **Recommend** — small, diffable, reviewable, and carries no case content |
| External artifact store | Needs an operator decision; not proposed |

Concretely: extend `visual-regression.ps1` with a manifest mode that writes and
compares a tracked JSON of `{ templateId, pageCount, pageHashes[] }`. Because
the default mode already renders from `AuthoringCatalog` **starter payloads**
(Core-owned placeholder prompts, not case data), adding the four new presets to
`AuthoringCatalog` automatically extends the regression sweep with non-case
data. That is the cheapest correct path.

Two determinism prerequisites before any hash baseline is meaningful:

- **Fonts.** `report.css` declares `Arial, Helvetica, sans-serif`; the
  Dockerfile installs `fonts-liberation` + `fonts-dejavu-core` as the
  Arial-metric substitute. Arial and Liberation Sans are metric-compatible but
  not pixel-identical, so a hash baseline is valid **per platform**. Pin the
  baseline platform and state it.
- **Dates.** `HtmlComposer.ResolveDate` falls back to `Format.Today()` when
  `meta.date` is absent, which makes any starter render non-deterministic. Every
  regression fixture must set an explicit date.

### Stress-test suite

Adopted from `DESIGN_SPEC`'s "Future improvements" and extended. Each is a
synthetic fixture, re-rendered on every template change:

| # | Fixture | Proves |
| --- | --- | --- |
| S1 | Maximum parts list | Table continuation with repeated headers; no garbling |
| S2 | 14+ photos | Six-per-page grid auto-continuation across pages |
| S3 | Minimal repairable job (every optional field omitted) | `—` / `TBC` fallbacks; omitted empty Vehicle Data rows; Desktop Assessment presence rule |
| S4 | Very long model names and a long single-token model | Wrapping without overflow in tiles, badges, and the vehicle grid |
| S5 | `repairer_vat_registered: false` | The parts-and-paint-only VAT branch and the exact calc-row label |
| S6 | `vehicle_type: bicycle`, no VIN/engine/fuel/odometer, `category: N/A` | The relaxed-VIN path and the `CATEGORY N/A` badge |
| S7 | `mileage_source: tbc` with no `odometer_miles` | The one legitimate odometer omission |
| S8 | `method: physical` with a long multi-line address | Address rendering; Desktop Assessment omitted entirely |
| S9 | `salvage_value > values.engineer` | Negative-settlement presentation and narrative (currently unspecified) |
| S10 | Zero labour hours; zero parts | Degenerate arithmetic; no divide-by-zero or blank rows |
| S11 | Each of the four outcomes at fixed data | The four-way branch is the *only* difference between outputs |

## 8. Design-authority route

`design/README.md:48` requires that "every deferred UI capability must re-enter
specification, alternatives, independent review, explicit approval, visual
generation and manual visual review before implementation."

Do report templates fall under it? The rule says *UI capability*, and a PDF is
not a Web surface. But `design/README.md:230-240` explicitly owns the renderer
asset boundary and names "Report templates and document stylesheet" as an
approved asset class, alongside the master logo and the supplied engineer
signatures. `design/README.md` is the durable authority for "approved assets,
component and pattern boundaries, and source-to-runtime mappings", and it is
where the excluded-token list lives.

**Verdict: report templates are design-authority artefacts and take the
equivalent route.** The recommended route, in order:

1. **Specification** — this plan plus a per-template page-sequence spec.
2. **Alternatives** — recorded here: one family with four presets (recommended)
   versus four independent templates (rejected: four drifting copies).
3. **Independent review** — an agent that did not write the templates reviews
   them against this plan, per the temp-plan contract.
4. **Explicit approval** — operator approval of wording (blocked today), the
   token additions (§9), and the lineage question (open question 1).
5. **Visual generation** — rendered PDFs from synthetic fixtures plus the
   stress-test suite.
6. **Manual visual review** — page-by-page operator comparison against the four
   sample PDFs.

Steps 4 and 6 cannot be performed by an agent. They are stop conditions.

Routing for the resulting documents: new Markdown is only an ADR or a temp-plan.
The template-family decision and the figure-engine ownership decision are
ADR-shaped; wording and provider selections are operator decisions recorded in
`docs/open-decisions.md`, not new files.

## 9. Colour and typography tokens

Checked against `design/assets/report-renderer/templates/report.css` (note: the
stylesheet is under `templates/`, not at the `report-renderer/` root) and
against `design/README.md`.

| Spec token | Spec value | In `report.css`? | In `design/README.md`? | Conflict? |
| --- | --- | --- | --- | --- |
| Doc red | `#C80A32` | **Yes**, lowercase `#c80a32` throughout | **No.** `design/README.md:72` sets Collision red `#DB0816`, and the excluded-token line explicitly lists "document red" among excluded marketing tokens | **No conflict, but no owner.** Document red is deliberately outside the Web palette, yet its exact hex is recorded nowhere in the design authority. Recommend recording it under the Web/renderer boundary section |
| Charcoal | `#2C2A27` | Yes, once | **Yes** — "Warm charcoal `#2C2A27`", exact match | None |
| Label grey | `#F2F2F2` | Yes | No | Consistent; unrecorded |
| Total-row grey | `#EFEFEF` | **No.** Absent. Nearest existing values are `#f5f5f5`, `#f2f2f2`, `#f4f4f3` | No | **Gap.** A new token is required, and `#EFEFEF` is close enough to the three existing greys to be mistaken for them. Needs explicit approval, not silent addition |
| Grid grey | `#BEBEBE` | Yes | No | Consistent; unrecorded |
| Body face | Arial / Liberation Sans | Partly — `report.css` declares `Arial, Helvetica, sans-serif`; **Liberation Sans is not named in the stylesheet**, it is supplied by the container | `design/README.md:88-96` mandates the system stack for *application* text and separates document faces | **No conflict** — document faces are separately owned. But the document body face is not recorded as an approved token, and the Arial→Liberation substitution is a cross-platform determinism risk for §7 |

Two further additions the spec requires that `report.css` has no rule for at
all: the **status badges** and the **four figure tiles** with a red settlement
tile. Both are new visual components, not restyles, and both need design
approval before implementation. Nothing here is chosen silently; the gaps are
reported for decision.

## 10. Staged delivery

Sequencing constraint, verbatim from `docs/requirements.md:53-54`: "accepted
`CASE-31`, `ENG-01`, and `ENG-02` data/workflow precede `EXT-08` and
`RPT-01`–`RPT-05` rendering." None of `CASE-31`, `ENG-01`, or `ENG-02` is built.
The current release is `0.1.0-alpha.1`. Every stage below is therefore
preparatory.

| Stage | Content | Advances | Does **not** advance | Remains unproved |
| --- | --- | --- | --- | --- |
| A | This plan; the lineage decision (open question 1); the template-family ADR; recording the token gaps | Nothing | Every capability | Everything |
| B | Core-owned figure engine (§3) with `decimal` + `MidpointRounding.AwayFromZero`, unit-tested against the §3 vector; move the existing fee-note computation out of `HtmlComposer` | Groundwork for `RPT-01` | `RPT-01` itself; `RPT-02`; `EXT-08` | Any caller; any accepted input |
| C | Composition engine (§4) with every blocked string behind a review-gated placeholder registry; validation rules (§5) as a `Validators.cs` case | Groundwork for `RPT-01` | Issuance of any report — seven of fifteen composed elements are blocked | Wording acceptance |
| D | `AssessmentReportDocument`, `assessment_report.scriban`, four `TemplateDescriptor` presets, `AuthoringCatalog` entries, new CSS components, page-scoped fee-note furniture | Groundwork for `RPT-02` | `RPT-02` acceptance; `RPT-03`/`04`/`05` | Design approval; operator visual review |
| E | Synthetic fixtures, stress-test suite S1–S11, manifest-based visual baseline, one-time manual comparison against the four sample PDFs | Verification only | Any capability | Operator acceptance of the comparison |
| F | *Blocked.* Core render contract, artifact/version identity and hash, real caller, recovery | `EXT-08`, `RPT-01`–`RPT-02` | — | Blocked on `CASE-31`, `ENG-01`, `ENG-02` and on the workspace activation conditions |

Stages A–E leave the renderer with no Pegasus caller, consistent with its
register row and with `docs/adr/0009`.

## 11. Verification plan

Mapped to the evidence tiers in `docs/operations.md`. Only tiers genuinely
reachable at each stage are claimed.

| Tier | Applies? | Evidence |
| --- | --- | --- |
| 1 Static/build/architecture | **Yes** | Solution builds and tests Release. Pegasus architecture tests must continue to prove no production `ProjectReference` into `workspaces/`, and that no workspace source is runtime-embedded |
| 2 Core/domain | **Yes — the primary tier** | Figure engine: positive, boundary, and tie cases; both VAT modes; negative settlement; zero labour. Composition: every enum branch of N4, N6, N8. Validation: each rule in §5 positive and negative, including the three conditionals absent from the schema and all three negative constraints |
| 3 Parser/adapter contracts | **Partly** | Payload deserialisation through `CrJson.Options`: unknown enum values, missing required sections, supplied-forbidden fields, `LenientStringConverter` behaviour against the new `decimal` fields |
| 4 LocalDB persistence | **No** at these stages | Nothing is persisted until the Core render contract exists |
| 5 Web/API/MCP caller | **No** | No Pegasus caller exists; asserting otherwise would contradict `EXT-08`'s note that "imported renderer source is not activation" |
| 6 Functions/Azurite | **No** | Not applicable |
| 7 Browser/accessibility | **No** | A PDF is not a Web surface. The design-authority route (§8) substitutes manual visual review |
| 8 Genuine corpus | **No — and must stay no** | Real case data must not enter template fixtures or baselines |
| 9 Security/observability | **Partly** | Image-path policy (§5); HTML encoding of every composed value — note composed narrative must be encoded exactly like caller-supplied text; attachment size limits |
| 10 Performance/concurrency | **Partly** | S1/S2 bound render time and page count for the largest realistic report |
| 11 Migration/recovery | **No** at these stages | Template/payload versioning is designed here but has no persisted artifact to migrate |
| 12 Integrated workflow | **No** | Explicitly out of scope; this plan does not activate a capability |

Order, per `docs/operations.md`: policy tests first (tier 2), contract tests
second (tier 3), then build/architecture (tier 1), then the visual regression
sweep (§7) as a change-detection gate rather than an acceptance gate.

## 12. Non-goals, stop conditions, and open questions

### Non-goals

- Activating `EXT-08` or any of `RPT-01`–`RPT-05`.
- Creating a Pegasus caller, project reference, or deployment for the renderer.
- Writing any report wording, salvage paragraph, statement of truth,
  qualification, intro sentence, or mileage sentence.
- Selecting a vehicle-history, valuation, or estimating provider.
- Introducing "blueprint" as a Pegasus concept.
- Adding, renaming, or removing an engineer or a signature key.
- Building `RPT-03` audit, `RPT-04` diminution, or `RPT-05` addendum rendering —
  the reference material contains nothing for them.
- Importing any file from `docs/reference/rendererref1/` into product source,
  fixtures, starters, or baselines.
- Changing the shared footer, letterhead, or `report.css` in a way that alters
  the other eleven templates.
- Manipulating images in the renderer (crop, resize, rotate, reorder).

### Stop conditions

Stop and return to the operator if any of these is reached:

1. Implementation would require inventing any of the seven blocked wordings in
   §4.
2. Implementation would require naming a vehicle-history, valuation, or
   estimating provider.
3. A change to `report.css`, the shared footer, or `HtmlComposer.Shell` would
   alter output for a template other than the four new presets.
4. A regression baseline would need to contain real or sample case data.
5. The `DESIGN_SPEC`-versus-C#-renderer authority question is still open when a
   template body is about to be written.
6. A derivation is about to be placed in the renderer or Infrastructure rather
   than `Pegasus.Core`.

### Open questions for the operator

1. **Which lineage is the accepted design?** `DESIGN_SPEC.md` records an
   approved Python generator that is not in this repository; the workspace
   ADR-0003 rejected Python and the C# `CollisionRenderer` is the successor
   stack, but it never received this job schema. Is `DESIGN_SPEC` the accepted
   design for Pegasus report output, to be re-implemented on the C# family — or
   is it superseded evidence? Everything in this plan is contingent on the
   answer.
2. **Vehicle history check provider.** `DESIGN_SPEC` names Experian AutoCheck as
   the source and marks the check Mandatory. Pegasus has `EXT-01`/`EXT-02` and
   no Experian adapter. Which provider, and what does the report print when the
   check is unavailable?
3. **VIN rule.** The spec asserts a 17-char VIN check in one place and says
   there is no VIN format rule in two others. Which governs?
4. **Recovery and storage charges.** Do they enter the subtotal, the repair
   total, the settlement, or none of them?
5. **Enum display forms.** How does each enum value render in prose and in the
   badge? Specifically `below_average`, `moderate_to_heavy`, `right_rear`,
   `wheel`, and the literal slash in "collision/impact damage".
6. **Negative settlement.** When salvage exceeds the engineer's value, F5 is
   negative. What does the red tile and the settlement sentence say?
7. **Matter line.** The composed prefix is hard-coded for every report. Pegasus
   supports case types that are not road traffic accidents. Is the prefix fixed,
   or case-type-driven?
8. **Superseded template ids.** `total-loss-report` and
   `repairable-contract-repair-report` exist today on the generic expert-report
   family. Are they retired, repointed, or kept alongside the new presets?
9. **Sample-data handling.** The four sample jobs carry a claimant name, a
   principal address, a registration, and a VIN, and are committed under
   `docs/reference/`. Confirm that these files are retained as-is and that no
   derivative (fixture, starter, baseline raster) may carry their values.
10. **Token additions.** Approve or reject the new `#EFEFEF` total-row grey, the
    recording of document red `#C80A32` and the document body face in
    `design/README.md`, and the two new visual components.
11. **The four wording placeholders**, per `docs/open-decisions.md` "Report
    wording". Note additionally that the Category S salvage wording is described
    as "confirmed" but is not reproduced in this repository, so no salvage
    wording exists here at all.
12. **VAT-row label in the registered mode.** The not-registered label is fixed;
    the registered-mode label is unspecified.
