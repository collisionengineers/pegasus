# Open questions — TICK-060

- [x] Expose no transient Processing state. — Operator decision, 2026-08-21.
- [x] Map a known nonterminal receipt to HTTP 202 with `Retry-After: 2` and the receipt identifier only. — Minimal polling default.
- [x] Return HTTP 200 only when an actual Case link supplies immutable Case/PO; never infer it from a processing decision. — Product invariant.
- [x] Return a stable terminal failure code without internal exception text. — Existing bounded-failure convention.
- [x] Return the same 404 for unknown and cross-principal receipts. — Fail-closed isolation.

## Parked (explicitly deferred)

- [ ] Add push/webhook completion. Deferred until a real second caller proves polling inadequate.
- [ ] Choose live rate limits and retention SLA. Deferred to named-provider activation.
