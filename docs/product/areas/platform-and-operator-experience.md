# Platform and operator experience

## Outcome

Pegasus provides a restrained, accessible Windows-operated staff application
and directly controlled Azure release path with observable, recoverable
behavior. UI, source incorporation, caller, deployment and acceptance evidence
remain separate.

## Settled requirements

- The internal staff experience supports Operations, Intake, Triage, Cases and
  authorized Administration with keyboard operation, visible focus, semantic
  structure, associated errors, practical targets, forced colours and reduced
  motion.
- Mobile staff UI is not planned. Constrained-width/zoom reflow does not create
  a mobile product.
- Operations-first is selected for the first shell and landing strategy.
- Windows and PowerShell 7 own repository/release operations. GitHub Actions
  validates but does not deploy.
- Azure release requires explicit approval, immutable artifacts,
  migration/health/smoke evidence, telemetry and tested recovery; dated
  inventory is not live proof.
- Separate staging/UAT/training environments, deployment slots/S1, private
  networking, zone/multi-region resilience and quarterly restore exercises are
  not planned.

## Workload and capacity

Current workload is 1,000–1,200 jobs per month. The design-capacity target is
2,000 new cases per month for approximately eight concurrent operational staff.
Alex is the developer and an Administrator, not a ninth operational caseworker.
The staff roster is evidence only and does not define authorization.

The intended operating outcome is one administrator monitoring exceptions
rather than two or three people rekeying routine work, while Engineers spend
time on judgement rather than filing. This is a product objective, not evidence
that automation or staffing change has been implemented.

## Engineer workbench

`UI-15` arranges one case-centred progressive workbench for:

1. inspection details, contact/address, circumstances, instructions and notes;
2. vehicle identity/specification, condition, modifications, history and
   roadworthiness;
3. damage zones/severity, tyres/belts, unrelated damage and material transfer;
4. versioned estimate lines, totals and repairer comparison;
5. accepted valuation, adjustments and report-summary inputs;
6. photos, screenshots, documents and custody;
7. salvage, recovery, storage, owner retention and movement;
8. report text; and
9. report, fee note, authority, total-loss, salvage-letter and notification
   actions.

The workbench preserves typed provenance and versions without copying EVA's
12-step/multi-tab navigation. It owns arrangement only; case, document,
`ENG-*`, `RPT-*`, `EXT-*`, `MAIL-*` and accounting capabilities retain their
data, calculations, decisions and effects. `UI-15` remains `Later`/unallocated.

## Current state and activation

The Development UI is a narrow intake proof and differs from the approved
design foundation. Azure IaC is target design, not live/deployed evidence.
Imported workspaces and planned sections are not UI callers or disabled
placeholders. Each UI/platform slice requires a real caller or release route,
proportional evidence and current owner updates.
