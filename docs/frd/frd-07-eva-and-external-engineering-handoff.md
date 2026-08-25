# FRD-07: EVA and external engineering handoff
> Owner capabilities: EXT · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design/README.md

## EVA and external engineering handoff

### Focused EVA manual handoff

The current send-to-Engineer route is the operator's EVA export. Pegasus makes
no EVA network call: it downloads one package for staff to import into EVA.
That import is the operational handoff to engineering, although Pegasus cannot
prove EVA receipt or a named-Engineer assignment.

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
export date is emitted as the named system default. An unaccepted mapping is a
configuration failure and blocks export.

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

### External boundary

Three routes are planned:

1. the current manual package import into EVA;
2. the EVA API when EVA supplies a usable contract; and
3. direct integrations with estimating systems such as Audatex and Glass's,
   replacing EVA.

Some AI-generated estimates remain in Pegasus for Engineer review and report
generation. That is a distinct Pegasus-owned route, not a reason to redefine
the EVA export as something other than sending to an Engineer.

The EVA API and direct estimating integrations remain deferred until their
actual contracts, authentication, idempotency, failure/recovery behaviour,
current-version handling, real callers, and operator acceptance exist. The
supplied EVA schema is reference evidence, not proof that the API works and not
authorization to infer an operation. External success, rejection, partial or
unknown outcomes must remain distinct when those routes are implemented.
