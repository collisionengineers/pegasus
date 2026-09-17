# FRD-06: Vehicle and engineering evidence

> Owner capabilities: INT (image/VRM), ENG · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Every assessment is a desktop assessment. The report address is either a
  real vehicle location someone supplied or confirmed, or the exact words
  `Image Based Assessment`. Pegasus never guesses an address.
- A number plate read from a photo is a suggestion until staff confirm it,
  except that a confident read at the 0.80 bar may register a Vehicle images
  record and pair it with one matching Case automatically.
- DVLA and DVSA lookups fill only empty vehicle fields. They never overwrite
  what the instruction or a staff member entered.
- Roadworthiness and Assessment are separate Engineer findings. A correction
  is a new reasoned version, never an edit of the old one.
- Every repair estimate is an immutable version. Imported or AI material
  stays a Draft until an Engineer accepts it with **Use estimate**.

## Purpose

This document owns the evidence about the vehicle and the engineering
judgement built on it: where the vehicle was inspected, its registration and
identity, external vehicle data, the Engineer's findings, damage, valuation,
settlement and repair estimates. It serves the PRD outcomes for accurate,
source-labelled Case data and a definitive Engineer report.

Grouped images with no usable registration are one Unidentified item, and two
valid registrations are Conflicting identification
([FRD-02](frd-02-intake-and-source-identity.md#unidentified-destination-and-reference),
[FRD-19](frd-19-image-led-intake-and-pairing.md#grouped-image-intake-routing)).

## Vehicle and engineering evidence

Vehicle identity, registration, location, valuation, repair evidence,
roadworthiness, total-loss and salvage information always show where they
came from and can be reviewed.

### Inspection address

**What the report says.** The report records one of two things: the physical
location of the vehicle or repairer, when that location was supplied or
confirmed by a person, or the exact value `Image Based Assessment`. Collision
Engineers does desktop assessments only. The report never claims anyone
attended. The address field is blank, `Image Based Assessment`, or a chosen
physical location with its source recorded.

**Which mode applies.** The Principal record carries an inspection-mode
setting ([ADR-0018](../adr/0018-provider-inspection-mode-database-setting.md)).
Instruction documents never contain the literal value, so the mode is never
read from text.

- For an always-image-based Principal (QDOS is set up this way), Pegasus
  fills `Image Based Assessment` at Case creation even if the instruction
  shows a physical location. Authorised staff may override it on that Case to
  the supplied or confirmed location, with a reason recorded against them.
- For a physical-address Principal, the location is extracted from the
  instruction and confirmed by an operator. The setting picks the default
  mode; it never invents or picks an address.
- Where the source has no address at all, a staff member types the physical
  location at Case creation. It is kept with that person as its source. The
  ban is on Pegasus inferring an address, not on a person stating one.

The provider-domain reference package holds no address and no mode default.
No address is ever inferred from a provider or domain match.

**Reasons and history.** Choosing `Image Based Assessment` by hand, or
overriding the filled mode, needs a staff reason in permanent Case history.
The automatic fill records its provider-setting source and its own permanent
history event.

**Inspect at.** The Case record's Inspection section offers a quick choice:
`Image Based Assessment`, Claimant address, Repairer location, Storage
location, the previous addresses used for this Principal, and Manual entry.
An option whose value is not on the Case is disabled, never shown empty. The
Case stores its storage location as its own field. Choosing an option records
that value under the source and reason rules above. The choice never invents
an address.

**Reference addresses.** When `DATA-02` activates, its separately approved
reference-data pipeline accepts only reviewed full addresses. It keeps each
complete display address with a normalised postcode, keeps operator-confirmed
rows across refreshes, and is deterministic and auditable. Frequency,
recency, proximity, accepted Principal, Repairer, Image Source and normalised
search text may rank suggestions but never choose an address. Nothing here
activates a spreadsheet import, route or caller before its own acceptance
evidence.

### Ordinary-image VRM and image analysis

**The bar.** The accepted recognition threshold is **0.80**. It applies only
under the image-origin and matching conditions below. It is not a mailbox
routing score. [ADR-0019](../adr/0019-in-process-onnx-vrm-recognition.md)
owns the engine choice. The
[recorded cohort and holdout evidence](https://github.com/collisionengineers/pegasus/blob/af1625fae8ac8018054c95e988907f6c44fa4639/docs/operations.md)
keeps its dated results; no fresh evaluation is implied.

**Suggestion first.** Reading a registration from an ordinary vehicle photo
produces a suggestion. Each result stays attached to one retained source
image. Staff confirmation creates the provisional vehicle identity. Before
confirmation a suggestion must not create or identify a Case, allocate a
Case/PO, overwrite a confirmed registration, select an EVA image, satisfy a
readiness gate, or change Case workflow.

**Automatic registration and pairing.** A confident, unambiguous read at the
bar may do two things automatically. It may register a Vehicle images record
and allocate its Image reference. And where exactly one eligible pre-report
Case carries that confirmed registration with no contradictory identity
evidence, it may pair the images with that Case under the matching rules.
Both actions are recorded as system actions and staff can reverse them with a
reason.

**Near-miss rules.** These decide when a read counts as an unambiguous match
to a candidate's confirmed registration:

- A read missing exactly one character of a candidate's confirmed
  registration matches. The confirmed registration completes the read and is
  the registered identity. A truncated read is never registered as its own
  value when a confirmed registration completes it.
- A substituted character is never a match.
- A second consistent candidate makes the read ambiguous, unless the read is
  exactly equal to one candidate's confirmed registration, which is
  unambiguous regardless of other near-miss candidates.
- A read one character longer than the standard seven-character registration
  whose fifth character is a `1` is retried without that character, because
  plate furniture is often read as an inserted `1`. A match found this way
  assumes the confirmed registration is correct.

**Pairing in reverse.** When a new eligible Case is accepted, it pairs with a
waiting unpaired Vehicle images record only on exact equality with the
registered identity. The registered identity is immutable, so the completion
rules above cannot apply after registration. A near-miss in this direction is
a staff suggestion with a reason, never automatic.

A multi-image upload evaluates this rule once for the whole group, not per
image. Group membership, waiting for completion, VRM aggregation and
fail-closed precedence are in
[FRD-19](frd-19-image-led-intake-and-pairing.md#grouped-image-intake-routing).
A readable image keeps a registration-free damage close-up in the same group.
Images with no readable registration, or with conflicting valid
registrations, get no invented Image reference; they go to Unidentified with
the applicable reason, including the Conflicting identification reason.
Editing a Vehicle images record uses the record edit scope in
[FRD-14](frd-14-record-edit-leases.md#record-edit-scopes).

**What the screen shows.** The operator sees the difference between a
suggestion, no readable result or an unknown result, an unavailable
dependency, and a technical failure. An empty value is never shown as
success. Pegasus records the source image, task, engine or provider and
version, time, output, supplied confidence, failure or unknown outcome, and
any later staff decision, separately from confirmed Case data.

**Two layers.** Recognition runs plate detection, then plate reading.
Diagnostics must show which layer ran and which one stopped, without a second
outcome vocabulary and without logging image content or raw candidate text.
"No plate detected" and "plate found but unreadable" are both the single
visible `NoReadableResult` outcome, told apart only by a non-sensitive
code-level diagnostic reason. A recorded outcome is durable: re-evaluating the
same image (a sibling arriving, a replay) reuses it rather than running the
detector or reader again, so one retained image is recognised at most once.

**Separate capabilities.** Ordinary-image VRM reading, Document Intelligence
extraction from scanned PDFs, and wider image or damage AI are different
capabilities. Generated or synthetic vehicle images are not acceptance
evidence. No recogniser, model or adapter acts on its own.

**Every image is kept.** An automated VRM or colour result may only suggest
that a photo shows a different vehicle. It does not exclude the photo from
the Case-vehicle, EVA-export or report-selection pools. An authorised staff
member must confirm that by applying the Third party image tag
([FRD-05](frd-05-documents-extraction-and-custody.md#image-tags)). Until then
the photo stays visible as unmatched-vehicle evidence. Neither outcome
deletes a source image or turns an automated result into Case fact.

**Image readiness advice.** When activated, an AI-assisted readiness check
runs whenever current Case images are added, replaced or removed. It returns
a source- and version-labelled advisory on whether the set has a registration
overview, at least one damage close-up, and a reflected image. An
always-image-based Principal setting waives only the reflection advisory. The
check may run before market valuation and never creates or returns an AI
Proposal. Its result does not affect Case/PO allocation, Case state, Review,
Engineer eligibility, due work, chasing or staff discretion. Source images
stay retained, and report-image selection still excludes images showing a
person's reflection. The advice never selects, excludes, orders or decides
report images. Choosing report images is an Engineer decision in the
report-generation section, not a toggle on the evidence screen.

**Report images are prepared without changing the source.** The retained
bytes and their hashes never change. Every crop or ordering act writes
normalised output beside the source. A report needs two distinct images, one
marked `Close-up` first and one `Overview` second; optional supporting images
follow in the order the operator set. Crop and order data are a normalised,
versioned, attributed record under the same expected-version and edit-lease
rules as other Case changes. An issued report keeps the exact curation
snapshot and source hashes it used, so later changes never alter it.

This section creates no AI caller. Activation still needs accepted model and
transport, data, cost, evaluation, failure and recovery, real-caller and
approval evidence. Wider image or damage analysis and AI-generated repair
specifications stay separate capabilities.

### Vehicle data and MOT enrichment

**What is looked up.** Vehicle identity and specification are required on
every Case. Where the instruction omits vehicle facts, an accepted DVLA/DVSA
caller supplies make, model, manufacture year, engine capacity, fuel type,
available MOT history and mileage observations for the registration. Once
active, DVSA runs for every Case. Until then, approved local replay returns
its preserved result, and with no replay evidence the value is
source-labelled `Unavailable`.

The mileage tiers and discrepancy rule are in
[FRD-02](frd-02-intake-and-source-identity.md#global-vehicle-and-value-checks).

**Every lookup is an observation.** Each lookup or refresh keeps its provider
or source, retrieval time, effective date, source age, response or version
identity, and a typed outcome: current, stale, unavailable, partial or
failed. A refresh creates a new observation. It never silently overwrites a
last-good observation, a confirmed value or a higher-tier mileage. Accepting,
rejecting or linking an external fact goes into permanent history. Routine
calls, retries and polling are content-safe telemetry.

**Look up DVLA & MOT.** The Case record offers one action with that name. A
looked-up value fills Make, Model, Year, Mileage or the derived Vehicle type
directly, as a working value with Lookup provenance.

- Make, Model, Year and Mileage fill only where the field is empty. They
  never overwrite an extracted instruction value or a staff-entered value.
- Vehicle type follows one rule: type approval, then wheelplan, then
  rigid-body revenue weight. L1 and L2 mopeds are `scooter`, other L-class
  vehicles are `motorcycle`, and heavy, PSV or tractor classifications are
  `other`. It fills only where staff have not confirmed a type. A changed
  lookup classification may replace an earlier unconfirmed lookup value; an
  unchanged value is not re-stamped.
- The Lookup value stays unconfirmed until staff Save, which re-stamps it as
  staff-confirmed.
- There are no per-field suggestion chips and no suggestion table.
- The Model comes from the DVSA MOT history vehicle record. DVLA supplies no
  model. Experian stays a disabled seam.

**Automatic lookup at creation.** The same combined lookup also runs when a
Case is created, whether by hand or by an intake acceptance that allocates a
Case. It queues the same external work item the manual action uses, inside
the creation transaction, and only when lookup availability is enabled. This
is an extra trigger, not a replacement for the existing 10-second Worker
sweep, which remains the recovery path if the creation-time attempt fails or
is unavailable. The outcome shows on the Case whichever trigger produced it:
looked up and current, or a stated failure reason, separately from whether
any field was filled. The automatic trigger fills only an empty Make, Model,
Year or Mileage, and an unconfirmed Vehicle type, under the fill rule above.
It never overwrites and never confirms a field.

**A 404 is classified first.** Only a 404 whose body is that provider's own
vehicle-not-found error counts as `NotFound`. Any other 404 (a gateway, route
or withdrawn-subscription 404) is recorded as a failed lookup, not as
"no such vehicle".

**Mileage sentence on the report.** The report's mileage sentence code
(`online_data`, `owner`, `repairer`, `principal`, `average` or `tbc`) comes
from where the Case mileage came from; it is not recorded separately.

| Mileage source | Code |
| --- | --- |
| Extracted from the instruction, including a third-party engineer report supplied with it | `principal` |
| DVSA lookup | `online_data` |
| Staff-entered, with the staff member's own choice recorded beside it in edit mode | `owner`, `repairer` or `principal` |
| No mileage on the Case | `tbc` |

The `average` code is still a renderer sentence, but nothing derives it.
Staff no longer record a separate mileage-source report field.

**Evidence boundary.** The DVLA/DVSA production adapter and its composition
exist. A returned field fills the Case only under the fill rule. Unavailable
fields are stated, never inferred. Credentials, an exact deployed artifact,
real caller and failure evidence, and operator acceptance are separate from
the code being present. Vehicle enrichment does not switch on valuation
behaviour.

### Professional engineering findings and correction

The Collision Engineers Engineer report is definitive for the Case.
Roadworthiness (`Roadworthy` or `Unroadworthy`) and Assessment
(`Repairable` or `Total loss`) are separate professional findings. Neither is
derived from the other, and Triage findings never fill or change either one.

**Corrections.** A correction never edits an accepted or issued finding in
place. It creates a reasoned superseding report, finding or addendum with
actor, time, source, structured before-and-after values, and the earlier
artifact or version kept. Case edits follow the state, role, lease and
version rules in [FRD-13](frd-13-case-lifecycle-and-workflow.md#actions) and
[FRD-14](frd-14-record-edit-leases.md#case-edit-lease). Completed is
reversible and queries follow
[FRD-13](frd-13-case-lifecycle-and-workflow.md#completed-and-query).

**Retained figures.** Betterment figures and estimate `guide` codes recorded
on a source or an estimate version are evidence only. No finding, figure,
outcome, deduction or settlement meaning is derived from them. They are shown
as recorded.

**No money effects.** Triage findings and their corrections have no Case,
report, Audit-reference, fee or invoice effect. Invoicing is deferred
separately: a finding correction must not create, alter, credit or void an
invoice. Any later financial consequence needs the separately accepted,
versioned finance contract.

**AI proposes, people decide.** Automated or AI-assisted extraction may
propose candidate facts, confidence, damage observations, repair operations,
costs, flags, valuation comparables, roadworthiness, total-loss or salvage
evidence only where an allocated capability and accepted evaluation allow
it. `Pegasus.Core` and an authorised person own accepted facts, economics,
findings, outcome, legal use and approval. A skill, prompt, model, workspace,
external schema or imported reference never becomes OEM instruction, repair
policy, valuation authority, legal advice, Engineer approval or product
policy just by existing.

### Damage record

Report readiness no longer has separate Engineer name, qualification or
signature items. The Sign-off Engineer account
([FRD-04](frd-04-parties-accounts-and-access.md#staff-accounts)) is the only
source of the signatory tuple.

A damage record uses 23 detailed regions, with a parent-region map beside the
broad regions. Each region carries a severity and a note; collision work has
no separate damage type. The record also carries tyres and seat belts per
corner, the spare tyre, the centre belt, unrelated damage with its deduction,
and paint or material transfer. `impact_location` and `impact_severity` are
derived from the zone list by `Pegasus.Core`, never typed in. The report
prints the marked diagram
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#assessment-report-outcomes)).

### Valuation sources

Valuation records keep guide month and source. Glass's, Brego and Super CAP
are guide sources. Each is a card whose **Get valuation** asks that source's
connected provider for the month, shows a notice while the source has no
provider, and also lets the figures be typed by hand
([FRD-16](frd-16-case-record-workspace.md#case-workspace)). Cazana is a
disabled seam. AI market research is automation-only. No guide provider is
connected today; connecting one needs its own accepted decision
([ADR-0031](../adr/0031-automation-actor-contract-without-eva-export-tools.md)).

Every entry keeps its date, time, mileage, retail and trade values, plus the
guide month. Glass's valuation and Glass's repair estimating are two systems
and both are used: the valuation source and the estimate import source keep
separate label entries and are never merged. An AI market research entry is
the proposal recorded by the `MarketResearch` job
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#ai-job-list));
it never becomes the Engineer's Value by itself.

**Engineer's Value** is adopted only by an explicit Apply, in this order:
commercial VAT 20%, prior total loss 10% or 20%, fixed additions, then
condition deduction, rounding to whole pounds away from zero. A generic
assessment save never writes the adopted value. This calculation is current
required behaviour. Extra rationale or revaluation-history scope needs its
own accepted contract.

### Settlement

The settlement fields are outcome, category, salvage value, excess,
betterment, claimant VAT registered, reserve, equity (derived), repair days
and delays, report delay, storage per day, recovery, hire start and daily
cost, diminution, and salvage logistics. Equity is derived, never typed in.
Financial ratio lines are allowed, not required; the "no percentage" rule in
[FRD-13](frd-13-case-lifecycle-and-workflow.md#readiness-and-review) applies
only to completeness. Outcome meanings are owned by
[FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#assessment-report-outcomes).

Settlement saves with the Case's single workspace Save. Storage per day and
recovery use the existing typed Inspection members; a lump storage charge is
a separate fact. Repair total and repair days are read from the Current
accepted estimate, and repair days are edited only through Estimate. Equity
uses the report's existing calculation over accepted inputs and is absent
when those inputs are incomplete, never a made-up zero.

### Canonical repair specifications

**One current version.** Every accepted repair specification is an
immutable, versioned Core aggregate. Each Case has exactly one current
accepted version, shared by all of the Case's report projections.

Each version keeps its stable identity, ordered technical lines, source
route, source artifact identity, version and hash, mapping evidence, raw
calculation basis and totals, the selected labour-rate card version if one
was selected, creating actor and time, and, when accepted, the named Engineer
and acceptance time. Glass's, Audatex PDF, an approved AI proposal and manual
entry are provenance routes, never authorities. Imported or automated
material stays a Draft until an authorised Engineer accepts the exact source,
mapping, ordered lines and calculation basis. A Draft without the required
provenance cannot satisfy report readiness. Obsolete development-state
estimates need not be converted or kept.

**Import is keyed by Case plus source hash.** A raw artifact imported through
either caller of the shared import command uses that key. The same Case with
the same hash is a replay that returns the existing Draft. A different
artifact creates the next immutable Draft. The provider and parser are
detected from the registered types; an ambiguous artifact is refused, never
guessed.

**Authority is checked twice.** Before reading a replay or parsing the source,
the command proves the typed actor, the current persisted Case version, and
the edit lease holder, token and expiry. Both that check and the final save
need an assessment-writable state: Not ready, Review or With Engineer, where
With Engineer covers before and after the report
([FRD-13](frd-13-case-lifecycle-and-workflow.md#states-and-labels)). A file
occurrence must name the exact confirmed, non-removed document version; a
correctly paired historical version is still valid evidence. The new Draft is
guarded again in the save transaction. Importing never confirms rows or
changes Current, even when an Engineer started it. The Engineer's
**Use estimate** action confirms and accepts the Draft once its source,
mapping, rows and calculation basis pass the normal acceptance rules.

**Glass's calculation PDFs.** These keep ordered Body, Auxiliary and Paint
rows, included-operation context, source guide codes, unambiguous
manufacturer part identities, notes and printed amounts. PDF labour hours are
already net of overlap and do not reuse the XML gross-time conversion. Parts
and position appendices are evidence for existing rows, never extra charges.
Repeated printed charges and visibly clipped text stay as printed. Whole-row,
section and document reconciliation is required. Section labour must equal
the printed rate × section hours as the source computes it. Missing or
ambiguous required evidence refuses the whole import. Source rates and VAT do
not select a Pegasus rate card or decide a repairer's VAT status.

**Readable text is required.** Estimate PDFs need readable embedded text.
Unreadable, scan-like or unsupported estimates are refused, with no OCR and
no partial Draft. Completing an interrupted import reuses the retained source
under fresh Case authority, without uploading again. Source attribution and
complete arithmetic must agree before an import succeeds. The Estimate
section offers **Complete import** for confirmed retained import sources not
yet represented by an estimate. Pending custody becomes selectable only when
its confirmation completes. Replaying the original upload does not revive
its old lease or assume another Case-version increment.

**Corrections and projections.** A correction creates a new reasoned version
that keeps and supersedes the earlier accepted one. Accepted rows and their
evidence are never edited in place. A Case with no unambiguous current
accepted version fails closed. The specification uses one line vocabulary and
one calculation basis. The three assessment-report lists (new parts, repairs
and additional operations) are one deterministic names-only projection of
those ordered lines, not a second renderer-owned specification.

**Replay in the estimate editor.** The editor saves the Case version and line
identities the Engineer submitted. Retrying the same operation keeps that
intent: source evidence and amendment timestamps are resolved only for a new
operation, not rebuilt before replay detection. A prior successful operation
returns the same estimate identity in its current state, even after later
edits, and never reapplies the older edit. Changed intent under the same
operation key is refused, as is a new operation against a stale Case
version.

### Glass's interrupted sessions

A Glass's launch records its callback and external account before contacting
the provider. Vehicle and estimate identities are kept as soon as they
arrive. Resume continues an interrupted preparation or a known vehicle that
has not started an estimate; an existing estimate reopens by its existing
identity. These actions sit in the Case estimate section and do not need
credentials reset.

Keeping a returned estimate's source files does not use up the Engineer's
still-valid Case edit authority. The import uses that authority to land one
Draft. A genuine Case edit in between, or an expired or lost lease, leaves
the retained result waiting until the Engineer regains authority. Callback
replay creates neither another Draft nor another change.

**Unknown answers hold the account.** A provider write whose answer was lost
stays `Unknown` and keeps the account. It must not create another vehicle or
calculation, and must not release the account just because time passed. The
owning Engineer can close any session that still holds the account, except
one in the middle of an import, only after confirming Glass's is closed and
no estimate is open, and with a reason. Stale versions and closure by another
Engineer are refused. An account holds one live session: while it is held
from another Case, every Case the Engineer opens names that Case instead of
offering a launch, and a second launch is refused before the provider is
contacted. Reopening an estimate may come back under a different provider
estimate id; every id a session was launched under is kept, and the provider
may return any of them. Checkpoints and explicit closure are audited
permanently without provider credentials, callback tokens or document
content.

### Conservative MOT mileage estimation

This section is the only owner of the rule that
[ADR-0012](../adr/0012-conservative-mot-mileage-estimation.md) (superseded)
once held.

When DVSA history must estimate a Case mileage, Pegasus:

- keeps the raw observations and accepts only recognised mile or kilometre
  units;
- groups fail-and-retest episodes;
- sets aside implausible or low-information intervals without deleting them;
- treats a corroborated odometer drop as a new segment;
- derives the estimate from a recency- and quality-weighted median of clean
  rates, using a versioned cohort prior only for sparse histories that pass
  its sample checks;
- returns the exact observation on an exact MOT date, interpolates only
  inside a compatible segment, forecasts only within a validated horizon, and
  gives calibrated intervals only with eligible chronological holdouts;
- otherwise shows a wider, explicitly non-probabilistic range and never
  writes it into the Case by default.

The rule prefers a reviewable abstention or a qualified range over a
plausible but unsupported number. It applies only after the DVSA/DVLA route,
input contract and caller evidence have activated vehicle enrichment. It
neither picks a provider nor authorises an external call.

- **Activation evidence.** The DVLA/DVSA adapter is selected and composed;
  credentials, real caller evidence and live acceptance are evidenced
  separately. Representative chronological holdouts, contract and
  failure-recovery proof, a real caller and operator acceptance are required.
- **Preserved seam.** Raw observations, normalised units, model or rule
  version, estimate or range, calibration evidence and staff disposition stay
  distinct, source-labelled identities.
- **Excluded.** No provider adapter, scheduled lookup, cohort dataset,
  automatic external call or unreviewed Case change is created here.
- **Irreversible choice.** The estimate may be derived only by this
  conservative algorithm. Unsafe evidence gives an abstention or a qualified
  range, never an invented mileage.

### Inspection location and estimate sources

Every Collision Engineers assessment is a desktop inspection. A physical
inspection address is report data, not evidence that anyone attended. The
Principal setting picks a physical vehicle location or the literal
`Image Based Assessment`; a staff override needs a recorded reason and is
reversed the same way. Address suggestions may use Principal usage frequency,
accident location and image or vision evidence; a suggestion never becomes a
confirmed fact by itself.

Repair cost figures come from external estimate imports (including Audatex
and Glass's), AI estimates returned through MCP, or staff file import. Manual
repair totals are never invented to get around the estimate contract. An
unknown repairer VAT status needs an explicit status or category before
totals are accepted. Supplied, observed, derived and professionally accepted
values keep their distinctions.

### Retained PDF estimate import

The estimate-import command accepts the supplied Glass's calculation and
Audatex full-report PDFs through their deterministic provider mappings. It
keeps the original document and its source hash before importing a Draft;
the same Case and hash replay the same import. Printed totals, rates, line
structure and provider identity must agree. PDF net labour is not reduced
again by the XML-specific overlap rule.

Readable embedded text is required. An unusable font map, a scan-like page
or a parser failure gives an explicit refusal, never an OCR request
([ADR-0047](../adr/0047-scanned-instruction-ocr-only.md)). Retained sources
from an interrupted import can be completed later by the same command under
the current Case version and edit lease. Import never selects a Current
estimate.

### Market Research requests

Choosing **Market Research** on the Valuation screen creates a ledger job.
External Claude Cowork, using the Pegasus connector and its own research
tools, does the research and produces files. The connector attaches those
files to the Case and the Automation Actor marks the job Completed. The tools
and the research run outside this repository. Research evidence and any
source-labelled valuation proposal never become the Engineer's Value on their
own. Job states and attribution are owned by
[FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#ai-job-list).

### Valuation readiness

Any valuation check required before Review or Hand to Engineer must be
resolvable at that stage by an authorised person. The Engineer sections are
editable before handoff in Not ready and Review, so availability is not a
reason to defer such a check. Engineer's Value, settlement and report
calculations are engineering work, not invented pre-assignment blockers. A
named external-check failure shows its actual permitted resolution. No
circular readiness gate is acceptable.

## States and transitions

| Thing | States |
| --- | --- |
| VRM read | suggestion, `NoReadableResult`, unknown, dependency unavailable, technical failure; confirmed by staff or registered automatically at the bar |
| Vehicle lookup | current, stale, unavailable, partial, failed; a field value is Lookup then staff-confirmed on Save |
| Repair specification | Draft, then accepted (Current) by Use estimate; a correction makes a new version that supersedes the old |
| Glass's session | launched, `Unknown` (holds the account), resumed, closed by the owning Engineer with a reason |
| Engineer finding | recorded; a correction is a superseding version, never an edit |

## Edge cases and fail-closed behaviour

- No address in the source and no staff entry: the address stays blank. It
  is never inferred.
- An ambiguous plate read is a suggestion only. Two valid registrations in
  one group are Conflicting identification.
- A provider 404 that is not the provider's own not-found error is a failed
  lookup.
- An ambiguous estimate artifact, an unreadable PDF, or a reconciliation
  mismatch refuses the whole import.
- A Case with no unambiguous current accepted specification fails closed.
- A lost Glass's answer stays `Unknown` and holds the account until the
  owning Engineer closes it with a reason.
- Sparse or unsafe MOT history gives an abstention or a qualified range, not
  a number.

## Acceptance evidence

Core tests cover the near-miss and reverse-pairing rules, the vehicle-type
rule, the 404 classification, the fill rule, the Engineer's Value order, the
import replay key, and the conservative estimation algorithm. Integration
tests cover Look up DVLA & MOT, the estimate import command under a lease,
Use estimate, and Glass's session closure. Live DVLA/DVSA, Glass's and
recognition evidence are separate tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `INT-13`, `INT-27`, `INT-28`, `ENG-01`–`ENG-04`, `CASE-28`,
  `CASE-29`, `CASE-34`, `EXT-07`–`EXT-13`, `DATA-02` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-02](frd-02-intake-and-source-identity.md),
  [FRD-05](frd-05-documents-extraction-and-custody.md),
  [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-14](frd-14-record-edit-leases.md),
  [FRD-16](frd-16-case-record-workspace.md),
  [FRD-19](frd-19-image-led-intake-and-pairing.md).
- Technical constraints:
  [ADR-0012](../adr/0012-conservative-mot-mileage-estimation.md)
  (superseded; rule now here),
  [ADR-0018](../adr/0018-provider-inspection-mode-database-setting.md),
  [ADR-0019](../adr/0019-in-process-onnx-vrm-recognition.md),
  [ADR-0031](../adr/0031-automation-actor-contract-without-eva-export-tools.md),
  [ADR-0047](../adr/0047-scanned-instruction-ocr-only.md).
