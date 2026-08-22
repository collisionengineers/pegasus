# Checklist

- [x] Confirm the Repairable/Total Loss outcome is known at allocation — operator confirmed, and acceptance already refuses an audit without its retained report evidence
- [x] Put the `a.`/`ap.` prefix on the case's own reference in `EfCaseAcceptanceStore`
- [x] Stop allocating a second `AuditReference` for an audit
- [x] Stop issuing an audit folder-creation token for an audit
- [x] Name the Box root by the case reference for every case, closing the audit/custody split
- [x] Leave a later Audit reference on a non-audit case untouched
- [x] Build clean; 916 Core and 99 architecture tests pass
- [ ] Live: a new audit shows one reference in the app and one matching Box folder

## Progress notes

**2026-08-22** — implemented in `79bf3f86`. `AuditIdentity.Create` needed no change; it was
producing the right string all along, applied to the wrong thing.

Two compiler errors caught real leftovers rather than typos: a `const string? auditReference
= null` made an `is null` test always-true (CS8520), and removing `isAuditCase` left the
audit-folder branch referring to it. Both were the old two-identity model still showing
through, and both are now gone.

The final item needs a real audit through the live pipeline and is Phase 6.
