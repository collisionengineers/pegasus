# Post-implementation report — ENG-026

## Delivered

Named estimates on a Case with per-estimate VAT and a Current estimate
(wave 3, EPIC-011):

- Core `Assessment/Estimates.cs`: EstimateDetails/EstimateTotals/
  EstimateOperations/EstimatePolicy and the Save/Duplicate/Discard/
  SetCurrent/List use cases; `EstimateTotals.Compute` is the single totals
  owner (D9: free VAT % per estimate; the Current estimate's VAT overrides
  the report's built-in repairer-VAT rule, which now applies only when no
  Current estimate exists).
- `RepairSpecificationVersion` gains Details, IsCurrent, AiJobId,
  DiscardReason, state Discarded, routes Json/AiDraft; lines gain
  PaintWorkUnits and Quantity; line-op mapping Replace/Repair/R&I/Paint/
  Other ↔ EstimateLineCodes has one owner.
- Persistence: EfRepairSpecificationStore estimate operations with the
  one-Current-per-case filtered unique index; migration
  `20260828112103_NamedEstimates` (+ snapshot + census).
- JsonEstimateParser beside the Audatex parser; registered concrete
  singleton.
- MCP `pegasus_estimate_save` / `pegasus_estimate_list` under
  `automation.assessment` (AiDraft route only, job id required).
- Tests: Core totals/policy, integration store, MCP ingress (inventory
  updated to 43 tools), JSON parser (11 rejection cases with unique
  display names).

## Verification record

- Implementer build green (a0daecd9 tree); orchestrator wave loop
  2026-08-28 on head `1edc7b70`: restore exit 0, build exit 0, Core
  1119/1119, Architecture 100/100, Integration 1010/1010 exit 0,
  `Test-MigrationGrants.ps1` 82 files clean. CI all green after two
  DELIV-031-class shard reruns (runner cancellations, no test failures);
  merged to dev as PR #595 at 2026-08-28T18:52Z.
- NamedEstimates creates no table and carries no grant, so no bootstrap
  census entry is required (reasoning recorded in the migration).

## Deviations and follow-ups

- Wave-4 seam: Duplicate/Discard/SetCurrent/Import have no production
  caller yet (ENG-028 wires the estimate editor and import dialog); Save/
  List are reachable via MCP. Not claimed as an operator-visible
  capability until then.
- Duplicate name rule ("<name> copy", 100-char truncation) composes in
  the Infrastructure store from Core-owned components — accepted at
  review; fold into EstimatePolicy only if a second caller appears.
- CI fixes 9c0d9181 (tool inventory) and c97889f1 (Theory display names)
  post-date the original simplification pass; both recorded in the plan.
