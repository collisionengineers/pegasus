# FRD-07: EVA and external engineering handoff
> Owner capabilities: EXT · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design/README.md

## EVA and external engineering handoff

### Focused EVA manual handoff

There are two send-to-Engineer routes. The export downloads one package for
staff to import into EVA; that import is the operational handoff to
engineering, although Pegasus cannot prove EVA receipt or a named-Engineer
assignment. The API submission (EXT-04) sends the same case to EVA directly and
does prove delivery. Both are reached from one **Send to EVA** control on the
case, which opens a page offering whichever routes that case and principal
allow.

Export is available only while the case is in `Review`. `Review` is the single
business readiness decision: reaching it requires complete instructions and
at least one eligible case image. Staff-review flags cannot override missing
completeness. The export does not repeat a second field, evidence-status, Case
custody, or Audit custody readiness policy. Saving case data invalidates the
previous completeness confirmation, returns the case to `Not ready`, and tells
the operator that completeness must be confirmed again.

Pressing Export confirms the values currently populated on the reviewed case.
A populated suggestion is therefore exportable and keeps its `Suggested`
provenance. VAT and mileage are optional. Mileage and mileage unit must be
saved together when mileage is present. If Inspection Date is blank, the
export date is emitted as the named system default. Export has no separate EVA
activation or mapping-acceptance switch; the API submission does, and it is
per principal — see [Direct EVA API submission](#direct-eva-api-submission).

The package contains deterministic UTF-8 JSON in this exact key order and every
eligible retained Case-vehicle image:

1. `Work Provider`
2. `VRM`
3. `Vehicle Model`
4. `Claimant Name`
5. `Reference`
6. `Incident Date`
7. `Instruction Date`
8. `Inspection Date`
9. `Inspection Address`
10. `Accident Circumstances`
11. `VAT Status`
12. `Mileage`
13. `Mileage Unit`

`Reference` is the work provider's reference, not the Pegasus case reference.
The archive contains the ordered JSON and `Images/` only; there is no manifest
or provenance sidecar. Pegasus does not select or presentation-order images
for EVA. A retained image's storage/custody status is used to locate verified
bytes, not as a separate case-readiness decision.

Every successful export writes replay-safe Case action history containing the
case version, mapping identity, exported values and provenance, archive hashes,
and image identities/hashes. The first successful export also records the
once-per-case `First sent to Engineer` proxy used by the dashboard; later
exports are additional action-history records. Export does not change the Case
state or version. The HTTP download includes the archive SHA-256 as
`Content-Digest`.

### Direct EVA API submission

EXT-04. Pegasus submits a case to EVA over its API, carrying the same mapped
values and the same eligible images the export carries. Activated against
EVA's test environment on 2026-08-27; live credentials are a separate,
operator-gated change.

Each Principal carries two independent settings, both off by default:

- **Manual API submission** — an operator may submit a case in `Review` from
  the Send to EVA page.
- **Automatic API submission** — a case reaching `Review` is submitted without
  operator action.

They are independent, so a Principal may submit automatically and offer no
button. Such a Principal has no manual recovery from a failed submission;
recovery is the reconciliation that re-arms the work. A replacement Principal
inherits its predecessor's settings.

**A case is submitted at most once.** EVA has no idempotency: a second accepted
instruction creates a second claim with its own File Reference, and no API call
can withdraw it. So a case that has reached EVA is never submitted again, by
either route, and the rule is a database constraint rather than only a code
path.

The consequence must be stated plainly: **once a case has been submitted, later
changes to it do not reach EVA.** A case retracted from `Review`, reworked and
returned is not resubmitted. EVA's update endpoints are not suitable for this
product's use case (operator decision, 2026-08-27), so the export remains the
only route by which a changed case reaches EVA a second time.

Every submission records its outcome, and the four outcomes stay distinct:

| Outcome | Meaning | Retried |
| --- | --- | --- |
| Succeeded | EVA accepted the instruction and returned its identifiers | no |
| Rejected | EVA refused it and said why | no — the same payload will be refused again |
| Partial | EVA accepted it but returned no identifier | no — the case did reach EVA |
| Unknown | delivery could not be determined | yes, with backoff, to an attempt cap |

Only `Unknown` is retried, because it is the only outcome where the case may
not have reached EVA. Both EVA identifiers are retained: the response
identifier and the File Reference EVA embeds in its message text, which is what
an operator quotes.

Submission is gated on `Review` and on at least one eligible image, exactly as
the export is; it repeats no other readiness policy. It records replay-safe
Case action history and does not change the Case state or version.

Values EVA's instruction model has no field for — the inspection date and the
mileage — are sent as labelled lines in the instruction's note rather than
mapped to a field whose meaning no accepted source establishes. The work
provider travels the same way, because the claimant name occupies `InsName` at
the operator's direction. The instruction date is not sent: EVA sets it when
the instruction arrives.

### External boundary

Three routes are planned:

1. the current manual package import into EVA;
2. the EVA API when EVA supplies a usable contract; and
3. direct integrations with estimating systems such as Audatex and Glass's,
   replacing EVA.

Some AI-generated estimates remain in Pegasus for Engineer review and report
generation. That is a distinct Pegasus-owned route, not a reason to redefine
the EVA export as something other than sending to an Engineer.

Direct estimating integrations remain deferred until their actual contracts,
authentication, idempotency, failure/recovery behaviour, current-version
handling, real callers, and operator acceptance exist. A supplied vendor schema
is reference evidence, not proof that an API works and not authorization to
infer an operation. External success, rejection, partial or unknown outcomes
must remain distinct when those routes are implemented.

The EVA API route was activated on 2026-08-27 against EVA's test environment,
with its contract, authentication, failure behaviour, callers and outcome model
recorded above. Pegasus owns the idempotency EVA does not provide. Live
credentials remain a separate operator-gated change; EVA serves both
environments from one host, so the swap is a credential change and nothing
else.
