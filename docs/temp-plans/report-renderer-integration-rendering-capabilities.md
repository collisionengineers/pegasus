# Report rendering capabilities — draft supporting plan

This is a **draft supporting plan** for the `report-renderer-integration` task.
It is a planning document only. It activates no capability, writes no report
wording, changes no accepted requirement, allocates no capability ID, and
authorises no caller. It plans the work that follows Stage 1 of
[the seam plan](report-renderer-integration-seam.md) and takes ownership of the
capability outcomes the rest of the plan set leaves unowned.

## What this plan owns, and what it does not

The plan set already covers placement, runtime, desktop removal, MCP,
documentation and the consolidated questions. Two things are unowned:

- the **content** of `RPT-01`–`RPT-05` — what a report is, what computes its
  figures, and what data those figures come from; and
- the **catalogue tail** — six renderer template identifiers that map to no
  Pegasus capability.

This plan owns exactly those. It defers to the seam plan for the Core contract,
artifact identity, determinism, wording gate and adapter shape, and amends that
contract only where a named capability cannot be satisfied without the
amendment; every such amendment is marked and justified.

`report-renderer-integration-templates.md` is superseded as a work plan by
operator decision. Its sections 1 and 6 are cited here as **analysis of the
existing C# renderer**, never as specification. Nothing in this plan derives a
requirement from `docs/reference/rendererref1/DESIGN_SPEC.md`, and nothing is
imported from that directory.

## Durable outcomes this plan is written against

Quoted exactly from [the capability inventory](../capabilities.md). Band and
target are as recorded there; this plan changes neither.

| ID | Band | Target | Durable outcome (exact) |
| --- | --- | --- | --- |
| RPT-01 | Later | 1.1.0 | Deterministic renderer validates accepted data, computes each figure once, and applies the fixed Collision Engineers design |
| RPT-02 | Later | 1.1.0 | Assessment rendering covers four outcome variants and emits the fee note plus itemised repair-specification breakdown |
| RPT-03 | Later | 1.1.0 | Audit rendering preserves conservative and maximised specifications and records their uplift |
| RPT-04 | Later | 1.1.0 | Diminution rendering uses accepted original-case data plus the Engineer-entered percentage |
| RPT-05 | Later | 1.1.0 | Addenda render from accepted case data plus a versioned amendment without retyping the case |
| EXT-08 | Later | 1.1.0 | Activate deterministic report generation from accepted Core-owned data through the approved renderer contract |
| EXT-09 | Later | 1.0.0 | Versioned repair-estimate lines, source versions, approvals, original-versus-assessed comparison, and savings |
| EXT-10 | Later | 1.0.0 | Versioned vehicle-valuation evidence, explicit Engineer acceptance/adjustments/rationale, and revaluation history |
| EXT-11 | Later | 1.2.0 | Versioned fee/invoice and Engineer cost/payment inputs, accounting status, and role-restricted visibility |
| EXT-12 | Later | 1.0.0 | Audatex/PDF repair-estimate ingestion with retained source artifact, mapped version, and variant proof |
| EXT-13 | Later | 1.0.0 | Independently licensed valuation-source adapters that preserve each source observation and version |
| ENG-01 | Later | 1.0.0 | One canonical repair specification with route provenance for Glass's, Audatex PDF, or an approved AI proposal |
| ENG-02 | Later | 1.0.0 | Engineer-owned final value/deductions, outcome, salvage category/value, and roadworthiness/reason drive derived figures and narratives without retyping |
| CASE-31 | Later | 1.0.0 | One accepted structured case/engineering record is the source for every deterministic report, fee note, addendum, query document, invoice input, and statistic |
| CASE-22 | Later | 1.0.0 | Replace EVA inspection and report-preparation work inside Pegasus |
| CASE-23 | Next | 0.4.0 | Post-report query and dispute work on the existing case with retained report/reply-chain evidence and an explicit lifecycle |

Three capability notes bind the design and are quoted because they are routinely
forgotten:

- `EXT-08` — *"Imported renderer source is not activation; versioning,
  correction, caller, validation, recovery, and acceptance remain required."*
- `ENG-02` — *"Only accepted source versions and explicit named-Engineer
  decisions may drive outputs."*
- `CASE-23` — *"State transitions, actors, response proof, due/chaser
  interaction, closure, and dispute resolution remain unresolved."*

[Requirements](../requirements.md) lines 53–54 fix the order:

> accepted `CASE-31`, `ENG-01`, and `ENG-02` data/workflow precede `EXT-08` and
> `RPT-01`–`RPT-05` rendering;

None of `CASE-31`, `ENG-01` or `ENG-02` exists. Nothing in this plan may be read
as permission to build any `RPT-*` outcome ahead of them. The single most useful
product of this plan is therefore the section on what the renderer demands of
those three capabilities.

## Ownership rules applied throughout

| Rule | Source |
| --- | --- |
| `Pegasus.Core` is the single owner of business policy; Infrastructure implements ports and owns no business decision | [architecture](../architecture.md) |
| Future rendering consumes a Core-owned render contract; report policy does not move into Infrastructure or the renderer | [ADR-0009](../adr/0009-adopt-pegasus-monorepo-workspaces.md) |
| Where a capability already owns a concept, this plan defers to it and creates no second owner | [master plan](report-renderer-integration.md) stop conditions |
| Report wording is an open decision; no wording is invented, defaulted, or paraphrased | [open decisions](../open-decisions.md) "Report wording" |

The working boundary between policy and layout, stated once:

- **Policy (Core):** which figures exist, how each is computed, in what order, to
  what precision; which sections a report contains and in what order; which
  wording key fills each prose slot; which accepted source versions an issue
  consumed; what makes a payload invalid.
- **Not policy (Infrastructure and `design/`):** typography, rules, borders, page
  furniture, column widths, page breaks, density, image placement, and the
  mechanics of turning a composed document into PDF bytes.

## The wording position, stated once

[Open decisions](../open-decisions.md) records "Report wording" as open, naming
accepted wording for salvage Categories N, A, B and N/A; recovery and storage;
the final statement of truth; and named qualifications.

Two additions this plan records because they are not obvious from that row:

1. **No salvage wording of any category exists in this repository.** The
   superseded reference material describes the Category S text as "confirmed"
   but does not reproduce it anywhere. So Category S is not a fifth settled
   case; it is a sixth blocked one, and the blocked set is *all* salvage
   categories.
2. **Engineer-authored, case-specific prose is not "report wording."** An
   Engineer's comments on a particular vehicle, or the reason given for an
   amendment, are accepted case data authored by a named human. Standard firm
   wording that appears on every report of a kind is what the open decision
   blocks. The two must not be conflated, or every report becomes blocked
   forever; nor may the first be used to smuggle in the second.

Mechanism, in Core, with no default text anywhere:

- `ReportWordingKey` — a closed, ordered set of slot identifiers, one per
  composed prose element. The key set is policy and may be shipped now.
- `ReportWordingSet` — key → text, supplied by an accepted wording acceptance
  (`ReportWordingAcceptance`, already in the Stage 1 contract) and never compiled
  into source.
- Composition resolves every slot in a report kind's slot list. An unresolved
  slot is a **hard stop**: the render returns `RendererUnavailable` naming the
  missing keys. It never renders an empty paragraph, a placeholder, a key name,
  or an approximation.
- An architecture test asserts that no `ReportWordingKey` has a compiled default
  string.

## Per-capability specification

### RPT-01 — deterministic, validated, computed once, fixed design

**What must be rendered.** RPT-01 renders nothing on its own. It is the substrate
every other RPT capability stands on: the figure set, the payload schema, the
validation gate, and the fixed design. Its acceptance evidence is therefore
produced *through* RPT-02.

**What data it consumes.** A Core input record — not a payload. Core receives
accepted case, specification, valuation and fee data by version identity,
computes, and only then constructs `ReportPayload` and `ReportComputedFigures`.
The renderer never sees the accepted records.

**Which Core policy computes what.** `ReportFigurePolicy` computes every figure.
`ReportComposition` composes every section. Neither is a port; both are pure,
deterministic policy classes following the `ReportArtifactSchema` precedent
already in the Stage 1 contract.

**"Applies the fixed Collision Engineers design."** Two concrete consequences:

- **Density is fixed by policy per report kind, not auto-fitted.** Auto-fit
  re-renders at Normal → Compact → UltraCompact and the chosen density changes
  the bytes. An issued artifact must be reproducible, so `ReportKind` binds one
  density and auto-fit is excluded from the issued path. Auto-fit may remain on
  the preview seam, which produces no artifact.
- **The design is `design/`-owned and version-pinned.** `ReportTemplateBinding`
  already carries `TemplateVersion` and `TemplateSha256`; extend the observed
  hash to cover `report.css`, because a stylesheet edit changes every issued
  report's appearance while leaving the body template hash untouched.

**What is blocked.** CASE-31/ENG-01/ENG-02; the wording set; the production
execution question (seam risks R1/R2/R3); and the Linux font question, which
makes byte determinism conditional on the deployed image.

### RPT-02 — four outcome variants, fee note, itemised breakdown

**The four variants are not enumerated by any accepted Pegasus source.** This is
the first thing RPT-02 needs and does not have.
[Requirements](../requirements.md) names Assessment as `Repairable` or `Total
loss` — two values, not four. The renderer catalogue carries `total-loss-report`
and a conflated `repairable-contract-repair-report`. The superseded reference
material implies Total loss, Repairable, Cash in lieu and Contract repair, and is
evidence only.

`ENG-02` owns outcome. RPT-02 renders whatever closed set ENG-02 accepts.
**RPT-02's durable outcome is unsatisfiable unless ENG-02's outcome enum has
exactly four members**, and naming them is an operator decision, not a rendering
decision. See open question 1.

**What must be rendered.** Per variant: the document title and heading; the
settlement or outcome statement and its figure; the vehicle and reference block;
the incident and inspection block; the itemised repair specification; the
valuation figures; salvage where applicable; roadworthiness where applicable;
photographs; the statement of truth; and the signature block. Every *label and
sentence* is a wording slot. Every *figure* comes from `ReportComputedFigures`.
Every *structure* is composed by Core.

**What data it consumes.**

| Concept | Owner | Consumed as |
| --- | --- | --- |
| Case, parties, vehicle, dates, references | CASE-31 | Snapshot version identity |
| Repair specification lines and raw cost components | ENG-01 (routes), EXT-12 (Audatex ingestion) | Specification version identity |
| Outcome, salvage, roadworthiness, final values | ENG-02 | Decision version identities |
| Valuation figures behind the accepted values | EXT-10 acceptance; EXT-13 sources | Version identity only; the report never names a valuation source |
| Inspection mode and address | [ADR-0018](../adr/0018-provider-inspection-mode-database-setting.md) | Resolved value; the renderer offers no control and derives no mode |
| Fee lines, rate and payment details | EXT-11 | Fee version identity |
| Photographs | DOC-02/DOC-03 custody | `ReportAttachmentReference` by document-version identity, never a path |

**"Emits the fee note" — as a separate issued artifact.** Recommended, with
reasons: `MAIL-17` speaks of "report/fee-note" send as two things;
[requirements](../requirements.md) states *"A correction does not silently alter
an issued fee note or invoice"*, which is only enforceable if the fee note has
its own immutable issue identity; `EXT-11` versions fee inputs independently on a
later target (1.2.0) than the report (1.1.0); and the existing catalogue already
has a standalone `fee-note` descriptor with its own model. So RPT-02 issues two
artifacts from one accepted data set, each with its own `ReportIssueId`, sharing
one `ReportComputedFigures` set so they cannot disagree. The superseded
material's in-report fee page is the rejected alternative and is recorded as such.

**"Itemised repair-specification breakdown."** ENG-01 owns the specification;
RPT-02 owns only its presentation:

- lines render in ENG-01's accepted order, by stable line number; the renderer
  never sorts, merges, deduplicates or rewrites a description;
- lines group by ENG-01's accepted category; the category set is ENG-01's;
- whether per-line prices, labour hours and part numbers print is a **commercial
  disclosure decision**, not a layout decision, and is open question 4. Core
  emits or suppresses them; the template does not choose.

**Which template family serves it.** `ExpertReportDocument` plus
`expert_report.scriban`, with one `TemplateDescriptor` per outcome variant. The
existing `total-loss-report` is retained; `repairable-contract-repair-report` is
split, because one identifier cannot carry two file-name suffixes, two titles and
two settlement statements.

### RPT-03 — audit rendering

Treated in full below, because it starts from nothing: no template, no data
model, no accepted definition of "conservative", "maximised" or "uplift" anywhere
in the repository.

### RPT-04 — diminution rendering

**A document-identity conflict comes first.** The existing descriptor is
`diminution-rebuttal`: *"Letter-style rebuttal of a third-party diminution-in-
value claim"* — a document that argues a claimed diminution figure **down**.
RPT-04's durable outcome describes rendering that *uses* "the Engineer-entered
percentage" — which reads as asserting a diminution figure. These are opposite
documents with opposite arithmetic. The percentage input exists in neither. See
open question 8.

**A case-type conflict comes second.** `CASE-05 Diminution cases` is
`Later / 0.5.0`, allocation only, and [requirements](../requirements.md) states
plainly that *"Diminution and Commercial remain deferred… They are not active
alpha aliases or generic case types."* RPT-04 cannot render a case type that does
not exist. Whether the diminution report is issued on the original case or on a
linked Diminution case is a CASE-05 decision, and it changes what "accepted
original-case data" means.

**What must be rendered.** The subject vehicle and original-case identity; the
accepted basis value and where it came from by version; the Engineer-entered
percentage with the named Engineer who entered it; the resulting amount; the
Engineer's reasoning; the statement of truth; the signature. All standard prose
slots blocked.

**Which Core policy computes what.** `ReportFigurePolicy` emits
`diminution.baseValue`, `diminution.basisKey`, `diminution.percentage` and
`diminution.amount`; nothing else derives a diminution figure anywhere.

**What is blocked.** CASE-05; the ENG-02 percentage field; the document identity;
all wording.

### RPT-05 — addenda from accepted case data plus a versioned amendment

Treated in full below. The seam plan gives RPT-05 issue *identity*
(`ReportIssueKind.Addendum`, `SupersededIssueId`, `NextIssueVersion`,
`EnsureIssueIsNew`) and deliberately omits the lifecycle because CASE-23 is
unresolved. It does not give RPT-05 the words "without retyping the case", which
are the whole capability.

## The figure engine in `Pegasus.Core`

### Placement and shape

New file `src/Pegasus.Core/Reports/ReportFigurePolicy.cs`. Static, pure, no port,
no I/O, no clock, no culture ambient state. Its single entry point takes a typed
Core input record and returns the `ReportComputedFigures` the Stage 1 contract
already defines.

Three non-negotiable properties:

- **`decimal` throughout, never `double`.** `double` cannot represent `0.20`
  exactly and produces penny drift that varies by input. An architecture test
  forbids `double` and `float` in `src/Pegasus.Core/Reports/`.
- **Each figure is computed once, in a fixed order, and later figures derive from
  the already-rounded earlier value.** Sum-of-rounded and rounded-of-sum differ,
  and a report whose printed rows do not add up is worse than one that fails to
  render.
- **The figure set is an ordered list, never a dictionary iteration.** The
  contract's `IReadOnlyList<ReportFigure>` already gives this; the policy must
  preserve declaration order so two runs emit the same order.

### Rounding

Money and money-derived figures round to 2 decimal places, ties away from zero:

```
decimal.Round(value, 2, MidpointRounding.AwayFromZero)
```

.NET's `MidpointRounding.AwayFromZero` is the equivalent of ROUND_HALF_UP, and
the two agree for both signs, so a figure that can go negative (a settlement
where salvage exceeds the accepted value; a downward audit adjustment) rounds
correctly without a sign special case.

| Quantity | Scale | Rounding | Status |
| --- | --- | --- | --- |
| Money | 2 dp | AwayFromZero | Recommended; matches existing fee-note behaviour |
| Labour hours | 2 dp | AwayFromZero | Recommended; ENG-01 supplies hours, Core does not invent precision |
| Rate per hour | 2 dp | AwayFromZero | Recommended |
| Percentage (diminution, uplift) | undecided | AwayFromZero | **Open question 9** — Core must not pick silently |

Core **rejects** an input whose money component carries more than 2 decimal
places rather than silently rounding it. Silent rounding of an accepted input
would make the report disagree with the record it came from.

### Figure keys and formulas

Keys are a closed, versioned namespace owned by Core. `PolicyKey` and
`PolicyVersion` on `ReportComputedFigures` already exist for this; a change to
any formula or key increments `PolicyVersion` and is therefore attributable on
every artifact that used it.

| # | Key | Formula | Rounding | Inputs owned by |
| --- | --- | --- | --- | --- |
| F1 | `assessment.labour.total` | `labourHours × labourRate` | 2 dp | ENG-01 |
| F2 | `assessment.subtotal` | `F1 + parts + paintAndMaterials + specialistOther` | exact over 2 dp operands | ENG-01 |
| F3 | `assessment.vat` | `vatRate × F2` | 2 dp | EXT-11 supplies the rate version |
| F4 | `assessment.repairTotal` | `F2 + F3` | exact | — |
| F5 | `assessment.settlement` | outcome-dependent; for a total-loss outcome `engineerValue − salvageValue` | 2 dp | ENG-02 |
| F6 | `fee.subtotal` | `Σ feeLine.amount` over 2 dp lines | exact | EXT-11 |
| F7 | `fee.vat` | `feeVatRate × F6` | 2 dp | EXT-11 |
| F8 | `fee.total` | `F6 + F7` | exact | — |
| F9 | `diminution.amount` | `diminution.baseValue × diminution.percentage ÷ 100` | 2 dp | ENG-02 |
| F10 | `audit.delta.*` | see the audit section | 2 dp | EXT-09 |

Pass-through figures — `assessment.parts`, `assessment.paintAndMaterials`,
`assessment.specialistOther`, `assessment.engineerValue`,
`assessment.retailValue`, `assessment.tradeValue`, `assessment.salvageValue`,
`assessment.recovery`, `assessment.storage`, `diminution.baseValue` — are emitted
as figures too, even though they are not derived. This is deliberate: it means
the renderer receives every printed money value through one mechanism and can be
forbidden from reading money out of the payload at all.

**Recovery and storage do not enter any total.** They are emitted as their own
figures and folded into no subtotal, no repair total and no settlement until an
owner decides otherwise. A total that silently includes them cannot be reconciled
against the specification it came from. Open question 5.

**The two-mode VAT rule is not adopted.** The superseded reference material
describes a second VAT calculation for a non-VAT-registered repairer. That is
DESIGN_SPEC-only material with no owner in this repository, and adopting it would
create a repairer-VAT-status concept that nothing supplies. If it is a real
business rule it belongs to EXT-11 or ENG-01. Open question 6.

**The VAT rate is versioned policy data, not a payload field.** Today
`FeeNoteDocument.VatRate` defaults to `0.20m` and is caller-supplied, so a
reissued fee note would silently adopt today's rate. The rate must be recorded on
the accepted fee version so a reissue reproduces the original figure.

### The figure/payload separation, and contract amendments

`ReportFigure` is `(string Key, string Value)` in the Stage 1 contract, with
`Value` the Core-formatted presentation string. Two additive amendments are
proposed:

1. **`ReportFigureKind`** on `ReportFigure` — `Money`, `Percentage`, `Hours`,
   `Count`, `Date`, `Text`. Presentation *metadata*, not policy. Without it the
   template must infer format class from the key name, which is exactly the
   implicit coupling that drifts.
2. **`FiguresSha256`** on `RenderedReportArtifact`, beside the existing
   `FiguresPolicyKey` and `FiguresPolicyVersion`. The payload is hashed; the
   figures are not. A figure-policy defect that produced a wrong number under a
   correct policy version is currently unattributable from the artifact record.

A third amendment is required by RPT-03 and RPT-05: **`ReportSourceVersions`** on
`RenderedReportArtifact`.

### Removing the `HtmlComposer.FeeNote` duplication

`workspaces/report-renderer/src/CollisionRenderer.Core/Templating/HtmlComposer.cs:122-125`
computes subtotal, VAT and total inside the renderer:

```csharp
var subtotal = m.Items.Sum(i => i.Amount);
var vat = decimal.Round(subtotal * m.VatRate, 2, MidpointRounding.AwayFromZero);
var total = subtotal + vat;
```

This is the repository's only report arithmetic, and it sits on the wrong side of
[ADR-0009](../adr/0009-adopt-pegasus-monorepo-workspaces.md)'s *"report policy
does not move into Infrastructure or the renderer"*. Left as it is, integration
ships two owners of one VAT rule. Resolution, in order:

1. **Move the arithmetic, preserving the behaviour.** F6–F8 reproduce these three
   lines exactly, so the move can be proved by an equality test against the
   current implementation before it is deleted.
2. **Delete lines 122–125** and bind `subtotal`, `vat` and `total` in the Scriban
   context from `fee.subtotal`, `fee.vat` and `fee.total`.
3. **Delete `HtmlComposer.FormatPercent` (line 493).** The VAT rate label is a
   composed presentation of a policy value, emitted as `fee.vatRate` with
   `ReportFigureKind.Percentage`.
4. **Remove `VatRate` from `FeeNoteDocument`.** A payload that can carry a rate
   is a payload that can disagree with the accepted fee version.
5. **Negative validation.** Core rejects a payload whose JSON contains any key in
   the computed-figure namespace, returning `PayloadRejected` and naming the
   offending paths. "Computed once" is enforced by making a second copy
   unrepresentable, not by hoping no one supplies one.
6. **Guard it.** An architecture test asserts that
   `src/Pegasus.Infrastructure/Reports/**` contains no `decimal.Round`, no
   `MidpointRounding`, and no arithmetic operator applied to a `decimal`. A
   second test asserts the standalone fee note and the report's fee figures
   resolve from the same figure keys.

### The payload schema, and where the document model lives

If Core composes the document, Core owns the document *schema*. Two options:

- **(a) Promote the block vocabulary into Core** as `ReportDocumentSchema` — the
  section/block records plus the four family models. Infrastructure depends on
  Core, so the adapter deserialises straight into Core types and no mirror
  exists.
- **(b) Keep `Models/Documents.cs` internal to Infrastructure** as the Stage 1
  plan places it, and have Core own a parallel payload schema.

**Recommend (a).** Option (b) creates two record sets describing one wire
contract, which is the drift failure the single-owner rule exists to prevent. The
trade-off is honest: the block vocabulary is arguably layout, and promoting it
puts a presentational vocabulary in Core. The counter is that it is the payload
contract, and `EvaBundleSchema` is already the precedent for a Core-owned wire
schema. This changes a Stage 1 placement decision. Open question 2.

## The RPT-03 audit design

### What exists, and what does not

What the repository does establish:

- **Audit is a real, reserved case type.** *"another engineering firm has already
  inspected the vehicle; Collision Engineers accepts that firm's original
  Engineer report and audits or double-checks the work."* Inspection + Audit
  performs a distinct Audit of CE's own inspection inside the same case.
- **Audit has its own reference identity.** A standalone Audit derives lowercase
  `a.` or `ap.` *"only from an unambiguous repairable or total-loss assessment in
  the original Engineer report"*.
- **The Audit has its own evidence and acceptance boundary**, and its Box folder
  nests beneath the parent Inspection folder ([operator notes](../operator-notes.md)).
- **`MI-01` consumes "Audit uplift"** as a management measure.
- **`EXT-09` owns "original-versus-assessed comparison, and savings."**

What does not exist anywhere: any definition of "conservative specification",
"maximised specification", or "uplift"; any template; any data model; any worked
example. RPT-03 is the largest gap in the plan set and this section is a *shape*,
not a specification of meaning.

### The ownership chain — no second owner

`EXT-09` already owns *"original-versus-assessed comparison, and savings"*.
RPT-03 must not become a second comparison engine.

| Concern | Owner | RPT-03's part |
| --- | --- | --- |
| Holding two or more repair specifications with route provenance | ENG-01 | Consumes by version identity |
| Comparing original against assessed, and computing savings | EXT-09 | Consumes the accepted comparison |
| Deciding which comparison is authoritative and why | ENG-02 | Consumes the decision |
| Computing the printed uplift figures once | Core `ReportFigurePolicy` | Owns |
| Rendering the pair and the uplift; recording it on the issue | RPT-03 | Owns |
| Aggregating uplift across Engineers and periods | MI-01 | Consumes accepted report events |

### The ENG-01 multiplicity conflict

`ENG-01`'s durable outcome is *"**One** canonical repair specification with route
provenance"*. RPT-03's is *"preserves **conservative and maximised**
specifications"*. Read literally these contradict. Two resolutions:

- **(a) Role is part of specification identity.** ENG-01 holds one canonical
  specification *per role* per case — `Original`, `Conservative`, `Maximised` —
  each with its own version chain and route provenance. "One canonical" then
  means "one current accepted version per role".
- **(b) The pair is an audit-specific record** owned by ENG-02 or CASE-31,
  leaving ENG-01 strictly single.

**Recommend (a)**, because the conservative and maximised specifications are
repair specifications in every respect. Open question 10, and it is a change to
what ENG-01 must build, so it must be answered before ENG-01 is designed.

### Preserving the pair

"Preserves" is stronger than "renders". Three obligations:

1. **The issue names both specification versions.** This requires the third
   contract amendment: `ReportSourceVersions` on `RenderedReportArtifact` — an
   ordered list of `(SourceKind, SourceId, SourceVersion)` triples covering the
   case snapshot, every specification role consumed, the valuation acceptance,
   the fee version, the wording key and version, and the figure policy version.
2. **Those versions are retention-pinned.** Once an issue names a version, that
   version is retained unchanged.
3. **The rendered document reproduces both specifications in full**, not a
   summary. A summary is not preservation.

### Uplift — three deltas, and only the operator can say which is *the* uplift

| Key | Formula | Meaning |
| --- | --- | --- |
| `audit.original.repairTotal` | F4 over the Original specification | The audited firm's position |
| `audit.conservative.repairTotal` | F4 over the Conservative specification | — |
| `audit.maximised.repairTotal` | F4 over the Maximised specification | — |
| `audit.delta.originalToConservative` | `conservative − original` | 2 dp, may be negative |
| `audit.delta.originalToMaximised` | `maximised − original` | 2 dp, may be negative |
| `audit.delta.conservativeToMaximised` | `maximised − conservative` | 2 dp, the width of the defensible range |
| `audit.uplift.amount` | the delta selected by the accepted uplift definition | 2 dp |
| `audit.uplift.basisKey` | which delta was selected | Makes the choice attributable |
| `audit.uplift.percentage` | `uplift.amount ÷ named denominator × 100` | Denominator must be named, never assumed |

Two properties that must not be lost:

- **Uplift can be negative.** An audit that reduces the original firm's figure is
  a correct and valuable outcome. Nothing in the model, arithmetic, wording slots
  or MI-01 aggregation may assume a positive value, and a percentage with a zero
  denominator must be emitted as absent, not as zero or infinity.
- **"Records" means persists, not prints.** A number in a PDF is not a record.
  The uplift is persisted with the report issue and emitted as an accepted event
  carrying the issue identity, the three deltas, the selected basis, the two
  specification versions and the deciding Engineer. MI-01 aggregates from that,
  never from rendered text.

### The audit template family

**What model and body serve it.** `ExpertReportDocument` +
`expert_report.scriban`, with **no new `.scriban` file required**. This is a real
finding rather than a convenience: the existing `evidencetable` block is fully
generic — columns with header, alignment and optional width, and rows of strings
— so a three-way comparison table is already expressible, and `valuebox` already
expresses a highlighted uplift figure.

Two things the block vocabulary genuinely cannot express:

- **Row alignment across specifications with different line counts.** Core aligns
  the rows and emits blank cells; the template receives a rectangular table.
  Alignment requires a `ComparisonKey` per specification line. Where no key
  exists, Core renders the specifications sequentially rather than side by side,
  and says so in a composed sentence rather than silently mis-aligning.
- **Cell-level emphasis on a changed line.** Either Core encodes the difference
  as an explicit column value, needing no template change; or an emphasis field
  is added to the evidence-table rows, a genuine renderer change. **Recommend the
  first**, because it keeps the decision about what counts as a material
  difference in Core.

**File naming.** `ReportArtifactSchema.FileName` slugs the case reference. The
audit reference is the parent reference with a lowercase `a.` or `ap.` prefix
form, so slugging must preserve the distinction between the Inspection artifact
and the Audit artifact of the same case. A slug that collapses `.` and case would
produce two artifacts with one file name in one Box folder tree.

**What is blocked.** The meaning of conservative and maximised; the uplift
definition and its denominator; ENG-01 multiplicity; EXT-09's comparison
contract; all wording; whether the audited firm may be named in the document.

## The RPT-05 data-reuse design

### The mechanism: recompose from pinned source versions

- **(a) Recompose from source versions — recommended.** The base issue records
  `ReportSourceVersions`. An addendum re-runs Core composition against *those
  same versions*, plus a typed amendment, plus any source versions the amendment
  deliberately re-pins. The Engineer supplies only the amendment. "Without
  retyping the case" is satisfied structurally, and the result is reproducible: a
  later unrelated change to case data cannot silently alter the addendum.
- **(b) Copy the base payload JSON and patch it — rejected.** The payload is a
  rendering artefact, not a record. Patching it makes the addendum's content
  depend on a serialisation rather than on accepted data, and puts a merge
  algorithm in the render path.

Option (a) is why `ReportSourceVersions` is a contract amendment this plan
insists on rather than proposes: RPT-05's durable outcome is not implementable
without it.

### `ReportAmendment` — the versioned amendment

| Field | Shape | Why |
| --- | --- | --- |
| `AmendmentId` | Guid | Durable identity independent of the issue it produces |
| `AmendmentVersion` | int, monotonic from 1 | "a versioned amendment", verbatim from the outcome |
| `BaseReportId`, `BaseReportIssueId`, `BaseIssueVersion` | Guid, Guid, int | The addendum is a new issue under the same `ReportId` |
| `Reason` | required text | Engineer-authored, case-specific; not blocked wording |
| `Changes` | ordered list of `ReportAmendmentChange` | See below |
| `RepinnedSources` | list of `(SourceKind, SourceId, SourceVersion)` | Everything unlisted is carried forward unchanged |
| `AuthorisedBy` | named Engineer identity | ENG-02's "explicit named-Engineer decisions" |
| `AuthorisedAtUtc` | `DateTimeOffset` | Permanent history |

`ReportAmendmentChange` carries `TargetKey`, `Before` and `After` as structured
values, and a flag for whether the change alters a figure input. This satisfies
[requirements](../requirements.md)' *"structured before/after values"*, and it is
what lets the rendered addendum show what changed without an Engineer retyping
it.

### What RPT-05 needs from the unresolved CASE-23 lifecycle

| Concern | Owner | State |
| --- | --- | --- |
| Addendum issue identity, monotonic version, no-overwrite | Seam plan, Stage 1 | Specified |
| Data reuse from pinned source versions | This plan | Specifiable now |
| The amendment record and its before/after | This plan | Specifiable now |
| Reasoned reopen before revising a closed case's report | [Requirements](../requirements.md), already accepted | Specified |
| What *triggers* an addendum obligation from a received query | CASE-23 | **Unresolved** |
| Which actors may raise, review and authorise one | CASE-23 | **Unresolved** |
| Due-work and chaser interaction | CASE-23 | **Unresolved** |
| Response proof and closure of the post-report work | CASE-23 | **Unresolved** |
| Dispute resolution | CASE-23 | **Unresolved** |

**RPT-05's rendering half can be built and locally verified once
CASE-31/ENG-01/ENG-02 exist; RPT-05 cannot be accepted until CASE-23 resolves**,
because there is no accepted definition of the work the addendum completes.

`ReportIssueKind.Correction` gets **no** lifecycle in this plan, for the same
reason the seam plan withheld one. An addendum adds; a correction supersedes; the
difference is a CASE-23 decision.

## Unallocated-template dispositions

Every allocation below is a **proposal requiring an operator decision**. IDs are
illustrative next-in-sequence values; retired IDs are never reused, and adding
rows changes the inventory's stated totals and per-target counts. Those
mechanical consequences are part of the decision.

| Template ID | Disposition | Proposed ID / band / target | Reasoning |
| --- | --- | --- | --- |
| `market-valuation-evidence` | **Allocate** | `RPT-06`, Later, 1.1.0 | It renders EXT-10's versioned valuation evidence as an issued document, from sources EXT-13 owns. It is issued standalone to a principal and therefore needs its own issue identity, artifact hash and correction path, which folding it into RPT-02 would deny it |
| `advert-evidence-pack` | **Allocate separately** | `RPT-07`, Later, 1.1.0 | Companion to RPT-06, given its own row deliberately: it reproduces third-party advertiser screenshots and appends captured advertiser PDFs, so it carries a third-party-content licensing and retention question that RPT-06 does not. A separate ID lets that question block the pack without blocking the valuation document |
| `blank-letterhead` | **Retire from the issued-report catalogue** | — | It produces a free-text document with no accepted case data, no computed figure, no validation and nothing for the wording gate to gate — the inverse of RPT-01's outcome and of *"Reports are produced from accepted case facts and source-labelled evidence"*. If ad-hoc letterhead correspondence is needed it belongs to correspondence (`MAIL-12`). Open question 12 |
| `roadworthy-criminal-report` | **Allocate the roadworthiness half; do not carry the conflated name** | `RPT-08`, Later, 1.1.0 | Roadworthiness has a real owner and real data: ENG-02 owns *"roadworthiness/reason"*, and requirements make `Roadworthy`/`Unroadworthy` a distinct professional finding never derived from the assessment. The "criminal" half is not: no Pegasus case type covers instructed criminal-matter work. Open question 13 |
| `part-35-response` | **Allocate** | `RPT-09`, Later, 1.2.0 | Part 35 of the Civil Procedure Rules governs expert evidence, and written answers to a schedule of questions to the expert are post-report dispute work. CASE-23 owns the *lifecycle*; the rendering row belongs in the post-report cluster at 1.2.0 alongside `MAIL-17`, `EXT-11` and `MI-02`/`MI-03` |
| `response-letter` | **Retire; fold the need into `RPT-09`** | — | It produces the same artefact class as `part-35-response` from the same data, on the same model and body. Two descriptors with one owner is drift risk with no offsetting benefit |

Note on `expert-report`: it is better described as the **shared model and body**
behind most descriptors than as a report kind in its own right; whether it
survives as a user-selectable descriptor is open question 14.

## What the renderer demands of CASE-31, ENG-01 and ENG-02

This section is written for the CASE-31/ENG-01/ENG-02 work, not for the renderer
work. Three rules apply to every row:

- **Everything is reachable by version identity.** A value that cannot be named
  by version cannot appear on an issued artifact.
- **No derived figure is ever supplied.** These capabilities supply raw
  components and decisions. Core derives. A supplied total is a rejected payload.
- **No free-text retype.** A field an Engineer types into the report, rather than
  into the record, defeats CASE-31's *"one accepted structured case/engineering
  record"* and ENG-02's *"without retyping"*.

### CASE-31 — the accepted structured case/engineering record

| Field | Shape | If absent |
| --- | --- | --- |
| `CaseId` | Guid | Cannot render |
| `CaseReference` including the lowercase `a.` / `ap.` audit form | string | Cannot render; the slug must keep Audit and Inspection artifacts distinct |
| `CaseType` | closed enum: Inspection, Standalone Audit, Inspection + Audit | Cannot select a family |
| `CaseSnapshotId` + `SnapshotVersion` | Guid + monotonic int | RPT-05 is not implementable |
| Principal identity + snapshotted name and ordered address lines (≥ 1) | Guid + string + list | `AcceptedDataIncomplete` |
| `YourRef` | optional string | Row omitted |
| Claimant / insured party name, snapshotted | string | Blocked slot cannot resolve |
| Incident date; instructions-received date; assessed date | `DateOnly` each | `AcceptedDataIncomplete` |
| Vehicle: registration, make, model, derivative, body type, fuel, transmission, engine, first-registered, colour | strings / `DateOnly` | Rendered as an explicit "not stated" only where policy allows |
| VIN | optional string | No format rule is asserted by this plan; trailers and cycles may have none |
| Odometer miles + `MileageSource` closed enum + source label | int + enum + string | The mileage sentence cannot be composed |
| Inspection mode and address, already resolved per [ADR-0018](../adr/0018-provider-inspection-mode-database-setting.md) | the literal `Image Based Assessment`, or a confirmed physical address | Fails closed |
| Vehicle-history-check result **or** its recorded exception | `(Source, Result, ObservedAtUtc)` or `(ExceptionReason, Actor, AtUtc)` | Fails closed; the report never names a provider it was not given |
| Ordered photograph references | list of `(DocumentId, DocumentVersionId, Sha256, ContentLength, MediaType)` | Renders without photographs only if policy permits |
| Fee note number / matter reference | string | Fee note cannot be issued |

Two demands that are easy to miss:

- **Exactly one authoritative field per date.** The superseded reference material
  carried the incident date twice, in two sections, with two different format
  rules. CASE-31 must not reproduce that.
- **Snapshot, not live read.** Parties and addresses are snapshotted at issue; a
  reissue reads the snapshot, not the current case.

### ENG-01 — the canonical repair specification

| Field | Shape | Note |
| --- | --- | --- |
| `SpecificationId` + `Version` | Guid + monotonic int | Retention-pinned once an issue names it |
| `Role` | closed enum: Original, Conservative, Maximised | **The multiplicity conflict; open question 10** |
| `Route` + route provenance | enum (Glass's / Audatex PDF / approved AI proposal) + source document version + mapping version + reviewing Engineer + accepted-at | ENG-01's own outcome text |
| Lines: `LineNumber` | int, stable, gap-free | The renderer never sorts |
| Lines: `Category` | closed enum owned by ENG-01 | The renderer does not define categories |
| Lines: `Description` | text exactly as accepted | Never rewritten or truncated by the renderer |
| Lines: `Quantity`, `LabourHours`, `UnitRate`, `Amount` | `decimal`, 2 dp | More than 2 dp is rejected, not rounded |
| Lines: `PartNumber` | optional string | Printing is a disclosure decision (open question 4) |
| Lines: `ComparisonKey` | optional stable key shared across roles | Absent ⇒ Core renders sequentially, not mis-aligned |
| Totals of raw components: `LabourHours`, `LabourRate`, `Parts`, `PaintAndMaterials`, `SpecialistOther` | `decimal`, 2 dp | **Raw only.** A supplied subtotal, VAT or repair total is rejected |
| `Recovery`, `Storage` | `decimal`, 2 dp, optional | Enter no total until decided (open question 5) |

### ENG-02 — the Engineer's decisions

| Field | Shape | Note |
| --- | --- | --- |
| `Outcome` | closed enum, **exactly four members** | The members are unnamed by any accepted source; open question 1 |
| `RoadworthinessFinding` | Roadworthy \| Unroadworthy | Never derived from the assessment; the two findings are independent |
| `UnroadworthyReason` | required text when Unroadworthy | Engineer-authored, not blocked wording |
| `SalvageCategory` | closed enum A \| B \| S \| N \| N/A, plus explicit absence | **Wording for every category is blocked**, including S |
| `SalvageValue` | `decimal`, 2 dp | — |
| `EngineerValue`, `RetailValue`, `TradeValue` | `decimal` 2 dp each, plus the EXT-10 acceptance version each came from | The report names no valuation source |
| Deductions / adjustments and rationale | structured, versioned | ENG-02's own outcome text |
| `DiminutionPercentage` | `decimal` with a stated scale, plus `BasisValueKey` naming which accepted value it applies to, plus reason, deciding Engineer, version | **Does not exist today.** The basis must be named, never assumed |
| `AuditUpliftDecision` | selected delta basis + reasoning + deciding Engineer + version | Consumes EXT-09's comparison; creates no second comparison |
| Engineer identity | one bound record: `EngineerId`, display name, qualifications, `SignatureKey` ∈ the three allowlisted keys, AQP number | Bound as one record so name, qualifications and signature cannot disagree |
| Actor, time and reason on every decision | — | *"Only accepted source versions and explicit named-Engineer decisions may drive outputs"* |

## Staged delivery

### Stage 2A — the Core figure engine

Delivers `ReportFigurePolicy`, the closed figure-key namespace, the rounding
rules, the negative payload validation, and the removal of the
`HtmlComposer.FeeNote` arithmetic and `FormatPercent`.

**Advances:** nothing in the capability register. It removes a single-owner
violation and adds policy that no caller reaches.
**Does NOT advance:** RPT-01 or any other RPT row, EXT-08, MI-01.

### Stage 2B — payload schema and composition mechanism

Delivers the Core-owned document schema (subject to open question 2),
`ReportComposition`, the `ReportWordingKey` set with no default text, the
unresolved-slot hard stop, and the three contract amendments.

**Advances:** nothing. No wording exists, so no report composes to completion.

### Stage 3A — RPT-01 with real data (blocked)

Requires accepted CASE-31, ENG-01 and ENG-02; an accepted wording set; the
production execution decision; report-issue persistence and migration; and the
Web caller with its authorised review action.

**Advances:** `RPT-01` and `EXT-08`, in part.

### Stage 3B — RPT-02

Requires the named four-outcome enum, ENG-01 lines, ENG-02 outcome and values,
EXT-11 fee inputs, and the disclosure decision. **Advances:** `RPT-02`.

### Stage 3C — RPT-03

Requires the accepted definitions of conservative, maximised and uplift; ENG-01
role multiplicity; EXT-09's comparison; the audit descriptors; and the uplift
persistence and event. **Advances:** `RPT-03`. **Does NOT advance:** `MI-01`.

### Stage 3D — RPT-04

Requires CASE-05's activation or an explicit decision that the diminution report
is issued on the original case; the ENG-02 percentage field; and the
document-identity answer. **Advances:** `RPT-04`.

### Stage 3E — RPT-05, rendering half only

**Advances:** `RPT-05` to locally verified, **not** to accepted. Acceptance waits
on CASE-23. Stated as a stage outcome so no later reader mistakes a rendered
addendum for a completed capability.

### Stage 4 — the newly allocated rows, if allocated

`RPT-06`/`RPT-07`, `RPT-08`, `RPT-09`, each with its own data prerequisites and
its own acceptance. Nothing in Stages 2A–3E depends on this stage.

## Verification

Mapped to [required evidence tiers](../operations.md#required-evidence-tiers).

| Check | Tier | Stage | What it proves |
| --- | --- | --- | --- |
| `src/Pegasus.Core/Reports/` contains no `double`/`float`; `src/Pegasus.Infrastructure/Reports/` contains no `decimal.Round`, `MidpointRounding`, or decimal arithmetic | 1 | 2A | One owner of report arithmetic; decimal-only policy |
| No `ReportWordingKey` has a compiled default string; the Infrastructure report tree contains no report prose outside the `design/`-owned templates | 1 | 2B | The open wording decision cannot be bypassed by a default |
| `ReportFigurePolicy`: positive, boundary, negative, contradictory and failure cases per formula — including negative settlement, negative uplift, zero denominator, more-than-2-dp rejection, and midpoint values at `.005` | 2 | 2A | The figure engine's whole contract |
| Figure ordering: two runs over the same input emit the same keys in the same order with the same values | 2 | 2A | "Computed once" and deterministic ordering |
| Payload negative validation: a payload containing any computed-figure key returns `PayloadRejected` naming the paths | 2 | 2A | A second copy of a figure is unrepresentable |
| Equality test: the new `fee.*` figures match the current `HtmlComposer.FeeNote` output across a fee-line matrix, before those lines are deleted | 2 | 2A | The move is a relocation, not a silent behaviour change |
| Composition: every wording slot resolves, or the render returns `RendererUnavailable` naming the missing keys; never an empty paragraph | 2 | 2B | The wording gate behaves as a gate |
| `ReportSourceVersions` completeness | 2 | 2B | RPT-03 preservation and RPT-05 reuse are possible at all |
| Recomposition: an addendum built from pinned versions reproduces the base sections byte-identically where unamended, and differs exactly at the amended keys | 2 | 3E | "Without retyping the case", proved rather than asserted |
| Adapter contract against a fake PDF engine | 3 | 3A | Stable contract codes and deterministic failures without a browser |
| Report-issue and uplift-record persistence, migration, rollback, action-history atomicity | 4 | 3A/3C | The uplift is recorded, not merely printed |
| Authorised staff render action reaches Core; authorisation failure; validation failure; actor in permanent history | 5 | 3A | A real caller |
| Operator presentation of status, validation and failure without implying delivery | 7 | 3A | The accessible-presentation rule |
| Reissue from retained versions after a schema migration reproduces the original artifact hash | 11 | 3A | Immutable artifact identity survives migration |
| Accepted data through Core, persisted issue, artifact in custody, operator view, safe replay | 12 | 3A | Integrated workflow |

Two honest limits:

- **Tier 8 has no direct analogue here.** The genuine-corpus tier is written for
  intake. The equivalent evidence for rendering is a reviewed cohort of real
  historical Collision Engineers reports compared field-by-field against rendered
  output — which requires operator-approved real case data and is a separately
  approved gate.
- **Byte determinism remains conditional on the deployed image.** Until the seam
  plan's font and browser risks are resolved, only the same-machine and
  same-image legs of the determinism matrix can pass.

## Non-goals

- Writing, defaulting, paraphrasing or reconstructing any report wording.
- Deriving any requirement from `docs/reference/rendererref1/`, or importing
  anything from it.
- Building a second owner of repair specifications (ENG-01), estimate comparison
  and savings (EXT-09), valuation acceptance (EXT-10), valuation sources
  (EXT-13), fee and invoice inputs (EXT-11), inspection mode (ADR-0018), or
  engineering decisions (ENG-02).
- Allocating a capability ID, changing a band or target, or editing
  `docs/capabilities.md`. Every allocation here is a proposal.
- Specifying the CASE-23 lifecycle, or any correction state machine.
- Deciding where rendering executes in production.
- Any UI surface.
- Selecting a vehicle-history-check provider, a valuation source, or an
  estimating system.

## Stop conditions

1. A design would require inventing, defaulting or reconstructing wording blocked
   by [open decisions](../open-decisions.md).
2. A design would compute a report figure anywhere other than `Pegasus.Core`, or
   would let a payload carry a derived figure.
3. A design would require `double` or `float` for a money value.
4. A design would create a second owner of a concept an existing capability owns.
5. A stage would advance an RPT capability ahead of accepted CASE-31, ENG-01 and
   ENG-02.
6. A design would name conservative, maximised or uplift with a meaning no
   accepted source supplies.
7. A design would render an artifact from data that cannot be named by version
   identity, making the issue irreproducible.
8. An allocation proposal would change a capability band, target or count without
   an explicit operator decision.

## Open questions

1. **What are the four outcome variants?** RPT-02 names four; requirements name
   two professional Assessment findings; the superseded material implies four.
   ENG-02 must define the closed set, and RPT-02's durable outcome is
   unsatisfiable unless it has exactly four members.
2. **Does the document block vocabulary move into `Pegasus.Core`?** This changes
   a Stage 1 placement decision.
3. **Are the three contract amendments accepted?** `ReportFigureKind`,
   `FiguresSha256`, and `ReportSourceVersions`. The third is not optional.
4. **Do per-line prices, labour hours and part numbers print?** A commercial
   disclosure decision that may differ by principal.
5. **Do recovery and storage charges enter any total?** This plan folds them into
   none.
6. **Is there a second VAT mode for a non-VAT-registered repairer?** It appears
   only in superseded material and has no owner here.
7. **Is the fee note a separate issued artifact, or a page of the report?** This
   plan recommends separate.
8. **Is RPT-04 a rebuttal, an assertion, or both?** And which accepted value is
   the percentage's basis, and on which case is the document issued given that
   CASE-05 is deferred?
9. **What scale and rounding apply to percentages?** Money is settled; percentages
   are not, and Core must not pick silently.
10. **Does ENG-01 hold one canonical specification per role?** Must be answered
    before ENG-01 is designed.
11. **What is "uplift"?** Which delta is authoritative, what denominator does its
    percentage use, and how is a negative uplift treated in the document, the
    record and MI-01's aggregation?
12. **Is `blank-letterhead` retired?**
13. **Does "criminal report" name real instructed work?** If so it needs a case
    type before it can have a rendering capability.
14. **Does `expert-report` survive as a user-selectable descriptor**, or is it
    only the shared model and body behind the named kinds?
15. **May the audited firm be named in an RPT-03 document?**
16. **Can the accepted wording set be supplied, and does it include Category S?**
17. **What is the file-name policy for an Audit artifact** whose reference is the
    parent reference with a lowercase `a.` / `ap.` form?
18. **Are the four proposed capability allocations accepted** — `RPT-06`,
    `RPT-07`, `RPT-08`, `RPT-09` — with the consequent changes to the inventory's
    totals and per-target counts?
