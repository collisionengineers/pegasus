# Open questions

No operator-only question blocks research. The operator has selected automatic generation when all required assessment details are accepted, immutable version/hash/custody, idempotent replay, append-only correction versions, human approval before issue, and no separate renderer runtime.

- [x] **What human-readable reference appears on a generated report?** — Resolved by the operator on 2026-08-19: the report reference is the existing Case/PO number (`OurReference`). Do not create a separate outward report-number sequence. Pegasus retains a separate internal immutable report/version identity only for generation idempotency, custody, provenance, corrections, approvals, and Sent-evidence linkage.

The implementation plan must not be written until these evidence prerequisites merge and are re-read:

- [[TICK-093]] — accepted versioned canonical repair specification.
- [[TICK-094]] — accepted Engineer-decision component.
- [[TICK-092]] — one consistent accepted report-input snapshot/query and deterministic payload hash.

## Parked (explicitly deferred)

- Version-specific preservation of final Sent evidence through correction belongs to [[TICK-208]].
- Addendum identity/presentation belongs to [[TICK-100]].
- Deployment scheduling, runtime health and Azure proof belong to [[PLAT-007]].
- Audit-specific process inputs remain owned by RPT-03, but Audit reuses the same physical inspection/assessment report template.
- Diminution and other unsupported renderer families remain with their owning capabilities.
