## Independent review — PR #462 (orchestrator, 2026-08-20)

Verdict: **pass**, merge on green.

- Correct pipeline reuse: two new `ExternalWorkKinds` (`create_image_case_custody`, `merge_image_case_custody`) processed by the SAME queued custody processor — no parallel pipeline. Registration enqueues creation; the pairing/merge path enqueues the move+fold.
- The Box operations stay fenced under the approved root: ancestry verification mirrors the case-root pattern, and `DeleteFolderAsync` fires only on the emptied image-case folder after its contents moved into the paired case's Evidence folder (children listed + binding file handled first). Failure retries follow a bounded `ImageCustodyRetryPolicy`; Pegasus blob custody stays authoritative so a Box outage never loses images or blocks registration/merge.
- Custody remote ids land as nullable columns on ImageIntakes (migration `20260820055900_ImageCaseCustody`; existing granted table, no census change needed — Test-MigrationGrants green per lane).
- LocalCaseCustody grew the same operations so local/integration testing never touches real Box; no live Box calls were made from the lane.
