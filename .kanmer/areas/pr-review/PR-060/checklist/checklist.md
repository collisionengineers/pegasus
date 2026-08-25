# Checklist — PR-060

- [x] Correct only the migration comment block to describe operation-keyed `ActionHistory`, the once-per-case proxy, and ADR-0030 roll-forward recovery.
- [x] Confirm ADR-0030, PR #539 and the ENG-016 report contain no conflicting recovery/replay statement; edit no additional surface unless a concrete contradiction remains.
- [x] Run `git diff --check`, prove migration operations/generated files are unchanged, and write the comment-only post-implementation report.

## Progress notes

Planning complete on 2026-08-25. No schema, runtime, compatibility, rollback, data-clearing, or test change is authorized.


2026-08-25: implementation and focused verification completed at c86b803c. Simplification lenses: reused existing lock and batch-read conventions; removed obsolete switches and catch/retry path; no new abstraction; no deferred code finding.
