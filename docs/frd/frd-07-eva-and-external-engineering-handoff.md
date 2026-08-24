# FRD-07: EVA and external engineering handoff
> Owner capabilities: EXT · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design/README.md

## EVA and external engineering handoff

### Focused EVA manual handoff

**Accepted focused-alpha boundary:** EVA remains the authoritative external
engineering/report workflow. Pegasus performs no EVA network call. It
deterministically serializes UTF-8 JSON in the exact 13-key order below,
includes every custody-confirmed eligible Case-vehicle image, and writes a
SHA-256 manifest over the JSON and image identities and bytes. Stable manifest
ordering exists only for reproducible package integrity; Pegasus owns no EVA
presentation, selection, or report-image order.
The two retained populated EVA JSON examples are immutable
reference evidence for the field shape; they do not supply credentials or
activate an adapter.

The JSON keys, in serialization order, are:

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

There is one act that produces the package: the operator's export of a Case
(ENG-016). The first export that succeeds for a Case records the once-per-case
`First sent to Engineer` proxy; later exports of the same Case produce the same
package and record nothing further. The proxy proves Pegasus package generation
only; it does not claim EVA receipt or named-Engineer assignment, which remain
EVA-owned events. An image/document upload into Pegasus, Box custody, or the
presence of a report PDF is not this handoff and is not external delivery
evidence.

The export is a `POST`. Producing the package records a business event, so it
must not be reachable by a browser prefetch or an ordinary page refresh.

A Case that is missing a field is exported, not refused: the value is emitted
empty and the operator is told which fields the Case does not hold before the
file leaves Pegasus. Only an unaccepted field mapping refuses an export.

A successful export answers with the complete JSON and all-eligible-image bundle immediately. It proves neither EVA receipt nor report delivery, and it does not change Case state.
The container format is intentionally unspecified: its selection must evaluate
whether a single archive is the clearest usable representation without changing
the exact package contents, manifest, or manual-handoff boundary.

The focused handoff readiness review keeps four source-labelled inputs distinct:
the saved source email, vehicle images, valuation evidence, and initial
instructions. A missing item remains visible and cannot be represented as
present. The Experian adverse-history check remains an EVA-owned downstream
step; Pegasus preserves its source-labelled result if later received but does
not claim that manual package generation performed the check.

The focused alpha exports every custody-confirmed Case-vehicle image except an image that authorised staff have confirmed as third-party vehicle evidence. Pegasus does not select, duplicate, or presentation-order EVA images and exposes no `Use for EVA`/`Exclude` controls. EVA owns image selection, ordering, and report eligibility after import. When EVA is replaced, those Engineering decisions move to the accepted `1.0.0` Engineers screen and remain under Engineer authority. Video-derived screenshots are exported only when retained as distinct Case-vehicle image occurrences with source-video and capture-position provenance. The source observations and their scope are retained in the [Collision Engineers administration overview](../../reference/reports/collision_engineers_admin_overview.md).

### External boundary

EVA API integration and EVA replacement remain deferred. Activation requires
vendor access; every required Collision Engineers principal code; parity with
the accepted manual JSON/all-eligible-image handoff; stable source and image
identity; accepted mapping; identity/authorization; idempotency;
failure/recovery; current-version handling; real caller proof; and operator
acceptance.

Any later adapter treats a proxy-only case/vehicle/inspection fetch as a
read-only external observation. Fetch, create-with-children, picture upload, and
report-with-PDF handoff retain separate operation, correlation, and outcome
identities; success of one never proves another. A parent or overall success is
not inferred when required child validation failed. The exact vendor contract
must decide whether creation is atomic or partial, and an unknown/partial
outcome remains recoverable rather than being retried as a new creation.

Pegasus preserves structured vendor success, validation failure, rejection,
partial/unknown outcome, and correlation evidence instead of collapsing them
into one Boolean. These are Pegasus evidence classes, not claimed EVA response
labels. No response identifier, fetch result, upload result, or external
success creates, selects, or changes a Pegasus case/reference; only the Core
intake/allocation transaction may do that.

**Source limitation:** the supplied EVA schema is reference evidence, not an
accepted Pegasus operation or wire contract. No allowed accepted source
establishes a proxy-only case/vehicle/inspection fetch, a
create-with-children operation or its validation/atomicity, separate picture
upload, report-with-PDF handoff, response model, or case/reference correlation
semantics. Those details remain unresolved in [EVA API
activation](../open-decisions.md#eva-api-activation-070--ext-04); none may be inferred from
the manual export or used to authorize an EVA call.

Audatex remains a separate estimating-system role unless an accepted capability
and integration contract establish otherwise. Guided-capture providers are
candidates/evidence, not active routes.
