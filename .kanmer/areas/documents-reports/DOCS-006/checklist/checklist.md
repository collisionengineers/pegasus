# Checklist — DOCS-006

- [x] `InstructionEvidenceImages` selection rule + unit facts (3/3)
- [x] Custody promotion of embedded photos (ordinals, op keys, replay)
- [x] `DownloadIntakeAsset` + `/Received/{id}/Asset/{assetId}` endpoint (+ web fact: inline image, pdf 404, foreign 404, anonymous challenged)
- [x] `ListEvidenceImagesForCaseAsync` + Evidence-tab gallery + count
- [x] Corpus end-to-end custody fact (EREF9: ≥5 photos beside the source, letterhead excluded, download path verified; skip-if-absent)
- [x] Simplification pass recorded in plan (single query; generalized gallery partial; shared hash comparer)
- [x] Release build 0/0; custody + case web + image suites green
- [x] PR to dev; merged on green CI — shipped and deployed to production

## Progress notes

2026-08-21: implementation complete on the branch; corpus fact needed the
receipt's *current* stored version at acceptance (the real email's processing
advances it where synthetic fixtures do not).

2026-08-22: the last two boxes were stale bookkeeping — the PR merged and the
work has been on production since release 16. Ticked against the deployed
evidence rather than left open. Extraction and retention are now also observed on
a real instruction: QDOS26010 retained 20 embedded images across four pages,
including nine 709×768 damage photographs. See proof.
