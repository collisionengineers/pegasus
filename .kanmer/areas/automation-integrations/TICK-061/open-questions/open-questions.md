# Open questions — TICK-061

- [x] Put provider credential controls in PLAT-028's redesigned Principal administration surface. — Operator decision, 2026-08-21.
- [x] Pause blocks new submissions but permits authenticated reads of prior receipts/results. — Operator decision.
- [x] Reset immediately invalidates the previous secret and shows the replacement once. — Simplicity default accepted in the plan.
- [x] Revocation invalidates authentication until an Administrator generates a fresh credential. — Required lifecycle distinction.
- [x] Use one credential per Principal in v1. — No second concrete caller.
- [x] Generate a 32-byte cryptographically random base64url secret and store only ASP.NET Core PasswordHasher output. — Reuses framework one-way secret handling.

## Parked (explicitly deferred)

- [ ] Support overlapping credentials for zero-downtime rotation. Deferred until a real Principal has two independently deployed callers.
- [ ] Issue credentials to a live provider. Deferred because external activation requires exact-target approval.
