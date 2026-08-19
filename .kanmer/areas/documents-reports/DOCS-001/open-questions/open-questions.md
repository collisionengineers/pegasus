# Open questions

No operator-only question blocks research. The operator has already selected automatic generation when all required assessment details are accepted, immutable version/reference/hash/custody, idempotent replay, append-only correction versions, human approval before issue, and no separate renderer runtime.

The implementation plan must not be written until the following evidence prerequisites merge and are re-read:

- [[TICK-093]] — accepted versioned canonical repair specification (currently Implementing, unmerged).
- [[TICK-094]] — accepted Engineer-decision component (currently Preparing and dependent on TICK-093).
- [[TICK-092]] — one consistent accepted report-input snapshot/query and deterministic payload hash (currently Preparing and blocked by both).

These are structured technical blockers, not questions for the operator.

## Parked (explicitly deferred)

- The outward/human-readable report-reference format is not defined by current operator truth. DOCS-001 needs a stable internal immutable report identity/version and retains the existing Case `OurReference` in rendered content; do not invent a new externally meaningful numbering scheme. If a later workflow requires one, route that product choice separately.
- Version-specific preservation of final Sent evidence through correction belongs to [[TICK-208]].
- Addendum identity/presentation belongs to [[TICK-100]].
- Deployment scheduling, runtime health and Azure proof belong to [[PLAT-007]].
- Audit, diminution and unsupported renderer families remain unavailable.
