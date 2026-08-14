# FRD-06: Vehicle and engineering evidence
> Owner capabilities: INT (image/VRM), ENG · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design.md

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

A manual selection of `Image Based Assessment`, and any override of the
autofilled mode, requires an attributed staff reason in permanent Case
history; the always-image-based autofill records its provider-setting
provenance and a permanent Case-history event. Neither the mode default nor
any address is inferred from a corpus row or domain match.

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
bar may automatically register the pre-Case Image intake (allocating its Image
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

The operator surface distinguishes a suggestion from no readable result or an
unknown result, an unavailable dependency, and a technical failure. It never
renders an empty value as success. Record the source occurrence, task,
engine/provider and version where applicable, time, output, supplied
confidence, failure or unknown outcome, and later staff disposition separately
from confirmed case data.

The implementation mechanism is not inferred: ordinary-image VRM reading,
Document Intelligence extraction from scanned PDFs, and broader image/damage AI
or vision assistance are different capabilities.
Generated or synthetic vehicle imagery is not acceptance evidence, and no recogniser, model, or adapter acts autonomously.

Pegasus retains every source image. An automated VRM or colour result may only suggest that an image depicts another vehicle; it does not exclude the image from Case-vehicle, EVA-export, or future report-selection pools. An authorised staff member must confirm the different-vehicle finding before the retained source is categorised and excluded as third-party vehicle evidence. Without that confirmation it remains visible as unmatched-vehicle evidence. Neither outcome deletes source evidence or turns an automated assessment into accepted Case fact.

When activated, an AI-assisted image readiness assessment runs automatically whenever current Case images are added, replaced, or removed. It returns a source- and version-labelled advisory on whether the set contains a registration overview, at least one damage close-up, and a reflected image. An always-image-based Principal inspection-mode setting waives only the reflection advisory.

The assessment may run before market valuation and neither creates nor returns an AI Proposal. Its result does not affect Case/PO allocation, Case state, Review, Engineers-queue eligibility, due work, chasing, or staff discretion. Source images remain retained, and report-image selection continues to exclude images showing a person's reflection.

Image-readiness advice never selects, excludes, orders, or otherwise decides report images. Report-image selection is a human Engineering decision in the report-generation section, not an opposing-toggle control on the Case evidence surface.

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

**Source limitation:** no allowed source selects the live DVLA/DVSA provider,
API, licence, exact response fields, credentials, rate/limit behavior, error
contract, target, or caller proof. Those items remain activation gates.
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

### Conservative MOT mileage estimation

> Owner capability: ENG (vehicle enrichment). Relocated from ADR-0012 (2026-07-30).

When DVSA history must estimate Case mileage, Pegasus preserves raw observations; accepts only recognised mile/kilometre units; groups fail/retest episodes; excludes implausible or low-information intervals without deleting them; and treats a corroborated odometer drop as a new segment. It derives an estimate from a recency- and quality-weighted median of clean rates, using a versioned cohort prior only for sparse histories that pass its sample checks. Exact observations are returned on exact MOT dates, interpolation is limited to a compatible segment, forecasting is limited to a validated horizon, and calibrated intervals require eligible chronological holdouts. Otherwise Pegasus shows a wider, explicitly non-probabilistic range and never defaults it into the Case.

This deliberately favours a reviewable abstention or qualified range over a plausible but unsupported mileage value. It applies only after the separately accepted DVSA/DVLA route, input contract, and caller evidence activate vehicle enrichment; it neither selects a provider nor authorises an external call.

- **Deferred:** DVLA/DVSA provider selection, licence, contract, credentials, caller, and live activation remain open.
- **Preserved seam:** raw observations, normalized units, model/rule version, estimate/range, calibration evidence, and staff disposition remain distinct source-labelled identities.
- **Excluded:** this creates no provider adapter, scheduled lookup, cohort dataset, automatic external call, or unreviewed Case mutation.
- **Activation evidence:** representative chronological holdouts, contract and failure/recovery proof, a real caller, and operator acceptance are required.
- **Irreversible choice:** the estimate may be derived only by this conservative algorithm; unsafe evidence yields abstention or a qualified range rather than an invented mileage value.
