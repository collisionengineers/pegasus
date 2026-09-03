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

## Parked (explicitly deferred)

None.
