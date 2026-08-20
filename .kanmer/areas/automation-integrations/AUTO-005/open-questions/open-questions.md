# Open questions — AUTO-005

No unresolved question blocks the combined AUTO-004/AUTO-005 implementation. ADR-0011, ADR-0021, FRD-03, and the operator direction establish ordinary-casework parity.

## Parked (explicitly deferred)

- [x] **Can Automation assign Triage “to me” or choose an arbitrary staff assignee?** — No. The actor is not a staff GUID and never impersonates staff; an arbitrary-assignee API would be broader than the Web caller.
