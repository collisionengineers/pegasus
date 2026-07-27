# Patterns

| Pattern/journey | User goal | Steps and states | Components | Runtime owner |
| --- | --- | --- | --- | --- |
| Development manual intake review | retain and inspect one local source safely | upload -> fail-closed processing -> persisted Draft ready/Needs sorting/OCR required/Unsupported/retryable failure -> queue/review/download | upload form, queue, review | `src/CollisionSpike.Web/Pages/Intake/` -> Core `ProcessIntake` |

The planned authenticated Intake, Triage, Case, Operations, and Administration
journeys remain in `docs/plans/ui-ux/ui-spec.md`. Their shell direction and
runtime callers are not approved/implemented, so they are not duplicated here.
