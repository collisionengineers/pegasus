## Backfill plan (VERIFY2, 2026-08-20)

No implementation is planned. AI-09 was already implemented under ADR-0021 before this ticket was worked. The plan is the verification itself (see `research.md`):

1. Compare the capability text clause-by-clause against the code (contract, transport, tests).
2. Determine the production gate state by inspecting `Features:SendToAi` in Core/Web and confirming it is absent from `infra/modules/platform.bicep`.
3. Confirm the fail-closed behavior outside `DevelopmentOffline` is a deliberate coded boundary, not an oversight.
4. Record what a future production-activation ticket would need, without actioning it (a business/architecture decision on transport is out of this lane's scope).
5. Stop the pipeline walk at `review` — a closed composition gate is not delivered, so `verifying`/`done` (which imply live production evidence) are not honestly reachable.

Simplification pass: n/a — docs-only backfill, no diff.
