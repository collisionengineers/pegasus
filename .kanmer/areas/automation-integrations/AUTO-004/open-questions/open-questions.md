# Open questions — AUTO-004

No unresolved question blocks planning. ADR-0011, ADR-0021, the existing Core ports, and the operator direction establish ordinary-casework parity.

## Parked (explicitly deferred)

- [x] **Should AUTO-004 include Triage?** — Yes. [[AUTO-005]] is in the same implementation unit/worktree; use a separate typed Triage tool class over the same Core owners.
- [x] **How is Triage assignment handled?** — [[INTK-019]] retires “Assign to me” in favour of explicit named-Engineer selection. This PR does not implement the replacement assignment contract; when it lands, staff and Automation must share it while actor and assignee identities remain distinct.
- [x] **Should AUTO-004 absorb classified-mail parity?** — No. [[AUTO-003]] already owns that separately dependency-bound surface.
