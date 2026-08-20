# Open questions — AUTO-005

No unresolved question blocks the combined AUTO-004/AUTO-005 implementation. ADR-0011, ADR-0021, FRD-03, and the operator direction establish ordinary-casework parity.

## Parked (explicitly deferred)

- [x] **How is Triage assignment handled?** — [[INTK-019]] retires actor-relative “Assign to me” and replaces it with explicit named-Engineer selection. That shared contract is outside this PR; it must preserve distinct acting-principal and assignee identities.
