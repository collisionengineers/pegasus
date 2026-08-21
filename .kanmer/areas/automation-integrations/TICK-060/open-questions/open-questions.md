# Open questions — TICK-060

- [x] A provider result lookup is scoped to the authenticated Principal's own API-01 submission receipt; it is not a general Case/PO lookup. — Operator clarification, 2026-08-21.
- [x] Success requires an actual active Case link supplying immutable Case/PO. — Operator clarification and product invariant.
- [x] If processing completes without creating or linking a Case, return terminal failure. Do not leave the provider polling. — Operator clarification.
- [x] Unknown, random, and cross-Principal identifiers return indistinguishable absence. — Fail-closed isolation.
- [x] Return identifiers only; no files, reports, source material, general Case detail, or outbound delivery. — Operator clarification and documented contract separation.
- [x] Expose no transient Processing state; unfinished work receives only a generic nonterminal response. — Operator decision, 2026-08-21.

## Parked (explicitly deferred)

- [ ] Select exact routes, headers, response schema, error codes, retry hints, limits, and throttling. Deferred to the unresolved provider wire-contract decision; these must not be inferred as planning defaults.
- [ ] Add push/webhook completion. Deferred until a real second caller proves it necessary.
