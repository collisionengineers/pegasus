## Backfill plan (VERIFY2, 2026-08-20)

No implementation is planned. The cutover route and rollback procedure were already documented and, for the Web host, actually exercised in production before this ticket was worked. The plan is the verification itself (see `research.md`):

1. Locate the cutover (build-once/deploy-same-artifact) route in the runbook and confirm it matches the actual release history (13 releases, retained artifacts).
2. Locate the rollback procedure for both Web and Worker.
3. Find whether a rollback was ever actually exercised in production, not just documented — confirmed via `docs/operations.md`'s real 2026-08-18 revision-rollback record.
4. Name the one still-open residual in the runbook honestly (ADR-0024 per-mailbox Worker-control contract) without letting it block the cutover/rollback procedure's own completeness.

Simplification pass: n/a — docs-only backfill, no diff.
