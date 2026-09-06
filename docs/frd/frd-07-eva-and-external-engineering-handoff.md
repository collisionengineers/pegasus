# FRD-07: EVA and external engineering handoff
> Owner capabilities: EXT · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design/README.md

## EVA and external engineering handoff

### Focused EVA manual handoff

There are two send-to-Engineer routes. The export downloads one package for
staff to import into EVA; that import is the operational handoff to
engineering, although Pegasus cannot prove EVA receipt or a named-Engineer
assignment. The API submission (EXT-04) sends the same case to EVA directly and
does prove delivery. Both are reached from one **Send to EVA** control on the
case, offered in `Review` and again in `With Engineer` as a re-send (D36,
2026-09-02). Its dialog holds Engineer, Sign-off Engineer, Download ZIP and —
when the Principal enables it — Send via API; it offers whichever routes that
case and principal allow. There is no separate Download EVA package action.

Export is first available while the case is in `Review`, and again from
`With Engineer` as a re-send (D36). `Review` is the single
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
exports are additional action-history records. The first successful Download
ZIP from `Review` atomically records the handoff and moves the Case to `With
Engineer`, increasing its version; Send to EVA is the implicit review (D44,
D47). If either part fails, the Case remains in `Review` and no handoff is
recorded. A re-send from `With Engineer` does not change state or version. The
HTTP download includes the archive SHA-256 as `Content-Digest`.

### Direct EVA API submission

EXT-04. Pegasus submits a case to EVA over its API, carrying the same mapped
values and the same eligible images the export carries. The route was built
against EVA's test credentials on 2026-08-27 by operator direction.

**Pegasus has not yet called EVA.** The contract below is proved against the
vendor's own recorded traffic and against its published request model; no
submission has been made to any EVA environment, so nothing here establishes
that EVA accepts this payload, that images land, or what it returns. That is a
deliberate deferral (operator decision, 2026-08-27), not an oversight, and it
is why every Principal setting defaults to off. Live credentials are a further,
separately gated change.

Each Principal carries two independent settings, both off by default:

- **Manual API submission** — an operator may submit a case in `Review` from
  the Send to EVA dialog, and re-send it from `With Engineer` (D36).
- **Automatic API submission** — a case reaching `Review` is submitted without
  operator action.

They are independent, so a Principal may submit automatically and offer no
button. Such a Principal has no case-page recovery from a failed submission:
the reconciliation sweep does not re-arm a case that already carries a
submission work row, so a submission that exhausted its retries is recovered
from the Operations external-work retry surface, which every queued kind
shares. A replacement Principal inherits its predecessor's settings.

**A case is submitted automatically at most once.** EVA has no idempotency: a
second accepted instruction creates a second claim with its own File
Reference, and no API call can withdraw it; EVA's update endpoints are not
suitable for this product's use case (operator decision, 2026-08-27). So
automatic submission fires once, on reaching `Review`, the reconciliation
sweep never re-arms a case that carries a submission work row, and a case
retracted from `Review`, reworked and returned is not resubmitted
automatically. Reaching EVA means a `Succeeded` **or** a `Partial` outcome: an
acceptance that returned no identifier still created the claim.

The consequence must be stated plainly: **once a case has been submitted,
later changes to it reach EVA only through an explicit re-send.** The earlier
rule that a submitted case is never submitted again by either route is
superseded by D36 (2026-09-02): from `With Engineer` the Send to EVA dialog
offers Download ZIP, and Send via API when the Principal enables it. A
re-send over the API is a new, separately recorded submission with its own
outcome and EVA identifiers; because EVA cannot update a claim, it creates a
second claim, and that is the operator's deliberate act in the dialog — never
a retry and never an update.

Every submission records its outcome, and the four outcomes stay distinct:

| Outcome | Meaning | Retried |
| --- | --- | --- |
| Succeeded | EVA accepted the instruction and returned its identifiers | no |
| Rejected | EVA refused it and said why | no — the same payload will be refused again |
| Partial | EVA accepted it but returned no identifier | no — the case did reach EVA |
| Unknown | delivery could not be determined | no automatic retry; retain uncertainty and require explicit staff re-send |

An `Unknown` result may already have reached EVA and is never retried
automatically. It is terminal; staff review the retained attempt before an
explicit re-send.
Both EVA identifiers are retained: the response
identifier and the File Reference EVA embeds in its message text, which is what
an operator quotes.

Submission is gated on `Review` — or on `With Engineer` for a re-send (D36) —
and on at least one eligible image, exactly as the export is; it repeats no
other readiness policy. It records replay-safe Case action history for every
attempt, delivered or not. The first successful manual Send via API from
`Review` — an outcome of `Succeeded` or `Partial`, meaning EVA accepted the
instruction — atomically records the handoff and moves the Case to `With
Engineer`, increasing its version; Send to EVA is the implicit review (D44,
D47). A `Rejected` or `Unknown` outcome is not a handoff: EVA did not accept
the instruction, or delivery could not be determined, so the Case remains in
`Review`, unchanged in version and edit lease, with no state transition —
the attempt is still recorded in Case action history (CASE-040 review). A
failure detected before the transport call leaves the Case in `Review` with
nothing recorded at all. A failure detected only after EVA has already
accepted the instruction — a state change or version conflict found on the
post-delivery re-check — still records the submission and its action
history, since the delivery already happened and must not be lost, but
likewise leaves the Case in `Review`. A re-send from `With Engineer` does not
change state or version. Automatic submission remains a once-only `Review`
action. D47's first-send transition remains one route into report preparation;
explicit Start Case Work or assignment is the other route, and neither requires
EVA delivery.

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

The EVA API route was built on 2026-08-27 by operator direction, with its
contract, authentication, failure behaviour, callers and outcome model recorded
above, and Pegasus owns the idempotency EVA does not provide. It has made no
call: the route is proved against recorded vendor traffic only, and no
submission to a live or test EVA environment forms part of its evidence. Both
that first submission and the live-credential swap remain separately gated on
the operator.
