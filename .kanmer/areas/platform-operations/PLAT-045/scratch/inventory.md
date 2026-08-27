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
