# Checklist — DOCS-006

- [x] `InstructionEvidenceImages` selection rule + unit facts (3/3)
- [x] Custody promotion of embedded photos (ordinals, op keys, replay)
- [x] `DownloadIntakeAsset` + `/Received/{id}/Asset/{assetId}` endpoint (+ web fact: inline image, pdf 404, foreign 404, anonymous challenged)
- [x] `ListEvidenceImagesForCaseAsync` + Evidence-tab gallery + count
- [x] Corpus end-to-end custody fact (EREF9: ≥5 photos beside the source, letterhead excluded, download path verified; skip-if-absent)
- [x] Simplification pass recorded in plan (single query; generalized gallery partial; shared hash comparer)
- [ ] Release build 0/0; custody + case web + image suites green (running)
- [ ] PR to dev; merge on green CI (serialized with MAIL-006's PR)

## Progress notes

2026-08-21: implementation complete on the branch; corpus fact needed the
receipt's *current* stored version at acceptance (the real email's processing
advances it where synthetic fixtures do not).
