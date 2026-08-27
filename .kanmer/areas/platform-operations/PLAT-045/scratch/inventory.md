## Dry-run inventory — 2026-08-27 08:55Z, before release 34

Script: `artifacts/plat-045-wipe/Invoke-Plat045Wipe.ps1` (ignored path),
Entra token, read-only without `-Execute`.

- Tables total 97; preserve list 31/31 found; effective preserved 32
  (extra: `ApprovedMailboxSubscriptions`, MAIL-013 Graph subscription state).
- Tables to wipe 65; rows to delete 623 (largest: IntakeAssets 215,
  CaseDataFields 68, CaseDocuments/DocumentOccurrences/DocumentVersions 34
  each, IntakeSearchDocuments 22, IntakeReceiptEvents 18,
  RetainedMailboxAttachments 16, CaseHistory 15, ExternalWorkItems 11).
- `CaseSequences.LastAllocatedSequence` = 23 → next case QDOS26024;
  ImageIntakeSequences 6 rows, UnidentifiedSequences 1 row — preserved.

## Blob and queue inventory — 2026-08-27 08:57Z

- `pegcustody252ow37gij` containers: `authentication-ring`, `box-links`,
  `transient-intake`. `transient-intake` lists **0 blobs** already (the
  release-33 intake path retains nothing there) — nothing to delete; will be
  re-listed after the SQL wipe.
- `pegtrans252ow37gij` queues `intake-work`, `intake-work-poison`,
  `external-work`, `external-work-poison`: peek returns **0 messages** each.
