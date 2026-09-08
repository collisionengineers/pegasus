# Open questions — ENG-031 (2026-09-02)

- [x] Which durable event defines the report version that snapshots curation?
  Resolved 2026-09-03 by the controller: report approval
  (`CaseReportApprovals`) snapshots the curation; detected sent evidence is a
  later fact about the same approved report.
- [x] What durable disposition identifies an image with a person's
  reflection? Resolved 2026-09-03 by the controller: the `Not used` role is
  the disposition; no persisted reflection marker (no abstraction without a
  second caller). FRD-06's "continues to exclude" is satisfied by the role.

Amendment (operator, 2026-09-03, D46): the crop tool must behave like any
photo-editing cropper (drag the frame, resize by handles, rotate, aspect
lock, reset, live preview) and be reachable from the Files section's image
viewer as well as from the Report section's image cards, without first
pressing Edit Case; saving a crop starts the edit lease. One curation record
per image whichever entry point is used.

## Current v1 dispositions — 2026-09-08

The resolved Q1 attribution above remains historical. The accepted
generation-freeze implementation now owns the immutable source/curation
snapshot instead of CaseReportApprovals; no second approval entity is planned.
Q2 and D46 remain binding.

- [x] Global Save/Discard must include image edits. Root explicitly retained
  this current design/Astra requirement; reuse the existing workspace
  transaction and PrepareSaveAsync, not an undo store or immediate commit.
- [x] Lazy lease acquisition may reuse native ClaimLease and rerender staged
  fields. Root approved provided invalid/stale acquisition preserves original
  values/Case/preparation versions and never persists or silently refreshes
  retry authority.
- [ ] Root full-read approval of current executable plan and exact map.
- [ ] ENG-029 shared source/fixtures, INTK-064 DI and root snapshot/index
  ownership released or precisely handed off; stale ENG-034 blocking relation
  disposed on its actual integrated-host evidence without forcing old claim.

## Parked (explicitly deferred)

None.
