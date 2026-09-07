# FRD-06: Vehicle and engineering evidence

## Vehicle-image failure boundary

Grouped vehicle images are evaluated together. A completed group with one usable,
unambiguous VRM follows the existing-case or Image-initiated route; a group with no
usable VRM enters Unidentified once, retaining all files. Two different valid VRMs
are the explicit `ConflictingIdentification` reason and never attach silently to a
Case.
> Owner capabilities: INT (image/VRM), ENG · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design/README.md

## Vehicle and engineering evidence

Vehicle identity, registration, location, valuation, repair evidence,
roadworthiness, total-loss, and salvage information remain source-labelled and
reviewable.

### Inspection address

**Settled operator truth:** the report records either the physical vehicle/repairer location, when that
location is explicitly supplied or operator-confirmed, or the exact value
`Image Based Assessment`. Collision Engineers performs desktop assessments
only. The inspection mode is determined by the Principal's persisted
inspection-mode setting ([ADR-0018](../adr/0018-provider-inspection-mode-database-setting.md)),
not derived from instruction text: instruction documents never contain the
literal value. For an always-image-based Principal (QDOS is seeded so),
`Image Based Assessment` is autofilled at Case creation even when a physical
location appears in the instruction; authorised staff may override it on the
specific Case to the explicitly supplied or confirmed location with an
attributed reason. For a physical-address Principal the location is extracted
from the instruction and operator-confirmed; the provider setting determines
the default mode but never invents or selects a physical address. The
provider-domain reference package contains no address or address-mode default;
the setting lives on the Principal record, and no address is ever inferred
from a provider or domain match. Where the source carries no address evidence
at all, a member of staff supplies the physical location directly at Case
creation and it is retained with their identity as its source; the prohibition
is on Pegasus inferring an address, never on a person stating one.

Every assessment is desktop. The report address is blank, `Image Based
Assessment`, or a selected physical vehicle location with recorded provenance;
it never states attendance.

A manual selection of `Image Based Assessment`, and any override of the
autofilled mode, requires an attributed staff reason in permanent Case
history; the always-image-based autofill records its provider-setting
provenance and a permanent Case-history event. Neither the mode default nor
any address is inferred from a corpus row or domain match.

Inspect at is a fast-update choice on the Case record's Inspection section
(D33, 2026-09-02): `Image Based Assessment`, Claimant address, Repairer
location, Storage location, the previous addresses used for this Principal,
and Manual entry. An option whose value is not recorded on the Case is
disabled, never offered empty. A Case records a storage location as its own
field. Choosing an option records the chosen value with the provenance and
reason rules above; the choice never invents an address.

When `DATA-02` activates, its separately approved reference-data pipeline
accepts only reviewed full addresses, retaining each complete display address
with a normalized postcode. It preserves operator-maintained confirmed rows
across refresh and is deterministic and auditable. Frequency, recency,
proximity, accepted Principal, Repairer, Image Source, and normalized search
text may rank suggestions but never select an address. This activates no
spreadsheet import, route, or caller before its separate acceptance evidence.

### Ordinary-image VRM and image analysis

**Accepted source boundary:** automatic registration reading from an ordinary vehicle image is
suggestion-first. Every result remains attached to one retained source-image
occurrence; staff confirmation creates the provisional vehicle identity. Before
confirmation, a suggestion must not create or identify a case, allocate a
Case/PO reference, overwrite a confirmed registration, select an EVA image,
satisfy a readiness gate, or mutate case workflow. By operator direction
(2026-08-03), a confident unambiguous read at the current accepted recognition
bar may automatically register the Image-initiated Case projection (allocating its Image
Intake Reference) and, where exactly one eligible pre-report instructed Case
carries that confirmed registration with no contradictory identity evidence,
automatically associate it under the settled matching rules; both actions are
recorded with system attribution and remain reasonedly reversible by staff. A
read missing exactly one character of a candidate's confirmed registration
counts as that unambiguous match (operator-directed 2026-08-03): the confirmed
registration completes the read and is the registered identity — a truncated
read is never registered as its own value when a confirmed registration
completes it, a substituted character is never a match, and any second
consistent candidate makes the read ambiguous, except that a read exactly
equal to one candidate's confirmed registration is unambiguous regardless of
additional near-miss candidates. Likewise a read one character
longer than the standard seven-character registration whose fifth character is
a `1` is retried without that character (plate furniture is commonly read as an
inserted `1`); a match found that way assumes the confirmed registration is
correct (operator-directed 2026-08-03). Pairing also runs in reverse on case
acceptance, where a newly accepted eligible case associates a waiting
unassociated Image intake only on exact equality with its registered
identity: the registered identity is immutable, so the completion rules
cannot apply after registration, and a near-miss in this direction stays a
reasoned staff suggestion.

A multi-image upload evaluates this automatic registration/association rule
once across the whole group of images rather than per image; the group
membership, wait-for-completion, VRM aggregation, and fail-closed precedence
rules are defined in
[Grouped image-intake routing](frd-02-intake-and-source-identity.md#grouped-image-intake-routing).

The operator surface distinguishes a suggestion from no readable result or an
unknown result, an unavailable dependency, and a technical failure. It never
renders an empty value as success. Record the source occurrence, task,
engine/provider and version where applicable, time, output, supplied
confidence, failure or unknown outcome, and later staff disposition separately
from confirmed case data.

Recognition runs two distinguishable layers in sequence — plate detection,
then plate reading — and diagnostics must prove which layer ran and which one
abstained without a second business-decision outcome taxonomy and without
logging image content or raw candidate text. Detector-empty (no plate
detected) and recognizer-empty (a plate detected but no readable registration
recovered) both remain the single visible `NoReadableResult` outcome; they are
distinguished only by a non-sensitive, code-level diagnostic reason attached
to that outcome, never by adding a third terminal recognition state. A
retained image's recognition outcome is durable once recorded: re-evaluating
the same image (a sibling group member arriving, a replay) reuses the
recorded outcome rather than re-running the detector or recognizer, so one
retained image is recognised at most once.

The implementation mechanism is not inferred: ordinary-image VRM reading,
Document Intelligence extraction from scanned PDFs, and broader image/damage AI
or vision assistance are different capabilities.
Generated or synthetic vehicle imagery is not acceptance evidence, and no recogniser, model, or adapter acts autonomously.

Pegasus retains every source image. An automated VRM or colour result may only suggest that an image depicts another vehicle; it does not exclude the image from Case-vehicle, EVA-export, or future report-selection pools. An authorised staff member must confirm the different-vehicle finding before the retained source is categorised and excluded as third-party vehicle evidence. Without that confirmation it remains visible as unmatched-vehicle evidence. Neither outcome deletes source evidence or turns an automated assessment into accepted Case fact.

When activated, an AI-assisted image readiness assessment runs automatically whenever current Case images are added, replaced, or removed. It returns a source- and version-labelled advisory on whether the set contains a registration overview, at least one damage close-up, and a reflected image. An always-image-based Principal inspection-mode setting waives only the reflection advisory.

The assessment may run before market valuation and neither creates nor returns an AI Proposal. Its result does not affect Case/PO allocation, Case state, Review, Engineers-queue eligibility, due work, chasing, or staff discretion. Source images remain retained, and report-image selection continues to exclude images showing a person's reflection.

Image-readiness advice never selects, excludes, orders, or otherwise decides report images. Report-image selection is a human Engineering decision in the report-generation section, not an opposing-toggle control on the Case evidence surface.

Report-image preparation is non-destructive (D19, 2026-09-01): the retained source bytes and their hashes never change, and every crop or ordering act produces normalized output beside the source rather than replacing it. A report requires two distinct images, one designated `Close-up` first and one `Overview` second; optional supporting images follow in the explicit order the operator set. The crop and ordering data are a normalized, versioned, attributable record protected by the same expected-version and edit-lease rules as other Case mutations. An issued report retains the exact curation snapshot and the source hashes it used, so a later Case-image or curation change never alters an issued report.

This allocation creates no AI caller. Its activation still requires accepted model/transport, data, cost, evaluation, failure/recovery, real-caller, and approval evidence. Broader image or damage analysis and AI-generated repair specifications remain separate capabilities.

### Vehicle data and MOT enrichment

Vehicle identity/specification is a global Case requirement. Where instruction
evidence omits vehicle facts, an accepted DVLA/DVSA caller supplies
registration-linked make, model, manufacture year, engine capacity, fuel type,
available MOT history, and mileage observations. At activation, DVSA runs for
every Case; until then, approved local replay returns its preserved result and
absent replay evidence returns source-labelled `Unavailable`.

The mileage tiers and discrepancy rule are defined in
[Global vehicle and value checks](frd-02-intake-and-source-identity.md#global-vehicle-and-value-checks). Every
lookup or refresh preserves provider/source, retrieval time, applicable
effective date, source age, response/version identity, and a typed current,
stale, unavailable, partial, or failed outcome. A refresh creates a new
observation; it never silently overwrites a last-good observation, confirmed
value, or higher-tier mileage. Acceptance, rejection, or linking of an
external fact enters permanent business history. Routine calls, retries, and
polling remain content-safe telemetry.

The Case record offers one **Look up DVLA & MOT** action (D34, 2026-09-02).
Looked-up values are suggestions: each appears as a chip beside its field and
fills the field only when chosen, with the accepted value recorded as above.
There is no checks panel and no suggestion table. Experian stays a disabled
seam (D7, `ENG-001`).

**Evidence boundary:** the DVLA/DVSA production adapter and its composition
exist. Returned fields remain source-labelled suggestions; unavailable fields
are explicit and never inferred. Credential configuration, an exact deployed
artifact, real caller/failure evidence and operator acceptance remain separate
from source presence.
Vehicle enrichment does not activate valuation behavior.

### Professional engineering findings and correction

**Settled operator truth:** the Collision Engineers Engineer report is definitive for the case.
Roadworthiness (`Roadworthy` or `Unroadworthy`) and Assessment (`Repairable` or
`Total loss`) are separate professional findings: neither is derived from the
other, and Triage findings never populate or change either one.

A correction never edits an earlier accepted or issued finding in place. It
creates a reasoned superseding report/finding or addendum with actor, time,
source, structured before/after values, and the prior artifact/version retained.
If the case is closed, an authorised reasoned reopen through the ordinary
destination gates must occur before the correction; `Created in error` remains
non-reopenable. Current views may recompute from the superseding version, but
historical reports, events, and counts keep their original provenance.

Betterment figures and estimate `guide` codes recorded on a source or an
estimate version are retained evidence only (D17, 2026-09-01). No finding,
figure, outcome, deduction, or settlement semantics are derived from either;
they are shown and retained as they were recorded.

Triage findings and their corrections have no case, report, Audit-reference,
fee, or invoice effect. Invoicing is separately deferred: a professional
finding correction must not silently create, alter, credit, or void an invoice.
Any later financial consequence requires the separately accepted,
versioned finance contract.

Automated or AI-assisted extraction may propose candidate facts, confidence,
damage observations, repair operations, costs, flags, valuation comparables,
roadworthiness, total-loss, or salvage evidence only where an allocated
capability and accepted evaluation permit it. `Pegasus.Core` and an authorised
human own accepted facts, economics, findings, outcome, legal use, and approval.

A skill, prompt, model, workspace, external schema, or imported reference never
becomes current OEM instruction, repair policy, valuation authority, legal
advice, Engineer approval, or product policy merely by existing.

### Damage record

The retired D18 Engineer name, qualifications and signature readiness items
are removed. The Sign-off Engineer account tuple is their sole owner
(ENG-038).

Damage records use 23 detailed regions with a parent-region map beside the
broad regions. Each region carries a severity and a note;
collision work has no separate damage type (D45, 2026-09-03). The record also
carries tyres and seat belts per corner, the spare tyre, the centre belt,
unrelated damage with its deduction, and paint or
material transfer. `impact_location` and `impact_severity` are derived from
the zone list by `Pegasus.Core`, never entered. The report prints the marked
diagram
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#assessment-report-outcomes)).

### Valuation sources

Valuation records keep guide month and source: Glass's, Brego and Super CAP
are manual sources; Cazana is a disabled seam; AI market research is
automation-only. Every entry keeps its date, time, mileage, retail and trade
values; guide month is an additional per-entry field owned by `CASE-029`
(EPIC-012 context). Glass's
valuation and Glass's repair estimating are two systems and both are used:
the valuation source and the estimate import source keep separate label
entries and are never merged. An AI market research entry is the proposal
recorded by the `MarketResearch` job
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#ai-job-list));
it never becomes the Engineer's Value by itself. Engineer's Value is adopted
only by an explicit Apply using this order: commercial VAT 20%, prior total
loss 10% or 20%, fixed additions, then condition deduction, with whole-pound
rounding away from zero. A generic assessment save never writes the adopted
value (AUTO-015). Valuation adjustments, rationale and revaluation history
stay with `EXT-10` (later).

### Settlement

The settlement fields are outcome, category, salvage value, excess,
betterment, claimant VAT registered, reserve, equity (derived), repair
duration and delays, report delay, storage per day, recovery, hire start and
daily cost, diminution, and salvage logistics (D41, 2026-09-02). Equity is
derived, never entered. Financial ratio lines are permitted, not required;
the "no percentage" rule
([FRD-01](frd-01-case-identity-and-lifecycle.md#lifecycle-closure-and-correspondence))
applies only to completeness. Outcome semantics are owned by
[FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#assessment-report-outcomes).

### Canonical repair specifications

Every accepted repair specification is an immutable, versioned Core aggregate.
Each Case has exactly one current accepted canonical version, shared by the
Case's report projections.

Each version retains its stable identity, ordered technical
lines, source route, source artifact identity/version/hash, mapping evidence,
raw calculation basis and totals, the selected labour-rate card version where
one was selected, creating actor/time, and—when accepted—the
named Engineer and acceptance time. Glass's, Audatex PDF, an approved AI
proposal, and manual entry are provenance routes, never authorities: imported
or automated material remains a draft until an authorised Engineer accepts the
exact source, mapping, ordered lines, and calculation basis. Legacy lines with
no such evidence remain explicit `LegacyUnresolved` drafts and cannot satisfy
report readiness.

A raw artifact imported through either caller of the shared import command is
keyed by Case plus source hash (D16, 2026-09-01): the same Case with the same
hash is an idempotent replay that returns the existing Draft, while a different
artifact creates the next immutable Draft. The provider and parser are
auto-detected from the registered types and an ambiguous artifact is refused,
never guessed.

Corrections create a new reasoned version which retains and supersedes the
earlier accepted version; accepted rows and their evidence are never edited in
place. A Case with no unambiguous current accepted version fails closed. The
shared specification uses one technical line vocabulary and calculation basis. The three
assessment-report lists—new parts, repairs, and additional operations—are a
single deterministic names-only projection of those ordered lines, not a
second renderer-owned repair specification.

### Conservative MOT mileage estimation

> Owner capability: ENG (vehicle enrichment). Relocated from ADR-0012 (2026-07-30).

When DVSA history must estimate Case mileage, Pegasus preserves raw observations; accepts only recognised mile/kilometre units; groups fail/retest episodes; excludes implausible or low-information intervals without deleting them; and treats a corroborated odometer drop as a new segment. It derives an estimate from a recency- and quality-weighted median of clean rates, using a versioned cohort prior only for sparse histories that pass its sample checks. Exact observations are returned on exact MOT dates, interpolation is limited to a compatible segment, forecasting is limited to a validated horizon, and calibrated intervals require eligible chronological holdouts. Otherwise Pegasus shows a wider, explicitly non-probabilistic range and never defaults it into the Case.

This deliberately favours a reviewable abstention or qualified range over a plausible but unsupported mileage value. It applies only after the separately accepted DVSA/DVLA route, input contract, and caller evidence activate vehicle enrichment; it neither selects a provider nor authorises an external call.

- **Activation evidence:** the DVLA/DVSA adapter is selected and composed; credentials, real caller evidence and live acceptance remain independently evidenced.
- **Preserved seam:** raw observations, normalized units, model/rule version, estimate/range, calibration evidence, and staff disposition remain distinct source-labelled identities.
- **Excluded:** this creates no provider adapter, scheduled lookup, cohort dataset, automatic external call, or unreviewed Case mutation.
- **Activation evidence:** representative chronological holdouts, contract and failure/recovery proof, a real caller, and operator acceptance are required.

The accepted VRM reading bar may create an Image-initiated Case reference before
formal instructions arrive. A readable sibling keeps a registration-free damage
close-up in the same group. No-readable or conflicting valid VRMs do not receive
a fabricated image reference; they enter the grouped Unidentified contract with
the applicable reason, including conflicting_vrms.
- **Irreversible choice:** the estimate may be derived only by this conservative algorithm; unsafe evidence yields abstention or a qualified range rather than an invented mileage value.
