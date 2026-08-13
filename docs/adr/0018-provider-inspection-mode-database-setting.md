---
id: ADR-0018
status: accepted
date: 2026-08-03
supersedes: []
superseded_by: []
related_capabilities: []
related_frd: [frd-02, frd-06]
tags: [intake, config]
---
# ADR-0018: Provider-determined inspection mode as a database setting

- Date: 2026-08-03
- Status: accepted
- Owners: Collision Engineers product owner and Pegasus development team
- Relation: scoped exception to ADR-0008's code-owned-configuration consequence
  for one provider attribute; ADR-0008's route-selection predicates remain
  code-owned and unaffected

## Context

The inspection-address flow assumed the mode (physical vehicle/repairer
address versus the exact value `Image Based Assessment`) could be derived from
instruction-document text: the exact literal had to be extracted from the
document, staff could not enter it as an intake correction, and intake
acceptance was blocked until an address was resolved from document evidence.

The product owner clarified on 2026-08-03 that instruction documents never
contain the literal text. The mode is a property of the work provider: some
providers always use `Image Based Assessment`; others require the vehicle's
physical location on the report. The historical evidence workbook
`docs/reference/workproviders-and-repairers/providers-worked-on.xlsx`
(sheet `Final`, "Principal Inspection Address Frequency") records QDOS at
7,408 of 7,415 cases image-based, with PCH and AX similarly image-based and
QCL predominantly physical. Under the document-derived assumption, a genuine
QDOS instruction could not be accepted into a case without staff entering a
physical address that the provider does not use.

Two homes for the provider→mode mapping were rejected:

- The provider-domain reference package: its authoring contract
  (operations § provider-domain reference authoring) forbids emitting an
  inspection location or default, and the package remains domain evidence
  only. The requirements statement that the package contains no address or
  address-mode default stays true.
- The code-owned route-policy catalog: the product owner directed on
  2026-08-03 that the setting be operable without a code change and deploy.

## Decision

1. Each Principal carries a persisted inspection-mode setting:
   `physical_address` or `image_based_assessment`. It lives on the
   `Principals` table and defaults to `physical_address`.
2. QDOS is seeded `image_based_assessment` by migration, citing the evidence
   workbook above.
3. At case creation, a Principal whose setting is `image_based_assessment`
   has the exact value `Image Based Assessment` autofilled as the confirmed
   inspection address and mode, even when a physical location appears in the
   instruction. The autofill records provider-setting provenance
   (policy `provider-inspection-mode` v1) and a permanent case-history event.
4. Authorised staff may override the autofilled mode on a specific case to a
   physical address — and back — through the versioned SaveCase command with
   an attributed reason; the override records staff-correction provenance.
5. For a Principal whose setting is `physical_address`, the existing
   evidence-first flow is unchanged: the address is extracted from the
   instruction and staff accept or correct it before acceptance.
6. Intake acceptance no longer requires an inspection-address resolution for
   an image-based Principal. The intake rule that a staff correction may
   never be the literal `Image Based Assessment` remains: intake resolution
   is exclusively the physical-address pathway.
7. The acceptance command material includes the resolved provider mode
   (material schema version 4), so idempotent replays detect a setting change
   between the original acceptance and a replay and fail closed.
8. A dedicated post-creation administration operation for changing an
   existing Principal's mode is deferred; principal creation and replacement
   carry the setting, and a production change is an operations runbook action
   until an admin operation is separately justified.
9. The abbreviation "IBA" is not a staff-facing term. Staff-facing UI and
   documentation always use the full phrase `Image Based Assessment`.

## Consequences

- `Principals` gains an `InspectionMode` column with a check constraint over
  the two mode codes already used by case data
  (`physical_address`, `image_based_assessment`).
- Case data snapshots gain the `provider_setting` source kind so autofilled
  values are distinguishable from intake evidence and staff corrections; EVA
  handoff treats them as accepted evidence.
- The exact-extraction branch that recognised the literal in document text
  becomes vestigial for real traffic; it is retained for fail-closed
  compatibility, not as a product pathway.
- Replays of acceptances recorded under material schema version 3 conflict
  instead of deduplicating after this change deploys; the window is a
  double-submit across the deployment, consistent with prior material
  version bumps.
- ADR-0008's consequence that provider configuration is "code-owned by the
  route-policy catalog, not database-authored configuration" continues to
  govern route selection. This decision adds one database-authored provider
  attribute that governs case-data defaulting only; it selects no route,
  policy, or provider identity.

## Deferred-capability impact

Only QDOS is an activated Principal, so the seed covers the alpha scope.
Activating further providers (INT-04) sets each new Principal's mode at
creation from separately accepted provider evidence. DATA-02's
inspection-address reference-data pipeline remains deferred and is unaffected:
this decision selects a mode, never an address.
