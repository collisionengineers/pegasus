# Open questions — TICK-061

- [x] Put provider controls in PLAT-028's Principal administration surface. — Operator decision.
- [x] Pause blocks new submissions but permits authenticated reads of prior receipts/results; revocation invalidates authentication. — Operator decision.
- [x] Reset immediately invalidates the previous secret and shows the replacement once. — Settled lifecycle.
- [x] Use one credential per Principal and persist only a framework verifier in existing Azure SQL. — Simplicity/one-way secret design.
- [x] Retain ADR-0004; do not allocate a new ADR. — Existing decision already covers the boundary.
- [x] Compose authentication only with TICK-058's real endpoint. — Real-caller rail.

## Parked (explicitly deferred)

- [ ] Select the public credential presentation (for example Basic versus a dedicated header). Deferred to TICK-058/FRD-09's complete wire contract.
- [ ] Support overlapping credentials. Deferred until one Principal has two concrete deployed callers.
- [ ] Issue a live credential. Deferred to exact-target approval.
