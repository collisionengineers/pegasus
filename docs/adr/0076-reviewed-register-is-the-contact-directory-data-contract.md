---
id: ADR-0076
status: accepted
date: 2026-10-15
supersedes: [ADR-0018]
superseded_by: []
related_capabilities: [DATA-02, ACC-05]
related_frd: [frd-04, frd-06, frd-17]
tags: [reference-data, contacts, principals, inspection-address]
---

# ADR-0076: The reviewed register is the Contacts directory data contract

## Status

Accepted on 15 October 2026 by operator decision, with the operator's rulings
of 16 October 2026 on the Insurer kind, organisations that are both a
Principal and a Claim Source, inspection modes and links to inactive
Principals.

This record replaces, in part:

- [ADR-0018](0018-provider-inspection-mode-database-setting.md): the
  Deferred-capability impact clause that DATA-02's inspection-address
  reference-data pipeline "remains deferred", and decision 5, which defers a
  dedicated operation for changing an existing Principal's inspection mode.
  The rest of ADR-0018 stands, and its body stays as filed.

[FRD-04](../frd/frd-04-parties-accounts-and-access.md#contacts-administration)
owns the Contacts directory and
[FRD-06](../frd/frd-06-vehicle-and-engineering-evidence.md#inspection-address)
the inspection address and its reference addresses.

## Context

The PRD excluded "broad production-data import from raw reference workbooks
or evidence", FRD-04 read raw workbooks as "evidence, not import authority",
and `reference/README.md` required "operator review, an accepted data
contract, and separately authorized target operations" for any import.
ADR-0018 left DATA-02's pipeline deferred. The Contacts directory has one
writer, `IContactDirectoryAdministration.SaveAsync`, which an Administrator
calls from the Contact page.

The operator reviewed a register of the organisations, codes and addresses
the evidence names, kept outside the repository
(`artifacts/pegasus-release-readiness/entities/register/`). Two rulings of
15 October 2026 govern the evidence behind each row:

- Principal code conflicts: "find evidence to support any aliases and cite
  this", checking the SOP details in the report-dispatch skill
  (`outlook-and-box-admin/dist/report-dispatch.skill`).
- Report-sending address spelling conflicts: "check and clarify
  discrepancies against" the same skill.

## Decision

1. **The Contact types are unchanged.** The operator first ruled "add an
   Insurer kind (52)" (15 October 2026). After a cross-reference of those
   rows
   (`artifacts/pegasus-release-readiness/entities/register/insurer-cross-reference-2026-10-16.md`),
   the operator ruled "Drop the Insurer kind" and "the 52 rows stay as
   evidence in the register" (16 October 2026). Owner:
   [FRD-04](../frd/frd-04-parties-accounts-and-access.md#parties-principals-organisations-accounts-and-access).
2. **An intermediary is a Claim Source.** "intermediary is claim source now"
   and "some of these can also be principles e.g. sixways" (15 October 2026);
   "Two Contacts (Principal and Claim Source) for all three now: Sixways,
   ACSP and CS" (16 October 2026). Owner:
   [FRD-04](../frd/frd-04-parties-accounts-and-access.md#parties-principals-organisations-accounts-and-access).
3. **The reviewed register is the accepted data contract.** "One-off
   operator-run loader through `IContactDirectoryAdministration.SaveAsync`
   from reviewed CSVs under `artifacts/`; PRD, FRD-04 and reference/README
   amended to name the reviewed register as the accepted data contract; prod
   run under separate authorisation" (15 October 2026). Owner:
   `IContactDirectoryAdministration.SaveAsync`.
4. **What loads.** Principals: "Live 53 enriched + the evidenced codes
   (corpus, SOP, job sheet, corpus JSON) added as inactive; the EVA-only tail
   stays register evidence" (15 October 2026); "inactive codes from the four
   cited evidence sources (29 rows), the 32 EVA-only codes held", "the 12
   codeless Principals' proposed codes held until approved in the CSV" and
   "where live and register differ, live is kept and the register value
   listed" (16 October 2026). The other types: "Repairers (223), Storage &
   recovery (51), Claim sources (21, 10 live), Third-party engineers (from
   the 20)" (15 October 2026). Owner:
   [FRD-04](../frd/frd-04-parties-accounts-and-access.md#contacts-administration).
5. **Links follow the evidence.** "they all need to be correctly LINKED to
   principles too" and "also need to link these correctly to principles
   based on evidence" (15 October 2026). Links to inactive Principals: "Allow
   them: `SaveAsync` accepts a link to an inactive Principal" and "the
   Contact page shows an inactive Principal in the picker only when already
   linked, so a later page save keeps it" (16 October 2026). Owner:
   `IContactDirectoryAdministration.SaveAsync` and the
   [Contact page](../frd/frd-17-administration-workspace.md#contacts).
6. **Inspection mode.** "Image Based Assessment for the five at ≥95 %: PCH,
   AX, CASTLE, SBL, ALS" (15 October 2026); "`SaveAsync` honours
   `InspectionMode` on an existing Principal and the Contact page posts the
   current mode (one page change), so an ordinary save cannot flip QDOS to
   Physical; the loader sets modes through the same save; no new operation,
   no SQL step" (16 October 2026). Owner:
   `IContactDirectoryAdministration.SaveAsync`; Case creation reads the mode
   through `IPrincipalInspectionModeStore`
   ([FRD-06](../frd/frd-06-vehicle-and-engineering-evidence.md#inspection-address)).
7. **Inspection locations.** "Load inspection locations (1,391)" and "Match
   to repairer/storage contacts by name and postcode; the remainder in a new
   `InspectionLocations` reference table read by the existing `Directory`
   source kind; new ADR supersedes the ADR-0018 deferral" (15 October 2026).
   Owner: `InspectionLocationSourceKind.Directory`
   ([FRD-06](../frd/frd-06-vehicle-and-engineering-evidence.md#inspection-address)).
8. **Hygiene.** "Normalise and keep the first valid value; report the rest;
   no validation change" (15 October 2026). Owner:
   `ContactDirectoryPolicy.Normalize`.

## Consequences

- The PRD's permanent boundaries, FRD-04, FRD-06 and `reference/README.md`
  name the reviewed register as the accepted data contract, and the DATA-02
  label reads "from the reviewed register".
- No register row reaches production until the operator authorises the
  production run.
- ADR-0018 lists this record in `superseded_by`, and its index row names the
  DATA-02 deferral and decision 5 as the replaced scope.

## Links

- [FRD-04 — Contacts administration](../frd/frd-04-parties-accounts-and-access.md#contacts-administration)
- [FRD-06 — Inspection address](../frd/frd-06-vehicle-and-engineering-evidence.md#inspection-address)
- [FRD-17 — Contacts](../frd/frd-17-administration-workspace.md#contacts)
- [PRD — Permanent boundaries](../prd/pegasus-product.md#permanent-boundaries)
- [Reference evidence](../../reference/README.md)
- [ADR-0018](0018-provider-inspection-mode-database-setting.md)
