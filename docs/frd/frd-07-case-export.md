# FRD-07: Case export

> Owner capabilities: CASE-21, CASE-30, EXT-03 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Every standard Case has **Export case** in its Actions menu, in every
  state. There is no Principal setting and no Administration switch.
- The export is a plain download: a ZIP holding a 13-field JSON file and the
  Case's eligible images.
- Exporting never changes the Case. It writes one history line and nothing
  else.
- A Triage Case has no export. Unidentified and Image Intake items are not
  Cases, so they have none either.

## Purpose

This document owns the Case export: what it holds, when it is offered and
what it records. Engineering work, Assign Engineer and every Case state
change are owned by [FRD-13](frd-13-case-lifecycle-and-workflow.md). Reports
are owned by [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md).

## Behaviour

### Export case

**Where it is.** **Export case** is an item in the Case page's Actions menu
([FRD-16](frd-16-case-record-workspace.md#actions-menu)). It is shown on
every standard Case, whatever its state, Closed and Completed included.
Any staff member who may do casework may use it. It needs no Sign-off
Engineer, no readiness item and no Principal setting. A Triage Case answers
Not found on the export route
([FRD-12](frd-12-operator-experience.md)).

**What it holds.** The export confirms the values currently on the Case. A
populated suggestion is exported and keeps its `Suggested` provenance. VAT
and mileage are optional. If mileage is present, mileage and its unit must be
saved together. If Inspection Date is blank, the export date is the named
system default.

The JSON file is deterministic UTF-8 with these keys in this exact order:

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

`Work Provider` is the Case's Principal. `Reference` is the Principal's
reference, not the Pegasus Case reference. `Instruction Date` is the Case's
Received date
([FRD-23](frd-23-case-draft-fields-provenance-and-global-checks.md#instruction-field-meanings))
as `dd/MM/yyyy`; every Case has one, so it is never blank.

**Images.** The ZIP carries every eligible retained Case-vehicle image in an
`Images/` folder. Pegasus does not choose or order the images, with one
exception: an image tagged Third party image
([FRD-05](frd-05-documents-extraction-and-custody.md#image-tags)) is left
out. An image's custody status is used to find its verified bytes, not as a
readiness decision. A Case with no eligible image exports the JSON file
alone.

**File names.** The download is `{reference}.zip`, where `{reference}` is the
Pegasus Case reference. It holds `{reference}.json` and the `Images/` folder
only. There is no manifest and no provenance sidecar. The HTTP download
carries the archive's SHA-256 as `Content-Digest`.

**What is recorded.** Every successful export writes one replay-safe
`case_exported` history record. It holds the Case version, the mapping
identity, the exported values and their provenance, the archive hashes, and
the image identities and hashes. Replaying the same operation returns the
same record. The export does not change the Case state, version or edit
lease, and it records no `First sent to Engineer` event. That event is the
Case's first entry into With Engineer
([FRD-13](frd-13-case-lifecycle-and-workflow.md#assign-engineer)).

### External boundary

The export is a download for staff to use as they choose. Pegasus never
claims that anyone received it. Native estimates, imported provider
estimates and accepted AI estimates stay Pegasus-owned engineering behaviour
([FRD-25](frd-25-repair-estimates-imports-and-glasss-sessions.md)). The export
adds no calculation policy.

## States and transitions

The export changes no Case state. It is offered in every state of a standard
Case.

## Edge cases and fail-closed behaviour

- A Case with no eligible image exports the JSON file alone.
- A Third party image is never included.
- A mapped value that fails validation refuses the export and names the
  field; nothing is recorded and the Case is unchanged.
- A replayed operation returns its stored record and writes nothing new.
- A Triage Case answers Not found.

## Acceptance evidence

Core tests cover the field order, image exclusion and file names.
Integration tests cover the download over HTTP in more than one state,
including a Case with no images and a Case with no Sign-off Engineer, and
prove the Case state and version are unchanged
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `CASE-21`, `CASE-30`, `EXT-03` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-05](frd-05-documents-extraction-and-custody.md),
  [FRD-06](frd-06-vehicle-and-engineering-evidence.md),
  [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-16](frd-16-case-record-workspace.md).
- Technical constraints:
  [ADR-0064](../adr/0064-case-export-replaces-eva-routes.md).
