## Backfill plan (VERIFY2, 2026-08-20)

No implementation is planned. This ticket's capability (CASE-13, CASE-14, CASE-16, UI-02) was already built and deployed under prior tickets/PRs before this verification pass. The plan is the verification itself, recorded in `research.md`:

1. Locate the Core policy owner and confirm it enforces the completeness rule (staff cannot confirm what Core has not independently determined complete).
2. Locate the real Web caller and confirm it is wired end-to-end to the same Core policy and persistence.
3. Confirm the three queues read live application data (already established for this run in prod-diagnostics).
4. Confirm file presence at the exact production release ancestor (2325ed4a).
5. Run a read-only SQL check against production to state the actual live-exercise evidence honestly, gap included.

Simplification pass: n/a — docs-only backfill, no diff.
