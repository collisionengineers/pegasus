# Shared context

NOW.md and docs/requirements.md were retired by SIMPLI-004/SIMPLI-006. This horizon removes stale authority references from the live board without deleting real work.

## Binding constraints

- AGENTS.md owns repository workflow and declares Kanmer the canonical work queue; do not invent a PRD, FRD, or ADR for process hygiene.
- Operator decisions recorded 2026-08-14: the hold is lifted; Done and archived tickets are in scope; renderer tickets TICK-203 through TICK-216 are preserved and consolidated through SIMPLI-015.
- Fresh audit 2026-08-17: TICK-194, TICK-195, TICK-197, and TICK-200 now carry substantive research or EPIC-001 membership and must be preserved, not blindly archived.
- Use optimistic concurrency for live ticket-body updates.
