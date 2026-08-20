## Backfill plan (VERIFY2, 2026-08-20)

No implementation is planned. CASE-17/18/19/20 and MAIL-18 were already built and deployed under prior tickets before this verification pass. The plan is the verification itself (see `research.md`):

1. Confirm the seven-day chase rule's exact wording in Core policy.
2. Confirm the staff-facing copyable-chaser caller and that it never claims a message was sent.
3. Confirm the Worker timer that runs the sweep, its schedule, and that it is enabled in production.
4. Confirm file presence at the exact production release ancestor (2325ed4a).
5. Trace and resolve the `blocked` label's origin (TICK-116 → TICK-112 → BUG-001) against current production evidence.
6. Run read-only SQL against production to state the actual live-exercise evidence honestly.

Simplification pass: n/a — docs-only backfill, no diff.
