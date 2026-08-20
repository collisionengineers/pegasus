## Backfill plan (VERIFY2, 2026-08-20)

No implementation is planned. EVAL-02 was already implemented under ADR-0016 before this ticket was worked, in `scripts/email-eval-desktop/`, not in the Pegasus web app. The plan is the verification itself (see `research.md`):

1. Correct the mapping — confirm via `docs/capabilities.md`'s own row and ADR-0016 that EVAL-02 belongs to the standalone desktop evaluator, not the in-app mail classification panel.
2. Confirm the taxonomy is exactly 8 Received + 4 Sent categories, structurally enforced.
3. Confirm reasoning is required and validated before filing.
4. Confirm test coverage exists for both.

Simplification pass: n/a — docs-only backfill, no diff.
