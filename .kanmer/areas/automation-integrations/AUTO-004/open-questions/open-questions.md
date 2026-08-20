# Open questions — AUTO-004

No unresolved question blocks planning. ADR-0011, ADR-0021, the existing Core ports, and the operator direction establish ordinary-casework parity.

## Parked (explicitly deferred)

- [x] **Should AUTO-004 include Triage?** — Yes. [[AUTO-005]] is in the same implementation unit/worktree; use a separate typed Triage tool class over the same Core owners.
- [x] **Can Automation use staff “Assign to me” semantics?** — No. It never impersonates a staff GUID; excluding that identity-specific UI action preserves rather than weakens the accepted actor boundary.
- [x] **Should AUTO-004 absorb classified-mail parity?** — No. [[AUTO-003]] already owns that separately dependency-bound surface.
